// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {

    public class ContinueStatement : Statement {
        private ILoopStatement _loop;

        public ContinueStatement() {
        }

        public override MSAst.Expression Reduce() {
            return GlobalParent.AddDebugInfo(MSAst.Expression.Continue(_loop.ContinueLabel), Span);
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
            }
            walker.PostWalk(this);
        }

        internal override bool CanThrow {
            get {
                return false;
            }
        }

        internal ILoopStatement LoopStatement {
            get {
                return _loop;
            }
            set {
                _loop = value;
            }
        }
    }
}
