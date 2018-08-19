// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

using System;

namespace IronPython.Compiler.Ast {
    public class LambdaExpression : Expression {
        private readonly FunctionDefinition _function;

        public LambdaExpression(FunctionDefinition function) {
            _function = function;
        }

        public FunctionDefinition Function {
            get { return _function; }
        }

        public override MSAst.Expression Reduce() {
            return _function.MakeFunctionExpression();
        }

        public override string NodeName => "lambda";

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                _function?.Walk(walker);
            }
            walker.PostWalk(this);
        }
    }
}
