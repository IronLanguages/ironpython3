// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using IronPython.Runtime;
using IronPython.Runtime.Operations;

using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {
    public class TupleExpression : SequenceExpression {
        public TupleExpression(bool expandable, params Expression[] items)
            : base(items) {
            IsExpandable = expandable;
        }

        internal override string? CheckAssign() {
            return base.CheckAssign();
        }

        internal override string? CheckDelete() {
            return base.CheckDelete();
        }

        public override MSAst.Expression Reduce() {
            if (IsExpandable) {
                return Expression.NewArrayInit(
                    typeof(object),
                    ToObjectArray(_items)
                );
            }

            if (Items.Count == 0) {
                return Expression.Field(
                    null!,
                    typeof(PythonOps).GetField(nameof(PythonOps.EmptyTuple))!
                );
            }

            if (HasStarredExpression) {
                return Expression.Call(AstMethods.ListToTuple, UnpackSequenceHelper<PythonList>(_items, AstMethods.MakeEmptyList, AstMethods.ListAppend, AstMethods.ListExtend));
            }

            return Expression.Call(
                AstMethods.MakeTuple,
                Expression.NewArrayInit(
                    typeof(object),
                    ToObjectArray(_items)
                )
            );
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (Items != null) {
                    foreach (Expression e in Items) {
                        e.Walk(walker);
                    }
                }
            }
            walker.PostWalk(this);
        }

        public bool IsExpandable { get; }

        internal override bool IsConstant {
            get {
                foreach (var item in Items) {
                    if (!item.IsConstant) {
                        return false;
                    }
                }
                return true;
            }
        }

        internal override object GetConstantValue() {
            if (Items.Count == 0) {
                return PythonTuple.EMPTY;
            }

            object[] items = new object[Items.Count];
            for (int i = 0; i < items.Length; i++) {
                items[i] = Items[i].GetConstantValue();
            }

            return PythonTuple.MakeTuple(items);
        }
    }
}
