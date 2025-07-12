// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Dynamic;
using System.Linq.Expressions;

using IronPython.Runtime.Operations;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;

    internal class PythonDeleteSliceBinder : DynamicMetaObjectBinder, IPythonSite, IExpressionSerializable {
        private readonly PythonContext/*!*/ _context;

        public PythonDeleteSliceBinder(PythonContext/*!*/ context) {
            _context = context;
        }

        public override DynamicMetaObject Bind(DynamicMetaObject target, DynamicMetaObject[] args) {
            return PythonProtocol.Index(this, PythonIndexType.DeleteSlice, ArrayUtils.Insert(target, args));
        }

        public override int GetHashCode() {
            return base.GetHashCode() ^ _context.Binder.GetHashCode();
        }

        public override bool Equals(object? obj) {
            if (!(obj is PythonDeleteSliceBinder ob)) {
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
                typeof(PythonOps).GetMethod(nameof(PythonOps.MakeDeleteSliceBinder))!,
                BindingHelpers.CreateBinderStateExpression()
            );
        }

        #endregion
    }
}
