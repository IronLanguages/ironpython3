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

using Microsoft.Scripting;

namespace IronPython.Compiler.Ast {
    public class IfStatementTest : Node {
        private int _headerIndex;
        private readonly Expression _test;
        private Statement _body;

        public IfStatementTest(Expression test, Statement body) {
            _test = test;
            _body = body;
        }

        public SourceLocation Header {
            get { return GlobalParent.IndexToLocation(_headerIndex); }
        }

        public int HeaderIndex {
            set { _headerIndex = value; }
            get { return _headerIndex; }
        }

        public Expression Test {
            get { return _test; }
        }

        public Statement Body {
            get { return _body; }
            set { _body = value; }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_test != null) {
                    _test.Walk(walker);
                }
                if (_body != null) {
                    _body.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }
    }
}
