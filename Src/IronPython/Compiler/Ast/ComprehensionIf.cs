// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

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
                _test?.Walk(walker);
            }
            walker.PostWalk(this);
        }
    }
}
