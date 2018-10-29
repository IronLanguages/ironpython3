// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Threading;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Types;
using IronPython.Runtime.Binding;

namespace IronPython.Runtime.Operations {
    
    // These operations get linked into all new-style classes. 
    public static class UserTypeOps {
        public static string ToStringReturnHelper(object o) {
            if (o is string && o != null) {
                return (string)o;
            }
            throw PythonOps.TypeError("__str__ returned non-string type ({0})", PythonTypeOps.GetName(o));
        }

        public static PythonDictionary SetDictHelper(ref PythonDictionary dict, PythonDictionary value) {
            if (Interlocked.CompareExchange<PythonDictionary>(ref dict, value, null) == null)
                return value;
            return dict;
        }

        public static object GetPropertyHelper(object prop, object instance, string name) {
            PythonTypeSlot desc = prop as PythonTypeSlot;
            if (desc == null) {
                throw PythonOps.TypeError("Expected property for {0}, but found {1}",
                    name.ToString(), DynamicHelpers.GetPythonType(prop).Name);
            }
            object value;
            desc.TryGetValue(DefaultContext.Default, instance, DynamicHelpers.GetPythonType(instance), out value);
            return value;
        }

        public static void SetPropertyHelper(object prop, object instance, object newValue, string name) {
            PythonTypeSlot desc = prop as PythonTypeSlot;
            if (desc == null) {
                throw PythonOps.TypeError("Expected settable property for {0}, but found {1}",
                    name.ToString(), DynamicHelpers.GetPythonType(prop).Name);
            }
            desc.TrySetValue(DefaultContext.Default, instance, DynamicHelpers.GetPythonType(instance), newValue);
        }

        public static bool SetWeakRefHelper(IPythonObject obj, WeakRefTracker value) {
            if (!obj.PythonType.IsWeakReferencable) {
                return false;
            }

            object[] slots = obj.GetSlotsCreate();
            slots[slots.Length - 1] = value;
            return true;            
        }

        public static WeakRefTracker GetWeakRefHelper(IPythonObject obj) {
            object[] slots = obj.GetSlots();
            if (slots == null) {
                return null;
            }

            return (WeakRefTracker)slots[slots.Length - 1];
        }

        public static void SetFinalizerHelper(IPythonObject obj, WeakRefTracker value) {
            object[] slots = obj.GetSlotsCreate();
            if (Interlocked.CompareExchange(ref slots[slots.Length - 1], value, null) != null) {
                GC.SuppressFinalize(value);
            }
        }

        public static object[] GetSlotsCreate(IPythonObject obj, ref object[] slots) {
            if (slots != null) {
                return slots;
            }

            Interlocked.CompareExchange(
                ref slots,
                new object[obj.PythonType.SlotCount + 1],   // weakref is stored at the end
                null);

            return slots;
        }

        public static void AddRemoveEventHelper(object method, IPythonObject instance, object eventValue, string name) {
            object callable = method;

            // TODO: dt gives us a PythonContext which we should use
            PythonType dt = instance.PythonType;
            PythonTypeSlot dts = method as PythonTypeSlot;
            if (dts != null) {
                if (!dts.TryGetValue(DefaultContext.Default, instance, dt, out callable))
                    throw PythonOps.AttributeErrorForMissingAttribute(dt.Name, name);
            }

            if (!PythonOps.IsCallable(DefaultContext.Default, callable)) {
                throw PythonOps.TypeError("Expected callable value for {0}, but found {1}", name.ToString(),
                    PythonTypeOps.GetName(method));
            }

            PythonCalls.Call(callable, eventValue);
        }

        public static DynamicMetaObject/*!*/ GetMetaObjectHelper(IPythonObject self, Expression/*!*/ parameter, DynamicMetaObject baseMetaObject) {
            return new Binding.MetaUserObject(parameter, BindingRestrictions.Empty, baseMetaObject, self);
        }

        public static bool TryGetDictionaryValue(PythonDictionary dict, string name, int keyVersion, int keyIndex, out object res) {
            CustomInstanceDictionaryStorage dictStorage;
            if (dict != null) {
                if ((dictStorage = dict._storage as CustomInstanceDictionaryStorage) != null && dictStorage.KeyVersion == keyVersion) {
                    if (dictStorage.TryGetValue(keyIndex, out res)) {
                        return true;
                    }
                } else if (dict.TryGetValue(name, out res)) {
                    return true;
                }
            }
            res = null;
            return false;
        }

        public static object SetDictionaryValue(IPythonObject self, string name, object value) {
            PythonDictionary dict = GetDictionary(self);

            return dict[name] = value;
        }

        public static object SetDictionaryValueOptimized(IPythonObject ipo, string name, object value, int keysVersion, int index) {
            var dict = UserTypeOps.GetDictionary(ipo);
            CustomInstanceDictionaryStorage storage;

            if ((storage = dict._storage as CustomInstanceDictionaryStorage) != null && storage.KeyVersion == keysVersion) {
                storage.SetExtraValue(index, value);
            } else {
                dict[name] = value;
            }
            return value;
        }

        public static object FastSetDictionaryValue(ref PythonDictionary dict, string name, object value) {
            if (dict == null) {
                Interlocked.CompareExchange(ref dict, PythonDictionary.MakeSymbolDictionary(), null);
            }

            return dict[name] = value;
        }

        public static object FastSetDictionaryValueOptimized(PythonType type, ref PythonDictionary dict, string name, object value, int keysVersion, int index) {
            if (dict == null) {
                Interlocked.CompareExchange(ref dict, type.MakeDictionary(), null);
            }

            CustomInstanceDictionaryStorage storage;

            if ((storage = dict._storage as CustomInstanceDictionaryStorage) != null && storage.KeyVersion == keysVersion) {
                storage.SetExtraValue(index, value);
                return value;
            } else {
                return dict[name] = value;
            }
        }

        public static object RemoveDictionaryValue(IPythonObject self, string name) {
            PythonDictionary dict = self.Dict;
            if (dict != null) {
                if (dict.Remove(name)) {
                    return null;
                }
            }

            throw PythonOps.AttributeErrorForMissingAttribute(self.PythonType, name);
        }

        internal static PythonDictionary GetDictionary(IPythonObject self) {
            PythonDictionary dict = self.Dict;
            if (dict == null && self.PythonType.HasDictionary) {
                dict = self.SetDict(self.PythonType.MakeDictionary());
            }
            return dict;
        }

        /// <summary>
        /// Object.ToString() displays the CLI type name.  But we want to display the class name (e.g.
        /// '&lt;foo object at 0x000000000000002C&gt;' unless we've overridden __repr__ but not __str__ in 
        /// which case we'll display the result of __repr__.
        /// </summary>
        public static string ToStringHelper(IPythonObject o) {
            return ObjectOps.__str__(DefaultContext.Default, o);
        }

        public static bool TryGetNonInheritedMethodHelper(PythonType dt, object instance, string name, out object callTarget) {
            // search MRO for other user-types in the chain that are overriding the method
            foreach (PythonType type in dt.ResolutionOrder) {
                if (type.IsSystemType) break;           // hit the .NET types, we're done

                if (LookupValue(type, instance, name, out callTarget)) {
                    return true;
                }
            }

            // check instance
            IPythonObject isdo = instance as IPythonObject;
            PythonDictionary dict;
            if (isdo != null && (dict = isdo.Dict) != null) {
                if (dict.TryGetValue(name, out callTarget))
                    return true;
            }

            callTarget = null;
            return false;
        }

        private static bool LookupValue(PythonType dt, object instance, string name, out object value) {
            PythonTypeSlot dts;
            if (dt.TryLookupSlot(DefaultContext.Default, name, out dts) &&
                dts.TryGetValue(DefaultContext.Default, instance, dt, out value)) {
                return true;
            }
            value = null;
            return false;
        }

        public static bool TryGetNonInheritedValueHelper(IPythonObject instance, string name, out object callTarget) {
            PythonType dt = instance.PythonType;
            PythonTypeSlot dts;
            // search MRO for other user-types in the chain that are overriding the method
            foreach (PythonType type in dt.ResolutionOrder) {
                if (type.IsSystemType) break;           // hit the .NET types, we're done

                if (type.TryLookupSlot(DefaultContext.Default, name, out dts)) {
                    callTarget = dts;
                    return true;
                }
            }

            // check instance
            IPythonObject isdo = instance as IPythonObject;
            PythonDictionary dict;
            if (isdo != null && (dict = isdo.Dict) != null) {
                if (dict.TryGetValue(name, out callTarget))
                    return true;
            }

            callTarget = null;
            return false;
        }

        public static object GetAttribute(CodeContext/*!*/ context, object self, string name, PythonTypeSlot getAttributeSlot, PythonTypeSlot getAttrSlot, SiteLocalStorage<CallSite<Func<CallSite, CodeContext, object, string, object>>>/*!*/ callSite) {
            object value;
            if (callSite.Data == null) {
                callSite.Data = MakeGetAttrSite(context);
            }

            try {
                if (getAttributeSlot.TryGetValue(context, self, ((IPythonObject)self).PythonType, out value)) {
                    return callSite.Data.Target(callSite.Data, context, value, name);
                } 
            } catch (MissingMemberException) {
                if (getAttrSlot != null && getAttrSlot.TryGetValue(context, self, ((IPythonObject)self).PythonType, out value)) {
                    return callSite.Data.Target(callSite.Data, context, value, name);
                }

                throw;
            }

            if (getAttrSlot != null && getAttrSlot.TryGetValue(context, self, ((IPythonObject)self).PythonType, out value)) {
                return callSite.Data.Target(callSite.Data, context, value, name);
            }

            throw PythonOps.AttributeError(name);
        }

        public static object GetAttributeNoThrow(CodeContext/*!*/ context, object self, string name, PythonTypeSlot getAttributeSlot, PythonTypeSlot getAttrSlot, SiteLocalStorage<CallSite<Func<CallSite, CodeContext, object, string, object>>>/*!*/ callSite) {
            object value;
            if (callSite.Data == null) {
                callSite.Data = MakeGetAttrSite(context);
            }

            try {
                if (getAttributeSlot.TryGetValue(context, self, ((IPythonObject)self).PythonType, out value)) {
                    return callSite.Data.Target(callSite.Data, context, value, name);
                }
            } catch (MissingMemberException) {
                try {
                    if (getAttrSlot != null && getAttrSlot.TryGetValue(context, self, ((IPythonObject)self).PythonType, out value)) {
                        return callSite.Data.Target(callSite.Data, context, value, name);
                    }

                    return OperationFailed.Value;
                } catch (MissingMemberException) {
                    return OperationFailed.Value;
                }
            }

            try {
                if (getAttrSlot != null && getAttrSlot.TryGetValue(context, self, ((IPythonObject)self).PythonType, out value)) {
                    return callSite.Data.Target(callSite.Data, context, value, name);
                }
            } catch (MissingMemberException) {
            }

            return OperationFailed.Value;
        }

        private static CallSite<Func<CallSite, CodeContext, object, string, object>> MakeGetAttrSite(CodeContext context) {
            return CallSite<Func<CallSite, CodeContext, object, string, object>>.Create(
                context.LanguageContext.InvokeOne
            );
        }

        internal static Binding.FastBindResult<T> MakeGetBinding<T>(CodeContext codeContext, CallSite<T> site, IPythonObject self, Binding.PythonGetMemberBinder getBinder) where T : class {
            Type finalType = self.PythonType.FinalSystemType;
            if (typeof(IDynamicMetaObjectProvider).IsAssignableFrom(finalType) &&
                !(self is IFastGettable)) {
                // very tricky, user is inheriting from a class which implements IDO, we
                // don't optimize this yet.
                return new Binding.FastBindResult<T>();
            }
            return (Binding.FastBindResult<T>)(object)new Binding.MetaUserObject.FastGetBinderHelper(
                codeContext,
                (CallSite<Func<CallSite, object, CodeContext, object>>)(object)site, 
                self, 
                getBinder).GetBinding(codeContext, getBinder.Name);
        }

        internal static FastBindResult<T> MakeSetBinding<T>(CodeContext codeContext, CallSite<T> site, IPythonObject self, object value, PythonSetMemberBinder setBinder) where T : class {
            if (typeof(IDynamicMetaObjectProvider).IsAssignableFrom(self.GetType().BaseType)) {
                // very tricky, user is inheriting from a class which implements IDO, we
                // don't optimize this yet.
                return new FastBindResult<T>();
            }

            // optimized versions for possible literals that can show up in code.
            Type setType = typeof(T);
            if (setType == typeof(Func<CallSite, object, object, object>)) {
                return (FastBindResult<T>)(object)new Binding.MetaUserObject.FastSetBinderHelper<object>(
                    codeContext,
                    self,
                    value,
                    setBinder).MakeSet();
            } else if (setType == typeof(Func<CallSite, object, string, object>)) {
                return (FastBindResult<T>)(object)new Binding.MetaUserObject.FastSetBinderHelper<string>(
                    codeContext,
                    self,
                    value,
                    setBinder).MakeSet();
            } else if (setType == typeof(Func<CallSite, object, int, object>)) {
                return (FastBindResult<T>)(object)new Binding.MetaUserObject.FastSetBinderHelper<int>(
                    codeContext,
                    self,
                    value,
                    setBinder).MakeSet();
            } else if (setType == typeof(Func<CallSite, object, double, object>)) {
                return (FastBindResult<T>)(object)new Binding.MetaUserObject.FastSetBinderHelper<double>(
                    codeContext,
                    self,
                    value,
                    setBinder).MakeSet();
            } else if (setType == typeof(Func<CallSite, object, PythonList, object>)) {
                return (FastBindResult<T>)(object)new Binding.MetaUserObject.FastSetBinderHelper<PythonList>(
                    codeContext,
                    self,
                    value,
                    setBinder).MakeSet();
            } else if (setType == typeof(Func<CallSite, object, PythonTuple, object>)) {
                return (FastBindResult<T>)(object)new Binding.MetaUserObject.FastSetBinderHelper<PythonTuple>(
                    codeContext,
                    self,
                    value,
                    setBinder).MakeSet();
            } else if (setType == typeof(Func<CallSite, object, PythonDictionary, object>)) {
                return (FastBindResult<T>)(object)new Binding.MetaUserObject.FastSetBinderHelper<PythonDictionary>(
                    codeContext,
                    self,
                    value,
                    setBinder).MakeSet();
            }

            return new FastBindResult<T>();
        }
    }

    /// <summary>
    /// Provides a debug view for user defined types.  This class is declared as public
    /// because it is referred to from generated code.  You should not use this class.
    /// </summary>
    public class UserTypeDebugView {
        private readonly IPythonObject _userObject;
        
        public UserTypeDebugView(IPythonObject userObject) {
            _userObject = userObject;
        }

        public PythonType __class__ {
            get {
                return _userObject.PythonType;
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        internal List<ObjectDebugView> Members {
            get {
                var res = new List<ObjectDebugView>();
                if (_userObject.Dict != null) {
                    foreach (var v in _userObject.Dict) {
                        res.Add(new ObjectDebugView(v.Key, v.Value));
                    }
                }

                // collect any slots on the object
                object[] slots = _userObject.GetSlots();
                if (slots != null) {
                    var mro = _userObject.PythonType.ResolutionOrder;
                    List<string> slotNames = new List<string>();
                    
                    for(int i = mro.Count - 1; i>= 0; i--) {
                        slotNames.AddRange(mro[i].GetTypeSlots());
                    }

                    for (int i = 0; i < slots.Length - 1; i++) {
                        if (slots[i] != Uninitialized.Instance) {
                            res.Add(new ObjectDebugView(slotNames[i], slots[i]));
                        }
                    }
                }
                return res;
            }
        }
    }
}
