// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;


using System;
using System.Collections.Generic;

using Microsoft.Scripting;

using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {

    public class GlobalStatement : Statement {
        private readonly string[] _names;

        public GlobalStatement(string[] names) {
            _names = names;
        }

        public IList<string> Names {
            get { return _names; }
        }

        public override MSAst.Expression Reduce() {
            // global statement is Python's specific syntactic sugar.
            return AstUtils.Empty();
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
    }
}
