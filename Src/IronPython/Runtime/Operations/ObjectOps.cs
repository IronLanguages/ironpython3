// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Threading;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Types;

namespace IronPython.Runtime.Operations {

    /// <summary>
    /// Contains Python extension methods that are added to object
    /// </summary>
    public static class ObjectOps {
        /// <summary> Types for which the pickle module has built-in support (from PEP 307 case 2)  </summary>
        [MultiRuntimeAware]
        private static HashSet<PythonType>? _nativelyPickleableTypes;

        /// <summary>
        /// __class__, a custom slot so that it works for both objects and types.
        /// </summary>
        [SlotField]
        public static PythonTypeSlot __class__ = new PythonTypeTypeSlot();

        /// <summary>
        /// Removes an attribute from the provided member
        /// </summary>
        public static void __delattr__(CodeContext/*!*/ context, object self, string name) {
            if (self is PythonType) {
                throw PythonOps.TypeError("can't apply this __delattr__ to type object");
            }

            PythonOps.ObjectDeleteAttribute(context, self, name);
        }

        /// <summary>
        /// Returns the hash code of the given object
        /// </summary>
        public static int __hash__(object self) {
            if (self == null) return NoneTypeOps.NoneHashCode;
            return self.GetHashCode();
        }

        /// <summary>
        /// Gets the specified attribute from the object without running any custom lookup behavior
        /// (__getattr__ and __getattribute__)
        /// </summary>
        public static object __getattribute__(CodeContext/*!*/ context, object self, string name) {
            return PythonOps.ObjectGetAttribute(context, self, name);
        }

        /// <summary>
        /// Initializes the object.  The base class does nothing.
        /// </summary>
        public static void __init__(CodeContext/*!*/ context, object self) { }

        /// <summary>
        /// Initializes the object.  The base class does nothing.
        /// </summary>
        public static void __init__(CodeContext/*!*/ context, object self, [NotNone] params object[] args\u00F8) {
            InstanceOps.CheckInitArgs(context, null, args\u00F8, self);
        }

        /// <summary>
        /// Initializes the object.  The base class does nothing.
        /// </summary>
        public static void __init__(CodeContext/*!*/ context, object self, [ParamDictionary]IDictionary<object, object> kwargs, params object[] args\u00F8) {
            InstanceOps.CheckInitArgs(context, kwargs, args\u00F8, self);
        }

        /// <summary>
        /// Creates a new instance of the type
        /// </summary>
        [StaticExtensionMethod]
        public static object __new__(CodeContext/*!*/ context, PythonType cls) {
            if (cls == null) {
                throw PythonOps.TypeError("__new__ expected type object, got {0}", PythonOps.Repr(context, DynamicHelpers.GetPythonType(cls)));
            }

            return cls.CreateInstance(context);
        }

        /// <summary>
        /// Creates a new instance of the type
        /// </summary>
        [StaticExtensionMethod]
        public static object __new__(CodeContext/*!*/ context, PythonType cls, [NotNone] params object[] args\u00F8) {
            if (cls == null) {
                throw PythonOps.TypeError("__new__ expected type object, got {0}", PythonOps.Repr(context, DynamicHelpers.GetPythonType(cls)));
            }

            InstanceOps.CheckNewArgs(context, null, args\u00F8, cls);

            return cls.CreateInstance(context);
        }

        /// <summary>
        /// Creates a new instance of the type
        /// </summary>
        [StaticExtensionMethod]
        public static object __new__(CodeContext/*!*/ context, PythonType cls, [ParamDictionary]IDictionary<object, object> kwargs\u00F8, [NotNone] params object[] args\u00F8) {
            if (cls == null) {
                throw PythonOps.TypeError("__new__ expected type object, got {0}", PythonOps.Repr(context, DynamicHelpers.GetPythonType(cls)));
            }

            InstanceOps.CheckNewArgs(context, kwargs\u00F8, args\u00F8, cls);

            return cls.CreateInstance(context);
        }

        /// <summary>
        /// Runs the pickle protocol
        /// </summary>
        public static object? __reduce__(CodeContext/*!*/ context, object self) {
            return __reduce_ex__(context, self, 0);
        }

        /// <summary>
        /// Runs the pickle protocol
        /// </summary>
        public static object? __reduce_ex__(CodeContext/*!*/ context, object self) {
            return __reduce_ex__(context, self, 0);
        }

        /// <summary>
        /// Runs the pickle protocol
        /// </summary>
        public static object? __reduce_ex__(CodeContext/*!*/ context, object self, object protocol) {
            object objectReduce = PythonOps.GetBoundAttr(context, TypeCache.Object, "__reduce__");
            if (PythonOps.TryGetBoundAttr(context, DynamicHelpers.GetPythonType(self), "__reduce__", out object? myReduce)) {
                if (!PythonOps.IsRetBool(myReduce, objectReduce)) {
                    // A derived class overrode __reduce__ but not __reduce_ex__, so call
                    // specialized __reduce__ instead of generic __reduce_ex__.
                    // (see the "The __reduce_ex__ API" section of PEP 307)
                    return PythonOps.CallWithContext(context, myReduce, self)!;
                }
            }

            if (context.LanguageContext.ConvertToInt32(protocol) < 2) {
                return ReduceProtocol0(context, self);
            } else {
                return ReduceProtocol2(context, self);
            }
        }

        /// <summary>
        /// Returns the code representation of the object.  The default implementation returns
        /// a string which consists of the type and a unique numerical identifier.
        /// </summary>
        public static string __repr__(object self) {
            return $"<{PythonOps.GetPythonTypeName(self)} object at {PythonOps.HexId(self)}>";
        }

        /// <summary>
        /// Sets an attribute on the object without running any custom object defined behavior.
        /// </summary>
        public static void __setattr__(CodeContext/*!*/ context, object self, string name, object value) {
            if (self is PythonType) {
                throw PythonOps.TypeError("can't apply this __setattr__ to type object");
            }

            PythonOps.ObjectSetAttribute(context, self, name, value);
        }

        private static int AdjustPointerSize(int size) {
            if (IntPtr.Size == 4) {
                return size;
            }

            return size * 2;
        }

        /// <summary>
        /// Returns the number of bytes of memory required to allocate the object.
        /// </summary>
        public static int __sizeof__(object self) {
            int res = AdjustPointerSize(8); // vtable, sync blk
            if (self is IPythonObject) {
                res += AdjustPointerSize(12); // class, dict, slots 
            }

            Type t = DynamicHelpers.GetPythonType(self).FinalSystemType;
            res += GetTypeSize(t);

            return res;
        }

        private static int GetTypeSize(Type t) {
            FieldInfo[] fields = t.GetFields(System.Reflection.BindingFlags.FlattenHierarchy | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            int res = 0;
            foreach (FieldInfo fi in fields) {
                if (fi.FieldType.IsClass || fi.FieldType.IsInterface) {
                    res += AdjustPointerSize(4);
                } else if (fi.FieldType.IsPrimitive) {
                    return System.Runtime.InteropServices.Marshal.SizeOf(fi.FieldType);
                } else {
                    res += GetTypeSize(fi.FieldType);
                }
            }

            return res;
        }

        /// <summary>
        /// Returns a friendly string representation of the object. 
        /// </summary>
        public static string __str__(CodeContext/*!*/ context, object o) {
            try {
                PythonOps.FunctionPushFrame(context.LanguageContext);
                return PythonOps.Repr(context, o);
            } finally {
                PythonOps.FunctionPopFrame();
            }
        }

        public static NotImplementedType __subclasshook__(params object[] args) {
            return NotImplementedType.Value;
        }

        [return: MaybeNotImplemented]
        public static object __eq__(object self, object value) {
            if (ReferenceEquals(self, value)) return ScriptingRuntimeHelpers.True;
            return NotImplementedType.Value;
        }

        [return: MaybeNotImplemented]
        public static object __ne__(CodeContext context, object self, object other) {
            if (PythonTypeOps.TryInvokeBinaryOperator(context, self, other, "__eq__", out object res)) {
                if (res is NotImplementedType) {
                    return NotImplementedType.Value;
                }
                return !PythonOps.IsTrue(res);
            }
            return NotImplementedType.Value;
        }

        [return: MaybeNotImplemented]
        public static NotImplementedType __gt__(object self, object value) => NotImplementedType.Value;

        [return: MaybeNotImplemented]
        public static NotImplementedType __lt__(object self, object value) => NotImplementedType.Value;

        [return: MaybeNotImplemented]
        public static NotImplementedType __ge__(object self, object value) => NotImplementedType.Value;

        [return: MaybeNotImplemented]
        public static NotImplementedType __le__(object self, object value) => NotImplementedType.Value;

        public static string __format__(CodeContext/*!*/ context, object self, [NotNone] string/*!*/ formatSpec) {
            if (formatSpec != string.Empty)
                throw PythonOps.TypeError("unsupported format string passed to {0}.__format__", PythonOps.GetPythonTypeName(self));

            return PythonOps.ToString(context, self);
        }

        #region Pickle helpers

        // This is a dynamically-initialized property rather than a statically-initialized field
        // to avoid a bootstrapping dependency loop
        private static HashSet<PythonType> NativelyPickleableTypes {
            get {
                if (_nativelyPickleableTypes == null) {
                    var hashSet = new HashSet<PythonType> {
                        TypeCache.Null,
                        TypeCache.Boolean,
                        TypeCache.Int32,
                        TypeCache.BigInteger,
                        TypeCache.Double,
                        TypeCache.Complex,
                        TypeCache.String,
                        TypeCache.PythonTuple,
                        TypeCache.PythonList,
                        TypeCache.Dict,
                        TypeCache.Function
                    };

                    // type dict needs to be ensured to be fully initialized before assigning back
                    Thread.MemoryBarrier();
                    _nativelyPickleableTypes = hashSet;
                }
                return _nativelyPickleableTypes;
            }
        }

        /// <summary>
        /// Return a dict that maps slot names to slot values, but only include slots that have been assigned to.
        /// Looks up slots in base types as well as the current type.
        /// 
        /// Sort-of Python equivalent (doesn't look up base slots, while the real code does):
        ///   return dict([(slot, getattr(self, slot)) for slot in type(self).__slots__ if hasattr(self, slot)])
        /// 
        /// Return null if the object has no __slots__, or empty dict if it has __slots__ but none are initialized.
        /// </summary>
        private static PythonDictionary? GetInitializedSlotValues(object obj) {
            PythonDictionary initializedSlotValues = new PythonDictionary();
            IList<PythonType> mro = DynamicHelpers.GetPythonType(obj).ResolutionOrder;
            foreach (object type in mro) {
                if (PythonOps.TryGetBoundAttr(type, "__slots__", out object? slots)) {
                    List<string> slotNames = PythonType.SlotsToList(slots);
                    foreach (string slotName in slotNames) {
                        if (slotName == "__dict__") continue;
                        // don't reassign same-named slots from types earlier in the MRO
                        if (initializedSlotValues.__contains__(slotName)) continue;
                        if (PythonOps.TryGetBoundAttr(obj, slotName, out object? slotValue)) {
                            initializedSlotValues[slotName] = slotValue;
                        }
                    }
                }
            }
            if (initializedSlotValues.Count == 0) return null;
            return initializedSlotValues;
        }

        /// <summary>
        /// Implements the default __reduce_ex__ method as specified by PEP 307 case 2 (new-style instance, protocol 0 or 1)
        /// </summary>
        internal static object? ReduceProtocol0(CodeContext/*!*/ context, object self) {
            var copyreg = context.LanguageContext.GetCopyRegModule();
            var _reduce_ex = PythonOps.GetBoundAttr(context, copyreg, "_reduce_ex");
            return PythonOps.CallWithContext(context, _reduce_ex, self, 0);
        }

        /// <summary>
        /// Implements the default __reduce_ex__ method as specified by PEP 307 case 3 (new-style instance, protocol 2)
        /// </summary>
        private static PythonTuple ReduceProtocol2(CodeContext/*!*/ context, object self) {
            // builtin types which can't be pickled (due to tp_itemsize != 0)
            if (self is MemoryView) {
                throw PythonOps.TypeError("can't pickle memoryview objects");
            }

            PythonType myType = DynamicHelpers.GetPythonType(self);

            object? state;
            object?[] funcArgs;

            var copyreg = context.LanguageContext.GetCopyRegModule();
            var func = PythonOps.GetBoundAttr(context, copyreg, "__newobj__");

            if (PythonOps.TryGetBoundAttr(context, myType, "__getnewargs__", out object? getNewArgsCallable)) {
                // TypeError will bubble up if __getnewargs__ isn't callable
                if (!(PythonOps.CallWithContext(context, getNewArgsCallable, self) is PythonTuple newArgs)) {
                    throw PythonOps.TypeError("__getnewargs__ should return a tuple");
                }
                funcArgs = new object[1 + newArgs.Count];
                funcArgs[0] = myType;
                for (int i = 0; i < newArgs.Count; i++) funcArgs[i + 1] = newArgs[i];
            } else {
                funcArgs = new object[] { myType };
            }

            if (!PythonTypeOps.TryInvokeUnaryOperator(context,
                    self,
                    "__getstate__",
                    out state)) {
                object? dict;
                if (self is IPythonObject ipo) {
                    dict = ipo.Dict;
                } else if (!PythonOps.TryGetBoundAttr(context, self, "__dict__", out dict)) {
                    dict = null;
                }

                PythonDictionary? initializedSlotValues = GetInitializedSlotValues(self);
                if (initializedSlotValues != null && initializedSlotValues.Count == 0) {
                    initializedSlotValues = null;
                }

                if (dict == null && initializedSlotValues == null) state = null;
                else if (dict != null && initializedSlotValues == null) state = dict;
                else if (dict != null && initializedSlotValues != null) state = PythonTuple.MakeTuple(dict, initializedSlotValues);
                else   /*dict == null && initializedSlotValues != null*/ state = PythonTuple.MakeTuple(null, initializedSlotValues);
            }

            object? listIterator = null;
            if (self is PythonList) {
                listIterator = PythonOps.GetEnumerator(self);
            }

            object? dictIterator = null;
            if (self is PythonDictionary) {
                dictIterator = Modules.Builtin.iter(context, PythonOps.Invoke(context, self, nameof(PythonDictionary.items)));
            }

            if (state is PythonDictionary d && d.Count == 0) state = null;

            return PythonTuple.MakeTuple(func, PythonTuple.MakeTuple(funcArgs), state, listIterator, dictIterator);
        }

        #endregion
    }
}
