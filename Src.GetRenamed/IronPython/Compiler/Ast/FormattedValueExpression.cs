// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Diagnostics;

namespace IronPython.Compiler.Ast {
    public class FormattedValueExpression : Expression {
        public FormattedValueExpression(Expression value, char? conversion, JoinedStringExpression? formatSpec) {
            if (value is null) throw new ArgumentNullException(nameof(value));

            Value = value;
            Conversion = conversion;
            FormatSpec = formatSpec;
        }

        public Expression Value { get; }

        public char? Conversion { get; }

        public JoinedStringExpression? FormatSpec { get; }

        public override System.Linq.Expressions.Expression Reduce() {
            System.Linq.Expressions.Expression expr = Convert(Value, typeof(object));

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
                    expr,
                    Convert((System.Linq.Expressions.Expression?)FormatSpec ?? Expression.Constant(string.Empty), typeof(string))
                );
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Value.Walk(walker);
                FormatSpec?.Walk(walker);
            }
            walker.PostWalk(this);
        }
    }
}
