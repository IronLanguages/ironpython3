// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    public class ReturnStatement : Statement {
        public ReturnStatement(Expression expression) {
            Expression = expression;
        }

        public Expression Expression { get; }

        public override MSAst.Expression Reduce() {
            if (Parent.IsGeneratorMethod) {
                if (Expression == null) {
                    return GlobalParent.AddDebugInfo(AstUtils.YieldBreak(GeneratorLabel), Span);
                }
                // Reduce to a yield return with a marker of -2, this will be interpreted as a yield break with a return value
                return GlobalParent.AddDebugInfo(AstUtils.YieldReturn(GeneratorLabel, TransformOrConstantNull(Expression, typeof(object)), -2), Span);
            }

            return GlobalParent.AddDebugInfo(
                Ast.Return(
                    FunctionDefinition._returnLabel,
                    TransformOrConstantNull(Expression, typeof(object))
                ),
                Span
            );
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Expression?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        internal override bool CanThrow {
            get {
                if (Expression == null) {
                    return false;
                }

                return Expression.CanThrow;
            }
        }
    }
}
