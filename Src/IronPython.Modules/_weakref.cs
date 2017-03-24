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
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

[assembly: PythonModule("_weakref", typeof(IronPython.Modules.PythonWeakRef))]
namespace IronPython.Modules {
    public static partial class PythonWeakRef {
        public const string __doc__ = "Provides support for creating weak references and proxies to objects";

        /// <summary>
        /// Wrapper provided for backwards compatibility.
        /// </summary>
        internal static IWeakReferenceable ConvertToWeakReferenceable(PythonContext context, object obj) {
            return context.ConvertToWeakReferenceable(obj);
        }

        public static int getweakrefcount(CodeContext context, object @object) {
            return @ref.GetWeakRefCount(PythonContext.GetContext(context), @object);
        }

        public static List getweakrefs(CodeContext context, object @object) {
            return @ref.GetWeakRefs(PythonContext.GetContext(context), @object);
        }

        public static object proxy(CodeContext context, object @object) {
            return proxy(context, @object, null);
        }

        public static object proxy(CodeContext context, object @object, object callback) {
            if (PythonOps.IsCallable(context, @object)) {
                return weakcallableproxy.MakeNew(context, @object, callback);
            } else {
                return weakproxy.MakeNew(context, @object, callback);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly PythonType CallableProxyType = DynamicHelpers.GetPythonTypeFromType(typeof(weakcallableproxy));
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly PythonType ProxyType = DynamicHelpers.GetPythonTypeFromType(typeof(weakproxy));
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly PythonType ReferenceType = DynamicHelpers.GetPythonTypeFromType(typeof(@ref));

        [PythonType]
        public class @ref : IStructuralEquatable
#if CLR2
            , IValueEquality
#endif
        {
            private readonly CodeContext _context;
            private readonly WeakHandle _target;
            private readonly long _targetId;
            private int _hashVal;
            private bool _fHasHash;

            #region Python Constructors
            public static object __new__(CodeContext context, PythonType cls, object @object) {
                IWeakReferenceable iwr = ConvertToWeakReferenceable(PythonContext.GetContext(context), @object);

                if (cls == DynamicHelpers.GetPythonTypeFromType(typeof(@ref))) {
                    WeakRefTracker wrt = iwr.GetWeakRef();
                    if (wrt != null) {
                        for (int i = 0; i < wrt.HandlerCount; i++) {
                            if (wrt.GetHandlerCallback(i) == null && wrt.GetWeakRef(i) is @ref) {
                                return wrt.GetWeakRef(i);
                            }
                        }
                    }

                    return new @ref(context, @object);
                } else {
                    return cls.CreateInstance(context, @object);
                }
            }

            public static object __new__(CodeContext context, PythonType cls, object @object, object callback) {
                if (callback == null) return __new__(context, cls, @object);
                if (cls == DynamicHelpers.GetPythonTypeFromType(typeof(@ref))) {
                    return new @ref(context, @object, callback);
                } else {
                    return cls.CreateInstance(context, @object, callback);
                }
            }
            #endregion

            #region Constructors
            public @ref(CodeContext context, object @object)
                : this(context, @object, null) {
            }

            public @ref(CodeContext context, object @object, object callback) {
                _context = context;
                WeakRefTracker wrt = WeakRefHelpers.InitializeWeakRef(_context.GetPythonContext(), this, @object, callback);

                _target = new WeakHandle(@object, false);
                _targetId = wrt.TargetId;
            }
            #endregion

            #region Finalizer
            ~@ref() {
                IWeakReferenceable iwr;
                if (_context.GetPythonContext().TryConvertToWeakReferenceable(_target.Target, out iwr))
                {
                    WeakRefTracker wrt = iwr.GetWeakRef();
                    if (wrt != null) {
                        // weak reference being finalized before target object,
                        // we don't want to run the callback when the object is
                        // finalized.
                        wrt.RemoveHandler(this);
                    }
                }

                _target.Free();
            }
            #endregion

            #region Static helpers

            internal static int GetWeakRefCount(PythonContext context, object o) {
                IWeakReferenceable iwr;
                if (context.TryConvertToWeakReferenceable(o, out iwr)) {
                    WeakRefTracker wrt = iwr.GetWeakRef();
                    if (wrt != null) return wrt.HandlerCount;
                }

                return 0;
            }

            internal static List GetWeakRefs(PythonContext context, object o) {
                List l = new List();
                IWeakReferenceable iwr;
                if (context.TryConvertToWeakReferenceable(o, out iwr)) {
                    WeakRefTracker wrt = iwr.GetWeakRef();
                    if (wrt != null) {
                        for (int i = 0; i < wrt.HandlerCount; i++) {
                            l.AddNoLock(wrt.GetWeakRef(i));
                        }
                    }
                }
                return l;
            }

            #endregion

            [SpecialName]
            public object Call(CodeContext context) {
                object res = _target.Target;
                GC.KeepAlive(this);
                return res;
            }

            [return: MaybeNotImplemented]
            public static NotImplementedType operator >(@ref self, object other) {
                return PythonOps.NotImplemented;
            }

            [return: MaybeNotImplemented]
            public static NotImplementedType operator <(@ref self, object other) {
                return PythonOps.NotImplemented;
            }

            [return: MaybeNotImplemented]
            public static NotImplementedType operator <=(@ref self, object other) {
                return PythonOps.NotImplemented;
            }

            [return: MaybeNotImplemented]
            public static NotImplementedType operator >=(@ref self, object other) {
                return PythonOps.NotImplemented;
            }

            #region IValueEquality Members
#if CLR2
            int IValueEquality.GetValueHashCode() {
                return __hash__(DefaultContext.Default);
            }

            bool IValueEquality.ValueEquals(object other) {
                return EqualsWorker(other, null);
            }
#endif
            #endregion

            #region IStructuralEquatable Members

            /// <summary>
            /// Special hash function because IStructuralEquatable.GetHashCode is not allowed to throw.
            /// </summary>
            public int __hash__(CodeContext/*!*/ context) {
                if (!_fHasHash) {
                    object refObj = _target.Target;
                    if (refObj == null) throw PythonOps.TypeError("weak object has gone away");
                    _hashVal = PythonContext.GetContext(context).EqualityComparerNonGeneric.GetHashCode(refObj);
                    _fHasHash = true;
                }
                GC.KeepAlive(this);
                return _hashVal;
            }

            int IStructuralEquatable.GetHashCode(IEqualityComparer comparer) {
                if (!_fHasHash) {
                    object refObj = _target.Target;
                    _hashVal = comparer.GetHashCode(refObj);
                    _fHasHash = true;
                }
                GC.KeepAlive(this);
                return _hashVal;
            }

            bool IStructuralEquatable.Equals(object other, IEqualityComparer comparer) {
                return EqualsWorker(other, comparer);
            }

            private bool EqualsWorker(object other, IEqualityComparer comparer) {
                if (object.ReferenceEquals(this, other)) {
                    return true;
                }

                bool fResult = false;
                @ref wr = other as @ref;
                if (wr != null) {
                    object ourTarget = _target.Target;
                    object itsTarget = wr._target.Target;

                    if (ourTarget != null && itsTarget != null) {
                        fResult = RefEquals(ourTarget, itsTarget, comparer);
                    } else {
                        fResult = (_targetId == wr._targetId);
                    }
                }
                GC.KeepAlive(this);
                return fResult;
            }

            /// <summary>
            /// Special equals because none of the special cases in Ops.Equals
            /// are applicable here, and the reference equality check breaks some tests.
            /// </summary>
            private static bool RefEquals(object x, object y, IEqualityComparer comparer) {
                CodeContext context;
                if (comparer != null && comparer is PythonContext.PythonEqualityComparer) {
                    context = ((PythonContext.PythonEqualityComparer)comparer).Context.SharedContext;
                } else {
                    context = DefaultContext.Default;
                }

                object ret;
                if (PythonTypeOps.TryInvokeBinaryOperator(context, x, y, "__eq__", out ret) &&
                    ret != NotImplementedType.Value) {
                    return (bool)ret;
                }

                if (PythonTypeOps.TryInvokeBinaryOperator(context, y, x, "__eq__", out ret) &&
                    ret != NotImplementedType.Value) {
                    return (bool)ret;
                }

                if (comparer != null) {
                    return comparer.Equals(x, y);
                }

                return x.Equals(y);
            }


            #endregion            
        }

        [PythonType, DynamicBaseTypeAttribute, PythonHidden]
        public sealed partial class weakproxy : IPythonObject, ICodeFormattable, IProxyObject, IPythonMembersList, IStructuralEquatable
#if CLR2
            , IValueEquality
#endif
        {
            private readonly WeakHandle _target;
            private readonly CodeContext/*!*/ _context;

            #region Python Constructors
            internal static object MakeNew(CodeContext/*!*/ context, object @object, object callback) {
                IWeakReferenceable iwr = ConvertToWeakReferenceable(PythonContext.GetContext(context), @object);

                if (callback == null) {
                    WeakRefTracker wrt = iwr.GetWeakRef();
                    if (wrt != null) {
                        for (int i = 0; i < wrt.HandlerCount; i++) {
                            if (wrt.GetHandlerCallback(i) == null && wrt.GetWeakRef(i) is weakproxy) {
                                return wrt.GetWeakRef(i);
                            }
                        }
                    }
                }

                return new weakproxy(context, @object, callback);
            }
            #endregion

            #region Constructors

            private weakproxy(CodeContext/*!*/ context, object target, object callback) {
                WeakRefHelpers.InitializeWeakRef(PythonContext.GetContext(context), this, target, callback);
                _target = new WeakHandle(target, false);
                _context = context;
            }
            #endregion

            #region Finalizer
            ~weakproxy() {
                // remove our self from the chain...
                IWeakReferenceable iwr;
                if (_context.GetPythonContext().TryConvertToWeakReferenceable(_target.Target, out iwr)) {
                    WeakRefTracker wrt = iwr.GetWeakRef();
                    wrt.RemoveHandler(this);
                }

                _target.Free();
            }
            #endregion

            #region private members
            /// <summary>
            /// gets the object or throws a reference exception
            /// </summary>
            object GetObject() {
                object res;
                if (!TryGetObject(out res)) {
                    throw PythonOps.ReferenceError("weakly referenced object no longer exists");
                }
                return res;
            }

            bool TryGetObject(out object result) {
                result = _target.Target;
                if (result == null) return false;
                GC.KeepAlive(this);
                return true;
            }
            #endregion

            #region IPythonObject Members

            PythonDictionary IPythonObject.Dict {
                get {
                    IPythonObject sdo = GetObject() as IPythonObject;
                    if (sdo != null) {
                        return sdo.Dict;
                    }

                    return null;
                }
            }

            PythonDictionary IPythonObject.SetDict(PythonDictionary dict) {
                return (GetObject() as IPythonObject).SetDict(dict);
            }

            bool IPythonObject.ReplaceDict(PythonDictionary dict) {
                return (GetObject() as IPythonObject).ReplaceDict(dict);
            }

            void IPythonObject.SetPythonType(PythonType newType) {
                (GetObject() as IPythonObject).SetPythonType(newType);
            }

            PythonType IPythonObject.PythonType {
                get {
                    return DynamicHelpers.GetPythonTypeFromType(typeof(weakproxy));
                }
            }

            object[] IPythonObject.GetSlots() { return null; }
            object[] IPythonObject.GetSlotsCreate() { return null; }
            
            #endregion

            #region object overloads

            public override string ToString() {
                return PythonOps.ToString(GetObject());
            }

            #endregion

            #region ICodeFormattable Members

            public string/*!*/ __repr__(CodeContext/*!*/ context) {
                object obj = _target.Target;
                GC.KeepAlive(this);
                return String.Format("<weakproxy at {0} to {1} at {2}>",
                    IdDispenser.GetId(this),
                    PythonOps.GetPythonTypeName(obj),
                    IdDispenser.GetId(obj));
            }

            #endregion

            #region Custom member access

            [SpecialName]
            public object GetCustomMember(CodeContext/*!*/ context, string name) {
                object value, o = GetObject();
                if (PythonOps.TryGetBoundAttr(context, o, name, out value)) {
                    return value;
                }

                return OperationFailed.Value;
            }

            [SpecialName]
            public void SetMember(CodeContext/*!*/ context, string name, object value) {
                object o = GetObject();
                PythonOps.SetAttr(context, o, name, value);
            }

            [SpecialName]
            public void DeleteMember(CodeContext/*!*/ context, string name) {
                object o = GetObject();
                PythonOps.DeleteAttr(context, o, name);
            }

            IList<string> IMembersList.GetMemberNames() {
                return PythonOps.GetStringMemberList(this);
            }

            IList<object> IPythonMembersList.GetMemberNames(CodeContext/*!*/ context) {
                object o;
                if (!TryGetObject(out o)) {
                    // if we've been disconnected return an empty list
                    return new List();
                }

                return PythonOps.GetAttrNames(context, o);
            }

            #endregion

            #region IProxyObject Members

            object IProxyObject.Target {
                get { return GetObject(); }
            }

            #endregion

            #region IValueEquality Members
#if CLR2
            int IValueEquality.GetValueHashCode() {
                throw PythonOps.TypeErrorForUnhashableType("weakproxy");
            }

            bool IValueEquality.ValueEquals(object other) {
                weakproxy wrp = other as weakproxy;
                if (wrp != null) return EqualsWorker(wrp);

                return PythonOps.EqualRetBool(_context, GetObject(), other);
            }
#endif
            #endregion

            #region IStructuralEquatable Members

            public const object __hash__ = null;

            private bool EqualsWorker(weakproxy other) {
                return PythonOps.EqualRetBool(_context, GetObject(), other.GetObject());
            }

            /// <summary>
            /// Special equality function because IStructuralEquatable.Equals is not allowed to throw.
            /// </summary>
            [return: MaybeNotImplemented]
            public object __eq__(object other) {
                if (!(other is weakproxy)) return NotImplementedType.Value;

                return ScriptingRuntimeHelpers.BooleanToObject(EqualsWorker((weakproxy)other));
            }

            [return: MaybeNotImplemented]
            public object __ne__(object other) {
                if (!(other is weakproxy)) return NotImplementedType.Value;

                return ScriptingRuntimeHelpers.BooleanToObject(!EqualsWorker((weakproxy)other));
            }

            int IStructuralEquatable.GetHashCode(IEqualityComparer comparer) {
                object obj;
                if (TryGetObject(out obj)) {
                    return comparer.GetHashCode(obj);
                }
                return comparer.GetHashCode(null);
            }

            bool IStructuralEquatable.Equals(object other, IEqualityComparer comparer) {
                object obj;
                if (!TryGetObject(out obj)) {
                    obj = null;
                }

                weakproxy wrp = other as weakproxy;
                if (wrp != null) {
                    object otherObj;
                    if (!TryGetObject(out otherObj)) {
                        otherObj = null;
                    }

                    return comparer.Equals(obj, otherObj);
                }

                return comparer.Equals(obj, other);
            }
            
            #endregion

            public object __bool__() {
                return Converter.ConvertToBoolean(GetObject());
            }

            public static explicit operator bool(weakproxy self) {
                return Converter.ConvertToBoolean(self.GetObject());
            }
        }

        [PythonType, DynamicBaseTypeAttribute, PythonHidden]
        public sealed partial class weakcallableproxy :
            IPythonObject,
            ICodeFormattable,            
            IProxyObject,
            IStructuralEquatable,
#if CLR2
            IValueEquality,
#endif
            IPythonMembersList {

            private WeakHandle _target;
            private readonly CodeContext/*!*/ _context;

            #region Python Constructors

            internal static object MakeNew(CodeContext/*!*/ context, object @object, object callback) {
                IWeakReferenceable iwr = ConvertToWeakReferenceable(PythonContext.GetContext(context), @object);

                if (callback == null) {
                    WeakRefTracker wrt = iwr.GetWeakRef();
                    if (wrt != null) {
                        for (int i = 0; i < wrt.HandlerCount; i++) {

                            if (wrt.GetHandlerCallback(i) == null &&
                                wrt.GetWeakRef(i) is weakcallableproxy) {
                                return wrt.GetWeakRef(i);
                            }
                        }
                    }
                }

                return new weakcallableproxy(context, @object, callback);
            }

            #endregion

            #region Constructors

            private weakcallableproxy(CodeContext context, object target, object callback) {
                WeakRefHelpers.InitializeWeakRef(PythonContext.GetContext(context), this, target, callback);
                _target = new WeakHandle(target, false);
                _context = context;
            }

            #endregion

            #region Finalizer

            ~weakcallableproxy() {
                // remove our self from the chain...
                IWeakReferenceable iwr;
                if (_context.GetPythonContext().TryConvertToWeakReferenceable(_target.Target, out iwr))
                {
                    WeakRefTracker wrt = iwr.GetWeakRef();
                    wrt.RemoveHandler(this);
                }
                _target.Free();
            }

            #endregion

            #region private members

            /// <summary>
            /// gets the object or throws a reference exception
            /// </summary>
            private object GetObject() {
                object res;
                if (!TryGetObject(out res)) {
                    throw PythonOps.ReferenceError("weakly referenced object no longer exists");
                }
                return res;
            }

            private bool TryGetObject(out object result) {
                try {
                    result = _target.Target;
                    if (result == null) return false;
                    GC.KeepAlive(this);
                    return true;
                } catch (InvalidOperationException) {
                    result = null;
                    return false;
                }
            }

            #endregion

            #region IPythonObject Members

            PythonDictionary IPythonObject.Dict {
                get {
                    return (GetObject() as IPythonObject).Dict;
                }
            }

            PythonDictionary IPythonObject.SetDict(PythonDictionary dict) {
                return (GetObject() as IPythonObject).SetDict(dict);
            }

            bool IPythonObject.ReplaceDict(PythonDictionary dict) {
                return (GetObject() as IPythonObject).ReplaceDict(dict);
            }

            void IPythonObject.SetPythonType(PythonType newType) {
                (GetObject() as IPythonObject).SetPythonType(newType);
            }

            PythonType IPythonObject.PythonType {
                get {
                    return DynamicHelpers.GetPythonTypeFromType(typeof(weakcallableproxy));
                }
            }

            object[] IPythonObject.GetSlots() { return null; }
            object[] IPythonObject.GetSlotsCreate() { return null; }
            
            #endregion

            #region object overloads

            public override string ToString() {
                return PythonOps.ToString(GetObject());
            }

            #endregion

            #region ICodeFormattable Members

            public string/*!*/ __repr__(CodeContext/*!*/ context) {
                object obj = _target.Target;
                GC.KeepAlive(this);
                return String.Format("<weakproxy at {0} to {1} at {2}>",
                    IdDispenser.GetId(this),
                    PythonOps.GetPythonTypeName(obj),
                    IdDispenser.GetId(obj));
            }

            #endregion

            [SpecialName]
            public object Call(CodeContext/*!*/ context, params object[] args) {
                return PythonContext.GetContext(context).CallSplat(GetObject(), args);
            }
                        
            [SpecialName]
            public object Call(CodeContext/*!*/ context, [ParamDictionary]IDictionary<object, object> dict, params object[] args) {
                return PythonCalls.CallWithKeywordArgs(context, GetObject(), args, dict);
            }

            #region Custom members access

            [SpecialName]
            public object GetCustomMember(CodeContext/*!*/ context, string name) {
                object o = GetObject();
                object value;
                if (PythonOps.TryGetBoundAttr(context, o, name, out value)) {
                    return value;
                }
                return OperationFailed.Value;
            }

            [SpecialName]
            public void SetMember(CodeContext/*!*/ context, string name, object value) {
                object o = GetObject();
                PythonOps.SetAttr(context, o, name, value);
            }

            [SpecialName]
            public void DeleteMember(CodeContext/*!*/ context, string name) {
                object o = GetObject();
                PythonOps.DeleteAttr(context, o, name);
            }

            IList<string> IMembersList.GetMemberNames() {
                return PythonOps.GetStringMemberList(this);
            }

            IList<object> IPythonMembersList.GetMemberNames(CodeContext/*!*/ context) {
                object o;
                if (!TryGetObject(out o)) {
                    // if we've been disconnected return an empty list
                    return new List();
                }

                return PythonOps.GetAttrNames(context, o);
            }

            #endregion

            #region IProxyObject Members

            object IProxyObject.Target {
                get { return GetObject(); }
            }

            #endregion

            #region IValueEquality Members
#if CLR2
            int IValueEquality.GetValueHashCode() {
                throw PythonOps.TypeErrorForUnhashableType("weakcallableproxy");
            }

            bool IValueEquality.ValueEquals(object other) {
                return __eq__(other);
            }
#endif
            #endregion

            #region IStructuralEquatable Members

            public const object __hash__ = null;

            /// <summary>
            /// Special equality function because IStructuralEquatable.Equals is not allowed to throw.
            /// </summary>
            public bool __eq__(object other) {
                weakcallableproxy wrp = other as weakcallableproxy;
                if (wrp != null) return GetObject().Equals(wrp.GetObject());

                return PythonOps.EqualRetBool(_context, GetObject(), other);
            }

            public bool __ne__(object other) {
                return !__eq__(other);
            }

            int IStructuralEquatable.GetHashCode(IEqualityComparer comparer) {
                object obj;
                if (TryGetObject(out obj)) {
                    return comparer.GetHashCode(obj);
                }
                return comparer.GetHashCode(null);
            }

            bool IStructuralEquatable.Equals(object other, IEqualityComparer comparer) {
                object obj;
                if (!TryGetObject(out obj)) {
                    obj = null;
                }

                weakcallableproxy wrp = other as weakcallableproxy;
                if (wrp != null) {
                    object otherObj;
                    if (!TryGetObject(out otherObj)) {
                        otherObj = null;
                    }

                    return comparer.Equals(obj, otherObj);
                }

                return comparer.Equals(obj, other);
            }

            #endregion

            public object __bool__() {
                return Converter.ConvertToBoolean(GetObject());
            }
        }

        static class WeakRefHelpers {
            public static WeakRefTracker InitializeWeakRef(PythonContext context, object self, object target, object callback) {
                IWeakReferenceable iwr = ConvertToWeakReferenceable(context, target);

                WeakRefTracker wrt = iwr.GetWeakRef();
                if (wrt == null) {
                    if (!iwr.SetWeakRef(wrt = new WeakRefTracker(iwr))) 
                        throw PythonOps.TypeError("cannot create weak reference to '{0}' object", PythonOps.GetPythonTypeName(target));
                }

                wrt.ChainCallback(callback,self);
                return wrt;
            }
        }
    }

    [PythonType("wrapper_descriptor")]
    class SlotWrapper : PythonTypeSlot, ICodeFormattable {
        private readonly string _name;
        private readonly PythonType _type;

        public SlotWrapper(string slotName, PythonType targetType) {
            _name = slotName;
            _type = targetType;
        }

        #region ICodeFormattable Members

        public virtual string/*!*/ __repr__(CodeContext/*!*/ context) {
            return String.Format("<slot wrapper {0} of {1} objects>",
                PythonOps.Repr(context, _name),
                PythonOps.Repr(context, _type.Name));
        }

        #endregion

        #region PythonTypeSlot Overrides

        internal override bool TryGetValue(CodeContext context, object instance, PythonType owner, out object value) {
            if (instance == null) {
                value = this;
                return true;
            }

            IProxyObject proxy = instance as IProxyObject;

            if (proxy == null)
                throw PythonOps.TypeError("descriptor for {0} object doesn't apply to {1} object",
                    PythonOps.Repr(context, _type.Name),
                    PythonOps.Repr(context, PythonTypeOps.GetName(instance)));

            if (!DynamicHelpers.GetPythonType(proxy.Target).TryGetBoundMember(context, proxy.Target, _name, out value))
                return false;

            value = new GenericMethodWrapper(_name, proxy);
            return true;
        }

        #endregion
    }

    [PythonType("method-wrapper")]
    public class GenericMethodWrapper {
        string name;
        IProxyObject target;

        public GenericMethodWrapper(string methodName, IProxyObject proxyTarget) {
            name = methodName;
            target = proxyTarget;
        }

        [SpecialName]
        public object Call(CodeContext context, params object[] args) {
            return PythonOps.Invoke(context, target.Target, name, args);
        }


        [SpecialName]
        public object Call(CodeContext context, [ParamDictionary]IDictionary<object, object> dict, params object[] args) {
            object targetMethod;
            if (!DynamicHelpers.GetPythonType(target.Target).TryGetBoundMember(context, target.Target, name, out targetMethod))
                throw PythonOps.AttributeError("type {0} has no attribute {1}",
                    DynamicHelpers.GetPythonType(target.Target),
                    name);

            return PythonCalls.CallWithKeywordArgs(context, targetMethod, args, dict);
        }
    }
}
