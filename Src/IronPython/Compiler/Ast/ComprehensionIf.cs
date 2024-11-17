// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Microsoft.Scripting.Actions;

using AstUtils = Microsoft.Scripting.Ast.Utils;
using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {
    public class ComprehensionIf : ComprehensionIterator {
        public ComprehensionIf(Expression test) {
            Test = test;
        }

        public Expression Test { get; }

        internal override MSAst.Expression Transform(MSAst.Expression body) {
            return GlobalParent.AddDebugInfoAndVoid(
                AstUtils.If(
                    GlobalParent.Convert(typeof(bool), ConversionResultKind.ExplicitCast, Test),
                    body
                ),
                Span
            );
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Test?.Walk(walker);
            }
            walker.PostWalk(this);
        }
    }
}
