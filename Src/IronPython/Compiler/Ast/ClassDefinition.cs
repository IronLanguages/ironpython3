// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

using IronPython.Runtime;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Utils;

using AstUtils = Microsoft.Scripting.Ast.Utils;
using LightLambdaExpression = Microsoft.Scripting.Ast.LightLambdaExpression;
using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class ClassDefinition : ScopeStatement {
        private readonly string _name;
        private readonly Expression[] _bases;
        private readonly Keyword[] _keywords;

        private LightLambdaExpression? _dlrBody;       // the transformed body including all of our initialization, etc...

        private static int _classId;

        private static readonly MSAst.ParameterExpression _outerContextParam = Ast.Parameter(typeof(CodeContext), "$outerContext");
        private static readonly MSAst.Expression _tupleExpression = MSAst.Expression.Call(AstMethods.GetClosureTupleFromContext, _outerContextParam);

        public ClassDefinition(string name, IReadOnlyList<Expression>? bases, IReadOnlyList<Keyword>? keywords, Statement? body = null) {
            _name = name;
            _bases = bases?.ToArray() ?? Array.Empty<Expression>();
            _keywords = keywords?.ToArray() ?? Array.Empty<Keyword>();
            Body = body ?? EmptyStatement.PreCompiledInstance;
        }

        public SourceLocation Header => GlobalParent.IndexToLocation(HeaderIndex);

        public int HeaderIndex { get; set; }

        public override string Name => _name;

        public IReadOnlyList<Expression> Bases => _bases;

        public IReadOnlyList<Keyword> Keywords => _keywords;

        public Statement Body { get; set; }

        public IList<Expression>? Decorators { get; internal set; }

        /// <summary>
        /// Variable corresponding to the class name, set during name binding
        /// </summary>
        [DisallowNull]
        internal PythonVariable? PythonVariable { get; set; }

        /// <summary>
        /// Variable for the the __module__ (module name), set during name binding
        /// </summary>
        [DisallowNull]
        internal PythonVariable? ModVariable { get; set; }

        /// <summary>
        /// Variable for the __doc__ attribute, set during name binding
        /// </summary>
        [DisallowNull]
        internal PythonVariable? DocVariable { get; set; }

        /// <summary>
        /// Variable for the module's __name__, set during name binding
        /// </summary>
        [DisallowNull]
        internal PythonVariable? ModuleNameVariable { get; set; }

        // set during name binding
        [DisallowNull]
        private PythonVariable? ClassCellVariable { get; set; }
        [DisallowNull]
        private PythonVariable? ClassVariable { get; set; }

        internal override bool HasLateBoundVariableSets {
            get => true; // If a class or any of its bases uses a metaclass, __prepare__ may insert extra variables into the class namespace
            set => base.HasLateBoundVariableSets = value;
        }

        internal override bool ExposesLocalVariable(PythonVariable variable) {
            Debug.Assert(variable.Name == "__class__");
            return true;
        }


        internal override bool TryBindOuter(ScopeStatement from, PythonReference reference, [NotNullWhen(true)] out PythonVariable? variable) {
            if (reference.Name == "__class__") {
                ClassVariable = variable = EnsureClassVariable();
                ClassCellVariable = EnsureVariable("__classcell__");
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

        internal override PythonVariable? BindReference(PythonNameBinder binder, PythonReference reference) {
            PythonVariable? variable;

            // Python semantics: The variables bound local in the class
            // scope are accessed by name - the dictionary behavior of classes
            if (TryGetVariable(reference.Name, out variable)) {
                if (variable.Kind is VariableKind.Global) {
                    // Variable declared with `global` statement
                    AddReferencedGlobal(reference.Name);
                    return variable;
                } else if (variable.Kind is not VariableKind.Nonlocal) {
                    // Fall back on LookupName/SetName in local context dict,
                    // which is slightly faster than LookupGlobalVariable expression used by variables of Attribute kind.
                    return null;
                    // In practice, variable.Kind will always be Attribute here
                    // as the only Local can be __class__ and it is never referenced directly from within the class body
                    // and Parameter does not exist for a class.
                }

                // else NonLocal (i.e. declared with `nonlocal`): continue binding
            }

            // Try to bind in outer scopes, except the global scope
            for (ScopeStatement parent = Parent; parent is not null && !parent.IsGlobal; parent = parent.Parent) {
                if (parent.TryBindOuter(this, reference, out PythonVariable? outerVariable)) {
                    // for implicit globals, fall back on dictionary behaviour
                    if (outerVariable.Kind is VariableKind.Global) return null;

                    return outerVariable;
                }
            }

            return null;
        }

        internal override PythonVariable EnsureVariable(string name) {
            if (TryGetVariable(name, out PythonVariable? variable)) {
                return variable;
            }
            return CreateVariable(name, VariableKind.Attribute);
        }

        internal PythonVariable EnsureClassVariable() {
            if (TryGetVariable("$__class__", out PythonVariable? variable)) {
                return variable;
            }
            return CreateVariable("__class__", VariableKind.Local, "$__class__");
        }

        internal override MSAst.Expression LookupVariableExpression(PythonVariable variable) {
            if (variable.Kind is VariableKind.Global) {
                // `global` declaration overrides class namespace lookup
                return base.LookupVariableExpression(variable);
            }
            if (TryGetNonlocalStatement(variable.Name, out _)) {
                // In IronPython, `nonlocal` declaration overrides class namespace lookup
                // https://github.com/IronLanguages/ironpython3/issues/1560
                return base.LookupVariableExpression(variable);
            }

            // Emulates opcode LOAD_CLASSDEREF
            MSAst.Expression fallbackValue = GetVariableExpression(variable);
            if (fallbackValue is Microsoft.Scripting.Ast.ILightExceptionAwareExpression lightAware) {
                fallbackValue = lightAware.ReduceForLightExceptions();
            }
            return Ast.Call(
                AstMethods.LookupLocalName,
                LocalContext,
                Ast.Constant(variable.Name),
                fallbackValue
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
                UnpackBasesHelper(_bases),
                UnpackKeywordsHelper(Parent.LocalContext, _keywords),
                AstUtils.Constant(FindSelfNames())
            );

            classDef = AddDecorators(classDef, Decorators);

            return GlobalParent.AddDebugInfoAndVoid(
                AssignValue(Parent.GetVariableExpression(PythonVariable!), classDef),
                new SourceSpan(
                    GlobalParent.IndexToLocation(StartIndex),
                    GlobalParent.IndexToLocation(HeaderIndex)
                )
            );

            // Compare to: CallExpression.Reduce.__UnpackListHelper
            static MSAst.Expression UnpackBasesHelper(ReadOnlySpan<Expression> bases) {
                if (bases.Length == 0) {
                    return Expression.Call(AstMethods.MakeEmptyTuple);
                }
                foreach (var arg in bases) {
                    if (arg is StarredExpression) {
                        return Expression.Call(AstMethods.ListToTuple,
                            Expression.UnpackSequenceHelper<PythonList>(bases, AstMethods.MakeEmptyList, AstMethods.ListAppend, AstMethods.ListExtend)
                        );
                    }
                }
                return Expression.Call(AstMethods.MakeTuple,
                    Expression.NewArrayInit(
                        typeof(object),
                        ToObjectArray(bases)
                    )
                );
            }

            // Compare to: CallExpression.Reduce.__UnpackDictHelper
            static MSAst.Expression UnpackKeywordsHelper(MSAst.Expression context, ReadOnlySpan<Keyword> kwargs) {
                if (kwargs.Length == 0) {
                    return AstUtils.Constant(null, typeof(PythonDictionary));
                }

                var expressions = new ReadOnlyCollectionBuilder<MSAst.Expression>(kwargs.Length + 2);
                var varExpr = Expression.Variable(typeof(PythonDictionary), "$dict");
                expressions.Add(Expression.Assign(varExpr, Expression.Call(AstMethods.MakeEmptyDict)));
                foreach (var arg in kwargs) {
                    if (arg.Name is null) {
                        expressions.Add(Expression.Call(AstMethods.DictMerge, context, varExpr, AstUtils.Convert(arg.Expression, typeof(object))));
                    } else {
                        expressions.Add(Expression.Call(AstMethods.DictMergeOne, context, varExpr, AstUtils.Constant(arg.Name, typeof(object)), AstUtils.Convert(arg.Expression, typeof(object))));
                    }
                }
                expressions.Add(varExpr);
                return Expression.Block(typeof(PythonDictionary), new MSAst.ParameterExpression[] { varExpr }, expressions);
            }
        }

        private MSAst.Expression SetLocalName(string name, MSAst.Expression expression)
            => Ast.Call(AstMethods.SetName, LocalContext, Ast.Constant(name), expression);

        private Microsoft.Scripting.Ast.LightExpression<Func<CodeContext, CodeContext>> MakeClassBody() {
            // we always need to create a nested context for class defs            

            var init = new List<MSAst.Expression>();
            var locals = new ReadOnlyCollectionBuilder<MSAst.ParameterExpression>();

            locals.Add(LocalCodeContextVariable);
            locals.Add(PythonAst._globalContext);

            init.Add(Ast.Assign(PythonAst._globalContext, new GetGlobalContextExpression(_outerContextParam)));

            GlobalParent.PrepareScope(locals, init);

            CreateVariables(locals, init);

            var createLocal = CreateLocalContext(_outerContextParam, newNamespace: false);

            init.Add(Ast.Assign(LocalCodeContextVariable, createLocal));

            // __module__ = __name__
            MSAst.Expression modStmt = SetLocalName("__module__", AstUtils.Convert(GetVariableExpression(ModuleNameVariable!), typeof(object)));

            // TODO: set __qualname__

            // __doc__ = """..."""
            MSAst.Expression? docStmt = null;
            string doc = GetDocumentation(Body);
            if (doc is not null) {
                docStmt = SetLocalName("__doc__", Ast.Constant(doc, typeof(object)));
            }

            // Create the body
            MSAst.Expression bodyStmt = Body;
            if (Body.CanThrow && GlobalParent.PyContext.PythonOptions.Frames) {
                bodyStmt = AddFrame(LocalContext, FuncCodeExpr, bodyStmt);
                locals.Add(FunctionStackVariable);
            }

            // __classcell__ == ClosureCell(__class__)
            MSAst.Expression? assignClassCellStmt = null;
            if (ClassVariable is not null) {
                var exp = (ClosureExpression)GetVariableExpression(ClassVariable);
                assignClassCellStmt = AssignValue(GetVariableExpression(ClassCellVariable!), exp.ClosureCell);
            }

            bodyStmt = WrapScopeStatements(
                Ast.Block(
                    Ast.Block(init),
                    modStmt,
                    // __qualname__
                    docStmt is not null ? docStmt : AstUtils.Empty(),
                    bodyStmt,
                    assignClassCellStmt is not null ? assignClassCellStmt : AstUtils.Empty(),
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
                new[] { _outerContextParam }
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
                foreach (var b in _bases) {
                    b.Walk(walker);
                }
                foreach (var b in _keywords) {
                    b.Walk(walker);
                }
                Body.Walk(walker);
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
                    && _function.TryGetVariable(ne.Name, out PythonVariable? variable)
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
