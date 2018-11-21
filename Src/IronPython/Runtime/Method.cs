// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime {

    [PythonType("method"), DontMapGetMemberNamesToDir]
    public sealed partial class Method : PythonTypeSlot, IWeakReferenceable, IPythonMembersList, IDynamicMetaObjectProvider, ICodeFormattable, Binding.IFastInvokable {
        private readonly object _declaringClass;
        private WeakRefTracker _weakref;

        public Method(object function, object instance, object @class) {
            __func__ = function;
            __self__ = instance;
            _declaringClass = @class;
        }

        public Method(object function, object instance) {
            if (instance == null) {
                throw PythonOps.TypeError("unbound methods must have a class provided");
            }

            __func__ = function;
            __self__ = instance;
        }

        internal string Name {
            get { return (string)PythonOps.GetBoundAttr(DefaultContext.Default, __func__, "__name__"); }
        }

        public string __doc__ {
            get {
                return PythonOps.GetBoundAttr(DefaultContext.Default, __func__, "__doc__") as string;
            }
        }

        public object __func__ { get; }

        public object __self__ { get; }

        internal object im_class {
            get {
                // we could have an OldClass (or any other object) here if the user called the ctor directly
                return PythonOps.ToPythonType(_declaringClass as PythonType) ?? _declaringClass;
            }
        }

        [SpecialName]
        public object Call(CodeContext/*!*/ context, params object[] args) {
            return context.LanguageContext.CallSplat(this, args);
        }

        [SpecialName]
        public object Call(CodeContext/*!*/ context, [ParamDictionary]IDictionary<object, object> kwArgs, params object[] args) {
            return context.LanguageContext.CallWithKeywords(this, args, kwArgs);
        }

        private Exception BadSelf(object got) {

            string firstArg;
            if (got == null) {
                firstArg = "nothing";
            } else {
                firstArg = PythonOps.GetPythonTypeName(got) + " instance";
            }
            PythonType pt = im_class as PythonType;

            return PythonOps.TypeError("unbound method {0}() must be called with {1} instance as first argument (got {2} instead)",
                Name,
                (pt != null) ? pt.Name : im_class,
                firstArg);
        }

        /// <summary>
        /// Validates that the current self object is usable for this method.  
        /// </summary>
        internal object CheckSelf(CodeContext context, object self) {
            if (!PythonOps.IsInstance(context, self, im_class)) {
                throw BadSelf(self);
            }
            return self;
        }
        
        #region Object Overrides
        private string DeclaringClassAsString() {
            if (im_class == null) return "?";
            PythonType dt = im_class as PythonType;
            if (dt != null) return dt.Name;
            return im_class.ToString();
        }

        public override bool Equals(object obj) {
            Method other = obj as Method;
            if (other == null) return false;
            return PythonOps.IsOrEqualsRetBool(__self__, other.__self__) && PythonOps.EqualRetBool(__func__, other.__func__);
        }

        public override int GetHashCode() {
            if (__self__ == null) return PythonOps.Hash(DefaultContext.Default, __func__);

            return PythonOps.Hash(DefaultContext.Default, __self__) ^ PythonOps.Hash(DefaultContext.Default, __func__);
        }
        
        #endregion

        #region IWeakReferenceable Members

        WeakRefTracker IWeakReferenceable.GetWeakRef() {
            return _weakref;
        }

        bool IWeakReferenceable.SetWeakRef(WeakRefTracker value) {
            _weakref = value;
            return true;
        }

        void IWeakReferenceable.SetFinalizer(WeakRefTracker value) {
            ((IWeakReferenceable)this).SetWeakRef(value);
        }

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
        public void SetMemberAfter(CodeContext context, string name, object value) {
            TypeCache.Method.SetMember(context, this, name, value);
        }

        [SpecialName]
        public void DeleteMember(CodeContext context, string name) {
            TypeCache.Method.DeleteMember(context, this, name);
        }

        IList<string> IMembersList.GetMemberNames() {
            return PythonOps.GetStringMemberList(this);
        }

        IList<object> IPythonMembersList.GetMemberNames(CodeContext/*!*/ context) {
            PythonList ret = TypeCache.Method.GetMemberNames(context);

            ret.AddNoLockNoDups("__module__");

            PythonFunction pf = __func__ as PythonFunction;
            if (pf != null) {
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
            if (this.__self__ == null) {
                if (owner == null || owner == im_class || PythonOps.IsSubClass(context, owner, im_class)) {
                    value = new Method(__func__, instance, owner);
                    return true;
                }
            }
            value = this;
            return true;
        }

        internal override bool GetAlwaysSucceeds {
            get {
                return true;
            }
        }

        #endregion

        #region ICodeFormattable Members

        public string/*!*/ __repr__(CodeContext/*!*/ context) {
            object name;
            if (!PythonOps.TryGetBoundAttr(context, __func__, "__name__", out name)) {
                name = "?";
            }

            if (__self__ != null) {
                return string.Format("<bound method {0}.{1} of {2}>",
                    DeclaringClassAsString(),
                    name,
                    PythonOps.Repr(context, __self__));
            } else {
                return string.Format("<unbound method {0}.{1}>", DeclaringClassAsString(), name);
            }
        }

        #endregion

        #region IDynamicMetaObjectProvider Members

        DynamicMetaObject/*!*/ IDynamicMetaObjectProvider.GetMetaObject(Expression/*!*/ parameter) {
            return new Binding.MetaMethod(parameter, BindingRestrictions.Empty, this);
        }

        #endregion
    }
}
