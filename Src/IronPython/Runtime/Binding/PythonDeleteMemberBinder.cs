// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;

using System;
using Microsoft.Scripting;
using System.Dynamic;
using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Binding;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    internal class PythonDeleteMemberBinder : DeleteMemberBinder, IPythonSite, IExpressionSerializable {
        private readonly PythonContext/*!*/ _context;

        public PythonDeleteMemberBinder(PythonContext/*!*/ context, string/*!*/ name)
            : base(name, false) {
            _context = context;
        }

        public PythonDeleteMemberBinder(PythonContext/*!*/ context, string/*!*/ name, bool ignoreCase)
            : base(name, ignoreCase) {
            _context = context;
        }

        public override DynamicMetaObject FallbackDeleteMember(DynamicMetaObject self, DynamicMetaObject errorSuggestion) {
            if (self.NeedsDeferral()) {
                return Defer(self);
            }

            return Context.Binder.DeleteMember(Name, self, new PythonOverloadResolverFactory(_context.Binder, AstUtils.Constant(Context.SharedContext)), errorSuggestion);
        }

        public PythonContext/*!*/ Context {
            get {
                return _context;
            }
        }

        public override int GetHashCode() {
            return base.GetHashCode() ^ _context.Binder.GetHashCode();
        }

        public override bool Equals(object obj) {
            PythonDeleteMemberBinder ob = obj as PythonDeleteMemberBinder;
            if (ob == null) {
                return false;
            }

            return ob._context.Binder == _context.Binder && base.Equals(obj);
        }

        public override string ToString() {
            return "Python DeleteMember " + Name;
        }

        #region IExpressionSerializable Members

        public Expression CreateExpression() {
            return Ast.Call(
                typeof(PythonOps).GetMethod(nameof(PythonOps.MakeDeleteAction)),
                BindingHelpers.CreateBinderStateExpression(),
                AstUtils.Constant(Name)
            );
        }

        #endregion
    }
}

