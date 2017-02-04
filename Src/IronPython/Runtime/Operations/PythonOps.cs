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
using Microsoft.Scripting.Ast;
#else
using Microsoft.Scripting.Ast;
using Microsoft.Scripting.Math;
using Complex = Microsoft.Scripting.Math.Complex64;
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Hosting.Providers;
using Microsoft.Scripting.Hosting.Shell;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Compiler;
using IronPython.Hosting;
using IronPython.Modules;
using IronPython.Runtime.Binding;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Types;

namespace IronPython.Runtime.Operations {

    /// <summary>
    /// Contains functions that are called directly from
    /// generated code to perform low-level runtime functionality.
    /// </summary>
    public static partial class PythonOps {
        #region Shared static data

        [ThreadStatic]
        private static List<object> InfiniteRepr;

        // The "current" exception on this thread that will be returned via sys.exc_info()
        [ThreadStatic]
        internal static Exception RawException;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly PythonTuple EmptyTuple = PythonTuple.EMPTY;
        private static readonly Type[] _DelegateCtorSignature = new Type[] { typeof(object), typeof(IntPtr) };

        #endregion

        public static BigInteger MakeIntegerFromHex(string s) {
            return LiteralParser.ParseBigInteger(s, 16);
        }

        public static PythonDictionary MakeDict(int size) {
            return new PythonDictionary(size);
        }

        public static PythonDictionary MakeEmptyDict() {
            return new PythonDictionary(EmptyDictionaryStorage.Instance);
        }

        /// <summary>
        /// Creates a new dictionary extracting the keys and values from the
        /// provided data array.  Keys/values are adjacent in the array with
        /// the value coming first.
        /// </summary>
        public static PythonDictionary MakeDictFromItems(params object[] data) {
            return new PythonDictionary(new CommonDictionaryStorage(data, false));
        }

        public static PythonDictionary MakeConstantDict(object items) {
            return new PythonDictionary((ConstantDictionaryStorage)items);
        }

        public static object MakeConstantDictStorage(params object[] data) {
            return new ConstantDictionaryStorage(new CommonDictionaryStorage(data, false));
        }

        public static SetCollection MakeSet(params object[] items) {
            return new SetCollection(items);
        }

        public static SetCollection MakeEmptySet() {
            return new SetCollection();
        }

        /// <summary>
        /// Creates a new dictionary extracting the keys and values from the
        /// provided data array.  Keys/values are adjacent in the array with
        /// the value coming first.
        /// </summary>
        public static PythonDictionary MakeHomogeneousDictFromItems(object[] data) {
            return new PythonDictionary(new CommonDictionaryStorage(data, true));
        }

        public static bool IsCallable(CodeContext/*!*/ context, object o) {
            // This tells if an object can be called, but does not make a claim about the parameter list.
            // In 1.x, we could check for certain interfaces like ICallable*, but those interfaces were deprecated
            // in favor of dynamic sites. 
            // This is difficult to infer because we'd need to simulate the entire callbinder, which can include
            // looking for [SpecialName] call methods and checking for a rule from IDynamicMetaObjectProvider. But even that wouldn't
            // be complete since sites require the argument list of the call, and we only have the instance here. 
            // Thus check a dedicated IsCallable operator. This lets each object describe if it's callable.


            // Invoke Operator.IsCallable on the object. 
            return PythonContext.GetContext(context).IsCallable(o);
        }

        public static bool UserObjectIsCallable(CodeContext/*!*/ context, object o) {
            object callFunc;
            return TryGetBoundAttr(context, o, "__call__", out callFunc) && callFunc != null;
        }

        public static bool IsTrue(object o) {
            return Converter.ConvertToBoolean(o);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1002:DoNotExposeGenericLists")]
        public static List<object> GetReprInfinite() {
            if (InfiniteRepr == null) {
                InfiniteRepr = new List<object>();
            }
            return InfiniteRepr;
        }

        [LightThrowing]
        internal static object LookupEncodingError(CodeContext/*!*/ context, string name) {
            Dictionary<string, object> errorHandlers = PythonContext.GetContext(context).ErrorHandlers;
            lock (errorHandlers) {
                if (errorHandlers.ContainsKey(name))
                    return errorHandlers[name];
                else
                    return LightExceptions.Throw(PythonOps.LookupError("unknown error handler name '{0}'", name));
            }
        }

        internal static void RegisterEncodingError(CodeContext/*!*/ context, string name, object handler) {
            Dictionary<string, object> errorHandlers = PythonContext.GetContext(context).ErrorHandlers;

            lock (errorHandlers) {
                if (!PythonOps.IsCallable(context, handler))
                    throw PythonOps.TypeError("handler must be callable");

                errorHandlers[name] = handler;
            }
        }

        internal static PythonTuple LookupEncoding(CodeContext/*!*/ context, string encoding) {
            PythonContext.GetContext(context).EnsureEncodings();

            List<object> searchFunctions = PythonContext.GetContext(context).SearchFunctions;
            string normalized = encoding.ToLower().Replace(' ', '-');
            if (normalized.IndexOf(Char.MinValue) != -1) {
                throw PythonOps.TypeError("lookup string cannot contain null character");
            }
            lock (searchFunctions) {
                for (int i = 0; i < searchFunctions.Count; i++) {
                    object res = PythonCalls.Call(context, searchFunctions[i], normalized);
                    if (res != null) return (PythonTuple)res;
                }
            }

            throw PythonOps.LookupError("unknown encoding: {0}", encoding);
        }

        internal static void RegisterEncoding(CodeContext/*!*/ context, object search_function) {
            if (!PythonOps.IsCallable(context, search_function))
                throw PythonOps.TypeError("search_function must be callable");

            List<object> searchFunctions = PythonContext.GetContext(context).SearchFunctions;

            lock (searchFunctions) {
                searchFunctions.Add(search_function);
            }
        }

        internal static string GetPythonTypeName(object obj) {
            return PythonTypeOps.GetName(obj);
        }

        public static string Repr(CodeContext/*!*/ context, object o) {
            if (o == null) return "None";

            string s;
            if ((s = o as string) != null) return StringOps.__repr__(s);
            if (o is int) return Int32Ops.__repr__((int)o);
            if (o is long) return ((long)o).ToString();

            // could be a container object, we need to detect recursion, but only
            // for our own built-in types that we're aware of.  The user can setup
            // infinite recursion in their own class if they want.
            ICodeFormattable f = o as ICodeFormattable;
            if (f != null) {
                return f.__repr__(context);
            }

            PerfTrack.NoteEvent(PerfTrack.Categories.Temporary, "Repr " + o.GetType().FullName);

            return PythonContext.InvokeUnaryOperator(context, UnaryOperators.Repr, o) as string;
        }

        public static List<object> GetAndCheckInfinite(object o) {
            List<object> infinite = GetReprInfinite();
            foreach (object o2 in infinite) {
                if (o == o2) {
                    return null;
                }
            }
            return infinite;
        }

        public static string ToString(object o) {
            return ToString(DefaultContext.Default, o);
        }

        public static string ToString(CodeContext/*!*/ context, object o) {
            string x = o as string;
            PythonType dt;
            if (x != null) return x;
            if (o == null) return "None";
            if (o is double) return DoubleOps.__str__(context, (double)o);
            if ((dt = o as PythonType) != null) return dt.__repr__(DefaultContext.Default);
            if (o.GetType() == typeof(object).Assembly.GetType("System.__ComObject")) return ComOps.__repr__(o);

            object value = PythonContext.InvokeUnaryOperator(context, UnaryOperators.String, o);
            string ret = value as string;
            if (ret == null) {
                Extensible<string> es = value as Extensible<string>;
                if (es == null) {
                    throw PythonOps.TypeError("expected str, got {0} from __str__", PythonTypeOps.GetName(value));
                }

                ret = es.Value;
            }
            
            return ret;
        }

        public static string FormatString(CodeContext/*!*/ context, string str, object data) {
            return new StringFormatter(context, str, data).Format();
        }

        public static string FormatUnicode(CodeContext/*!*/ context, string str, object data) {
            return new StringFormatter(context, str, data, true).Format();
        }

        public static object Plus(object o) {
            object ret;

            if (o is int) return o;
            else if (o is double) return o;
            else if (o is BigInteger) return o;
            else if (o is Complex) return o;
            else if (o is long) return o;
            else if (o is float) return o;
            else if (o is bool) return ScriptingRuntimeHelpers.Int32ToObject((bool)o ? 1 : 0);

            if (PythonTypeOps.TryInvokeUnaryOperator(DefaultContext.Default, o, "__pos__", out ret) &&
                ret != NotImplementedType.Value) {
                return ret;
            }

            throw PythonOps.TypeError("bad operand type for unary +");
        }

        public static object Negate(object o) {
            if (o is int) return Int32Ops.Negate((int)o);
            else if (o is double) return DoubleOps.Negate((double)o);
            else if (o is long) return Int64Ops.Negate((long)o);
            else if (o is BigInteger) return BigIntegerOps.Negate((BigInteger)o);
            else if (o is Complex) return -(Complex)o;
            else if (o is float) return DoubleOps.Negate((float)o);
            else if (o is bool) return ScriptingRuntimeHelpers.Int32ToObject((bool)o ? -1 : 0);

            object ret;
            if (PythonTypeOps.TryInvokeUnaryOperator(DefaultContext.Default, o, "__neg__", out ret) &&
                ret != NotImplementedType.Value) {
                return ret;
            }

            throw PythonOps.TypeError("bad operand type for unary -");
        }

        public static bool IsSubClass(PythonType/*!*/ c, PythonType/*!*/ typeinfo) {
            Assert.NotNull(c, typeinfo);

            return typeinfo.__subclasscheck__(c);
        }

        public static bool IsSubClass(CodeContext/*!*/ context, PythonType c, object typeinfo) {
            if (c == null) throw PythonOps.TypeError("issubclass: arg 1 must be a class");
            if (typeinfo == null) throw PythonOps.TypeError("issubclass: arg 2 must be a class");

            PythonTuple pt = typeinfo as PythonTuple;
            PythonContext pyContext = PythonContext.GetContext(context);
            if (pt != null) {
                // Recursively inspect nested tuple(s)
                foreach (object o in pt) {
                    try {
                        FunctionPushFrame(pyContext);
                        if (IsSubClass(context, c, o)) {
                            return true;
                        }
                    } finally {
                        FunctionPopFrame();
                    }
                }
                return false;
            }

            Type t = typeinfo as Type;
            if (t != null) {
                typeinfo = DynamicHelpers.GetPythonTypeFromType(t);
            }

            object bases;
            PythonType dt = typeinfo as PythonType;
            if (dt == null) {
                if (!PythonOps.TryGetBoundAttr(typeinfo, "__bases__", out bases)) {
                    //!!! deal with classes w/ just __bases__ defined.
                    throw PythonOps.TypeErrorForBadInstance("issubclass(): {0} is not a class nor a tuple of classes", typeinfo);
                }

                IEnumerator ie = PythonOps.GetEnumerator(bases);
                while (ie.MoveNext()) {
                    PythonType baseType = ie.Current as PythonType;
                    if (baseType == null) continue;
                    if (c.IsSubclassOf(baseType)) return true;
                }
                return false;
            }

            return IsSubClass(c, dt);
        }

        public static bool IsInstance(object o, PythonType typeinfo) {
            if (typeinfo.__instancecheck__(o)) {
                return true;
            }

            return IsInstanceDynamic(o, typeinfo, DynamicHelpers.GetPythonType(o));
        }

        public static bool IsInstance(CodeContext/*!*/ context, object o, PythonTuple typeinfo) {
            PythonContext pyContext = PythonContext.GetContext(context);
            foreach (object type in typeinfo) {
                try {
                    PythonOps.FunctionPushFrame(pyContext);
                    if (type is PythonType) {
                        if (IsInstance(o, (PythonType)type)) {
                            return true;
                        }
                    } else if (type is PythonTuple) {
                        if (IsInstance(context, o, (PythonTuple)type)) {
                            return true;
                        }
                    } else if (IsInstance(context, o, type)) {
                        return true;
                    }
                } finally {
                    PythonOps.FunctionPopFrame();
                }
            }
            return false;
        }

        public static bool IsInstance(CodeContext/*!*/ context, object o, object typeinfo) {
            if (typeinfo == null) throw PythonOps.TypeError("isinstance: arg 2 must be a class, type, or tuple of classes and types");

            PythonTuple tt = typeinfo as PythonTuple;
            if (tt != null) {
                return IsInstance(context, o, tt);
            }

            PythonType odt = DynamicHelpers.GetPythonType(o);
            if (IsSubClass(context, odt, typeinfo)) {
                return true;
            }

            return IsInstanceDynamic(o, typeinfo);
        }

        private static bool IsInstanceDynamic(object o, object typeinfo) {
            return IsInstanceDynamic(o, typeinfo, DynamicHelpers.GetPythonType(o));
        }

        private static bool IsInstanceDynamic(object o, object typeinfo, PythonType odt) {
            if (o is IPythonObject) {
                object cls;
                if (PythonOps.TryGetBoundAttr(o, "__class__", out cls) &&
                    (!object.ReferenceEquals(odt, cls))) {
                    return IsSubclassSlow(cls, typeinfo);
                }
            }
            return false;
        }

        private static bool IsSubclassSlow(object cls, object typeinfo) {
            Debug.Assert(typeinfo != null);
            if (cls == null) return false;

            // Same type
            if (cls.Equals(typeinfo)) {
                return true;
            }

            // Get bases
            object bases;
            if (!PythonOps.TryGetBoundAttr(cls, "__bases__", out bases)) {
                return false;   // no bases, cannot be subclass
            }
            PythonTuple tbases = bases as PythonTuple;
            if (tbases == null) {
                return false;   // not a tuple, cannot be subclass
            }

            foreach (object baseclass in tbases) {
                if (IsSubclassSlow(baseclass, typeinfo)) return true;
            }

            return false;
        }

        public static object OnesComplement(object o) {
            if (o is int) return ~(int)o;
            if (o is long) return ~(long)o;
            if (o is BigInteger) return ~((BigInteger)o);
            if (o is bool) return ScriptingRuntimeHelpers.Int32ToObject((bool)o ? -2 : -1);

            object ret;
            if (PythonTypeOps.TryInvokeUnaryOperator(DefaultContext.Default, o, "__invert__", out ret) &&
                ret != NotImplementedType.Value)
                return ret;


            throw PythonOps.TypeError("bad operand type for unary ~");
        }

        public static bool Not(object o) {
            return !IsTrue(o);
        }

        public static object Is(object x, object y) {
            return x == y ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
        }

        public static bool IsRetBool(object x, object y) {
            return x == y;
        }

        public static object IsNot(object x, object y) {
            return x != y ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
        }

        public static bool IsNotRetBool(object x, object y) {
            return x != y;
        }

        internal delegate T MultiplySequenceWorker<T>(T self, int count);

        /// <summary>
        /// Wraps up all the semantics of multiplying sequences so that all of our sequences
        /// don't duplicate the same logic.  When multiplying sequences we need to deal with
        /// only multiplying by valid sequence types (ints, not floats), support coercion
        /// to integers if the type supports it, not multiplying by None, and getting the
        /// right semantics for multiplying by negative numbers and 1 (w/ and w/o subclasses).
        /// 
        /// This function assumes that it is only called for case where count is not implicitly
        /// coercible to int so that check is skipped.
        /// </summary>
        internal static object MultiplySequence<T>(MultiplySequenceWorker<T> multiplier, T sequence, Index count, bool isForward) {
            if (isForward && count != null) {
                object ret;
                if (PythonTypeOps.TryInvokeBinaryOperator(DefaultContext.Default, count.Value, sequence, "__rmul__", out ret)) {
                    if (ret != NotImplementedType.Value) return ret;
                }
            }

            int icount = GetSequenceMultiplier(sequence, count.Value);

            if (icount < 0) icount = 0;
            return multiplier(sequence, icount);
        }

        internal static int GetSequenceMultiplier(object sequence, object count) {
            int icount;
            if (!Converter.TryConvertToIndex(count, out icount)) {
                throw TypeError("can't multiply sequence by non-int of type '{0}'", PythonTypeOps.GetName(count));
            }
            return icount;
        }

        public static object Equal(CodeContext/*!*/ context, object x, object y) {
            PythonContext pc = PythonContext.GetContext(context);
            return pc.EqualSite.Target(pc.EqualSite, x, y);
        }

        public static bool EqualRetBool(object x, object y) {
            //TODO just can't seem to shake these fast paths
            if (x is int && y is int) { return ((int)x) == ((int)y); }
            if (x is string && y is string) { return ((string)x).Equals((string)y); }
            
            return DynamicHelpers.GetPythonType(x).EqualRetBool(x, y);
        }

        public static bool EqualRetBool(CodeContext/*!*/ context, object x, object y) {
            // TODO: use context

            //TODO just can't seem to shake these fast paths
            if (x is int && y is int) { return ((int)x) == ((int)y); }
            if (x is string && y is string) { return ((string)x).Equals((string)y); }

            return DynamicHelpers.GetPythonType(x).EqualRetBool(x, y);
        }

        public static int Compare(object x, object y) {
            return Compare(DefaultContext.Default, x, y);
        }

        public static int Compare(CodeContext/*!*/ context, object x, object y) {
            if (x == y) return 0;

            return DynamicHelpers.GetPythonType(x).Compare(x, y);
        }

        public static object CompareEqual(int res) {
            return res == 0 ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
        }

        public static object CompareNotEqual(int res) {
            return res == 0 ? ScriptingRuntimeHelpers.False : ScriptingRuntimeHelpers.True;
        }

        public static object CompareGreaterThan(int res) {
            return res > 0 ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
        }

        public static object CompareGreaterThanOrEqual(int res) {
            return res >= 0 ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
        }

        public static object CompareLessThan(int res) {
            return res < 0 ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
        }

        public static object CompareLessThanOrEqual(int res) {
            return res <= 0 ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
        }

        public static bool CompareTypesEqual(CodeContext/*!*/ context, object x, object y) {
            if (x == null && y == null) return true;
            if (x == null) return false;
            if (y == null) return false;

            if (DynamicHelpers.GetPythonType(x) == DynamicHelpers.GetPythonType(y)) {
                // avoid going to the ID dispenser if we have the same types...
                return x == y;
            }

            return PythonOps.CompareTypesWorker(context, false, x, y) == 0;
        }

        public static bool CompareTypesNotEqual(CodeContext/*!*/ context, object x, object y) {
            return PythonOps.CompareTypesWorker(context, false, x, y) != 0;
        }

        public static bool CompareTypesGreaterThan(CodeContext/*!*/ context, object x, object y) {
            return PythonOps.CompareTypes(context, x, y) > 0;
        }

        public static bool CompareTypesLessThan(CodeContext/*!*/ context, object x, object y) {
            return PythonOps.CompareTypes(context, x, y) < 0;
        }

        public static bool CompareTypesGreaterThanOrEqual(CodeContext/*!*/ context, object x, object y) {
            return PythonOps.CompareTypes(context, x, y) >= 0;
        }

        public static bool CompareTypesLessThanOrEqual(CodeContext/*!*/ context, object x, object y) {
            return PythonOps.CompareTypes(context, x, y) <= 0;
        }

        public static int CompareTypesWorker(CodeContext/*!*/ context, bool shouldWarn, object x, object y) {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            string name1, name2;
            int diff;

            if (DynamicHelpers.GetPythonType(x) != DynamicHelpers.GetPythonType(y)) {
                if (shouldWarn && PythonContext.GetContext(context).PythonOptions.WarnPython30) {
                    PythonOps.Warn(context, PythonExceptions.DeprecationWarning, "comparing unequal types not supported in 3.x");
                }

                name1 = PythonTypeOps.GetName(x);
                name2 = PythonTypeOps.GetName(y);
                diff = String.CompareOrdinal(name1, name2);
                if (diff == 0) {
                    // if the types are different but have the same name compare based upon their types. 
                    diff = (int)(IdDispenser.GetId(DynamicHelpers.GetPythonType(x)) - IdDispenser.GetId(DynamicHelpers.GetPythonType(y)));
                }
            } else {
                diff = (int)(IdDispenser.GetId(x) - IdDispenser.GetId(y));
            }

            if (diff < 0) return -1;
            if (diff == 0) return 0;
            return 1;
        }

        public static int CompareTypes(CodeContext/*!*/ context, object x, object y) {
            return CompareTypesWorker(context, true, x, y);
        }

        public static object GreaterThanHelper(CodeContext/*!*/ context, object self, object other) {
            return InternalCompare(context, PythonOperationKind.GreaterThan, self, other);
        }

        public static object LessThanHelper(CodeContext/*!*/ context, object self, object other) {
            return InternalCompare(context, PythonOperationKind.LessThan, self, other);
        }

        public static object GreaterThanOrEqualHelper(CodeContext/*!*/ context, object self, object other) {
            return InternalCompare(context, PythonOperationKind.GreaterThanOrEqual, self, other);
        }

        public static object LessThanOrEqualHelper(CodeContext/*!*/ context, object self, object other) {
            return InternalCompare(context, PythonOperationKind.LessThanOrEqual, self, other);
        }

        internal static object InternalCompare(CodeContext/*!*/ context, PythonOperationKind op, object self, object other) {
            object ret;
            if (PythonTypeOps.TryInvokeBinaryOperator(context, self, other, Symbols.OperatorToSymbol(op), out ret))
                return ret;

            return NotImplementedType.Value;
        }

        public static int CompareToZero(object value) {
            double val;
            if (Converter.TryConvertToDouble(value, out val)) {
                if (val > 0) return 1;
                if (val < 0) return -1;
                return 0;
            }
            throw PythonOps.TypeErrorForBadInstance("an integer is required (got {0})", value);
        }

        public static int CompareArrays(object[] data0, int size0, object[] data1, int size1) {
            int size = Math.Min(size0, size1);
            for (int i = 0; i < size; i++) {
                int c = PythonOps.Compare(data0[i], data1[i]);
                if (c != 0) return c;
            }
            if (size0 == size1) return 0;
            return size0 > size1 ? +1 : -1;
        }

        public static int CompareArrays(object[] data0, int size0, object[] data1, int size1, IComparer comparer) {
            int size = Math.Min(size0, size1);
            for (int i = 0; i < size; i++) {
                int c = comparer.Compare(data0[i], data1[i]);
                if (c != 0) return c;
            }
            if (size0 == size1) return 0;
            return size0 > size1 ? +1 : -1;
        }

        public static bool ArraysEqual(object[] data0, int size0, object[] data1, int size1) {
            if (size0 != size1) {
                return false;
            }
            for (int i = 0; i < size0; i++) {
                if (data0[i] != null) {
                    if (!EqualRetBool(data0[i], data1[i])) {
                        return false;
                    }
                } else if (data1[i] != null) {
                    return false;
                }
            }
            return true;
        }

        public static bool ArraysEqual(object[] data0, int size0, object[] data1, int size1, IEqualityComparer comparer) {
            if (size0 != size1) {
                return false;
            }
            for (int i = 0; i < size0; i++) {
                if (data0[i] != null) {
                    if (!comparer.Equals(data0[i], data1[i])) {
                        return false;
                    }
                } else if (data1[i] != null) {
                    return false;
                }
            }
            return true;
        }

        public static object PowerMod(CodeContext/*!*/ context, object x, object y, object z) {
            object ret;
            if (z == null) {
                return PythonContext.GetContext(context).Operation(PythonOperationKind.Power, x, y);
            }
            if (x is int && y is int && z is int) {
                ret = Int32Ops.Power((int)x, (int)y, (int)z);
                if (ret != NotImplementedType.Value) return ret;
            } else if (x is BigInteger) {
                ret = BigIntegerOps.Power((BigInteger)x, y, z);
                if (ret != NotImplementedType.Value) return ret;
            }

            if (x is Complex || y is Complex || z is Complex) {
                throw PythonOps.ValueError("complex modulo");
            }

            if (PythonTypeOps.TryInvokeTernaryOperator(context, x, y, z, "__pow__", out ret)) {
                if (ret != NotImplementedType.Value) {
                    return ret;
                } else if (!IsNumericObject(y) || !IsNumericObject(z)) {
                    // special error message in this case...
                    throw TypeError("pow() 3rd argument not allowed unless all arguments are integers");
                }
            }

            throw PythonOps.TypeErrorForBinaryOp("power with modulus", x, y);
        }

        public static long Id(object o) {
            return IdDispenser.GetId(o);
        }

        public static string HexId(object o) {
            return string.Format("0x{0:X16}", Id(o));
        }

        // For hash operators, it's essential that:
        //  Cmp(x,y)==0 implies hash(x) == hash(y)
        //
        // Equality is a language semantic determined by the Python's numerical Compare() ops 
        // in IronPython.Runtime.Operations namespaces.
        // For example, the CLR compares float(1.0) and int32(1) as different, but Python
        // compares them as equal. So Hash(1.0f) and Hash(1) must be equal.
        //
        // Python allows an equality relationship between int, double, BigInteger, and complex.
        // So each of these hash functions must be aware of their possible equality relationships
        // and hash appropriately.
        //
        // Types which differ in hashing from .NET have __hash__ functions defined in their
        // ops classes which do the appropriate hashing.        
        public static int Hash(CodeContext/*!*/ context, object o) {
            return PythonContext.Hash(o);
        }

        public static object Index(object o) {
            if (o is int) {
                return Int32Ops.__index__((int)o);
            } else if (o is uint) {
                return UInt32Ops.__index__((uint)o);
            } else if (o is ushort) {
                return UInt16Ops.__index__((ushort)o);
            } else if (o is short) {
                return Int16Ops.__index__((short)o);
            } else if (o is byte) {
                return ByteOps.__index__((byte)o);
            } else if (o is sbyte) {
                return SByteOps.__index__((sbyte)o);
            } else if (o is long) {
                return Int64Ops.__index__((long)o);
            } else if(o is ulong) {
                return UInt64Ops.__index__((ulong)o);
            } else if (o is BigInteger) {
                return BigIntegerOps.__index__((BigInteger)o);
            }

            object index;

            if (PythonTypeOps.TryInvokeUnaryOperator(DefaultContext.Default,
                o,
                "__index__",
                out index)) {
                if (!(index is int) && !(index is BigInteger))
                    throw PythonOps.TypeError("__index__ returned non-int (type {0})", PythonTypeOps.GetName(index));

                return index;
            }
            throw TypeError("'{0}' object cannot be interpreted as an integer", PythonTypeOps.GetName(o));
        }

        public static int Length(object o) {
            string s = o as string;
            if (s != null) {
                return s.Length;
            }

            object[] os = o as object[];
            if (os != null) {
                return os.Length;
            }

            object len = PythonContext.InvokeUnaryOperator(DefaultContext.Default, UnaryOperators.Length, o, "len() of unsized object");

            int res;
            if (len is int) {
                res = (int)len;
            } else {
                res = Converter.ConvertToInt32(len);
            }

            if (res < 0) {
                throw PythonOps.ValueError("__len__ should return >= 0, got {0}", res);
            }
            return res;
        }

        public static object CallWithContext(CodeContext/*!*/ context, object func, params object[] args) {
            return PythonCalls.Call(context, func, args);
        }

        /// <summary>
        /// Supports calling of functions that require an explicit 'this'
        /// Currently, we check if the function object implements the interface 
        /// that supports calling with 'this'. If not, the 'this' object is dropped
        /// and a normal call is made.
        /// </summary>
        public static object CallWithContextAndThis(CodeContext/*!*/ context, object func, object instance, params object[] args) {
            // drop the 'this' and make the call
            return CallWithContext(context, func, args);
        }

        public static object ToPythonType(PythonType dt) {
            return ((object)dt);
        }

        public static object CallWithArgsTupleAndContext(CodeContext/*!*/ context, object func, object[] args, object argsTuple) {
            PythonTuple tp = argsTuple as PythonTuple;
            if (tp != null) {
                object[] nargs = new object[args.Length + tp.__len__()];
                for (int i = 0; i < args.Length; i++) nargs[i] = args[i];
                for (int i = 0; i < tp.__len__(); i++) nargs[i + args.Length] = tp[i];
                return CallWithContext(context, func, nargs);
            }

            List allArgs = PythonOps.MakeEmptyList(args.Length + 10);
            allArgs.AddRange(args);
            IEnumerator e = PythonOps.GetEnumerator(argsTuple);
            while (e.MoveNext()) allArgs.AddNoLock(e.Current);

            return CallWithContext(context, func, allArgs.GetObjectArray());
        }

        [Obsolete("Use ObjectOpertaions instead")]
        public static object CallWithArgsTupleAndKeywordDictAndContext(CodeContext/*!*/ context, object func, object[] args, string[] names, object argsTuple, object kwDict) {
            IDictionary kws = kwDict as IDictionary;
            if (kws == null && kwDict != null) throw PythonOps.TypeError("argument after ** must be a dictionary");

            if ((kws == null || kws.Count == 0) && names.Length == 0) {
                List<object> largs = new List<object>(args);
                if (argsTuple != null) {
                    foreach (object arg in PythonOps.GetCollection(argsTuple))
                        largs.Add(arg);
                }
                return CallWithContext(context, func, largs.ToArray());
            } else {
                List<object> largs;

                if (argsTuple != null && args.Length == names.Length) {
                    PythonTuple tuple = argsTuple as PythonTuple;
                    if (tuple == null) tuple = new PythonTuple(argsTuple);

                    largs = new List<object>(tuple);
                    largs.AddRange(args);
                } else {
                    largs = new List<object>(args);
                    if (argsTuple != null) {
                        largs.InsertRange(args.Length - names.Length, PythonTuple.Make(argsTuple));
                    }
                }

                List<string> lnames = new List<string>(names);

                if (kws != null) {
                    IDictionaryEnumerator ide = kws.GetEnumerator();
                    while (ide.MoveNext()) {
                        lnames.Add((string)ide.Key);
                        largs.Add(ide.Value);
                    }
                }

                return PythonCalls.CallWithKeywordArgs(context, func, largs.ToArray(), lnames.ToArray());
            }
        }

        public static object CallWithKeywordArgs(CodeContext/*!*/ context, object func, object[] args, string[] names) {
            return PythonCalls.CallWithKeywordArgs(context, func, args, names);
        }

        public static object CallWithArgsTuple(object func, object[] args, object argsTuple) {
            PythonTuple tp = argsTuple as PythonTuple;
            if (tp != null) {
                object[] nargs = new object[args.Length + tp.__len__()];
                for (int i = 0; i < args.Length; i++) nargs[i] = args[i];
                for (int i = 0; i < tp.__len__(); i++) nargs[i + args.Length] = tp[i];
                return PythonCalls.Call(func, nargs);
            }

            List allArgs = PythonOps.MakeEmptyList(args.Length + 10);
            allArgs.AddRange(args);
            IEnumerator e = PythonOps.GetEnumerator(argsTuple);
            while (e.MoveNext()) allArgs.AddNoLock(e.Current);

            return PythonCalls.Call(func, allArgs.GetObjectArray());
        }

        public static object GetIndex(CodeContext/*!*/ context, object o, object index) {
            PythonContext pc = PythonContext.GetContext(context);
            return pc.GetIndexSite.Target(pc.GetIndexSite, o, index);
        }

        public static bool TryGetBoundAttr(object o, string name, out object ret) {
            return TryGetBoundAttr(DefaultContext.Default, o, name, out ret);
        }

        public static void SetAttr(CodeContext/*!*/ context, object o, string name, object value) {
            PythonContext.GetContext(context).SetAttr(context, o, name, value);
        }

        public static bool TryGetBoundAttr(CodeContext/*!*/ context, object o, string name, out object ret) {
            return DynamicHelpers.GetPythonType(o).TryGetBoundAttr(context, o, name, out ret);
        }

        public static void DeleteAttr(CodeContext/*!*/ context, object o, string name) {
            PythonContext.GetContext(context).DeleteAttr(context, o, name);
        }

        public static bool HasAttr(CodeContext/*!*/ context, object o, string name) {
            object dummy;
            return TryGetBoundAttr(context, o, name, out dummy);
        }

        public static object GetBoundAttr(CodeContext/*!*/ context, object o, string name) {
            object ret;
            if (!DynamicHelpers.GetPythonType(o).TryGetBoundAttr(context, o, name, out ret)) {
                throw PythonOps.AttributeError("'{0}' object has no attribute '{1}'", PythonTypeOps.GetName(o), name);
            }

            return ret;
        }

        public static void ObjectSetAttribute(CodeContext/*!*/ context, object o, string name, object value) {
            if (!DynamicHelpers.GetPythonType(o).TrySetNonCustomMember(context, o, name, value))
                throw AttributeErrorForMissingOrReadonly(context, DynamicHelpers.GetPythonType(o), name);
        }

        public static void ObjectDeleteAttribute(CodeContext/*!*/ context, object o, string name) {
            if (!DynamicHelpers.GetPythonType(o).TryDeleteNonCustomMember(context, o, name)) {
                throw AttributeErrorForMissingOrReadonly(context, DynamicHelpers.GetPythonType(o), name);
            }
        }

        public static object ObjectGetAttribute(CodeContext/*!*/ context, object o, string name) {
            object value;
            if (DynamicHelpers.GetPythonType(o).TryGetNonCustomMember(context, o, name, out value)) {
                return value;
            }

            throw PythonOps.AttributeErrorForObjectMissingAttribute(o, name);
        }

        internal static IList<string> GetStringMemberList(IPythonMembersList pyMemList) {
            List<string> res = new List<string>();
            foreach (object o in pyMemList.GetMemberNames(DefaultContext.Default)) {
                if (o is string) {
                    res.Add((string)o);
                }
            }
            return res;
        }

        public static IList<object> GetAttrNames(CodeContext/*!*/ context, object o) {
            IPythonMembersList pyMemList = o as IPythonMembersList;
            if (pyMemList != null) {
                return pyMemList.GetMemberNames(context);
            }

            IMembersList memList = o as IMembersList;
            if (memList != null) {
                return new List(memList.GetMemberNames());
            }

            IPythonObject po = o as IPythonObject;
            if (po != null) {
                return po.PythonType.GetMemberNames(context, o);
            }

            List res = DynamicHelpers.GetPythonType(o).GetMemberNames(context, o);

#if FEATURE_COM

            if (o != null && Microsoft.Scripting.ComInterop.ComBinder.IsComObject(o)) {
                foreach (string name in Microsoft.Scripting.ComInterop.ComBinder.GetDynamicMemberNames(o)) {
                    if (!res.Contains(name)) {
                        res.AddNoLock(name);
                    }
                }
            }
#endif
            return res;
        }

        /// <summary>
        /// Called from generated code emitted by NewTypeMaker.
        /// </summary>
        public static void CheckInitializedAttribute(object o, object self, string name) {
            if (o == Uninitialized.Instance) {
                throw PythonOps.AttributeError("'{0}' object has no attribute '{1}'",
                    PythonTypeOps.GetName(self),
                    name);
            }
        }

        public static object GetUserSlotValue(CodeContext/*!*/ context, PythonTypeUserDescriptorSlot slot, object instance, PythonType type) {
            return slot.GetValue(context, instance, type);
        }

        /// <summary>
        /// Handles the descriptor protocol for user-defined objects that may implement __get__
        /// </summary>
        public static object GetUserDescriptor(object o, object instance, object context) {
            if (o is IPythonObject) {
                // slow, but only encountred for user defined descriptors.
                PerfTrack.NoteEvent(PerfTrack.Categories.DictInvoke, "__get__");
                object ret;
                if (PythonContext.TryInvokeTernaryOperator(DefaultContext.Default,
                    TernaryOperators.GetDescriptor,
                    o,
                    instance,
                    context,
                    out ret)) {
                    return ret;
                }
            }

            return o;
        }

        /// <summary>
        /// Handles the descriptor protocol for user-defined objects that may implement __set__
        /// </summary>
        public static bool TrySetUserDescriptor(object o, object instance, object value) {
            // slow, but only encountred for user defined descriptors.
            PerfTrack.NoteEvent(PerfTrack.Categories.DictInvoke, "__set__");

            object dummy;
            return PythonContext.TryInvokeTernaryOperator(DefaultContext.Default,
                TernaryOperators.SetDescriptor,
                o,
                instance,
                value,
                out dummy);
        }

        /// <summary>
        /// Handles the descriptor protocol for user-defined objects that may implement __delete__
        /// </summary>
        public static bool TryDeleteUserDescriptor(object o, object instance) {
            // slow, but only encountred for user defined descriptors.
            PerfTrack.NoteEvent(PerfTrack.Categories.DictInvoke, "__delete__");

            object dummy;
            return PythonTypeOps.TryInvokeBinaryOperator(DefaultContext.Default,
                o,
                instance,
                "__delete__",
                out dummy);
        }

        public static object Invoke(CodeContext/*!*/ context, object target, string name, params object[] args) {
            return PythonCalls.Call(context, PythonOps.GetBoundAttr(context, target, name), args);
        }

        public static Delegate CreateDynamicDelegate(DynamicMethod meth, Type delegateType, object target) {
            // Always close delegate around its own instance of the frame
            return meth.CreateDelegate(delegateType, target);
        }

        public static double CheckMath(double v) {
            if (double.IsInfinity(v)) {
                throw PythonOps.OverflowError("math range error");
            } else if (double.IsNaN(v)) {
                throw PythonOps.ValueError("math domain error");
            } else {
                return v;
            }
        }

        public static double CheckMath(double input, double output) {
            if (double.IsInfinity(input) && double.IsInfinity(output) ||
                double.IsNaN(input) && double.IsNaN(output)) {
                return output;
            } else {
                return CheckMath(output);
            }
        }

        public static double CheckMath(double in0, double in1, double output) {
            if ((double.IsInfinity(in0) || double.IsInfinity(in1)) && double.IsInfinity(output) ||
                (double.IsNaN(in0) || double.IsNaN(in1)) && double.IsNaN(output)) {
                return output;
            } else {
                return CheckMath(output);
            }
        }

        public static object IsMappingType(CodeContext/*!*/ context, object o) {
            if (o is IDictionary || o is PythonDictionary || o is IDictionary<object, object> || o is PythonDictionary) {
                return ScriptingRuntimeHelpers.True;
            }
            object getitem;
            if ((o is IPythonObject) && PythonOps.TryGetBoundAttr(context, o, "__getitem__", out getitem)) {
                if (!PythonOps.IsClsVisible(context)) {
                    // in standard Python methods aren't mapping types, therefore
                    // if the user hasn't broken out of that box yet don't treat 
                    // them as mapping types.
                    if (o is BuiltinFunction) return ScriptingRuntimeHelpers.False;
                }
                return ScriptingRuntimeHelpers.True;
            }
            return ScriptingRuntimeHelpers.False;
        }

        public static int FixSliceIndex(int v, int len) {
            if (v < 0) v = len + v;
            if (v < 0) return 0;
            if (v > len) return len;
            return v;
        }

        public static long FixSliceIndex(long v, long len) {
            if (v < 0) v = len + v;
            if (v < 0) return 0;
            if (v > len) return len;
            return v;
        }

        public static void FixSlice(
            int length, object start, object stop, object step,
            out int ostart, out int ostop, out int ostep
        ) {
            if (step == null) {
                ostep = 1;
            } else {
                ostep = Converter.ConvertToIndex(step);
                if (ostep == 0) {
                    throw PythonOps.ValueError("step cannot be zero");
                }
            }

            if (start == null) {
                ostart = ostep > 0 ? 0 : length - 1;
            } else {
                ostart = Converter.ConvertToIndex(start);
                if (ostart < 0) {
                    ostart += length;
                    if (ostart < 0) {
                        ostart = ostep > 0 ? Math.Min(length, 0) : Math.Min(length - 1, -1);
                    }
                } else if (ostart >= length) {
                    ostart = ostep > 0 ? length : length - 1;
                }
            }

            if (stop == null) {
                ostop = ostep > 0 ? length : -1;
            } else {
                ostop = Converter.ConvertToIndex(stop);
                if (ostop < 0) {
                    ostop += length;
                    if (ostop < 0) {
                        ostop = ostep > 0 ? Math.Min(length, 0) : Math.Min(length - 1, -1);
                    }
                } else if (ostop >= length) {
                    ostop = ostep > 0 ? length : length - 1;
                }
            }
        }

        public static void FixSlice(
            long length, long? start, long? stop, long? step,
            out long ostart, out long ostop, out long ostep, out long ocount
        ) {
            if (step == null) {
                ostep = 1;
            } else if (step == 0) {
                throw PythonOps.ValueError("step cannot be zero");
            } else {
                ostep = step.Value;
            }

            if (start == null) {
                ostart = ostep > 0 ? 0 : length - 1;
            } else {
                ostart = start.Value;
                if (ostart < 0) {
                    ostart += length;
                    if (ostart < 0) {
                        ostart = ostep > 0 ? Math.Min(length, 0) : Math.Min(length - 1, -1);
                    }
                } else if (ostart >= length) {
                    ostart = ostep > 0 ? length : length - 1;
                }
            }

            if (stop == null) {
                ostop = ostep > 0 ? length : -1;
            } else {
                ostop = stop.Value;
                if (ostop < 0) {
                    ostop += length;
                    if (ostop < 0) {
                        ostop = ostep > 0 ? Math.Min(length, 0) : Math.Min(length - 1, -1);
                    }
                } else if (ostop >= length) {
                    ostop = ostep > 0 ? length : length - 1;
                }
            }

            ocount = Math.Max(0, ostep > 0 ? (ostop - ostart + ostep - 1) / ostep
                                           : (ostop - ostart + ostep + 1) / ostep);
        }

        public static int FixIndex(int v, int len) {
            if (v < 0) {
                v += len;
                if (v < 0) {
                    throw PythonOps.IndexError("index out of range: {0}", v - len);
                }
            } else if (v >= len) {
                throw PythonOps.IndexError("index out of range: {0}", v);
            }
            return v;
        }

        public static void InitializeForFinalization(CodeContext/*!*/ context, object newObject) {
            IWeakReferenceable iwr = context.GetPythonContext().ConvertToWeakReferenceable(newObject);
            Debug.Assert(iwr != null);

            InstanceFinalizer nif = new InstanceFinalizer(context, newObject);
            iwr.SetFinalizer(new WeakRefTracker(nif, nif));
        }

        private static bool TryGetMetaclass(CodeContext/*!*/ context, PythonTuple bases, PythonDictionary dict, out object metaclass) {
            return dict.TryGetValue("__metaclass__", out metaclass) && metaclass != null;
        }

        internal static object CallPrepare(CodeContext/*!*/ context, PythonType meta, string name, PythonTuple bases, PythonDictionary dict) {
            object classdict = dict;

            PythonTypeSlot pts;
            // if available, call the __prepare__ method to get the classdict (PEP 3115)
            if (meta.TryLookupSlot(context, "__prepare__", out pts)) {
                object value;
                if (pts.TryGetValue(context, null, meta, out value)) {
                    classdict = PythonOps.CallWithContext(context, value, name, bases);
                    // copy the contents of dict to the classdict
                    foreach (var pair in dict)
                        context.LanguageContext.SetIndex(classdict, pair.Key, pair.Value);
                }
            }

            return classdict;
        }

        public static object MakeClass(FunctionCode funcCode, Func<CodeContext, CodeContext> body, CodeContext/*!*/ parentContext, string name, object[] bases, string selfNames) {
            Func<CodeContext, CodeContext> func = GetClassCode(parentContext, funcCode, body);

            return MakeClass(parentContext, name, bases, selfNames, func(parentContext).Dict);
        }

        private static Func<CodeContext, CodeContext> GetClassCode(CodeContext/*!*/ context, FunctionCode funcCode, Func<CodeContext, CodeContext> body) {
            if (body == null) {
                if (funcCode.Target == null) {
                    funcCode.UpdateDelegate(context.LanguageContext, true);
                }
                return (Func<CodeContext, CodeContext>)funcCode.Target;
            } else {
                if (funcCode.Target == null) {
                    funcCode.SetTarget(body);
                    funcCode._normalDelegate = body;
                }
                return body;
            }
        }

        internal static object MakeClass(CodeContext/*!*/ context, string name, object[] bases, string selfNames, PythonDictionary vars) {
            foreach (object dt in bases) {
                if (dt is TypeGroup) {
                    object[] newBases = new object[bases.Length];
                    for (int i = 0; i < bases.Length; i++) {
                        TypeGroup tc = bases[i] as TypeGroup;
                        if (tc != null) {
                            Type nonGenericType;
                            if (!tc.TryGetNonGenericType(out nonGenericType)) {
                                throw PythonOps.TypeError("cannot derive from open generic types " + Builtin.repr(context, tc).ToString());
                            }
                            newBases[i] = DynamicHelpers.GetPythonTypeFromType(nonGenericType);
                        } else {
                            newBases[i] = bases[i];
                        }
                    }
                    bases = newBases;
                    break;
                }
            }

            PythonTuple tupleBases = PythonTuple.MakeTuple(bases);

            object metaclass;
            if (!TryGetMetaclass(context, tupleBases, vars, out metaclass)) {
                return PythonType.__new__(context, TypeCache.PythonType, name, tupleBases, vars, selfNames);
            }

            object classdict = vars;

            if (metaclass is PythonType) {
                classdict = CallPrepare(context, (PythonType)metaclass, name, tupleBases, vars);
            }            

            // eg:
            // def foo(*args): print args            
            // __metaclass__ = foo
            // class bar: pass
            // calls our function...
            PythonContext pc = PythonContext.GetContext(context);

            object clazz = pc.MetaClassCallSite.Target(
                pc.MetaClassCallSite,
                context,
                metaclass,
                name,
                tupleBases,
                classdict
            );

            PythonType newType = clazz as PythonType;
            if(newType != null && newType.BaseTypes.Count == 0) {
                newType.BaseTypes.Add(DynamicHelpers.GetPythonTypeFromType(typeof(object)));
            }

            return clazz;
        }

        /// <summary>
        /// Python runtime helper for raising assertions. Used by AssertStatement.
        /// </summary>
        /// <param name="msg">Object representing the assertion message</param>
        public static void RaiseAssertionError(object msg) {
            if (msg == null) {
                throw PythonOps.AssertionError(String.Empty, ArrayUtils.EmptyObjects);
            } else {
                string message = PythonOps.ToString(msg);
                throw PythonOps.AssertionError("{0}", new object[] { message });
            }

        }

        /// <summary>
        /// Python runtime helper to create instance of Python List object.
        /// </summary>
        /// <returns>New instance of List</returns>
        public static List MakeList() {
            return new List();
        }

        /// <summary>
        /// Python runtime helper to create a populated instance of Python List object.
        /// </summary>
        public static List MakeList(params object[] items) {
            return new List(items);
        }

        /// <summary>
        /// Python runtime helper to create a populated instance of Python List object w/o
        /// copying the array contents.
        /// </summary>
        [NoSideEffects]
        public static List MakeListNoCopy(params object[] items) {
            return List.FromArrayNoCopy(items);
        }

        /// <summary>
        /// Python runtime helper to create a populated instance of Python List object.
        /// 
        /// List is populated by arbitrary user defined object.
        /// </summary>
        public static List MakeListFromSequence(object sequence) {
            return new List(sequence);
        }

        /// <summary>
        /// Python runtime helper to create an instance of Python List object.
        /// 
        /// List has the initial provided capacity.
        /// </summary>
        [NoSideEffects]
        public static List MakeEmptyList(int capacity) {
            return new List(capacity);
        }

        [NoSideEffects]
        public static List MakeEmptyListFromCode() {
            return List.FromArrayNoCopy(ArrayUtils.EmptyObjects);
        }

        /// <summary>
        /// Python runtime helper to create an instance of Tuple
        /// </summary>
        /// <param name="items"></param>
        /// <returns></returns>
        [NoSideEffects]
        public static PythonTuple MakeTuple(params object[] items) {
            return PythonTuple.MakeTuple(items);
        }

        /// <summary>
        /// Python runtime helper to create an instance of Tuple
        /// </summary>
        /// <param name="items"></param>
        [NoSideEffects]
        public static PythonTuple MakeTupleFromSequence(object items) {
            return PythonTuple.Make(items);
        }

        /// <summary>
        /// Python Runtime Helper for enumerator unpacking (tuple assignments, ...)
        /// Creates enumerator from the input parameter e, and then extracts 
        /// expected number of values, returning them as array
        /// 
        /// If the input is a Python tuple returns the tuples underlying data array.  Callers
        /// should not mutate the resulting tuple.
        /// </summary>
        /// <param name="context">The code context of the AST getting enumerator values.</param>
        /// <param name="e">object to enumerate</param>
        /// <param name="expected">expected number of objects to extract from the enumerator</param>
        /// <returns>
        /// array of objects (.Lengh == expected) if exactly expected objects are in the enumerator.
        /// Otherwise throws exception
        /// </returns>
        [LightThrowing]
        public static object GetEnumeratorValues(CodeContext/*!*/ context, object e, int expected, int argcntafter) {
            if (e != null && e.GetType() == typeof(PythonTuple)) {
                // fast path for tuples, avoid enumerating & copying the tuple.
                return GetEnumeratorValuesFromTuple((PythonTuple)e, expected, argcntafter);
            }

            IEnumerator ie = PythonOps.GetEnumeratorForUnpack(context, e);

            if (argcntafter == -1) {
                int count = 0;
                object[] values = new object[expected];

                while (count < expected) {
                    if (!ie.MoveNext()) {
                        return LightExceptions.Throw(PythonOps.ValueErrorForUnpackMismatch(expected, argcntafter, count));
                    }
                    values[count] = ie.Current;
                    count++;
                }

                if (ie.MoveNext()) {
                    return LightExceptions.Throw(PythonOps.ValueErrorForUnpackMismatch(expected, argcntafter, count + 1));
                }

                return values;
            }

            return OneTimeIEnumerableFromEnumerator(ie).ToArray();
        }

        private static IEnumerable<object> OneTimeIEnumerableFromEnumerator(IEnumerator ie) {
            while (ie.MoveNext()) {
                yield return ie.Current;
            }
        }

        [LightThrowing]
        public static object GetEnumeratorValuesNoComplexSets(CodeContext/*!*/ context, object e, int expected, int argcntafter) {
            if (e != null && e.GetType() == typeof(List)) {
                // fast path for lists, avoid enumerating & copying the list.
                return GetEnumeratorValuesFromList((List)e, expected, argcntafter);
            }

            return GetEnumeratorValues(context, e, expected, argcntafter);
        }

        [LightThrowing]
        private static object GetEnumeratorValuesFromTuple(PythonTuple pythonTuple, int expected, int argcntafter) {
            if (argcntafter == -1) {
                if (pythonTuple.Count == expected) {
                    return pythonTuple._data;
                }
            }
            else {
                if (pythonTuple.Count >= expected + argcntafter) {
                    return pythonTuple._data;
                }
            }

            return LightExceptions.Throw(PythonOps.ValueErrorForUnpackMismatch(expected, argcntafter, pythonTuple.Count));
        }

        [LightThrowing]
        private static object GetEnumeratorValuesFromList(List list, int expected, int argcntafter) {
            if (argcntafter == -1) {
                if (list._size == expected) {
                    return list._data;
                }
            }
            else {
                if (list._size > expected + argcntafter) {
                    return list._data;
                }
            }

            return LightExceptions.Throw(PythonOps.ValueErrorForUnpackMismatch(expected, argcntafter, list._size));
        }

        [LightThrowing]
        public static object UnpackIterable(CodeContext/*!*/ context, object e, int expected, int argcntafter) {
            var enumeratorValues = GetEnumeratorValuesNoComplexSets(context, e, expected, argcntafter);
            if (argcntafter == -1 || LightExceptions.IsLightException(enumeratorValues)) return enumeratorValues;

            Debug.Assert(enumeratorValues is object[]);

            var list = new List((object[])enumeratorValues);

            if (list._size < expected + argcntafter) return LightExceptions.Throw(PythonOps.ValueErrorForUnpackMismatch(expected, argcntafter, list._size));

            var newValues = new object[expected + argcntafter + 1];

            for (var i = 0; i < expected; i++) {
                newValues[i] = list.pop(0);
            }

            newValues[expected] = list;

            for (var i = argcntafter; i > 0; i--) {
                newValues[expected + i] = list.pop();
            }

            return newValues;
        }

        /// <summary>
        /// Python runtime helper to create instance of Slice object
        /// </summary>
        /// <param name="start">Start of the slice.</param>
        /// <param name="stop">End of the slice.</param>
        /// <param name="step">Step of the slice.</param>
        /// <returns>Slice</returns>
        public static Slice MakeSlice(object start, object stop, object step) {
            return new Slice(start, stop, step);
        }

        #region Standard I/O support

        public static void Write(CodeContext/*!*/ context, object f, string text) {
            PythonContext pc = PythonContext.GetContext(context);

            if (f == null) {
                f = pc.SystemStandardOut;
            }
            if (f == null || f == Uninitialized.Instance) {
                throw PythonOps.RuntimeError("lost sys.stdout");
            }

            PythonFile pf = f as PythonFile;
            if (pf != null) {
                // avoid spinning up a site in the normal case
                pf.write(text);
                return;
            }

            pc.WriteCallSite.Target(
                pc.WriteCallSite,
                context,
                GetBoundAttr(context, f, "write"),
                text
            );
        }

        private static object ReadLine(CodeContext/*!*/ context, object f) {
            if (f == null || f == Uninitialized.Instance) throw PythonOps.RuntimeError("lost sys.std_in");
            return PythonOps.Invoke(context, f, "readline");
        }

        public static void WriteSoftspace(CodeContext/*!*/ context, object f) {
            if (CheckSoftspace(f)) {
                SetSoftspace(f, ScriptingRuntimeHelpers.False);
                Write(context, f, " ");
            }
        }

        public static void SetSoftspace(object f, object value) {
            PythonOps.SetAttr(DefaultContext.Default, f, "softspace", value);
        }

        public static bool CheckSoftspace(object f) {
            PythonFile pf = f as PythonFile;
            if (pf != null) {
                // avoid spinning up a site in the common case
                return pf.softspace;
            }

            object result;
            if (PythonOps.TryGetBoundAttr(f, "softspace", out result)) {
                return PythonOps.IsTrue(result);
            }

            return false;
        }

        // Must stay here for now because libs depend on it.
        public static void Print(CodeContext/*!*/ context, object o) {
            PrintWithDest(context, PythonContext.GetContext(context).SystemStandardOut, o);
        }

        public static void PrintNoNewline(CodeContext/*!*/ context, object o) {
            PrintWithDestNoNewline(context, PythonContext.GetContext(context).SystemStandardOut, o);
        }

        public static void PrintWithDest(CodeContext/*!*/ context, object dest, object o) {
            PrintWithDestNoNewline(context, dest, o);
            Write(context, dest, "\n");
        }

        public static void PrintWithDestNoNewline(CodeContext/*!*/ context, object dest, object o) {
            WriteSoftspace(context, dest);
            Write(context, dest, o == null ? "None" : ToString(o));
        }

        public static object ReadLineFromSrc(CodeContext/*!*/ context, object src) {
            return ReadLine(context, src);
        }

        /// <summary>
        /// Prints newline into default standard output
        /// </summary>
        public static void PrintNewline(CodeContext/*!*/ context) {
            PrintNewlineWithDest(context, PythonContext.GetContext(context).SystemStandardOut);
        }

        /// <summary>
        /// Prints newline into specified destination. Sets softspace property to false.
        /// </summary>
        public static void PrintNewlineWithDest(CodeContext/*!*/ context, object dest) {
            PythonOps.Write(context, dest, "\n");
            PythonOps.SetSoftspace(dest, ScriptingRuntimeHelpers.False);
        }

        /// <summary>
        /// Prints value into default standard output with Python comma semantics.
        /// </summary>
        public static void PrintComma(CodeContext/*!*/ context, object o) {
            PrintCommaWithDest(context, PythonContext.GetContext(context).SystemStandardOut, o);
        }

        /// <summary>
        /// Prints value into specified destination with Python comma semantics.
        /// </summary>
        public static void PrintCommaWithDest(CodeContext/*!*/ context, object dest, object o) {
            PythonOps.WriteSoftspace(context, dest);
            string s = o == null ? "None" : PythonOps.ToString(o);

            PythonOps.Write(context, dest, s);
            PythonOps.SetSoftspace(dest, !s.EndsWith("\n"));
        }

        /// <summary>
        /// Called from generated code when we are supposed to print an expression value
        /// </summary>
        public static void PrintExpressionValue(CodeContext/*!*/ context, object value) {
            PythonContext pc = PythonContext.GetContext(context);
            object dispHook = pc.GetSystemStateValue("displayhook");
            pc.CallWithContext(context, dispHook, value);
        }

#if FEATURE_FULL_CONSOLE
        public static void PrintException(CodeContext/*!*/ context, Exception/*!*/ exception, IConsole console) {
            PythonContext pc = PythonContext.GetContext(context);
            PythonTuple exInfo = GetExceptionInfoLocal(context, exception);
            pc.SetSystemStateValue("last_type", exInfo[0]);
            pc.SetSystemStateValue("last_value", exInfo[1]);
            pc.SetSystemStateValue("last_traceback", exInfo[2]);

            object exceptHook = pc.GetSystemStateValue("excepthook");
            BuiltinFunction bf = exceptHook as BuiltinFunction;
            if (console != null && bf != null && bf.DeclaringType == typeof(SysModule) && bf.Name == "excepthook") {
                // builtin except hook, display it to the console which may do nice coloring
                console.WriteLine(pc.FormatException(exception), Style.Error);
            } else {
                // user defined except hook or no console
                try {
                    PythonCalls.Call(context, exceptHook, exInfo[0], exInfo[1], exInfo[2]);
                } catch (Exception e) {
                    PrintWithDest(context, pc.SystemStandardError, "Error in sys.excepthook:");
                    PrintWithDest(context, pc.SystemStandardError, pc.FormatException(e));
                    PrintNewlineWithDest(context, pc.SystemStandardError);

                    PrintWithDest(context, pc.SystemStandardError, "Original exception was:");
                    PrintWithDest(context, pc.SystemStandardError, pc.FormatException(exception));
                }
            }
        }
#endif

        #endregion

        #region Import support

        /// <summary>
        /// Called from generated code for:
        /// 
        /// import spam.eggs
        /// </summary>
        [ProfilerTreatsAsExternal, LightThrowing]
        public static object ImportTop(CodeContext/*!*/ context, string fullName, int level) {
            return Importer.ImportLightThrow(context, fullName, null, level);
        }

        /// <summary>
        /// Python helper method called from generated code for:
        /// 
        /// import spam.eggs as ham
        /// </summary>
        [ProfilerTreatsAsExternal, LightThrowing]
        public static object ImportBottom(CodeContext/*!*/ context, string fullName, int level) {
            object module = Importer.ImportLightThrow(context, fullName, null, level);

            if (!LightExceptions.IsLightException(module) && fullName.IndexOf('.') >= 0) {
                // Extract bottom from the imported module chain
                string[] parts = fullName.Split('.');

                for (int i = 1; i < parts.Length; i++) {
                    module = PythonOps.GetBoundAttr(context, module, parts[i]);
                }
            }
            return module;
        }

        /// <summary>
        /// Called from generated code for:
        /// 
        /// from spam import eggs1, eggs2 
        /// </summary>
        [ProfilerTreatsAsExternal, LightThrowing]
        public static object ImportWithNames(CodeContext/*!*/ context, string fullName, string[] names, int level) {
            return Importer.ImportLightThrow(context, fullName, PythonTuple.MakeTuple(names), level);
        }


        /// <summary>
        /// Imports one element from the module in the context of:
        /// 
        /// from module import a, b, c, d
        /// 
        /// Called repeatedly for all elements being imported (a, b, c, d above)
        /// </summary>
        public static object ImportFrom(CodeContext/*!*/ context, object module, string name) {
            return Importer.ImportFrom(context, module, name);
        }

        /// <summary>
        /// Called from generated code for:
        /// 
        /// from spam import *
        /// </summary>
        [ProfilerTreatsAsExternal]
        public static void ImportStar(CodeContext/*!*/ context, string fullName, int level) {
            object newmod = Importer.Import(context, fullName, PythonTuple.MakeTuple("*"), level);

            PythonModule scope = newmod as PythonModule;
            NamespaceTracker nt = newmod as NamespaceTracker;
            PythonType pt = newmod as PythonType;

            if (pt != null &&
                !pt.UnderlyingSystemType.IsEnum() &&
                (!pt.UnderlyingSystemType.IsAbstract() || !pt.UnderlyingSystemType.IsSealed())) {
                // from type import * only allowed on static classes (and enums)
                throw PythonOps.ImportError("no module named {0}", pt.Name);
            }

            IEnumerator exports;
            object all;
            bool filterPrivates = false;

            // look for __all__, if it's defined then use that to get the attribute names,
            // otherwise get all the names and filter out members starting w/ _'s.
            if (PythonOps.TryGetBoundAttr(context, newmod, "__all__", out all)) {
                exports = PythonOps.GetEnumerator(all);
            } else {
                exports = PythonOps.GetAttrNames(context, newmod).GetEnumerator();
                filterPrivates = true;
            }

            // iterate through the names and populate the scope with the values.
            while (exports.MoveNext()) {
                string name = exports.Current as string;
                if (name == null) {
                    throw PythonOps.TypeErrorForNonStringAttribute();
                } else if (filterPrivates && name.Length > 0 && name[0] == '_') {
                    continue;
                }

                // we special case several types to avoid one-off code gen of dynamic sites                
                if (scope != null) {
                    context.SetVariable(name, scope.__dict__[name]);
                } else if (nt != null) {
                    object value = NamespaceTrackerOps.GetCustomMember(context, nt, name);
                    if (value != OperationFailed.Value) {
                        context.SetVariable(name, value);
                    }
                } else if (pt != null) {
                    PythonTypeSlot pts;
                    object value;
                    if (pt.TryResolveSlot(context, name, out pts) &&
                        pts.TryGetValue(context, null, pt, out value)) {
                        context.SetVariable(name, value);
                    }
                } else {
                    // not a known type, we'll do use a site to do the get...
                    context.SetVariable(name, PythonOps.GetBoundAttr(context, newmod, name));
                }
            }
        }

        #endregion

        #region Exec

        /// <summary>
        /// Unqualified exec statement support.
        /// A Python helper which will be called for the statement:
        /// 
        /// exec code
        /// </summary>
        [ProfilerTreatsAsExternal]
        public static void UnqualifiedExec(CodeContext/*!*/ context, object code) {
            PythonDictionary locals = null;
            PythonDictionary globals = null;

            // if the user passes us a tuple we'll extract the 3 values out of it            
            PythonTuple codeTuple = code as PythonTuple;
            if (codeTuple != null && codeTuple.__len__() > 0 && codeTuple.__len__() <= 3) {
                code = codeTuple[0];

                if (codeTuple.__len__() > 1 && codeTuple[1] != null) {
                    globals = codeTuple[1] as PythonDictionary;
                    if (globals == null) throw PythonOps.TypeError("globals must be dictionary or none");
                }

                if (codeTuple.__len__() > 2 && codeTuple[2] != null) {
                    locals = codeTuple[2] as PythonDictionary;
                    if (locals == null) throw PythonOps.TypeError("locals must be dictionary or none");
                } else {
                    locals = globals;
                }
            }

            QualifiedExec(context, code, globals, locals);
        }

        /// <summary>
        /// Qualified exec statement support,
        /// Python helper which will be called for the statement:
        /// 
        /// exec code in globals [, locals ]
        /// </summary>
        [ProfilerTreatsAsExternal]
        public static void QualifiedExec(CodeContext/*!*/ context, object code, PythonDictionary globals, object locals) {
            PythonFile pf;
            Stream cs;

            var pythonContext = PythonContext.GetContext(context);

            bool noLineFeed = true;

            // TODO: use ContentProvider?
            if ((pf = code as PythonFile) != null) {
                List lines = pf.readlines();

                StringBuilder fullCode = new StringBuilder();
                for (int i = 0; i < lines.__len__(); i++) {
                    fullCode.Append(lines[i]);
                }

                code = fullCode.ToString();
            } else if ((cs = code as Stream) != null) {

                using (StreamReader reader = new StreamReader(cs)) { // TODO: encoding? 
                    code = reader.ReadToEnd();
                }

                noLineFeed = false;
            }

            string strCode = code as string;

            if (strCode != null) {
                SourceUnit source;

                if (noLineFeed) {
                    source = pythonContext.CreateSourceUnit(new NoLineFeedSourceContentProvider(strCode), "<string>", SourceCodeKind.Statements);
                } else {
                    source = pythonContext.CreateSnippet(strCode, SourceCodeKind.Statements);
                }

                PythonCompilerOptions compilerOptions = Builtin.GetRuntimeGeneratedCodeCompilerOptions(context, true, 0);

                // do interpretation only on strings -- not on files, streams, or code objects
                code = FunctionCode.FromSourceUnit(source, compilerOptions, false);
            }

            FunctionCode fc = code as FunctionCode;
            if (fc == null) {
                throw PythonOps.TypeError("arg 1 must be a string, file, Stream, or code object, not {0}", PythonTypeOps.GetName(code));
            }

            if (locals == null) locals = globals;
            if (globals == null) globals = context.GlobalDict;

            if (locals != null && PythonOps.IsMappingType(context, locals) != ScriptingRuntimeHelpers.True) {
                throw PythonOps.TypeError("exec: arg 3 must be mapping or None");
            }

            CodeContext execContext = Builtin.GetExecEvalScope(context, globals, Builtin.GetAttrLocals(context, locals), true, false);
            if (context.LanguageContext.PythonOptions.Frames) {
                List<FunctionStack> stack = PushFrame(execContext, fc);
                try {
                    fc.Call(execContext);
                } finally {
                    stack.RemoveAt(stack.Count - 1);
                }
            } else {
                fc.Call(execContext);
            }
        }

        #endregion

        public static ICollection GetCollection(object o) {
            ICollection ret = o as ICollection;
            if (ret != null) return ret;

            List<object> al = new List<object>();
            IEnumerator e = GetEnumerator(o);
            while (e.MoveNext()) al.Add(e.Current);
            return al;
        }

        public static IEnumerator GetEnumerator(object o) {
            return GetEnumerator(DefaultContext.Default, o);
        }

        public static IEnumerator GetEnumerator(CodeContext/*!*/ context, object o) {
            IEnumerator ie;
            if (!TryGetEnumerator(context, o, out ie)) {
                throw PythonOps.TypeError("{0} is not iterable", PythonTypeOps.GetName(o));
            }
            return ie;
        }

        // Lack of type restrictions allows this method to return the direct result of __iter__ without
        // wrapping it. This is the proper behavior for Builtin.iter().
        public static object GetEnumeratorObject(CodeContext/*!*/ context, object o) {
            object iterFunc;
            if (PythonOps.TryGetBoundAttr(context, o, "__iter__", out iterFunc) &&
                !Object.ReferenceEquals(iterFunc, NotImplementedType.Value)) {
                return PythonOps.CallWithContext(context, iterFunc);
            }

            return GetEnumerator(context, o);
        }

        public static IEnumerator GetEnumeratorForUnpack(CodeContext/*!*/ context, object enumerable) {
            IEnumerator enumerator;
            if (!TryGetEnumerator(context, enumerable, out enumerator)) {
                throw TypeErrorForNotIterable(enumerable);
            }

            return enumerator;
        }

        public static Exception TypeErrorForNotIterable(object enumerable) {
            return PythonOps.TypeError("'{0}' object is not iterable", PythonTypeOps.GetName(enumerable));
        }        

        public static KeyValuePair<IEnumerator, IDisposable> ThrowTypeErrorForBadIteration(CodeContext/*!*/ context, object enumerable) {
            throw PythonOps.TypeError("iteration over non-sequence of type {0}", PythonTypeOps.GetName(enumerable));
        }

        internal static bool TryGetEnumerator(CodeContext/*!*/ context, object enumerable, out IEnumerator enumerator) {
            enumerator = null;
            IEnumerable enumer;
            if (PythonContext.GetContext(context).TryConvertToIEnumerable(enumerable, out enumer)) {
                enumerator = enumer.GetEnumerator();
                return true;
            }

            return false;
        }

        public static void ForLoopDispose(KeyValuePair<IEnumerator, IDisposable> iteratorInfo) {
            if (iteratorInfo.Value != null) {
                iteratorInfo.Value.Dispose();
            }
        }

        public static KeyValuePair<IEnumerator, IDisposable> StringEnumerator(string str) {
            return new KeyValuePair<IEnumerator, IDisposable>(StringOps.StringEnumerator(str), null);
        }

        public static KeyValuePair<IEnumerator, IDisposable> BytesEnumerator(IList<byte> bytes) {
            return new KeyValuePair<IEnumerator, IDisposable>(IListOfByteOps.BytesEnumerator(bytes), null);
        }

        public static KeyValuePair<IEnumerator, IDisposable> BytesIntEnumerator(IList<byte> bytes) {
            return new KeyValuePair<IEnumerator, IDisposable>(IListOfByteOps.BytesIntEnumerator(bytes), null);
        }

        public static KeyValuePair<IEnumerator, IDisposable> GetEnumeratorFromEnumerable(IEnumerable enumerable) {
            IEnumerator enumerator = enumerable.GetEnumerator();
            return new KeyValuePair<IEnumerator, IDisposable>(enumerator, enumerator as IDisposable);
        }

        public static IEnumerable StringEnumerable(string str) {
            return StringOps.StringEnumerable(str);
        }

        public static IEnumerable BytesEnumerable(IList<byte> bytes) {
            return IListOfByteOps.BytesEnumerable(bytes);
        }

        public static IEnumerable BytesIntEnumerable(IList<byte> bytes) {
            return IListOfByteOps.BytesIntEnumerable(bytes);
        }

        #region Exception handling

        // The semantics here are:
        // 1. Each thread has a "current exception", which is returned as a tuple by sys.exc_info().
        // 2. The current exception is set on encountering an except block, even if the except block doesn't
        //    match the exception.
        // 3. Each function on exit (either via exception, return, or yield) will restore the "current exception" 
        //    to the value it had on function-entry. 
        //
        // So common codegen would be:
        // 
        // function() {
        //   $save = SaveCurrentException();
        //   try { 
        //      def foo():
        //              try:
        //              except:
        //                  SetCurrentException($exception)
        //                  <except body>
        //   
        //   finally {
        //      RestoreCurrentException($save)
        //   }

        // Called at the start of the except handlers to set the current exception. 
        public static object SetCurrentException(CodeContext/*!*/ context, Exception/*!*/ clrException) {
            Assert.NotNull(clrException);

            // we need to extract before we check because ThreadAbort.ExceptionState is cleared after
            // we reset the abort.
            object res = PythonExceptions.ToPython(clrException);

#if FEATURE_EXCEPTION_STATE
            // Check for thread abort exceptions.
            // This is necessary to be able to catch python's KeyboardInterrupt exceptions.
            // CLR restrictions require that this must be called from within a catch block.  This gets
            // called even if we aren't going to handle the exception - we'll just reset the abort 
            ThreadAbortException tae = clrException as ThreadAbortException;
            if (tae != null && tae.ExceptionState is KeyboardInterruptException) {
                Thread.ResetAbort();
            }
#endif

            RawException = clrException;
            return res;
        }

        /// <summary>
        /// Called from generated code at the start of a catch block.
        /// </summary>
        public static void BuildExceptionInfo(CodeContext/*!*/ context, Exception clrException) {
            object pyExcep = PythonExceptions.ToPython(clrException);
            List<DynamicStackFrame> frames = clrException.GetFrameList();
            IPythonObject pyObj = pyExcep as IPythonObject;

            object excType;
            if (pyObj != null) {
                // class is always the Python type for new-style types (this is also the common case)
                excType = pyObj.PythonType;
            } else {
                excType = PythonOps.GetBoundAttr(context, pyExcep, "__class__");
            }

            context.LanguageContext.UpdateExceptionInfo(clrException, excType, pyExcep, frames);
        }

        // Clear the current exception. Most callers should restore the exception.
        // This is mainly for sys.exc_clear()        
        public static void ClearCurrentException() {
            RestoreCurrentException(null);
        }

        public static void ExceptionHandled(CodeContext context) {
            var pyCtx = context.LanguageContext;

            pyCtx.ExceptionHandled();
        }

        // Called by code-gen to save it. Codegen just needs to pass this back to RestoreCurrentException.
        public static Exception SaveCurrentException() {
            return RawException;
        }

        // Called at function exit (like popping). Pass value from SaveCurrentException.
        public static void RestoreCurrentException(Exception clrException) {
            RawException = clrException;
        }

        public static object CheckException(CodeContext/*!*/ context, object exception, object test) {
            Debug.Assert(exception != null);

            ObjectException objex;

            if ((objex = exception as ObjectException) != null) {
                if (PythonOps.IsSubClass(context, objex.Type, test)) {
                    return objex.Instance;
                }
                return null;
            } else if (test is PythonType) {
                if (PythonOps.IsSubClass(test as PythonType, TypeCache.BaseException)) {
                    // catching a Python exception type explicitly.
                    if (PythonOps.IsInstance(context, exception, test)) return exception;
                } else if (PythonOps.IsSubClass(test as PythonType, DynamicHelpers.GetPythonTypeFromType(typeof(Exception)))) {
                    // catching a CLR exception type explicitly.
                    Exception clrEx = PythonExceptions.ToClr(exception);
                    if (PythonOps.IsInstance(context, clrEx, test)) return clrEx;
                }
            } else if (test is PythonTuple) {
                // we handle multiple exceptions, we'll check them one at a time.
                PythonTuple tt = test as PythonTuple;
                for (int i = 0; i < tt.__len__(); i++) {
                    object res = CheckException(context, exception, tt[i]);
                    if (res != null) return res;
                }
            }

            return null;
        }

        private static TraceBack CreateTraceBack(PythonContext pyContext, Exception e) {
            // user provided trace back
            var tb = e.GetTraceBack();
            if (tb != null) {
                return tb;
            }

            IList<DynamicStackFrame> frames = ((IList<DynamicStackFrame>)e.GetFrameList()) ?? new DynamicStackFrame[0];
            return CreateTraceBack(e, frames, frames.Count);
        }

        internal static TraceBack CreateTraceBack(Exception e, IList<DynamicStackFrame> frames, int frameCount) {
            TraceBack tb  = null;
            for (int i = 0; i < frameCount; i++) {
                DynamicStackFrame frame = frames[i];

                string name = frame.GetMethodName();
                if (name.IndexOf('#') > 0) {
                    // dynamic method, strip the trailing id...
                    name = name.Substring(0, name.IndexOf('#'));
                }

                PythonDynamicStackFrame pyFrame = frame as PythonDynamicStackFrame;
                if (pyFrame != null && pyFrame.CodeContext != null) {
                    CodeContext context = pyFrame.CodeContext;
                    FunctionCode code = pyFrame.Code;

                    TraceBackFrame tbf = new TraceBackFrame(
                        context,
                        context.GlobalDict,
                        context.Dict,
                        code,
                        tb != null ? tb.tb_frame : null);

                    tb = new TraceBack(tb, tbf);
                    tb.SetLine(frame.GetFileLineNumber());
                }
            }

            e.SetTraceBack(tb);
            return tb;
        }

        /// <summary>
        /// Get an exception tuple for the "current" exception. This is used for sys.exc_info()
        /// </summary>
        public static PythonTuple GetExceptionInfo(CodeContext/*!*/ context) {
            return GetExceptionInfoLocal(context, RawException);
        }

        /// <summary>
        /// Get an exception tuple for a given exception. This is like the inverse of MakeException.
        /// </summary>
        /// <param name="context">the code context</param>
        /// <param name="ex">the exception to create a tuple for.</param>
        /// <returns>a tuple of (type, value, traceback)</returns>
        /// <remarks>This is called directly by the With statement so that it can get an exception tuple
        /// in its own private except handler without disturbing the thread-wide sys.exc_info(). </remarks>
        public static PythonTuple/*!*/ GetExceptionInfoLocal(CodeContext/*!*/ context, Exception ex) {
            if (ex == null) {
                return PythonTuple.MakeTuple(null, null, null);
            }

            PythonContext pc = context.LanguageContext;

            object pyExcep = PythonExceptions.ToPython(ex);
            TraceBack tb = CreateTraceBack(pc, ex);

            IPythonObject pyObj = pyExcep as IPythonObject;
            object excType;
            if (pyObj != null) {
                // class is always the Python type for new-style types (this is also the common case)
                excType = pyObj.PythonType;
            } else {
                excType = PythonOps.GetBoundAttr(context, pyExcep, "__class__");
            }

            pc.UpdateExceptionInfo(excType, pyExcep, tb);

            return PythonTuple.MakeTuple(excType, pyExcep, tb);
        }

        /// <summary>
        /// helper function for re-raised exceptions.
        /// </summary>
        public static Exception MakeRethrownException(CodeContext/*!*/ context) {
            PythonTuple t = GetExceptionInfo(context);

            Exception e = MakeExceptionWorker(context, t[0], t[1], t[2], null, true);
            return MakeRethrowExceptionWorker(e);
        }
        /// <summary>
        /// helper function for re-raised exception.
        /// This entry point is used by 'raise' inside 'with' statement
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public static Exception MakeRethrowExceptionWorker(Exception e) {
            e.RemoveTraceBack();
            ExceptionHelpers.UpdateForRethrow(e);
            return e;
        }

        /// <summary>
        /// helper function for non-re-raise exceptions.
        /// 
        /// type is the type of exception to throw or an instance.  If it 
        /// is an instance then value should be null.  
        /// 
        /// If type is a type then value can either be an instance of type,
        /// a Tuple, or a single value.  This case is handled by EC.CreateThrowable.
        /// </summary>
        public static Exception MakeException(CodeContext/*!*/ context, object type, object value, object traceback, object cause) {
            Exception e = MakeExceptionWorker(context, type, value, traceback, cause, false);
            e.RemoveFrameList();
            return e;
        }

        internal static PythonExceptions.BaseException GetRawContextException() {
            if(RawException != null) {
                return RawException.GetPythonException() as PythonExceptions.BaseException;
            }
            return null;
        }

        private static Exception MakeExceptionWorker(CodeContext/*!*/ context, object type, object value, object traceback, object cause, bool forRethrow) {
            Exception throwable;
            PythonType pt;

            // unwrap tuples
            while (type is PythonTuple && ((PythonTuple)type).Any()) {
                type = ((PythonTuple)type).First();
            }

            if (type is PythonExceptions.BaseException) {
                var baseExc = ((PythonExceptions.BaseException)type);
                var c = cause as PythonExceptions.BaseException;
                baseExc.CreateClrExceptionWithCause(c, GetRawContextException());
                throwable = PythonExceptions.ToClr(type);
            } else if (type is Exception) {
                throwable = type as Exception;
            } else if ((pt = type as PythonType) != null && typeof(PythonExceptions.BaseException).IsAssignableFrom(pt.UnderlyingSystemType)) {
                throwable = PythonExceptions.CreateThrowableForRaise(context, pt, value, cause);
            } else {
                throwable = MakeExceptionTypeError(type);
            }

            if (traceback != null) {
                if (!forRethrow) {
                    TraceBack tb = traceback as TraceBack;
                    if (tb == null) throw PythonOps.TypeError("traceback argument must be a traceback object");

                    throwable.SetTraceBack(tb);
                }
            } else {
                throwable.RemoveTraceBack();
            }

            PerfTrack.NoteEvent(PerfTrack.Categories.Exceptions, throwable);

            return throwable;
        }

        public static Exception CreateThrowable(PythonType type, params object[] args) {
            return PythonExceptions.CreateThrowable(type, args);
        }

        #endregion

        public static string[] GetFunctionSignature(PythonFunction function) {
            return new string[] { function.GetSignatureString() };
        }

        public static PythonDictionary CopyAndVerifyDictionary(PythonFunction function, IDictionary dict) {
            foreach (object o in dict.Keys) {
                if (!(o is string)) {
                    throw TypeError("{0}() keywords must be strings", function.__name__);
                }
            }
            return new PythonDictionary(dict);
        }

        public static PythonDictionary/*!*/ CopyAndVerifyUserMapping(PythonFunction/*!*/ function, object dict) {
            return UserMappingToPythonDictionary(function.Context, dict, function.__name__);
        }

        public static PythonDictionary UserMappingToPythonDictionary(CodeContext/*!*/ context, object dict, string funcName) {
            // call dict.keys()
            object keys;
            if (!PythonTypeOps.TryInvokeUnaryOperator(context, dict, "keys", out keys)) {
                throw PythonOps.TypeError("{0}() argument after ** must be a mapping, not {1}",
                    funcName,
                    PythonTypeOps.GetName(dict));
            }

            PythonDictionary res = new PythonDictionary();

            // enumerate the keys getting their values
            IEnumerator enumerator = GetEnumerator(keys);
            while (enumerator.MoveNext()) {
                object o = enumerator.Current;
                string s = o as string;
                if (s == null) {
                    Extensible<string> es = o as Extensible<string>;
                    if (es == null) {
                        throw PythonOps.TypeError("{0}() keywords must be strings, not {0}",
                            funcName,
                            PythonTypeOps.GetName(dict));
                    }

                    s = es.Value;
                }

                res[o] = PythonOps.GetIndex(context, dict, o);
            }

            return res;
        }

        public static PythonDictionary CopyAndVerifyPythonDictionary(PythonFunction function, PythonDictionary dict) {
            if (dict._storage.HasNonStringAttributes()) {
                throw TypeError("{0}() keywords must be strings", function.__name__);
            }

            return new PythonDictionary(dict);
        }

        public static object ExtractDictionaryArgument(PythonFunction function, string name, int argCnt, PythonDictionary dict) {
            object val;
            if (dict.TryGetValue(name, out val)) {
                dict.Remove(name);
                return val;
            }

            throw PythonOps.TypeError("{0}() takes exactly {1} arguments ({2} given)",
                function.__name__,
                function.NormalArgumentCount,
                argCnt);
        }

        public static void AddDictionaryArgument(PythonFunction function, string name, object value, PythonDictionary dict) {
            if (dict.ContainsKey(name)) {
                throw MultipleKeywordArgumentError(function, name);
            }

            dict[name] = value;
        }

        public static void VerifyUnduplicatedByPosition(PythonFunction function, string name, int position, int listlen) {
            if (listlen > 0 && listlen > position) {
                throw MultipleKeywordArgumentError(function, name);
            }
        }

        public static List CopyAndVerifyParamsList(PythonFunction function, object list) {
            return new List(list);
        }

        public static PythonTuple GetOrCopyParamsTuple(PythonFunction function, object input) {
            if (input == null) {
                throw PythonOps.TypeError("{0}() argument after * must be a sequence, not NoneType", function.__name__);
            } else if (input.GetType() == typeof(PythonTuple)) {
                return (PythonTuple)input;
            }

            return PythonTuple.Make(input);
        }

        public static object ExtractParamsArgument(PythonFunction function, int argCnt, List list) {
            if (list.__len__() != 0) {
                return list.pop(0);
            }

            throw function.BadArgumentError(argCnt);
        }

        public static void AddParamsArguments(List list, params object[] args) {
            for (int i = 0; i < args.Length; i++) {
                list.insert(i, args[i]);
            }
        }

        /// <summary>
        /// Extracts an argument from either the dictionary or params
        /// </summary>
        public static object ExtractAnyArgument(PythonFunction function, string name, int argCnt, List list, IDictionary dict) {
            object val;
            if (dict.Contains(name)) {
                if (list.__len__() != 0) {
                    throw MultipleKeywordArgumentError(function, name);
                }
                val = dict[name];
                dict.Remove(name);
                return val;
            }

            if (list.__len__() != 0) {
                return list.pop(0);
            }

            if (function.ExpandDictPosition == -1 && dict.Count > 0) {
                // python raises an error for extra splatted kw keys before missing arguments.
                // therefore we check for this in the error case here.
                foreach (string x in dict.Keys) {
                    bool found = false;
                    foreach (string y in function.ArgNames) {
                        if (x == y) {
                            found = true;
                            break;
                        }
                    }

                    if (!found) {
                        throw UnexpectedKeywordArgumentError(function, x);
                    }
                }
            }

            throw BinderOps.TypeErrorForIncorrectArgumentCount(
                function.__name__,
                function.NormalArgumentCount,
                function.Defaults.Length,
                argCnt,
                function.ExpandListPosition != -1,
                dict.Count > 0);
        }

        public static ArgumentTypeException SimpleTypeError(string message) {
            return new TypeErrorException(message);
        }
        
        public static object GetParamsValueOrDefault(PythonFunction function, int index, List extraArgs) {
            if (extraArgs.__len__() > 0) {
                return extraArgs.pop(0);
            }

            return function.Defaults[index];
        }

        public static object GetFunctionParameterValue(PythonFunction function, int index, string name, List extraArgs, PythonDictionary dict) {
            if (extraArgs != null && extraArgs.__len__() > 0) {
                return extraArgs.pop(0);
            }

            object val;
            if (dict != null && dict.TryRemoveValue(name, out val)) {
                return val;
            }

            return function.Defaults[index];
        }

        public static void CheckParamsZero(PythonFunction function, List extraArgs) {
            if (extraArgs.__len__() != 0) {
                throw function.BadArgumentError(extraArgs.__len__() + function.NormalArgumentCount);
            }
        }

        public static void CheckUserParamsZero(PythonFunction function, object sequence) {
            int len = PythonOps.Length(sequence);
            if (len != 0) {
                throw function.BadArgumentError(len + function.NormalArgumentCount);
            }
        }

        public static void CheckDictionaryZero(PythonFunction function, IDictionary dict) {
            if (dict.Count != 0) {
                IDictionaryEnumerator ie = dict.GetEnumerator();
                ie.MoveNext();

                throw UnexpectedKeywordArgumentError(function, (string)ie.Key);
            }
        }

        public static bool CheckDictionaryMembers(PythonDictionary dict, string[] names) {
            if (dict.Count != names.Length) {
                return false;
            }

            foreach (string name in names) {
                if (!dict.ContainsKey(name)) {
                    return false;
                }
            }

            return true;
        }

        public static object PythonFunctionGetMember(PythonFunction function, string name) {
            object res;
            if (function._dict != null && function._dict.TryGetValue(name, out res)) {
                return res;
            }

            return OperationFailed.Value;
        }

        public static object PythonFunctionSetMember(PythonFunction function, string name, object value) {
            return function.__dict__[name] = value;
        }

        public static void PythonFunctionDeleteDict() {
            throw PythonOps.TypeError("cannot delete __dict__");
        }

        public static void PythonFunctionDeleteDoc(PythonFunction function) {
            function.__doc__ = null;
        }

        public static void PythonFunctionDeleteDefaults(PythonFunction function) {
            function.__defaults__ = null;
        }

        public static bool PythonFunctionDeleteMember(PythonFunction function, string name) {
            if (function._dict == null) return false;

            return function._dict.Remove(name);
        }

        /// <summary>
        /// Creates a new array the values set to Uninitialized.Instance.  The array
        /// is large enough to hold for all of the slots allocated for the type and
        /// its sub types.
        /// </summary>
        public static object[] InitializeUserTypeSlots(PythonType/*!*/ type) {
            if (type.SlotCount == 0) {
                // if we later set the weak reference obj we'll create the array
                return null;
            }

            // weak reference is stored at end of slots
            object[] res = new object[type.SlotCount + 1];
            for (int i = 0; i < res.Length - 1; i++) {
                res[i] = Uninitialized.Instance;
            }
            return res;
        }

        public static bool IsClsVisible(CodeContext/*!*/ context) {
            return context.ModuleContext.ShowCls;
        }

        public static object GetInitMember(CodeContext/*!*/ context, PythonType type, object instance) {
            object value;
            bool res = type.TryGetNonCustomBoundMember(context, instance, "__init__", out value);
            Debug.Assert(res);

            return value;
        }

        public static object GetInitSlotMember(CodeContext/*!*/ context, PythonType type, PythonTypeSlot slot, object instance) {
            object value;
            if (!slot.TryGetValue(context, instance, type, out value)) {
                throw PythonOps.TypeError("bad __init__");
            }

            return value;
        }

        public static object GetMixedMember(CodeContext/*!*/ context, PythonType type, object instance, string name) {
            foreach (PythonType t in type.ResolutionOrder) {
                PythonTypeSlot dts;
                if (t.TryLookupSlot(context, name, out dts)) {
                    object ret;
                    if (dts.TryGetValue(context, instance, type, out ret)) {
                        return ret;
                    }
                    return dts;
                }
            }

            throw AttributeErrorForMissingAttribute(type, name);
        }

        #region Slicing support

        /// <summary>
        /// Helper to determine if the value is a simple numeric type (int or big int or bool) - used for OldInstance
        /// deprecated form of slicing.
        /// </summary>
        public static bool IsNumericObject(object value) {
            return value is int || value is Extensible<int> || value is BigInteger || value is Extensible<BigInteger> || value is bool;
        }

        /// <summary>
        /// Helper to determine if the type is a simple numeric type (int or big int or bool) - used for OldInstance
        /// deprecated form of slicing.
        /// </summary>
        internal static bool IsNumericType(Type t) {
            return IsNonExtensibleNumericType(t) ||
                t.IsSubclassOf(typeof(Extensible<int>)) ||
                t.IsSubclassOf(typeof(Extensible<BigInteger>));
        }

        /// <summary>
        /// Helper to determine if the type is a simple numeric type (int or big int or bool) but not a subclass
        /// </summary>
        internal static bool IsNonExtensibleNumericType(Type t) {
            return t == typeof(int) ||
                t == typeof(bool) ||
                t == typeof(BigInteger);
        }

        /// <summary>
        /// For slicing.  Fixes up a BigInteger and returns an integer w/ the length of the
        /// object added if the value is negative.
        /// </summary>
        public static int NormalizeBigInteger(object self, BigInteger bi, ref Nullable<int> length) {
            int val;
            if (bi < BigInteger.Zero) {
                GetLengthOnce(self, ref length);

                if (bi.AsInt32(out val)) {
                    Debug.Assert(length.HasValue);
                    return val + length.Value;
                } else {
                    return -1;
                }
            } else if (bi.AsInt32(out val)) {
                return val;
            }

            return Int32.MaxValue;
        }

        /// <summary>
        /// For slicing.  Gets the length of the object, used to only get the length once.
        /// </summary>
        public static int GetLengthOnce(object self, ref Nullable<int> length) {
            if (length != null) return length.Value;

            length = PythonOps.Length(self);
            return length.Value;
        }

        #endregion

        public static ReflectedEvent.BoundEvent MakeBoundEvent(ReflectedEvent eventObj, object instance, Type type) {
            return new ReflectedEvent.BoundEvent(eventObj, instance, DynamicHelpers.GetPythonTypeFromType(type));
        }

        /// <summary>
        /// Helper method for DynamicSite rules that check the version of their dynamic object
        /// TODO - Remove this method for more direct field accesses
        /// </summary>
        /// <param name="o"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        public static bool CheckTypeVersion(object o, int version) {
            IPythonObject po = o as IPythonObject;
            if (po == null) return false;

            return po.PythonType.Version == version;
        }

        public static bool CheckSpecificTypeVersion(PythonType type, int version) {
            return type.Version == version;
        }

        #region Conversion helpers

        internal static MethodInfo GetConversionHelper(string name, ConversionResultKind resultKind) {
            MethodInfo res;
            switch (resultKind) {
                case ConversionResultKind.ExplicitCast:
                case ConversionResultKind.ImplicitCast:
                    res = typeof(PythonOps).GetMethod("Throwing" + name);
                    break;
                case ConversionResultKind.ImplicitTry:
                case ConversionResultKind.ExplicitTry:
                    res = typeof(PythonOps).GetMethod("NonThrowing" + name);
                    break;
                default: throw new InvalidOperationException();
            }
            Debug.Assert(res != null);
            return res;
        }

        public static object ConvertFloatToComplex(object value) {
            if (value == null) {
                return null;
            }

            double d = (double)value;
            return new Complex(d, 0.0);
        }

        internal static bool CheckingConvertToInt(object value) {
            return value is int || value is BigInteger || value is Extensible<int> || value is Extensible<BigInteger>;
        }

        internal static bool CheckingConvertToLong(object value) {
            return CheckingConvertToInt(value);
        }

        internal static bool CheckingConvertToFloat(object value) {
            return value is double || (value != null && value is Extensible<double>);
        }

        internal static bool CheckingConvertToComplex(object value) {
            return value is Complex || value is Extensible<Complex> || CheckingConvertToInt(value) || CheckingConvertToFloat(value);
        }

        internal static bool CheckingConvertToString(object value) {
            return value is string || value is Extensible<string>;
        }

        public static bool CheckingConvertToBool(object value) {
            return value is bool;
        }

        public static object NonThrowingConvertToInt(object value) {
            if (!CheckingConvertToInt(value)) return null;
            return value;
        }

        public static object NonThrowingConvertToLong(object value) {
            if (!CheckingConvertToInt(value)) return null;
            return value;
        }

        public static object NonThrowingConvertToFloat(object value) {
            if (!CheckingConvertToFloat(value)) return null;
            return value;
        }

        public static object NonThrowingConvertToComplex(object value) {
            if (!CheckingConvertToComplex(value)) return null;
            return value;
        }

        public static object NonThrowingConvertToString(object value) {
            if (!CheckingConvertToString(value)) return null;
            return value;
        }

        public static object NonThrowingConvertToBool(object value) {
            if (!CheckingConvertToBool(value)) return null;
            return value;
        }

        public static object ThrowingConvertToInt(object value) {
            if (!CheckingConvertToInt(value)) throw TypeError(" __int__ returned non-int (type {0})", PythonTypeOps.GetName(value));
            return value;
        }

        public static object ThrowingConvertToFloat(object value) {
            if (!CheckingConvertToFloat(value)) throw TypeError(" __float__ returned non-float (type {0})", PythonTypeOps.GetName(value));
            return value;
        }

        public static object ThrowingConvertToComplex(object value) {
            if (!CheckingConvertToComplex(value)) throw TypeError(" __complex__ returned non-complex (type {0})", PythonTypeOps.GetName(value));
            return value;
        }

        public static object ThrowingConvertToLong(object value) {
            if (!CheckingConvertToComplex(value)) throw TypeError(" __long__ returned non-long (type {0})", PythonTypeOps.GetName(value));
            return value;
        }

        public static object ThrowingConvertToString(object value) {
            if (!CheckingConvertToString(value)) throw TypeError(" __str__ returned non-str (type {0})", PythonTypeOps.GetName(value));
            return value;
        }

        public static bool ThrowingConvertToBool(object value) {
            if (!CheckingConvertToBool(value)) throw TypeError("__bool__ should return bool, returned {0}", PythonTypeOps.GetName(value));
            return (bool)value;
        }

        #endregion

        public static bool SlotTryGetBoundValue(CodeContext/*!*/ context, PythonTypeSlot/*!*/ slot, object instance, PythonType owner, out object value) {
            Debug.Assert(slot != null);
            return slot.TryGetValue(context, instance, owner, out value);
        }

        public static bool SlotTryGetValue(CodeContext/*!*/ context, PythonTypeSlot/*!*/ slot, object instance, PythonType owner, out object value) {
            return slot.TryGetValue(context, instance, owner, out value);
        }

        public static object SlotGetValue(CodeContext/*!*/ context, PythonTypeSlot/*!*/ slot, object instance, PythonType owner) {
            object value;
            if (!slot.TryGetValue(context, instance, owner, out value)) {
                throw new InvalidOperationException();
            }

            return value;
        }

        public static bool SlotTrySetValue(CodeContext/*!*/ context, PythonTypeSlot/*!*/ slot, object instance, PythonType owner, object value) {
            return slot.TrySetValue(context, instance, owner, value);
        }

        public static object SlotSetValue(CodeContext/*!*/ context, PythonTypeSlot/*!*/ slot, object instance, PythonType owner, object value) {
            if (!slot.TrySetValue(context, instance, owner, value)) {
                throw new InvalidOperationException();
            }

            return value;
        }

        public static bool SlotTryDeleteValue(CodeContext/*!*/ context, PythonTypeSlot/*!*/ slot, object instance, PythonType owner) {
            return slot.TryDeleteValue(context, instance, owner);
        }

        public static BuiltinFunction/*!*/ MakeBoundBuiltinFunction(BuiltinFunction/*!*/ function, object/*!*/ target) {
            return function.BindToInstance(target);
        }

        public static object GetBuiltinFunctionSelf(BuiltinFunction function) {
            return function.BindingSelf;
        }

        /// <summary>
        /// Called from generated code.  Gets a builtin function and the BuiltinFunctionData associated
        /// with the object.  Tests to see if the function is bound and has the same data for the generated
        /// rule.
        /// </summary>
        public static bool TestBoundBuiltinFunction(BuiltinFunction/*!*/ function, object data) {
            if (function.IsUnbound) {
                // not bound
                return false;
            }

            return function.TestData(data);
        }

        public static BuiltinFunction/*!*/ GetBuiltinMethodDescriptorTemplate(BuiltinMethodDescriptor/*!*/ descriptor) {
            return descriptor.Template;
        }

        public static int GetTypeVersion(PythonType type) {
            return type.Version;
        }

        public static bool TryResolveTypeSlot(CodeContext/*!*/ context, PythonType type, string name, out PythonTypeSlot slot) {
            return type.TryResolveSlot(context, name, out slot);
        }

        public static T[] ConvertTupleToArray<T>(PythonTuple tuple) {
            T[] res = new T[tuple.__len__()];
            for (int i = 0; i < tuple.__len__(); i++) {
                try {
                    res[i] = (T)tuple[i];
                } catch (InvalidCastException) {
                    res[i] = Converter.Convert<T>(tuple[i]);
                }
            }
            return res;
        }

        #region Function helpers

        public static PythonGenerator MakeGenerator(PythonFunction function, MutableTuple data, object generatorCode) {
            Func<MutableTuple, object> next = generatorCode as Func<MutableTuple, object>;
            if (next == null) {
                next = ((LazyCode<Func<MutableTuple, object>>)generatorCode).EnsureDelegate();
            }

            return new PythonGenerator(function, next, data);
        }

        public static object MakeGeneratorExpression(object function, object input) {
            PythonFunction func = (PythonFunction)function;
            return ((Func<PythonFunction, object, object>)func.__code__.Target)(func, input);
        }

        public static FunctionCode MakeFunctionCode(CodeContext/*!*/ context, string name, string documentation, string[] argNames, FunctionAttributes flags, int startIndex, int endIndex, string path, Delegate code, string[] freeVars, string[] names, string[] cellVars, string[] varNames, int localCount) {
            Compiler.Ast.SerializedScopeStatement scope = new Compiler.Ast.SerializedScopeStatement(name, argNames, flags, startIndex, endIndex, path, freeVars, names, cellVars, varNames);

            return new FunctionCode(context.LanguageContext, code, scope, documentation, localCount);
        }

        [NoSideEffects]
        public static object MakeFunction(CodeContext/*!*/ context, FunctionCode funcInfo, object modName, object[] defaults) {
            return new PythonFunction(context, funcInfo, modName, defaults, null);
        }

        [NoSideEffects]
        public static object MakeFunctionDebug(CodeContext/*!*/ context, FunctionCode funcInfo, object modName, object[] defaults, Delegate target) {
            funcInfo.SetDebugTarget(PythonContext.GetContext(context), target);

            return new PythonFunction(context, funcInfo, modName, defaults, null);
        }

        public static CodeContext FunctionGetContext(PythonFunction func) {
            return func.Context;
        }

        public static object FunctionGetDefaultValue(PythonFunction func, int index) {
            return func.Defaults[index];
        }

        public static int FunctionGetCompatibility(PythonFunction func) {
            return func.FunctionCompatibility;
        }

        public static int FunctionGetID(PythonFunction func) {
            return func.FunctionID;
        }

        public static Delegate FunctionGetTarget(PythonFunction func) {
            return func.__code__.Target;
        }

        public static Delegate FunctionGetLightThrowTarget(PythonFunction func) {
            return func.__code__.LightThrowTarget;
        }

        public static void FunctionPushFrame(PythonContext context) {
            if (PythonFunction.AddRecursionDepth(1) > context.RecursionLimit) {
                throw PythonOps.RuntimeError("maximum recursion depth exceeded");
            }
        }

        public static void FunctionPushFrameCodeContext(CodeContext context) {
            FunctionPushFrame(PythonContext.GetContext(context));
        }

        public static void FunctionPopFrame() {
            PythonFunction.AddRecursionDepth(-1);
        }

        #endregion

        public static object ReturnConversionResult(object value) {
            PythonTuple pt = value as PythonTuple;
            if (pt != null) {
                return pt[0];
            }
            return NotImplementedType.Value;
        }

        /// <summary>
        /// Convert object to a given type. This code is equivalent to NewTypeMaker.EmitConvertFromObject
        /// except that it happens at runtime instead of compile time.
        /// </summary>
        public static T ConvertFromObject<T>(object obj) {
            Type toType = typeof(T);
            object result;
            MethodInfo fastConvertMethod = PythonBinder.GetFastConvertMethod(toType);
            if (fastConvertMethod != null) {
                result = fastConvertMethod.Invoke(null, new object[] { obj });
            } else if (typeof(Delegate).IsAssignableFrom(toType)) {
                result = Converter.ConvertToDelegate(obj, toType);
            } else {
                result = obj;
            }
            return (T)result;
        }

        public static DynamicMetaObjectBinder MakeComplexCallAction(int count, bool list, string[] keywords) {
            Argument[] infos = CompilerHelpers.MakeRepeatedArray(Argument.Simple, count + keywords.Length);
            if (list) {
                infos[checked(count - 1)] = new Argument(ArgumentType.List);
            }
            for (int i = 0; i < keywords.Length; i++) {
                infos[count + i] = new Argument(keywords[i]);
            }

            return DefaultContext.DefaultPythonContext.Invoke(
                new CallSignature(infos)
            );
        }

        public static DynamicMetaObjectBinder MakeSimpleCallAction(int count) {
            return DefaultContext.DefaultPythonContext.Invoke(
                new CallSignature(CompilerHelpers.MakeRepeatedArray(Argument.Simple, count))
            );
        }

        public static PythonTuple ValidateCoerceResult(object coerceResult) {
            if (coerceResult == null || coerceResult == NotImplementedType.Value) {
                return null;
            }

            PythonTuple pt = coerceResult as PythonTuple;
            if (pt == null) throw PythonOps.TypeError("coercion should return None, NotImplemented, or 2-tuple, got {0}", PythonTypeOps.GetName(coerceResult));
            return pt;
        }

        public static object GetCoerceResultOne(PythonTuple coerceResult) {
            return coerceResult._data[0];
        }

        public static object GetCoerceResultTwo(PythonTuple coerceResult) {
            return coerceResult._data[1];
        }

        public static object MethodCheckSelf(CodeContext/*!*/ context, Method method, object self) {
            return method.CheckSelf(context, self);
        }

        [LightThrowing]
        public static object GeneratorCheckThrowableAndReturnSendValue(object self) {
            return ((PythonGenerator)self).CheckThrowableAndReturnSendValue();
        }

        public static ItemEnumerable CreateItemEnumerable(object callable, CallSite<Func<CallSite, CodeContext, object, int, object>> site) {
            return new ItemEnumerable(callable, site);
        }

        public static DictionaryKeyEnumerator MakeDictionaryKeyEnumerator(PythonDictionary dict) {
            return new DictionaryKeyEnumerator(dict._storage);
        }

        public static IEnumerable CreatePythonEnumerable(object baseObject) {
            return PythonEnumerable.Create(baseObject);
        }

        public static IEnumerator CreateItemEnumerator(object callable, CallSite<Func<CallSite, CodeContext, object, int, object>> site) {
            return new ItemEnumerator(callable, site);
        }

        public static IEnumerator CreatePythonEnumerator(object baseObject) {
            return PythonEnumerator.Create(baseObject);
        }

        public static bool ContainsFromEnumerable(CodeContext/*!*/ context, object enumerable, object value) {
            IEnumerator ie = enumerable as IEnumerator;
            if (ie == null) {
                IEnumerable ienum = enumerable as IEnumerable;
                if (ienum != null) {
                    ie = ienum.GetEnumerator();
                } else {
                    ie = Converter.ConvertToIEnumerator(enumerable);
                }
            }
            
            while (ie.MoveNext()) {
                if (PythonOps.EqualRetBool(context, ie.Current, value)) {
                    return true;
                }
            }
            return false;
        }

        public static object PythonTypeGetMember(CodeContext/*!*/ context, PythonType type, object instance, string name) {
            return type.GetMember(context, instance, name);
        }

        [NoSideEffects]
        public static object CheckUninitialized(object value, string name) {
            if (value == Uninitialized.Instance) {
                throw new UnboundLocalException(String.Format("Local variable '{0}' referenced before assignment.", name));
            }
            return value;
        }      

        public static object PythonTypeSetCustomMember(CodeContext/*!*/ context, PythonType self, string name, object value) {
            self.SetCustomMember(context, name, value);
            return value;
        }

        public static object PythonTypeDeleteCustomMember(CodeContext/*!*/ context, PythonType self, string name) {
            self.DeleteCustomMember(context, name);
            return null;
        }

        public static bool IsPythonType(PythonType type) {
            return type.IsPythonType;
        }

        public static object PublishModule(CodeContext/*!*/ context, string name) {
            object original = null;
            context.LanguageContext.SystemStateModules.TryGetValue(name, out original);
            var module = ((PythonScopeExtension)context.GlobalScope.GetExtension(context.LanguageContext.ContextId)).Module;
            context.LanguageContext.SystemStateModules[name] = module;
            return original;
        }

        public static void RemoveModule(CodeContext/*!*/ context, string name, object oldValue) {
            if (oldValue != null) {
                PythonContext.GetContext(context).SystemStateModules[name] = oldValue;
            } else {
                PythonContext.GetContext(context).SystemStateModules.Remove(name);
            }
        }

        public static Ellipsis Ellipsis {
            get {
                return Ellipsis.Value;
            }
        }

        public static NotImplementedType NotImplemented {
            get {
                return NotImplementedType.Value;
            }
        }

        public static void ListAddForComprehension(List l, object o) {
            l.AddNoLock(o);
        }

        public static void SetAddForComprehension(SetCollection s, object o) {
            s._items.AddNoLock(o);
        }

        public static void DictAddForComprehension(PythonDictionary d, object k, object v) {
            d._storage.AddNoLock(ref d._storage, k, v);
        }


        public static void ModuleStarted(CodeContext/*!*/ context, ModuleOptions features) {
            context.ModuleContext.Features |= features;
        }

        public static void Warn(CodeContext/*!*/ context, PythonType category, string message, params object[] args) {
            PythonContext pc = PythonContext.GetContext(context);
            object warnings = pc.GetWarningsModule(), warn = null;

            if (warnings != null) {
                warn = PythonOps.GetBoundAttr(context, warnings, "warn");
            }

            message = FormatWarning(message, args);

            if (warn == null) {
                PythonOps.PrintWithDest(context, pc.SystemStandardError, "warning: " + category.Name + ": " + message);
            } else {
                PythonOps.CallWithContext(context, warn, message, category);
            }
        }

        public static void ShowWarning(CodeContext/*!*/ context, PythonType category, string message, string filename, int lineNo) {
            PythonContext pc = PythonContext.GetContext(context);
            object warnings = pc.GetWarningsModule(), warn = null;

            if (warnings != null) {
                warn = PythonOps.GetBoundAttr(context, warnings, "showwarning");
            }

            if (warn == null) {
                PythonOps.PrintWithDestNoNewline(context, pc.SystemStandardError, String.Format("{0}:{1}: {2}: {3}\n", filename, lineNo, category.Name, message));
            } else {
                PythonOps.CallWithContext(context, warn, message, category, filename ?? "", lineNo);
            }
        }

        private static string FormatWarning(string message, object[] args) {
            for (int i = 0; i < args.Length; i++) {
                args[i] = PythonOps.ToString(args[i]);
            }

            message = String.Format(message, args);
            return message;
        }

        public static DynamicMetaObjectBinder MakeComboAction(CodeContext/*!*/ context, DynamicMetaObjectBinder opBinder, DynamicMetaObjectBinder convBinder) {
            return PythonContext.GetContext(context).BinaryOperationRetType((PythonBinaryOperationBinder)opBinder, (PythonConversionBinder)convBinder);
        }

        public static DynamicMetaObjectBinder MakeInvokeAction(CodeContext/*!*/ context, CallSignature signature) {
            return PythonContext.GetContext(context).Invoke(signature);
        }

        public static DynamicMetaObjectBinder MakeGetAction(CodeContext/*!*/ context, string name, bool isNoThrow) {
            return PythonContext.GetContext(context).GetMember(name);
        }

        public static DynamicMetaObjectBinder MakeCompatGetAction(CodeContext/*!*/ context, string name) {
            return PythonContext.GetContext(context).CompatGetMember(name, false);
        }

        public static DynamicMetaObjectBinder MakeCompatInvokeAction(CodeContext/*!*/ context, CallInfo callInfo) {
            return PythonContext.GetContext(context).CompatInvoke(callInfo);
        }

        public static DynamicMetaObjectBinder MakeCompatConvertAction(CodeContext/*!*/ context, Type toType, bool isExplicit) {
            return PythonContext.GetContext(context).Convert(toType, isExplicit ? ConversionResultKind.ExplicitCast : ConversionResultKind.ImplicitCast).CompatBinder;
        }

        public static DynamicMetaObjectBinder MakeSetAction(CodeContext/*!*/ context, string name) {
            return PythonContext.GetContext(context).SetMember(name);
        }

        public static DynamicMetaObjectBinder MakeDeleteAction(CodeContext/*!*/ context, string name) {
            return PythonContext.GetContext(context).DeleteMember(name);
        }

        public static DynamicMetaObjectBinder MakeConversionAction(CodeContext/*!*/ context, Type type, ConversionResultKind kind) {
            return PythonContext.GetContext(context).Convert(type, kind);
        }

        public static DynamicMetaObjectBinder MakeTryConversionAction(CodeContext/*!*/ context, Type type, ConversionResultKind kind) {
            return PythonContext.GetContext(context).Convert(type, kind);
        }

        public static DynamicMetaObjectBinder MakeOperationAction(CodeContext/*!*/ context, int operationName) {
            return PythonContext.GetContext(context).Operation((PythonOperationKind)operationName);
        }

        public static DynamicMetaObjectBinder MakeUnaryOperationAction(CodeContext/*!*/ context, ExpressionType expressionType) {
            return PythonContext.GetContext(context).UnaryOperation(expressionType);
        }

        public static DynamicMetaObjectBinder MakeBinaryOperationAction(CodeContext/*!*/ context, ExpressionType expressionType) {
            return PythonContext.GetContext(context).BinaryOperation(expressionType);
        }

        public static DynamicMetaObjectBinder MakeGetIndexAction(CodeContext/*!*/ context, int argCount) {
            return PythonContext.GetContext(context).GetIndex(argCount);
        }

        public static DynamicMetaObjectBinder MakeSetIndexAction(CodeContext/*!*/ context, int argCount) {
            return PythonContext.GetContext(context).SetIndex(argCount);
        }

        public static DynamicMetaObjectBinder MakeDeleteIndexAction(CodeContext/*!*/ context, int argCount) {
            return PythonContext.GetContext(context).DeleteIndex(argCount);
        }

        public static DynamicMetaObjectBinder MakeGetSliceBinder(CodeContext/*!*/ context) {
            return PythonContext.GetContext(context).GetSlice;
        }

        public static DynamicMetaObjectBinder MakeSetSliceBinder(CodeContext/*!*/ context) {
            return PythonContext.GetContext(context).SetSliceBinder;
        }

        public static DynamicMetaObjectBinder MakeDeleteSliceBinder(CodeContext/*!*/ context) {
            return PythonContext.GetContext(context).DeleteSlice;
        }

#if FEATURE_REFEMIT
        /// <summary>
        /// Provides access to AppDomain.DefineDynamicAssembly which cannot be called from a DynamicMethod
        /// </summary>
        public static AssemblyBuilder DefineDynamicAssembly(AssemblyName name, AssemblyBuilderAccess access) {
            return AppDomain.CurrentDomain.DefineDynamicAssembly(name, access);
        }

        /// <summary>
        /// Generates a new delegate type.  The last type in the array is the return type.
        /// </summary>
        public static Type/*!*/ MakeNewCustomDelegate(Type/*!*/[]/*!*/ types) {
            return MakeNewCustomDelegate(types, null);
        }

        /// <summary>
        /// Generates a new delegate type.  The last type in the array is the return type.
        /// </summary>
        public static Type/*!*/ MakeNewCustomDelegate(Type/*!*/[]/*!*/ types, CallingConvention? callingConvention) {
            const MethodAttributes CtorAttributes = MethodAttributes.RTSpecialName | MethodAttributes.HideBySig | MethodAttributes.Public;
            const MethodImplAttributes ImplAttributes = MethodImplAttributes.Runtime | MethodImplAttributes.Managed;
            const MethodAttributes InvokeAttributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual;

            Type returnType = types[types.Length - 1];
            Type[] parameters = ArrayUtils.RemoveLast(types);

            TypeBuilder builder = Snippets.Shared.DefineDelegateType("Delegate" + types.Length);
            builder.DefineConstructor(CtorAttributes, CallingConventions.Standard, _DelegateCtorSignature).SetImplementationFlags(ImplAttributes);
            builder.DefineMethod("Invoke", InvokeAttributes, returnType, parameters).SetImplementationFlags(ImplAttributes);

            if (callingConvention != null) {
                builder.SetCustomAttribute(new CustomAttributeBuilder(
                    typeof(UnmanagedFunctionPointerAttribute).GetConstructor(new[] { typeof(CallingConvention) }),
                    new object[] { callingConvention })
                );
            }

            return builder.CreateType();
        }

#if !SILVERLIGHT
        /// <summary>
        /// Provides the entry point for a compiled module.  The stub exe calls into InitializeModule which
        /// does the actual work of adding references and importing the main module.  Upon completion it returns
        /// the exit code that the program reported via SystemExit or 0.
        /// </summary>
        public static int InitializeModule(Assembly/*!*/ precompiled, string/*!*/ main, string[] references) {
            return InitializeModuleEx(precompiled, main, references, false);
        }

        /// <summary>
        /// Provides the entry point for a compiled module.  The stub exe calls into InitializeModule which
        /// does the actual work of adding references and importing the main module.  Upon completion it returns
        /// the exit code that the program reported via SystemExit or 0.
        /// </summary>
        public static int InitializeModuleEx(Assembly/*!*/ precompiled, string/*!*/ main, string[] references, bool ignoreEnvVars) {
            ContractUtils.RequiresNotNull(precompiled, "precompiled");
            ContractUtils.RequiresNotNull(main, "main");

            Dictionary<string, object> options = new Dictionary<string, object>();
            options["Arguments"] = Environment.GetCommandLineArgs();

            var pythonEngine = Python.CreateEngine(options);

            var pythonContext = (PythonContext)HostingHelpers.GetLanguageContext(pythonEngine);

            if (!ignoreEnvVars) {
                int pathIndex = pythonContext.PythonOptions.SearchPaths.Count;
                string path = Environment.GetEnvironmentVariable("IRONPYTHONPATH");
                if (path != null && path.Length > 0) {
                    string[] paths = path.Split(Path.PathSeparator);
                    foreach (string p in paths) {
                        pythonContext.InsertIntoPath(pathIndex++, p);
                    }
                }
                // TODO: add more environment variable setup here...
            }

            foreach (var scriptCode in SavableScriptCode.LoadFromAssembly(pythonContext.DomainManager, precompiled)) {
                pythonContext.GetCompiledLoader().AddScriptCode(scriptCode);
            }

            if (references != null) {
                foreach (string referenceName in references) {
                    pythonContext.DomainManager.LoadAssembly(Assembly.Load(referenceName));
                }
            }

            ModuleContext modCtx = new ModuleContext(new PythonDictionary(), pythonContext);
            // import __main__
            try {
                Importer.Import(modCtx.GlobalContext, main, PythonTuple.EMPTY, 0);
            } catch (SystemExitException ex) {
                object dummy;
                return ex.GetExitCode(out dummy);
            }

            return 0;
        }
#endif
#endif

        public static CodeContext GetPythonTypeContext(PythonType pt) {
            return pt.PythonContext.SharedContext;
        }

        public static Delegate GetDelegate(CodeContext/*!*/ context, object target, Type type) {
            return context.LanguageContext.DelegateCreator.GetDelegate(target, type);
        }

        public static int CompareLists(List self, List other) {
            return self.CompareTo(other);
        }

        public static int CompareTuples(PythonTuple self, PythonTuple other) {
            return self.CompareTo(other);
        }

        public static int CompareFloats(double self, double other) {
            return DoubleOps.Compare(self, other);
        }

        public static Bytes MakeBytes(byte[] bytes) {
            return new Bytes(bytes);
        }

        internal static Bytes MakeBytes(string s) => MakeBytes(MakeByteArray(s));

        public static byte[] MakeByteArray(this string s) {
            byte[] ret = new byte[s.Length];
            for (int i = 0; i < s.Length; i++) {
                if (s[i] < 0x100) ret[i] = (byte)s[i];
                else {
                    throw PythonOps.UnicodeEncodeError("ascii", s[i], i,
                        "'ascii' codec can't decode byte {0:X} in position {1}: ordinal not in range", (int)s[i], i);
                }
            }
            return ret;
        }

        public static string MakeString(this IList<byte> bytes) {
            return MakeString(bytes, bytes.Count);
        }

        internal static string MakeString(this byte[] preamble, IList<byte> bytes) {
            char[] chars = new char[preamble.Length + bytes.Count];
            for (int i = 0; i < preamble.Length; i++) {
                chars[i] = (char)preamble[i];
            }
            for (int i = 0; i < bytes.Count; i++) {
                chars[i + preamble.Length] = (char)bytes[i];
            }
            return new String(chars);
        }

        internal static string MakeString(this IList<byte> bytes, int maxBytes) {
            int bytesToCopy = Math.Min(bytes.Count, maxBytes);
            StringBuilder b = new StringBuilder(bytesToCopy);
            for (int i = 0; i < bytesToCopy; i++) {
                b.Append((char)bytes[i]);
            }
            return b.ToString();
        }

        /// <summary>
        /// Called from generated code, helper to remove a name
        /// </summary>
        public static void RemoveName(CodeContext/*!*/ context, string name) {
            if (!context.TryRemoveVariable(name)) {
                throw PythonOps.NameError(name);
            }
        }

        /// <summary>
        /// Called from generated code, helper to do name lookup
        /// </summary>
        public static object LookupName(CodeContext/*!*/ context, string name) {
            object value;
            if (context.TryLookupName(name, out value)) {
                return value;
            } else if (context.TryLookupBuiltin(name, out value)) {
                return value;
            }

            throw PythonOps.NameError(name);
        }

        /// <summary>
        /// Called from generated code, helper to do name assignment
        /// </summary>
        public static object SetName(CodeContext/*!*/ context, string name, object value) {
            context.SetVariable(name, value);
            return value;
        }

        /// <summary>
        /// Returns an IntPtr in the proper way to CPython - an int or a Python long
        /// </summary>
        public static object/*!*/ ToPython(this IntPtr handle) {
            long value = handle.ToInt64();
            if (value >= Int32.MinValue && value <= Int32.MaxValue) {
                return ScriptingRuntimeHelpers.Int32ToObject((int)value);
            }

            return (BigInteger)value;
        }

        #region Global Access

        public static CodeContext/*!*/ CreateLocalContext(CodeContext/*!*/ outerContext, MutableTuple boxes, string[] args) {
            return new CodeContext(
                new PythonDictionary(
                    new RuntimeVariablesDictionaryStorage(boxes, args)
                ),
                outerContext.ModuleContext
            );
        }

        public static CodeContext/*!*/ GetGlobalContext(CodeContext/*!*/ context) {
            return context.ModuleContext.GlobalContext;
        }

        public static ClosureCell/*!*/ MakeClosureCell() {
            return new ClosureCell(Uninitialized.Instance);
        }

        public static ClosureCell/*!*/ MakeClosureCellWithValue(object initialValue) {
            return new ClosureCell(initialValue);
        }

        public static MutableTuple/*!*/ GetClosureTupleFromFunction(PythonFunction/*!*/ function) {
            return GetClosureTupleFromContext(function.Context);
        }

        public static MutableTuple/*!*/ GetClosureTupleFromGenerator(PythonGenerator/*!*/ generator) {
            return GetClosureTupleFromContext(generator.Context);
        }

        public static MutableTuple/*!*/ GetClosureTupleFromContext(CodeContext/*!*/ context) {
            return (context.Dict._storage as RuntimeVariablesDictionaryStorage).Tuple;
        }

        public static CodeContext/*!*/ GetParentContextFromFunction(PythonFunction/*!*/ function) {
            return function.Context;
        }

        public static CodeContext/*!*/ GetParentContextFromGenerator(PythonGenerator/*!*/ generator) {
            return generator.Context;
        }

        public static object GetGlobal(CodeContext/*!*/ context, string name) {
            return GetVariable(context, name, true, false);
        }

        public static object GetLocal(CodeContext/*!*/ context, string name) {
            return GetVariable(context, name, false, false);
        }

        internal static object GetVariable(CodeContext/*!*/ context, string name, bool isGlobal, bool lightThrow) {
            object res;
            if (isGlobal) {
                if (context.TryGetGlobalVariable(name, out res)) {
                    return res;
                }
            } else {
                if (context.TryLookupName(name, out res)) {
                    return res;
                }
            }

            PythonDictionary builtins = context.GetBuiltinsDict();
            if (builtins != null && builtins.TryGetValue(name, out res)) {
                return res;
            }

            Exception ex;
            if (isGlobal) {
                ex = GlobalNameError(name);
            } else {
                ex = NameError(name);
            }
            if (lightThrow) {
                return LightExceptions.Throw(ex);
            }
            throw ex;
        }

        public static object RawGetGlobal(CodeContext/*!*/ context, string name) {
            object res;
            if (context.TryGetGlobalVariable(name, out res)) {
                return res;
            }

            return Uninitialized.Instance;
        }

        public static object RawGetLocal(CodeContext/*!*/ context, string name) {
            object res;
            if (context.TryLookupName(name, out res)) {
                return res;
            }

            return Uninitialized.Instance;
        }

        public static void SetGlobal(CodeContext/*!*/ context, string name, object value) {
            context.SetGlobalVariable(name, value);
        }

        public static void SetLocal(CodeContext/*!*/ context, string name, object value) {
            context.SetVariable(name, value);
        }

        public static void DeleteGlobal(CodeContext/*!*/ context, string name) {
            if (context.TryRemoveGlobalVariable(name)) {
                return;
            }

            throw NameError(name);
        }

        public static void DeleteLocal(CodeContext/*!*/ context, string name) {
            if (context.TryRemoveVariable(name)) {
                return;
            }

            throw NameError(name);
        }

        public static PythonGlobal/*!*/[] GetGlobalArrayFromContext(CodeContext/*!*/ context) {
            return context.GetGlobalArray();
        }

        #endregion

        #region Exception Factories

        public static Exception MultipleKeywordArgumentError(PythonFunction function, string name) {
            return TypeError("{0}() got multiple values for keyword argument '{1}'", function.__name__, name);
        }

        public static Exception UnexpectedKeywordArgumentError(PythonFunction function, string name) {
            return TypeError("{0}() got an unexpected keyword argument '{1}'", function.__name__, name);
        }

        public static Exception StaticAssignmentFromInstanceError(PropertyTracker tracker, bool isAssignment) {
            if (isAssignment) {
                if (DynamicHelpers.GetPythonTypeFromType(tracker.DeclaringType).IsPythonType) {
                    return PythonOps.TypeError(
                        "can't set attributes of built-in/extension type '{0}'",
                        NameConverter.GetTypeName(tracker.DeclaringType));
                }

                return new MissingMemberException(string.Format(
                    Resources.StaticAssignmentFromInstanceError,
                    tracker.Name,
                    NameConverter.GetTypeName(tracker.DeclaringType)));
            }
            return new MissingMemberException(string.Format(Resources.StaticAccessFromInstanceError,
                tracker.Name,
                NameConverter.GetTypeName(tracker.DeclaringType)));
        }

        public static Exception FunctionBadArgumentError(PythonFunction func, int count) {
            return func.BadArgumentError(count);
        }

        public static Exception BadKeywordArgumentError(PythonFunction func, int count) {
            return func.BadKeywordArgumentError(count);
        }

        public static Exception AttributeErrorForMissingOrReadonly(CodeContext/*!*/ context, PythonType dt, string name) {
            PythonTypeSlot dts;
            if (dt.TryResolveSlot(context, name, out dts)) {
                throw PythonOps.AttributeErrorForReadonlyAttribute(dt.Name, name);
            }

            throw PythonOps.AttributeErrorForMissingAttribute(dt.Name, name);
        }

        public static Exception AttributeErrorForMissingAttribute(object o, string name) {
            PythonType dt = o as PythonType;
            if (dt != null)
                return PythonOps.AttributeErrorForMissingAttribute(dt.Name, name);

            return AttributeErrorForReadonlyAttribute(PythonTypeOps.GetName(o), name);
        }


        public static Exception ValueError(string format, params object[] args) {
            return new ValueErrorException(string.Format(format, args));
        }

        public static Exception KeyError(object key) {
            return PythonExceptions.CreateThrowable(PythonExceptions.KeyError, key);
        }

        public static Exception KeyError(string format, params object[] args) {
            return new KeyNotFoundException(string.Format(format, args));
        }

        public static Exception UnicodeDecodeError(string format, params object[] args) {
            return new System.Text.DecoderFallbackException(string.Format(format, args));
        }

        public static Exception UnicodeDecodeError(string message, byte[] bytesUnknown, int index) {
            return new System.Text.DecoderFallbackException(message, bytesUnknown, index);
        }

        public static Exception UnicodeEncodeError(string format, params object[] args) {
            return new System.Text.EncoderFallbackException(string.Format(format, args));
        }

        public static Exception UnicodeEncodeError(string encoding, char charUnkown, int index,
            string format, params object[] args) {
            var ctor = typeof (EncoderFallbackException).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance, null, new [] { typeof(string), typeof(char), typeof(int) } , null);
            var ex = (EncoderFallbackException)ctor.Invoke(new object[] { string.Format(format, args), charUnkown, index });
            ex.Data["encoding"] = encoding;
            return ex;
        }


        public static Exception IOError(Exception inner) {
            return new System.IO.IOException(inner.Message, inner);
        }

        public static Exception IOError(string format, params object[] args) {
            return new System.IO.IOException(string.Format(format, args));
        }

        public static Exception EofError(string format, params object[] args) {
            return new System.IO.EndOfStreamException(string.Format(format, args));
        }

        public static Exception StandardError(string format, params object[] args) {
            return new SystemException(string.Format(format, args));
        }

        public static Exception ZeroDivisionError(string format, params object[] args) {
            return new DivideByZeroException(string.Format(format, args));
        }

        public static Exception SystemError(string format, params object[] args) {
            return new SystemException(string.Format(format, args));
        }

        public static Exception TypeError(string format, params object[] args) {
            return new TypeErrorException(string.Format(format, args));
        }

        public static Exception IndexError(string format, params object[] args) {
            return new System.IndexOutOfRangeException(string.Format(format, args));
        }

        public static Exception MemoryError(string format, params object[] args) {
            return new OutOfMemoryException(string.Format(format, args));
        }

        public static Exception ArithmeticError(string format, params object[] args) {
            return new ArithmeticException(string.Format(format, args));
        }

        public static Exception NotImplementedError(string format, params object[] args) {
            return new NotImplementedException(string.Format(format, args));
        }

        public static Exception AttributeError(string format, params object[] args) {
            return new MissingMemberException(string.Format(format, args));
        }

        public static Exception OverflowError(string format, params object[] args) {
            return new System.OverflowException(string.Format(format, args));
        }
        public static Exception WindowsError(string format, params object[] args) {
#if FEATURE_WIN32EXCEPTION // System.ComponentModel.Win32Exception
            return new System.ComponentModel.Win32Exception(string.Format(format, args));
#else
            return new System.SystemException(string.Format(format, args));
#endif
        }

        public static Exception SystemExit() {
            return new SystemExitException();
        }

        public static void SyntaxWarning(string message, SourceUnit sourceUnit, SourceSpan span, int errorCode) {
            PythonContext pc = (PythonContext)sourceUnit.LanguageContext;
            CodeContext context = pc.SharedContext;

            ShowWarning(context, PythonExceptions.SyntaxWarning, message, sourceUnit.Path, span.Start.Line);
        }

        public static SyntaxErrorException SyntaxError(string message, SourceUnit sourceUnit, SourceSpan span, int errorCode) {
            switch (errorCode & ErrorCodes.ErrorMask) {
                case ErrorCodes.IndentationError:
                    return new IndentationException(message, sourceUnit, span, errorCode, Severity.FatalError);

                case ErrorCodes.TabError:
                    return new TabException(message, sourceUnit, span, errorCode, Severity.FatalError);

                default:
                    var res = new SyntaxErrorException(message, sourceUnit, span, errorCode, Severity.FatalError);
                    if ((errorCode & ErrorCodes.NoCaret) != 0) {
                        res.Data[PythonContext._syntaxErrorNoCaret] = ScriptingRuntimeHelpers.True;
                    }
                    return res;
            }
        }

        public static SyntaxErrorException BadSourceError(byte badByte, SourceSpan span, string path) {
            SyntaxErrorException res = new SyntaxErrorException(
                String.Format("Non-ASCII character '\\x{0:x2}' in file {2} on line {1}, but no encoding declared; see http://www.python.org/peps/pep-0263.html for details",
                    badByte,
                    span.Start.Line,
                    path
                ),
                path,
                null,
                null,
                span,
                ErrorCodes.SyntaxError,
                Severity.FatalError
            );
            res.Data[PythonContext._syntaxErrorNoCaret] = ScriptingRuntimeHelpers.True;
            return res;
        }

        public static Exception StopIteration() {
            return StopIteration("");
        }

        public static Exception InvalidType(object o, RuntimeTypeHandle handle) {
            return PythonOps.TypeErrorForTypeMismatch(DynamicHelpers.GetPythonTypeFromType(Type.GetTypeFromHandle(handle)).Name, o);
        }

        public static Exception ZeroDivisionError() {
            return ZeroDivisionError("Attempted to divide by zero.");
        }

        // If you do "(a, b) = (1, 2, 3, 4)"
        public static Exception ValueErrorForUnpackMismatch(int left, int argcntafter, int right) {
            if (argcntafter == -1) {
                Debug.Assert(left != right);
                if (left > right)
                    return ValueError("not enough values to unpack (expected {0}, got {1})", left, right);
                else
                    return ValueError("too many values to unpack (expected {0})", left);
            }
            else {
                Debug.Assert(right < left + argcntafter);
                return ValueError("not enough values to unpack (expected at least {0}, got {1})", left + argcntafter, right);
            }
        }

        public static Exception NameError(string name) {
            return new UnboundNameException(string.Format("name '{0}' is not defined", name));
        }

        public static Exception GlobalNameError(string name) {
            return new UnboundNameException(string.Format("global name '{0}' is not defined", name));
        }

        // If an unbound method is called without a "self" argument, or a "self" argument of a bad type
        public static Exception TypeErrorForUnboundMethodCall(string methodName, Type methodType, object instance) {
            return TypeErrorForUnboundMethodCall(methodName, DynamicHelpers.GetPythonTypeFromType(methodType), instance);
        }

        public static Exception TypeErrorForUnboundMethodCall(string methodName, PythonType methodType, object instance) {
            string message = string.Format("unbound method {0}() must be called with {1} instance as first argument (got {2} instead)",
                                           methodName, methodType.Name, PythonTypeOps.GetName(instance));
            return TypeError(message);
        }

        // When a generator first starts, before it gets to the first yield point, you can't call generator.Send(x) where x != null.
        // See Pep342 for details.
        public static Exception TypeErrorForIllegalSend() {
            string message = "can't send non-None value to a just-started generator";
            return TypeError(message);
        }

        // If a method is called with an incorrect number of arguments
        // You should use TypeErrorForUnboundMethodCall() for unbound methods called with 0 arguments
        public static Exception TypeErrorForArgumentCountMismatch(string methodName, int expectedArgCount, int actualArgCount) {
            return TypeError("{0}() takes exactly {1} argument{2} ({3} given)",
                             methodName, expectedArgCount, expectedArgCount == 1 ? "" : "s", actualArgCount);
        }

        public static Exception TypeErrorForTypeMismatch(string expectedTypeName, object instance) {
            return TypeError("expected {0}, got {1}", expectedTypeName, PythonOps.GetPythonTypeName(instance));
        }

        // If hash is called on an instance of an unhashable type
        public static Exception TypeErrorForUnhashableType(string typeName) {
            return TypeError(typeName + " objects are unhashable");
        }

        public static Exception TypeErrorForUnhashableObject(object obj) {
            return TypeErrorForUnhashableType(PythonTypeOps.GetName(obj));
        }

        internal static Exception TypeErrorForIncompatibleObjectLayout(string prefix, PythonType type, Type newType) {
            return TypeError("{0}: '{1}' object layout differs from '{2}'", prefix, type.Name, newType);
        }

        public static Exception TypeErrorForNonStringAttribute() {
            return TypeError("attribute name must be string");
        }

        internal static Exception TypeErrorForBadInstance(string template, object instance) {
            return TypeError(template, PythonOps.GetPythonTypeName(instance));
        }

        public static Exception TypeErrorForBinaryOp(string opSymbol, object x, object y) {
            throw PythonOps.TypeError("unsupported operand type(s) for {0}: '{1}' and '{2}'",
                                opSymbol, GetPythonTypeName(x), GetPythonTypeName(y));
        }

        public static Exception TypeErrorForUnaryOp(string opSymbol, object x) {
            throw PythonOps.TypeError("unsupported operand type for {0}: '{1}'",
                                opSymbol, GetPythonTypeName(x));
        }

        public static Exception TypeErrorForNonIterableObject(object o) {
            return PythonOps.TypeError(
                "argument of type '{0}' is not iterable",
                PythonTypeOps.GetName(o)
            );
        }

        public static Exception TypeErrorForDefaultArgument(string message) {
            return PythonOps.TypeError(message);
        }

        public static Exception AttributeErrorForReadonlyAttribute(string typeName, string attributeName) {
            // CPython uses AttributeError for all attributes except "__class__"
            if (attributeName == "__class__")
                return PythonOps.TypeError("can't delete __class__ attribute");

            return PythonOps.AttributeError("'{1}' object attribute '{0}' is read-only", attributeName, typeName);
        }

        public static Exception AttributeErrorForBuiltinAttributeDeletion(string typeName, string attributeName) {
            return PythonOps.AttributeError("cannot delete attribute '{0}' of builtin type '{1}'", attributeName, typeName);
        }

        public static Exception MissingInvokeMethodException(object o, string name) {
            throw PythonOps.AttributeError("'{0}' object has no attribute '{1}'", GetPythonTypeName(o), name);
        }

        /// <summary>
        /// Create at TypeError exception for when Raise() can't create the exception requested.  
        /// </summary>
        /// <param name="type">original type of exception requested</param>
        /// <returns>a TypeEror exception</returns>
        internal static Exception MakeExceptionTypeError(object type) {
            return PythonOps.TypeError("exceptions must be classes, or instances, not {0}", PythonTypeOps.GetName(type));
        }

        public static Exception AttributeErrorForObjectMissingAttribute(object obj, string attributeName) {
            return AttributeErrorForMissingAttribute(PythonTypeOps.GetName(obj), attributeName);
        }

        public static Exception AttributeErrorForMissingAttribute(string typeName, string attributeName) {
            return PythonOps.AttributeError("'{0}' object has no attribute '{1}'", typeName, attributeName);
        }

        public static Exception AttributeErrorForOldInstanceMissingAttribute(string typeName, string attributeName) {
            return PythonOps.AttributeError("{0} instance has no attribute '{1}'", typeName, attributeName);
        }

        public static Exception AttributeErrorForOldClassMissingAttribute(string typeName, string attributeName) {
            return PythonOps.AttributeError("class {0} has no attribute '{1}'", typeName, attributeName);
        }

        public static Exception UncallableError(object func) {
            return PythonOps.TypeError("{0} is not callable", PythonTypeOps.GetName(func));
        }

        public static Exception TypeErrorForProtectedMember(Type/*!*/ type, string/*!*/ name) {
            return PythonOps.TypeError("cannot access protected member {0} without a python subclass of {1}", name, NameConverter.GetTypeName(type));
        }

        public static Exception TypeErrorForGenericMethod(Type/*!*/ type, string/*!*/ name) {
            return PythonOps.TypeError("{0}.{1} is a generic method and must be indexed with types before calling", NameConverter.GetTypeName(type), name);
        }

        public static Exception TypeErrorForUnIndexableObject(object o) {
            IPythonObject ipo;
            if (o == null) {
                return PythonOps.TypeError("'NoneType' object cannot be interpreted as an index");
            } else if ((ipo = o as IPythonObject) != null) {
                return TypeError("'{0}' object cannot be interpreted as an index", ipo.PythonType.Name);
            }

            return TypeError("object cannot be interpreted as an index");
        }

        [Obsolete("no longer used anywhere")]
        public static Exception/*!*/ TypeErrorForBadDictionaryArgument(PythonFunction/*!*/ f) {
            return PythonOps.TypeError("{0}() argument after ** must be a dictionary", f.__name__);
        }

        public static T TypeErrorForBadEnumConversion<T>(object value) {
            throw TypeError("Cannot convert numeric value {0} to {1}.  The value must be zero.", value, NameConverter.GetTypeName(typeof(T)));
        }

        public static Exception/*!*/ UnreadableProperty() {
            return PythonOps.AttributeError("unreadable attribute");
        }


        public static Exception/*!*/ UnsetableProperty() {
            return PythonOps.AttributeError("readonly attribute");
        }

        public static Exception/*!*/ UndeletableProperty() {
            return PythonOps.AttributeError("undeletable attribute");
        }

        public static Exception Warning(string format, params object[] args) {
            return new WarningException(string.Format(format, args));
        }

        #endregion

        [ThreadStatic]
        private static List<FunctionStack> _funcStack;

        public static List<FunctionStack> GetFunctionStack() {
            return _funcStack ?? (_funcStack = new List<FunctionStack>());
        }

        public static List<FunctionStack> GetFunctionStackNoCreate() {
            return _funcStack;
        }

        public static List<FunctionStack> PushFrame(CodeContext/*!*/ context, FunctionCode function) {
            List<FunctionStack> stack = GetFunctionStack();
            stack.Add(new FunctionStack(context, function));
            return stack;
        }

        internal static LightLambdaExpression ToGenerator(this LightLambdaExpression code, bool shouldInterpret, bool debuggable, int compilationThreshold) {
            return Utils.LightLambda(
                typeof(object),
                code.Type,
                new GeneratorRewriter(code.Name, code.Body).Reduce(shouldInterpret, debuggable, compilationThreshold, code.Parameters, x => x),
                code.Name,
                code.Parameters
            );
        }

        public static void UpdateStackTrace(Exception e, CodeContext context, FunctionCode funcCode, int line) {
            if (line != -1) {
                Debug.Assert(line != SourceLocation.None.Line);

                List<DynamicStackFrame> pyFrames = e.GetFrameList();
                
                if (pyFrames == null) {
                    e.SetFrameList(pyFrames = new List<DynamicStackFrame>());
                }
                
                var frame = new PythonDynamicStackFrame(context, funcCode, line);
                funcCode.LightThrowCompile(context);
                pyFrames.Add(frame);
            }
        }

        /// <summary>
        /// Gets a list of DynamicStackFrames for the given exception.  These stack frames
        /// can be programmatically inspected to understand the frames the exception crossed
        /// through including Python frames.
        /// 
        /// Dynamic stack frames are not preserved when an exception crosses an app domain
        /// boundary.
        /// </summary>
        public static DynamicStackFrame[] GetDynamicStackFrames(Exception e) {
            return PythonExceptions.GetDynamicStackFrames(e);
        }

        public static byte[] ConvertBufferToByteArray(PythonBuffer buffer) {
            return buffer.ToString().MakeByteArray();
        }

        public static bool ModuleTryGetMember(CodeContext/*!*/ context, PythonModule module, string name, out object res) {
            object value = module.GetAttributeNoThrow(context, name);
            if (value != OperationFailed.Value) {
                res = value;
                return true;
            }

            res = null;
            return false;
        }

        internal static void ScopeSetMember(CodeContext/*!*/ context, Scope scope, string name, object value) {
            ScopeStorage scopeStorage = ((object)scope.Storage) as ScopeStorage;
            if (scopeStorage != null) {
                scopeStorage.SetValue(name, false, value);
                return;
            }

            PythonOps.SetAttr(context, scope, name, value);
        }

        internal static object ScopeGetMember(CodeContext/*!*/ context, Scope scope, string name) {
            ScopeStorage scopeStorage = ((object)scope.Storage) as ScopeStorage;
            if (scopeStorage != null) {
                return scopeStorage.GetValue(name, false);
            }

            return PythonOps.GetBoundAttr(context, scope, name);
        }

        internal static bool ScopeTryGetMember(CodeContext/*!*/ context, Scope scope, string name, out object value) {
            ScopeStorage scopeStorage = ((object)scope.Storage) as ScopeStorage;
            if (scopeStorage != null) {
                return scopeStorage.TryGetValue(name, false, out value);
            }

            return PythonOps.TryGetBoundAttr(context, scope, name, out value);
        }

        internal static bool ScopeContainsMember(CodeContext/*!*/ context, Scope scope, string name) {
            ScopeStorage scopeStorage = ((object)scope.Storage) as ScopeStorage;
            if (scopeStorage != null) {
                return scopeStorage.HasValue(name, false);
            }

            return PythonOps.HasAttr(context, scope, name);
        }

        internal static bool ScopeDeleteMember(CodeContext/*!*/ context, Scope scope, string name) {
            ScopeStorage scopeStorage = ((object)scope.Storage) as ScopeStorage;
            if (scopeStorage != null) {
                return scopeStorage.DeleteValue(name, false);
            }

            bool res = PythonOps.HasAttr(context, scope, name);
            PythonOps.DeleteAttr(context, scope, name);
            return res;
        }

        internal static IList<object> ScopeGetMemberNames(CodeContext/*!*/ context, Scope scope) {
            ScopeStorage scopeStorage = ((object)scope.Storage) as ScopeStorage;
            if (scopeStorage != null) {
                List<object> res = new List<object>();

                foreach (string name in scopeStorage.GetMemberNames()) {
                    res.Add(name);
                }

                var objKeys = ((PythonScopeExtension)context.LanguageContext.EnsureScopeExtension(scope)).ObjectKeys;
                if (objKeys != null) {
                    foreach (object o in objKeys.Keys) {
                        res.Add(o);
                    }
                }

                return res;
            }

            return PythonOps.GetAttrNames(context, scope);
        }

        public static bool IsUnicode(object unicodeObj) {
            return unicodeObj == TypeCache.String;
        }

        public static BuiltinFunction GetUnicodeFuntion() {
            return UnicodeHelper.Function;
        }

        public static bool IsExtensionSet(CodeContext codeContext, int id) {
            return codeContext.ModuleContext.ExtensionMethods.Id == id;
        }

        public static object GetExtensionMethodSet(CodeContext context) {
            return context.ModuleContext.ExtensionMethods;
        }
    }

    /// <summary>
    /// Helper clas for calls to unicode(...).  We generate code which checks if unicode
    /// is str and if it is we redirect those calls to the unicode function defined on this
    /// class.
    /// </summary>
    public class UnicodeHelper {
        internal static BuiltinFunction Function = BuiltinFunction.MakeFunction("unicode",
            ArrayUtils.ConvertAll(
                typeof(UnicodeHelper).GetMember("unicode"),
                x => (MethodInfo)x
            ),
            typeof(string)
        );

        public static object unicode(CodeContext context) {
            return String.Empty;
        }

        public static object unicode(CodeContext/*!*/ context, object @string) {
            return StringOps.FastNewUnicode(context, @string);
        }

        public static object unicode(CodeContext/*!*/ context, object @string, object encoding) {
            return StringOps.FastNewUnicode(context, @string, encoding);
        }

        public static object unicode(CodeContext/*!*/ context, object @string, [Optional]object encoding, object errors) {
            return StringOps.FastNewUnicode(context, @string, encoding, errors);
        }
    }

    public struct FunctionStack {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public readonly CodeContext/*!*/ Context;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public readonly FunctionCode/*!*/ Code;
        public TraceBackFrame Frame;

        internal FunctionStack(CodeContext/*!*/ context, FunctionCode/*!*/ code) {
            Assert.NotNull(context, code);

            Context = context;
            Code = code;
            Frame = null;
        }

        internal FunctionStack(CodeContext/*!*/ context, FunctionCode/*!*/ code, TraceBackFrame frame) {
            Assert.NotNull(context, code);

            Context = context;
            Code = code;
            Frame = frame;
        }

        internal FunctionStack(TraceBackFrame frame) {
            Context = frame.Context;
            Code = frame.f_code;
            Frame = frame;
        }
    }

}
