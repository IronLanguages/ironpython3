// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Ast;
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

using NotNullAttribute = System.Diagnostics.CodeAnalysis.NotNullAttribute;

namespace IronPython.Runtime.Operations {

    internal class ExceptionState {
        public Exception? Exception { get; set; }
        public ExceptionState? PrevException { get; set; }
    }

    /// <summary>
    /// Contains functions that are called directly from
    /// generated code to perform low-level runtime functionality.
    /// </summary>
    public static partial class PythonOps {
        #region Shared static data

        [ThreadStatic]
        private static List<object>? InfiniteRepr;

        // The "current" exception on this thread that will be returned via sys.exc_info()
        [ThreadStatic]
        internal static ExceptionState? CurrentExceptionState;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly PythonTuple EmptyTuple = PythonTuple.EMPTY;
        private static readonly Type[] _DelegateCtorSignature = new Type[] { typeof(object), typeof(IntPtr) };

        #endregion

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static PythonDictionary MakeEmptyDict() {
            return new PythonDictionary();
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

        public static bool IsCallable(CodeContext/*!*/ context, [NotNullWhen(true)]object? o) {
            // This tells if an object can be called, but does not make a claim about the parameter list.
            // In 1.x, we could check for certain interfaces like ICallable*, but those interfaces were deprecated
            // in favor of dynamic sites. 
            // This is difficult to infer because we'd need to simulate the entire callbinder, which can include
            // looking for [SpecialName] call methods and checking for a rule from IDynamicMetaObjectProvider. But even that wouldn't
            // be complete since sites require the argument list of the call, and we only have the instance here. 
            // Thus check a dedicated IsCallable operator. This lets each object describe if it's callable.

            // Invoke Operator.IsCallable on the object. 
            return context.LanguageContext.IsCallable(o);
        }

        public static bool UserObjectIsCallable(CodeContext/*!*/ context, object o) {
            return TryGetBoundAttr(context, o, "__call__", out object? callFunc) && callFunc != null;
        }

        public static bool IsTrue(object? o) {
            return Converter.ConvertToBoolean(o);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1002:DoNotExposeGenericLists")]
        internal static List<object> GetReprInfinite() {
            if (InfiniteRepr == null) {
                InfiniteRepr = new List<object>();
            }
            return InfiniteRepr;
        }

        [LightThrowing]
        internal static object LookupEncodingError(CodeContext/*!*/ context, string name) {
            Dictionary<string, object> errorHandlers = context.LanguageContext.ErrorHandlers;
            lock (errorHandlers) {
                if (errorHandlers.ContainsKey(name))
                    return errorHandlers[name];
                else
                    return LightExceptions.Throw(PythonOps.LookupError("unknown error handler name '{0}'", name));
            }
        }

        internal static void RegisterEncodingError(CodeContext/*!*/ context, string name, object? handler) {
            Dictionary<string, object> errorHandlers = context.LanguageContext.ErrorHandlers;

            lock (errorHandlers) {
                if (!PythonOps.IsCallable(context, handler))
                    throw PythonOps.TypeError("handler must be callable");

                errorHandlers[name] = handler;
            }
        }

        internal static PythonTuple LookupEncoding(CodeContext/*!*/ context, string encoding) {
            if (encoding.IndexOf('\0') != -1) {
                throw PythonOps.TypeError("lookup string cannot contain null character");
            }
            //compute encoding.ToLower().Replace(' ', '-') but ToLower only on ASCII letters
            var sb = new StringBuilder(encoding.Length);
            foreach (var c in encoding) {
                if (c == ' ') sb.Append('-');
                else if (c < 0x80) sb.Append(char.ToLowerInvariant(c));
                else sb.Append(c);
            }
            string normalized = sb.ToString();

            context.LanguageContext.EnsureEncodings();
            List<object> searchFunctions = context.LanguageContext.SearchFunctions;
            lock (searchFunctions) {
                for (int i = 0; i < searchFunctions.Count; i++) {
                    object? res = PythonCalls.Call(context, searchFunctions[i], normalized);
                    if (res != null) {
                        if (res is PythonTuple pt && pt.__len__() == 4) {
                            return pt;
                        } else {
                            throw PythonOps.TypeError("codec search functions must return 4-tuples");
                        }
                    }
                }
            }

            throw PythonOps.LookupError("unknown encoding: {0}", encoding);
        }

        internal static PythonTuple LookupTextEncoding(CodeContext/*!*/ context, string encoding, string alternateCommand) {
            var tuple = LookupEncoding(context, encoding);
            if (TryGetBoundAttr(tuple, "_is_text_encoding", out object? isTextEncodingObj)
                    && isTextEncodingObj is bool isTextEncoding && !isTextEncoding) {
                throw LookupError("'{0}' is not a text encoding; use {1} to handle arbitrary codecs", encoding, alternateCommand);
            }
            return tuple;
        }

        internal static void RegisterEncoding(CodeContext/*!*/ context, object? search_function) {
            if (!PythonOps.IsCallable(context, search_function))
                throw PythonOps.TypeError("search_function must be callable");

            List<object> searchFunctions = context.LanguageContext.SearchFunctions;

            lock (searchFunctions) {
                searchFunctions.Add(search_function);
            }
        }

        internal static string GetPythonTypeName(object? obj) {
            return PythonTypeOps.GetName(obj);
        }

        public static string Ascii(CodeContext/*!*/ context, object? o) {
            return StringOps.AsciiEncode(Repr(context, o));
        }

        public static string Repr(CodeContext/*!*/ context, object? o) {
            if (o == null) return "None";

            if (o is string s) return StringOps.__repr__(s);
            if (o is int) return Int32Ops.__repr__((int)o);
            if (o is long) return ((long)o).ToString();

            // could be a container object, we need to detect recursion, but only
            // for our own built-in types that we're aware of.  The user can setup
            // infinite recursion in their own class if they want.
            if (o is ICodeFormattable f) {
                if (o is PythonExceptions.BaseException) {
                    Debug.Assert(typeof(PythonExceptions.BaseException).IsDefined(typeof(DynamicBaseTypeAttribute), false));
                    // let it fall through to InvokeUnaryOperator, resolves the following:
                    // class MyException(Exception):
                    //     def __repr__(self): return "qwerty"
                    //
                    // assert repr(MyException) == "qwerty"
                } else {
                    return f.__repr__(context);
                }
            }

            PerfTrack.NoteEvent(PerfTrack.Categories.Temporary, "Repr " + o.GetType().FullName);

            object? repr = PythonContext.InvokeUnaryOperator(context, UnaryOperators.Repr, o);

            if (repr is string strRepr) return strRepr;
            if (repr is Extensible<string> esRepr) return esRepr;

            throw PythonOps.TypeError("__repr__ returned non-string (got '{0}' from type '{1}')", PythonOps.GetPythonTypeName(repr), PythonOps.GetPythonTypeName(o));
        }

        public static List<object>? GetAndCheckInfinite(object o) {
            List<object> infinite = GetReprInfinite();
            foreach (object o2 in infinite) {
                if (o == o2) {
                    return null;
                }
            }
            return infinite;
        }

        public static string ToString(object? o) {
            return ToString(DefaultContext.Default, o);
        }

        public static string ToString(CodeContext/*!*/ context, object? o) {
            if (o is string x) return x;
            if (o is null) return "None";
            if (o is double) return DoubleOps.__str__(context, (double)o);
            if (o is PythonType dt) return dt.__repr__(DefaultContext.Default);
            if (o.GetType() == typeof(object).Assembly.GetType("System.__ComObject")) return ComOps.__repr__(o);

            object value = PythonContext.InvokeUnaryOperator(context, UnaryOperators.String, o);
            if (!(value is string ret)) {
                if (!(value is Extensible<string> es)) {
                    throw PythonOps.TypeError("expected str, got {0} from __str__", PythonTypeOps.GetName(value));
                }

                ret = es.Value;
            }

            return ret;
        }

        public static string FormatString(CodeContext/*!*/ context, string str, object data) {
            return new StringFormatter(context, str, data).Format();
        }

        public static object Plus(object o) {
            if (o is int) return o;
            else if (o is double) return o;
            else if (o is BigInteger) return o;
            else if (o is Complex) return o;
            else if (o is long) return o;
            else if (o is float) return o;
            else if (o is bool) return ScriptingRuntimeHelpers.Int32ToObject((bool)o ? 1 : 0);

            if (PythonTypeOps.TryInvokeUnaryOperator(DefaultContext.Default, o, "__pos__", out object ret) &&
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

            if (PythonTypeOps.TryInvokeUnaryOperator(DefaultContext.Default, o, "__neg__", out object ret) &&
                ret != NotImplementedType.Value) {
                return ret;
            }

            throw PythonOps.TypeError("bad operand type for unary -");
        }

        internal static bool IsSubClass(PythonType/*!*/ c, PythonType/*!*/ typeinfo) {
            Assert.NotNull(c, typeinfo);

            return typeinfo.__subclasscheck__(c);
        }

        internal static bool IsSubClass(CodeContext/*!*/ context, PythonType c, [NotNull]object? typeinfo) {
            if (c == null) throw PythonOps.TypeError("issubclass: arg 1 must be a class");
            if (typeinfo == null) throw PythonOps.TypeError("issubclass: arg 2 must be a class");

            PythonContext pyContext = context.LanguageContext;
            if (typeinfo is PythonTuple pt) {
                // Recursively inspect nested tuple(s)
                foreach (object? o in pt) {
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

            if (typeinfo is Type t) {
                typeinfo = DynamicHelpers.GetPythonTypeFromType(t);
            }

            if (!(typeinfo is PythonType dt)) {
                if (!PythonOps.TryGetBoundAttr(typeinfo, "__bases__", out object? bases)) {
                    //!!! deal with classes w/ just __bases__ defined.
                    throw PythonOps.TypeErrorForBadInstance("issubclass(): {0} is not a class nor a tuple of classes", typeinfo);
                }

                IEnumerator ie = PythonOps.GetEnumerator(bases);
                while (ie.MoveNext()) {
                    if (!(ie.Current is PythonType baseType)) continue;
                    if (c.IsSubclassOf(baseType)) return true;
                }
                return false;
            }

            return IsSubClass(c, dt);
        }

        internal static bool IsInstance(object? o, PythonType typeinfo) {
            if (typeinfo.__instancecheck__(o)) {
                return true;
            }

            return IsInstanceDynamic(o, typeinfo, DynamicHelpers.GetPythonType(o));
        }

        internal static bool IsInstance(CodeContext/*!*/ context, object? o, PythonTuple typeinfo) {
            PythonContext pyContext = context.LanguageContext;
            foreach (object? type in typeinfo) {
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

        internal static bool IsInstance(CodeContext/*!*/ context, object? o, [NotNull]object? typeinfo) {
            if (typeinfo == null) throw PythonOps.TypeError("isinstance: arg 2 must be a class, type, or tuple of classes and types");

            if (typeinfo is PythonTuple tt) {
                return IsInstance(context, o, tt);
            }

            PythonType odt = DynamicHelpers.GetPythonType(o);
            if (IsSubClass(context, odt, typeinfo)) {
                return true;
            }

            return IsInstanceDynamic(o, typeinfo);
        }

        private static bool IsInstanceDynamic(object? o, object typeinfo) {
            return IsInstanceDynamic(o, typeinfo, DynamicHelpers.GetPythonType(o));
        }

        private static bool IsInstanceDynamic(object? o, object typeinfo, PythonType odt) {
            if (o is IPythonObject) {
                if (PythonOps.TryGetBoundAttr(o, "__class__", out object? cls) &&
                    (!object.ReferenceEquals(odt, cls))) {
                    return IsSubclassSlow(cls, typeinfo);
                }
            }
            return false;
        }

        private static bool IsSubclassSlow([NotNullWhen(true)]object? cls, object typeinfo) {
            if (cls == null) return false;

            // Same type
            if (cls.Equals(typeinfo)) {
                return true;
            }

            // Get bases
            if (!PythonOps.TryGetBoundAttr(cls, "__bases__", out object? bases)) {
                return false;   // no bases, cannot be subclass
            }

            if (!(bases is PythonTuple tbases)) {
                return false;   // not a tuple, cannot be subclass
            }

            foreach (object? baseclass in tbases) {
                if (IsSubclassSlow(baseclass, typeinfo)) return true;
            }

            return false;
        }

        public static object OnesComplement(object o) {
            if (o is int) return ~(int)o;
            if (o is long) return ~(long)o;
            if (o is BigInteger) return ~((BigInteger)o);
            if (o is bool) return ScriptingRuntimeHelpers.Int32ToObject((bool)o ? -2 : -1);

            if (PythonTypeOps.TryInvokeUnaryOperator(DefaultContext.Default, o, "__invert__", out object ret) &&
                ret != NotImplementedType.Value)
                return ret;


            throw PythonOps.TypeError("bad operand type for unary ~");
        }

        public static bool Not(object? o) {
            return !IsTrue(o);
        }

        public static object Is(object? x, object? y) {
            return IsRetBool(x, y) ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
        }

        public static bool IsRetBool(object? x, object? y) {
            if (x == y)
                return true;

            // Special case "is True"/"is False" checks. They are somewhat common in 
            // Python (particularly in tests), but non-Python code may not stick to the
            // convention of only using the two singleton instances at ScriptingRuntimeHelpers.
            // (https://github.com/IronLanguages/main/issues/1299)
            if (x is bool xb)
                return xb == (y as bool?);

            return false;
        }

        public static object IsNot(object? x, object? y) {
            return IsRetBool(x, y) ? ScriptingRuntimeHelpers.False : ScriptingRuntimeHelpers.True;
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
        internal static object MultiplySequence<T>(MultiplySequenceWorker<T> multiplier, T sequence, Index count, bool isForward) where T : notnull {
            if (isForward) {
                if (PythonTypeOps.TryInvokeBinaryOperator(DefaultContext.Default, count.Value, sequence, "__rmul__", out object ret)) {
                    if (ret != NotImplementedType.Value) return ret;
                }
            }

            int icount = GetSequenceMultiplier(sequence, count.Value);

            if (icount < 0) icount = 0;
            return multiplier(sequence, icount);
        }

        internal static int GetSequenceMultiplier(object sequence, object count) {
            if (!Converter.TryConvertToIndex(count, out int icount)) {
                throw TypeError("can't multiply sequence by non-int of type '{0}'", PythonTypeOps.GetName(count));
            }
            return icount;
        }

        public static object Equal(CodeContext/*!*/ context, object? x, object? y) {
            PythonContext pc = context.LanguageContext;
            return pc.EqualSite.Target(pc.EqualSite, x, y);
        }

        public static bool EqualRetBool(object? x, object? y) {
            //TODO just can't seem to shake these fast paths
            if (x is int && y is int) { return ((int)x) == ((int)y); }
            if (x is string && y is string) { return ((string)x).Equals((string)y); }

            return DynamicHelpers.GetPythonType(x).EqualRetBool(x, y);
        }

        public static bool EqualRetBool(CodeContext/*!*/ context, object? x, object? y) {
            // TODO: use context

            //TODO just can't seem to shake these fast paths
            if (x is int && y is int) { return ((int)x) == ((int)y); }
            if (x is string && y is string) { return ((string)x).Equals((string)y); }

            return DynamicHelpers.GetPythonType(x).EqualRetBool(x, y);
        }

        internal static bool IsOrEqualsRetBool(object? x, object? y) => ReferenceEquals(x, y) || EqualRetBool(x, y);

        internal static bool IsOrEqualsRetBool(CodeContext/*!*/ context, object? x, object? y) => ReferenceEquals(x, y) || EqualRetBool(context, x, y);

        public static int Compare(object? x, object? y) {
            return Compare(DefaultContext.Default, x, y);
        }

        public static int Compare(CodeContext/*!*/ context, object? x, object? y) {
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

        public static bool CompareTypesEqual(CodeContext/*!*/ context, object? x, object? y) {
            if (x == null && y == null) return true;
            if (x == null) return false;
            if (y == null) return false;

            if (DynamicHelpers.GetPythonType(x) == DynamicHelpers.GetPythonType(y)) {
                // avoid going to the ID dispenser if we have the same types...
                return x == y;
            }

            return PythonOps.CompareTypesWorker(context, false, x, y) == 0;
        }

        public static bool CompareTypesNotEqual(CodeContext/*!*/ context, object? x, object? y) {
            return PythonOps.CompareTypesWorker(context, false, x, y) != 0;
        }

        private static int CompareTypesWorker(CodeContext/*!*/ context, bool shouldWarn, object? x, object? y) {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            int diff;

            if (DynamicHelpers.GetPythonType(x) != DynamicHelpers.GetPythonType(y)) {
                string name1 = PythonTypeOps.GetName(x);
                string name2 = PythonTypeOps.GetName(y);
                diff = string.CompareOrdinal(name1, name2);
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

        public static int CompareTypes(CodeContext/*!*/ context, object? x, object? y) {
            return CompareTypesWorker(context, true, x, y);
        }

        public static object GreaterThanHelper(CodeContext/*!*/ context, object? self, object? other) {
            return InternalCompare(context, PythonOperationKind.GreaterThan, self, other);
        }

        public static object LessThanHelper(CodeContext/*!*/ context, object? self, object? other) {
            return InternalCompare(context, PythonOperationKind.LessThan, self, other);
        }

        public static object GreaterThanOrEqualHelper(CodeContext/*!*/ context, object? self, object? other) {
            return InternalCompare(context, PythonOperationKind.GreaterThanOrEqual, self, other);
        }

        public static object LessThanOrEqualHelper(CodeContext/*!*/ context, object? self, object? other) {
            return InternalCompare(context, PythonOperationKind.LessThanOrEqual, self, other);
        }

        internal static object InternalCompare(CodeContext/*!*/ context, PythonOperationKind op, object? self, object? other) {
            if (PythonTypeOps.TryInvokeBinaryOperator(context, self, other, Symbols.OperatorToSymbol(op), out object ret))
                return ret;

            return NotImplementedType.Value;
        }

        public static int CompareToZero(object? value) {
            if (Converter.TryConvertToDouble(value, out double val)) {
                if (val > 0) return 1;
                if (val < 0) return -1;
                return 0;
            }
            throw PythonOps.TypeErrorForBadInstance("an integer is required (got {0})", value);
        }

        internal static int CompareArrays(object?[] data0, int size0, object?[] data1, int size1) {
            int size = Math.Min(size0, size1);
            for (int i = 0; i < size; i++) {
                int c = PythonOps.Compare(data0[i], data1[i]);
                if (c != 0) return c;
            }
            if (size0 == size1) return 0;
            return size0 > size1 ? +1 : -1;
        }

        internal static int CompareArrays(object?[] data0, int size0, object?[] data1, int size1, IComparer comparer) {
            int size = Math.Min(size0, size1);
            for (int i = 0; i < size; i++) {
                int c = comparer.Compare(data0[i], data1[i]);
                if (c != 0) return c;
            }
            if (size0 == size1) return 0;
            return size0 > size1 ? +1 : -1;
        }

        internal static bool ArraysEqual(object?[] data0, int size0, object?[] data1, int size1) {
            if (size0 != size1) {
                return false;
            }
            for (int i = 0; i < size0; i++) {
                if (!IsOrEqualsRetBool(data0[i], data1[i])) {
                    return false;
                }
            }
            return true;
        }

        internal static bool ArraysEqual(object?[] data0, int size0, object?[] data1, int size1, IEqualityComparer comparer) {
            if (size0 != size1) {
                return false;
            }
            for (int i = 0; i < size0; i++) {
                var d0 = data0[i];
                var d1 = data1[i];
                if (!ReferenceEquals(d0, d1) && !comparer.Equals(d0, d1)) {
                    return false;
                }
            }
            return true;
        }

        public static object PowerMod(CodeContext/*!*/ context, object? x, object? y, object? z) {
            object ret;
            if (z == null) {
                return context.LanguageContext.Operation(PythonOperationKind.Power, x, y);
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

        public static long Id(object? o) {
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

        public static object Index(object? o) {
            if (TryToIndex(o, out object? index)) return index;
            throw TypeError("'{0}' object cannot be interpreted as an integer", PythonTypeOps.GetName(o));
        }

        internal static bool TryToIndex(object? o, [NotNullWhen(true)]out object? index) {
            switch (o) {
                case int i:
                    index = Int32Ops.__index__(i);
                    return true;
                case uint ui:
                    index = UInt32Ops.__index__(ui);
                    return true;
                case ushort us:
                    index = UInt16Ops.__index__(us);
                    return true;
                case short s:
                    index = Int16Ops.__index__(s);
                    return true;
                case byte b:
                    index = ByteOps.__index__(b);
                    return true;
                case sbyte sb:
                    index = SByteOps.__index__(sb);
                    return true;
                case long l:
                    index = Int64Ops.__index__(l);
                    return true;
                case ulong ul:
                    index = UInt64Ops.__index__(ul);
                    return true;
                case BigInteger bi:
                    index = BigIntegerOps.__index__(bi);
                    return true;
                default:
                    break;
            }

            if (PythonTypeOps.TryInvokeUnaryOperator(DefaultContext.Default, o, "__index__", out index)) {
                if (!(index is int) && !(index is BigInteger))
                    throw TypeError("__index__ returned non-int (type {0})", PythonTypeOps.GetName(index));
                return true;
            }

            index = default;
            return false;
        }

        private static bool ObjectToInt(object o, out int res, out BigInteger longRes) {
            if (o is BigInteger bi) {
                if (!bi.AsInt32(out res)) {
                    longRes = bi;
                    return false;
                }
            } else if (o is int i) {
                res = i;
            } else {
                res = Converter.ConvertToInt32(o);
            }

            longRes = default;
            return true;
        }

        internal static bool Length(object? o, out int res, out BigInteger bigRes) {
            if (o is string s) {
                res = s.Length;
                bigRes = default;
                return true;
            }

            if (o is object[] os) {
                res = os.Length;
                bigRes = default;
                return true;
            }

            object len = PythonContext.InvokeUnaryOperator(DefaultContext.Default, UnaryOperators.Length, o, $"object of type '{GetPythonTypeName(o)}' has no len()");

            if (ObjectToInt(len, out res, out bigRes)) {
                if (res < 0) throw ValueError("__len__() should return >= 0");
                return true;
            } else {
                if (bigRes < 0) throw ValueError("__len__() should return >= 0");
                return false;
            }
        }

        public static int Length(object? o) {
            if (Length(o, out int res, out _)) {
                return res;
            }
            throw new OverflowException();
        }

        internal static bool TryInvokeLengthHint(CodeContext context, object? sequence, out int hint) {
            if (PythonTypeOps.TryInvokeUnaryOperator(context, sequence, "__len__", out object len_obj) ||
                PythonTypeOps.TryInvokeUnaryOperator(context, sequence, "__length_hint__", out len_obj)) {
                if (!(len_obj is NotImplementedType)) {
                    hint = Converter.ConvertToInt32(len_obj);
                    return true;
                }
            }
            hint = 0;
            return false;
        }

        public static object? CallWithContext(CodeContext/*!*/ context, [NotNull]object? func, params object?[] args) {
            return PythonCalls.Call(context, func, args);
        }

        /// <summary>
        /// Supports calling of functions that require an explicit 'this'
        /// Currently, we check if the function object implements the interface 
        /// that supports calling with 'this'. If not, the 'this' object is dropped
        /// and a normal call is made.
        /// </summary>
        public static object? CallWithContextAndThis(CodeContext/*!*/ context, [NotNull]object? func, object? instance, params object?[] args) {
            // drop the 'this' and make the call
            return CallWithContext(context, func, args);
        }

        [Obsolete("Use ObjectOpertaions instead")]
        public static object? CallWithArgsTupleAndKeywordDictAndContext(CodeContext/*!*/ context, object func, object[] args, string[] names, object argsTuple, object kwDict) {
            IDictionary? kws = kwDict as IDictionary;
            if (kws == null && kwDict != null) throw PythonOps.TypeError("argument after ** must be a dictionary");

            if ((kws == null || kws.Count == 0) && names.Length == 0) {
                List<object?> largs = new List<object?>(args);
                if (argsTuple != null) {
                    foreach (object? arg in PythonOps.GetCollection(argsTuple))
                        largs.Add(arg);
                }
                return CallWithContext(context, func, largs.ToArray());
            } else {
                List<object?> largs;

                if (argsTuple != null && args.Length == names.Length) {
                    if (!(argsTuple is PythonTuple tuple)) tuple = new PythonTuple(argsTuple);

                    largs = new List<object?>(tuple);
                    largs.AddRange(args);
                } else {
                    largs = new List<object?>(args);
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

        public static object? CallWithArgsTuple(object func, object?[] args, object argsTuple) {
            if (argsTuple is PythonTuple tp) {
                object?[] nargs = new object[args.Length + tp.__len__()];
                for (int i = 0; i < args.Length; i++) nargs[i] = args[i];
                for (int i = 0; i < tp.__len__(); i++) nargs[i + args.Length] = tp[i];
                return PythonCalls.Call(func, nargs);
            }

            PythonList allArgs = new PythonList(args.Length + 10);
            allArgs.AddRange(args);
            IEnumerator e = PythonOps.GetEnumerator(argsTuple);
            while (e.MoveNext()) allArgs.AddNoLock(e.Current);

            return PythonCalls.Call(func, allArgs.GetObjectArray());
        }

        public static object GetIndex(CodeContext/*!*/ context, object? o, object index) {
            PythonContext pc = context.LanguageContext;
            return pc.GetIndexSite.Target(pc.GetIndexSite, o, index);
        }

        public static bool TryGetBoundAttr(object? o, string name, out object? ret) {
            return TryGetBoundAttr(DefaultContext.Default, o, name, out ret);
        }

        public static void SetAttr(CodeContext/*!*/ context, object? o, string name, object? value) {
            context.LanguageContext.SetAttr(context, o, name, value);
        }

        public static bool TryGetBoundAttr(CodeContext/*!*/ context, object? o, string name, out object? ret) {
            return DynamicHelpers.GetPythonType(o).TryGetBoundAttr(context, o, name, out ret);
        }

        public static void DeleteAttr(CodeContext/*!*/ context, object? o, string name) {
            context.LanguageContext.DeleteAttr(context, o, name);
        }

        public static bool HasAttr(CodeContext/*!*/ context, object? o, string name) {
            return TryGetBoundAttr(context, o, name, out _);
        }

        public static object GetBoundAttr(CodeContext/*!*/ context, object? o, string name) {
            if (!DynamicHelpers.GetPythonType(o).TryGetBoundAttr(context, o, name, out object ret)) {
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
            if (DynamicHelpers.GetPythonType(o).TryGetNonCustomMember(context, o, name, out object value)) {
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

        public static IList<object?> GetAttrNames(CodeContext/*!*/ context, object? o) {
            if (o is IPythonMembersList pyMemList) {
                return pyMemList.GetMemberNames(context);
            }

            if (o is IMembersList memList) {
                return new PythonList(memList.GetMemberNames());
            }

            if (o is IPythonObject po) {
                return po.PythonType.GetMemberNames(context, o);
            }

            PythonList res = DynamicHelpers.GetPythonType(o).GetMemberNames(context, o);

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
                if (PythonContext.TryInvokeTernaryOperator(DefaultContext.Default,
                    TernaryOperators.GetDescriptor,
                    o,
                    instance,
                    context,
                    out object ret)) {
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

            return PythonContext.TryInvokeTernaryOperator(DefaultContext.Default,
                TernaryOperators.SetDescriptor,
                o,
                instance,
                value,
                out _);
        }

        /// <summary>
        /// Handles the descriptor protocol for user-defined objects that may implement __delete__
        /// </summary>
        public static bool TryDeleteUserDescriptor(object o, object instance) {
            // slow, but only encountred for user defined descriptors.
            PerfTrack.NoteEvent(PerfTrack.Categories.DictInvoke, "__delete__");

            return PythonTypeOps.TryInvokeBinaryOperator(DefaultContext.Default,
                o,
                instance,
                "__delete__",
                out _);
        }

        public static object? Invoke(CodeContext/*!*/ context, object? target, string name, object? arg0) {
            return PythonCalls.Call(context, PythonOps.GetBoundAttr(context, target, name), arg0);
        }

        public static object? Invoke(CodeContext/*!*/ context, object? target, string name, params object?[] args) {
            return PythonCalls.Call(context, PythonOps.GetBoundAttr(context, target, name), args);
        }

#if FEATURE_LCG
        public static Delegate CreateDynamicDelegate(DynamicMethod meth, Type delegateType, object target) {
            // Always close delegate around its own instance of the frame
            return meth.CreateDelegate(delegateType, target);
        }
#endif

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

        internal static int ClampToInt32(this long value) {
            return (int)Math.Max(Math.Min(value, int.MaxValue), int.MinValue);
        }

        public static bool IsMappingType(CodeContext/*!*/ context, object o) {
            if (o is IDictionary || o is PythonDictionary || o is IDictionary<object, object>) {
                return true;
            }

            if ((o is IPythonObject) && PythonOps.TryGetBoundAttr(context, o, "__getitem__", out _)) {
                if (!PythonOps.IsClsVisible(context)) {
                    // in standard Python methods aren't mapping types, therefore
                    // if the user hasn't broken out of that box yet don't treat 
                    // them as mapping types.
                    if (o is BuiltinFunction) return false;
                }
                return true;
            }
            return false;
        }

        //  Indexing is 0-based. Need to deal with negative indices
        //  (which mean count backwards from end of sequence)
        //  +---+---+---+---+---+
        //  | a | b | c | d | e |
        //  +---+---+---+---+---+
        //    0   1   2   3   4
        //   -5  -4  -3  -2  -1
        public static int FixSliceIndex(int v, int len) {
            Debug.Assert(len >= 0);
            if (v < 0) v = len + v;
            if (v < 0) return 0;
            if (v > len) return len;
            return v;
        }

        public static long FixSliceIndex(long v, long len) {
            Debug.Assert(len >= 0);
            if (v < 0) v = len + v;
            if (v < 0) return 0;
            if (v > len) return len;
            return v;
        }

        internal static int GetSliceCount(int start, int stop, int step) {
            stop = step > 0 ? Math.Max(stop, start) : Math.Min(stop, start);
            // calculations on int could cause overflow, so using long instead
            return (int)((step > 0 ? ((long)stop - start + step - 1) : ((long)stop - start + step + 1)) / step);
        }

        private static long GetSliceCount(long start, long stop, long step) {
            // ostep can be close long.MaxValue or long.MinValue so unconditional (ostop - ostart + ostep) could overflow
            return step > 0 ? (stop <= start ? 0 : step >= (stop - start) ? 1 : checked(stop - start + step - 1) / step)
                             : (stop >= start ? 0 : step <= (stop - start) ? 1 : checked(stop - start + step + 1) / step);
        }

        internal static void FixSlice(
            int length, object? start, object? stop, object? step,
            out int ostart, out int ostop, out int ostep
        ) {
            Debug.Assert(length >= 0);

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
                        ostart = ostep > 0 ? 0 : -1;
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
                        ostop = ostep > 0 ? 0 : -1;
                    }
                } else if (ostop >= length) {
                    ostop = ostep > 0 ? length : length - 1;
                }
            }
        }

        internal static void FixSlice(
            long length, long? start, long? stop, long? step,
            out long ostart, out long ostop, out long ostep, out long ocount
        ) {
            Debug.Assert(length >= 0);

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
                        ostart = ostep > 0 ? 0 : -1;
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
                        ostop = ostep > 0 ? 0 : -1;
                    }
                } else if (ostop >= length) {
                    ostop = ostep > 0 ? length : length - 1;
                }
            }

            ocount = GetSliceCount(ostart, ostop, ostep);
        }

        /// <summary>
        /// Maps negative indices to their positive counterparts.
        /// If true is returned, then both start and end are mapped to 0 and len range
        /// (inclusive) and end is not less than start.
        /// </summary>
        internal static bool TryFixSubsequenceIndices(int len, ref int start, ref int end) {
            if (start > len) return false;
            int fixedStart = FixSliceIndex(start, len);
            int fixedEnd = FixSliceIndex(end, len);
            if (fixedEnd < fixedStart) return false;
            start = fixedStart;
            end = fixedEnd;
            return true;
        }

        public static int FixIndex(int v, int len) {
            if (!TryFixIndex(v, len, out int fixedIndex)) {
                throw PythonOps.IndexError("index out of range: {0}", v);
            }
            return fixedIndex;
        }

        internal static bool TryFixIndex(int v, int len, out int res) {
            res = 0;
            if (v < 0) {
                v += len;
                if (v < 0) {
                    return false;
                }
            } else if (v >= len) {
                return false;
            }
            res = v;
            return true;
        }

        public static void InitializeForFinalization(CodeContext/*!*/ context, object newObject) {
            IWeakReferenceable iwr = context.LanguageContext.ConvertToWeakReferenceable(newObject);
            InstanceFinalizer nif = new InstanceFinalizer(context, newObject);
            iwr.SetFinalizer(new WeakRefTracker(iwr, nif, nif));
        }

        private static bool TryGetMetaclass(CodeContext/*!*/ context, PythonTuple bases, PythonDictionary dict, out object metaclass) {
            return dict.TryGetValue("__metaclass__", out metaclass) && metaclass != null;
        }

        internal static object? CallPrepare(CodeContext/*!*/ context, PythonType meta, string name, PythonTuple bases, PythonDictionary dict) {
            object? classdict = dict;

            // if available, call the __prepare__ method to get the classdict (PEP 3115)
            if (meta.TryLookupSlot(context, "__prepare__", out PythonTypeSlot pts)) {
                if (pts.TryGetValue(context, null, meta, out object value)) {
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
                return (Func<CodeContext, CodeContext>)funcCode.Target!;
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
                        if (bases[i] is TypeGroup tc) {
                            if (!tc.TryGetNonGenericType(out Type nonGenericType)) {
                                throw PythonOps.TypeError("cannot derive from open generic types {0}", Repr(context, tc));
                            }
                            newBases[i] = DynamicHelpers.GetPythonTypeFromType(nonGenericType);
                        } else {
                            newBases[i] = bases[i];
                        }
                    }
                    bases = newBases;
                    break;
                } else if (dt is PythonType pt) {
                    if (pt.Equals(PythonType.GetPythonType(typeof(Enum))) || pt.Equals(PythonType.GetPythonType(typeof(Array)))
                    || pt.Equals(PythonType.GetPythonType(typeof(Delegate))) || pt.Equals(PythonType.GetPythonType(typeof(ValueType)))) {
                        // .NET does not allow inheriting from these types
                        throw PythonOps.TypeError("cannot derive from special class '{0}'", pt.FinalSystemType.FullName);
                    }
                }
            }

            PythonTuple tupleBases = PythonTuple.MakeTuple(bases);

            if (!TryGetMetaclass(context, tupleBases, vars, out object metaclass)) {
                // this makes sure that object is a base
                if (tupleBases.Count == 0) {
                    tupleBases = PythonTuple.MakeTuple(DynamicHelpers.GetPythonTypeFromType(typeof(object)));
                }
                return PythonType.__new__(context, TypeCache.PythonType, name, tupleBases, vars, selfNames);
            }

            object? classdict = vars;

            if (metaclass is PythonType) {
                classdict = CallPrepare(context, (PythonType)metaclass, name, tupleBases, vars);
            }

            // eg:
            // def foo(*args): print args            
            // __metaclass__ = foo
            // class bar: pass
            // calls our function...
            PythonContext pc = context.LanguageContext;

            object obj = pc.MetaClassCallSite.Target(
                pc.MetaClassCallSite,
                context,
                metaclass,
                name,
                tupleBases,
                classdict
            );

            if (obj is PythonType newType && newType.BaseTypes.Count == 0) {
                newType.BaseTypes.Add(DynamicHelpers.GetPythonTypeFromType(typeof(object)));
            }

            return obj;
        }

        public static void RaiseAssertionError(CodeContext context) {
            PythonDictionary builtins = context.GetBuiltinsDict() ?? context.LanguageContext.BuiltinModuleDict;

            if (builtins._storage.TryGetValue("AssertionError", out object obj)) {
                if (obj is PythonType type) {
                    throw PythonOps.CreateThrowable(type);
                }
                throw PythonOps.CreateThrowable(DynamicHelpers.GetPythonType(obj));
            }

            throw PythonOps.AssertionError("");
        }


        /// <summary>
        /// Python runtime helper for raising assertions. Used by AssertStatement.
        /// </summary>
        /// <param name="msg">Object representing the assertion message</param>
        public static void RaiseAssertionError(CodeContext context, object msg) {
            PythonDictionary builtins = context.GetBuiltinsDict() ?? context.LanguageContext.BuiltinModuleDict;

            if (builtins._storage.TryGetValue("AssertionError", out object obj)) {
                if (obj is PythonType type) {
                    throw PythonOps.CreateThrowable(type, msg);
                }
                throw PythonOps.CreateThrowable(DynamicHelpers.GetPythonType(obj), msg);
            }

            throw PythonOps.AssertionError("{0}", msg);
        }

        /// <summary>
        /// Python runtime helper to create a populated instance of Python List object.
        /// </summary>
        internal static PythonList MakeList(params object[] items) {
            return new PythonList(items);
        }

        /// <summary>
        /// Python runtime helper to create a populated instance of Python List object w/o
        /// copying the array contents.
        /// </summary>
        [NoSideEffects]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static PythonList MakeListNoCopy(params object[] items) {
            return PythonList.FromArrayNoCopy(items);
        }

        /// <summary>
        /// Python runtime helper to create an instance of Python List object.
        /// 
        /// List has the initial provided capacity.
        /// </summary>
        [NoSideEffects]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static PythonList MakeEmptyList() {
            return new PythonList();
        }

        /// <summary>
        /// Python runtime helper to create an instance of Tuple
        /// </summary>
        /// <param name="items"></param>
        /// <returns></returns>
        [NoSideEffects]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static PythonTuple MakeTuple(params object[] items) {
            return PythonTuple.MakeTuple(items);
        }

        /// <summary>
        /// Python runtime helper to create an instance of Tuple
        /// </summary>
        /// <param name="items"></param>
        [NoSideEffects]
        [EditorBrowsable(EditorBrowsableState.Never)]
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
        public static object GetEnumeratorValues(CodeContext/*!*/ context, object? e, int expected, int argcntafter) {
            if (e != null && e.GetType() == typeof(PythonTuple)) {
                // fast path for tuples, avoid enumerating & copying the tuple.
                return GetEnumeratorValuesFromTuple((PythonTuple)e, expected, argcntafter);
            }

            IEnumerator ie = PythonOps.GetEnumerator(context, e);

            if (argcntafter == -1) {
                int count = 0;
                object?[] values = new object?[expected];

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

            static IEnumerable<object?> OneTimeIEnumerableFromEnumerator(IEnumerator ie) {
                while (ie.MoveNext()) {
                    yield return ie.Current;
                }
            }
        }

        [LightThrowing]
        public static object GetEnumeratorValuesNoComplexSets(CodeContext/*!*/ context, object? e, int expected, int argcntafter) {
            if (e != null && e.GetType() == typeof(PythonList)) {
                // fast path for lists, avoid enumerating & copying the list.
                return GetEnumeratorValuesFromList((PythonList)e, expected, argcntafter);
            }

            return GetEnumeratorValues(context, e, expected, argcntafter);
        }

        [LightThrowing]
        private static object GetEnumeratorValuesFromTuple(PythonTuple pythonTuple, int expected, int argcntafter) {
            if (argcntafter == -1) {
                if (pythonTuple.Count == expected) {
                    return pythonTuple._data;
                }
            } else {
                if (pythonTuple.Count >= expected + argcntafter) {
                    return pythonTuple._data;
                }
            }

            return LightExceptions.Throw(PythonOps.ValueErrorForUnpackMismatch(expected, argcntafter, pythonTuple.Count));
        }

        [LightThrowing]
        private static object GetEnumeratorValuesFromList(PythonList list, int expected, int argcntafter) {
            if (argcntafter == -1) {
                if (list._size == expected) {
                    return list._data;
                }
            } else {
                if (list._size >= expected + argcntafter) {
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

            var list = new PythonList((object[])enumeratorValues);

            if (list._size < expected + argcntafter) return LightExceptions.Throw(PythonOps.ValueErrorForUnpackMismatch(expected, argcntafter, list._size));

            var newValues = new object?[expected + argcntafter + 1];

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

        internal static void Write(CodeContext/*!*/ context, object f, string text) {
            PythonContext pc = context.LanguageContext;

            if (f == null) {
                f = pc.SystemStandardOut;
            }
            if (f == null || f == Uninitialized.Instance) {
                throw PythonOps.RuntimeError("lost sys.stdout");
            }

            if (f is PythonIOModule._IOBase pf) {
                // avoid spinning up a site in the normal case
                pf.write(context, text);
                return;
            }

            pc.WriteCallSite.Target(
                pc.WriteCallSite,
                context,
                GetBoundAttr(context, f, "write"),
                text
            );
        }

        private static object? ReadLine(CodeContext/*!*/ context, object f) {
            if (f == null || f == Uninitialized.Instance) throw PythonOps.RuntimeError("lost sys.std_in");
            return PythonOps.Invoke(context, f, "readline");
        }

        internal static void PrintWithDest(CodeContext/*!*/ context, object dest, object o, bool noNewLine = false, bool flush = false) {
            Write(context, dest, ToString(context, o));
            if (!noNewLine) Write(context, dest, "\n");

            if (flush) {
                if (dest is PythonIOModule._IOBase pf) {
                    pf.flush(context);
                } else if (TryGetBoundAttr(context, dest, "flush", out object? flushAttr)) {
                    PythonCalls.Call(context, flushAttr);
                }
            }
        }

        internal static object? ReadLineFromSrc(CodeContext/*!*/ context, object src) {
            return ReadLine(context, src);
        }

        /// <summary>
        /// Prints newline into default standard output
        /// </summary>
        internal static void PrintNewline(CodeContext/*!*/ context) {
            PrintNewlineWithDest(context, context.LanguageContext.SystemStandardOut);
        }

        /// <summary>
        /// Prints newline into specified destination
        /// </summary>
        internal static void PrintNewlineWithDest(CodeContext/*!*/ context, object dest) {
            PythonOps.Write(context, dest, "\n");
        }

        /// <summary>
        /// Called from generated code when we are supposed to print an expression value
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void PrintExpressionValue(CodeContext/*!*/ context, object value) {
            PythonContext pc = context.LanguageContext;
            object dispHook = pc.GetSystemStateValue("displayhook");
            pc.CallWithContext(context, dispHook, value);
        }

#if FEATURE_FULL_CONSOLE
        internal static void PrintException(CodeContext/*!*/ context, Exception/*!*/ exception, object? console = null) {
            PythonContext pc = context.LanguageContext;
            PythonTuple exInfo = GetExceptionInfoLocal(context, exception);
            pc.SetSystemStateValue("last_type", exInfo[0]);
            pc.SetSystemStateValue("last_value", exInfo[1]);
            pc.SetSystemStateValue("last_traceback", exInfo[2]);

            object exceptHook = pc.GetSystemStateValue("excepthook");
            if (exceptHook is BuiltinFunction bf && bf.DeclaringType == typeof(SysModule) && bf.Name == "excepthook") {
                // builtin except hook, display it to the console which may do nice coloring
                if (console is IConsole con) {
                    con.WriteLine(pc.FormatException(exception), Style.Error);
                } else {
                    PrintWithDest(context, pc.SystemStandardError, pc.FormatException(exception));
                }
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

            PythonType? pt = newmod as PythonType;

            if (pt != null &&
                !pt.UnderlyingSystemType.IsEnum &&
                (!pt.UnderlyingSystemType.IsAbstract || !pt.UnderlyingSystemType.IsSealed)) {
                // from type import * only allowed on static classes (and enums)
                throw PythonOps.ImportError("no module named {0}", pt.Name);
            }

            IEnumerator exports;
            bool filterPrivates = false;

            // look for __all__, if it's defined then use that to get the attribute names,
            // otherwise get all the names and filter out members starting w/ _'s.
            if (PythonOps.TryGetBoundAttr(context, newmod, "__all__", out object? all)) {
                exports = PythonOps.GetEnumerator(all);
            } else {
                exports = PythonOps.GetAttrNames(context, newmod).GetEnumerator();
                filterPrivates = true;
            }

            // iterate through the names and populate the scope with the values.
            while (exports.MoveNext()) {
                if (!(exports.Current is string name)) {
                    throw PythonOps.TypeErrorForNonStringAttribute();
                } else if (filterPrivates && name.Length > 0 && name[0] == '_') {
                    continue;
                }

                // we special case several types to avoid one-off code gen of dynamic sites                
                if (newmod is PythonModule scope) {
                    context.SetVariable(name, scope.__dict__[name]);
                } else if (newmod is NamespaceTracker nt) {
                    object value = NamespaceTrackerOps.GetCustomMember(context, nt, name);
                    if (value != OperationFailed.Value) {
                        context.SetVariable(name, value);
                    }
                } else if (pt != null) {
                    if (pt.TryResolveSlot(context, name, out PythonTypeSlot pts) &&
                        pts.TryGetValue(context, null, pt, out object value)) {
                        context.SetVariable(name, value);
                    }
                } else {
                    // not a known type, we'll do use a site to do the get...
                    context.SetVariable(name, PythonOps.GetBoundAttr(context, newmod, name));
                }
            }
        }

        #endregion

        public static ICollection GetCollection(object o) {
            if (o is ICollection ret) return ret;

            List<object?> al = new List<object?>();
            IEnumerator e = GetEnumerator(o);
            while (e.MoveNext()) al.Add(e.Current);
            return al;
        }

        public static IEnumerator GetEnumerator(object? o) {
            return GetEnumerator(DefaultContext.Default, o);
        }

        public static IEnumerator GetEnumerator(CodeContext/*!*/ context, object? o) {
            if (TryGetEnumerator(context, o, out IEnumerator? ie)) {
                return ie;
            }
            throw TypeErrorForNotIterable(o);
        }

        // Lack of type restrictions allows this method to return the direct result of __iter__ without
        // wrapping it. This is the proper behavior for Builtin.iter().
        public static object GetEnumeratorObject(CodeContext/*!*/ context, object? o) {
            if (TryGetEnumeratorObject(context, o, out object? enumerator)) {
                return enumerator;
            }
            throw TypeErrorForNotIterable(o);
        }

        private static bool TryGetEnumeratorObject(CodeContext/*!*/ context, object? o, [NotNullWhen(true)]out object? enumerator) {
            if (o is PythonType pt && !pt.IsIterable(context)) {
                enumerator = default;
                return false;
            }

            if (PythonTypeOps.TryGetOperator(context, o, "__iter__", out object? iterFunc) && iterFunc != null) {
                var iter = PythonCalls.Call(context, iterFunc);
                if (!PythonTypeOps.TryGetOperator(context, iter, "__next__", out _)) {
                    throw TypeError("iter() returned non-iterator of type '{0}'", PythonTypeOps.GetName(iter));
                }
                enumerator = iter!;
                return true;
            }

            if (TryGetEnumerator(context, o, out IEnumerator? ie)) {
                enumerator = ie;
                return true;
            }

            enumerator = default;
            return false;
        }

        public static Exception TypeErrorForNotIterable(object? enumerable) {
            return PythonOps.TypeError("'{0}' object is not iterable", PythonTypeOps.GetName(enumerable));
        }

        public static KeyValuePair<IEnumerator, IDisposable> ThrowTypeErrorForBadIteration(CodeContext/*!*/ context, object enumerable) {
            throw PythonOps.TypeError("iteration over non-sequence of type {0}", PythonTypeOps.GetName(enumerable));
        }

        internal static bool TryGetEnumerator(CodeContext/*!*/ context, [NotNullWhen(true)]object? enumerable, [NotNullWhen(true)]out IEnumerator? enumerator) {
            enumerator = null;

            if (enumerable is PythonType ptEnumerable && !ptEnumerable.IsIterable(context)) {
                return false;
            }

            if (context.LanguageContext.TryConvertToIEnumerable(enumerable, out IEnumerable enumer)) {
                enumerator = enumer.GetEnumerator();
                return true;
            }

            return false;
        }

        public static void ForLoopDispose(KeyValuePair<IEnumerator, IDisposable> iteratorInfo) => iteratorInfo.Value?.Dispose();

        public static KeyValuePair<IEnumerator, IDisposable?> StringEnumerator(string str) {
            return new KeyValuePair<IEnumerator, IDisposable?>(StringOps.StringEnumerator(str), null);
        }

        public static KeyValuePair<IEnumerator, IDisposable?> BytesEnumerator(IList<byte> bytes) {
            return new KeyValuePair<IEnumerator, IDisposable?>(IListOfByteOps.BytesEnumerator(bytes), null);
        }

        public static KeyValuePair<IEnumerator, IDisposable?> GetEnumeratorFromEnumerable(IEnumerable enumerable) {
            IEnumerator enumerator = enumerable.GetEnumerator();
            return new KeyValuePair<IEnumerator, IDisposable?>(enumerator, enumerator as IDisposable);
        }

        public static IEnumerable StringEnumerable(string str) {
            return StringOps.StringEnumerable(str);
        }

        public static IEnumerable BytesEnumerable(IList<byte> bytes) {
            return IListOfByteOps.BytesEnumerable(bytes);
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
            if (clrException is ThreadAbortException tae && tae.ExceptionState is KeyboardInterruptException) {
                Thread.ResetAbort();
            }
#endif

            RestoreCurrentException(clrException);
            return res;
        }

        private static Exception? GetCurrentException() {
            var ex = CurrentExceptionState;
            while (ex != null && ex.Exception == null) {
                ex = ex.PrevException;
            }
            return ex?.Exception;
        }

        // Clear the current exception. Most callers should restore the exception.
        public static void ClearCurrentException() {
            RestoreCurrentException(null);
        }

        // Called by code-gen to save it. Codegen just needs to pass this back to RestoreCurrentException.
        public static Exception? SaveCurrentException() {
            return CurrentExceptionState?.Exception;
        }

        // Called at function exit (like popping). Pass value from SaveCurrentException.
        public static void RestoreCurrentException(Exception? clrException) {
            if (CurrentExceptionState == null) {
                CurrentExceptionState = new ExceptionState();
            }
            CurrentExceptionState.Exception = clrException;
        }

        public static object? CheckException(CodeContext/*!*/ context, object exception, object? test) {
            if (test is PythonType pt) {
                if (PythonOps.IsSubClass(pt, TypeCache.BaseException)) {
                    // catching a Python exception type explicitly.
                    if (PythonOps.IsInstance(context, exception, pt)) return exception;
                } else if (PythonOps.IsSubClass(pt, DynamicHelpers.GetPythonTypeFromType(typeof(Exception)))) {
                    // catching a CLR exception type explicitly.
                    Exception clrEx = PythonExceptions.ToClr(exception);
                    if (PythonOps.IsInstance(context, clrEx, pt)) return clrEx;
                } else {
                    throw PythonOps.TypeError("catching classes that do not inherit from BaseException is not allowed");
                }
            } else if (test is PythonTuple tt) {
                // we handle multiple exceptions, we'll check them one at a time.
                for (int i = 0; i < tt.__len__(); i++) {
                    object? res = CheckException(context, exception, tt[i]);
                    if (res != null) return res;
                }
            } else {
                throw PythonOps.TypeError("catching classes that do not inherit from BaseException is not allowed");
            }

            return null;
        }

        private static TraceBack? CreateTraceBack(PythonContext pyContext, Exception e) {
            // user provided trace back
            var tb = e.GetTraceBack();
            if (tb != null) {
                return tb;
            }

            var frames = ((IList<DynamicStackFrame>)e.GetFrameList()) ?? new DynamicStackFrame[0];
            var stacks = GetFunctionStackNoCreate();
            return CreateTraceBack(e, frames, stacks, frames.Count);
        }

        internal static TraceBack? CreateTraceBack(Exception e, IList<DynamicStackFrame> frames, IList<FunctionStack>? stacks, int frameCount) {
            TraceBackFrame? lastTraceBackFrame = null;
            if (stacks != null) {
                foreach (var stack in stacks) {
                    TraceBackFrame? tbf = stack.Frame;

                    if (tbf == null) {
                        tbf = new TraceBackFrame(
                            stack.Context,
                            stack.Context.GlobalDict,
                            stack.Context.Dict,
                            stack.Code,
                            lastTraceBackFrame
                        );
                    }

                    lastTraceBackFrame = tbf;
                }
            }

            // extend the stack
            var first = true;
            foreach (var frame in frames.Take(frameCount).Reverse()) {
                if (frame is PythonDynamicStackFrame pyFrame && pyFrame.CodeContext != null) {
                    if (first && lastTraceBackFrame?.f_code == pyFrame.Code) continue;
                    first = false;

                    CodeContext context = pyFrame.CodeContext;
                    TraceBackFrame tbf = new TraceBackFrame(
                        context,
                        context.GlobalDict,
                        context.Dict,
                        pyFrame.Code,
                        lastTraceBackFrame);

                    lastTraceBackFrame = tbf;
                }
            }

            TraceBack? tb = null;
            foreach (var frame in frames.Take(frameCount)) {
                if (frame is PythonDynamicStackFrame pyFrame && pyFrame.CodeContext != null) {
                    tb = new TraceBack(tb, lastTraceBackFrame);
                    tb.SetLine(frame.GetFileLineNumber());

                    lastTraceBackFrame = lastTraceBackFrame?.f_back;
                }
            }

            e.SetTraceBack(tb);
            return tb;
        }

        /// <summary>
        /// Get an exception tuple for the "current" exception. This is used for sys.exc_info()
        /// </summary>
        public static PythonTuple GetExceptionInfo(CodeContext/*!*/ context) {
            return GetExceptionInfoLocal(context, GetCurrentException());
        }

        /// <summary>
        /// Get an exception tuple for a given exception. This is like the inverse of MakeException.
        /// </summary>
        /// <param name="context">the code context</param>
        /// <param name="ex">the exception to create a tuple for.</param>
        /// <returns>a tuple of (type, value, traceback)</returns>
        /// <remarks>This is called directly by the With statement so that it can get an exception tuple
        /// in its own private except handler without disturbing the thread-wide sys.exc_info(). </remarks>
        public static PythonTuple/*!*/ GetExceptionInfoLocal(CodeContext/*!*/ context, Exception? ex) {
            if (ex == null) {
                return PythonTuple.MakeTuple(null, null, null);
            }

            PythonContext pc = context.LanguageContext;

            object pyExcep = PythonExceptions.ToPython(ex);
            TraceBack? tb = CreateTraceBack(pc, ex);

            object excType;
            if (pyExcep is IPythonObject pyObj) {
                // class is always the Python type for new-style types (this is also the common case)
                excType = pyObj.PythonType;
            } else {
                excType = PythonOps.GetBoundAttr(context, pyExcep, "__class__");
            }

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

        public static Exception MakeException(CodeContext/*!*/ context, object exception, object cause) {
            Exception e = MakeExceptionWorker(context, exception, cause, false);
            e.RemoveFrameList();
            return e;
        }

        internal static PythonExceptions.BaseException? GetRawContextException() =>
            GetCurrentException()?.GetPythonException() as PythonExceptions.BaseException;

        public static Exception MakeExceptionForGenerator(CodeContext/*!*/ context, object type, object value, object traceback, object cause) {
            Exception e = MakeExceptionWorker(context, type, value, traceback, cause, false, true);
            e.RemoveFrameList();
            return e;
        }

        private static Exception MakeExceptionWorker(CodeContext/*!*/ context, object? type, object? value, object? traceback, object? cause, bool forRethrow, bool forGenerator = false) {
            Exception throwable;

            if (cause != null) {
                // TODO: allow CLR exceptions as a cause

                if (cause is PythonType ptCause && typeof(PythonExceptions.BaseException).IsAssignableFrom(ptCause.UnderlyingSystemType)) {
                    cause = PythonCalls.Call(context, ptCause);
                }

                if (!(cause is PythonExceptions.BaseException)) {
                    throw PythonOps.TypeError("exception causes must derive from BaseException");
                }
            }

            if (type is PythonExceptions.BaseException) {
                throwable = ((PythonExceptions.BaseException)type).CreateClrExceptionWithCause((PythonExceptions.BaseException?)cause, GetRawContextException());
            } else if (type is PythonType pt && typeof(PythonExceptions.BaseException).IsAssignableFrom(pt.UnderlyingSystemType)) {
                throwable = PythonExceptions.CreateThrowableForRaise(context, pt, value, (PythonExceptions.BaseException?)cause);
            } else if (type is Exception ex) {
                // TODO: maybe add cause?
                throwable = ex;
            } else {
                throwable = MakeExceptionTypeError(type, forGenerator);
            }

            if (traceback != null) {
                if (!forRethrow) {
                    if (!(traceback is TraceBack tb)) throw PythonOps.TypeError("traceback argument must be a traceback object");

                    throwable.SetTraceBack(tb);
                }
            } else {
                throwable.RemoveTraceBack();
            }

            PerfTrack.NoteEvent(PerfTrack.Categories.Exceptions, throwable);

            return throwable;
        }

        private static Exception MakeExceptionWorker(CodeContext/*!*/ context, object exception, object cause, bool forRethrow) {
            return MakeExceptionWorker(context, exception, null, null, cause, forRethrow);
        }

        public static Exception CreateThrowable(PythonType type, params object[] args) {
            return PythonExceptions.CreateThrowable(type, args);
        }

        #endregion

        public static string[] GetFunctionSignature(PythonFunction function) {
            return new string[] { function.GetSignatureString() };
        }

        public static PythonDictionary CopyAndVerifyDictionary(PythonFunction function, IDictionary dict) {
            foreach (object? o in dict.Keys) {
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
            if (!PythonTypeOps.TryInvokeUnaryOperator(context, dict, "keys", out object keys)) {
                throw PythonOps.TypeError("{0}() argument after ** must be a mapping, not {1}",
                    funcName,
                    PythonTypeOps.GetName(dict));
            }

            PythonDictionary res = new PythonDictionary();

            // enumerate the keys getting their values
            IEnumerator enumerator = GetEnumerator(keys);
            while (enumerator.MoveNext()) {
                object? o = enumerator.Current;
                if (!(o is string s)) {
                    if (!(o is Extensible<string> es)) {
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
            if (dict.TryGetValue(name, out object val)) {
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

        public static PythonList CopyAndVerifyParamsList(PythonFunction function, object list) {
            return new PythonList(list);
        }

        public static PythonTuple UserMappingToPythonTuple(CodeContext/*!*/ context, object list, string funcName) {
            if (!TryGetEnumeratorObject(context, list, out object? enumerator)) {
                // TODO: CPython 3.5 uses "an iterable" in the error message instead of "a sequence"
                throw TypeError($"{funcName}() argument after * must be a sequence, not {PythonTypeOps.GetName(list)}");
            }

            return PythonTuple.Make(enumerator);
        }

        public static PythonTuple GetOrCopyParamsTuple(PythonFunction function, object input) {
            if (input == null) {
                throw PythonOps.TypeError("{0}() argument after * must be a sequence, not NoneType", function.__name__);
            }

            return PythonTuple.Make(input);
        }

        public static object? ExtractParamsArgument(PythonFunction function, int argCnt, PythonList list) {
            if (list.__len__() != 0) {
                return list.pop(0);
            }

            throw function.BadArgumentError(argCnt);
        }

        public static void AddParamsArguments(PythonList list, params object[] args) {
            for (int i = 0; i < args.Length; i++) {
                list.insert(i, args[i]);
            }
        }

        /// <summary>
        /// Extracts an argument from either the dictionary or params
        /// </summary>
        public static object? ExtractAnyArgument(PythonFunction function, string name, int argCnt, PythonList list, IDictionary dict) {
            object? val;
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
                foreach (string? x in dict.Keys) {
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

        public static object? GetParamsValueOrDefault(PythonFunction function, int index, PythonList extraArgs) {
            if (extraArgs.__len__() > 0) {
                return extraArgs.pop(0);
            }

            return function.Defaults[index];
        }

        public static object? GetFunctionParameterValue(PythonFunction function, int index, string name, PythonList? extraArgs, PythonDictionary? dict) {
            if (extraArgs != null && extraArgs.__len__() > 0) {
                return extraArgs.pop(0);
            }

            if (dict != null && dict.TryRemoveValue(name, out object val)) {
                return val;
            }

            return function.Defaults[index];
        }

        public static object? GetFunctionKeywordOnlyParameterValue(PythonFunction function, string name, PythonDictionary? dict) {
            if (dict != null && dict.TryRemoveValue(name, out object val)) {
                return val;
            }

            return function.__kwdefaults__?[name];
        }

        public static void CheckParamsZero(PythonFunction function, PythonList extraArgs) {
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
            if (function._dict != null && function._dict.TryGetValue(name, out object res)) {
                return res;
            }

            return OperationFailed.Value;
        }

        public static object? PythonFunctionSetMember(PythonFunction function, string name, object? value) {
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
        public static object[]? InitializeUserTypeSlots(PythonType/*!*/ type) {
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
            bool res = type.TryGetNonCustomBoundMember(context, instance, "__init__", out object value);
            Debug.Assert(res);

            return value;
        }

        public static object GetInitSlotMember(CodeContext/*!*/ context, PythonType type, PythonTypeSlot slot, object instance) {
            if (!slot.TryGetValue(context, instance, type, out object value)) {
                throw PythonOps.TypeError("bad __init__");
            }

            return value;
        }

        #region Slicing support

        /// <summary>
        /// Helper to determine if the value is a simple numeric type (int or big int or bool) - used for OldInstance
        /// deprecated form of slicing.
        /// </summary>
        public static bool IsNumericObject([NotNullWhen(true)]object? value) {
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
            if (!(o is IPythonObject po)) return false;

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
                    res = typeof(PythonOps).GetMethod("Throwing" + name)!;
                    break;
                case ConversionResultKind.ImplicitTry:
                case ConversionResultKind.ExplicitTry:
                    res = typeof(PythonOps).GetMethod("NonThrowing" + name)!;
                    break;
                default: throw new InvalidOperationException();
            }
            return res;
        }

        public static object ConvertToPythonPrimitive(object value) {
            return value switch
            {
                float f => (double)f,
                double d => d,
                byte b => (int)b,
                char c => (int)c,
                short s => (int)s,
                ushort us => (int)us,
                int i => i,
                uint ui => (BigInteger)ui,
                long l => (BigInteger)l,
                ulong ul => (BigInteger)ul,
                BigInteger bi => bi,
                bool b => b,
                _ => value,
            };
        }

        public static object? ConvertFloatToComplex(object value) {
            if (value == null) {
                return null;
            }

            if (value is double d) return new Complex(d, 0.0);
            if (value is Extensible<double> ed) return new Complex(ed.Value, 0.0);
            throw new InvalidOperationException();
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

        public static object? NonThrowingConvertToInt(object value) {
            if (!CheckingConvertToInt(value)) return null;
            return value;
        }

        public static object? NonThrowingConvertToLong(object value) {
            if (!CheckingConvertToInt(value)) return null;
            return value;
        }

        public static object? NonThrowingConvertToFloat(object value) {
            if (!CheckingConvertToFloat(value)) return null;
            return value;
        }

        public static object? NonThrowingConvertToComplex(object value) {
            if (!CheckingConvertToComplex(value)) return null;
            return value;
        }

        public static object? NonThrowingConvertToString(object value) {
            if (!CheckingConvertToString(value)) return null;
            return value;
        }

        public static object? NonThrowingConvertToBool(object value) {
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

        public static bool SlotTryGetBoundValue(CodeContext/*!*/ context, PythonTypeSlot/*!*/ slot, object instance, PythonType owner, out object? value) {
            return slot.TryGetValue(context, instance, owner, out value);
        }

        public static bool SlotTryGetValue(CodeContext/*!*/ context, PythonTypeSlot/*!*/ slot, object instance, PythonType owner, out object? value) {
            return slot.TryGetValue(context, instance, owner, out value);
        }

        public static object? SlotGetValue(CodeContext/*!*/ context, PythonTypeSlot/*!*/ slot, object instance, PythonType owner) {
            if (!slot.TryGetValue(context, instance, owner, out object? value)) {
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
                    res[i] = (T)tuple[i]!;
                } catch (InvalidCastException) {
                    res[i] = Converter.Convert<T>(tuple[i]);
                }
            }
            return res;
        }

        #region Function helpers

        public static PythonGenerator MakeGenerator(PythonFunction function, MutableTuple data, object generatorCode) {
            if (!(generatorCode is Func<MutableTuple, object> next)) {
                next = ((LazyCode<Func<MutableTuple, object>>)generatorCode).EnsureDelegate();
            }

            return new PythonGenerator(function, next, data);
        }

        public static object MakeGeneratorExpression(object function, object input) {
            PythonFunction func = (PythonFunction)function;
            return ((Func<PythonFunction, object, object>)func.__code__.Target)(func, input);
        }

        public static FunctionCode MakeFunctionCode(CodeContext/*!*/ context, string name, string documentation, string[] parameterNames, int argCount, FunctionAttributes flags, int startIndex, int endIndex, string path, Delegate code, string[] freeVars, string[] names, string[] cellVars, string[] varNames, int localCount) {
            Compiler.Ast.SerializedScopeStatement scope = new Compiler.Ast.SerializedScopeStatement(name, parameterNames, argCount, flags, startIndex, endIndex, path, freeVars, names, cellVars, varNames);

            return new FunctionCode(context.LanguageContext, code, scope, documentation, localCount);
        }

        [NoSideEffects]
        public static object MakeFunction(CodeContext/*!*/ context, FunctionCode funcInfo, object modName, object[] defaults, PythonDictionary kwdefaults, PythonDictionary annotations) {
            return new PythonFunction(context, funcInfo, modName, defaults, kwdefaults, annotations, null);
        }

        [NoSideEffects]
        public static object MakeFunctionDebug(CodeContext/*!*/ context, FunctionCode funcInfo, object modName, object[] defaults, PythonDictionary kwdefaults, PythonDictionary annotations, Delegate target) {
            funcInfo.SetDebugTarget(context.LanguageContext, target);

            return new PythonFunction(context, funcInfo, modName, defaults, kwdefaults, annotations, null);
        }

        public static CodeContext FunctionGetContext(PythonFunction func) {
            return func.Context;
        }

        public static object FunctionGetDefaultValue(PythonFunction func, int index) {
            return func.Defaults[index];
        }

        public static object? FunctionGetKeywordOnlyDefaultValue(PythonFunction func, string name) {
            if (func.__kwdefaults__ == null) return null;
            func.__kwdefaults__.TryGetValue(name, out object res);
            return res;
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
            FunctionPushFrame(context.LanguageContext);
        }

        public static void FunctionPopFrame() {
            PythonFunction.AddRecursionDepth(-1);
        }

        #endregion

        /// <summary>
        /// Convert object to a given type. This code is equivalent to NewTypeMaker.EmitConvertFromObject
        /// except that it happens at runtime instead of compile time.
        /// </summary>
        public static T ConvertFromObject<T>(object obj) {
            Type toType = typeof(T);
            object? result;
            MethodInfo fastConvertMethod = PythonBinder.GetFastConvertMethod(toType);
            if (fastConvertMethod != null) {
                result = fastConvertMethod.Invoke(null, new object[] { obj });
            } else if (typeof(Delegate).IsAssignableFrom(toType)) {
                result = Converter.ConvertToDelegate(obj, toType);
            } else {
                result = obj;
            }
            return (T)result!;
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
            if (!(enumerable is IEnumerator ie)) {
                if (enumerable is IEnumerable ienum) {
                    ie = ienum.GetEnumerator();
                } else {
                    ie = Converter.ConvertToIEnumerator(enumerable);
                }
            }
            
            while (ie.MoveNext()) {
                if (IsOrEqualsRetBool(context, ie.Current, value)) {
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
                throw new UnboundLocalException(string.Format("Local variable '{0}' referenced before assignment.", name));
            }
            return value;
        }      

        public static object PythonTypeSetCustomMember(CodeContext/*!*/ context, PythonType self, string name, object value) {
            self.SetCustomMember(context, name, value);
            return value;
        }

        public static object? PythonTypeDeleteCustomMember(CodeContext/*!*/ context, PythonType self, string name) {
            self.DeleteCustomMember(context, name);
            return null;
        }

        public static bool IsPythonType(PythonType type) {
            return type.IsPythonType;
        }

        public static object? PublishModule(CodeContext/*!*/ context, string name) {
            context.LanguageContext.SystemStateModules.TryGetValue(name, out object? original);
            var module = ((PythonScopeExtension)context.GlobalScope.GetExtension(context.LanguageContext.ContextId)).Module;
            context.LanguageContext.SystemStateModules[name] = module;
            return original;
        }

        public static void RemoveModule(CodeContext/*!*/ context, string name, object? oldValue) {
            if (oldValue != null) {
                context.LanguageContext.SystemStateModules[name] = oldValue;
            } else {
                context.LanguageContext.SystemStateModules.Remove(name);
            }
        }

        public static Ellipsis Ellipsis => Ellipsis.Value;

        public static NotImplementedType NotImplemented => NotImplementedType.Value;

        public static void ListAddForComprehension(PythonList l, object? o) {
            l.AddNoLock(o);
        }

        public static void SetAddForComprehension(SetCollection s, object? o) {
            s._items.AddNoLock(o);
        }

        public static void DictAddForComprehension(PythonDictionary d, object? k, object? v) {
            d._storage.AddNoLock(ref d._storage, k, v);
        }

        public static void ModuleStarted(CodeContext/*!*/ context, ModuleOptions features) {
            context.ModuleContext.Features |= features;
        }

        public static void Warn(CodeContext/*!*/ context, PythonType category, string message, params object?[] args) {
            PythonContext pc = context.LanguageContext;
            object? warnings = pc.GetWarningsModule();
            object? warn = null;

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
            PythonContext pc = context.LanguageContext;
            object? warnings = pc.GetWarningsModule();
            object? warn = null;

            if (warnings != null) {
                warn = PythonOps.GetBoundAttr(context, warnings, "showwarning");
            }

            if (warn == null) {
                PythonOps.PrintWithDest(context, pc.SystemStandardError, $"{filename}:{lineNo}: {category.Name}: {message}\n", noNewLine: true);
            } else {
                PythonOps.CallWithContext(context, warn, message, category, filename ?? "", lineNo);
            }
        }

        private static string FormatWarning(string message, object?[] args) {
            for (int i = 0; i < args.Length; i++) {
                args[i] = PythonOps.ToString(args[i]);
            }

            message = string.Format(message, args);
            return message;
        }

        public static DynamicMetaObjectBinder MakeComboAction(CodeContext/*!*/ context, DynamicMetaObjectBinder opBinder, DynamicMetaObjectBinder convBinder) {
            return context.LanguageContext.BinaryOperationRetType((PythonBinaryOperationBinder)opBinder, (PythonConversionBinder)convBinder);
        }

        public static DynamicMetaObjectBinder MakeInvokeAction(CodeContext/*!*/ context, CallSignature signature) {
            return context.LanguageContext.Invoke(signature);
        }

        public static DynamicMetaObjectBinder MakeGetAction(CodeContext/*!*/ context, string name, bool isNoThrow) {
            return context.LanguageContext.GetMember(name);
        }

        public static DynamicMetaObjectBinder MakeCompatGetAction(CodeContext/*!*/ context, string name) {
            return context.LanguageContext.CompatGetMember(name, false);
        }

        public static DynamicMetaObjectBinder MakeCompatInvokeAction(CodeContext/*!*/ context, CallInfo callInfo) {
            return context.LanguageContext.CompatInvoke(callInfo);
        }

        public static DynamicMetaObjectBinder MakeCompatConvertAction(CodeContext/*!*/ context, Type toType, bool isExplicit) {
            return context.LanguageContext.Convert(toType, isExplicit ? ConversionResultKind.ExplicitCast : ConversionResultKind.ImplicitCast).CompatBinder;
        }

        public static DynamicMetaObjectBinder MakeSetAction(CodeContext/*!*/ context, string name) {
            return context.LanguageContext.SetMember(name);
        }

        public static DynamicMetaObjectBinder MakeDeleteAction(CodeContext/*!*/ context, string name) {
            return context.LanguageContext.DeleteMember(name);
        }

        public static DynamicMetaObjectBinder MakeConversionAction(CodeContext/*!*/ context, Type type, ConversionResultKind kind) {
            return context.LanguageContext.Convert(type, kind);
        }

        public static DynamicMetaObjectBinder MakeTryConversionAction(CodeContext/*!*/ context, Type type, ConversionResultKind kind) {
            return context.LanguageContext.Convert(type, kind);
        }

        public static DynamicMetaObjectBinder MakeOperationAction(CodeContext/*!*/ context, int operationName) {
            return context.LanguageContext.Operation((PythonOperationKind)operationName);
        }

        public static DynamicMetaObjectBinder MakeUnaryOperationAction(CodeContext/*!*/ context, ExpressionType expressionType) {
            return context.LanguageContext.UnaryOperation(expressionType);
        }

        public static DynamicMetaObjectBinder MakeBinaryOperationAction(CodeContext/*!*/ context, ExpressionType expressionType) {
            return context.LanguageContext.BinaryOperation(expressionType);
        }

        public static DynamicMetaObjectBinder MakeGetIndexAction(CodeContext/*!*/ context, int argCount) {
            return context.LanguageContext.GetIndex(argCount);
        }

        public static DynamicMetaObjectBinder MakeSetIndexAction(CodeContext/*!*/ context, int argCount) {
            return context.LanguageContext.SetIndex(argCount);
        }

        public static DynamicMetaObjectBinder MakeDeleteIndexAction(CodeContext/*!*/ context, int argCount) {
            return context.LanguageContext.DeleteIndex(argCount);
        }

        public static DynamicMetaObjectBinder MakeGetSliceBinder(CodeContext/*!*/ context) {
            return context.LanguageContext.GetSlice;
        }

        public static DynamicMetaObjectBinder MakeSetSliceBinder(CodeContext/*!*/ context) {
            return context.LanguageContext.SetSliceBinder;
        }

        public static DynamicMetaObjectBinder MakeDeleteSliceBinder(CodeContext/*!*/ context) {
            return context.LanguageContext.DeleteSlice;
        }

#if FEATURE_REFEMIT
        /// <summary>
        /// Provides access to AppDomain.DefineDynamicAssembly which cannot be called from a DynamicMethod
        /// </summary>
        public static AssemblyBuilder DefineDynamicAssembly(AssemblyName name, AssemblyBuilderAccess access) {
#if FEATURE_ASSEMBLYBUILDER_DEFINEDYNAMICASSEMBLY
            return AssemblyBuilder.DefineDynamicAssembly(name, access);
#else
            return AppDomain.CurrentDomain.DefineDynamicAssembly(name, access);
#endif
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
                    typeof(UnmanagedFunctionPointerAttribute).GetConstructor(new[] { typeof(CallingConvention) })!,
                    new object[] { callingConvention })
                );
            }

            return builder.CreateTypeInfo()!;
        }

        /// <summary>
        /// Provides the entry point for a compiled module.  The stub exe calls into InitializeModule which
        /// does the actual work of adding references and importing the main module.  Upon completion it returns
        /// the exit code that the program reported via SystemExit or 0.
        /// </summary>
        public static int InitializeModule(Assembly/*!*/ precompiled, string/*!*/ main, string[] references) {
            return InitializeModuleEx(precompiled, main, references, false, null);
        }

        /// <summary>
        /// Provides the entry point for a compiled module.  The stub exe calls into InitializeModule which
        /// does the actual work of adding references and importing the main module.  Upon completion it returns
        /// the exit code that the program reported via SystemExit or 0.
        /// </summary>
        public static int InitializeModuleEx(Assembly/*!*/ precompiled, string/*!*/ main, string[] references, bool ignoreEnvVars) {
            return InitializeModuleEx(precompiled, main, references, ignoreEnvVars, null);
        }


        public static int InitializeModuleEx(Assembly/*!*/ precompiled, string/*!*/ main, string[] references, bool ignoreEnvVars, Dictionary<string, object>? options) {
            ContractUtils.RequiresNotNull(precompiled, nameof(precompiled));
            ContractUtils.RequiresNotNull(main, nameof(main));

            if(options == null) {
                options = new Dictionary<string, object>();
            }
            options["Arguments"] = Environment.GetCommandLineArgs();

            var pythonEngine = Python.CreateEngine(options);

            var pythonContext = (PythonContext)HostingHelpers.GetLanguageContext(pythonEngine);

            if (!ignoreEnvVars) {
                int pathIndex = pythonContext.PythonOptions.SearchPaths.Count;
                string? path = Environment.GetEnvironmentVariable("IRONPYTHONPATH");
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
                    pythonContext.DomainManager.LoadAssembly(Assembly.Load(new AssemblyName(referenceName)));
                }
            }

            ModuleContext modCtx = new ModuleContext(new PythonDictionary(), pythonContext);
            // import __main__
            try {
                Importer.Import(modCtx.GlobalContext, main, PythonTuple.EMPTY, 0);
            } catch (SystemExitException ex) {
                return ex.GetExitCode(out _);
            }

            return 0;
        }
#endif

        public static CodeContext GetPythonTypeContext(PythonType pt) {
            return pt.PythonContext.SharedContext;
        }

        public static Delegate GetDelegate(CodeContext/*!*/ context, object target, Type type) {
            return context.LanguageContext.DelegateCreator.GetDelegate(target, type);
        }

        public static int CompareLists(PythonList self, PythonList other) {
            return self.CompareTo(other);
        }

        public static int CompareTuples(PythonTuple self, PythonTuple other) {
            return self.CompareTo(other);
        }

        public static int CompareFloats(double self, double other) {
            return DoubleOps.Compare(self, other);
        }

        [Obsolete("Use Bytes(IList<byte>) instead.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Bytes MakeBytes(byte[] bytes) {
            return Bytes.Make(bytes);
        }

        public static byte[] MakeByteArray(this string s) {
            return StringOps.Latin1Encoding.GetBytes(s);
        }

        public static string MakeString(this IList<byte> bytes) {
            return MakeString(bytes, bytes.Count);
        }

        internal static string MakeString(this IList<byte> bytes, int maxBytes) {
            return MakeString(bytes, 0, maxBytes);
        }

        internal static string MakeString(this IList<byte> bytes, int startIdx, int maxBytes) {
            Debug.Assert(startIdx >= 0);

            if (bytes.Count < maxBytes) maxBytes = bytes.Count;
            if (maxBytes <= 0 || startIdx >= bytes.Count) return string.Empty;

            byte[] byteArr = (bytes is byte[] arr) ? arr : bytes.ToArray();
            return StringOps.Latin1Encoding.GetString(byteArr, startIdx, maxBytes);
        }

        internal static string MakeString(this Span<byte> bytes) {
            return MakeString((ReadOnlySpan<byte>)bytes);
        }

        internal static string MakeString(this ReadOnlySpan<byte> bytes) {
            if (bytes.IsEmpty) {
                return string.Empty;
            } else unsafe {
                fixed (byte* bp = bytes) {
                    return StringOps.Latin1Encoding.GetString(bp, bytes.Length);
                }
            }
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
            if (value >= int.MinValue && value <= int.MaxValue) {
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
            return ((RuntimeVariablesDictionaryStorage)context.Dict._storage).Tuple;
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

            var ex = NameError(name);
            if (lightThrow) {
                return LightExceptions.Throw(ex);
            }
            throw ex;
        }

        public static object RawGetGlobal(CodeContext/*!*/ context, string name) {
            if (context.TryGetGlobalVariable(name, out object res)) {
                return res;
            }

            return Uninitialized.Instance;
        }

        public static object RawGetLocal(CodeContext/*!*/ context, string name) {
            if (context.TryLookupName(name, out object res)) {
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

        public static Exception UnexpectedKeywordArgumentError(PythonFunction function, string? name) {
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
            if (dt.TryResolveSlot(context, name, out _)) {
                throw PythonOps.AttributeErrorForReadonlyAttribute(dt.Name, name);
            }

            throw PythonOps.AttributeErrorForMissingAttribute(dt.Name, name);
        }

        public static Exception AttributeErrorForMissingAttribute(object o, string name) {
            if (o is PythonType dt) {
                return PythonOps.AttributeErrorForMissingAttribute(dt.Name, name);
            }
            else if (o is NamespaceTracker) {
                return PythonOps.AttributeErrorForMissingAttribute(PythonTypeOps.GetName(o), name);
            }

            return AttributeErrorForReadonlyAttribute(PythonTypeOps.GetName(o), name);
        }


        public static Exception ValueError(string format, params object?[] args) {
            return new ValueErrorException(string.Format(format, args));
        }

        public static Exception KeyError(object key) {
            return PythonExceptions.CreateThrowable(PythonExceptions.KeyError, key);
        }

        public static Exception KeyError(string format, params object?[] args) {
            return new KeyNotFoundException(string.Format(format, args));
        }

        public static Exception UnicodeDecodeError(string message, byte[] bytesUnknown, int index) {
            return new DecoderFallbackException(message, bytesUnknown, index);
        }

        public static Exception UnicodeEncodeError(string encoding, string @object, int start, int end, string reason) {
            return PythonExceptions.CreateThrowable(PythonExceptions.UnicodeEncodeError, encoding, @object, start, end, reason);
        }

        // access to possibly internal constructors of EncoderFallbackException
        public static Exception UnicodeEncodeError(string message, char charUnknown, int index) {
            var ctor = typeof(EncoderFallbackException).GetConstructor(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(string), typeof(char), typeof(int) }, null)!;
            return (EncoderFallbackException)ctor.Invoke(new object[] { message, charUnknown, index });
        }

        public static Exception UnicodeEncodeError(string message, char charUnknownHigh, char charUnknownLow, int index) {
            var ctor = typeof(EncoderFallbackException).GetConstructor(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(string), typeof(char), typeof(char), typeof(int) }, null)!;
            return (EncoderFallbackException)ctor.Invoke(new object[] { message, charUnknownHigh, charUnknownLow, index });
        }

        public static Exception IOError(Exception inner) {
            return OSError(inner.Message, inner);
        }

        public static Exception IOError(string format, params object?[] args) {
            return OSError(format, args);
        }

        internal static Exception OSError(int errno, string strerror, string? filename = null, int? winerror = null, string? filename2 = null) {
            if (filename2 != null) {
                return PythonExceptions.CreateThrowable(PythonExceptions.OSError, errno, strerror, filename, winerror, filename2);
            } else if (winerror != null) {
                return PythonExceptions.CreateThrowable(PythonExceptions.OSError, errno, strerror, filename, winerror);
            } else if (filename != null) {
                return PythonExceptions.CreateThrowable(PythonExceptions.OSError, errno, strerror, filename);
            } else {
                return PythonExceptions.CreateThrowable(PythonExceptions.OSError, errno, strerror);
            }
        }

        public static Exception EofError(string format, params object?[] args) {
            return new EndOfStreamException(string.Format(format, args));
        }

        public static Exception ZeroDivisionError(string format, params object?[] args) {
            return new DivideByZeroException(string.Format(format, args));
        }

        public static Exception SystemError(string format, params object?[] args) {
            return new SystemException(string.Format(format, args));
        }

        public static Exception TypeError(string format, params object?[] args) {
            return new TypeErrorException(string.Format(format, args));
        }

        public static Exception IndexError(string format, params object?[] args) {
            return new IndexOutOfRangeException(string.Format(format, args));
        }

        public static Exception MemoryError() => new OutOfMemoryException();

        public static Exception MemoryError(string message) => new OutOfMemoryException(message);

        public static Exception MemoryError(string format, params object[] args) => new OutOfMemoryException(string.Format(format, args));

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
            return new OverflowException(string.Format(format, args));
        }

        public static Exception WindowsError(string format, params object[] args) {
            return OSError(format, args);
        }

        public static Exception TimeoutError(string format, params object[] args) {
            return new TimeoutException(string.Format(format, args));
        }

        public static Exception SystemExit() {
            return new SystemExitException();
        }

        public static void SyntaxWarning(string message, SourceUnit sourceUnit, SourceSpan span, int errorCode) {
            PythonContext pc = (PythonContext)sourceUnit.LanguageContext;
            CodeContext context = pc.SharedContext;

            ShowWarning(context, PythonExceptions.SyntaxWarning, message, sourceUnit.Path, span.Start.Line);
        }

        public static SyntaxErrorException SyntaxError(string format, params object[] args) {
            return new SyntaxErrorException(string.Format(format, args));
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

        public static SyntaxErrorException BadSourceEncodingError(string message, int line, string path) {
            SourceLocation sloc = new SourceLocation(0, line, 1); // index and column will be ignored
            SyntaxErrorException res = new SyntaxErrorException(
                message,
                path,
                null, 
                null,
                new SourceSpan(sloc, sloc),
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

        public static Exception TypeErrorForTypeMismatch(string expectedTypeName, object? instance) {
            return TypeError("expected {0}, got {1}", expectedTypeName, PythonOps.GetPythonTypeName(instance));
        }

        public static Exception TypeErrorForBytesLikeTypeMismatch(object? instance) {
            return TypeError("a bytes-like object is required, not '{0}'", PythonOps.GetPythonTypeName(instance));
        }

        // If hash is called on an instance of an unhashable type
        public static Exception TypeErrorForUnhashableType(string typeName) {
            return TypeError(typeName + " objects are unhashable");
        }

        public static Exception TypeErrorForUnhashableObject(object? obj) {
            return TypeErrorForUnhashableType(PythonTypeOps.GetName(obj));
        }

        internal static Exception TypeErrorForIncompatibleObjectLayout(string prefix, PythonType type, Type newType) {
            return TypeError("{0}: '{1}' object layout differs from '{2}'", prefix, type.Name, newType);
        }

        public static Exception TypeErrorForNonStringAttribute() {
            return TypeError("attribute name must be string");
        }

        internal static Exception TypeErrorForBadInstance(string template, object? instance) {
            return TypeError(template, PythonOps.GetPythonTypeName(instance));
        }

        public static Exception TypeErrorForBinaryOp(string opSymbol, object? x, object? y) {
            throw PythonOps.TypeError("unsupported operand type(s) for {0}: '{1}' and '{2}'",
                                opSymbol, GetPythonTypeName(x), GetPythonTypeName(y));
        }

        public static Exception TypeErrorForUnaryOp(string opSymbol, object? x) {
            throw PythonOps.TypeError("unsupported operand type for {0}: '{1}'",
                                opSymbol, GetPythonTypeName(x));
        }

        public static Exception TypeErrorForNonIterableObject(object? o) {
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
        internal static Exception MakeExceptionTypeError(object? type, bool forGenerator = false) {
                        return PythonOps.TypeError(forGenerator ?
            "exceptions must be classes or instances deriving from BaseException, not {0}" :
            "exceptions must derive from BaseException", PythonTypeOps.GetName(type));
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

        public static Exception UncallableError(object? func) {
            return PythonOps.TypeError("'{0}' object is not callable", PythonTypeOps.GetName(func));
        }

        public static Exception TypeErrorForProtectedMember(Type/*!*/ type, string/*!*/ name) {
            return PythonOps.TypeError("cannot access protected member {0} without a python subclass of {1}", name, NameConverter.GetTypeName(type));
        }

        public static Exception TypeErrorForGenericMethod(Type/*!*/ type, string/*!*/ name) {
            return PythonOps.TypeError("{0}.{1} is a generic method and must be indexed with types before calling", NameConverter.GetTypeName(type), name);
        }

        public static Exception TypeErrorForUnIndexableObject(object? o) {
            return TypeError("'{0}' object cannot be interpreted as an integer", DynamicHelpers.GetPythonType(o).Name);
        }

        public static T TypeErrorForBadEnumConversion<T>(object? value) {
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
        private static List<FunctionStack>? _funcStack;

        public static List<FunctionStack> GetFunctionStack() {
            return _funcStack ?? (_funcStack = new List<FunctionStack>());
        }

        public static List<FunctionStack>? GetFunctionStackNoCreate() {
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

        public static bool ModuleTryGetMember(CodeContext/*!*/ context, PythonModule module, string name, out object? res) {
            object? value = module.GetAttributeNoThrow(context, name);
            if (value != OperationFailed.Value) {
                res = value;
                return true;
            }

            res = null;
            return false;
        }

        internal static void ScopeSetMember(CodeContext/*!*/ context, Scope scope, string name, object value) {
            if (((object)scope.Storage) is ScopeStorage scopeStorage) {
                scopeStorage.SetValue(name, false, value);
                return;
            }

            PythonOps.SetAttr(context, scope, name, value);
        }

        internal static object ScopeGetMember(CodeContext/*!*/ context, Scope scope, string name) {
            if (((object)scope.Storage) is ScopeStorage scopeStorage) {
                return scopeStorage.GetValue(name, false);
            }

            return PythonOps.GetBoundAttr(context, scope, name);
        }

        internal static bool ScopeTryGetMember(CodeContext/*!*/ context, Scope scope, string name, out object? value) {
            if (((object)scope.Storage) is ScopeStorage scopeStorage) {
                return scopeStorage.TryGetValue(name, false, out value);
            }

            return PythonOps.TryGetBoundAttr(context, scope, name, out value);
        }

        internal static bool ScopeContainsMember(CodeContext/*!*/ context, Scope scope, string name) {
            if (((object)scope.Storage) is ScopeStorage scopeStorage) {
                return scopeStorage.HasValue(name, false);
            }

            return PythonOps.HasAttr(context, scope, name);
        }

        internal static bool ScopeDeleteMember(CodeContext/*!*/ context, Scope scope, string name) {
            if (((object)scope.Storage) is ScopeStorage scopeStorage) {
                return scopeStorage.DeleteValue(name, false);
            }

            bool res = PythonOps.HasAttr(context, scope, name);
            PythonOps.DeleteAttr(context, scope, name);
            return res;
        }

        internal static IList<object?> ScopeGetMemberNames(CodeContext/*!*/ context, Scope scope) {
            if (((object)scope.Storage) is ScopeStorage scopeStorage) {
                List<object?> res = new List<object?>();

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

        public static bool IsExtensionSet(CodeContext codeContext, int id) {
            return codeContext.ModuleContext.ExtensionMethods.Id == id;
        }

        public static object GetExtensionMethodSet(CodeContext context) {
            return context.ModuleContext.ExtensionMethods;
        }
    }

    [DebuggerDisplay("Code = {Code.co_name}, Line = {Frame.f_lineno}")]
    public readonly struct FunctionStack {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public readonly CodeContext/*!*/ Context;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public readonly FunctionCode/*!*/ Code;
        public readonly TraceBackFrame? Frame;

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
