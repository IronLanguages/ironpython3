// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {
    public class LambdaExpression : Expression {
        public LambdaExpression(FunctionDefinition function) {
            Function = function;
        }

        public FunctionDefinition Function { get; }

        public override MSAst.Expression Reduce() => Function.MakeFunctionExpression();

        public override string NodeName => "lambda";

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Function?.Walk(walker);
            }
            walker.PostWalk(this);
        }
    }
}
