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

using System.Diagnostics;
using Microsoft.Scripting;
using IronPython.Runtime;
using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class ExecStatement : Statement {
        private readonly Expression _code, _locals, _globals;

        public ExecStatement(Expression code, Expression locals, Expression globals) {
            _code = code;
            _locals = locals;
            _globals = globals;
        }

        public Expression Code {
            get { return _code; }
        }

        public Expression Locals {
            get { return _locals; }
        }

        public Expression Globals {
            get { return _globals; }
        }

        public bool NeedsLocalsDictionary() {
            return _globals == null && _locals == null;
        }

        public override MSAst.Expression Reduce() {
            MSAst.MethodCallExpression call;

            if (_locals == null && _globals == null) {
                // exec code
                call = Ast.Call(
                    AstMethods.UnqualifiedExec,
                    Parent.LocalContext,
                    AstUtils.Convert(_code, typeof(object))
                );
            } else {
                // exec code in globals [ , locals ]
                // We must have globals now (locals is last and may be absent)
                Debug.Assert(_globals != null);
                call = Ast.Call(
                    AstMethods.QualifiedExec,
                    Parent.LocalContext,
                    AstUtils.Convert(_code, typeof(object)),
                    TransformAndDynamicConvert(_globals, typeof(PythonDictionary)),
                    TransformOrConstantNull(_locals, typeof(object))
                );
            }

            return GlobalParent.AddDebugInfo(call, Span);
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_code != null) {
                    _code.Walk(walker);
                }
                if (_locals != null) {
                    _locals.Walk(walker);
                }
                if (_globals != null) {
                    _globals.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }
    }
}
