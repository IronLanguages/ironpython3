// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

using System;
using System.Collections;
using System.Collections.Generic;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class ComprehensionFor : ComprehensionIterator {
        public ComprehensionFor(Expression lhs, Expression list) {
            Left = lhs;
            List = list;
        }

        public Expression Left { get; }

        public Expression List { get; }

        internal override MSAst.Expression Transform(MSAst.Expression body) {
            MSAst.ParameterExpression temp = Ast.Parameter(typeof(KeyValuePair<IEnumerator, IDisposable>), "list_comprehension_for");

            return Ast.Block(
                new[] { temp },
                ForStatement.TransformFor(Parent, temp, List, Left, body, null, Span, GlobalParent.IndexToLocation(Left.EndIndex), null, null, false)
            );
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Left?.Walk(walker);
                List?.Walk(walker);
            }
            walker.PostWalk(this);
        }
    }
}
