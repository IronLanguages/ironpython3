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

using Microsoft.Scripting.Actions;

using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class ComprehensionIf : ComprehensionIterator {
        private readonly Expression _test;

        public ComprehensionIf(Expression test) {
            _test = test;
        }

        public Expression Test {
            get { return _test; }
        }

        internal override MSAst.Expression Transform(MSAst.Expression body) {
            return GlobalParent.AddDebugInfoAndVoid(
                AstUtils.If(
                    GlobalParent.Convert(typeof(bool), ConversionResultKind.ExplicitCast, _test),
                    body
                ),
                Span
            );
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_test != null) {
                    _test.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }
    }
}
