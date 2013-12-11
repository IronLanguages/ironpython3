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

#if FEATURE_CORE_DLR
using MSAst = System.Linq.Expressions;
#else
using MSAst = Microsoft.Scripting.Ast;
#endif

using System;

using Microsoft.Scripting.Actions;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    public class ConditionalExpression : Expression {
        private readonly Expression _testExpr;
        private readonly Expression _trueExpr;
        private readonly Expression _falseExpr;

        public ConditionalExpression(Expression testExpression, Expression trueExpression, Expression falseExpression) {
            this._testExpr = testExpression;
            this._trueExpr = trueExpression;
            this._falseExpr = falseExpression;
        }

        public Expression FalseExpression {
            get { return _falseExpr; }
        }

        public Expression Test {
            get { return _testExpr; }
        }

        public Expression TrueExpression {
            get { return _trueExpr; }
        }

        public override MSAst.Expression Reduce() {
            MSAst.Expression ifTrue = AstUtils.Convert(_trueExpr, typeof(object));
            MSAst.Expression ifFalse = AstUtils.Convert(_falseExpr, typeof(object));

            return Ast.Condition(
                GlobalParent.Convert(typeof(bool), ConversionResultKind.ExplicitCast, _testExpr), 
                ifTrue, 
                ifFalse
            );
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_testExpr != null) {
                    _testExpr.Walk(walker);
                }
                if (_trueExpr != null) {
                    _trueExpr.Walk(walker);
                }
                if (_falseExpr != null) {
                    _falseExpr.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }
    }
}
