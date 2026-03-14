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

            // Helper to assign proper parent and span to nodes
            T SetScope<T>(T node) where T : Node {
                node.Parent = parent;
                node.IndexSpan = span;
                return node;
            }

            // mgr = EXPR
            var assignMgr = SetScope(new AssignmentStatement([SetScope(new NameExpression("__asyncwith_mgr"))], ContextManager));

            // await mgr.__aenter__()
            var aenterAttr = SetScope(new MemberExpression(SetScope(new NameExpression("__asyncwith_mgr")), "__aenter__"));
            var aenterCall = SetScope(new CallExpression(aenterAttr, null, null));
            var awaitEnter = new AwaitExpression(aenterCall);

            Statement bodyStmt;
            if (Variable is not null) {
                // VAR = await value; BLOCK
                var assignVar = SetScope(new AssignmentStatement([Variable], awaitEnter));
                bodyStmt = new SuiteStatement([assignVar, Body]) { Parent = parent };
            } else {
                var exprStmt = SetScope(new ExpressionStatement(awaitEnter));
                bodyStmt = new SuiteStatement([exprStmt, Body]) { Parent = parent };
            }

            // await mgr.__aexit__(None, None, None)
            var aexitAttr = SetScope(new MemberExpression(SetScope(new NameExpression("__asyncwith_mgr")), "__aexit__"));
            var none = SetScope(new ConstantExpression(null));
            var aexitCallNormal = SetScope(new CallExpression(aexitAttr, [none, none, none], null));
            var awaitExitNormal = new AwaitExpression(aexitCallNormal);

            // try/finally: await __aexit__ on normal exit
            var finallyExprStmt = SetScope(new ExpressionStatement(awaitExitNormal));
            var tryFinally = SetScope(new TryStatement(bodyStmt, null, null, finallyExprStmt) { HeaderIndex = span.End });

            return SetScope(new SuiteStatement([assignMgr, tryFinally]));
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
