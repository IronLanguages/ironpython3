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
using System.Reflection;
using IronPython.Runtime;
using IronPython.Runtime.Binding;
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;
using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class IndexExpression : Expression {
        private readonly Expression _target;
        private readonly Expression _index;

        public IndexExpression(Expression target, Expression index) {
            _target = target;
            _index = index;
        }

        public Expression Target {
            get { return _target; }
        }

        public Expression Index {
            get { return _index; }
        }

        public override MSAst.Expression Reduce() {
            if (IsSlice) {
                return GlobalParent.GetSlice(GetActionArgumentsForGetOrDelete());
            }
            return GlobalParent.GetIndex(GetActionArgumentsForGetOrDelete());
        }

        private MSAst.Expression[] GetActionArgumentsForGetOrDelete() {
            TupleExpression te = _index as TupleExpression;
            if (te != null && te.IsExpandable) {
                return ArrayUtils.Insert(_target, te.Items);
            }

            SliceExpression se = _index as SliceExpression;
            if (se != null) {
                if (se.StepProvided) {
                    return new[] { 
                        _target,
                        GetSliceValue(se.SliceStart),
                        GetSliceValue(se.SliceStop),
                        GetSliceValue(se.SliceStep) 
                    };
                }

                return new[] { 
                    _target,
                    GetSliceValue(se.SliceStart),
                    GetSliceValue(se.SliceStop)
                };
            }

            return new[] { _target, _index };
        }

        private static MSAst.Expression GetSliceValue(Expression expr) {
            if (expr != null) {
                return expr;
            }

            return Ast.Field(null, typeof(MissingParameter).GetField("Value"));
        }

        private MSAst.Expression[] GetActionArgumentsForSet(MSAst.Expression right) {
            return ArrayUtils.Append(GetActionArgumentsForGetOrDelete(), right);
        }

        internal override MSAst.Expression TransformSet(SourceSpan span, MSAst.Expression right, PythonOperationKind op) {
            if (op != PythonOperationKind.None) {
                right = GlobalParent.Operation(
                    typeof(object),
                    op,
                    this,
                    right
                );
            }

            MSAst.Expression index;
            if (IsSlice) {
                index = GlobalParent.SetSlice(GetActionArgumentsForSet(right));
            } else {
                index = GlobalParent.SetIndex(GetActionArgumentsForSet(right));
            }

            return GlobalParent.AddDebugInfoAndVoid(index, Span);
        }

        internal override MSAst.Expression TransformDelete() {
            MSAst.Expression index;
            if (IsSlice) {
                index = GlobalParent.DeleteSlice(GetActionArgumentsForGetOrDelete());
            } else {
                index = GlobalParent.DeleteIndex(GetActionArgumentsForGetOrDelete());
            }

            return GlobalParent.AddDebugInfoAndVoid(index, Span);
        }

        internal override string CheckAssign() {
            return null;
        }

        internal override string CheckDelete() {
            return null;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_target != null) {
                    _target.Walk(walker);
                }
                if (_index != null) {
                    _index.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        private bool IsSlice {
            get {
                return _index is SliceExpression;
            }
        }
    }
}
