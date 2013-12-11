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
                if (_lhs != null) {
                    _lhs.Walk(walker);
                }
                if (_list != null) {
                    _list.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }
    }
}
