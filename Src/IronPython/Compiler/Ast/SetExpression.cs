// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

using System;
using System.Collections.Generic;
using Microsoft.Scripting.Utils;
using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class SetExpression : Expression {
        private readonly Expression[] _items;

        public SetExpression(params Expression[] items) {
            ContractUtils.RequiresNotNull(items, nameof(items));

            _items = items;
        }

        public IList<Expression> Items => _items;

        public override MSAst.Expression Reduce() {
            return Expression.Call(
                AstMethods.MakeSet,
                NewArrayInit(
                    typeof(object),
                    ArrayUtils.ConvertAll(_items, x => AstUtils.Convert(x, typeof(object)))
                )
            );
        }

        internal override string CheckAssign() {
            return "can't assign to literal";
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                foreach (Expression s in _items) {
                    s.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }
    }
}
