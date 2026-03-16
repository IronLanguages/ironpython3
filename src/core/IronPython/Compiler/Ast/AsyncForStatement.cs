// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Threading;

using IronPython.Runtime.Binding;
using IronPython.Runtime.Exceptions;

using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {

    /// <summary>
    /// Represents an async for statement.
    /// Desugared to Python AST that uses __aiter__ and await __anext__().
    /// </summary>
    public class AsyncForStatement : Statement, ILoopStatement {
        private static int _counter;
        private Statement? _desugared;

        public AsyncForStatement(Expression left, Expression list, Statement body, Statement? @else) {
            Left = left;
            List = list;
            Body = body;
            Else = @else;
        }

        public int HeaderIndex { private get; set; }

        public Expression Left { get; }

        public Expression List { get; set; }

        public Statement Body { get; set; }

        public Statement? Else { get; }

        MSAst.LabelTarget ILoopStatement.BreakLabel { get; set; } = null!;

        MSAst.LabelTarget ILoopStatement.ContinueLabel { get; set; } = null!;

        /// <summary>
        /// Build the desugared tree. Called during Walk when Parent and IndexSpan are available.
        /// </summary>
        private Statement BuildDesugared() {
            var parent = Parent;
            var span = IndexSpan;
            var id = Interlocked.Increment(ref _counter);

            // async for TARGET in ITER:
            //     BLOCK
            // else:
            //     ELSE_BLOCK
            //
            // desugars to:
            //
            // __aiter = ITER.__aiter__()
            // __running = True
            // while __running:
            //     try:
            //         TARGET = await __aiter.__anext__()
            //     except StopAsyncIteration:
            //         __running = False
            //     else:
            //         BLOCK
            // else:
            //     ELSE_BLOCK

            var iterName = $"__asyncfor_iter{id}";
            var runningName = $"__asyncfor_running{id}";

            // Helper to assign proper parent and span to nodes
            T SetScope<T>(T node) where T : Node {
                node.Parent = parent;
                node.IndexSpan = span;
                return node;
            }

            // _iter = ITER.__aiter__()
            var aiterCall = SetScope(new UnaryExpression(PythonOperationKind.AIter, List));
            var assignIter = SetScope(new AssignmentStatement([SetScope(new NameExpression(iterName))], aiterCall));

            // running = True
            var trueConst = SetScope(new ConstantExpression(true));
            var assignRunning = SetScope(new AssignmentStatement([SetScope(new NameExpression(runningName))], trueConst));

            // TARGET = await __aiter.__anext__()
            var anextCall = SetScope(new UnaryExpression(PythonOperationKind.ANext, SetScope(new NameExpression(iterName))));
            var awaitNext = new AwaitExpression(anextCall);
            var assignTarget = SetScope(new AssignmentStatement([Left], awaitNext));

            // except StopAsyncIteration: __running = False
            var falseConst = SetScope(new ConstantExpression(false));
            var stopRunning = SetScope(new AssignmentStatement([SetScope(new NameExpression(runningName))], falseConst));
            var handler = SetScope(new TryStatementHandler(SetScope(new NameExpression(nameof(PythonExceptions.StopAsyncIteration))), null!, SetScope(new SuiteStatement([stopRunning]))));
            handler.HeaderIndex = span.End;

            // try/except/else block
            var tryExcept = SetScope(new TryStatement(assignTarget, [handler], SetScope(new SuiteStatement([Body])), null));
            tryExcept.HeaderIndex = span.End;

            // while __running: try/except/else
            var whileStmt = new WhileStatement(SetScope(new NameExpression(runningName)), tryExcept, Else);
            whileStmt.SetLoc(GlobalParent, span.Start, span.End, span.End);
            whileStmt.Parent = parent;

            return SetScope(new SuiteStatement([assignIter, assignRunning, whileStmt]));
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

        internal override bool CanThrow => true;
    }
}
