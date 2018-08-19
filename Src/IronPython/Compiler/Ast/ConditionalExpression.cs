// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

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
            _testExpr = testExpression;
            _trueExpr = trueExpression;
            _falseExpr = falseExpression;
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

        public override string NodeName => "conditional expression";

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
                _testExpr?.Walk(walker);
                _trueExpr?.Walk(walker);
                _falseExpr?.Walk(walker);
            }
            walker.PostWalk(this);
        }
    }
}
