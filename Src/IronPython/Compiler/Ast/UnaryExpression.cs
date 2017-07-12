/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

using MSAst = System.Linq.Expressions;

using System;
using System.Diagnostics;
using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Binding;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    public class UnaryExpression : Expression {
        private readonly Expression _expression;
        private readonly PythonOperator _op;

        public UnaryExpression(PythonOperator op, Expression expression) {
            _op = op;
            _expression = expression;
            EndIndex = expression.EndIndex;
        }

        public Expression Expression {
            get { return _expression; }
        }

        public PythonOperator Op {
            get { return _op; }
        }

        public override MSAst.Expression Reduce() {
            return GlobalParent.Operation(
                typeof(object),
                PythonOperatorToOperatorString(_op),
                _expression
            );
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_expression != null) {
                    _expression.Walk(walker);
                }
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
