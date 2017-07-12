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
