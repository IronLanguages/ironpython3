// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.
#if !FEATURE_REFEMIT

using System.Linq.Expressions;

using System;
using System.Diagnostics;
using System.Dynamic;
using System.Runtime.CompilerServices;

using IronPython.Runtime;
using IronPython.Runtime.Types;
using IronPython.Runtime.Operations;

namespace IronPython.NewTypes {
    // Type: IronPython.NewTypes.System.Object_1$1
    // Assembly: Snippets.scripting, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
    // Assembly location: C:\Users\Jeff\Documents\Repositories\ironlanguages\Scratch\Snippets.scripting.dll

    namespace System {
        [DebuggerTypeProxy(typeof(UserTypeDebugView))]
        [DebuggerDisplay("{get_PythonType().GetTypeDebuggerDisplay()}")]
        public class Object_1_1 : IPythonObject, IDynamicMetaObjectProvider, IWeakReferenceable {
            private static CallSite<Func<CallSite, CodeContext, object, object>> site_0 = CallSite<Func<CallSite, CodeContext, object, object>>.Create((CallSiteBinder)PythonOps.MakeSimpleCallAction(0));
            private static CallSite<Func<CallSite, CodeContext, object, object, object>> site_1 = CallSite<Func<CallSite, CodeContext, object, object, object>>.Create((CallSiteBinder)PythonOps.MakeSimpleCallAction(1));
            private static CallSite<Func<CallSite, CodeContext, object, object>> site_2 = CallSite<Func<CallSite, CodeContext, object, object>>.Create((CallSiteBinder)PythonOps.MakeSimpleCallAction(0));
            private static CallSite<Func<CallSite, CodeContext, object, object>> site_3 = CallSite<Func<CallSite, CodeContext, object, object>>.Create((CallSiteBinder)PythonOps.MakeSimpleCallAction(0));
            public PythonType _class;
            public object[] _slots_and_weakref;
            public PythonDictionary _dict;

            public Object_1_1(PythonType cls) {
                this._class = cls;
                this._slots_and_weakref = PythonOps.InitializeUserTypeSlots(cls);
            }

            PythonDictionary IPythonObject.Dict {
                get { return this._dict; }
            }

            bool IPythonObject.ReplaceDict(PythonDictionary dict) {
                this._dict = dict;
                return true;
            }

            PythonDictionary IPythonObject.SetDict(PythonDictionary dict) {
                return UserTypeOps.SetDictHelper(ref this._dict, dict);
            }

            PythonType IPythonObject.PythonType {
                get { return this._class; }
            }

            void IPythonObject.SetPythonType(PythonType newType) {
                this._class = newType;
            }

            object[] IPythonObject.GetSlots() {
                return this._slots_and_weakref;
            }

            object[] IPythonObject.GetSlotsCreate() {
                return UserTypeOps.GetSlotsCreate((IPythonObject)this, ref this._slots_and_weakref);
            }

            DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter) {
                return UserTypeOps.GetMetaObjectHelper((IPythonObject)this, parameter, (DynamicMetaObject)null);
            }

            WeakRefTracker IWeakReferenceable.GetWeakRef() {
                return UserTypeOps.GetWeakRefHelper((IPythonObject)this);
            }

            bool IWeakReferenceable.SetWeakRef(WeakRefTracker obj0) {
                return UserTypeOps.SetWeakRefHelper((IPythonObject)this, obj0);
            }

            void IWeakReferenceable.SetFinalizer(WeakRefTracker obj0) {
                UserTypeOps.SetFinalizerHelper((IPythonObject)this, obj0);
            }

            public override string ToString() {
                object obj;
                if (!UserTypeOps.TryGetNonInheritedMethodHelper(this._class, (object)this, "ToString", out obj))
                    return base.ToString();
                else
                    return Converter.ConvertToString(Object_1_1.site_0.Target((CallSite)Object_1_1.site_0, DefaultContext.Default, obj));
            }

            [SpecialName]
            public string _base_ToString() {
                return base.ToString();
            }

            public override bool Equals(object obj) {
                object obj1;
                if (!UserTypeOps.TryGetNonInheritedMethodHelper(this._class, (object)this, "Equals", out obj1))
                    return base.Equals(obj);
                else
                    return Converter.ConvertToBoolean(Object_1_1.site_1.Target((CallSite)Object_1_1.site_1, DefaultContext.Default, obj1, obj));
            }

            [SpecialName]
            public bool _base_Equals(Object_1_1 obj) {
                return base.Equals((object)obj);
            }

            public override int GetHashCode() {
                object obj;
                if (!UserTypeOps.TryGetNonInheritedMethodHelper(this._class, (object)this, "GetHashCode", out obj))
                    return base.GetHashCode();
                else
                    return Converter.ConvertToInt32(Object_1_1.site_2.Target((CallSite)Object_1_1.site_2, DefaultContext.Default, obj));
            }

            [SpecialName]
            public int _base_GetHashCode() {
                return base.GetHashCode();
            }

            public new object MemberwiseClone() {
                object obj;
                if (!UserTypeOps.TryGetNonInheritedMethodHelper(this._class, (object)this, "MemberwiseClone", out obj))
                    return base.MemberwiseClone();
                else
                    return Object_1_1.site_3.Target((CallSite)Object_1_1.site_3, DefaultContext.Default, obj);
            }

            [SpecialName]
            public object _base_MemberwiseClone() {
                return base.MemberwiseClone();
            }
        }
    }

}

#endif
