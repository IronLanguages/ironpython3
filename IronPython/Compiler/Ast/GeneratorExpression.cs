// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class GeneratorExpression : Expression {
        public GeneratorExpression(FunctionDefinition function, Expression iterable) {
            Function = function;
            Iterable = iterable;
        }

        public override MSAst.Expression Reduce() {
            return Ast.Call(
                AstMethods.MakeGeneratorExpression,
                Function.MakeFunctionExpression(),
                Iterable
            );
        }

        public FunctionDefinition Function { get; }

        public Expression Iterable { get; }

        public override string NodeName => "generator expression";

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Function.Walk(walker);
                Iterable.Walk(walker);
            }
            walker.PostWalk(this);
        }
    }
}
