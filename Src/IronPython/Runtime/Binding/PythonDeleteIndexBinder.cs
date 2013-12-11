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
using System.Linq.Expressions;
#else
using Microsoft.Scripting.Ast;
#endif

using System;
using System.Dynamic;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;
using AstUtils = Microsoft.Scripting.Ast.Utils;

using IronPython.Runtime.Operations;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;

    class PythonDeleteIndexBinder : DeleteIndexBinder, IPythonSite, IExpressionSerializable {
        private readonly PythonContext/*!*/ _context;

        public PythonDeleteIndexBinder(PythonContext/*!*/ context, int argCount)
            : base(new CallInfo(argCount)) {
            _context = context;
        }

        public override DynamicMetaObject FallbackDeleteIndex(DynamicMetaObject target, DynamicMetaObject[] indexes, DynamicMetaObject errorSuggestion) {
            return PythonProtocol.Index(this, PythonIndexType.DeleteItem, ArrayUtils.Insert(target, indexes),errorSuggestion);
        }

        public override int GetHashCode() {
            return base.GetHashCode() ^ _context.Binder.GetHashCode();
        }

        public override bool Equals(object obj) {
            PythonDeleteIndexBinder ob = obj as PythonDeleteIndexBinder;
            if (ob == null) {
                return false;
            }

            return ob._context.Binder == _context.Binder && base.Equals(obj);
        }

        #region IPythonSite Members

        public PythonContext/*!*/ Context {
            get { return _context; }
        }

        #endregion

        #region IExpressionSerializable Members

        public Expression/*!*/ CreateExpression() {
            return Ast.Call(
                typeof(PythonOps).GetMethod("MakeDeleteIndexAction"),
                BindingHelpers.CreateBinderStateExpression(),
                AstUtils.Constant(CallInfo.ArgumentCount)
            );
        }

        #endregion
    }
}
