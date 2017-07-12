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

using System;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

using IronPython.Runtime;
using IronPython.Runtime.Binding;
using IronPython.Runtime.Operations;

using MSAst = System.Linq.Expressions;


namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class TupleExpression : SequenceExpression {
        private bool _expandable;

        public TupleExpression(bool expandable, params Expression[] items)
            : base(items) {
            _expandable = expandable;
        }

        internal override string CheckAssign() {
            if (Items.Count == 0) {
                return "can't assign to ()";
            }
            for (int i = 0; i < Items.Count; i++) {
                Expression e = Items[i];
                if (e.CheckAssign() != null) {
                    // we don't return the same message here as CPython doesn't seem to either, 
                    // for example ((yield a), 2,3) = (2,3,4) gives a different error than
                    // a = yield 3 = yield 4.
                    return "can't assign to " + e.NodeName;
                }
            }
            return null;
        }

        public override MSAst.Expression Reduce() {
            if (_expandable) {
                return Ast.NewArrayInit(
                    typeof(object),
                    ToObjectArray(Items)
                );
            }

            if (Items.Count == 0) {
                return Ast.Field(
                    null,
                    typeof(PythonOps).GetField("EmptyTuple")
                );
            }

            return Ast.Call(
                AstMethods.MakeTuple,
                Ast.NewArrayInit(
                    typeof(object),
                    ToObjectArray(Items)
                )
            );
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (Items != null) {
                    foreach (Expression e in Items) {
                        e.Walk(walker);
                    }
                }
            }
            walker.PostWalk(this);
        }

        public bool IsExpandable {
            get {
                return _expandable;
            }
        }

        internal override bool IsConstant {
            get {
                foreach (var item in Items) {
                    if (!item.IsConstant) {
                        return false;
                    }
                }
                return true;
            }
        }

        internal override object GetConstantValue() {
            if (Items.Count == 0) {
                return PythonTuple.EMPTY;
            }

            object[] items = new object[Items.Count];
            for (int i = 0; i < items.Length; i++) {
                items[i] = Items[i].GetConstantValue();
            }

            return PythonOps.MakeTuple(items);
        }
    }
}
