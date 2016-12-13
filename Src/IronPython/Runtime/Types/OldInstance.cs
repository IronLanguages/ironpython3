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
using System.Numerics;
#else
using Microsoft.Scripting.Ast;
using Microsoft.Scripting.Math;
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Operations;

using SpecialNameAttribute = System.Runtime.CompilerServices.SpecialNameAttribute;

namespace IronPython.Runtime.Types {

    [PythonType("instance")]
    [Serializable]
    [DebuggerTypeProxy(typeof(OldInstance.OldInstanceDebugView)), DebuggerDisplay("old-style instance of {ClassName}")]
    public sealed partial class OldInstance :
        ICodeFormattable,
#if CLR2
        IValueEquality,
#endif
#if FEATURE_CUSTOM_TYPE_DESCRIPTOR
        ICustomTypeDescriptor,
#endif
        ISerializable,
        IWeakReferenceable,
        IDynamicMetaObjectProvider, 
        IPythonMembersList,
        Binding.IFastGettable
    {

        private PythonDictionary _dict;
        internal OldClass _class;
        private WeakRefTracker _weakRef;       // initialized if user defines finalizer on class or instance

        private static PythonDictionary MakeDictionary(OldClass oldClass) {
            return new PythonDictionary(new CustomInstanceDictionaryStorage(oldClass.OptimizedInstanceNames, oldClass.OptimizedInstanceNamesVersion));
        }


        public OldInstance(CodeContext/*!*/ context, OldClass @class) {
            _class = @class;
            _dict = MakeDictionary(@class);
            if (_class.HasFinalizer) {
                // class defines finalizer, we get it automatically.
                AddFinalizer(context);
            }
        }

        public OldInstance(CodeContext/*!*/ context, OldClass @class, PythonDictionary dict) {
            _class = @class;
            _dict = dict ?? PythonDictionary.MakeSymbolDictionary();
            if (_class.HasFinalizer) {
                // class defines finalizer, we get it automatically.
                AddFinalizer(context);
            }
        }

#if FEATURE_SERIALIZATION
        private OldInstance(SerializationInfo info, StreamingContext context) {
            _class = (OldClass)info.GetValue("__class__", typeof(OldClass));
            _dict = MakeDictionary(_class);

            List<object> keys = (List<object>)info.GetValue("keys", typeof(List<object>));
            List<object> values = (List<object>)info.GetValue("values", typeof(List<object>));
            for (int i = 0; i < keys.Count; i++) {
                _dict[keys[i]] = values[i];
            }
        }

#pragma warning disable 169 // unused method - called via reflection from serialization
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "context")]
        private void GetObjectData(SerializationInfo info, StreamingContext context) {
            ContractUtils.RequiresNotNull(info, "info");

            info.AddValue("__class__", _class);
            List<object> keys = new List<object>();
            List<object> values = new List<object>();
            foreach (object o in _dict.keys()) {
                keys.Add(o);
                object value;
                
                bool res = _dict.TryGetValue(o, out value);

                Debug.Assert(res);

                values.Add(value);
            }

            info.AddValue("keys", keys);
            info.AddValue("values", values);
        }
#pragma warning restore 169
#endif

        /// <summary>
        /// Returns the dictionary used to store state for this object
        /// </summary>
        internal PythonDictionary Dictionary {
            get { return _dict; }
        }

        internal string ClassName {
            get {
                return _class.Name;
            }
        }

        public static bool operator true(OldInstance self) {
            return (bool)self.__bool__(DefaultContext.Default);
        }

        public static bool operator false(OldInstance self) {
            return !(bool)self.__bool__(DefaultContext.Default);
        }

        #region Object overrides

        public override string ToString() {
            object ret = InvokeOne(this, "__str__");

            if (ret != NotImplementedType.Value) {
                string strRet;
                if (Converter.TryConvertToString(ret, out strRet) && strRet != null) {
                    return strRet;
                }
                throw PythonOps.TypeError("__str__ returned non-string type ({0})", PythonTypeOps.GetName(ret));
            }

            return __repr__(DefaultContext.Default);
        }

        #endregion

        #region ICodeFormattable Members

        public string/*!*/ __repr__(CodeContext/*!*/ context) {
            object ret = InvokeOne(this, "__repr__");
            if(ret != NotImplementedType.Value) {
                string strRet;
                if (Converter.TryConvertToString(ret, out strRet) && strRet != null) {
                    return strRet;
                }
                throw PythonOps.TypeError("__repr__ returned non-string type ({0})", PythonTypeOps.GetName(ret));
            }

            return string.Format("<{0} instance at {1}>", _class.FullName, PythonOps.HexId(this));
        }

        #endregion

        [return: MaybeNotImplemented]
        public object __divmod__(CodeContext context, object divmod) {
            object value;

            if (TryGetBoundCustomMember(context, "__divmod__", out value)) {
                return PythonCalls.Call(context, value, divmod);
            }


            return NotImplementedType.Value;
        }

        [return: MaybeNotImplemented]
        public static object __rdivmod__(CodeContext context, object divmod, [NotNull]OldInstance self) {
            object value;

            if (self.TryGetBoundCustomMember(context, "__rdivmod__", out value)) {
                return PythonCalls.Call(context, value, divmod);
            }

            return NotImplementedType.Value;
        }

        public object __coerce__(CodeContext context, object other) {
            object value;

            if (TryGetBoundCustomMember(context, "__coerce__", out value)) {
                return PythonCalls.Call(context, value, other);
            }

            return NotImplementedType.Value;
        }

        public object __len__(CodeContext context) {
            object value;

            if (TryGetBoundCustomMember(context, "__len__", out value)) {
                return PythonOps.CallWithContext(context, value);
            }

            throw PythonOps.AttributeErrorForOldInstanceMissingAttribute(_class.Name, "__len__");
        }

        public object __pos__(CodeContext context) {
            object value;

            if (TryGetBoundCustomMember(context, "__pos__", out value)) {
                return PythonOps.CallWithContext(context, value);
            }

            throw PythonOps.AttributeErrorForOldInstanceMissingAttribute(_class.Name, "__pos__");
        }

        [SpecialName]
        public object GetItem(CodeContext context, object item) {
            return PythonOps.Invoke(context, this, "__getitem__", item);
        }

        [SpecialName]
        public void SetItem(CodeContext context, object item, object value) {
            PythonOps.Invoke(context, this, "__setitem__", item, value);
        }

        [SpecialName]
        public object DeleteItem(CodeContext context, object item) {
            object value;

            if (TryGetBoundCustomMember(context, "__delitem__", out value)) {
                return PythonCalls.Call(context, value, item);
            }

            throw PythonOps.AttributeErrorForOldInstanceMissingAttribute(_class.Name, "__delitem__");
        }

        public object __getslice__(CodeContext context, int i, int j) {
            object callable;
            if (TryRawGetAttr(context, "__getslice__", out callable)) {
                return PythonCalls.Call(context, callable, i, j);
            } else if (TryRawGetAttr(context, "__getitem__", out callable)) {
                return PythonCalls.Call(context, callable, new Slice(i, j));
            }

            throw PythonOps.TypeError("instance {0} does not have __getslice__ or __getitem__", _class.Name);
        }
        
        public void __setslice__(CodeContext context, int i, int j, object value) {
            object callable;
            if (TryRawGetAttr(context, "__setslice__", out callable)) {
                PythonCalls.Call(context, callable, i, j, value);
                return;
            } else if (TryRawGetAttr(context, "__setitem__", out callable)) {
                PythonCalls.Call(context, callable, new Slice(i, j), value);
                return;
            }

            throw PythonOps.TypeError("instance {0} does not have __setslice__ or __setitem__", _class.Name);
        }

        public object __delslice__(CodeContext context, int i, int j) {
            object callable;
            if (TryRawGetAttr(context, "__delslice__", out callable)) {
                return PythonCalls.Call(context, callable, i, j);
            } else if (TryRawGetAttr(context, "__delitem__", out callable)) {
                return PythonCalls.Call(context, callable, new Slice(i, j));
            }

            throw PythonOps.TypeError("instance {0} does not have __delslice__ or __delitem__", _class.Name);
        }

        public object __index__(CodeContext context) {
            object value;

            if (TryGetBoundCustomMember(context, "__int__", out value)) {
                return PythonOps.CallWithContext(context, value);
            }

            throw PythonOps.TypeError("object cannot be converted to an index");
        }

        public object __neg__(CodeContext context) {
            object value;

            if (TryGetBoundCustomMember(context, "__neg__", out value)) {
                return PythonOps.CallWithContext(context, value);
            }

            throw PythonOps.AttributeErrorForOldInstanceMissingAttribute(_class.Name, "__neg__");
        }

        public object __abs__(CodeContext context) {
            object value;

            if (TryGetBoundCustomMember(context, "__abs__", out value)) {
                return PythonOps.CallWithContext(context, value);
            }

            throw PythonOps.AttributeErrorForOldInstanceMissingAttribute(_class.Name, "__abs__");
        }

        public object __invert__(CodeContext context) {
            object value;

            if (TryGetBoundCustomMember(context, "__invert__", out value)) {
                return PythonOps.CallWithContext(context, value);
            }

            throw PythonOps.AttributeErrorForOldInstanceMissingAttribute(_class.Name, "__invert__");
        }

        public object __contains__(CodeContext context, object index) {
            object value;

            if (TryGetBoundCustomMember(context, "__contains__", out value)) {
                return PythonCalls.Call(context, value, index);
            }

            IEnumerator ie = PythonOps.GetEnumerator(this);
            while (ie.MoveNext()) {
                if (PythonOps.EqualRetBool(context, ie.Current, index)) return ScriptingRuntimeHelpers.True;
            }

            return ScriptingRuntimeHelpers.False;
        }
        
        [SpecialName]
        public object Call(CodeContext context) {
            return Call(context, ArrayUtils.EmptyObjects);
        }

        [SpecialName]
        public object Call(CodeContext context, object args) {
            try {
                PythonOps.FunctionPushFrame(PythonContext.GetContext(context));

                object value;
                if (TryGetBoundCustomMember(context, "__call__", out value)) {
                    return PythonOps.CallWithContext(context, value, args);
                }
            } finally {
                PythonOps.FunctionPopFrame();
            }

            throw PythonOps.AttributeError("{0} instance has no __call__ method", _class.Name);
        }

        [SpecialName]
        public object Call(CodeContext context, params object[] args) {
            try {
                PythonOps.FunctionPushFrame(PythonContext.GetContext(context));

                object value;
                if (TryGetBoundCustomMember(context, "__call__", out value)) {
                    return PythonOps.CallWithContext(context, value, args);
                }
            } finally {
                PythonOps.FunctionPopFrame();
            }

            throw PythonOps.AttributeError("{0} instance has no __call__ method", _class.Name);
        }

        [SpecialName]
        public object Call(CodeContext context, [ParamDictionary]IDictionary<object, object> dict, params object[] args) {
            try {
                PythonOps.FunctionPushFrame(PythonContext.GetContext(context));

                object value;
                if (TryGetBoundCustomMember(context, "__call__", out value)) {
                    return context.LanguageContext.CallWithKeywords(value, args, dict);
                }
            } finally {
                PythonOps.FunctionPopFrame();
            }

            throw PythonOps.AttributeError("{0} instance has no __call__ method", _class.Name);
        }

        public object __bool__(CodeContext context) {
            object value;

            if (TryGetBoundCustomMember(context, "__bool__", out value)) {
                return PythonOps.CallWithContext(context, value);
            }

            if (TryGetBoundCustomMember(context, "__len__", out value)) {
                value = PythonOps.CallWithContext(context, value);
                // Convert resulting object to the desired type
                if (value is Int32 || value is BigInteger) {
                    return ScriptingRuntimeHelpers.BooleanToObject(Converter.ConvertToBoolean(value));
                }
                throw PythonOps.TypeError("an integer is required, got {0}", PythonTypeOps.GetName(value));
            }

            return ScriptingRuntimeHelpers.True;
        }

        public object __hex__(CodeContext context) {
            object value;
            if (TryGetBoundCustomMember(context, "__hex__", out value)) {
                return PythonOps.CallWithContext(context, value);
            }

            throw PythonOps.AttributeErrorForOldInstanceMissingAttribute(_class.Name, "__hex__");
        }

        public object __oct__(CodeContext context) {
            object value;
            if (TryGetBoundCustomMember(context, "__oct__", out value)) {
                return PythonOps.CallWithContext(context, value);
            }

            throw PythonOps.AttributeErrorForOldInstanceMissingAttribute(_class.Name, "__oct__");
        }

        public object __int__(CodeContext context) {
            object value;

            if (PythonOps.TryGetBoundAttr(context, this, "__int__", out value)) {
                return PythonOps.CallWithContext(context, value);
            }

            return NotImplementedType.Value;
        }

        public object __long__(CodeContext context) {
            object value;

            if (PythonOps.TryGetBoundAttr(context, this, "__long__", out value)) {
                return PythonOps.CallWithContext(context, value);
            }

            return NotImplementedType.Value;
        }

        public object __float__(CodeContext context) {
            object value;

            if (PythonOps.TryGetBoundAttr(context, this, "__float__", out value)) {
                return PythonOps.CallWithContext(context, value);
            }

            return NotImplementedType.Value;
        }

        public object __complex__(CodeContext context) {
            object value;

            if (TryGetBoundCustomMember(context, "__complex__", out value)) {
                return PythonOps.CallWithContext(context, value);
            }

            return NotImplementedType.Value;
        }

        public object __getattribute__(CodeContext context, string name) {
            object res;
            if (TryGetBoundCustomMember(context, name, out res)) {
                return res;
            }

            throw PythonOps.AttributeError("{0} instance has no attribute '{1}'", _class._name, name);
        }

        internal object GetBoundMember(CodeContext context, string name) {
            object ret;
            if (TryGetBoundCustomMember(context, name, out ret)) {
                return ret;
            }
            throw PythonOps.AttributeError("'{0}' object has no attribute '{1}'",
                PythonTypeOps.GetName(this), name);
        }

        #region ICustomMembers Members

        internal bool TryGetBoundCustomMember(CodeContext context, string name, out object value) {
            if (name == "__dict__") {
                //!!! user code can modify __del__ property of __dict__ behind our back
                value = _dict;
                return true;
            } else if (name == "__class__") {
                value = _class;
                return true;
            }

            if (TryRawGetAttr(context, name, out value)) return true;

            if (name != "__getattr__") {
                object getattr;
                if (TryRawGetAttr(context, "__getattr__", out getattr)) {
                    try {
                        value = PythonCalls.Call(context, getattr, name);
                        return true;
                    } catch (MissingMemberException) {
                        // __getattr__ raised AttributeError, return false.
                    }
                }
            }

            return false;
        }

        internal void SetCustomMember(CodeContext context, string name, object value) {
            object setFunc;
            if (name == "__class__") {
                SetClass(value);
            } else if (name == "__dict__") {
                SetDict(context, value);
            } else if (_class.HasSetAttr && _class.TryLookupSlot("__setattr__", out setFunc)) {
                PythonCalls.Call(context, _class.GetOldStyleDescriptor(context, setFunc, this, _class), name.ToString(), value);
            } else if (name == "__del__") {
                SetFinalizer(context, name, value);
            } else {
                _dict[name] = value;
            }
        }

        private void SetFinalizer(CodeContext/*!*/ context, string name, object value) {
            if (!HasFinalizer()) {
                // user is defining __del__ late bound for the 1st time
                AddFinalizer(context);
            }

            _dict[name] = value;
        }

        private void SetDict(CodeContext/*!*/ context, object value) {
            PythonDictionary dict = value as PythonDictionary;
            if (dict == null) {
                throw PythonOps.TypeError("__dict__ must be set to a dictionary");
            }
            if (HasFinalizer() && !_class.HasFinalizer) {
                if (!dict.ContainsKey("__del__")) {
                    ClearFinalizer();
                }
            } else if (dict.ContainsKey("__del__")) {
                AddFinalizer(context);
            }

            _dict = dict;
        }

        private void SetClass(object value) {
            OldClass oc = value as OldClass;
            if (oc == null) {
                throw PythonOps.TypeError("__class__ must be set to class");
            }
            _class = oc;
        }

        internal bool DeleteCustomMember(CodeContext context, string name) {
            if (name == "__class__") throw PythonOps.TypeError("__class__ must be set to class");
            if (name == "__dict__") throw PythonOps.TypeError("__dict__ must be set to a dictionary");

            object delFunc;
            if (_class.HasDelAttr && _class.TryLookupSlot("__delattr__", out delFunc)) {
                PythonCalls.Call(context, _class.GetOldStyleDescriptor(context, delFunc, this, _class), name.ToString());
                return true;
            }


            if (name == "__del__") {
                // removing finalizer
                if (HasFinalizer() && !_class.HasFinalizer) {
                    ClearFinalizer();
                }
            }

            if (!_dict.Remove(name)) {
                throw PythonOps.AttributeError("{0} is not a valid attribute", name);
            }
            return true;
        }

        #endregion

        #region IMembersList Members

        IList<string> IMembersList.GetMemberNames() {
            return PythonOps.GetStringMemberList(this);
        }

        IList<object> IPythonMembersList.GetMemberNames(CodeContext/*!*/ context) {
            PythonDictionary attrs = new PythonDictionary(_dict);
            OldClass.RecurseAttrHierarchy(this._class, attrs);
            return PythonOps.MakeListFromSequence(attrs);
        }

        #endregion

        [return: MaybeNotImplemented]
        public object __cmp__(CodeContext context, object other) {
            OldInstance oiOther = other as OldInstance;
            // CPython raises this if called directly, but not via cmp(os,ns) which still calls the user __cmp__
            //if(!(oiOther is OldInstance)) 
            //    throw Ops.TypeError("instance.cmp(x,y) -> y must be an instance, got {0}", Ops.StringRepr(DynamicHelpers.GetPythonType(other)));

            object res = InternalCompare("__cmp__", other);
            if (res != NotImplementedType.Value) return res;
            if (oiOther != null) {
                res = oiOther.InternalCompare("__cmp__", this);
                if (res != NotImplementedType.Value) return ((int)res) * -1;
            }

            return NotImplementedType.Value;
        }

        private object CompareForwardReverse(object other, string forward, string reverse) {
            object res = InternalCompare(forward, other);
            if (res != NotImplementedType.Value) return res;

            OldInstance oi = other as OldInstance;
            if (oi != null) {
                // comparison operators are reflexive
                return oi.InternalCompare(reverse, this);
            }

            return NotImplementedType.Value;
        }

        [return: MaybeNotImplemented]
        public static object operator >([NotNull]OldInstance self, object other) {
            return self.CompareForwardReverse(other, "__gt__", "__lt__");
        }

        [return: MaybeNotImplemented]
        public static object operator <([NotNull]OldInstance self, object other) {
            return self.CompareForwardReverse(other, "__lt__", "__gt__");
        }

        [return: MaybeNotImplemented]
        public static object operator >=([NotNull]OldInstance self, object other) {
            return self.CompareForwardReverse(other, "__ge__", "__le__");
        }

        [return: MaybeNotImplemented]
        public static object operator <=([NotNull]OldInstance self, object other) {
            return self.CompareForwardReverse(other, "__le__", "__ge__");
        }

        private object InternalCompare(string cmp, object other) {
            return InvokeOne(this, other, cmp);
        }

        #region ICustomTypeDescriptor Members
#if FEATURE_CUSTOM_TYPE_DESCRIPTOR

        AttributeCollection ICustomTypeDescriptor.GetAttributes() {
            return CustomTypeDescHelpers.GetAttributes(this);
        }

        string ICustomTypeDescriptor.GetClassName() {
            return CustomTypeDescHelpers.GetClassName(this);
        }

        string ICustomTypeDescriptor.GetComponentName() {
            return CustomTypeDescHelpers.GetComponentName(this);
        }

        TypeConverter ICustomTypeDescriptor.GetConverter() {
            return CustomTypeDescHelpers.GetConverter(this);
        }

        EventDescriptor ICustomTypeDescriptor.GetDefaultEvent() {
            return CustomTypeDescHelpers.GetDefaultEvent(this);
        }

        PropertyDescriptor ICustomTypeDescriptor.GetDefaultProperty() {
            return CustomTypeDescHelpers.GetDefaultProperty(this);
        }

        object ICustomTypeDescriptor.GetEditor(Type editorBaseType) {
            return CustomTypeDescHelpers.GetEditor(this, editorBaseType);
        }

        EventDescriptorCollection ICustomTypeDescriptor.GetEvents(Attribute[] attributes) {
            return CustomTypeDescHelpers.GetEvents(this, attributes);
        }

        EventDescriptorCollection ICustomTypeDescriptor.GetEvents() {
            return CustomTypeDescHelpers.GetEvents(this);
        }

        PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties(Attribute[] attributes) {
            return CustomTypeDescHelpers.GetProperties(this, attributes);
        }

        PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties() {
            return CustomTypeDescHelpers.GetProperties(this);
        }

        object ICustomTypeDescriptor.GetPropertyOwner(PropertyDescriptor pd) {
            return CustomTypeDescHelpers.GetPropertyOwner(this, pd);
        }

#endif
        #endregion

        #region IWeakReferenceable Members

        WeakRefTracker IWeakReferenceable.GetWeakRef() {
            return _weakRef;
        }

        bool IWeakReferenceable.SetWeakRef(WeakRefTracker value) {
            _weakRef = value;
            return true;
        }

        void IWeakReferenceable.SetFinalizer(WeakRefTracker value) {
            ((IWeakReferenceable)this).SetWeakRef(value);
        }

        #endregion

        #region Rich Equality
        // Specific rich equality support for when the user calls directly from oldinstance type.

        public int __hash__(CodeContext/*!*/ context) {
            object func;
            object ret = InvokeOne(this, "__hash__");
            if (ret != NotImplementedType.Value) {
                if (ret is BigInteger) {
                    return BigIntegerOps.__hash__((BigInteger)ret);
                } else if (!(ret is int))
                    throw PythonOps.TypeError("expected int from __hash__, got {0}", PythonTypeOps.GetName(ret));

                return (int)ret;
            }

            if (TryGetBoundCustomMember(context, "__cmp__", out func) ||
                TryGetBoundCustomMember(context, "__eq__", out func)) {
                throw PythonOps.TypeError("unhashable instance");
            }

            return base.GetHashCode();
        }


        public override int GetHashCode() {
            object ret;
            try {
                ret = InvokeOne(this, "__hash__");
            } catch {
                return base.GetHashCode();
            }

            if (ret != NotImplementedType.Value) {
                if (ret is int) {
                    return (int)ret;
                }

                if (ret is BigInteger) {
                    return BigIntegerOps.__hash__((BigInteger)ret);
                }
            }

            return base.GetHashCode();
        }

        [return: MaybeNotImplemented]
        public object __eq__(object other) {
            object res = InvokeBoth(other, "__eq__");
            if (res != NotImplementedType.Value) {
                return res;
            }


            return NotImplementedType.Value;
        }

        private object InvokeBoth(object other, string si) {
            object res = InvokeOne(this, other, si);
            if (res != NotImplementedType.Value) {
                return res;
            }
            OldInstance oi = other as OldInstance;
            if (oi != null) {
                res = InvokeOne(oi, this, si);
                if (res != NotImplementedType.Value) {
                    return res;
                }
            }
            return NotImplementedType.Value;
        }

        private static object InvokeOne(OldInstance self, object other, string si) {
            object func;
            try {
                if (!self.TryGetBoundCustomMember(DefaultContext.Default, si, out func)) {
                    return NotImplementedType.Value;
                }
            } catch (MissingMemberException) {
                return NotImplementedType.Value;
            }

            return PythonOps.CallWithContext(DefaultContext.Default, func, other);
        }

        private static object InvokeOne(OldInstance self, object other, object other2, string si) {
            object func;
            try {
                if (!self.TryGetBoundCustomMember(DefaultContext.Default, si, out func)) {
                    return NotImplementedType.Value;
                }
            } catch (MissingMemberException) {
                return NotImplementedType.Value;
            }

            return PythonOps.CallWithContext(DefaultContext.Default, func, other, other2);
        }

        private static object InvokeOne(OldInstance self, string si) {
            object func;
            try {
                if (!self.TryGetBoundCustomMember(DefaultContext.Default, si, out func)) {
                    return NotImplementedType.Value;
                }
            } catch (MissingMemberException) {
                return NotImplementedType.Value;
            }

            return PythonOps.CallWithContext(DefaultContext.Default, func);
        }

        [return: MaybeNotImplemented]
        public object __ne__(object other) {
            object res = InvokeBoth(other, "__ne__");
            if (res != NotImplementedType.Value) {
                return res;
            }

            return NotImplementedType.Value;
        }

        [return: MaybeNotImplemented]
        [SpecialName]
        public static object Power([NotNull]OldInstance self, object other, object mod) {
            object res = InvokeOne(self, other, mod, "__pow__");
            if (res != NotImplementedType.Value) return res;

            return NotImplementedType.Value;
        }

        #endregion

        #region ISerializable Members
#if FEATURE_SERIALIZATION
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context) {
            info.AddValue("__class__", _class);
            info.AddValue("__dict__", _dict);
        }

#endif
        #endregion

        #region Private Implementation Details

        private void RecurseAttrHierarchyInt(OldClass oc, IDictionary<string, object> attrs) {
            foreach (KeyValuePair<object, object> kvp in oc._dict._storage.GetItems()) {
                string strKey = kvp.Key as string;
                if (strKey != null) {
                    if (!attrs.ContainsKey(strKey)) {
                        attrs.Add(strKey, strKey);
                    }
                }
            }
            //  recursively get attrs in parent hierarchy
            if (oc.BaseClasses.Count != 0) {
                foreach (OldClass parent in oc.BaseClasses) {
                    RecurseAttrHierarchyInt(parent, attrs);
                }
            }
        }

        private void AddFinalizer(CodeContext/*!*/ context) {
            InstanceFinalizer oif = new InstanceFinalizer(context, this);
            _weakRef = new WeakRefTracker(oif, oif);
        }

        private void ClearFinalizer() {
            if (_weakRef == null) return;

            WeakRefTracker wrt = _weakRef;
            if (wrt != null) {
                // find our handler and remove it (other users could have created weak refs to us)
                for (int i = 0; i < wrt.HandlerCount; i++) {
                    if (wrt.GetHandlerCallback(i) is InstanceFinalizer) {
                        wrt.RemoveHandlerAt(i);
                        break;
                    }
                }

                // we removed the last handler
                if (wrt.HandlerCount == 0) {
                    GC.SuppressFinalize(wrt);
                    _weakRef = null;
                }
            }
        }

        private bool HasFinalizer() {
            if (_weakRef != null) {
                WeakRefTracker wrt = _weakRef;
                if (wrt != null) {
                    for (int i = 0; i < wrt.HandlerCount; i++) {
                        if (wrt.GetHandlerCallback(i) is InstanceFinalizer) {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private bool TryRawGetAttr(CodeContext context, string name, out object ret) {
            if (_dict._storage.TryGetValue(name, out ret)) {
                return true;
            }

            if (_class.TryLookupSlot(name, out ret)) {
                ret = _class.GetOldStyleDescriptor(context, ret, this, _class);
                return true;
            }

            return false;
        }

        #endregion

        #region IValueEquality Members
#if CLR2
        int IValueEquality.GetValueHashCode() {
            return GetHashCode();
        }

        bool IValueEquality.ValueEquals(object other) {
            return Equals(other);
        }
#endif
        #endregion

        #region IDynamicMetaObjectProvider Members

        DynamicMetaObject/*!*/ IDynamicMetaObjectProvider.GetMetaObject(Expression/*!*/ parameter) {
            return new Binding.MetaOldInstance(parameter, BindingRestrictions.Empty, this);
        }

        #endregion

        internal class OldInstanceDebugView {
            private readonly OldInstance _userObject;

            public OldInstanceDebugView(OldInstance userObject) {
                _userObject = userObject;
            }

            public OldClass __class__ {
                get {
                    return _userObject._class;
                }
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            internal List<ObjectDebugView> Members {
                get {
                    var res = new List<ObjectDebugView>();
                    if (_userObject._dict != null) {
                        foreach (var v in _userObject._dict) {
                            res.Add(new ObjectDebugView(v.Key, v.Value));
                        }
                    }

                    return res;
                }
            }
        }

        class FastOldInstanceGet {
            private readonly string _name;

            public FastOldInstanceGet(string name) {
                _name = name;
            }

            public object Target(CallSite site, object instance, CodeContext context) {
                OldInstance oi = instance as OldInstance;
                if (oi != null) {
                    object res;
                    if (oi.TryGetBoundCustomMember(context, _name, out res)) {
                        return res;
                    }
                    throw PythonOps.AttributeError("{0} instance has no attribute '{1}'", oi._class.Name, _name);
                }

                return ((CallSite<Func<CallSite, object, CodeContext, object>>)site).Update(site, instance, context);
            }

            public object LightThrowTarget(CallSite site, object instance, CodeContext context) {
                OldInstance oi = instance as OldInstance;
                if (oi != null) {
                    object res;
                    if (oi.TryGetBoundCustomMember(context, _name, out res)) {
                        return res;
                    }
                    return LightExceptions.Throw(PythonOps.AttributeError("{0} instance has no attribute '{1}'", oi._class.Name, _name));
                }

                return ((CallSite<Func<CallSite, object, CodeContext, object>>)site).Update(site, instance, context);
            }

            public object NoThrowTarget(CallSite site, object instance, CodeContext context) {
                OldInstance oi = instance as OldInstance;
                if (oi != null) {
                    object res;
                    if (oi.TryGetBoundCustomMember(context, _name, out res)) {
                        return res;
                    }
                    return OperationFailed.Value;
                }

                return ((CallSite<Func<CallSite, object, CodeContext, object>>)site).Update(site, instance, context);
            }
        }

        #region IFastGettable Members

        T Binding.IFastGettable.MakeGetBinding<T>(System.Runtime.CompilerServices.CallSite<T> site, Binding.PythonGetMemberBinder binder, CodeContext state, string name) {
            if (binder.IsNoThrow) {
                return (T)(object)new Func<CallSite, object, CodeContext, object>(new FastOldInstanceGet(name).NoThrowTarget);
            } else if (binder.SupportsLightThrow) {
                return (T)(object)new Func<CallSite, object, CodeContext, object>(new FastOldInstanceGet(name).LightThrowTarget);
            } else {
                return (T)(object)new Func<CallSite, object, CodeContext, object>(new FastOldInstanceGet(name).Target);
            }
        }

        #endregion
    }
}
