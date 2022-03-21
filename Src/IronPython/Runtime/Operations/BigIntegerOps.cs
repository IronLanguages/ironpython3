// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Types;

namespace IronPython.Runtime.Operations {

    public static partial class BigIntegerOps {

        #region Constructors

        private static object FastNew(CodeContext/*!*/ context, object o, int @base = 10) {
            object result;
            switch (o) {
                case int _:
                    return o;
                case BigInteger val:
                    return val.IsInt32() ? (int)val : o;
                case double d:
                    return DoubleOps.__int__(d);
                case bool b:
                    return BoolOps.__int__(b);
                case Extensible<BigInteger> ebi:
                    return TryInvokeInt(context, o, out result) ? result : ebi.Value.IsInt32() ? (object)(int)ebi.Value : ebi.Value;
                case float f:
                    return DoubleOps.__int__(f);
                case sbyte val:
                    return (int)val;
                case byte val:
                    return (int)val;
                case short val:
                    return (int)val;
                case ushort val:
                    return (int)val;
                case long val:
                    return int.MinValue <= val && val <= int.MaxValue ? (object)(int)val : (BigInteger)val;
                case uint val:
                    return val <= int.MaxValue ? (object)(int)val : (BigInteger)val;
                case ulong val:
                    return val <= int.MaxValue ? (object)(int)val : (BigInteger)val;
                case decimal val:
                    return int.MinValue <= val && val <= int.MaxValue ? (object)(int)val : (BigInteger)val;
                case Enum e:
                    return ((IConvertible)e).ToInt32(null);  // TODO: check long enums
                case string s:
                    return LiteralParser.ParseIntegerSign(s, @base, FindStart(s, @base));
                case Extensible<string> es:
                    return TryInvokeInt(context, o, out result) ? result : LiteralParser.ParseIntegerSign(es.Value, @base, FindStart(es.Value, @base));
                default:
                    break;
            }

            if (TryInvokeInt(context, o, out result)) {
                return result;
            } else if (PythonTypeOps.TryInvokeUnaryOperator(context, o, "__trunc__", out result)) {
                switch (result) {
                    case int _:
                        return result;
                    case BigInteger bi:
                        return bi.IsInt32() ? (object)(int)bi : result;
                    case bool b:
                        return BoolOps.__int__(b); // Python 3.6: return the int value
                    case Extensible<BigInteger> ebi:
                        return ebi.Value.IsInt32() ? (object)(int)ebi.Value : ebi.Value; // Python 3.6: return the int value
                    default: {
                            if (TryInvokeInt(context, result, out var intResult)) {
                                return intResult;
                            }
                            throw PythonOps.TypeError("__trunc__ returned non-Integral (type {0})", PythonOps.GetPythonTypeName(result));
                        }
                }
            }

            throw PythonOps.TypeError("int() argument must be a string, a bytes-like object or a number, not '{0}'", PythonOps.GetPythonTypeName(o));

            static bool TryInvokeInt(CodeContext context, object o, out object result) {
                if (PythonTypeOps.TryInvokeUnaryOperator(context, o, "__int__", out result)) {
                    switch (result) {
                        case int _:
                            return true;
                        case BigInteger bi:
                            if (bi.IsInt32()) result = (int)bi;
                            return true;
                        case bool b:
                            Warn(context, result);
                            result = BoolOps.__int__(b); // Python 3.6: return the int value
                            return true;
                        case Extensible<BigInteger> ebi:
                            Warn(context, result);
                            result = ebi.Value.IsInt32() ? (object)(int)ebi.Value : ebi.Value; // Python 3.6: return the int value
                            return true;
                        default:
                            throw PythonOps.TypeError("__int__ returned non-int (type {0})", PythonOps.GetPythonTypeName(result));
                    }

                    static void Warn(CodeContext context, object result) {
                        PythonOps.Warn(context, PythonExceptions.DeprecationWarning, $"__int__ returned non-int (type {PythonOps.GetPythonTypeName(result)}).  The ability to return an instance of a strict subclass of int is deprecated, and may be removed in a future version of Python.");
                    }
                }
                return false;
            }
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext context, PythonType cls, object x, object @base) {
            ValidateType(cls);

            var b = BaseFromObject(@base);

            if (!(x is string || x is Extensible<string>))
                throw PythonOps.TypeError("int() can't convert non-string with explicit base");

            return ReturnObject(context, cls, FastNew(context, x, b));
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext context, PythonType cls, object x) {
            ValidateType(cls);

            return ReturnObject(context, cls, FastNew(context, x));
        }

        // "int()" calls ReflectedType.Call(), which calls "Activator.CreateInstance" and return directly.
        // this is for derived int creation or direct calls to __new__...
        [StaticExtensionMethod]
        public static object __new__(CodeContext context, PythonType cls)
            => __new__(context, cls, ScriptingRuntimeHelpers.Int32ToObject(0));

        [StaticExtensionMethod]
        public static object __new__(CodeContext/*!*/ context, PythonType cls, [NotNull] IBufferProtocol x, int @base = 10) {
            ValidateType(cls);

            object value;
            if (!(x is IPythonObject po) || !PythonTypeOps.TryInvokeUnaryOperator(DefaultContext.Default, po, "__int__", out value)) {
                using IPythonBuffer buf = x.GetBufferNoThrow()
                    ?? throw PythonOps.TypeErrorForBadInstance("int() argument must be a string, a bytes-like object or a number, not '{0}'", x);

                var text = buf.AsReadOnlySpan().MakeString();
                if (!LiteralParser.TryParseIntegerSign(text, @base, FindStart(text, @base), out value))
                    throw PythonOps.ValueError($"invalid literal for int() with base {@base}: {new Bytes(x).__repr__(context)}");
            }

            return ReturnObject(context, cls, value);
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext/*!*/ context, PythonType cls, [NotNull] IBufferProtocol x, object @base)
            => __new__(context, cls, x, BaseFromObject(@base));

        private static void ValidateType(PythonType cls) {
            if (cls == TypeCache.Boolean)
                throw PythonOps.TypeError("int.__new__(bool) is not safe, use bool.__new__()");
        }

        private static int BaseFromObject(object @base) {
            switch (PythonOps.Index(@base)) {
                case int i:
                    return i;
                case BigInteger bi:
                    try {
                        return (int)bi;
                    } catch (OverflowException) {
                        return int.MaxValue;
                    }
                default:
                    throw new InvalidOperationException();
            }
        }

        private static object ReturnObject(CodeContext context, PythonType cls, object value)
            => cls == TypeCache.BigInteger ? value : cls.CreateInstance(context, value);

        private static int FindStart(string s, int radix) {
            int i = 0;

            // skip whitespace
            while (i < s.Length && char.IsWhiteSpace(s, i)) i++;

            // skip possible radix prefix
            if (i + 1 < s.Length && s[i] == '0') {
                switch (radix) {
                    case 16:
                        if (s[i + 1] == 'x' || s[i + 1] == 'X')
                            i += 2;
                        break;
                    case 8:
                        if (s[i + 1] == 'o' || s[i + 1] == 'O')
                            i += 2;
                        break;
                    case 2:
                        if (s[i + 1] == 'b' || s[i + 1] == 'B')
                            i += 2;
                        break;
                    default:
                        break;
                }
            }
            return i;
        }

        private static bool IsInt32(this BigInteger self)
            => int.MinValue <= self && self <= int.MaxValue;

        #endregion

        #region Binary operators

        [SpecialName]
        public static object Power(BigInteger x, object y, object z) {
            if (y is int) {
                return Power(x, (int)y, z);
            } else if (y is long) {
                return Power(x, (BigInteger)(long)y, z);
            } else if (y is BigInteger) {
                return Power(x, (BigInteger)y, z);
            }
            return NotImplementedType.Value;
        }

        [SpecialName]
        public static object Power(BigInteger x, int y, object z) {
            if (z is int) {
                return Power(x, y, (int)z);
            }

            if (z is long) {
                return Power(x, y, (long)z);
            }

            if (z is BigInteger) {
                return Power(x, y, (BigInteger)z);
            }

            if (z == null) {
                return Power(x, y);
            }

            return NotImplementedType.Value;
        }

        [SpecialName]
        public static object Power(BigInteger x, BigInteger y, object z) {
            if (z is int) {
                return Power(x, y, (BigInteger)(int)z);
            } else if (z is long) {
                return Power(x, y, (BigInteger)(long)z);
            } else if (z is BigInteger) {
                return Power(x, y, (BigInteger)z);
            } else if (z == null) {
                return Power(x, y);
            }
            return NotImplementedType.Value;
        }

        [SpecialName]
        public static object Power(BigInteger x, int y, BigInteger z) {
            if (y < 0) {
                throw PythonOps.TypeError("power", y, "power must be >= 0");
            }
            if (z == BigInteger.Zero) {
                throw PythonOps.ZeroDivisionError();
            }

            BigInteger result = x.ModPow(y, z);

            // fix the sign for negative moduli or negative mantissas
            if ((z < BigInteger.Zero && result > BigInteger.Zero)
                || (z > BigInteger.Zero && result < BigInteger.Zero)) {
                result += z;
            }
            return result;
        }

        [SpecialName]
        public static object Power(BigInteger x, BigInteger y, BigInteger z) {
            if (y < BigInteger.Zero) {
                throw PythonOps.TypeError("power", y, "power must be >= 0");
            }
            if (z == BigInteger.Zero) {
                throw PythonOps.ZeroDivisionError();
            }

            BigInteger result = x.ModPow(y, z);

            // fix the sign for negative moduli or negative mantissas
            if ((z < BigInteger.Zero && result > BigInteger.Zero)
                || (z > BigInteger.Zero && result < BigInteger.Zero)) {
                result += z;
            }
            return result;
        }


        [SpecialName]
        public static object Power([NotNull]BigInteger x, int y) {
            if (y < 0) {
                return DoubleOps.Power(x.ToFloat64(), y);
            }
            return x.Power(y);
        }

        [SpecialName]
        public static object Power([NotNull]BigInteger x, long y) {
            if(y < 0) {
                return DoubleOps.Power(x.ToFloat64(), y);
            }
            return x.Power(y);
        }

        [SpecialName]
        public static object Power([NotNull]BigInteger x, [NotNull]BigInteger y) {
            int yl;
            long y2;

            if (y.AsInt32(out yl)) {
                return Power(x, yl);
            } else if (y.AsInt64(out y2)) {
                return Power(x, y2);
            } else {
                if (x == BigInteger.Zero) {
                    if (y.Sign < 0)
                        throw PythonOps.ZeroDivisionError("0.0 cannot be raised to a negative power");
                    return BigInteger.Zero;
                } else if (x == BigInteger.One) {
                    return BigInteger.One;
                } else {
                    throw PythonOps.ValueError("Number too big");
                }
            }
        }

        private static BigInteger DivMod(BigInteger x, BigInteger y, out BigInteger r) {
            BigInteger rr;
            BigInteger qq = BigInteger.DivRem(x, y, out rr);

            if (x >= BigInteger.Zero) {
                if (y > BigInteger.Zero) {
                    r = rr;
                    return qq;
                } else {
                    if (rr == BigInteger.Zero) {
                        r = rr;
                        return qq;
                    } else {
                        r = rr + y;
                        return qq - BigInteger.One;
                    }
                }
            } else {
                if (y > BigInteger.Zero) {
                    if (rr == BigInteger.Zero) {
                        r = rr;
                        return qq;
                    } else {
                        r = rr + y;
                        return qq - BigInteger.One;
                    }
                } else {
                    r = rr;
                    return qq;
                }
            }
        }

        [SpecialName]
        public static BigInteger FloorDivide([NotNull]BigInteger x, [NotNull]BigInteger y) {
            return DivMod(x, y, out _);
        }

        [SpecialName]
        public static double TrueDivide([NotNull]BigInteger x, [NotNull]BigInteger y) {
            if (y == BigInteger.Zero) {
                throw new DivideByZeroException();
            }

            // first see if we can keep the two inputs as floats to give a precise result
            double fRes, fDiv;
            if (x.TryToFloat64(out fRes) && y.TryToFloat64(out fDiv)) {
                return fRes / fDiv;
            }

            // otherwise give the user the truncated result if the result fits in a float
            BigInteger rem;
            BigInteger res = BigInteger.DivRem(x, y, out rem);
            if (res.TryToFloat64(out fRes)) {
                if(rem != BigInteger.Zero) {
                    // scale remainder so that the fraction could be integer
                    BigInteger fraction = BigInteger.DivRem(rem  << 56, y, out rem); // adding 7 tailing zero bytes, bigger than sys.float_info.mant_dig
                    // round to nearest FPU
                    if (rem.IsPositive()) {
                        if (rem >= y / 2) {
                            fraction += 1;
                        }
                    } else {
                        if (rem <= -y / 2) {
                            fraction -= 1;
                        }
                    }

                    if (fraction.TryToFloat64(out fDiv)) {
                        fRes += fDiv / (1L << 56);
                    }
                }

                return fRes;
            }

            // otherwise report an error
            throw PythonOps.OverflowError("integer division result too large for a float");
        }

        [SpecialName]
        public static BigInteger Mod(BigInteger x, BigInteger y) {
            BigInteger r;
            DivMod(x, y, out r);
            return r;
        }

        [SpecialName]
        public static BigInteger LeftShift(BigInteger x, int y) {
            if (y < 0) {
                throw PythonOps.ValueError("negative shift count");
            }
            return x << y;
        }

        private static readonly bool hasShiftBug = BigInteger.Parse("-18446744073709543424") >> 32 == 0; // https://github.com/dotnet/runtime/issues/43396

        [SpecialName]
        public static BigInteger RightShift(BigInteger x, int y) {
            if (y < 0) {
                throw PythonOps.ValueError("negative shift count");
            }
            var res = x >> y;
            if (hasShiftBug && res.IsZero && x.IsNegative()) {
                Debug.Assert(y > 0); // bug does not occur when y is 0
                res = (x >> (y - 1)) >> 1;
            }
            return res;
        }

        [SpecialName]
        public static BigInteger LeftShift(BigInteger x, BigInteger y) {
            return LeftShift(x, (int)y);
        }

        [SpecialName]
        public static BigInteger RightShift(BigInteger x, BigInteger y) {
            return RightShift(x, (int)y);
        }

        #endregion

        [SpecialName]
        public static PythonTuple DivMod(BigInteger x, BigInteger y) {
            BigInteger mod;
            BigInteger div = DivMod(x, y, out mod);
            return PythonTuple.MakeTuple(div, mod);
        }

        #region Unary operators

        public static object __abs__(BigInteger x) {
            return x.Abs();
        }

        public static bool __bool__(BigInteger x) {
            return !x.IsZero();
        }

        [SpecialName]
        public static object Negate(BigInteger x) {
            return -x;
        }

        public static object __pos__(BigInteger x) {
            return x;
        }

        public static object __int__(BigInteger x) {
            // The python spec says __int__  should return a long if needed, rather than overflow.
            int i32;
            if (x.AsInt32(out i32)) {
                return Microsoft.Scripting.Runtime.ScriptingRuntimeHelpers.Int32ToObject(i32);
            }

            return x;
        }

        public static object __float__(BigInteger self) {
            return self.ToFloat64();
        }

        public static object __getnewargs__(CodeContext context, BigInteger self) {
            return PythonTuple.MakeTuple(BigIntegerOps.__new__(context, TypeCache.BigInteger, self));
        }

        #endregion

        #region Code generation helpers
        // These functions make the code generation of other integer types more regular.

        internal static BigInteger Add(BigInteger x, BigInteger y) => x + y;

        internal static BigInteger Subtract(BigInteger x, BigInteger y) => x - y;

        internal static BigInteger Multiply(BigInteger x, BigInteger y) => x * y;

        internal static BigInteger OnesComplement(BigInteger x) => ~x;

        internal static BigInteger BitwiseAnd(BigInteger x, BigInteger y) => x & y;

        internal static BigInteger BitwiseOr(BigInteger x, BigInteger y) => x | y;

        internal static BigInteger ExclusiveOr(BigInteger x, BigInteger y) => x ^ y;

        #endregion

        [PropertyMethod, SpecialName]
        public static BigInteger Getreal(BigInteger self) {
            return self;
        }
        [PropertyMethod, SpecialName]
        public static BigInteger Getimag(BigInteger self) {
            return (BigInteger)0;
        }
        public static BigInteger conjugate(BigInteger self) {
            return self;
        }
        [PropertyMethod, SpecialName]
        public static BigInteger Getnumerator(BigInteger self) {
            return self;
        }
        [PropertyMethod, SpecialName]
        public static BigInteger Getdenominator(BigInteger self) {
            return 1;
        }

        public static int bit_length(BigInteger self) {
            return MathUtils.BitLength(self);
        }

        public static BigInteger __trunc__(BigInteger self) {
            return self;
        }

        [SpecialName, ImplicitConversionMethod]
        public static double ConvertToDouble(BigInteger self) {
            return self.ToFloat64();
        }

        [SpecialName, ExplicitConversionMethod]
        public static int ConvertToInt32(BigInteger self) {
            int res;
            if (self.AsInt32(out res)) return res;

            throw Converter.CannotConvertOverflow("int", self);
        }

        [SpecialName, ExplicitConversionMethod]
        public static Complex ConvertToComplex(BigInteger self) {
            return MathUtils.MakeReal(ConvertToDouble(self));
        }

        [SpecialName, ImplicitConversionMethod]
        public static BigInteger ConvertToBigInteger(bool self) {
            return self ? BigInteger.One : BigInteger.Zero;
        }

        [SpecialName]
        public static bool LessThan(BigInteger x, BigInteger y) => x < y;
        [SpecialName]
        public static bool LessThanOrEqual(BigInteger x, BigInteger y) => x <= y;
        [SpecialName]
        public static bool GreaterThan(BigInteger x, BigInteger y) => x > y;
        [SpecialName]
        public static bool GreaterThanOrEqual(BigInteger x, BigInteger y) => x >= y;
        [SpecialName]
        public static bool Equals(BigInteger x, BigInteger y) => x == y;
        [SpecialName]
        public static bool NotEquals(BigInteger x, BigInteger y) => x != y;

        [SpecialName]
        public static bool LessThan(BigInteger x, int y) => x < y;
        [SpecialName]
        public static bool LessThanOrEqual(BigInteger x, int y) => x <= y;
        [SpecialName]
        public static bool GreaterThan(BigInteger x, int y) => x > y;
        [SpecialName]
        public static bool GreaterThanOrEqual(BigInteger x, int y) => x >= y;
        [SpecialName]
        public static bool Equals(BigInteger x, int y) => x == y;
        [SpecialName]
        public static bool NotEquals(BigInteger x, int y) => x != y;

        [SpecialName]
        public static bool LessThan(BigInteger x, uint y) => x < y;
        [SpecialName]
        public static bool LessThanOrEqual(BigInteger x, uint y) => x <= y;
        [SpecialName]
        public static bool GreaterThan(BigInteger x, uint y) => x > y;
        [SpecialName]
        public static bool GreaterThanOrEqual(BigInteger x, uint y) => x >= y;
        [SpecialName]
        public static bool Equals(BigInteger x, uint y) => x == y;
        [SpecialName]
        public static bool NotEquals(BigInteger x, uint y) => x != y;

        public static BigInteger __index__(BigInteger self) {
            return self;
        }

        public static int __hash__(BigInteger self) {
            // check if it's in the Int64 or UInt64 range, and use the built-in hashcode for that instead
            // this ensures that objects added to dictionaries as (U)Int64 can be looked up with Python longs
            if (self.AsInt64(out long i64)) {
                return Int64Ops.__hash__(i64);
            } else if (self.AsUInt64(out ulong u64)) {
                return UInt64Ops.__hash__(u64);
            }

            if (self.IsNegative()) {
                self = -self;
                var h = unchecked(-(int)((self >= int.MaxValue) ? (self % int.MaxValue) : self));
                if (h == -1) return -2;
                return h;
            }
            return unchecked((int)((self >= int.MaxValue) ? (self % int.MaxValue) : self));
        }

        public static string __repr__([NotNull]BigInteger/*!*/ self) {
            return self.ToString();
        }

        #region Direct Conversions

        [PythonHidden]
        public static BigInteger ToBigInteger(BigInteger self) {
            return self;
        }

        #endregion

        #region Mimic some IConvertible members

        [PythonHidden]
        public static bool ToBoolean(BigInteger self, IFormatProvider provider) {
            return !self.IsZero;
        }

        [PythonHidden]
        public static byte ToByte(BigInteger self, IFormatProvider provider) {
            return (byte)self;
        }

        [CLSCompliant(false), PythonHidden]
        public static sbyte ToSByte(BigInteger self, IFormatProvider provider) {
            return (sbyte)self;
        }

        [PythonHidden]
        public static char ToChar(BigInteger self, IFormatProvider provider) {
            int res;
            if (self.AsInt32(out res) && res <= Char.MaxValue && res >= Char.MinValue) {
                return (char)res;
            }
            throw new OverflowException("big integer won't fit into char");
        }

        [PythonHidden]
        public static decimal ToDecimal(BigInteger self, IFormatProvider provider) {
            return (decimal)self;
        }

        [PythonHidden]
        public static double ToDouble(BigInteger self, IFormatProvider provider) {
            return ConvertToDouble(self);
        }

        [PythonHidden]
        public static float ToSingle(BigInteger self, IFormatProvider provider) {
            return checked((float)self.ToFloat64());
        }

        [PythonHidden]
        public static short ToInt16(BigInteger self, IFormatProvider provider) {
            return (short)self;
        }

        [PythonHidden]
        public static int ToInt32(BigInteger self, IFormatProvider provider) {
            return (int)self;
        }

        [PythonHidden]
        public static long ToInt64(BigInteger self, IFormatProvider provider) {
            return (long)self;
        }

        [CLSCompliant(false), PythonHidden]
        public static ushort ToUInt16(BigInteger self, IFormatProvider provider) {
            return (ushort)self;
        }

        [CLSCompliant(false), PythonHidden]
        public static uint ToUInt32(BigInteger self, IFormatProvider provider) {
            return (uint)self;
        }

        [CLSCompliant(false), PythonHidden]
        public static ulong ToUInt64(BigInteger self, IFormatProvider provider) {
            return (ulong)self;
        }

        [PythonHidden]
        public static object ToType(BigInteger self, Type conversionType, IFormatProvider provider) {
            if (conversionType == typeof(BigInteger)) {
                return self;
            }
            throw new NotImplementedException();
        }

        [PythonHidden]
        public static TypeCode GetTypeCode(BigInteger self) {
            return TypeCode.Object;
        }

        #endregion

        public static string/*!*/ __format__(CodeContext/*!*/ context, BigInteger/*!*/ self, [NotNull]string/*!*/ formatSpec) {
            StringFormatSpec spec = StringFormatSpec.FromString(formatSpec);

            if (spec.Precision != null) {
                throw PythonOps.ValueError("Precision not allowed in integer format specifier");
            }

            BigInteger val = self;
            if (self < 0) {
                val = -self;
            }
            string digits;

            switch (spec.Type) {
                case 'n':
                    CultureInfo culture = context.LanguageContext.NumericCulture;

                    if (culture == CultureInfo.InvariantCulture) {
                        // invariant culture maps to CPython's C culture, which doesn't
                        // include any formatting info.
                        goto case 'd';
                    }

                    digits = FormattingHelper.ToCultureString(val, context.LanguageContext.NumericCulture.NumberFormat, spec);
                    break;
                case null:
                case 'd':
                    if (spec.ThousandsComma) {
                        var width = spec.Width ?? 0;
                        // If we're inserting commas, and we're padding with leading zeros.
                        // AlignNumericText won't know where to place the commas,
                        // so force .Net to help us out here.
                        if (spec.Fill.HasValue && spec.Fill.Value == '0' && width > 1) {
                            digits = val.ToString(FormattingHelper.ToCultureString(self, FormattingHelper.InvariantCommaNumberInfo, spec));
                        }
                        else {
                        digits = val.ToString("#,0", CultureInfo.InvariantCulture);
                        }
                    }
                    else {
                        digits = val.ToString("D", CultureInfo.InvariantCulture);
                    }
                    break;
                case '%':
                    if (spec.ThousandsComma) {
                        digits = val.ToString("#,0.000000%", CultureInfo.InvariantCulture);
                    } else {
                        digits = val.ToString("0.000000%", CultureInfo.InvariantCulture);
                    }
                    break;
                case 'e':
                    if (spec.ThousandsComma) {
                        digits = val.ToString("#,0.000000e+00", CultureInfo.InvariantCulture);
                    } else {
                        digits = val.ToString("0.000000e+00", CultureInfo.InvariantCulture);
                    }
                    break;
                case 'E':
                    if (spec.ThousandsComma) {
                        digits = val.ToString("#,0.000000E+00", CultureInfo.InvariantCulture);
                    } else {
                        digits = val.ToString("0.000000E+00", CultureInfo.InvariantCulture);
                    }
                    break;
                case 'f':
                case 'F':
                    if (spec.ThousandsComma) {
                        digits = val.ToString("#,########0.000000", CultureInfo.InvariantCulture);
                    } else {
                        digits = val.ToString("#########0.000000", CultureInfo.InvariantCulture);
                    }
                    break;
                case 'g':
                    if (val >= 1000000) {
                        digits = val.ToString("0.#####e+00", CultureInfo.InvariantCulture);
                    } else if (spec.ThousandsComma) {
                        goto case 'd';
                    } else {
                        digits = val.ToString(CultureInfo.InvariantCulture);
                    }
                    break;
                case 'G':
                    if (val >= 1000000) {
                        digits = val.ToString("0.#####E+00", CultureInfo.InvariantCulture);
                    } else if (spec.ThousandsComma) {
                        goto case 'd';
                    } else {
                        digits = val.ToString(CultureInfo.InvariantCulture);
                    }
                    break;
                case 'X':
                    digits = AbsToHex(val, false);
                    break;
                case 'x':
                    digits = AbsToHex(val, true);
                    break;
                case 'o': // octal
                    digits = ToOctal(val, true);
                    break;
                case 'b': // binary
                    digits = ToBinary(val, false, true);
                    break;
                case 'c': // single char
                    int iVal;
                    if (spec.Sign != null) {
                        throw PythonOps.ValueError("Sign not allowed with integer format specifier 'c'");
                    } else if (!self.AsInt32(out iVal)) {
                        throw PythonOps.OverflowError("Python int too large to convert to System.Int32");
                    } else if(iVal < 0 || iVal > 0x10ffff) {
                        throw PythonOps.OverflowError("%c arg not in range(0x110000)");
                    }

                    digits = (iVal > char.MaxValue) ? char.ConvertFromUtf32(iVal) : ScriptingRuntimeHelpers.CharToString((char)iVal);
                    break;
                default:
                    throw PythonOps.ValueError("Unknown format code '{0}' for object of type 'int'", spec.TypeRepr);
            }

            Debug.Assert(digits[0] != '-');

            return spec.AlignNumericText(digits, self.IsZero(), self.IsPositive());
        }

        public static Bytes to_bytes(BigInteger value, int length, string byteorder, bool signed = false) {
            // TODO: signed should be a keyword only argument
            // TODO: should probably be moved to IntOps.Generated and included in all types

            if (length < 0) throw PythonOps.ValueError("length argument must be non-negative");
            if (!signed && value < 0) throw PythonOps.OverflowError("can't convert negative int to unsigned");

            bool isLittle = byteorder == "little";
            if (!isLittle && byteorder != "big") throw PythonOps.ValueError("byteorder must be either 'little' or 'big'");

            var reqLength = (bit_length(value) + (signed ? 1 : 0)) / 8;
            if (reqLength > length) throw PythonOps.OverflowError("int too big to convert");

            var bytes = value.ToByteArray();
            IEnumerable<byte> res = bytes;
            if (length > bytes.Length) res = res.Concat(Enumerable.Repeat<byte>((value < 0) ? (byte)0xff : (byte)0, length - bytes.Length));
            else if (length < bytes.Length) res = res.Take(length);
            if (!isLittle) res = res.Reverse();

            return Bytes.Make(res.ToArray());
        }

        [ClassMethod, StaticExtensionMethod]
        public static object from_bytes(CodeContext context, PythonType type, object bytes, [NotNull] string byteorder, bool signed = false) {
            // TODO: signed should be a keyword only argument

            bool isLittle = byteorder == "little";
            if (!isLittle && byteorder != "big") throw PythonOps.ValueError("byteorder must be either 'little' or 'big'");

            byte[] bytesArr = Bytes.FromObject(context, bytes).UnsafeByteArray;
            if (bytesArr.Length == 0) return 0;

#if NETCOREAPP
            var val = new BigInteger(bytesArr.AsSpan(), isUnsigned: !signed, isBigEndian: !isLittle);
#else
            if (!isLittle) bytesArr = bytesArr.Reverse();
            if (!signed && (bytesArr[bytesArr.Length - 1] & 0x80) == 0x80) Array.Resize(ref bytesArr, bytesArr.Length + 1);
            var val = new BigInteger(bytesArr);
#endif

            // prevents a TypeError: int.__new__(bool) is not safe
            if (type == TypeCache.Boolean) return val == 0 ? ScriptingRuntimeHelpers.False : ScriptingRuntimeHelpers.True;

            return __new__(context, type, val);
        }

        public static BigInteger __round__(BigInteger number) {
            return number;
        }

        public static BigInteger __round__(BigInteger self, BigInteger ndigits) {
            // as of Python 3 rounding is to the nearest even number, not away from zero
            if (ndigits >= 0) {
                return self;
            }

            if (!ndigits.AsInt32(out var intNDigits)) {
                // probably the best course of action. anyone trying this is in for trouble anyway.
                return BigInteger.Zero;
            }

            // see https://bugs.python.org/issue4707#msg78141
            var i = BigInteger.Pow(10, -intNDigits);
            var r = Mod(self, 2 * i);
            var o = i / 2;
            self -= r;

            if (r <= o) {
                return self;
            } else if (r < 3 * o) {
                return self + i;
            } else {
                return self + 2 * i;
            }
        }

        public static BigInteger __round__(BigInteger self, object ndigits) {
            var index = PythonOps.Index(ndigits);
            switch (index) {
                case int i:
                    return __round__(self, i);

                case BigInteger bi:
                    return __round__(self, bi);
            }

            throw PythonOps.RuntimeError(
                "Unreachable code was reached. "
                + "PythonOps.Index is guaranteed to either throw or return an integral value.");
        }

        internal static string AbsToHex(BigInteger val, bool lowercase) {
            return ToDigits(val, 16, lowercase);
        }

        private static string ToOctal(BigInteger val, bool lowercase) {
            return ToDigits(val, 8, lowercase);
        }

        internal static string ToBinary(BigInteger val) {
            string res = ToBinary(val.Abs(), true, true);
            if (val.IsNegative()) {
                res = "-" + res;
            }
            return res;
        }

        private static string ToBinary(BigInteger val, bool includeType, bool lowercase) {
            Debug.Assert(!val.IsNegative());

            string digits = ToDigits(val, 2, lowercase);

            if (includeType) {
                digits = (lowercase ? "0b" : "0B") + digits;
            }
            return digits;
        }

        private static string/*!*/ ToDigits(BigInteger/*!*/ val, int radix, bool lower) {
            if (val.IsZero()) {
                return "0";
            }

            StringBuilder str = new StringBuilder();

            while (val != 0) {
                int digit = (int)(val % radix);
                if (digit < 10) str.Append((char)((digit) + '0'));
                else if (lower) str.Append((char)((digit - 10) + 'a'));
                else str.Append((char)((digit - 10) + 'A'));

                val /= radix;
            }

            StringBuilder res = new StringBuilder(str.Length);
            for (int i = str.Length - 1; i >= 0; i--) {
                res.Append(str[i]);
            }

            return res.ToString();
        }
    }
}
