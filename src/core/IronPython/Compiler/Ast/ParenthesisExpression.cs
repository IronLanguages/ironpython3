// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.Scripting;

using IronPython.Runtime.Binding;

using MSAst = System.Linq.Expressions;


namespace IronPython.Compiler.Ast {

    public class ParenthesisExpression : Expression {
        public ParenthesisExpression(Expression expression) {
            Expression = expression;
        }

        public Expression Expression { get; }

        public override MSAst.Expression Reduce() => Expression;

        internal override MSAst.Expression TransformSet(SourceSpan span, MSAst.Expression right, PythonOperationKind op)
            => Expression.TransformSet(span, right, op);

        internal override string CheckAssign() => Expression.CheckAssign();

        internal override string CheckAugmentedAssign() => Expression.CheckAugmentedAssign();

        internal override string CheckDelete() => Expression.CheckDelete();

        internal override MSAst.Expression TransformDelete() => Expression.TransformDelete();

        public override Type Type => Expression.Type;

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Expression?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        internal override bool CanThrow => Expression.CanThrow;
    }
}
