// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class ExpressionStatement : Statement {
        public ExpressionStatement(Expression expression) {
            Expression = expression;
        }

        public Expression Expression { get; }

        public override MSAst.Expression Reduce() {
            MSAst.Expression expression = Expression;

            return ReduceWorker(expression);
        }

        private MSAst.Expression ReduceWorker(MSAst.Expression expression) {
            if (Parent.PrintExpressions) {
                expression = Ast.Call(
                    AstMethods.PrintExpressionValue,
                    Parent.LocalContext,
                    ConvertIfNeeded(expression, typeof(object))
                );
            }

            return GlobalParent.AddDebugInfoAndVoid(expression, Expression.Span);
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Expression?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        public override string Documentation {
            get {
                if (Expression is ConstantExpression ce) {
                    return ce.Value as string;
                }
                return null;
            }
        }

        internal override bool CanThrow => Expression.CanThrow;
    }
}
