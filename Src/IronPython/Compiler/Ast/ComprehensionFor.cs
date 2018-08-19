// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Scripting;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class ComprehensionFor : ComprehensionIterator {
        private readonly Expression _lhs, _list;

        public ComprehensionFor(Expression lhs, Expression list) {
            _lhs = lhs;
            _list = list;
        }

        public Expression Left {
            get { return _lhs; }
        }

        public Expression List {
            get { return _list; }
        }

        internal override MSAst.Expression Transform(MSAst.Expression body) {
            MSAst.ParameterExpression temp = Ast.Parameter(typeof(KeyValuePair<IEnumerator, IDisposable>), "list_comprehension_for");

            return Ast.Block(
                new[] { temp },
                ForStatement.TransformFor(Parent, temp, _list, _lhs, body, null, Span, GlobalParent.IndexToLocation(_lhs.EndIndex), null, null, false)
            );
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                _lhs?.Walk(walker);
                _list?.Walk(walker);
            }
            walker.PostWalk(this);
        }
    }
}
