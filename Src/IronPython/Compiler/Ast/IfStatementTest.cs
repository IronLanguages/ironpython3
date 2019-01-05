// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Microsoft.Scripting;

namespace IronPython.Compiler.Ast {
    public class IfStatementTest : Node {
        public IfStatementTest(Expression test, Statement body) {
            Test = test;
            Body = body;
        }

        public SourceLocation Header => GlobalParent.IndexToLocation(HeaderIndex);

        public int HeaderIndex { set; get; }

        public Expression Test { get; }

        public Statement Body { get; set; }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Test?.Walk(walker);
                Body?.Walk(walker);
            }
            walker.PostWalk(this);
        }
    }
}
