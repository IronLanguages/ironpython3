// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Actions.Calls;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime {

    public static partial class Converter {
        #region Conversion Sites

        private static readonly CallSite<Func<CallSite, object, int>> _intSite = MakeExplicitConvertSite<int>();
        private static readonly CallSite<Func<CallSite, object, double>> _doubleSite = MakeExplicitConvertSite<double>();
        private static readonly CallSite<Func<CallSite, object, Complex>> _complexSite = MakeExplicitConvertSite<Complex>();
        private static readonly CallSite<Func<CallSite, object, BigInteger>> _bigIntSite = MakeExplicitConvertSite<BigInteger>();
        private static readonly CallSite<Func<CallSite, object, string>> _stringSite = MakeExplicitConvertSite<string>();
        private static readonly CallSite<Func<CallSite, object, bool>> _boolSite = MakeExplicitConvertSite<bool>();
        private static readonly CallSite<Func<CallSite, object, char>> _charSite = MakeImplicitConvertSite<char>();
        private static readonly CallSite<Func<CallSite, object, char>> _explicitCharSite = MakeExplicitConvertSite<char>();
        private static readonly CallSite<Func<CallSite, object, IEnumerable>> _ienumerableSite = MakeImplicitConvertSite<IEnumerable>();
        private static readonly CallSite<Func<CallSite, object, IEnumerator>> _ienumeratorSite = MakeImplicitConvertSite<IEnumerator>();
        private static readonly Dictionary<Type, CallSite<Func<CallSite, object, object>>> _siteDict = new Dictionary<Type, CallSite<Func<CallSite, object, object>>>();
        private static readonly CallSite<Func<CallSite, object, byte>> _byteSite = MakeExplicitConvertSite<byte>();
        private static readonly CallSite<Func<CallSite, object, sbyte>> _sbyteSite = MakeExplicitConvertSite<sbyte>();
        private static readonly CallSite<Func<CallSite, object, short>> _int16Site = MakeExplicitConvertSite<short>();
        private static readonly CallSite<Func<CallSite, object, ushort>> _uint16Site = MakeExplicitConvertSite<ushort>();
        private static readonly CallSite<Func<CallSite, object, uint>> _uint32Site = MakeExplicitConvertSite<uint>();
        private static readonly CallSite<Func<CallSite, object, long>> _int64Site = MakeExplicitConvertSite<long>();
        private static readonly CallSite<Func<CallSite, object, ulong>> _uint64Site = MakeExplicitConvertSite<ulong>();
        private static readonly CallSite<Func<CallSite, object, decimal>> _decimalSite = MakeExplicitConvertSite<decimal>();
        private static readonly CallSite<Func<CallSite, object, float>> _floatSite = MakeExplicitConvertSite<float>();

        private static readonly CallSite<Func<CallSite, object, object>>
            _tryByteSite       = MakeExplicitTrySite<byte>(),
            _trySByteSite      = MakeExplicitTrySite<sbyte>(),
            _tryInt16Site      = MakeExplicitTrySite<short>(),
            _tryInt32Site      = MakeExplicitTrySite<int>(),
            _tryInt64Site      = MakeExplicitTrySite<long>(),
            _tryUInt16Site     = MakeExplicitTrySite<ushort>(),
            _tryUInt32Site     = MakeExplicitTrySite<uint>(),
            _tryUInt64Site     = MakeExplicitTrySite<ulong>(),
            _tryDoubleSite     = MakeExplicitTrySite<double>(),
            _tryCharSite       = MakeExplicitTrySite<char>(),
            _tryBigIntegerSite = MakeExplicitTrySite<BigInteger>(),
            _tryComplexSite    = MakeExplicitTrySite<Complex>(),
            _tryStringSite     = MakeExplicitTrySite<string>();

        private static CallSite<Func<CallSite, object, T>> MakeImplicitConvertSite<T>() {
            return MakeConvertSite<T>(ConversionResultKind.ImplicitCast);
        }

        private static CallSite<Func<CallSite, object, T>> MakeExplicitConvertSite<T>() {
            return MakeConvertSite<T>(ConversionResultKind.ExplicitCast);
        }

        private static CallSite<Func<CallSite, object, T>> MakeConvertSite<T>(ConversionResultKind kind) {
            return CallSite<Func<CallSite, object, T>>.Create(
                DefaultContext.DefaultPythonContext.Convert(
                    typeof(T),
                    kind
                )
            );
        }

        private static CallSite<Func<CallSite, object, object>> MakeExplicitTrySite<T>() {
            return MakeTrySite<T>(ConversionResultKind.ExplicitTry);
        }

        private static CallSite<Func<CallSite, object, object>> MakeTrySite<T>(ConversionResultKind kind) {
            return CallSite<Func<CallSite, object, object>>.Create(
                DefaultContext.DefaultPythonContext.Convert(
                    typeof(T),
                    kind
                )
            );
        }

        #endregion

        #region Conversion entry points

        public static int ConvertToInt32(object value) { return _intSite.Target(_intSite, value); }
        public static string ConvertToString(object value) { return _stringSite.Target(_stringSite, value); }
        public static BigInteger ConvertToBigInteger(object value) { return _bigIntSite.Target(_bigIntSite, value); }
        public static double ConvertToDouble(object value) { return _doubleSite.Target(_doubleSite, value); }
        public static Complex ConvertToComplex(object value) { return _complexSite.Target(_complexSite, value); }
        public static bool ConvertToBoolean(object value) { return _boolSite.Target(_boolSite, value); }
        public static long ConvertToInt64(object value) { return _int64Site.Target(_int64Site, value); }

        public static byte ConvertToByte(object value) { return _byteSite.Target(_byteSite, value); }
        public static sbyte ConvertToSByte(object value) { return _sbyteSite.Target(_sbyteSite, value); }
        public static short ConvertToInt16(object value) { return _int16Site.Target(_int16Site, value); }
        public static ushort ConvertToUInt16(object value) { return _uint16Site.Target(_uint16Site, value); }
        public static uint ConvertToUInt32(object value) { return _uint32Site.Target(_uint32Site, value); }
        public static ulong ConvertToUInt64(object value) { return _uint64Site.Target(_uint64Site, value); }
        public static float ConvertToSingle(object value) { return _floatSite.Target(_floatSite, value); }
        public static decimal ConvertToDecimal(object value) { return _decimalSite.Target(_decimalSite, value); }
        public static char ConvertToChar(object value) { return _charSite.Target(_charSite, value); }

        internal static bool TryConvertToByte(object value, out byte result) {
            object res = _tryByteSite.Target(_tryByteSite, value);
            if (res != null) {
                result = (byte)res;
                return true;
            }
            result = default(byte);
            return false;
        }

        internal static bool TryConvertToSByte(object value, out sbyte result) {
            object res = _trySByteSite.Target(_trySByteSite, value);
            if (res != null) {
                result = (sbyte)res;
                return true;
            }
            result = default(sbyte);
            return false;
        }

        internal static bool TryConvertToInt16(object value, out short result) {
            object res = _tryInt16Site.Target(_tryInt16Site, value);
            if (res != null) {
                result = (short)res;
                return true;
            }
            result = default(short);
            return false;
        }

        internal static bool TryConvertToInt32(object value, out int result) {
            object res = _tryInt32Site.Target(_tryInt32Site, value);
            if (res != null) {
                result = (int)res;
                return true;
            }
            result = default(int);
            return false;
        }

        internal static bool TryConvertToInt64(object value, out long result) {
            object res = _tryInt64Site.Target(_tryInt64Site, value);
            if (res != null) {
                result = (long)res;
                return true;
            }
            result = default(long);
            return false;
        }

        internal static bool TryConvertToUInt16(object value, out ushort result) {
            object res = _tryUInt16Site.Target(_tryUInt16Site, value);
            if (res != null) {
                result = (ushort)res;
                return true;
            }
            result = default(ushort);
            return false;
        }

        internal static bool TryConvertToUInt32(object value, out uint result) {
            object res = _tryUInt32Site.Target(_tryUInt32Site, value);
            if (res != null) {
                result = (uint)res;
                return true;
            }
            result = default(uint);
            return false;
        }

        internal static bool TryConvertToUInt64(object value, out ulong result) {
            object res = _tryUInt64Site.Target(_tryUInt64Site, value);
            if (res != null) {
                result = (ulong)res;
                return true;
            }
            result = default(ulong);
            return false;
        }

        internal static bool TryConvertToDouble(object value, out double result) {
            object res = _tryDoubleSite.Target(_tryDoubleSite, value);
            if (res != null) {
                result = (double)res;
                return true;
            }
            result = default(double);
            return false;
        }

        internal static bool TryConvertToBigInteger(object value, out BigInteger result) {
            object res = _tryBigIntegerSite.Target(_tryBigIntegerSite, value);
            if (res != null) {
                result = (BigInteger)res;
                return true;
            }
            result = default(BigInteger);
            return false;
        }

        internal static bool TryConvertToComplex(object value, out Complex result) {
            object res = _tryComplexSite.Target(_tryComplexSite, value);
            if (res != null) {
                result = (Complex)res;
                return true;
            }
            result = default(Complex);
            return false;
        }

#nullable enable
        internal static bool TryConvertToString(object? value, [NotNullWhen(true)] out string? result) {
            object res = _tryStringSite.Target(_tryStringSite, value);
            if (res != null) {
                result = (string)res;
                return true;
            }
            result = default(string);
            return false;
        }
#nullable restore

        internal static bool TryConvertToChar(object value, out char result) {
            object res = _tryCharSite.Target(_tryCharSite, value);
            if (res != null) {
                result = (char)res;
                return true;
            }
            result = default;
            return false;
        }

        #endregion

        internal static char ExplicitConvertToChar(object value) {
            return _explicitCharSite.Target(_explicitCharSite, value);
        }

        public static T Convert<T>(object value) {
            return (T)Convert(value, typeof(T));
        }

        internal static bool TryConvert<T>(object value, out T result) {
            try {
                result = Convert<T>(value);
                return true;
            } catch {
                result = default;
                return false;
            }
        }

        /// <summary>
        /// General conversion routine TryConvert - tries to convert the object to the desired type.
        /// Try to avoid using this method, the goal is to ultimately remove it!
        /// </summary>
        internal static bool TryConvert(object value, Type to, out object result) {
            try {
                result = Convert(value, to);
                return true;
            } catch {
                result = default(object);
                return false;
            }
        }

        internal static object Convert(object value, Type to) {
            CallSite<Func<CallSite, object, object>> site;
            lock (_siteDict) {
                if (!_siteDict.TryGetValue(to, out site)) {
                    _siteDict[to] = site = CallSite<Func<CallSite, object, object>>.Create(
                        DefaultContext.DefaultPythonContext.ConvertRetObject(
                            to,
                            ConversionResultKind.ExplicitCast
                        )
                    );
                }
            }

            object res = site.Target(site, value);
            if (to.IsValueType && res == null &&
                (!to.IsGenericType || to.GetGenericTypeDefinition() != typeof(Nullable<>))) {
                throw MakeTypeError(to, value);
            }
            return res;
        }

        /// <summary>
        /// This function tries to convert an object to IEnumerator, or wraps it into an adapter
        /// Do not use this function directly. It is only meant to be used by Ops.GetEnumerator.
        /// </summary>
        internal static bool TryConvertToIEnumerator(object o, out IEnumerator e) {
            try {
                e = _ienumeratorSite.Target(_ienumeratorSite, o);
                return e != null;
            } catch {
                e = null;
                return false;
            }
        }

        /// <summary>
        /// This function tries to convert an object to IEnumerator, or wraps it into an adapter
        /// Do not use this function directly. It is only meant to be used by Ops.GetEnumerator.
        /// </summary>
        internal static IEnumerator ConvertToIEnumerator(object o) {
            return _ienumeratorSite.Target(_ienumeratorSite, o);
        }

        public static IEnumerable ConvertToIEnumerable(object o) {
            return _ienumerableSite.Target(_ienumerableSite, o);
        }

#nullable enable

        /// <summary>
        /// Attempts to convert value into a index usable for slicing and return the integer
        /// value.  If the conversion fails false is returned.
        /// 
        /// If throwOverflowError is true then BigInteger's outside the normal range of integers will
        /// result in an OverflowError.
        ///
        /// When throwNonInt is true, a TypeError will be thrown if __index__ returned a non-int.
        /// </summary>
        internal static bool TryConvertToIndex(object? value, out int index, bool throwOverflowError = true, bool throwNonInt = true) {
            if (TryGetInt(value, out index, throwOverflowError, value)) {
                return true;
            }

            if (PythonTypeOps.TryInvokeUnaryOperator(DefaultContext.Default, value, "__index__", out object indexObj)) {
                if (TryGetInt(indexObj, out index, throwOverflowError, value)) {
                    return true;
                }

                if (throwNonInt) {
                    throw PythonOps.TypeError("__index__ returned non-int (type {0})", PythonOps.GetPythonTypeName(indexObj));
                }
            }

            return false;
        }

        public static int ConvertToIndex(object? value, bool throwOverflowError = false) {
            if (TryConvertToIndex(value, out int index, throwOverflowError: throwOverflowError, throwNonInt: true))
                return index;

            throw PythonOps.TypeError("expected integer value, got {0}", PythonOps.GetPythonTypeName(value));
        }

#nullable restore

        private static bool TryGetInt(object o, out int value, bool throwOverflowError, object original) {
            if (o is int i) {
                value = i;
            } else {
                BigInteger bi;
                if (o is BigInteger) {
                    bi = (BigInteger)o;
                } else if (o is Extensible<BigInteger> ebi) {
                    bi = ebi.Value;
                } else {
                    value = default;
                    return false;
                }

                if (bi.AsInt32(out value)) return true;

                if (throwOverflowError) {
                    throw PythonOps.OverflowError("cannot fit '{0}' into an index-sized integer", PythonOps.GetPythonTypeName(original));
                }

                Debug.Assert(bi != 0);
                value = bi > 0 ? int.MaxValue : int.MinValue;
            }
            return true;
        }

        internal static Exception CannotConvertOverflow(string name, object value) {
            return PythonOps.OverflowError("Cannot convert {0}({1}) to {2}", PythonOps.GetPythonTypeName(value), value, name);
        }

        private static Exception MakeTypeError(Type expectedType, object o) {
            return MakeTypeError(DynamicHelpers.GetPythonTypeFromType(expectedType).Name.ToString(), o);
        }

        private static Exception MakeTypeError(string expectedType, object o) {
            return PythonOps.TypeErrorForTypeMismatch(expectedType, o);
        }

        #region Cached Type instances

        private static readonly Type StringType = typeof(string);
        private static readonly Type Int32Type = typeof(int);
        private static readonly Type DoubleType = typeof(double);
        private static readonly Type DecimalType = typeof(decimal);
        private static readonly Type Int64Type = typeof(long);
        private static readonly Type UInt64Type = typeof(ulong);
        private static readonly Type CharType = typeof(char);
        private static readonly Type SingleType = typeof(float);
        private static readonly Type BooleanType = typeof(bool);
        private static readonly Type BigIntegerType = typeof(BigInteger);
        private static readonly Type ComplexType = typeof(Complex);
        private static readonly Type ExtensibleBigIntegerType = typeof(Extensible<BigInteger>);
        private static readonly Type ExtensibleDoubleType = typeof(Extensible<double>);
        private static readonly Type ExtensibleComplexType = typeof(Extensible<Complex>);
        private static readonly Type DelegateType = typeof(Delegate);
        private static readonly Type IEnumerableType = typeof(IEnumerable);
        private static readonly Type TypeType = typeof(Type);
        private static readonly Type NullableOfTType = typeof(Nullable<>);
        private static readonly Type IListOfTType = typeof(IList<>);
        private static readonly Type IDictOfTType = typeof(IDictionary<,>);
        private static readonly Type IEnumerableOfTType = typeof(IEnumerable<>);
        private static readonly Type IListOfObjectType = typeof(IList<object>);
        private static readonly Type IEnumerableOfObjectType = typeof(IEnumerable<object>);
        private static readonly Type IDictionaryOfObjectType = typeof(IDictionary<object, object>);

        #endregion

        #region Entry points called from the generated code

        public static object ConvertToReferenceType(object fromObject, RuntimeTypeHandle typeHandle) {
            if (fromObject == null) return null;
            return Convert(fromObject, Type.GetTypeFromHandle(typeHandle));
        }

        public static object ConvertToNullableType(object fromObject, RuntimeTypeHandle typeHandle) {
            if (fromObject == null) return null;
            return Convert(fromObject, Type.GetTypeFromHandle(typeHandle));
        }

        public static object ConvertToValueType(object fromObject, RuntimeTypeHandle typeHandle) {
            if (fromObject == null) throw PythonOps.InvalidType(fromObject, typeHandle);
            return Convert(fromObject, Type.GetTypeFromHandle(typeHandle));
        }

        public static Type ConvertToType(object value) {
            if (value == null) return null;

            if (value is Type typeVal) return typeVal;

            if (value is PythonType pythonTypeVal) return pythonTypeVal.UnderlyingSystemType;

            if (value is TypeGroup typeCollision) {
                if (typeCollision.TryGetNonGenericType(out Type nonGenericType)) {
                    return nonGenericType;
                }
            }

            throw MakeTypeError("Type", value);
        }

        public static object ConvertToDelegate(object value, Type to) {
            if (value == null) return null;
            return DefaultContext.DefaultCLS.LanguageContext.DelegateCreator.GetDelegate(value, to);
        }


        #endregion

        public static bool CanConvertFrom(Type fromType, Type toType, NarrowingLevel allowNarrowing) {
            ContractUtils.RequiresNotNull(fromType, nameof(fromType));
            ContractUtils.RequiresNotNull(toType, nameof(toType));

            if (toType == fromType) return true;
            if (toType.IsAssignableFrom(fromType)) return true;
#if FEATURE_COM
            if (fromType.IsCOMObject && toType.IsInterface) return true; // A COM object could be cast to any interface
#endif
            if (HasImplicitNumericConversion(fromType, toType)) return true;

            // Handling the hole that Type is the only object that we 'box'
            if (toType == TypeType &&
                (typeof(PythonType).IsAssignableFrom(fromType) ||
                typeof(TypeGroup).IsAssignableFrom(fromType))) return true;

            // Support extensible types with simple implicit conversions to their base types
            if (typeof(Extensible<BigInteger>).IsAssignableFrom(fromType) && CanConvertFrom(BigIntegerType, toType, allowNarrowing)) {
                return true;
            }
            if (typeof(ExtensibleString).IsAssignableFrom(fromType) && CanConvertFrom(StringType, toType, allowNarrowing)) {
                return true;
            }
            if (typeof(Extensible<double>).IsAssignableFrom(fromType) && CanConvertFrom(DoubleType, toType, allowNarrowing)) {
                return true;
            }
            if (typeof(Extensible<Complex>).IsAssignableFrom(fromType) && CanConvertFrom(ComplexType, toType, allowNarrowing)) {
                return true;
            }

#if FEATURE_CUSTOM_TYPE_DESCRIPTOR
            // try available type conversions...
            object[] tcas = toType.GetCustomAttributes(typeof(TypeConverterAttribute), true);
            foreach (TypeConverterAttribute tca in tcas) {
                TypeConverter tc = GetTypeConverter(tca);

                if (tc == null) continue;

                if (tc.CanConvertFrom(fromType)) {
                    return true;
                }
            }
#endif

            //!!!do user-defined implicit conversions here

            if (allowNarrowing == PythonNarrowing.None) return false;

            return HasNarrowingConversion(fromType, toType, allowNarrowing);
        }

#if FEATURE_CUSTOM_TYPE_DESCRIPTOR
        private static TypeConverter GetTypeConverter(TypeConverterAttribute tca) {
            try {
                ConstructorInfo ci = Type.GetType(tca.ConverterTypeName).GetConstructor(ReflectionUtils.EmptyTypes);
                if (ci != null) return ci.Invoke(ArrayUtils.EmptyObjects) as TypeConverter;
            } catch (TargetInvocationException) {
            }
            return null;
        }
#endif

        private static bool HasImplicitNumericConversion(Type fromType, Type toType) {
            if (fromType.IsEnum) return false;

            if (fromType == typeof(BigInteger)) {
                if (toType == typeof(double)) return true;
                if (toType == typeof(Complex)) return true;
                return false;
            }

            if (fromType == typeof(bool)) {
                if (toType == typeof(int)) return true;
                return HasImplicitNumericConversion(typeof(int), toType);
            }

            switch (fromType.GetTypeCode()) {
                case TypeCode.SByte:
                    switch (toType.GetTypeCode()) {
                        case TypeCode.Int16:
                        case TypeCode.Int32:
                        case TypeCode.Int64:
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                        default:
                            if (toType == BigIntegerType) return true;
                            if (toType == ComplexType) return true;
                            return false;
                    }
                case TypeCode.Byte:
                    switch (toType.GetTypeCode()) {
                        case TypeCode.Int16:
                        case TypeCode.UInt16:
                        case TypeCode.Int32:
                        case TypeCode.UInt32:
                        case TypeCode.Int64:
                        case TypeCode.UInt64:
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                        default:
                            if (toType == BigIntegerType) return true;
                            if (toType == ComplexType) return true;
                            return false;
                    }
                case TypeCode.Int16:
                    switch (toType.GetTypeCode()) {
                        case TypeCode.Int32:
                        case TypeCode.Int64:
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                        default:
                            if (toType == BigIntegerType) return true;
                            if (toType == ComplexType) return true;
                            return false;
                    }
                case TypeCode.UInt16:
                    switch (toType.GetTypeCode()) {
                        case TypeCode.Int32:
                        case TypeCode.UInt32:
                        case TypeCode.Int64:
                        case TypeCode.UInt64:
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                        default:
                            if (toType == BigIntegerType) return true;
                            if (toType == ComplexType) return true;
                            return false;
                    }
                case TypeCode.Int32:
                    switch (toType.GetTypeCode()) {
                        case TypeCode.Int64:
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                        default:
                            if (toType == BigIntegerType) return true;
                            if (toType == ComplexType) return true;
                            return false;
                    }
                case TypeCode.UInt32:
                    switch (toType.GetTypeCode()) {
                        case TypeCode.Int64:
                        case TypeCode.UInt64:
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                        default:
                            if (toType == BigIntegerType) return true;
                            if (toType == ComplexType) return true;
                            return false;
                    }
                case TypeCode.Int64:
                    switch (toType.GetTypeCode()) {
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                        default:
                            if (toType == BigIntegerType) return true;
                            if (toType == ComplexType) return true;
                            return false;
                    }
                case TypeCode.UInt64:
                    switch (toType.GetTypeCode()) {
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                        default:
                            if (toType == BigIntegerType) return true;
                            if (toType == ComplexType) return true;
                            return false;
                    }
                case TypeCode.Char:
                    switch (toType.GetTypeCode()) {
                        case TypeCode.UInt16:
                        case TypeCode.Int32:
                        case TypeCode.UInt32:
                        case TypeCode.Int64:
                        case TypeCode.UInt64:
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                        default:
                            if (toType == BigIntegerType) return true;
                            if (toType == ComplexType) return true;
                            return false;
                    }
                case TypeCode.Single:
                    switch (toType.GetTypeCode()) {
                        case TypeCode.Double:
                            return true;
                        default:
                            if (toType == ComplexType) return true;
                            return false;
                    }
                case TypeCode.Double:
                    switch (toType.GetTypeCode()) {
                        default:
                            if (toType == ComplexType) return true;
                            return false;
                    }
                default:
                    return false;
            }
        }

        public static Candidate PreferConvert(Type t1, Type t2) {
            if (t1 == typeof(bool) && t2 == typeof(int)) return Candidate.Two;
            if (t1 == typeof(decimal) && t2 == typeof(BigInteger)) return Candidate.Two;

            switch (t1.GetTypeCode()) {
                case TypeCode.SByte:
                    switch (t2.GetTypeCode()) {
                        case TypeCode.Byte:
                        case TypeCode.UInt16:
                        case TypeCode.UInt32:
                        case TypeCode.UInt64:
                            return Candidate.Two;
                        default:
                            return Candidate.Equivalent;
                    }
                case TypeCode.Int16:
                    switch (t2.GetTypeCode()) {
                        case TypeCode.UInt16:
                        case TypeCode.UInt32:
                        case TypeCode.UInt64:
                            return Candidate.Two;
                        default:
                            return Candidate.Equivalent;
                    }
                case TypeCode.Int32:
                    switch (t2.GetTypeCode()) {
                        case TypeCode.UInt32:
                        case TypeCode.UInt64:
                            return Candidate.Two;
                        default:
                            return Candidate.Equivalent;
                    }
                case TypeCode.Int64:
                    switch (t2.GetTypeCode()) {
                        case TypeCode.UInt64:
                            return Candidate.Two;
                        default:
                            return Candidate.Equivalent;
                    }
            }
            return Candidate.Equivalent;
        }

        private static bool HasNarrowingConversion(Type fromType, Type toType, NarrowingLevel allowNarrowing) {

            if (allowNarrowing >= PythonNarrowing.All) {
                if (IsPythonNumeric(fromType) && IsPythonNumeric(toType)) {
                    if (IsPythonFloatingPoint(fromType) && !IsPythonFloatingPoint(toType)) return false;
                    return true;
                }
                if (toType == Int32Type && HasPythonProtocol(fromType, "__int__")) return true;
                if (toType == DoubleType && HasPythonProtocol(fromType, "__float__")) return true;
                if (toType == BigIntegerType && HasPythonProtocol(fromType, "__int__")) return true;
            }

            if (allowNarrowing >= PythonNarrowing.IndexOperator) {
                if (IsNumeric(toType)) {
                    if (IsInteger(fromType)) return true;
                    if (fromType == BooleanType) return true; // bool is a subclass of int in Python
                }

                if (toType == CharType && fromType == StringType) return true;
                if (toType == StringType && fromType == CharType) return true;
                if (toType == Int32Type && fromType == BooleanType) return true;

                // Everything can convert to Boolean in Python
                if (toType == BooleanType) return true;

                if (DelegateType.IsAssignableFrom(toType) && IsPythonType(fromType)) return true;
                if (IEnumerableType == toType && IsPythonType(fromType)) return true;

                if (toType == typeof(IEnumerator)) {
                    if (IsPythonType(fromType)) return true;
                } else if (toType.IsGenericType) {
                    Type genTo = toType.GetGenericTypeDefinition();
                    if (genTo == IEnumerableOfTType) {
                        return IEnumerableOfObjectType.IsAssignableFrom(fromType) ||
                            IEnumerableType.IsAssignableFrom(fromType);
                    } else if (genTo == typeof(System.Collections.Generic.IEnumerator<>)) {
                        if (IsPythonType(fromType)) return true;
                    }
                }

                // Check if there is an implicit converter defined on fromType to toType
                if (HasImplicitConversion(fromType, toType)) {
                    return true;
                }
            }

            if (allowNarrowing >= PythonNarrowing.BinaryOperator) {
                if (toType == SingleType) {
                    if (IsNumeric(fromType) && fromType != ComplexType) return true;
                }

                if (toType.IsGenericType) {
                    Type genTo = toType.GetGenericTypeDefinition();
                    if (genTo == IListOfTType) {
                        return IListOfObjectType.IsAssignableFrom(fromType);
                    } else if (genTo == NullableOfTType) {
                        if (fromType == typeof(DynamicNull) || CanConvertFrom(fromType, toType.GetGenericArguments()[0], allowNarrowing)) {
                            return true;
                        }
                    } else if (genTo == IDictOfTType) {
                        return IDictionaryOfObjectType.IsAssignableFrom(fromType);
                    }
                }

                if (toType.IsArray) {
                    return typeof(PythonTuple).IsAssignableFrom(fromType);
                }
            }

            if (allowNarrowing >= PythonNarrowing.Minimal) {
                if (toType.IsEnum && fromType == Enum.GetUnderlyingType(toType)) return true;

                if (IsFloatingPoint(toType) && toType != SingleType) {
                    if (IsNumeric(fromType) && fromType != ComplexType) return true;
                }
            }

            return false;
        }

        internal static bool HasImplicitConversion(Type fromType, Type toType) {
            return
                HasImplicitConversionWorker(fromType, fromType, toType) ||
                HasImplicitConversionWorker(toType, fromType, toType);
        }

        private static bool HasImplicitConversionWorker(Type lookupType, Type fromType, Type toType) {
            while (lookupType != null) {
                foreach (MethodInfo method in lookupType.GetMethods()) {
                    if (method.Name == "op_Implicit" &&
                        method.GetParameters()[0].ParameterType.IsAssignableFrom(fromType) &&
                        toType.IsAssignableFrom(method.ReturnType)) {
                        return true;
                    }
                }
                lookupType = lookupType.BaseType;
            }
            return false;
        }

        /// <summary>
        /// Converts a value to int ignoring floats
        /// </summary>
        public static int? ImplicitConvertToInt32(object o) {
            if (o is int i32) {
                return i32;
            } else if (o is BigInteger bi) {
                if (bi.AsInt32(out int res)) {
                    return res;
                }
            } else if (o is Extensible<BigInteger>) {
                if (Converter.TryConvertToInt32(o, out int res)) {
                    return res;
                }
            }

            if (!(o is double) && !(o is float) && !(o is Extensible<double>)) {
                if (PythonTypeOps.TryInvokeUnaryOperator(DefaultContext.Default, o, "__int__", out object objres)) {
                    if (objres is int res) {
                        return res;
                    }
                }
            }

            return null;
        }

        internal static bool IsNumeric(Type t) {
            if (t.IsEnum) return false;

            switch (t.GetTypeCode()) {
                case TypeCode.DateTime:
                case TypeCode.DBNull:
                case TypeCode.Char:
                case TypeCode.Empty:
                case TypeCode.String:
                case TypeCode.Boolean:
                    return false;
                case TypeCode.Object:
                    return t == BigIntegerType || t == ComplexType;
                default:
                    return true;
            }
        }

        internal static bool IsFloatingPoint(Type t) {
            switch (t.GetTypeCode()) {
                case TypeCode.Double:
                case TypeCode.Single:
                case TypeCode.Decimal:
                    return true;
                case TypeCode.Object:
                    return t == ComplexType;
                default:
                    return false;
            }
        }

        internal static bool IsInteger(Type t) {
            return IsNumeric(t) && !IsFloatingPoint(t);
        }

        internal static bool IsUnsignedInt(Type t) {
            if (t.IsEnum) return false;

            switch (t.GetTypeCode()) {
                case TypeCode.Byte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return true;
            }

            return false;
        }

        internal static bool IsBoolean(Type t)
            => t == BooleanType;

        internal static bool IsExtensibleNumeric(Type t) {
            return ExtensibleBigIntegerType.IsAssignableFrom(t)
                || ExtensibleDoubleType.IsAssignableFrom(t)
                || ExtensibleComplexType.IsAssignableFrom(t);
        }

        internal static bool IsPythonNumeric(Type t)
            => IsNumeric(t) || IsExtensibleNumeric(t);

        internal static bool IsPythonFloatingPoint(Type t) {
            return IsFloatingPoint(t)
                || ExtensibleDoubleType.IsAssignableFrom(t)
                || ExtensibleComplexType.IsAssignableFrom(t);
        }

        internal static bool IsPythonBigInt(Type t)
            => t == BigIntegerType || ExtensibleBigIntegerType.IsAssignableFrom(t);

        private static bool IsPythonType(Type t) {
            return t.FullName.StartsWith("IronPython.", StringComparison.Ordinal); //!!! this and the check below are hacks
        }

        private static bool HasPythonProtocol(Type t, string name) {
            if (t.FullName.StartsWith(NewTypeMaker.TypePrefix, StringComparison.Ordinal)) return true;
            PythonType dt = DynamicHelpers.GetPythonTypeFromType(t);
            if (dt == null) return false;
            return dt.TryResolveSlot(DefaultContext.Default, name, out _);
        }
    }
}
