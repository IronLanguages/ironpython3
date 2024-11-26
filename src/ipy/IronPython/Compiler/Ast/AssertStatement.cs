// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

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
