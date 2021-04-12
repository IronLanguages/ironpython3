// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using Microsoft.Scripting.Actions;

namespace IronPython.Compiler.Ast {
    public class Arg : Node {
        private static readonly Argument _listTypeArgument = new(ArgumentType.List);
        private static readonly Argument _dictTypeArgument = new(ArgumentType.Dictionary);

        public Arg(Expression expression) : this(null, expression) { }

        public Arg(string? name, Expression expression) {
            Name = name;
            Expression = expression;

            ArgumentInfo = name switch {
                null => Argument.Simple,
                "*"  => _listTypeArgument,
                "**" => _dictTypeArgument,
                _    => new Argument(name)
            };
        }

        public string? Name { get; }

        public Expression Expression { get; }

        internal Argument ArgumentInfo { get; }

        public override string ToString() {
            return base.ToString() + ":" + Name;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Expression?.Walk(walker);
            }
            walker.PostWalk(this);
        }
    }
}
