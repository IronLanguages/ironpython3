// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class ListExpression : SequenceExpression {
        public ListExpression(params Expression[] items)
            : base(items) {
        }

        public override MSAst.Expression Reduce() {
            if (Items.Count == 0) {
                return Ast.Call(
                    AstMethods.MakeEmptyListFromCode,
                    EmptyExpression
                );
            }

            return Call(
                AstMethods.MakeListNoCopy,  // method
                NewArrayInit(           // parameters
                    typeof(object),
                    ToObjectArray(Items)
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
    }
}
