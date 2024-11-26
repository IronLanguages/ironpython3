// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = System.Linq.Expressions.Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    public class AndExpression : Expression {
        public AndExpression(Expression left, Expression right) {
            ContractUtils.RequiresNotNull(left, nameof(left));
            ContractUtils.RequiresNotNull(right, nameof(right));

            Left = left;
            Right = right;
            StartIndex = left.StartIndex;
            EndIndex = right.EndIndex;
        }

        public Expression Left { get; }

        public Expression Right { get; }

        public override Ast Reduce() {
            var left = Left;
            var right = Right;

            Type t = Type;
            var tmp = Variable(t, "__all__");

            return Block(
                new[] { tmp },
                Condition(
                    GlobalParent.Convert(
                        typeof(bool),
                        ConversionResultKind.ExplicitCast,
                        Assign(
                            tmp,
                            AstUtils.Convert(
                                left,
                                t
                            )
                        )
                    ),
                    AstUtils.Convert(
                        right,
                        t
                    ),
                    tmp
                )
            );
        }

        public override Type Type {
            get {
                Type leftType = Left.Type;
                return leftType == Right.Type ? leftType : typeof(object);
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Left?.Walk(walker);
                Right?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        internal override bool CanThrow => Left.CanThrow || Right.CanThrow;
    }
}
