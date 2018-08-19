// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

using System;
using IronPython.Runtime.Binding;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Utils;
using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class AndExpression : Expression {
        private readonly Expression _left, _right;

        public AndExpression(Expression left, Expression right) {
            ContractUtils.RequiresNotNull(left, nameof(left));
            ContractUtils.RequiresNotNull(right, nameof(right));

            _left = left;
            _right = right;
            StartIndex = left.StartIndex;
            EndIndex = right.EndIndex;
        }

        public Expression Left {
            get { return _left; }
        }

        public Expression Right {
            get { return _right; }
        } 

        public override MSAst.Expression Reduce() {
            MSAst.Expression left = _left;
            MSAst.Expression right = _right;

            Type t = Type;
            MSAst.ParameterExpression tmp = Ast.Variable(t, "__all__");

            return Block(
                new [] { tmp },
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
                return _left.Type == _right.Type ? _left.Type : typeof(object);
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                _left?.Walk(walker);
                _right?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        internal override bool CanThrow {
            get {
                return _left.CanThrow || _right.CanThrow;
            }
        }
    }
}
