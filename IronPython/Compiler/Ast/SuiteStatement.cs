// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Microsoft.Scripting.Utils;

using AstUtils = Microsoft.Scripting.Ast.Utils;
using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public sealed class SuiteStatement : Statement {
        private readonly Statement[] _statements;

        public SuiteStatement(Statement[] statements) {
            Assert.NotNull(statements);
            _statements = statements;
        }

        public IList<Statement> Statements {
            get { return _statements; }
        }

        public override MSAst.Expression Reduce() {
            if (_statements.Length == 0) {
                return GlobalParent.AddDebugInfoAndVoid(AstUtils.Empty(), Span);
            }

            ReadOnlyCollectionBuilder<MSAst.Expression> statements = new ReadOnlyCollectionBuilder<MSAst.Expression>();

            int curStart = -1;
            foreach (var statement in _statements) {
                // CPython debugging treats multiple statements on the same line as a single step, we
                // match that behavior here.
                int newline = GlobalParent.IndexToLocation(statement.StartIndex).Line;
                if (newline == curStart) {
                    statements.Add(new DebugInfoRemovalExpression(statement, curStart));
                } else {
                    if (statement.CanThrow && newline != -1) {
                        statements.Add(UpdateLineNumber(newline));
                    }

                    statements.Add(statement);
                }
                curStart = newline;
            }

            return Ast.Block(statements.ToReadOnlyCollection());
        }

        internal class DebugInfoRemovalExpression : MSAst.Expression {
            private MSAst.Expression _inner;
            private int _start;

            public DebugInfoRemovalExpression(MSAst.Expression expression, int line) {
                _inner = expression;
                _start = line;
            }

            public override MSAst.Expression Reduce() {
                return RemoveDebugInfo(_start, _inner.Reduce());
            }

            public override MSAst.ExpressionType NodeType {
                get {
                    return MSAst.ExpressionType.Extension;
                }
            }

            public override System.Type Type {
                get {
                    return _inner.Type;
                }
            }

            public override bool CanReduce {
                get {
                    return true;
                }
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_statements != null) {
                    foreach (Statement s in _statements) {
                        s.Walk(walker);
                    }
                }
            }
            walker.PostWalk(this);
        }

        public override string Documentation {
            get {
                if (_statements.Length > 0) {
                    return _statements[0].Documentation;
                }
                return null;
            }
        }

        internal override bool CanThrow {
            get {
                foreach (Statement stmt in _statements) {
                    if (stmt.CanThrow) {
                        return true;
                    }
                }
                return false;
            }
        }
    }
}
