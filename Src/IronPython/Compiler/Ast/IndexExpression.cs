// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

using IronPython.Runtime;
using IronPython.Runtime.Binding;

using Microsoft.Scripting;
using Microsoft.Scripting.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class IndexExpression : Expression {
        public IndexExpression(Expression target, Expression index) {
            Target = target;
            Index = index;
        }

        public Expression Target { get; }

        public Expression Index { get; }

        public override MSAst.Expression Reduce() {
            if (IsSlice) {
                return GlobalParent.GetSlice(GetActionArgumentsForGetOrDelete());
            }
            return GlobalParent.GetIndex(GetActionArgumentsForGetOrDelete());
        }

        private MSAst.Expression[] GetActionArgumentsForGetOrDelete() {
            if (Index is TupleExpression te && te.IsExpandable) {
                return ArrayUtils.Insert(Target, te.Items);
            }

            if (Index is SliceExpression se) {
                if (se.StepProvided) {
                    return new[] {
                        Target,
                        GetSliceValue(se.SliceStart),
                        GetSliceValue(se.SliceStop),
                        GetSliceValue(se.SliceStep)
                    };
                }

                return new[] {
                    Target,
                    GetSliceValue(se.SliceStart),
                    GetSliceValue(se.SliceStop)
                };
            }

            return new[] { Target, Index };
        }

        private static MSAst.Expression GetSliceValue(Expression expr) {
            if (expr != null) {
                return expr;
            }

            return Ast.Field(null, typeof(MissingParameter).GetField(nameof(MissingParameter.Value)));
        }

        private MSAst.Expression[] GetActionArgumentsForSet(MSAst.Expression right) {
            return ArrayUtils.Append(GetActionArgumentsForGetOrDelete(), right);
        }

        internal override MSAst.Expression TransformSet(SourceSpan span, MSAst.Expression right, PythonOperationKind op) {
            if (op != PythonOperationKind.None) {
                right = GlobalParent.Operation(
                    typeof(object),
                    op,
                    this,
                    right
                );
            }

            MSAst.Expression index = IsSlice
                ? GlobalParent.SetSlice(GetActionArgumentsForSet(right))
                : GlobalParent.SetIndex(GetActionArgumentsForSet(right));

            return GlobalParent.AddDebugInfoAndVoid(index, Span);
        }

        internal override MSAst.Expression TransformDelete() {
            MSAst.Expression index;
            if (IsSlice) {
                index = GlobalParent.DeleteSlice(GetActionArgumentsForGetOrDelete());
            } else {
                index = GlobalParent.DeleteIndex(GetActionArgumentsForGetOrDelete());
            }

            return GlobalParent.AddDebugInfoAndVoid(index, Span);
        }

        internal override string CheckAssign() => null;

        internal override string CheckDelete() => null;

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Target?.Walk(walker);
                Index?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        private bool IsSlice => Index is SliceExpression;
    }
}
