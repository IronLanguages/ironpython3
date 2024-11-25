// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime {
    [PythonType("method"), DontMapGetMemberNamesToDir]
    public sealed partial class Method : PythonTypeSlot, IWeakReferenceable, IPythonMembersList, IDynamicMetaObjectProvider, ICodeFormattable, Binding.IFastInvokable {
        private WeakRefTracker _weakref;

        // TODO: get rid of this constructor?
        internal Method(object function, object instance, PythonType @class) {
            if (instance == null) throw new ArgumentNullException();
            __func__ = function;
            __self__ = instance;
            im_class = @class;
        }

        public Method(object function, object self) {
            if (self == null) throw PythonOps.TypeError("self must not be None");
            __func__ = function;
            __self__ = self;
            im_class = DynamicHelpers.GetPythonType(self);
        }

        internal string Name => (string)PythonOps.GetBoundAttr(DefaultContext.Default, __func__, "__name__");

        public string __doc__ => PythonOps.GetBoundAttr(DefaultContext.Default, __func__, "__doc__") as string;

        public object __func__ { get; }

        public object __self__ { get; } // TODO: mark that this property is never null

        internal PythonType im_class { get; } // TODO: get rid of this property?

        [SpecialName]
        public object Call(CodeContext/*!*/ context, params object[] args)
            => context.LanguageContext.CallSplat(this, args);

        [SpecialName]
        public object Call(CodeContext/*!*/ context, [ParamDictionary] IDictionary<object, object> kwArgs, params object[] args)
            => context.LanguageContext.CallWithKeywords(this, args, kwArgs);

        #region Object Overrides

        private string DeclaringClassAsString() => im_class == null ? "?" : im_class.Name;

        public override bool Equals(object obj)
            => obj is Method other && PythonOps.IsOrEqualsRetBool(__self__, other.__self__) && PythonOps.EqualRetBool(__func__, other.__func__);

        public override int GetHashCode()
            => PythonOps.Hash(DefaultContext.Default, __self__) ^ PythonOps.Hash(DefaultContext.Default, __func__);

        #endregion

        #region IWeakReferenceable Members

        WeakRefTracker IWeakReferenceable.GetWeakRef() => _weakref;

        bool IWeakReferenceable.SetWeakRef(WeakRefTracker value) {
            _weakref = value;
            return true;
        }

        void IWeakReferenceable.SetFinalizer(WeakRefTracker value) => ((IWeakReferenceable)this).SetWeakRef(value);

        #endregion

        #region Custom member access

        [SpecialName]
        public object GetCustomMember(CodeContext context, string name) {
            switch (name) {
                // Get the module name from the function and pass that out.  Note that CPython's method has
                // no __module__ attribute and this value can be gotten via a call to method.__getattribute__ 
                // there as well.
                case "__module__":
                    return PythonOps.GetBoundAttr(context, __func__, "__module__");
                case "__name__":
                    return PythonOps.GetBoundAttr(DefaultContext.Default, __func__, "__name__");
                default:
                    object value;
                    string symbol = name;
                    if (TypeCache.Method.TryGetBoundMember(context, this, symbol, out value) ||       // look on method
                        PythonOps.TryGetBoundAttr(context, __func__, symbol, out value)) {               // Forward to the func
                        return value;
                    }
                    return OperationFailed.Value;
            }
        }

        [SpecialName]
        public void SetMemberAfter(CodeContext context, string name, object value)
            => TypeCache.Method.SetMember(context, this, name, value);

        [SpecialName]
        public void DeleteMember(CodeContext context, string name)
            => TypeCache.Method.DeleteMember(context, this, name);

        IList<string> IMembersList.GetMemberNames() => PythonOps.GetStringMemberList(this);

        IList<object> IPythonMembersList.GetMemberNames(CodeContext/*!*/ context) {
            PythonList ret = TypeCache.Method.GetMemberNames(context);

            ret.AddNoLockNoDups("__module__");

            if (__func__ is PythonFunction pf) {
                PythonDictionary dict = pf.__dict__;

                // Check the func
                foreach (KeyValuePair<object, object> kvp in dict) {
                    ret.AddNoLockNoDups(kvp.Key);
                }
            }

            return ret;
        }

        #endregion

        #region PythonTypeSlot Overrides

        internal override bool TryGetValue(CodeContext context, object instance, PythonType owner, out object value) {
            value = this;
            return true;
        }

        internal override bool GetAlwaysSucceeds => true;

        #endregion

        #region ICodeFormattable Members

        public string/*!*/ __repr__(CodeContext/*!*/ context) {
            object name;
            if (!PythonOps.TryGetBoundAttr(context, __func__, "__name__", out name)) {
                name = "?";
            }

            return $"<bound method {DeclaringClassAsString()}.{name} of {PythonOps.Repr(context, __self__)}>";
        }

        #endregion

        #region IDynamicMetaObjectProvider Members

        DynamicMetaObject/*!*/ IDynamicMetaObjectProvider.GetMetaObject(Expression/*!*/ parameter)
            => new Binding.MetaMethod(parameter, BindingRestrictions.Empty, this);

        #endregion
    }
}
