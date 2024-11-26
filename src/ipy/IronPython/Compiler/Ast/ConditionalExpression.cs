// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

using Microsoft.Scripting.Actions;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    public class ConditionalExpression : Expression {
        public ConditionalExpression(Expression testExpression, Expression trueExpression, Expression falseExpression) {
            Test = testExpression;
            TrueExpression = trueExpression;
            FalseExpression = falseExpression;
        }

        public Expression FalseExpression { get; }

        public Expression Test { get; }

        public Expression TrueExpression { get; }

        public override string NodeName => "conditional expression";

        public override MSAst.Expression Reduce() {
            MSAst.Expression ifTrue = AstUtils.Convert(TrueExpression, typeof(object));
            MSAst.Expression ifFalse = AstUtils.Convert(FalseExpression, typeof(object));

            return Ast.Condition(
                GlobalParent.Convert(typeof(bool), ConversionResultKind.ExplicitCast, Test),
                ifTrue, 
                ifFalse
            );
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Test?.Walk(walker);
                TrueExpression?.Walk(walker);
                FalseExpression?.Walk(walker);
            }
            walker.PostWalk(this);
        }
    }
}
