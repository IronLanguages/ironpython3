/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

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

        public IList<Expression> Expressions {
            get { return _expressions; }
        }

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
