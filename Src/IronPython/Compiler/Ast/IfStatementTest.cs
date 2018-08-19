// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

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
                _test?.Walk(walker);
                _body?.Walk(walker);
            }
            walker.PostWalk(this);
        }
    }
}
