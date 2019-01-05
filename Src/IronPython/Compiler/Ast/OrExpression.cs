// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

using System;
using IronPython.Runtime.Binding;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    public class OrExpression : Expression {
        public OrExpression(Expression left, Expression right) {
            ContractUtils.RequiresNotNull(left, nameof(left));
            ContractUtils.RequiresNotNull(right, nameof(right));

            Left = left;
            Right = right;
            StartIndex = left.StartIndex;
            EndIndex = right.EndIndex;
        }

        public Expression Left { get; }
        public Expression Right { get; }

        public override MSAst.Expression Reduce() {
            MSAst.Expression left = Left;
            MSAst.Expression right = Right;

            Type t = Type;
            MSAst.ParameterExpression tmp = Ast.Variable(t, "__all__");

            return Ast.Block(
                new[] { tmp },
                Ast.Condition(
                    GlobalParent.Convert(
                        typeof(bool),
                        ConversionResultKind.ExplicitCast,
                        Ast.Assign(
                            tmp,
                            AstUtils.Convert(
                                left,
                                t
                            )
                        )
                    ),
                    tmp,
                    AstUtils.Convert(
                        right,
                        t
                    )
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
