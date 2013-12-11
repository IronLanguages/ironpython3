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
using System.Collections.Generic;
using Microsoft.Scripting.Utils;
using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class SetExpression : Expression {
        private readonly Expression[] _items;

        public SetExpression(params Expression[] items) {
            ContractUtils.RequiresNotNull(items, "items");

            _items = items;
        }

        public IList<Expression> Items {
            get { return _items; }
        }

        public override MSAst.Expression Reduce() {
            return Expression.Call(
                AstMethods.MakeSet,
                Ast.NewArrayInit(
                    typeof(object),
                    ArrayUtils.ConvertAll(_items, x => AstUtils.Convert(x, typeof(object)))
                )
            );
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                foreach (Expression s in _items) {
                    s.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }
    }
}
