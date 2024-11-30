// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class SliceExpression : Expression {
        public SliceExpression(Expression? start, Expression? stop, Expression? step) {
            SliceStart = start;
            SliceStop = stop;
            SliceStep = step;
        }

        public Expression? SliceStart { get; }

        public Expression? SliceStop { get; }

        public Expression? SliceStep { get; }

        public override MSAst.Expression Reduce() {
            return Call(
                AstMethods.MakeSlice,                                    // method
                TransformOrConstantNull(SliceStart, typeof(object)),    // parameters
                TransformOrConstantNull(SliceStop, typeof(object)),
                TransformOrConstantNull(SliceStep, typeof(object))
            );
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                SliceStart?.Walk(walker);
                SliceStop?.Walk(walker);
                SliceStep?.Walk(walker);
            }
            walker.PostWalk(this);
        }
    }
}
