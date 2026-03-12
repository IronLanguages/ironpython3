// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using MSAst = System.Linq.Expressions;

using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    /// <summary>
    /// Represents an await expression. Implemented as yield from expr.__await__().
    /// </summary>
    public class AwaitExpression : Expression {
        private readonly Statement _statement;
        private readonly NameExpression _result;

        public AwaitExpression(Expression expression) {
            Expression = expression;

            // await expr is equivalent to yield from expr.__await__()
            // We build: __awaitprefix_EXPR = expr; yield from __awaitprefix_EXPR.__await__(); __awaitprefix_r = __yieldfromprefix_r
            var parent = expression.Parent;

            var awaitableExpr = new NameExpression("__awaitprefix_EXPR") { Parent = parent };
            var getAwait = new MemberExpression(awaitableExpr, "__await__") { Parent = parent };
            var callAwait = new CallExpression(getAwait, null, null) { Parent = parent };
            var yieldFrom = new YieldFromExpression(callAwait);

            Statement s1 = new AssignmentStatement(new Expression[] { new NameExpression("__awaitprefix_EXPR") { Parent = parent } }, expression) { Parent = parent };
            Statement s2 = new ExpressionStatement(yieldFrom) { Parent = parent };
            Statement s3 = new AssignmentStatement(
                new Expression[] { new NameExpression("__awaitprefix_r") { Parent = parent } },
                new NameExpression("__yieldfromprefix_r") { Parent = parent }
            ) { Parent = parent };

            _statement = new SuiteStatement(new Statement[] { s1, s2, s3 }) { Parent = parent };

            _result = new NameExpression("__awaitprefix_r") { Parent = parent };
        }

        public Expression Expression { get; }

        public override MSAst.Expression Reduce() {
            return Ast.Block(
                typeof(object),
                _statement,
                AstUtils.Convert(_result, typeof(object))
            ).Reduce();
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Expression?.Walk(walker);
                _statement.Walk(walker);
                _result.Walk(walker);
            }
            walker.PostWalk(this);
        }

        public override string NodeName => "await expression";
    }
}
