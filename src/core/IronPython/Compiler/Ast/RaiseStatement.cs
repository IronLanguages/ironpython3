// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Diagnostics;

using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class RaiseStatement : Statement {
        public RaiseStatement(Expression? exception, Expression? cause) {
            if (exception is null && cause is not null) throw new ArgumentNullException(nameof(exception));
            Exception = exception;
            Cause = cause;
        }

        public Expression? Exception { get; }

        public Expression? Cause { get; }

        public override MSAst.Expression Reduce() {
            MSAst.Expression raiseExpression;
            if (Exception is null) {
                Debug.Assert(Cause is null);
                raiseExpression = Ast.Call(
                    AstMethods.MakeRethrownException,
                    Parent.LocalContext
                );

                if (!InFinally) {
                    raiseExpression = Ast.Block(
                        UpdateLineUpdated(true),
                        raiseExpression
                    );
                }
            } else if (Cause is null) {
                raiseExpression = Ast.Call(
                    AstMethods.MakeException,
                    Parent.LocalContext,
                    TransformOrConstantNull(Exception, typeof(object))
                );
            } else {
                raiseExpression = Ast.Call(
                    AstMethods.MakeExceptionWithCause,
                    Parent.LocalContext,
                    TransformOrConstantNull(Exception, typeof(object)),
                    TransformOrConstantNull(Cause, typeof(object))
                );
            }

            return GlobalParent.AddDebugInfo(
                Ast.Throw(raiseExpression),
                Span
            );
        }

        internal bool InFinally { get; set; }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Exception?.Walk(walker);
                Cause?.Walk(walker);
            }
            walker.PostWalk(this);
        }
    }
}
