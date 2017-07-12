/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

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
            ContractUtils.RequiresNotNull(left, "left");
            ContractUtils.RequiresNotNull(right, "right");

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

            return Ast.Block(
                new [] { tmp },
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
                if (_left != null) {
                    _left.Walk(walker);
                }
                if (_right != null) {
                    _right.Walk(walker);
                }
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
