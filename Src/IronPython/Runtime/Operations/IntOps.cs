// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Types;

using SpecialNameAttribute = System.Runtime.CompilerServices.SpecialNameAttribute;
using NotDynamicNullAttribute = Microsoft.Scripting.Runtime.NotNullAttribute;

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

        public static string __format__(CodeContext/*!*/ context, int self, [NotDynamicNull] string/*!*/ formatSpec) {
            StringFormatSpec spec = StringFormatSpec.FromString(formatSpec);

            if (spec.Precision != null) {
                throw PythonOps.ValueError("Precision not allowed in integer format specifier");
            }

            string digits;
            int width = 0;

            switch (spec.Type) {
                case 'n':
                    CultureInfo culture = context.LanguageContext.NumericCulture;

                    if (culture == CultureInfo.InvariantCulture) {
                        // invariant culture maps to CPython's C culture, which doesn't
                        // include any formatting info.
                        goto case 'd';
                    }
                    width = spec.Width ?? 0;

                    // If we're padding with leading zeros and we might be inserting
                    // culture sensitive number group separators. (i.e. commas)
                    // So use FormattingHelper.ToCultureString for that support.
                    if (spec.Fill.HasValue && spec.Fill.Value == '0' && width > 1) {
                        digits = FormattingHelper.ToCultureString(self, culture.NumberFormat, spec);
                    }
                    else {
                        digits = self.ToString("N0", culture);
                    }
                    break;
                case null:
                case 'd':
                    if (spec.ThousandsComma) {
                        width = spec.Width ?? 0;

                        // If we're inserting commas, and we're padding with leading zeros.
                        // AlignNumericText won't know where to place the commas,
                        // so use FormattingHelper.ToCultureString for that support.
                        if (spec.Fill.HasValue && spec.Fill.Value == '0' && width > 1) {
                            digits = FormattingHelper.ToCultureString(self, FormattingHelper.InvariantCommaNumberInfo, spec);
                        }
                        else {
                            digits = self.ToString("#,0", CultureInfo.InvariantCulture);
                        }
                    } else {
                        digits = self.ToString("D", CultureInfo.InvariantCulture);
                    }
                    break;
                case '%':
                    if (spec.ThousandsComma) {
                        digits = self.ToString("#,0.000000%", CultureInfo.InvariantCulture);
                    } else {
                        digits = self.ToString("0.000000%", CultureInfo.InvariantCulture);
                    }
                    break;
                case 'e':
                    if (spec.ThousandsComma) {
                        digits = self.ToString("#,0.000000e+00", CultureInfo.InvariantCulture);
                    } else {
                        digits = self.ToString("0.000000e+00", CultureInfo.InvariantCulture);
                    }
                    break;
                case 'E':
                    if (spec.ThousandsComma) {
                        digits = self.ToString("#,0.000000E+00", CultureInfo.InvariantCulture);
                    } else {
                        digits = self.ToString("0.000000E+00", CultureInfo.InvariantCulture);
                    }
                    break;
                case 'f':
                case 'F':
                    if (spec.ThousandsComma) {
                        digits = self.ToString("#,########0.000000", CultureInfo.InvariantCulture);
                    } else {
                        digits = self.ToString("#########0.000000", CultureInfo.InvariantCulture);
                    }
                    break;
                case 'g':
                    if (self >= 1000000 || self <= -1000000) {
                        digits = self.ToString("0.#####e+00", CultureInfo.InvariantCulture);
                    } else if (spec.ThousandsComma) {
                        // Handle the common case in 'd'.
                        goto case 'd';
                    } else {
                        digits = self.ToString(CultureInfo.InvariantCulture);
                    }
                    break;
                case 'G':
                    if (self >= 1000000 || self <= -1000000) {
                        digits = self.ToString("0.#####E+00", CultureInfo.InvariantCulture);
                    } else if (spec.ThousandsComma) {
                        // Handle the common case in 'd'.
                        goto case 'd';
                    }
                    else {
                        digits = self.ToString(CultureInfo.InvariantCulture);
                    }
                    break;
                case 'X':
                    digits = ToHex(self, false);
                    break;
                case 'x':
                    digits = ToHex(self, true);
                    break;
                case 'o': // octal
                    digits = ToOctal(self, true);
                    break;
                case 'b': // binary
                    digits = ToBinary(self, false);
                    break;
                case 'c': // single char
                    if (spec.Sign != null) {
                        throw PythonOps.ValueError("Sign not allowed with integer format specifier 'c'");
                    }

                    if (self < 0 || self > 0x10ffff) {
                        throw PythonOps.OverflowError("%c arg not in range(0x110000)");
                    }

                    digits = (self > char.MaxValue) ? char.ConvertFromUtf32(self) : ScriptingRuntimeHelpers.CharToString((char)self);
                    break;
                default:
                    throw PythonOps.ValueError("Unknown format code '{0}' for object of type 'int'", spec.TypeRepr);
            }

            if (self < 0 && digits[0] == '-') {
                digits = digits.Substring(1);
            }

            return spec.AlignNumericText(digits, self == 0, self > 0);
        }

        public static Bytes to_bytes(Int32 value, int length, [NotDynamicNull] string byteorder, bool signed=false) {
            // TODO: signed should be a keyword only argument
            // TODO: should probably be moved to IntOps.Generated and included in all types

            if (length < 0) throw PythonOps.ValueError("length argument must be non-negative");
            if (!signed && value < 0) throw PythonOps.OverflowError("can't convert negative int to unsigned");

            bool isLittle = byteorder == "little";
            if (!isLittle && byteorder != "big") throw PythonOps.ValueError("byteorder must be either 'little' or 'big'");

            var reqLength = (bit_length(value) + (value > 0 && signed ? 1 : 0) + 7) / 8;
            if (reqLength > length) throw PythonOps.OverflowError("int too big to convert");

            var bytes = new BigInteger(value).ToByteArray();
            IEnumerable<byte> res = bytes;
            if (length > bytes.Length) res = res.Concat(Enumerable.Repeat<byte>((value < 0) ? (byte)0xff : (byte)0, length - bytes.Length));
            else if (length < bytes.Length) res = res.Take(length);
            if (!isLittle) res = res.Reverse();

            return Bytes.Make(res.ToArray());
        }

        [ClassMethod, StaticExtensionMethod]
        public static object from_bytes(CodeContext context, PythonType type, object bytes, [NotDynamicNull] string byteorder, bool signed = false)
            // TODO: signed should be a keyword only argument
            => BigIntegerOps.from_bytes(context, type, bytes, byteorder, signed);


        #endregion

        #region Helpers

        private static string ToHex(int self, bool lowercase) {
            string digits;
            if (self != Int32.MinValue) {
                int val = self;
                if (self < 0) {
                    val = -self;
                }
                digits = val.ToString(lowercase ? "x" : "X", CultureInfo.InvariantCulture);
            } else {
                digits = "80000000";
            }

            return digits;
        }

        private static string ToOctal(int self, bool lowercase) {
            string digits;
            if (self == 0) {
                digits = "0";
            } else if (self != Int32.MinValue) {
                int val = self;
                if (self < 0) {
                    val = -self;
                }

                StringBuilder sbo = new StringBuilder();
                for (int i = 30; i >= 0; i -= 3) {
                    char value = (char)('0' + (val >> i & 0x07));
                    if (value != '0' || sbo.Length > 0) {
                        sbo.Append(value);
                    }
                }
                digits = sbo.ToString();
            } else {
                digits = "20000000000";
            }

            return digits;
        }

        internal static string ToBinary(int self) {
            if (self == Int32.MinValue) {
                return "-0b10000000000000000000000000000000";
            }
            
            string res = ToBinary(self, true);
            if (self < 0) {
                res = "-" + res;
            }
            return res;
        }

        private static string ToBinary(int self, bool includeType) {
            string digits;
            if (self == 0) {
                digits = "0";
            } else if (self != Int32.MinValue) {
                StringBuilder sbb = new StringBuilder();

                int val = self;
                if (self < 0) {
                    val = -self;
                }

                for (int i = 31; i >= 0; i--) {
                    if ((val & (1 << i)) != 0) {
                        sbb.Append('1');
                    } else if (sbb.Length != 0) {
                        sbb.Append('0');
                    }
                }
                digits = sbb.ToString();
            } else {
                digits = "10000000000000000000000000000000";
            }
            
            if (includeType) {
                digits = "0b" + digits;
            }
            return digits;
        }

        #endregion
    }
}
