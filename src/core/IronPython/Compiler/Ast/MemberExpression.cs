// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

using System;
using System.Dynamic;
using IronPython.Runtime.Binding;
using Microsoft.Scripting;
using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class MemberExpression : Expression {
        public MemberExpression(Expression target, string name) {
            Target = target;
            Name = name;
        }

        public Expression Target { get; }

        public string Name { get; }

        public override string ToString() => base.ToString() + ":" + Name;

        public override MSAst.Expression Reduce() {
            return GlobalParent.Get(
                Name,
                Target
            );
        }

        internal override MSAst.Expression TransformSet(SourceSpan span, MSAst.Expression right, PythonOperationKind op) {
            if (op == PythonOperationKind.None) {
                return GlobalParent.AddDebugInfoAndVoid(
                    GlobalParent.Set(
                        Name,
                        Target,
                        right
                    ),
                    span
                );
            } else {
                MSAst.ParameterExpression temp = Ast.Variable(typeof(object), "inplace");
                return GlobalParent.AddDebugInfo(
                    Ast.Block(
                        new[] { temp },
                        Ast.Assign(temp, Target),
                        SetMemberOperator(right, op, temp),
                        AstUtils.Empty()
                    ),
                    Span.Start,
                    span.End
                );
            }
        }

        internal override string CheckAssign() => null;

        internal override string CheckDelete() => null;

        private MSAst.Expression SetMemberOperator(MSAst.Expression right, PythonOperationKind op, MSAst.ParameterExpression temp) {
            return GlobalParent.Set(
                Name,
                temp,
                GlobalParent.Operation(
                    typeof(object),
                    op,
                    GlobalParent.Get(
                        Name,
                        temp
                    ),
                    right
                )
            );
        }

        internal override MSAst.Expression TransformDelete() {
            return GlobalParent.Delete(
                typeof(void),
                Name,
                Target
            );
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Target?.Walk(walker);
            }
            walker.PostWalk(this);
        }
    }
}
