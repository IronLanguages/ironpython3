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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Modules;
using IronPython.Runtime.Types;

#if FEATURE_NUMERICS
using System.Numerics;
#else
using Microsoft.Scripting.Math;
using Complex = Microsoft.Scripting.Math.Complex64;
#endif

using SpecialNameAttribute = System.Runtime.CompilerServices.SpecialNameAttribute;

namespace IronPython.Runtime.Operations {

    public static partial class Int32Ops {
        private static object FastNew(CodeContext/*!*/ context, object o) {
            Extensible<BigInteger> el;

            if (o is string) return __new__(null, (string)o, 10);
            if (o is double) return DoubleOps.__int__((double)o);
            if (o is int) return o;
            if (o is bool) return ((bool)o) ? 1 : 0;
            if (o is BigInteger) {
                int res;
                if (((BigInteger)o).AsInt32(out res)) {
                    return ScriptingRuntimeHelpers.Int32ToObject(res);
                }
                return o;
            }

            if ((el = o as Extensible<BigInteger>) != null) {
                int res;
                if (el.Value.AsInt32(out res)) {
                    return ScriptingRuntimeHelpers.Int32ToObject(res);
                }
                return el.Value;
            }

            if (o is float) return DoubleOps.__int__((double)(float)o);

            if (o is Complex) throw PythonOps.TypeError("can't convert complex to int; use int(abs(z))");

            if (o is Int64) {
                Int64 val = (Int64)o;
                if (Int32.MinValue <= val && val <= Int32.MaxValue) {
                    return (Int32)val;
                } else {
                    return (BigInteger)val;
                }
            } else if (o is UInt32) {
                UInt32 val = (UInt32)o;
                if (val <= Int32.MaxValue) {
                    return (Int32)val;
                } else {
                    return (BigInteger)val;
                }
            } else if (o is UInt64) {
                UInt64 val = (UInt64)o;
                if (val <= Int32.MaxValue) {
                    return (Int32)val;
                } else {
                    return (BigInteger)val;
                }
            } else if (o is Decimal) {
                Decimal val = (Decimal)o;
                if (Int32.MinValue <= val && val <= Int32.MaxValue) {
                    return (Int32)val;
                } else {
                    return (BigInteger)val;
                }
            } else if (o is Enum) {
                return ((IConvertible)o).ToInt32(null);
            }

            Extensible<string> es = o as Extensible<string>;
            if (es != null) {
                // __int__ takes precedence, call it if it's available...
                object value;
                if (PythonTypeOps.TryInvokeUnaryOperator(DefaultContext.Default, es, "__int__", out value)) {
                    return value;
                }

                // otherwise call __new__ on the string value
                return __new__(null, es.Value, 10);
            }

            object result;
            int intRes;
            BigInteger bigintRes;
            if (PythonTypeOps.TryInvokeUnaryOperator(context, o, "__int__", out result) &&
                !Object.ReferenceEquals(result, NotImplementedType.Value)) {
                if (result is int || result is BigInteger ||
                    result is Extensible<int> || result is Extensible<BigInteger>) {
                    return result;
                } else {
                    throw PythonOps.TypeError("__int__ returned non-Integral (type {0})", PythonTypeOps.GetOldName(result));
                }
            } else if (PythonOps.TryGetBoundAttr(context, o, "__trunc__", out result)) {
                result = PythonOps.CallWithContext(context, result);
                if (result is int || result is BigInteger ||
                    result is Extensible<int> || result is Extensible<BigInteger>) {
                    return result;
                } else if (Converter.TryConvertToInt32(result, out intRes)) {
                    return intRes;
                } else if (Converter.TryConvertToBigInteger(result, out bigintRes)) {
                    return bigintRes;
                } else {
                    throw PythonOps.TypeError("__trunc__ returned non-Integral (type {0})", PythonTypeOps.GetOldName(result));
                }
            }

            if (o is OldInstance) {
                throw PythonOps.AttributeError("{0} instance has no attribute '__trunc__'", PythonTypeOps.GetOldName((OldInstance)o));
            } else {
                throw PythonOps.TypeError("int() argument must be a string or a number, not '{0}'", PythonTypeOps.GetName(o));
            }
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext context, object o) {
            return __new__(context, TypeCache.Int32, o);
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext context, PythonType cls, Extensible<double> o) {
            object value;
            // always succeeds as float defines __int__
            PythonTypeOps.TryInvokeUnaryOperator(context, o, "__int__", out value);
            if (cls == TypeCache.Int32) {
                return (int)value;
            } else {
                return cls.CreateInstance(context, value);
            }
        }

        private static void ValidateType(PythonType cls) {
            if (cls == TypeCache.Boolean)
                throw PythonOps.TypeError("int.__new__(bool) is not safe, use bool.__new__()");
        }

        [StaticExtensionMethod]
        public static object __new__(PythonType cls, string s, int @base) {
            ValidateType(cls);

            // radix 16/8/2 allows a 0x/0o/0b preceding it... We either need a whole new
            // integer parser, or special case it here.
            if (@base == 16 || @base == 8 || @base == 2) {
                s = TrimRadix(s, @base);
            }

            return LiteralParser.ParseIntegerSign(s, @base);
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext/*!*/ context, PythonType cls, IList<byte> s) {
            object value;
            IPythonObject po = s as IPythonObject;
            if (po == null ||
                !PythonTypeOps.TryInvokeUnaryOperator(DefaultContext.Default, po, "__int__", out value)) {
                value = FastNew(context, s.MakeString());
            }

            if (cls == TypeCache.Int32) {
                return value;
            } else {
                ValidateType(cls);
                // derived int creation...
                return cls.CreateInstance(context, value);
            }
        }

        internal static string TrimRadix(string s, int radix) {
            for (int i = 0; i < s.Length; i++) {
                if (Char.IsWhiteSpace(s[i])) continue;

                if (s[i] == '0' && i < s.Length - 1) {
                    switch(radix) {
                        case 16:
                            if (s[i + 1] == 'x' || s[i + 1] == 'X') {
                                s = s.Substring(i + 2);
                            }
                            break;
                        case 8:
                            if (s[i + 1] == 'o' || s[i + 1] == 'O') {
                                s = s.Substring(i + 2);
                            }
                            break;
                        case 2:
                            if (s[i + 1] == 'b' || s[i + 1] == 'B') {
                                s = s.Substring(i + 2);
                            }
                            break;
                        default:
                            break;
                    }
                }
                break;
            }
            return s;
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext context, PythonType cls, object x) {
            object value = FastNew(context, x);
            if (cls == TypeCache.Int32) {
                return value;
            } else {
                ValidateType(cls);

                // derived int creation...
                return cls.CreateInstance(context, value);
            }
        }

        // "int()" calls ReflectedType.Call(), which calls "Activator.CreateInstance" and return directly.
        // this is for derived int creation or direct calls to __new__...
        [StaticExtensionMethod]
        public static object __new__(CodeContext context, PythonType cls) {
            if (cls == TypeCache.Int32) return 0;

            return cls.CreateInstance(context);
        }

        #region Binary Operators
        
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

            if (power < 0) throw PythonOps.TypeError("power", power, "power must be >= 0");

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
                return BigIntegerOps.Power((BigInteger)x, savePower);
            }
        }


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

        public static PythonTuple __divmod__(int x, int y) {
            return PythonTuple.MakeTuple(Divide(x, y), Mod(x, y));
        }

        [return: MaybeNotImplemented]
        public static object __divmod__(int x, object y) {
            return NotImplementedType.Value;
        }


        #region Unary Operators

        public static string __oct__(int x) {
            if (x == 0) {
                return "0";
            } else if (x > 0) {
                return "0" + ((BigInteger)x).ToString(8);
            } else {
                return "-0" + ((BigInteger)(-x)).ToString(8);
            }
        }

        public static string __hex__(int x) {
            if (x < 0) {
                return "-0x" + (-x).ToString("x");
            } else {
                return "0x" + x.ToString("x");
            }
        }

        #endregion

        public static object __getnewargs__(CodeContext context, int self) {
            return PythonTuple.MakeTuple(Int32Ops.__new__(context, TypeCache.Int32, self));
        }

        public static object __rdivmod__(int x, int y) {
            return __divmod__(y, x);
        }

        public static int __int__(int self) {
            return self;
        }

        public static int __index__(int self) {
            return self;
        }

        public static BigInteger __long__(int self) {
            return (BigInteger)self;
        }

        public static double __float__(int self) {
            return (double)self;
        }

        public static int __abs__(int self) {
            return Math.Abs(self);
        }

        public static object __coerce__(CodeContext context, int x, object o) {
            // called via builtin.coerce()
            if (o is int) {
                return PythonTuple.MakeTuple(ScriptingRuntimeHelpers.Int32ToObject(x), o);
            }
            return NotImplementedType.Value;
        }

        public static string __format__(CodeContext/*!*/ context, int self, [NotNull]string/*!*/ formatSpec) {
            StringFormatSpec spec = StringFormatSpec.FromString(formatSpec);

            if (spec.Precision != null) {
                throw PythonOps.ValueError("Precision not allowed in integer format specifier");
            }

            string digits;
            int width = 0;

            switch (spec.Type) {
                case 'n':
                    CultureInfo culture = PythonContext.GetContext(context).NumericCulture;

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
                    
                    if (self < 0 || self > 0xFF) {
                        throw PythonOps.OverflowError("%c arg not in range(0x10000)");
                    }

                    digits = ScriptingRuntimeHelpers.CharToString((char)self);
                    break;
                default:
                    throw PythonOps.ValueError("Unknown format code '{0}'", spec.Type.ToString());
            }

            if (self < 0 && digits[0] == '-') {
                digits = digits.Substring(1);
            }

            return spec.AlignNumericText(digits, self == 0, self > 0);
        }

        public static string/*!*/ __repr__(int self) {
            return self.ToString(CultureInfo.InvariantCulture);
        }

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
    }
}
