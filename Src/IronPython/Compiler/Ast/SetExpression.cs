// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;
using System.Linq;

using IronPython.Runtime;

using Microsoft.Scripting.Utils;

using AstUtils = Microsoft.Scripting.Ast.Utils;
using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {
    public class SetExpression : Expression {
        private readonly Expression[] _items;

        public SetExpression(params Expression[] items) {
            ContractUtils.RequiresNotNull(items, nameof(items));

            _items = items;
        }

        public IList<Expression> Items => _items;

        protected bool HasStarredExpression => Items.OfType<StarredExpression>().Any();

        public override MSAst.Expression Reduce() {
            if (HasStarredExpression) {
                return UnpackSequenceHelper<SetCollection>(Items, AstMethods.MakeEmptySet, AstMethods.SetAdd, AstMethods.SetUpdate);
            }

            return Expression.Call(
                AstMethods.MakeSet,
                NewArrayInit(
                    typeof(object),
                    ArrayUtils.ConvertAll(_items, x => AstUtils.Convert(x, typeof(object)))
                )
            );
        }

        internal override string CheckAssign() => "can't assign to literal";

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
