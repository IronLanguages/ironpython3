// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

using IronPython.Runtime;

using Microsoft.Scripting;
using Microsoft.Scripting.Utils;

using AstUtils = Microsoft.Scripting.Ast.Utils;
using LightLambdaExpression = Microsoft.Scripting.Ast.LightLambdaExpression;
using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class ClassDefinition : ScopeStatement {
        private readonly string _name;
        private readonly Expression[] _bases;
        private readonly Arg[] _keywords;

        private LightLambdaExpression _dlrBody;       // the transformed body including all of our initialization, etc...

        private static int _classId;

        private static readonly MSAst.ParameterExpression _parentContextParam = Ast.Parameter(typeof(CodeContext), "$parentContext");
        private static readonly MSAst.ParameterExpression _lookupVariableParam = Ast.Parameter(typeof(object), "$classLookupVar");
        private static readonly MSAst.Expression _tupleExpression = MSAst.Expression.Call(AstMethods.GetClosureTupleFromContext, _parentContextParam);

        public ClassDefinition(string name, Expression[] bases, Arg[] keywords, Statement body = null) {
            ContractUtils.RequiresNotNullItems(bases, nameof(bases));
            ContractUtils.RequiresNotNullItems(keywords, nameof(keywords));

            _name = name;
            _bases = bases;
            _keywords = keywords;
            Body = body;
            Metaclass = keywords.Where(arg => arg.Name == "metaclass").Select(arg => arg.Expression).FirstOrDefault();
        }

        public SourceLocation Header => GlobalParent.IndexToLocation(HeaderIndex);

        public int HeaderIndex { get; set; }

        public override string Name => _name;

        public IReadOnlyList<Expression> Bases => _bases;

        public IReadOnlyList<Arg> Keywords => _keywords;

        public Expression Metaclass { get; }

        public Statement Body { get; set; }

        public IList<Expression> Decorators { get; internal set; }

        /// <summary>
        /// Variable corresponding to the class name
        /// </summary>
        internal PythonVariable PythonVariable { get; set; }

        /// <summary>
        /// Variable for the the __module__ (module name)
        /// </summary>
        internal PythonVariable ModVariable { get; set; }

        /// <summary>
        /// Variable for the __doc__ attribute
        /// </summary>
        internal PythonVariable DocVariable { get; set; }

        /// <summary>
        /// Variable for the module's __name__
        /// </summary>
        internal PythonVariable ModuleNameVariable { get; set; }

        private PythonVariable ClassCellVariable { get; set; }
        private PythonVariable ClassVariable { get; set; }

        internal override bool HasLateBoundVariableSets {
            get {
                return base.HasLateBoundVariableSets || NeedsLocalsDictionary;
            }
            set {
                base.HasLateBoundVariableSets = value;
            }
        }

        internal override bool ExposesLocalVariable(PythonVariable variable) => true;

        private bool needClassCell { get; set; }

        internal override bool TryBindOuter(ScopeStatement from, PythonReference reference, out PythonVariable variable) {
            if (reference.Name == "__class__") {
                needClassCell = true;
                ClassCellVariable = EnsureVariable("__classcell__");
                ClassVariable = variable = EnsureVariable(reference.Name);
                variable.AccessedInNestedScope = true;
                from.AddFreeVariable(variable, true);
                for (ScopeStatement scope = from.Parent; scope != this; scope = scope.Parent) {
                    scope.AddFreeVariable(variable, false);
                }

                AddCellVariable(variable);
                return true;
            }
            return base.TryBindOuter(from, reference, out variable);
        }

        internal override PythonVariable BindReference(PythonNameBinder binder, PythonReference reference) {
            PythonVariable variable;

            // Python semantics: The variables bound local in the class
            // scope are accessed by name - the dictionary behavior of classes
            if (TryGetVariable(reference.Name, out variable)) {
                // TODO: This results in doing a dictionary lookup to get/set the local,
                // when it should probably be an uninitialized check / global lookup for gets
                // and a direct set
                if (variable.Kind == VariableKind.Global) {
                    AddReferencedGlobal(reference.Name);
                } else if (variable.Kind == VariableKind.Local) {
                    return null;
                }

                if (variable.Kind != VariableKind.Nonlocal) {
                    return variable;
                }
            }

            // Try to bind in outer scopes, if we have an unqualified exec we need to leave the
            // variables as free for the same reason that locals are accessed by name.
            bool stopAtGlobal = variable?.Kind == VariableKind.Nonlocal;
            for (ScopeStatement parent = Parent; parent != null && !(stopAtGlobal && parent.IsGlobal); parent = parent.Parent) {
                if (parent.TryBindOuter(this, reference, out variable)) {
                    return variable;
                }
            }

            return null;
        }

        internal override Ast LookupVariableExpression(PythonVariable variable) {
            MSAst.Expression lookup = Ast.Call(
                AstMethods.LookupLocalName,
                LocalContext,
                Ast.Constant(variable.Name)
            );
            MSAst.Expression varexp = GetVariableExpression(variable);

            return Ast.Block(
                new[] { _lookupVariableParam },
                Ast.Assign(_lookupVariableParam, lookup),
                Ast.IfThen( Ast.Equal(_lookupVariableParam, Ast.Constant(null)),
                    Ast.Assign(_lookupVariableParam, varexp)
                ),
                _lookupVariableParam
            );
        }

        private static readonly MSAst.Expression NullLambda = AstUtils.Default(typeof(Func<CodeContext, CodeContext>));

        public override MSAst.Expression Reduce() {
            var codeObj = GetOrMakeFunctionCode();
            var funcCode = GlobalParent.Constant(codeObj);
            FuncCodeExpr = funcCode;

            MSAst.Expression lambda;
            if (EmitDebugSymbols) {
                lambda = GetLambda();
            } else {
                lambda = NullLambda;
                ThreadPool.QueueUserWorkItem((x) => {
                    // class defs are almost always run, so start 
                    // compiling the code now so it might be ready
                    // when we actually go and execute it
                    codeObj.UpdateDelegate(PyContext, true);
                });
            }

            MSAst.Expression classDef = Ast.Call(
                AstMethods.MakeClass,
                funcCode,
                lambda,
                Parent.LocalContext,
                AstUtils.Constant(_name),
                Ast.NewArrayInit(
                    typeof(object),
                    ToObjectArray(_bases)
                ),
                Metaclass is null ? AstUtils.Constant(null, typeof(object)) : AstUtils.Convert(Metaclass, typeof(object)),
                AstUtils.Constant(FindSelfNames())
            );

            classDef = AddDecorators(classDef, Decorators);

            return GlobalParent.AddDebugInfoAndVoid(
                AssignValue(Parent.GetVariableExpression(PythonVariable), classDef), 
                new SourceSpan(
                    GlobalParent.IndexToLocation(StartIndex),
                    GlobalParent.IndexToLocation(HeaderIndex)
                )
            );
        }

        private Microsoft.Scripting.Ast.LightExpression<Func<CodeContext, CodeContext>> MakeClassBody() {
            // we always need to create a nested context for class defs            

            var init = new List<MSAst.Expression>();
            var locals = new ReadOnlyCollectionBuilder<MSAst.ParameterExpression>();

            locals.Add(LocalCodeContextVariable);
            locals.Add(PythonAst._globalContext);

            init.Add(Ast.Assign(PythonAst._globalContext, new GetGlobalContextExpression(_parentContextParam)));

            GlobalParent.PrepareScope(locals, init);

            CreateVariables(locals, init);

            var createLocal = CreateLocalContext(_parentContextParam);

            init.Add(Ast.Assign(LocalCodeContextVariable, createLocal));
            // __classcell__ == ClosureCell(__class__)
            if (needClassCell) {
                var exp = (ClosureExpression) GetVariableExpression(ClassVariable);
                MSAst.Expression assignClassCell = AssignValue(GetVariableExpression(ClassCellVariable), exp.ClosureCell);
                init.Add(assignClassCell);
            }

            List<MSAst.Expression> statements = new List<MSAst.Expression>();
            // Create the body
            MSAst.Expression bodyStmt = Body;


            // __module__ = __name__
            MSAst.Expression modStmt = AssignValue(GetVariableExpression(ModVariable), GetVariableExpression(ModuleNameVariable));

            string doc = GetDocumentation(Body);
            if (doc != null) {
                statements.Add(
                    AssignValue(
                        GetVariableExpression(DocVariable),
                        AstUtils.Constant(doc)
                    )
                );
            }

            if (Body.CanThrow && GlobalParent.PyContext.PythonOptions.Frames) {
                bodyStmt = AddFrame(LocalContext, FuncCodeExpr, bodyStmt);
                locals.Add(FunctionStackVariable);
            }

            bodyStmt = WrapScopeStatements(
                Ast.Block(
                    Ast.Block(init),
                    statements.Count == 0 ?
                        EmptyBlock :
                        Ast.Block(new ReadOnlyCollection<MSAst.Expression>(statements)),
                    modStmt,
                    bodyStmt,
                    LocalContext
                ),
                Body.CanThrow
            );

            var lambda = AstUtils.LightLambda<Func<CodeContext, CodeContext>>(
                typeof(CodeContext),
                Ast.Block(
                    locals,
                    bodyStmt
                ),
                Name + "$" + Interlocked.Increment(ref _classId),
                new[] { _parentContextParam }
                );

            return lambda;
        }

        internal override LightLambdaExpression GetLambda() {
            if (_dlrBody == null) {
                PerfTrack.NoteEvent(PerfTrack.Categories.Compiler, "Creating FunctionBody");
                _dlrBody = MakeClassBody();
            }

            return _dlrBody;
        }

        /// <summary>
        /// Gets the closure tuple from our parent context.
        /// </summary>
        internal override MSAst.Expression GetParentClosureTuple() => _tupleExpression;

        internal override string ScopeDocumentation => GetDocumentation(Body);

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (Decorators != null) {
                    foreach (Expression decorator in Decorators) {
                        decorator.Walk(walker);
                    }
                }
                if (_bases != null) {
                    foreach (Expression b in _bases) {
                        b.Walk(walker);
                    }
                }
                Body?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        private string FindSelfNames() {
            if (Body is SuiteStatement stmts) {
                foreach (Statement stmt in stmts.Statements) {
                    if (stmt is FunctionDefinition def && def.Name == "__init__") {
                        return string.Join(",", SelfNameFinder.FindNames(def));
                    }
                }
            }
            return string.Empty;
        }

        private class SelfNameFinder : PythonWalker {
            private readonly FunctionDefinition _function;
            private readonly Parameter _self;

            private SelfNameFinder(FunctionDefinition function, Parameter self) {
                _function = function;
                _self = self;
            }

            public static string[] FindNames(FunctionDefinition function) {
                var parameters = function.Parameters;

                if (parameters.Count == 0) {
                    // no point analyzing function with no parameters
                    return ArrayUtils.EmptyStrings;
                }

                var finder = new SelfNameFinder(function, parameters[0]);
                function.Body.Walk(finder);
                return ArrayUtils.ToArray(finder._names.Keys);
            }

            private readonly Dictionary<string, bool> _names = new Dictionary<string, bool>(StringComparer.Ordinal);

            private bool IsSelfReference(Expression expr) {
                return expr is NameExpression ne
                    && _function.TryGetVariable(ne.Name, out PythonVariable variable)
                    && variable == _self.PythonVariable;
            }

            // Don't recurse into class or function definitions
            public override bool Walk(ClassDefinition node) => false;
            public override bool Walk(FunctionDefinition node) => false;

            public override bool Walk(AssignmentStatement node) {
                foreach (Expression lhs in node.Left) {
                    if (lhs is MemberExpression me) {
                        if (IsSelfReference(me.Target)) {
                            _names[me.Name] = true;
                        }
                    }
                }
                return true;
            }
        }

        internal override void RewriteBody(MSAst.ExpressionVisitor visitor) {
            _dlrBody = null;
            Body = new RewrittenBodyStatement(Body, visitor.Visit(Body));
        }
    }
}
