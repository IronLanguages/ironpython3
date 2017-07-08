using MSAst = System.Linq.Expressions;


using System;
using System.Collections.Generic;

using Microsoft.Scripting;

using AstUtils = Microsoft.Scripting.Ast.Utils;


namespace IronPython.Compiler.Ast {
    public class NonlocalStatement : Statement {
        private readonly string[] _names;

        public NonlocalStatement(string[] names) {
            _names = names;
        }

        public IList<string> Names {
            get { return _names; }
        }

        public override MSAst.Expression Reduce() {
            // nonlocal statement is Python's specific syntactic sugar.
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
