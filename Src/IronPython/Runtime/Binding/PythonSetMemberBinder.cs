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
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using AstUtils = Microsoft.Scripting.Ast.Utils;

using IronPython.Runtime.Binding;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;
    using System.Runtime.CompilerServices;

    class PythonSetMemberBinder : SetMemberBinder, IPythonSite, IExpressionSerializable {
        private readonly PythonContext/*!*/ _context;

        public PythonSetMemberBinder(PythonContext/*!*/ context, string/*!*/ name)
            : base(name, false) {
            _context = context;
        }

        public PythonSetMemberBinder(PythonContext/*!*/ context, string/*!*/ name, bool ignoreCase)
            : base(name, ignoreCase) {
            _context = context;
        }

        public override DynamicMetaObject FallbackSetMember(DynamicMetaObject self, DynamicMetaObject value, DynamicMetaObject errorSuggestion) {
            if (self.NeedsDeferral()) {
                return Defer(self, value);
            }
#if FEATURE_COM
            DynamicMetaObject com;
            if (Microsoft.Scripting.ComInterop.ComBinder.TryBindSetMember(this, self, BindingHelpers.GetComArgument(value), out com)) {
                return com;
            }
#endif
            return Context.Binder.SetMember(Name, self, value, errorSuggestion, new PythonOverloadResolverFactory(_context.Binder, AstUtils.Constant(Context.SharedContext)));
        }

        public override T BindDelegate<T>(CallSite<T> site, object[] args) {
            IFastSettable fastSet = args[0] as IFastSettable;
            if (fastSet != null) {
                T res = fastSet.MakeSetBinding<T>(site, this);
                if (res != null) {
                    return res;
                }
            }

            IPythonObject ipo = args[0] as IPythonObject;
            if (ipo != null && !(ipo is IProxyObject)) {
                FastBindResult<T> res = UserTypeOps.MakeSetBinding<T>(Context.SharedContext, site, ipo, args[1], this);

                if (res.Target != null) {
                    PerfTrack.NoteEvent(PerfTrack.Categories.BindingFast, "IPythonObject");

                    if (res.ShouldCache) {
                        CacheTarget(res.Target);
                    }
                    return res.Target;
                }

                PerfTrack.NoteEvent(PerfTrack.Categories.BindingSlow, "IPythonObject Set");
            }

            return base.BindDelegate(site, args);
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
            PythonSetMemberBinder ob = obj as PythonSetMemberBinder;
            if (ob == null) {
                return false;
            }

            return ob._context.Binder == _context.Binder && base.Equals(obj);
        }

        public override string ToString() {
            return "Python SetMember " + Name;
        }

        #region IExpressionSerializable Members

        public Expression CreateExpression() {
            return Ast.Call(
                typeof(PythonOps).GetMethod("MakeSetAction"),
                BindingHelpers.CreateBinderStateExpression(),
                AstUtils.Constant(Name)
            );
        }

        #endregion

    }
}

