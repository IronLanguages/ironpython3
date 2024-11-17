// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {

    public class ContinueStatement : Statement {
        public ContinueStatement() {
        }

        public override MSAst.Expression Reduce() {
            return GlobalParent.AddDebugInfo(MSAst.Expression.Continue(LoopStatement.ContinueLabel), Span);
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
            }
            walker.PostWalk(this);
        }

        internal override bool CanThrow => false;

        internal ILoopStatement LoopStatement { get; set; }
    }
}
