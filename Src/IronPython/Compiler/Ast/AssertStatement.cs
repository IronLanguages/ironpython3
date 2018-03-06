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

using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class AssertStatement : Statement {
        
        public AssertStatement(Expression test, Expression message) {
            Test = test;
            Message = message;
        }

        public Expression Test { get; }

        public Expression Message { get; }

        public override MSAst.Expression Reduce() {
            // If debugging is off, return empty statement
            if (Optimize) {
                return AstUtils.Empty();
            }

            // Transform into:
            // if (_test) {
            // } else {
            //     RaiseAssertionError(_message);
            // }
            return GlobalParent.AddDebugInfoAndVoid(
                AstUtils.Unless(                                 // if
                    TransformAndDynamicConvert(Test, typeof(bool)), // _test
                    Message == null ? // else branch
                        Ast.Call(
                            AstMethods.RaiseAssertionErrorNoMessage,
                            Parent.LocalContext
                        ) :
                        Ast.Call(
                            AstMethods.RaiseAssertionError,
                            Parent.LocalContext,
                            TransformOrConstantNull(Message, typeof(object))
                        )
                ),
                Span
            );
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Test?.Walk(walker);
                Message?.Walk(walker);
            }
            walker.PostWalk(this);
        }
    }
}
