// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using IronPython.Runtime.Operations;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime.Types {
    /// <summary>
    /// Base class for properties backed by methods.  These include our slot properties,
    /// indexers, and normal properties.  This class provides the storage of these as well
    /// as the storage of our optimized getter/setter methods, documentation for the property,
    /// etc...
    /// </summary>
    public abstract class ReflectedGetterSetter : PythonTypeSlot {
        private MethodInfo/*!*/[]/*!*/ _getter, _setter;
        private readonly NameType _nameType;
        private BuiltinFunction _getfunc, _setfunc;

        protected ReflectedGetterSetter(MethodInfo[]/*!*/ getter, MethodInfo[]/*!*/ setter, NameType nt) {
            Debug.Assert(getter != null);
            Debug.Assert(setter != null);

            _getter = RemoveNullEntries(getter);
            _setter = RemoveNullEntries(setter);
            _nameType = nt;
        }

        protected ReflectedGetterSetter(ReflectedGetterSetter from) {
            _getter = from._getter;
            _setter = from._setter;
            _nameType = from._nameType;
        }

        internal void AddGetter(MethodInfo mi) {
            lock (this) {
                _getter = ArrayUtils.Append(_getter, mi);
                MakeGetFunc();
            }
        }

        private void MakeGetFunc() {
            _getfunc = PythonTypeOps.GetBuiltinFunction(DeclaringType, __name__, _getter);
        }

        internal void AddSetter(MethodInfo mi) {
            lock (this) {
                _setter = ArrayUtils.Append(_setter, mi);
                MakeSetFunc();
            }
        }

        private void MakeSetFunc() {
            _setfunc = PythonTypeOps.GetBuiltinFunction(DeclaringType, __name__, _setter);
        }

        internal abstract Type DeclaringType {
            get;
        }

        public abstract string __name__ {
            get;
        }

        public PythonType/*!*/ __objclass__ {
            get {
                return DynamicHelpers.GetPythonTypeFromType(DeclaringType);
            }
        }

        internal MethodInfo/*!*/[]/*!*/ Getter {
            get {
                return _getter;
            }
        }

        internal MethodInfo/*!*/[]/*!*/ Setter {
            get {
                return _setter;
            }
        }

        public virtual PythonType PropertyType {
            [PythonHidden]
            get {
                if (Getter != null && Getter.Length > 0) {
                    return DynamicHelpers.GetPythonTypeFromType(Getter[0].ReturnType);
                }
                return DynamicHelpers.GetPythonTypeFromType(typeof(object));
            }
        }

        internal NameType NameType {
            get {
                return _nameType;
            }
        }

        internal object CallGetter(CodeContext context, SiteLocalStorage<CallSite<Func<CallSite, CodeContext, object, object[], object>>> storage, object instance, object[] args) {
            if (NeedToReturnProperty(instance, Getter)) {
                return this;
            }

            if (Getter.Length == 0) {
                throw new MissingMemberException("unreadable property");
            }

            if (_getfunc == null) {
                lock (this) {
                    if (_getfunc == null) {
                        MakeGetFunc();
                    }
                }
            }

            return _getfunc.Call(context, storage, instance, args);
        }

        internal object CallTarget(CodeContext context, SiteLocalStorage<CallSite<Func<CallSite, CodeContext, object, object[], object>>> storage, MethodInfo[] targets, object instance, params object[] args) {
            BuiltinFunction target = PythonTypeOps.GetBuiltinFunction(DeclaringType, __name__, targets);

            return target.Call(context, storage, instance, args);
        }

        internal static bool NeedToReturnProperty(object instance, MethodInfo[] mis) {
            if (instance == null) {
                if (mis.Length == 0) {
                    return true;
                }

                foreach (MethodInfo mi in mis) {
                    if (!mi.IsStatic || 
                        (mi.IsDefined(typeof(PropertyMethodAttribute), true) && 
                        !mi.IsDefined(typeof(StaticExtensionMethodAttribute), true)) &&
                        !mi.IsDefined(typeof(WrapperDescriptorAttribute), true)) {
                        return true;
                    }
                }
            }
            return false;
        }

        internal bool CallSetter(CodeContext context, SiteLocalStorage<CallSite<Func<CallSite, CodeContext, object, object[], object>>> storage, object instance, object[] args, object value) {
            if (NeedToReturnProperty(instance, Setter)) {
                return false;
            }

            if (_setfunc == null) {
                lock (this) {
                    if (_setfunc == null) {
                        MakeSetFunc();
                    }
                }
            }

            if (args.Length != 0) {
                _setfunc.Call(context, storage, instance, ArrayUtils.Append(args, value));
            } else {
                _setfunc.Call(context, storage, instance, new [] { value });
            }

            return true;
        }

        internal override bool IsAlwaysVisible {
            get {
                return _nameType == NameType.PythonProperty;
            }
        }

        private static MethodInfo[] RemoveNullEntries(MethodInfo[] mis) {
            List<MethodInfo> res = null;
            for (int i = 0; i < mis.Length; i++) {
                if (mis[i] == null) {
                    if (res == null) {
                        res = new List<MethodInfo>();
                        for (int j = 0; j < i; j++) {
                            res.Add(mis[j]);
                        }
                    }
                } else if (res != null) {
                    res.Add(mis[i]);
                }
            }

            if (res != null) {
                return res.ToArray();
            }
            return mis;
        }
    }
}
