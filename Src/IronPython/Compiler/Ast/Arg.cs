// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

using IronPython.Runtime;
using Microsoft.Scripting;
using Microsoft.Scripting.Actions;

namespace IronPython.Compiler.Ast {
    public class Arg : Node {
        public Arg(Expression expression) : this(null, expression) { }

        public Arg(string name, Expression expression) {
            Name = name;
            Expression = expression;
        }

        public string Name { get; }

        public Expression Expression { get; }

        public override string ToString() {
            return base.ToString() + ":" + Name;
        }

        internal Argument GetArgumentInfo() {
            if (Name == null) {
                return Argument.Simple;
            } else if (Name == "*") {
                return new Argument(ArgumentType.List);
            } else if (Name == "**") {
                return new Argument(ArgumentType.Dictionary);
            } else {
                return new Argument(Name);
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Expression?.Walk(walker);
            }
            walker.PostWalk(this);
        }
    }
}
