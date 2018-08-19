// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

using IronPython.Runtime;
using Microsoft.Scripting;
using Microsoft.Scripting.Actions;

namespace IronPython.Compiler.Ast {
    public class Arg : Node {
        private readonly string _name;
        private readonly Expression _expression;

        public Arg(Expression expression) : this(null, expression) { }

        public Arg(string name, Expression expression) {
            _name = name;
            _expression = expression;
        }

        public string Name {
            get { return _name; }
        }

        public Expression Expression {
            get { return _expression; }
        } 

        public override string ToString() {
            return base.ToString() + ":" + _name;
        }

        internal Argument GetArgumentInfo() {
            if (_name == null) {
                return Argument.Simple;
            } else if (_name == "*") {
                return new Argument(ArgumentType.List);
            } else if (_name == "**") {
                return new Argument(ArgumentType.Dictionary);
            } else {
                return new Argument(_name);
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                _expression?.Walk(walker);
            }
            walker.PostWalk(this);
        }
    }
}
