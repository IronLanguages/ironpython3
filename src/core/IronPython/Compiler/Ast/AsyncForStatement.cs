// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Threading;

using Microsoft.Scripting;
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

            // Helper to create nodes with proper parent and span
            NameExpression MakeName(string name) {
                var n = new NameExpression(name) { Parent = parent };
                n.IndexSpan = span;
                return n;
            }

            T WithSpan<T>(T node) where T : Node {
                node.IndexSpan = span;
                return node;
            }

            // _iter = ITER.__aiter__()
            var aiterAttr = WithSpan(new MemberExpression(List, "__aiter__") { Parent = parent });
            var aiterCall = WithSpan(new CallExpression(aiterAttr, null, null) { Parent = parent });
            var assignIter = WithSpan(new AssignmentStatement(new Expression[] { MakeName(iterName) }, aiterCall) { Parent = parent });

            // running = True
            var trueConst = new ConstantExpression(true) { Parent = parent }; trueConst.IndexSpan = span;
            var assignRunning = WithSpan(new AssignmentStatement(new Expression[] { MakeName(runningName) }, trueConst) { Parent = parent });

            // TARGET = await __aiter.__anext__()
            var anextAttr = WithSpan(new MemberExpression(MakeName(iterName), "__anext__") { Parent = parent });
            var anextCall = WithSpan(new CallExpression(anextAttr, null, null) { Parent = parent });
            var awaitNext = new AwaitExpression(anextCall);
            var assignTarget = WithSpan(new AssignmentStatement(new Expression[] { Left }, awaitNext) { Parent = parent });

            // except StopAsyncIteration: __running = False
            var falseConst = new ConstantExpression(false) { Parent = parent }; falseConst.IndexSpan = span;
            var stopRunning = WithSpan(new AssignmentStatement(
                new Expression[] { MakeName(runningName) }, falseConst) { Parent = parent });
            var handler = WithSpan(new TryStatementHandler(
                MakeName("StopAsyncIteration"),
                null!,
                WithSpan(new SuiteStatement(new Statement[] { stopRunning }) { Parent = parent })
            ) { Parent = parent });
            handler.HeaderIndex = span.End;

            // try/except/else block
            var tryExcept = WithSpan(new TryStatement(
                assignTarget,
                new[] { handler },
                WithSpan(new SuiteStatement(new Statement[] { Body }) { Parent = parent }),
                null!
            ) { Parent = parent });
            tryExcept.HeaderIndex = span.End;

            // while __running: try/except/else
            var whileStmt = new WhileStatement(MakeName(runningName), tryExcept, Else);
            whileStmt.SetLoc(GlobalParent, span.Start, span.End, span.End);
            whileStmt.Parent = parent;

            var suite = WithSpan(new SuiteStatement(new Statement[] { assignIter, assignRunning, whileStmt }) { Parent = parent });
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

        internal override bool CanThrow => true;
    }
}
