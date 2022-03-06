#nullable enable

using System;
using System.Diagnostics;

namespace IronPython.Compiler.Ast {
    public class FormattedValueExpression : Expression {
        public FormattedValueExpression(Expression value, char? conversion, string formatSpec) {
            if (value is null) throw new ArgumentNullException(nameof(value));

            Value = value;
            Conversion = conversion;
            FormatSpec = formatSpec;
        }

        public Expression Value { get; }

        public char? Conversion { get; }

        public string FormatSpec { get; }

        public override System.Linq.Expressions.Expression Reduce() {
            System.Linq.Expressions.Expression expr = Value;

            if (Conversion == 'a') {
                expr = Expression.Call(AstMethods.Ascii, Parent.LocalContext, expr);
            } else if (Conversion == 's') {
                expr = Expression.Call(AstMethods.Str, Parent.LocalContext, expr);
            } else if (Conversion == 'r') {
                expr = Expression.Call(AstMethods.Repr, Parent.LocalContext, expr);
            } else {
                Debug.Assert(Conversion is null);
            }

            return Expression.Call(
                    AstMethods.Format,
                    Parent.LocalContext,
                    Convert(expr, typeof(object)),
                    Expression.Constant(FormatSpec)
                );
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Value.Walk(walker);
            }
            walker.PostWalk(this);
        }
    }
}
