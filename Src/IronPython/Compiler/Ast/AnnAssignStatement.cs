// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using IronPython.Runtime.Binding;

using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {

    public class AnnAssignStatement : Statement {
        public AnnAssignStatement(Expression target, Expression annotation, Expression? value, bool simple) {
            Target = target;
            Annotation = annotation;
            Value = value;
            Simple = simple;
        }

        public Expression Target { get; }

        public Expression Annotation { get; }

        public Expression? Value { get; }

        public bool Simple { get; }

        public override MSAst.Expression Reduce() {
            if (Value is null) {
                return Empty();
            } else {
                return Target.TransformSet(Span, Value, PythonOperationKind.None);
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Target.Walk(walker);
                Annotation.Walk(walker);
                Value?.Walk(walker);
            }
            walker.PostWalk(this);
        }
    }
}
