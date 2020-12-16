// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.Scripting;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

using MSAst = System.Linq.Expressions;

using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public abstract class ComprehensionIterator : Node {
        internal abstract MSAst.Expression Transform(MSAst.Expression body);
    }

    public abstract class Comprehension : Expression {
        public abstract IList<ComprehensionIterator> Iterators { get; }
        public abstract override string NodeName { get; }

        protected abstract MSAst.ParameterExpression MakeParameter();
        protected abstract MethodInfo Factory();
        protected abstract MSAst.Expression Body(MSAst.ParameterExpression res);

        public abstract override void Walk(PythonWalker walker);

        public override Ast Reduce() {
            MSAst.ParameterExpression res = MakeParameter();

            // 1. Initialization code - create list and store it in the temp variable
            MSAst.Expression initialize =
                Ast.Assign(
                    res,
                    Ast.Call(Factory())
                );

            // 2. Create body from LHS: res.Append(item), res.Add(key, value), etc.
            MSAst.Expression body = Body(res);

            // 3. Transform all iterators in reverse order, building the true bodies
            for (int current = Iterators.Count - 1; current >= 0; current--) {
                ComprehensionIterator iterator = Iterators[current];
                body = iterator.Transform(body);
            }

            return Ast.Block(
                new[] { res },
                initialize,
                body,
                res
            );
        }
    }

    public sealed class ListComprehension : Comprehension {
        private readonly ComprehensionIterator[] _iterators;

        public ListComprehension(Expression item, ComprehensionIterator[] iterators) {
            Item = item;
            _iterators = iterators;
            Scope = new ComprehensionScope(this);
        }

        public Expression Item { get; }

        public override IList<ComprehensionIterator> Iterators => _iterators;

        protected override MSAst.ParameterExpression MakeParameter()
            => Ast.Parameter(typeof(PythonList), "list_comprehension_list");

        protected override MethodInfo Factory() => AstMethods.MakeEmptyList;

        public override Ast Reduce() => Scope.AddVariables(base.Reduce());

        protected override Ast Body(MSAst.ParameterExpression res) {
            return GlobalParent.AddDebugInfo(
                Ast.Call(
                    AstMethods.ListAddForComprehension,
                    res,
                    AstUtils.Convert(Item, typeof(object))
                ),
                Item.Span
            );
        }

        public override string NodeName => "list comprehension";

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Item?.Walk(walker);
                if (_iterators != null) {
                    foreach (ComprehensionIterator ci in _iterators) {
                        ci.Walk(walker);
                    }
                }
            }
            walker.PostWalk(this);
        }

        internal ComprehensionScope Scope { get; }
    }
        
    public sealed class SetComprehension : Comprehension {
        private readonly ComprehensionIterator[] _iterators;

        public SetComprehension(Expression item, ComprehensionIterator[] iterators) {
            Item = item;
            _iterators = iterators;
            Scope = new ComprehensionScope(this);
        }

        public Expression Item { get; }

        public override IList<ComprehensionIterator> Iterators => _iterators;

        protected override MSAst.ParameterExpression MakeParameter()
            => Ast.Parameter(typeof(SetCollection), "set_comprehension_set");

        protected override MethodInfo Factory() => AstMethods.MakeEmptySet;

        public override Ast Reduce() => Scope.AddVariables(base.Reduce());

        protected override Ast Body(MSAst.ParameterExpression res) {
            return GlobalParent.AddDebugInfo(
                Ast.Call(
                    AstMethods.SetAddForComprehension,
                    res,
                    AstUtils.Convert(Item, typeof(object))
                ),
                Item.Span
            );
        }

        public override string NodeName => "set comprehension";

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Item?.Walk(walker);
                if (_iterators != null) {
                    foreach (ComprehensionIterator ci in _iterators) {
                        ci.Walk(walker);
                    }
                }
            }
            walker.PostWalk(this);
        }

        internal ComprehensionScope Scope { get; }
    }

    public sealed class DictionaryComprehension : Comprehension {
        private readonly ComprehensionIterator[] _iterators;

        public DictionaryComprehension(Expression key, Expression value, ComprehensionIterator[] iterators) {
            Key = key;
            Value = value;
            _iterators = iterators;
            Scope = new ComprehensionScope(this);
        }

        public Expression Key { get; }

        public Expression Value { get; }

        public override IList<ComprehensionIterator> Iterators => _iterators;

        protected override MSAst.ParameterExpression MakeParameter()
            => Ast.Parameter(typeof(PythonDictionary), "dict_comprehension_dict");

        protected override MethodInfo Factory() => AstMethods.MakeEmptyDict;

        public override Ast Reduce() => Scope.AddVariables(base.Reduce());

        protected override Ast Body(MSAst.ParameterExpression res) {
            return GlobalParent.AddDebugInfo(
                Ast.Call(
                    AstMethods.DictAddForComprehension,
                    res,
                    AstUtils.Convert(Key, typeof(object)),
                    AstUtils.Convert(Value, typeof(object))
                ),
                new SourceSpan(Key.Span.Start, Value.Span.End)
            );
        }

        public override string NodeName => "dict comprehension";

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Key?.Walk(walker);
                Value?.Walk(walker);
                if (_iterators != null) {
                    foreach (ComprehensionIterator ci in _iterators) {
                        ci.Walk(walker);
                    }
                }
            }
            walker.PostWalk(this);
        }

        internal ComprehensionScope Scope { get; }
    }

    /// <summary>
    /// Scope for the comprehension.  Because scopes are usually statements and comprehensions are expressions
    /// this doesn't actually show up in the AST hierarchy and instead hangs off the comprehension expression.
    /// </summary>
    internal class ComprehensionScope : ScopeStatement {
        private readonly Expression _comprehension;
        private static readonly MSAst.ParameterExpression _compContext = Ast.Parameter(typeof(CodeContext), "$compContext");

        public ComprehensionScope(Expression comprehension) {
            _comprehension = comprehension;
        }

        internal override bool ExposesLocalVariable(PythonVariable variable) {
            if (NeedsLocalsDictionary) {
                return true;
            } else if (variable.Scope == this) {
                return false;
            }
            return _comprehension.Parent.ExposesLocalVariable(variable);
        }

        internal override MSAst.Expression/*!*/ GetParentClosureTuple() {
            Debug.Assert(NeedsLocalContext);
            return MSAst.Expression.Call(null, typeof(PythonOps).GetMethod(nameof(PythonOps.GetClosureTupleFromContext)), _comprehension.Parent.LocalContext);
        }

        internal override bool TryBindOuter(ScopeStatement from, PythonReference reference, out PythonVariable variable) {
            ContainsNestedFreeVariables = true;
            if (TryGetVariable(reference.Name, out variable) && variable.Kind != VariableKind.Nonlocal) {
                variable.AccessedInNestedScope = true;

                if (variable.Kind == VariableKind.Local || variable.Kind == VariableKind.Parameter) {
                    from.AddFreeVariable(variable, true);

                    for (ScopeStatement scope = from.Parent; scope != this; scope = scope.Parent) {
                        scope.AddFreeVariable(variable, false);
                    }

                    AddCellVariable(variable);
                } else {
                    from.AddReferencedGlobal(reference.Name);
                }
                return true;
            }
            return false;
        }

        internal override PythonVariable BindReference(PythonNameBinder binder, PythonReference reference) {
            if (TryGetVariable(reference.Name, out PythonVariable variable)) {
                if (variable.Kind == VariableKind.Global) {
                    AddReferencedGlobal(reference.Name);
                }
                Debug.Assert(variable.Kind != VariableKind.Nonlocal, "there should be no nonlocals in a comprehension");
                return variable;
            }

            // then bind in our parent scope
            return _comprehension.Parent.BindReference(binder, reference);
        }

        internal override Ast GetVariableExpression(PythonVariable variable) {
            if (variable.Kind is VariableKind.Global) {
                return GlobalParent.ModuleVariables[variable];
            }

            if (_variableMapping.TryGetValue(variable, out Ast expr)) {
                return expr;
            }

            return _comprehension.Parent.GetVariableExpression(variable);
        }

        internal override Microsoft.Scripting.Ast.LightLambdaExpression GetLambda()
            => throw new NotImplementedException();

        public override void Walk(PythonWalker walker) => _comprehension.Walk(walker);

        internal override Ast LocalContext {
            get {
                if (NeedsLocalContext) {
                    return _compContext;
                }

                return _comprehension.Parent.LocalContext;
            }
        }

        internal Ast AddVariables(Ast expression) {
            ReadOnlyCollectionBuilder<MSAst.ParameterExpression> locals = new ReadOnlyCollectionBuilder<MSAst.ParameterExpression>();
            MSAst.ParameterExpression localContext = null;
            if (NeedsLocalContext) {
                localContext = _compContext;
                locals.Add(_compContext);
            }

            List<MSAst.Expression> body = new List<MSAst.Expression>();
            CreateVariables(locals, body);

            if (localContext != null) {
                var createLocal = CreateLocalContext(_comprehension.Parent.LocalContext);
                body.Add(Ast.Assign(_compContext, createLocal));
            }

            body.Add(expression);

            return Expression.Block(
                locals, 
                body
            );
        }
    }
}
