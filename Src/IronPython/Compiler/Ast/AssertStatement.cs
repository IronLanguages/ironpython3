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

#if FEATURE_CORE_DLR
using MSAst = System.Linq.Expressions;
#else
using MSAst = Microsoft.Scripting.Ast;
#endif

using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class AssertStatement : Statement {
        private readonly Expression _test, _message;

        public AssertStatement(Expression test, Expression message) {
            _test = test;
            _message = message;
        }

        public Expression Test {
            get { return _test; }
        }

        public Expression Message {
            get { return _message; }
        }

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
                    TransformAndDynamicConvert(_test, typeof(bool)), // _test
                    Ast.Call(                                           // else branch
                        AstMethods.RaiseAssertionError,
                        TransformOrConstantNull(_message, typeof(object))
                    )
                ),
                Span
            );
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_test != null) {
                    _test.Walk(walker);
                }
                if (_message != null) {
                    _message.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }
    }
}
