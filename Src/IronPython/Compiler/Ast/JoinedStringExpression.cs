#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace IronPython.Compiler.Ast {
    public class JoinedStringExpression : Expression {
        public JoinedStringExpression(IReadOnlyList<Expression>? values) {
            _values = values?.ToArray() ?? Array.Empty<Expression>();
        }

        private Expression[] _values;
        public IReadOnlyList<Expression> Values => _values;

        private static readonly MethodInfo concat = ((Func<string[], string>)string.Concat).Method;

        public override System.Linq.Expressions.Expression Reduce()
            => Expression.Call(concat, NewArrayInit(typeof(string), ToExpressionArray<string>(_values)));

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                foreach (var arg in Values) {
                    arg.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }
    }
}
