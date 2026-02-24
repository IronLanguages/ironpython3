// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using Microsoft.Scripting;
using MSAst = System.Linq.Expressions;

using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    /// <summary>
    /// Represents an async with statement.
    /// Desugared to Python AST that uses await on __aenter__ and __aexit__.
    /// </summary>
    public class AsyncWithStatement : Statement {
        private Statement? _desugared;

        public AsyncWithStatement(Expression contextManager, Expression? var, Statement body) {
            ContextManager = contextManager;
            Variable = var;
            Body = body;
        }

        public int HeaderIndex { private get; set; }

        public Expression ContextManager { get; }

        public new Expression? Variable { get; }

        public Statement Body { get; }

        /// <summary>
        /// Build the desugared tree. Called during Walk when Parent and IndexSpan are available.
        /// </summary>
        private Statement BuildDesugared() {
            var parent = Parent;
            var span = IndexSpan;

            // async with EXPR as VAR:
            //     BLOCK
            //
            // desugars to:
            //
            // mgr = EXPR
            // try:
            //     VAR = await mgr.__aenter__()  (or just await mgr.__aenter__())
            //     BLOCK
            // finally:
            //     await mgr.__aexit__(None, None, None)

            // Helper to create nodes with proper parent and span
            NameExpression MakeName(string name) {
                var n = new NameExpression(name) { Parent = parent };
                n.IndexSpan = span;
                return n;
            }

            // mgr = EXPR
            var assignMgr = new AssignmentStatement(new Expression[] { MakeName("__asyncwith_mgr") }, ContextManager) { Parent = parent };
            assignMgr.IndexSpan = span;

            // await mgr.__aenter__()
            var aenterAttr = new MemberExpression(MakeName("__asyncwith_mgr"), "__aenter__") { Parent = parent };
            aenterAttr.IndexSpan = span;
            var aenterCall = new CallExpression(aenterAttr, null, null) { Parent = parent };
            aenterCall.IndexSpan = span;
            var awaitEnter = new AwaitExpression(aenterCall);

            Statement bodyStmt;
            if (Variable != null) {
                // VAR = await value; BLOCK
                var assignVar = new AssignmentStatement(new Expression[] { Variable }, awaitEnter) { Parent = parent };
                assignVar.IndexSpan = span;
                bodyStmt = new SuiteStatement(new Statement[] { assignVar, Body }) { Parent = parent };
            } else {
                var exprStmt = new ExpressionStatement(awaitEnter) { Parent = parent };
                exprStmt.IndexSpan = span;
                bodyStmt = new SuiteStatement(new Statement[] { exprStmt, Body }) { Parent = parent };
            }

            // await mgr.__aexit__(None, None, None)
            var aexitAttr = new MemberExpression(MakeName("__asyncwith_mgr"), "__aexit__") { Parent = parent };
            aexitAttr.IndexSpan = span;
            var none1 = new ConstantExpression(null) { Parent = parent }; none1.IndexSpan = span;
            var none2 = new ConstantExpression(null) { Parent = parent }; none2.IndexSpan = span;
            var none3 = new ConstantExpression(null) { Parent = parent }; none3.IndexSpan = span;
            var aexitCallNormal = new CallExpression(aexitAttr,
                new Expression[] { none1, none2, none3 }, null) { Parent = parent };
            aexitCallNormal.IndexSpan = span;
            var awaitExitNormal = new AwaitExpression(aexitCallNormal);

            // try/finally: await __aexit__ on normal exit
            var finallyExprStmt = new ExpressionStatement(awaitExitNormal) { Parent = parent };
            finallyExprStmt.IndexSpan = span;
            var tryFinally = new TryStatement(bodyStmt, null, null, finallyExprStmt) { Parent = parent };
            tryFinally.IndexSpan = span;
            tryFinally.HeaderIndex = span.End;

            var suite = new SuiteStatement(new Statement[] { assignMgr, tryFinally }) { Parent = parent };
            suite.IndexSpan = span;
            return suite;
        }

        public override MSAst.Expression Reduce() {
            return _desugared!.Reduce();
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                // Build the desugared tree on first walk (when Parent and IndexSpan are set)
                if (_desugared == null) {
                    _desugared = BuildDesugared();
                }
                _desugared.Walk(walker);
            }
            walker.PostWalk(this);
        }
    }
}
