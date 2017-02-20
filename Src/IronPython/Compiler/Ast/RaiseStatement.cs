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

using System;
using System.Diagnostics;

using MSAst = System.Linq.Expressions;
using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class RaiseStatement : Statement {
        public RaiseStatement(Expression exception, Expression cause) {
            Exception = exception;
            Cause = cause;
        }

        public Expression Exception { get; }

        public Expression Cause { get; }

        public override MSAst.Expression Reduce() {
            MSAst.Expression raiseExpression;
            if (Exception == null) {
                Debug.Assert(Cause == null);
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
            } else {
                raiseExpression = Ast.Call(
                    AstMethods.MakeException,
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
