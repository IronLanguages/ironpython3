// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

using System;
using System.Diagnostics;

using IronPython.Runtime.Binding;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    public class UnaryExpression : Expression {
        public UnaryExpression(PythonOperator op, Expression expression) {
            Operator = op;
            Expression = expression;
            EndIndex = expression.EndIndex;
        }

        public Expression Expression { get; }

        public PythonOperator Operator { get; }

        public override MSAst.Expression Reduce() {
            return GlobalParent.Operation(
                typeof(object),
                PythonOperatorToOperatorString(Operator),
                Expression
            );
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Expression?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        private static PythonOperationKind PythonOperatorToOperatorString(PythonOperator op) {
            switch (op) {
                // Unary
                case PythonOperator.Not:
                    return PythonOperationKind.Not;
                case PythonOperator.Pos:
                    return PythonOperationKind.Positive;
                case PythonOperator.Invert:
                    return PythonOperationKind.OnesComplement;
                case PythonOperator.Negate:
                    return PythonOperationKind.Negate;
                default:
                    Debug.Assert(false, "Unexpected PythonOperator: " + op.ToString());
                    return PythonOperationKind.None;
            }
        }
    }
}
