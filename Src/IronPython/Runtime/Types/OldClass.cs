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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Dynamic;
using System.Runtime.Serialization;
using System.Threading;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Operations;

using SpecialNameAttribute = System.Runtime.CompilerServices.SpecialNameAttribute;

namespace IronPython.Runtime.Types {

    // OldClass represents the type of old-style Python classes (which could not inherit from 
    // built-in Python types). 
    // 
    // Object instances of old-style Python classes are represented by OldInstance.
    // 
    // UserType is the equivalent of OldClass for new-style Python classes (which can inherit 
    // from built-in types).

    [Flags]
    enum OldClassAttributes {
        None = 0x00,
        HasFinalizer = 0x01,
        HasSetAttr = 0x02,
        HasDelAttr = 0x04,
    }

    [PythonType("classobj"), DontMapGetMemberNamesToDir]
    [Serializable]
    [DebuggerTypeProxy(typeof(OldClass.OldClassDebugView)), DebuggerDisplay("old-style class {Name}")]
    public sealed class OldClass :
#if FEATURE_CUSTOM_TYPE_DESCRIPTOR
        ICustomTypeDescriptor,
#endif
        ICodeFormattable,
        IMembersList,
        IDynamicMetaObjectProvider, 
        IPythonMembersList,
        IWeakReferenceable {

        [NonSerialized]
        private List<OldClass> _bases;
        private PythonType _type;

        internal PythonDictionary _dict;
        private int _attrs;  // actually OldClassAttributes - losing type safety for thread safety
        internal object _name;

        private int _optimizedInstanceNamesVersion;
        private string[] _optimizedInstanceNames;
        private WeakRefTracker _tracker;

        public static string __doc__ = "classobj(name, bases, dict)";

        public static object __new__(CodeContext/*!*/ context, [NotNull]PythonType cls, string name, PythonTuple bases, PythonDictionary/*!*/ dict) {
            if (dict == null) {
                throw PythonOps.TypeError("dict must be a dictionary");
            } else if (cls != TypeCache.OldClass) {
                throw PythonOps.TypeError("{0} is not a subtype of classobj", cls.Name);
            }

            if (!dict.ContainsKey("__module__")) {
                object moduleValue;
                if (context.TryGetGlobalVariable("__name__", out moduleValue)) {
                    dict["__module__"] = moduleValue;
                }
            }

            foreach (object o in bases) {
                if (o is PythonType) {
                    return PythonOps.MakeClass(context, name, bases._data, String.Empty, dict);
                }
            }

            return new OldClass(name, bases, dict, String.Empty);
        }

        internal OldClass(string name, PythonTuple bases, PythonDictionary/*!*/ dict, string instanceNames) {
            _bases = ValidateBases(bases);

            Init(name, dict, instanceNames);
        }

        private void Init(string name, PythonDictionary/*!*/ dict, string instanceNames) {
            _name = name;

            InitializeInstanceNames(instanceNames);

            _dict = dict;

            
            if (!_dict._storage.Contains("__doc__")) {
                _dict._storage.Add(ref _dict._storage, "__doc__", null);
            }

            CheckSpecialMethods(_dict);
        }

        private void CheckSpecialMethods(PythonDictionary dict) {
            if (dict._storage.Contains("__del__")) {
                HasFinalizer = true;
            }

            if (dict._storage.Contains("__setattr__")) {
                HasSetAttr = true;
            }

            if (dict._storage.Contains("__delattr__")) {
                HasDelAttr = true;
            }

            foreach (OldClass oc in _bases) {
                if (oc.HasDelAttr) {
                    HasDelAttr = true;
                }
                if (oc.HasSetAttr) {
                    HasSetAttr = true;
                }
                if (oc.HasFinalizer) {
                    HasFinalizer = true;
                }
            }
        }

#if FEATURE_SERIALIZATION
        private OldClass(SerializationInfo info, StreamingContext context) {
            _bases = (List<OldClass>)info.GetValue("__class__", typeof(List<OldClass>));
            _name = info.GetValue("__name__", typeof(object));
            _dict = new PythonDictionary();

            InitializeInstanceNames(""); //TODO should we serialize the optimization data

            List<object> keys = (List<object>)info.GetValue("keys", typeof(List<object>));
            List<object> values = (List<object>)info.GetValue("values", typeof(List<object>));
            for (int i = 0; i < keys.Count; i++) {
                _dict[keys[i]] = values[i];
            }

            if (_dict.has_key("__del__")) HasFinalizer = true;
        }

#pragma warning disable 169 // unused method - called via reflection from serialization
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "context")]
        private void GetObjectData(SerializationInfo info, StreamingContext context) {
            ContractUtils.RequiresNotNull(info, "info");

            info.AddValue("__bases__", _bases);
            info.AddValue("__name__", _name);

            List<object> keys = new List<object>();
            List<object> values = new List<object>();
            foreach (KeyValuePair<object, object> kvp in _dict._storage.GetItems()) {
                keys.Add(kvp.Key);
                values.Add(kvp.Value);
            }

            info.AddValue("keys", keys);
            info.AddValue("values", values);
        }
#pragma warning restore 169
#endif

        private void InitializeInstanceNames(string instanceNames) {
            if (instanceNames.Length == 0) {
                _optimizedInstanceNames = ArrayUtils.EmptyStrings;
                _optimizedInstanceNamesVersion = 0;
                return;
            }

            string[] names = instanceNames.Split(',');
            _optimizedInstanceNames = new string[names.Length];
            for (int i = 0; i < names.Length; i++) {
                _optimizedInstanceNames[i] = names[i];
            }
            _optimizedInstanceNamesVersion = CustomInstanceDictionaryStorage.AllocateVersion();
        }

        internal string[] OptimizedInstanceNames {
            get { return _optimizedInstanceNames; }
        }

        internal int OptimizedInstanceNamesVersion {
            get { return _optimizedInstanceNamesVersion; }
        }

        internal string Name {
            get { return _name.ToString(); }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1007:UseGenericsWhereAppropriate")]
        internal bool TryLookupSlot(string name, out object ret) {
            if (_dict._storage.TryGetValue(name, out ret)) {
                return true;
            }

            // bases only ever contains OldClasses (tuples are immutable, and when assigned
            // to we verify the types in the Tuple)
            foreach (OldClass c in _bases) {
                if (c.TryLookupSlot(name, out ret)) return true;
            }

            ret = null;
            return false;
        }

        internal bool TryLookupOneSlot(PythonType lookingType, string name, out object ret) {
            if (_dict._storage.TryGetValue(name, out ret)) {
                ret = GetOldStyleDescriptor(TypeObject.Context.SharedContext, ret, null, lookingType);
                return true;
            }
            return false;
        }

        internal string FullName {
            get { return _dict["__module__"].ToString() + '.' + _name; }
        }


        internal List<OldClass> BaseClasses {
            get {
                return _bases;
            }
        }

        internal object GetOldStyleDescriptor(CodeContext context, object self, object instance, object type) {
            PythonTypeSlot dts = self as PythonTypeSlot;
            object callable;
            if (dts != null && dts.TryGetValue(context, instance, TypeObject, out callable)) {
                return callable;
            }

            return PythonOps.GetUserDescriptor(self, instance, type);
        }

        internal static object GetOldStyleDescriptor(CodeContext context, object self, object instance, PythonType type) {
            PythonTypeSlot dts = self as PythonTypeSlot;
            object callable;
            if (dts != null && dts.TryGetValue(context, instance, type, out callable)) {
                return callable;
            }

            return PythonOps.GetUserDescriptor(self, instance, type);
        }

        internal bool HasFinalizer {
            get {
                return (_attrs & (int)OldClassAttributes.HasFinalizer) != 0;
            }
            set {
                int oldAttrs, newAttrs;
                do {
                    oldAttrs = _attrs;
                    newAttrs = value ? oldAttrs | ((int)OldClassAttributes.HasFinalizer) : oldAttrs & ((int)~OldClassAttributes.HasFinalizer);
                } while (Interlocked.CompareExchange(ref _attrs, newAttrs, oldAttrs) != oldAttrs);
            }
        }

        internal bool HasSetAttr {
            get {
                return (_attrs & (int)OldClassAttributes.HasSetAttr) != 0;
            }
            set {
                int oldAttrs, newAttrs;
                do {
                    oldAttrs = _attrs;
                    newAttrs = value ? oldAttrs | ((int)OldClassAttributes.HasSetAttr) : oldAttrs & ((int)~OldClassAttributes.HasSetAttr);
                } while (Interlocked.CompareExchange(ref _attrs, newAttrs, oldAttrs) != oldAttrs);
            }
        }

        internal bool HasDelAttr {
            get {
                return (_attrs & (int)OldClassAttributes.HasDelAttr) != 0;
            }
            set {
                int oldAttrs, newAttrs;
                do {
                    oldAttrs = _attrs;
                    newAttrs = value ? oldAttrs | ((int)OldClassAttributes.HasDelAttr) : oldAttrs & ((int)~OldClassAttributes.HasDelAttr);
                } while (Interlocked.CompareExchange(ref _attrs, newAttrs, oldAttrs) != oldAttrs);
            }
        }

        public override string ToString() {
            return FullName;
        }

        #region Calls

        // Calling an OldClass instance means instantiating that class and invoking the __init__() member if 
        // it's defined.

        // OldClass impls IDynamicMetaObjectProvider. But May wind up here still if IDynamicObj doesn't provide a rule (such as for list sigs).
        // If our IDynamicMetaObjectProvider implementation is complete, we can then remove these Call methods.
        [SpecialName]
        public object Call(CodeContext context, [NotNull]params object[] args\u00F8) {
            OldInstance inst = new OldInstance(context, this);
            object value;
            // lookup the slot directly - we don't go through __getattr__
            // until after the instance is created.
            if (TryLookupSlot("__init__", out value)) {
                PythonOps.CallWithContext(context, GetOldStyleDescriptor(context, value, inst, this), args\u00F8);
            } else if (args\u00F8.Length > 0) {
                MakeCallError();
            }
            return inst;
        }

        [SpecialName]
        public object Call(CodeContext context, [ParamDictionary]IDictionary<object, object> dict\u00F8, [NotNull]params object[] args\u00F8) {
            OldInstance inst = new OldInstance(context, this);
            object meth;
            if (PythonOps.TryGetBoundAttr(inst, "__init__", out meth)) {
                PythonCalls.CallWithKeywordArgs(context, meth, args\u00F8, dict\u00F8);
            } else if (dict\u00F8.Count > 0 || args\u00F8.Length > 0) {
                MakeCallError();
            }
            return inst;
        }

        #endregion // calls
        
        internal PythonType TypeObject {
            get {
                if (_type == null) {
                    Interlocked.CompareExchange(ref _type, new PythonType(this), null);
                }
                return _type;
            }
        }

        private List<OldClass> ValidateBases(object value) {
            PythonTuple t = value as PythonTuple;
            if (t == null) throw PythonOps.TypeError("__bases__ must be a tuple object");

            List<OldClass> res = new List<OldClass>(t.__len__());
            foreach (object o in t) {
                OldClass oc = o as OldClass;
                if (oc == null) throw PythonOps.TypeError("__bases__ items must be classes (got {0})", PythonTypeOps.GetName(o));

                if (oc.IsSubclassOf(this)) {
                    throw PythonOps.TypeError("a __bases__ item causes an inheritance cycle");
                }

                res.Add(oc);
            }
            return res;
        }

        internal object GetMember(CodeContext context, string name) {
            object value;

            if (!TryGetBoundCustomMember(context, name, out value)) {
                throw PythonOps.AttributeError("type object '{0}' has no attribute '{1}'", Name, name);
            }

            return value;
        }

        internal bool TryGetBoundCustomMember(CodeContext context, string name, out object value) {
            if (name == "__bases__") { value = PythonTuple.Make(_bases); return true; }
            if (name == "__name__") { value = _name; return true; }
            if (name == "__dict__") {
                //!!! user code can modify __del__ property of __dict__ behind our back
                HasDelAttr = HasSetAttr = true;  // pessimisticlly assume the user is setting __setattr__ in the dict
                value = _dict; return true;
            }

            if (TryLookupSlot(name, out value)) {
                value = GetOldStyleDescriptor(context, value, null, this);
                return true;
            }
            return false;
        }

        internal bool DeleteCustomMember(CodeContext context, string name) {
            if (!_dict._storage.Remove(ref _dict._storage, name)) {
                throw PythonOps.AttributeError("{0} is not a valid attribute", name);
            }

            if (name == "__del__") {
                HasFinalizer = false;
            }
            if (name == "__setattr__") {
                HasSetAttr = false;
            }
            if (name == "__delattr__") {
                HasDelAttr = false;
            }

            return true;
        }

        internal static void RecurseAttrHierarchy(OldClass oc, IDictionary<object, object> attrs) {
            foreach (KeyValuePair<object, object> kvp in oc._dict._storage.GetItems()) {
                if (!attrs.ContainsKey(kvp.Key)) {
                    attrs.Add(kvp.Key, kvp.Key);
                }
            }

            //  recursively get attrs in parent hierarchy
            if (oc._bases.Count != 0) {
                foreach (OldClass parent in oc._bases) {
                    RecurseAttrHierarchy(parent, attrs);
                }
            }
        }

        #region IMembersList Members

        IList<string> IMembersList.GetMemberNames() {
            return PythonOps.GetStringMemberList(this);
        }

        IList<object> IPythonMembersList.GetMemberNames(CodeContext/*!*/ context) {
            PythonDictionary attrs = new PythonDictionary(_dict);
            RecurseAttrHierarchy(this, attrs);
            return PythonOps.MakeListFromSequence(attrs);
        }

        #endregion

        internal bool IsSubclassOf(object other) {
            if (this == other) return true;

            OldClass dt = other as OldClass;
            if (dt == null) return false;

            List<OldClass> bases = _bases;
            foreach (OldClass bc in bases) {
                if (bc.IsSubclassOf(other)) {
                    return true;
                }
            }
            return false;
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
            return CustomTypeDescHelpers.GetEvents(attributes);
        }

        EventDescriptorCollection ICustomTypeDescriptor.GetEvents() {
            return CustomTypeDescHelpers.GetEvents(this);
        }

        PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties(Attribute[] attributes) {
            return CustomTypeDescHelpers.GetProperties(attributes);
        }

        PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties() {
            return CustomTypeDescHelpers.GetProperties(this);
        }

        object ICustomTypeDescriptor.GetPropertyOwner(PropertyDescriptor pd) {
            return CustomTypeDescHelpers.GetPropertyOwner(this, pd);
        }
#endif
        #endregion

        #region ICodeFormattable Members

        public string/*!*/ __repr__(CodeContext/*!*/ context) {
            return string.Format("<class {0} at {1}>", FullName, PythonOps.HexId(this));
        }

        #endregion

        #region Internal Member Accessors

        internal bool TryLookupInit(object inst, out object ret) {
            if (TryLookupSlot("__init__", out ret)) {
                ret = GetOldStyleDescriptor(DefaultContext.Default, ret, inst, this);
                return true;
            }

            return false;
        }

        internal static object MakeCallError() {
            // Normally, if we have an __init__ method, the method binder detects signature mismatches.
            // This can happen when a class does not define __init__ and therefore does not take any arguments.
            // Beware that calls like F(*(), **{}) have 2 arguments but they're empty and so it should still
            // match against def F(). 
            throw PythonOps.TypeError("this constructor takes no arguments");
        }

        internal void SetBases(object value) {
            _bases = ValidateBases(value);
        }

        internal void SetName(object value) {
            string n = value as string;
            if (n == null) throw PythonOps.TypeError("TypeError: __name__ must be a string object");
            _name = n;
        }

        internal void SetDictionary(object value) {
            PythonDictionary d = value as PythonDictionary;
            if (d == null) throw PythonOps.TypeError("__dict__ must be set to dictionary");
            _dict = d;
        }

        internal void SetNameHelper(string name, object value) {
            _dict._storage.Add(ref _dict._storage, name, value);

            if (name == "__del__") {
                HasFinalizer = true;
            } else if (name == "__setattr__") {
                HasSetAttr = true;
            } else if (name == "__delattr__") {
                HasDelAttr = true;
            }
        }

        internal object LookupValue(CodeContext context, string name) {
            object value;
            if (TryLookupValue(context, name, out value)) {
                return value;
            }

            throw PythonOps.AttributeErrorForMissingAttribute(this, name);
        }

        internal bool TryLookupValue(CodeContext context, string name, out object value) {
            if (TryLookupSlot(name, out value)) {
                value = GetOldStyleDescriptor(context, value, null, this);
                return true;
            }

            return false;
        }

        internal void DictionaryIsPublic() {
            HasDelAttr = true;
            HasSetAttr = true;
        }

        #endregion

        #region IDynamicMetaObjectProvider Members

        DynamicMetaObject/*!*/ IDynamicMetaObjectProvider.GetMetaObject(Expression/*!*/ parameter) {
            return new Binding.MetaOldClass(parameter, BindingRestrictions.Empty, this);
        }

        #endregion

        internal class OldClassDebugView {
            private readonly OldClass _class;

            public OldClassDebugView(OldClass klass) {
                _class = klass;
            }

            public List<OldClass> __bases__ {
                get {
                    return _class.BaseClasses;
                }
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            internal List<ObjectDebugView> Members {
                get {
                    var res = new List<ObjectDebugView>();
                    if (_class._dict != null) {
                        foreach (var v in _class._dict) {
                            res.Add(new ObjectDebugView(v.Key, v.Value));
                        }
                    }

                    return res;
                }
            }
        }

        #region IWeakReferenceable Members

        WeakRefTracker IWeakReferenceable.GetWeakRef() {
            return _tracker;
        }

        bool IWeakReferenceable.SetWeakRef(WeakRefTracker value) {
            _tracker = value;
            return true;
        }

        void IWeakReferenceable.SetFinalizer(WeakRefTracker value) {
            _tracker = value;
        }

        #endregion
    }
}
