// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.Scripting;

using IronPython.Runtime.Binding;

using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {

    public class StarredExpression : Expression {
        public StarredExpression(Expression value) {
            Value = value;
        }

        public Expression Value { get; }

        public override MSAst.Expression Reduce() => Value;

        internal override MSAst.Expression TransformSet(SourceSpan span, MSAst.Expression right, PythonOperationKind op)
            => Value.TransformSet(span, right, op);

        internal override string CheckAssign() => Value.CheckAssign();

        internal override string CheckDelete() => Value.CheckDelete();

        internal override MSAst.Expression TransformDelete() => Value.TransformDelete();

        public override Type Type => Value.Type;

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Value?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        internal override bool CanThrow => Value.CanThrow;
    }
}
