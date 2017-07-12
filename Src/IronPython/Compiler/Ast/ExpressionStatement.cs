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

using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class ExpressionStatement : Statement {
        private readonly Expression _expression;

        public ExpressionStatement(Expression expression) {
            _expression = expression;
        }

        public Expression Expression {
            get { return _expression; }
        }

        public override MSAst.Expression Reduce() {
            MSAst.Expression expression = _expression;

            return ReduceWorker(expression);
        }

        private MSAst.Expression ReduceWorker(MSAst.Expression expression) {
            if (Parent.PrintExpressions) {
                expression = Ast.Call(
                    AstMethods.PrintExpressionValue,
                    Parent.LocalContext,
                    ConvertIfNeeded(expression, typeof(object))
                );
            }

            return GlobalParent.AddDebugInfoAndVoid(expression, _expression.Span);
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_expression != null) {
                    _expression.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        public override string Documentation {
            get {
                ConstantExpression ce = _expression as ConstantExpression;
                if (ce != null) {
                    return ce.Value as string;
                }
                return null;
            }
        }

        internal override bool CanThrow {
            get {
                return _expression.CanThrow;
            }
        }
    }
}
