// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {

    public class DelStatement : Statement {
        private readonly Expression[] _expressions;

        public DelStatement(Expression[] expressions) {
            _expressions = expressions;
        }

        public IList<Expression> Expressions => _expressions;

        public override MSAst.Expression Reduce() {
            // Transform to series of individual del statements.
            ReadOnlyCollectionBuilder<MSAst.Expression> statements = new ReadOnlyCollectionBuilder<MSAst.Expression>(_expressions.Length + 1);
            for (int i = 0; i < _expressions.Length; i++) {
                statements.Add(_expressions[i].TransformDelete());
            }
            statements.Add(AstUtils.Empty());
            return GlobalParent.AddDebugInfo(MSAst.Expression.Block(statements), Span);
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_expressions != null) {
                    foreach (Expression expression in _expressions) {
                        expression.Walk(walker);
                    }
                }
            }
            walker.PostWalk(this);
        }

        internal override bool CanThrow {
            get {
                foreach (Expression e in _expressions) {
                    if (e.CanThrow) {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
