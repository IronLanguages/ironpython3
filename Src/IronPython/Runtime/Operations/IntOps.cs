// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Types;

using SpecialNameAttribute = System.Runtime.CompilerServices.SpecialNameAttribute;

namespace IronPython.Runtime.Operations {

    public static partial class Int32Ops {

        #region Unary Operations

        public static object __getnewargs__(CodeContext context, int self) {
            return PythonTuple.MakeTuple(Int32Ops.__new__(TypeCache.Int32, self));
        }

        public static int __round__(int self) {
            return self;
        }

        public static object __round__(int number, BigInteger ndigits) {
            var result = BigIntegerOps.__round__(new BigInteger(number), ndigits);
            if (result.AsInt32(out var ret)) {
                return ret;
            }

            // this path can be hit when number is close to int.MaxValue and ndigits is negative,
            // causing number to be rounded up and over int.MaxValue
            return result;
        }

        public static object __round__(int self, object ndigits) {
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

        #endregion

        #region Binary and Ternary Operations - Arithmetic

        [SpecialName]
        public static object FloorDivide(int x, int y) {
            if (y == -1 && x == Int32.MinValue) {
                return -(BigInteger)Int32.MinValue;
            }
            return ScriptingRuntimeHelpers.Int32ToObject(MathUtils.FloorDivideUnchecked(x, y));
        }

        [SpecialName]
        public static int Mod(int x, int y) {
            return MathUtils.FloorRemainder(x, y);
        }

        public static PythonTuple __divmod__(int x, int y) {
            return PythonTuple.MakeTuple(FloorDivide(x, y), Mod(x, y));
        }

        [return: MaybeNotImplemented]
        public static object __divmod__(int x, object y) {
            return NotImplementedType.Value;
        }

        public static object __rdivmod__(int x, int y) {
            return __divmod__(y, x);
        }

        [SpecialName]
        public static object Power(int x, BigInteger power, BigInteger qmod) {
            return BigIntegerOps.Power((BigInteger)x, power, qmod);
        }

        [SpecialName]
        public static object Power(int x, double power, double qmod) {
            return NotImplementedType.Value;
        }

        [SpecialName]
        public static object Power(int x, int power, int? qmod) {
            if (qmod == null) return Power(x, power);
            int mod = (int)qmod;

            if (power < 0) throw PythonOps.ValueError("power must be >= 0");

            if (mod == 0) {
                throw PythonOps.ZeroDivisionError();
            }

            // This is "exponentiation by squaring" (described in Applied Cryptography; a log-time algorithm)
            long result = 1 % mod; // Handle special case of power=0, mod=1
            long factor = x;
            while (power != 0) {
                if ((power & 1) != 0) result = (result * factor) % mod;
                factor = (factor * factor) % mod;
                power >>= 1;
            }

            // fix the sign for negative moduli or negative mantissas
            if ((mod < 0 && result > 0) || (mod > 0 && result < 0)) {
                result += mod;
            }
            return (int)result;
        }

        [SpecialName]
        public static object Power(int x, int power) {
            if (power == 0) return 1;
            if (power < 0) {
                if (x == 0)
                    throw PythonOps.ZeroDivisionError("0.0 cannot be raised to a negative power");
                return DoubleOps.Power(x, power);
            }
            int factor = x;
            int result = 1;
            int savePower = power;
            try {
                checked {
                    while (power != 0) {
                        if ((power & 1) != 0) result = result * factor;
                        if (power == 1) break; // prevent overflow
                        factor = factor * factor;
                        power >>= 1;
                    }
                    return result;
                }
            } catch (OverflowException) {
                return BigIntegerOps.Power(x, savePower);
            }
        }


        #endregion

        #region Binary Operations - Bitwise

        [SpecialName]
        public static object LeftShift(int x, int y) {
            if (y < 0) {
                throw PythonOps.ValueError("negative shift count");
            }
            if (y > 31 ||
                (x > 0 && x > (Int32.MaxValue >> y)) ||
                (x < 0 && x < (Int32.MinValue >> y))) {
                return Int64Ops.LeftShift((long)x, y);
            }
            return ScriptingRuntimeHelpers.Int32ToObject(x << y);
        }

        [SpecialName]
        public static int RightShift(int x, int y) {
            if (y < 0) {
                throw PythonOps.ValueError("negative shift count");
            }
            if (y > 31) {
                return x >= 0 ? 0 : -1;
            }

            int q;

            if (x >= 0) {
                q = x >> y;
            } else {
                q = (x + ((1 << y) - 1)) >> y;
                int r = x - (q << y);
                if (r != 0) q--;
            }

            return q;
        }

        #endregion

        #region Public API - Numerics

        [PythonHidden]
        public static BigInteger ToBigInteger(this int self) {
            return self;
        }

        #endregion

        #region Public API - String/Bytes

        public static string __format__(CodeContext/*!*/ context, int self, [NotNone] string/*!*/ formatSpec) {
            if (self == int.MinValue) return BigIntegerOps.__format__(context, self, formatSpec);

            StringFormatSpec spec = StringFormatSpec.FromString(formatSpec);

            if (spec.Precision != null) {
                throw PythonOps.ValueError("Precision not allowed in integer format specifier");
            }

            int val = Math.Abs(self);

            string digits;

            switch (spec.Type) {
                case 'n':
                    CultureInfo culture = context.LanguageContext.NumericCulture;

                    if (culture == CultureInfo.InvariantCulture) {
                        // invariant culture maps to CPython's C culture, which doesn't
                        // include any formatting info.
                        goto case 'd';
                    }

                    // If we're padding with leading zeros and we might be inserting
                    // culture sensitive number group separators. (i.e. commas)
                    // So use FormattingHelper.ToCultureString for that support.
                    if (spec.Fill == '0' && spec.Width > 1) {
                        digits = FormattingHelper.ToCultureString(val, culture.NumberFormat, spec, (spec.Sign != null && spec.Sign != '-' || self < 0) ? spec.Width - 1 : null);
                    } else {
                        digits = val.ToString("N0", culture);
                    }
                    break;
                case null:
                case 'd':
                    if (spec.ThousandsComma || spec.ThousandsUnderscore) {
                        var numberFormat = spec.ThousandsUnderscore ? FormattingHelper.InvariantUnderscoreNumberInfo : CultureInfo.InvariantCulture.NumberFormat;

                        // If we're inserting commas, and we're padding with leading zeros.
                        // AlignNumericText won't know where to place the commas,
                        // so use FormattingHelper.ToCultureString for that support.
                        if (spec.Fill == '0' && spec.Width > 1) {
                            digits = FormattingHelper.ToCultureString(val, numberFormat, spec, (spec.Sign != null && spec.Sign != '-' || self < 0) ? spec.Width - 1 : null);
                        } else {
                            digits = val.ToString("#,0", numberFormat);
                        }
                    } else {
                        digits = val.ToString("D", CultureInfo.InvariantCulture);
                    }
                    break;
                case '%':
                case 'e':
                case 'E':
                case 'f':
                case 'F':
                case 'g':
                case 'G':
                    digits = DoubleOps.DoubleToFormatString(context, val, spec);
                    break;
                case 'X':
                    digits = ToHexDigits(val, lowercase: false);
                    if (spec.ThousandsUnderscore) {
                        digits = FormattingHelper.AddUnderscores(digits, spec, self < 0);
                    }
                    break;
                case 'x':
                    digits = ToHexDigits(val, lowercase: true);
                    if (spec.ThousandsUnderscore) {
                        digits = FormattingHelper.AddUnderscores(digits, spec, self < 0);
                    }
                    break;
                case 'o': // octal
                    digits = ToOctalDigits(val);
                    if (spec.ThousandsUnderscore) {
                        digits = FormattingHelper.AddUnderscores(digits, spec, self < 0);
                    }
                    break;
                case 'b': // binary
                    digits = ToBinaryDigits(val) ;
                    if (spec.ThousandsUnderscore) {
                        digits = FormattingHelper.AddUnderscores(digits, spec, self < 0);
                    }
                    break;
                case 'c': // single char
                    if (spec.Sign != null) {
                        throw PythonOps.ValueError("Sign not allowed with integer format specifier 'c'");
                    }

                    if (spec.AlternateForm) {
                        throw PythonOps.ValueError("Alternate form(#) not allowed with integer format specifier 'c'");
                    }

                    if (self < 0 || self > 0x10ffff) {
                        throw PythonOps.OverflowError("%c arg not in range(0x110000)");
                    }

                    digits = (self > char.MaxValue) ? char.ConvertFromUtf32(self) : ScriptingRuntimeHelpers.CharToString((char)self);
                    break;
                default:
                    throw PythonOps.ValueError("Unknown format code '{0}' for object of type 'int'", spec.TypeRepr);
            }

            Debug.Assert(digits[0] != '-');

            return spec.AlignNumericText(digits, self == 0, self > 0);
        }

        [ClassMethod, StaticExtensionMethod]
        public static object from_bytes(CodeContext context, PythonType type, object bytes, [NotNone] string byteorder, bool signed = false)
            // TODO: signed should be a keyword only argument
            => BigIntegerOps.from_bytes(context, type, bytes, byteorder, signed);

        #endregion

        #region Helpers

        private static string ToHexDigits(int val, bool lowercase) {
            Debug.Assert(val >= 0);
            return val.ToString(lowercase ? "x" : "X", CultureInfo.InvariantCulture);
        }

        private static string ToOctalDigits(int val) {
            Debug.Assert(val >= 0);
            if (val == 0) return "0";

            StringBuilder sb = new StringBuilder();
            for (int i = 30; i >= 0; i -= 3) {
                char value = (char)('0' + (val >> i & 0x07));
                if (value != '0' || sb.Length > 0) {
                    sb.Append(value);
                }
            }
            return sb.ToString();
        }

        private static string ToBinaryDigits(int val) {
            Debug.Assert(val >= 0);
            if (val == 0) return "0";

            StringBuilder sb = new StringBuilder();
            for (int i = 31; i >= 0; i--) {
                if ((val & (1 << i)) != 0) {
                    sb.Append('1');
                } else if (sb.Length != 0) {
                    sb.Append('0');
                }
            }
            return sb.ToString();
        }

        internal static string ToBinary(int val) {
            if (val == int.MinValue) {
                return "-0b10000000000000000000000000000000";
            }

            var digits = ToBinaryDigits(Math.Abs(val));
            return ((val < 0) ? "-0b" : "0b") + digits;
        }

        #endregion

        #region Mimic BigInteger members
        // Ideally only on instances

        #region Properties

        [SpecialName, PropertyMethod, PythonHidden]
        public static bool GetIsEven(int self) => (self & 1) == 0;

        [SpecialName, PropertyMethod, PythonHidden]
        public static bool GetIsOne(int self) => self == 1;

        [SpecialName, PropertyMethod, PythonHidden]
        public static bool GetIsPowerOfTwo(int self) => self > 0 && (self & (self - 1)) == 0;

        [SpecialName, PropertyMethod, PythonHidden]
        public static bool GetIsZero(int self) => self == 0;

        [SpecialName, PropertyMethod, PythonHidden]
        public static int GetSign(int self) => self == 0 ? 0 : self > 0 ? 1 : -1;

        [PythonHidden]
        public static object Zero => ScriptingRuntimeHelpers.Int32ToObject(0);

        [PythonHidden]
        public static object One => ScriptingRuntimeHelpers.Int32ToObject(1);

        [PythonHidden]
        public static object MinusOne => ScriptingRuntimeHelpers.Int32ToObject(-1);

        #endregion

        #region Methods

        [PythonHidden]
        public static byte[] ToByteArray(int self) => new BigInteger(self).ToByteArray();

#if NETCOREAPP
        [PythonHidden]
        public static byte[] ToByteArray(int self, bool isUnsigned = false, bool isBigEndian = false) => new BigInteger(self).ToByteArray(isUnsigned, isBigEndian);

        [PythonHidden]
        public static int GetByteCount(int self, bool isUnsigned = false) => new BigInteger(self).GetByteCount(isUnsigned);

        [PythonHidden]
        public static bool TryWriteBytes(int self, Span<byte> destination, out int bytesWritten, bool isUnsigned = false, bool isBigEndian = false)
            => new BigInteger(self).TryWriteBytes(destination, out bytesWritten, isUnsigned, isBigEndian);

#elif NETSTANDARD
        [PythonHidden]
        public static byte[] ToByteArray(int self, bool isUnsigned = false, bool isBigEndian = false) {
            if (self < 0 && isUnsigned) throw new OverflowException("Negative values do not have an unsigned representation.");
            byte[] bytes = ToByteArray(self);
            int count = bytes.Length;
            if (isUnsigned && count > 1 && bytes[count - 1] == 0) Array.Resize(ref bytes, count - 1);
            if (isBigEndian) Array.Reverse(bytes);
            return bytes;
        }

        [PythonHidden]
        public static int GetByteCount(int self, bool isUnsigned = false) => ToByteArray(self, isUnsigned).Length;

        [PythonHidden]
        public static bool TryWriteBytes(int self, Span<byte> destination, out int bytesWritten, bool isUnsigned = false, bool isBigEndian = false) {
            bytesWritten = 0;
            byte[] bytes = ToByteArray(self, isUnsigned, isBigEndian);
            if (bytes.Length > destination.Length) return false;

            bytes.AsSpan().CopyTo(destination);
            bytesWritten = bytes.Length;
            return true;
        }
#endif

#if NET || NETSTANDARD
        [PythonHidden]
        public static long GetBitLength(int self) {
            int length = MathUtils.BitLength(self);
            if (self < 0 && (self == int.MinValue || GetIsPowerOfTwo(-self))) length--;
            return length;
        }
#endif

        #endregion

        #region Static Extension Methods

        [StaticExtensionMethod, PythonHidden]
        public static BigInteger Compare(BigInteger left, BigInteger right) => BigInteger.Compare(left, right);

        [StaticExtensionMethod, PythonHidden]
        public static BigInteger Min(BigInteger left, BigInteger right) => BigInteger.Min(left, right);

        [StaticExtensionMethod, PythonHidden]
        public static BigInteger Max(BigInteger left, BigInteger right) => BigInteger.Max(left, right);

        [StaticExtensionMethod, PythonHidden]
        public static double Log(BigInteger value) => BigInteger.Log(value);

        [StaticExtensionMethod, PythonHidden]
        public static double Log(BigInteger value, double baseValue) => BigInteger.Log(value, baseValue);

        [StaticExtensionMethod, PythonHidden]
        public static double Log10(BigInteger value) => BigInteger.Log10(value);

        [StaticExtensionMethod, PythonHidden]
        public static object Pow(BigInteger value, int exponent) => BigInteger.Pow(value, exponent);

        [StaticExtensionMethod, PythonHidden]
        public static object ModPow(BigInteger value, BigInteger exponent, BigInteger modulus) => BigInteger.ModPow(value, exponent, modulus);

        [StaticExtensionMethod, PythonHidden]
        public static BigInteger Negate(BigInteger value) => BigInteger.Negate(value);

        [StaticExtensionMethod, PythonHidden]
        public static BigInteger Abs(BigInteger value) => BigInteger.Abs(value);

        [StaticExtensionMethod, PythonHidden]
        public static BigInteger Add(BigInteger left, BigInteger right) => BigInteger.Add(left, right);

        [StaticExtensionMethod, PythonHidden]
        public static BigInteger Subtract(BigInteger left, BigInteger right) => BigInteger.Subtract(left, right);

        [StaticExtensionMethod, PythonHidden]
        public static BigInteger Multiply(BigInteger left, BigInteger right) => BigInteger.Multiply(left, right);

        [StaticExtensionMethod, PythonHidden]
        public static BigInteger Divide(BigInteger left, BigInteger right) => BigInteger.Divide(left, right);

        [StaticExtensionMethod, PythonHidden]
        public static BigInteger Remainder(BigInteger dividend, BigInteger divisor) => BigInteger.Remainder(dividend, divisor);

        [StaticExtensionMethod, PythonHidden]
        public static BigInteger DivRem(BigInteger dividend, BigInteger divisor, out BigInteger remainder) => BigInteger.DivRem(dividend, divisor, out remainder);

        [StaticExtensionMethod, PythonHidden]
        public static BigInteger GreatestCommonDivisor(BigInteger left, BigInteger right) => BigInteger.GreatestCommonDivisor(left, right);

        #endregion

        #endregion
    }

    public static partial class Int64Ops {

        #region Public API - Bytes

        public static Bytes to_bytes(Int64 value, int length, [NotNone] string byteorder, bool signed = false) {
            // TODO: signed should be a keyword only argument
            bool isLittle = (byteorder == "little");
            if (!isLittle && byteorder != "big") throw PythonOps.ValueError("byteorder must be either 'little' or 'big'");

            if (length < 0) throw PythonOps.ValueError("length argument must be non-negative");
            if (!signed && value < 0) throw PythonOps.OverflowError("can't convert negative int to unsigned");

            if (value == 0) return Bytes.Make(new byte[length]);

            var bytes = new byte[length];
            int cur, end, step;
            if (isLittle) {
                cur = 0; end = length; step = 1;
            } else {
                cur = length - 1; end = -1; step = -1;
            }

            if (!signed || value >= 0) {
                ulong uvalue = unchecked((ulong)value);
                do {
                    if (cur == end) ThrowOverflow();
                    bytes[cur] = (byte)(uvalue & 0xFF);
                    uvalue >>= 8;
                    cur += step;
                } while (uvalue != 0);
            } else {
                byte curbyte;
                do {
                    if (cur == end) ThrowOverflow();
                    bytes[cur] = curbyte = (byte)(value & 0xFF);
                    value >>= 8;
                    cur += step;
                } while (value != -1 || (curbyte & 0x80) == 0);

                while (cur != end) {
                    bytes[cur] = 0xFF;
                    cur += step;
                }
            }

            return Bytes.Make(bytes);

            static void ThrowOverflow() => throw PythonOps.OverflowError("int too big to convert");
        }

        #endregion
    }

    public static partial class UInt64Ops {

        #region Public API - Bytes

        public static Bytes to_bytes(UInt64 value, int length, [NotNone] string byteorder, bool signed = false) {
            bool isLittle = (byteorder == "little");
            if (!isLittle && byteorder != "big") throw PythonOps.ValueError("byteorder must be either 'little' or 'big'");

            if (length < 0) throw PythonOps.ValueError("length argument must be non-negative");

            if (value == 0) return Bytes.Make(new byte[length]);

            var bytes = new byte[length];
            int cur, end, step;
            if (isLittle) {
                cur = 0; end = length; step = 1;
            } else {
                cur = length - 1; end = -1; step = -1;
            }

            do {
                if (cur == end) ThrowOverflow();
                bytes[cur] = (byte)(value & 0xFF);
                value >>= 8;
                cur += step;
            } while (value != 0);

            if (signed && (bytes[end - step] & 0x80) == 0x80) ThrowOverflow();

            return Bytes.Make(bytes);

            static void ThrowOverflow() => throw PythonOps.OverflowError("int too big to convert");
        }

        #endregion
    }
}
