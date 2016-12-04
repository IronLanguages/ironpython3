﻿/* ****************************************************************************
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using IronPython.Compiler;
using IronPython.Runtime.Types;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

#if !FEATURE_TYPE_INFO
using TypeInfo = System.Type;
#endif

namespace IronPython.Runtime {
    /// <summary>
    /// Enables lazy initialization of module dictionaries.
    /// </summary>
    class ModuleDictionaryStorage : GlobalDictionaryStorage {
        private Type/*!*/ _type;
        private bool _cleared;

        private static readonly Dictionary<string, PythonGlobal> _emptyGlobalDict = new Dictionary<string, PythonGlobal>(0);
        private static readonly PythonGlobal[] _emptyGlobals = new PythonGlobal[0];

        public ModuleDictionaryStorage(Type/*!*/ moduleType)
            : base(_emptyGlobalDict, _emptyGlobals) {
            Debug.Assert(moduleType != null);

            _type = moduleType;
        }

        public ModuleDictionaryStorage(Type/*!*/ moduleType, Dictionary<string, PythonGlobal> globals)
            : base(globals, _emptyGlobals) {
            Debug.Assert(moduleType != null);

            _type = moduleType;
        }

        public virtual BuiltinPythonModule Instance {
            get {
                return null;
            }
        }

        public override bool Remove(ref DictionaryStorage storage, object key) {
            string strKey = key as string;
            if (strKey == null) {
                return base.Remove(ref storage, key);
            }

            bool found = base.Remove(ref storage, key);
            object value;
            if (TryGetLazyValue(strKey, out value)) {
                // hide the deleted value
                base.Add(key, Uninitialized.Instance);
                found = true;
            }

            return found;
        }

        protected virtual void LazyAdd(object name, object value) {
            Add(name, value);
        }

        public override bool Contains(object key) {
            object dummy;
            return TryGetValue(key, out dummy);
        }

        public override void Clear(ref DictionaryStorage storage) {
            _cleared = true;
            base.Clear(ref storage);
        }

        public override List<KeyValuePair<object, object>> GetItems() {
            List<KeyValuePair<object, object>> res = new List<KeyValuePair<object, object>>();

            foreach (KeyValuePair<object, object> kvp in base.GetItems()) {
                if (kvp.Value != Uninitialized.Instance) {
                    res.Add(kvp);
                }
            }

            MemberInfo[] members = _type.GetMembers();
            foreach (MemberInfo mi in members) {
                if (base.Contains(mi.Name)) continue;

                object value;
                if (TryGetLazyValue(mi.Name, out value)) {
                    res.Add(new KeyValuePair<object, object>(mi.Name, value));
                }
            }
            return res;
        }

        public override int Count {
            get {
                // need to ensure we're fully populated
                GetItems();
                return base.Count;
            }
        }

        private bool TryGetLazyValue(string name, out object value) {
            return TryGetLazyValue(name, true, out value);
        }

        private bool TryGetLazyValue(string name, bool publish, out object value) {
            if (!_cleared) {
                MemberInfo[] members = NonHiddenMembers(GetMember(name));
                if (members.Length > 0) {
                    // we only support fields, methods, and nested types in modules.
                    switch (members[0].MemberType) {
                        case MemberTypes.Field:
                            Debug.Assert(members.Length == 1);

                            var fieldInfo = (FieldInfo)members[0];
                            if (fieldInfo.IsStatic) {
                                value = ((FieldInfo)members[0]).GetValue(null);
                            } else {
                                throw new InvalidOperationException("instance field declared on module.  Fields should stored as PythonGlobals, should be static readonly, or marked as PythonHidden.");
                            }

                            if (publish) {
                                LazyAdd(name, value);
                            }
                            return true;
                        case MemberTypes.Method:
                            if (!((MethodInfo)members[0]).IsSpecialName) {
                                var methods = new MethodInfo[members.Length];
                                FunctionType ft = FunctionType.ModuleMethod | FunctionType.AlwaysVisible;
                                for (int i = 0; i < members.Length; i++) {
                                    var method = (MethodInfo)members[i];
                                    if (method.IsStatic) {
                                        ft |= FunctionType.Function;
                                    } else {
                                        ft |= FunctionType.Method;
                                    }

                                    methods[i] = method;
                                }

                                var builtinFunc = BuiltinFunction.MakeMethod(
                                    name,
                                    methods,
                                    members[0].DeclaringType,
                                    ft
                                );

                                if ((ft & FunctionType.Method) != 0 && Instance != null) {
                                    value = builtinFunc.BindToInstance(Instance);
                                } else {
                                    value = builtinFunc;
                                }

                                if (publish) {
                                    LazyAdd(name, value);
                                }
                                return true;
                            }
                            break;
                        case MemberTypes.Property:
                            Debug.Assert(members.Length == 1);

                            var propInfo = (PropertyInfo)members[0];
                            if ((propInfo.GetGetMethod() ?? propInfo.GetSetMethod()).IsStatic) {
                                value = ((PropertyInfo)members[0]).GetValue(null, ArrayUtils.EmptyObjects);
                            } else {
                                throw new InvalidOperationException("instance property declared on module.  Propreties should be declared as static, marked as PythonHidden, or you should use a PythonGlobal.");
                            }

                            if (publish) {
                                LazyAdd(name, value);
                            }

                            return true;
                        case MemberTypes.NestedType:
                            if (members.Length == 1) {
                                value = DynamicHelpers.GetPythonTypeFromType(((TypeInfo)members[0]).AsType());
                            } else {
                                TypeTracker tt = (TypeTracker)MemberTracker.FromMemberInfo(members[0]);
                                for (int i = 1; i < members.Length; i++) {
                                    tt = TypeGroup.UpdateTypeEntity(tt, (TypeTracker)MemberTracker.FromMemberInfo(members[i]));
                                }

                                value = tt;
                            }

                            if (publish) {
                                LazyAdd(name, value);
                            }
                            return true;
                    }
                }
            }

            value = null;
            return false;
        }

        private static MemberInfo[] NonHiddenMembers(MemberInfo[] members) {
            List<MemberInfo> res = new List<MemberInfo>(members.Length);
            foreach (MemberInfo t in members) {
                if (t.IsDefined(typeof(PythonHiddenAttribute), false)) {
                    continue;
                }

                res.Add(t);
            }
            return res.ToArray();
        }

        private MemberInfo[] GetMember(string name) {
            return _type.GetMember(name, BindingFlags.DeclaredOnly | BindingFlags.Public | (Instance == null ? BindingFlags.Static : (BindingFlags.Instance | BindingFlags.Static)));
        }

        public override bool TryGetValue(object key, out object value) {
            if (base.TryGetValue(key, out value)) {
                return value != Uninitialized.Instance;
            }

            string strKey = key as string;
            if(strKey != null) {
                return TryGetLazyValue(strKey, out value);
            }

            return false;
        }

        public virtual void Reload() {
            foreach (KeyValuePair<object, object> kvp in base.GetItems()) {
                if (kvp.Value == Uninitialized.Instance) {
                    // hiding a member
                    base.Remove(kvp.Key);
                } else {
                    // member exists, need to remove it from the base class
                    // in case it differs from the member we actually have.
                    string strKey = kvp.Key as string;
                    if (strKey != null && GetMember(strKey).Length > 0) {
                        base.Remove(kvp.Key);
                    }
                }
            }
        }
    }
}
