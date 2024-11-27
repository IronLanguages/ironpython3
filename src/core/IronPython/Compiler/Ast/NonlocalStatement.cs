// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using AstUtils = Microsoft.Scripting.Ast.Utils;
using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {
    public class NonlocalStatement : Statement {
        private readonly string[] _names;

        public NonlocalStatement(string[] names) {
            _names = names;
        }

        public IList<string> Names => _names;

        public override MSAst.Expression Reduce() {
            // nonlocal statement is Python's specific syntactic sugar.
            return AstUtils.Empty();
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
            }
            walker.PostWalk(this);
        }

        internal override bool CanThrow => false;
    }
}
