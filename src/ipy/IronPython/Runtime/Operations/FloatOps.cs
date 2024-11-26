// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Types;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using SpecialNameAttribute = System.Runtime.CompilerServices.SpecialNameAttribute;

namespace IronPython.Runtime.Operations {

    public static partial class DoubleOps {
        private static Regex _fromHexRegex;

        [StaticExtensionMethod]
        public static object __new__(CodeContext/*!*/ context, PythonType cls) {
            if (cls == TypeCache.Double) return 0.0;

            return cls.CreateInstance(context);
        }

        internal static object NewFloat(CodeContext/*!*/ context, PythonType type, object x) {
            string str;
            bool replaceUnicode = true;
            if (x is string s) {
                str = s;
            } else if (x is char c) {
                str = ScriptingRuntimeHelpers.CharToString(c);
            } else if (TryToFloat(context, x, out var d)) {
                return d;
            } else if (x is Extensible<string> es) {
                str = es.Value;
            } else if (x is IBufferProtocol bufferProtocol) {
                using IPythonBuffer buf = bufferProtocol.GetBufferNoThrow()
                    ?? throw PythonOps.TypeErrorForBadInstance($"{type.Name}() argument must be a string or a number, not '{{0}}'", x);
                str = buf.AsReadOnlySpan().MakeString();
                replaceUnicode = false;
            } else {
                throw PythonOps.TypeErrorForBadInstance($"{type.Name}() argument must be a string or a number, not '{{0}}'", x);
            }

            if (LiteralParser.TryParseFloat(str, out var res, replaceUnicode: replaceUnicode))
                return res;

            throw PythonOps.ValueError($"could not convert string to {type.Name}: {PythonOps.Repr(context, x)}");
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext/*!*/ context, PythonType cls, object x) {
            object value = NewFloat(context, TypeCache.Double, x);

            if (cls == TypeCache.Double) {
                return value;
            } else {
                return cls.CreateInstance(context, value);
            }
        }

        internal static bool TryToFloat(CodeContext context, object/*?*/ value, out double result) {
            if (value is double d) {
                result = d;
            } else if (value is int i) {
                result = i;
            } else if (value is BigInteger bi) {
                result = BigIntegerOps.ToDouble(bi);
            } else if (TryInvokeFloat(context, value, out result)) {
                // pass
            } else if (value is Extensible<double> ed) {
                result = ed.Value;
            } else if (value is Extensible<BigInteger> ebi) {
                result = BigIntegerOps.ToDouble(ebi.Value);
            } else if (PythonOps.TryToIndex(value, out object ireal)) { // Python 3.8: fall back on __index__
                result = ireal switch {
                    int ii => ii,
                    BigInteger bii => BigIntegerOps.ToDouble(bii),
                    _ => throw new InvalidOperationException("Unreachable code")
                };
            } else {
                return false;
            }
            return true;

            static bool TryInvokeFloat(CodeContext context, object/*?*/ o, out double result) {
                if (PythonTypeOps.TryInvokeUnaryOperator(context, o, "__float__", out object retobj)) {
                    switch (retobj) {
                        case double d:
                            result = d;
                            return true;
                        case Extensible<double> ed:
                            Warn(context, retobj);
                            result = ed.Value; // Python 3.6: return the int value
                            return true;
                        default:
                            throw PythonOps.TypeError("__float__ returned non-float (type {0})", PythonOps.GetPythonTypeName(retobj));
                    }

                    static void Warn(CodeContext context, object result) {
                        PythonOps.Warn(context, PythonExceptions.DeprecationWarning, $"__float__ returned non-float (type {PythonOps.GetPythonTypeName(result)}).  The ability to return an instance of a strict subclass of float is deprecated, and may be removed in a future version of Python.");
                    }
                }
                result = default;
                return false;
            }
        }

        public static PythonTuple as_integer_ratio(double self) {
            if (Double.IsInfinity(self)) {
                throw PythonOps.OverflowError("Cannot pass infinity to float.as_integer_ratio.");
            } else if (Double.IsNaN(self)) {
                throw PythonOps.ValueError("Cannot pass nan to float.as_integer_ratio.");
            }

            BigInteger dem = 1;
            while ((self % 1) != 0.0) {
                self *= 2;
                dem *= 2;
            }
            return PythonTuple.MakeTuple((BigInteger)self, dem);
        }

        private static readonly char[] _whitespace = new[] { ' ', '\t', '\n', '\f', '\v', '\r' };

        [ClassMethod, StaticExtensionMethod]
        public static object fromhex(CodeContext/*!*/ context, PythonType/*!*/ cls, string self) {
            if (String.IsNullOrEmpty(self)) {
                throw PythonOps.ValueError("expected non empty string");
            }

            self = self.Trim(_whitespace);

            // look for inf, infinity, nan, etc...
            double? specialRes = TryParseSpecialFloat(self);
            if (specialRes != null) {
                return specialRes.Value;
            }

            // nothing special, parse the hex...
            if (_fromHexRegex == null) {
                _fromHexRegex = new Regex("\\A\\s*(?<sign>[-+])?(?:0[xX])?(?<integer>[0-9a-fA-F]+)?(?<fraction>\\.[0-9a-fA-F]*)?(?<exponent>[pP][-+]?[0-9]+)?\\s*\\z");
            }
            Match match = _fromHexRegex.Match(self);
            if (!match.Success) {
                throw InvalidHexString();
            }

            var sign = match.Groups["sign"];
            var integer = match.Groups["integer"];
            var fraction = match.Groups["fraction"];
            var exponent = match.Groups["exponent"];

            bool isNegative = sign.Success && sign.Value == "-";

            BigInteger intVal;
            if (integer.Success) {
                intVal = LiteralParser.ParseBigInteger(integer.Value, 16);
            } else {
                intVal = BigInteger.Zero;
            }

            // combine the integer and fractional parts into one big int
            BigInteger finalBits;
            int decimalPointBit = 0;       // the number of bits of fractions that we have
            if (fraction.Success) {
                BigInteger fractionVal = 0;
                // add the fractional bits to the integer value
                for (int i = 1; i < fraction.Value.Length; i++) {
                    char chr = fraction.Value[i];
                    int val;
                    if (chr >= '0' && chr <= '9') {
                        val = chr - '0';
                    } else if (chr >= 'a' && chr <= 'f') {
                        val = 10 + chr - 'a';
                    } else if (chr >= 'A' && chr <= 'Z') {
                        val = 10 + chr - 'A';
                    } else {
                        // unreachable due to the regex
                        throw new InvalidOperationException();
                    }

                    fractionVal = (fractionVal << 4) | val;
                    decimalPointBit += 4;
                }
                finalBits = (intVal << decimalPointBit) | fractionVal;
            } else {
                // we only have the integer value
                finalBits = intVal;
            }

            if (exponent.Success) {
                int exponentVal = 0;
                if (!Int32.TryParse(exponent.Value.Substring(1), out exponentVal)) {
                    if (exponent.Value.ToLowerAsciiTriggered().StartsWith("p-", StringComparison.Ordinal) || finalBits == BigInteger.Zero) {
                        double zeroRes = isNegative ? NegativeZero : PositiveZero;

                        if (cls == TypeCache.Double) {
                            return zeroRes;
                        }

                        return PythonCalls.Call(cls, zeroRes);
                    }
                    // integer value is too big, no way we're fitting this in.
                    throw HexStringOverflow();
                }

                // update the bits to truly reflect the exponent
                if (exponentVal > 0) {
                    finalBits = finalBits << exponentVal;
                } else if (exponentVal < 0) {
                    decimalPointBit -= exponentVal;
                }
            }

            if ((!exponent.Success && !fraction.Success && !integer.Success) ||
                (!integer.Success && fraction.Length == 1)) {
                throw PythonOps.ValueError("invalid hexidecimal floating point string '{0}'", self);
            }

            if (finalBits == BigInteger.Zero) {
                if (isNegative) {
                    return NegativeZero;
                } else {
                    return PositiveZero;
                }
            }

            int highBit = finalBits.GetBitCount();
            // minus 1 because we'll discard the high bit as it's implicit
            int finalExponent = highBit - decimalPointBit - 1;

            while (finalExponent < -1023) {
                // if we have a number with a very negative exponent
                // we'll throw away all of the insignificant bits even
                // if it takes the number down to zero.
                highBit++;
                finalExponent++;
            }

            if (finalExponent == -1023) {
                // the exponent bits will be all zero, we're going to be a denormalized number, so
                // we need to keep the most significant bit.
                highBit++;
            }

            // we have 52 bits to store the exponent.  In a normalized number the mantissa has an
            // implied 1 bit, in denormalized mode it doesn't. 
            int lostBits = highBit - 53;
            bool rounded = false;
            if (lostBits > 0) {
                // we have more bits then we can stick in the double, we need to truncate or round the value.
                BigInteger finalBitsAndRoundingBit = finalBits >> (lostBits - 1);

                // check if we need to round up (round half even aka bankers rounding)
                if ((finalBitsAndRoundingBit & BigInteger.One) != BigInteger.Zero) {
                    // grab the bits we need and the least significant bit which we care about for rounding
                    BigInteger discardedBits = finalBits & ((BigInteger.One << (lostBits - 1)) - 1);

                    if (discardedBits != BigInteger.Zero ||                            // not exactly .5
                        ((finalBits >> lostBits) & BigInteger.One) != BigInteger.Zero) { // or we're exactly .5 and odd and need to round up
                        // round the value up by adding 1
                        BigInteger roundedBits = finalBitsAndRoundingBit + 1;

                        // now remove the least significant bit we kept for rounding
                        finalBits = (roundedBits >> 1) & 0xfffffffffffff;

                        // check to see if we overflowed into the next bit (e.g. we had a pattern like ffffff rounding to 1000000)
                        if (roundedBits.GetBitCount() != finalBitsAndRoundingBit.GetBitCount()) {
                            if (finalExponent != -1023) {
                                // we overflowed and we're a normalized number.  Discard the new least significant bit so we have
                                // the correct number of bits.  We need to raise the exponent to account for this division by 2.
                                finalBits = finalBits >> 1;
                                finalExponent++;
                            } else if (finalBits == BigInteger.Zero) {
                                // we overflowed and we're a denormalized number == 0.  Increase the exponent making us a normalized
                                // number.  Don't adjust the bits because we're now gaining an implicit 1 bit.
                                finalExponent++;
                            }
                        }

                        rounded = true;
                    }
                }
            }

            if (!rounded) {
                // no rounding is necessary, just shift the bits to get the mantissa
                finalBits = (finalBits >> (highBit - 53)) & 0xfffffffffffff;
            }
            if (finalExponent > 1023) {
                throw HexStringOverflow();
            }

            // finally assemble the bits
            long bits = (long)finalBits;
            bits |= (((long)finalExponent) + 1023) << 52;
            if (isNegative) {
                bits |= unchecked((long)0x8000000000000000);
            }

            double res = BitConverter.Int64BitsToDouble(bits);
            if (cls == TypeCache.Double) {
                return res;
            }

            return PythonCalls.Call(cls, res);
        }

        private static double? TryParseSpecialFloat(string self) {
            switch (self.ToLowerInvariant()) {
                case "inf":
                case "+inf":
                case "infinity":
                case "+infinity":
                    return Double.PositiveInfinity;
                case "-inf":
                case "-infinity":
                    return Double.NegativeInfinity;
                case "nan":
                case "+nan":
                case "-nan":
                    return Double.NaN;
            }
            return null;
        }

        private static Exception HexStringOverflow() {
            return PythonOps.OverflowError("hexadecimal value too large to represent as a float");
        }

        private static Exception InvalidHexString() {
            return PythonOps.ValueError("invalid hexadecimal floating-point string");
        }

        public static string hex(double self) {
            if (Double.IsPositiveInfinity(self)) {
                return "inf";
            } else if (Double.IsNegativeInfinity(self)) {
                return "-inf";
            } else if (Double.IsNaN(self)) {
                return "nan";
            }

            ulong bits = (ulong)BitConverter.DoubleToInt64Bits(self);
            int exponent = (int)((bits >> 52) & 0x7ff) - 1023;
            long mantissa = (long)(bits & 0xfffffffffffff);

            StringBuilder res = new StringBuilder();
            if ((bits & 0x8000000000000000) != 0) {
                // negative
                res.Append('-');
            }
            if (exponent == -1023) {
                res.Append("0x0.");
                exponent++;
            } else {
                res.Append("0x1.");
            }
            res.Append(StringFormatSpec.FromString("013").AlignNumericText(BigIntegerOps.AbsToHex(mantissa, true), mantissa == 0, true));
            res.Append("p");
            if (exponent >= 0) {
                res.Append('+');
            }
            res.Append(exponent.ToString());
            return res.ToString();
        }

        public static bool is_integer(double self) {
            return (self % 1.0) == 0.0;
        }

        #region Binary operators

        [SpecialName]
        public static PythonTuple DivMod(double x, double y) {
            if (y == 0) throw PythonOps.ZeroDivisionError();

            // .NET does not provide Math.DivRem() for floats. Implementation along the CPython code.
            var mod = Math.IEEERemainder(x, y);
            var div = (x - mod) / y;
            if (mod != 0) {
                if ((y < 0) != (mod < 0)) {
                    mod += y;
                    div -= 1;
                }
            } else {
                mod = CopySign(0, y);
            }
            double floordiv;
            if (div != 0) {
                floordiv = Math.Floor(div);
                if (div - floordiv > 0.5)
                    floordiv += 1;
            } else {
                floordiv = CopySign(0, x / y);
            }
            return PythonTuple.MakeTuple(floordiv, mod);
        }

        [SpecialName]
        public static double Mod(double x, double y) {
            if (y == 0) throw PythonOps.ZeroDivisionError();

            // implemented as in CPython
            var mod = Math.IEEERemainder(x, y);
            if (mod != 0) {
                if ((y < 0) != (mod < 0)) {
                    mod += y;
                }
            } else {
                mod = CopySign(0, y);
            }
            return mod;
        }

        [SpecialName]
        public static object Power(double x, double y) {
            if (x == 1.0 || y == 0.0) {
                return 1.0;
            } else if (double.IsNaN(x) || double.IsNaN(y)) {
                return double.NaN;
            } else if (x == 0.0) {
                if (y > 0.0) {
                    // preserve sign if y is a positive, odd int
                    if (y % 2.0 == 1.0) {
                        return x;
                    }
                    return 0.0;
                } else if (y == 0.0) {
                    return 1.0;
                } else if (double.IsNegativeInfinity(y)) {
                    return double.PositiveInfinity;
                }
                throw PythonOps.ZeroDivisionError("0.0 cannot be raised to a negative power");
            } else if (double.IsPositiveInfinity(y)) {
                if (x > 1.0 || x < -1.0) {
                    return double.PositiveInfinity;
                } else if (x == -1.0) {
                    return 1.0;
                }
                return 0.0;
            } else if (double.IsNegativeInfinity(y)) {
                if (x > 1.0 || x < -1.0) {
                    return 0.0;
                } else if (x == -1.0) {
                    return 1.0;
                }
                return double.PositiveInfinity;
            } else if (double.IsNegativeInfinity(x)) {
                // preserve negative sign if y is an odd int
                if (Math.Abs(y % 2.0) == 1.0) {
                    return y > 0 ? double.NegativeInfinity : NegativeZero;
                } else {
                    return y > 0 ? double.PositiveInfinity : 0.0;
                }
            } else if (x < 0 && (Math.Floor(y) != y)) {
                // the return value is complex when x is negative and y is fractional. 
                return ComplexOps.Power(x, y);
            }

            return PythonOps.CheckMath(x, y, Math.Pow(x, y));
        }
        #endregion

        #region Unary operators

        public static object __int__(double d) {
            if (Int32.MinValue <= d && d <= Int32.MaxValue) {
                return (int)d;
            } else if (double.IsInfinity(d)) {
                throw PythonOps.OverflowError("cannot convert float infinity to integer");
            } else if (double.IsNaN(d)) {
                throw PythonOps.ValueError("cannot convert float NaN to integer");
            } else {
                return (BigInteger)d;
            }
        }

        public static object __getnewargs__(CodeContext context, double self) {
            return PythonTuple.MakeTuple(DoubleOps.__new__(context, TypeCache.Double, self));
        }

        #endregion

        #region ToString

        public static string __str__(CodeContext/*!*/ context, double x) {
            return __repr__(context, x);
        }

        public static string __str__(double x, IFormatProvider provider) {
            return x.ToString(provider);
        }

        public static string __str__(double x, string format) {
            return x.ToString(format);
        }

        public static string __str__(double x, string format, IFormatProvider provider) {
            return x.ToString(format, provider);
        }

        public static int __hash__(double d) {
            // Special values
            if (double.IsPositiveInfinity(d)) return 314159;
            if (double.IsNegativeInfinity(d)) return -314159;
            if (double.IsNaN(d)) return 0;
            if (d == 0) return 0;

            // it's an integer!
            if (d == Math.Truncate(d)) {
                // Use this constant since long.MaxValue doesn't cast precisely to a double
                const double maxValue = (ulong)long.MaxValue + 1;
                if (long.MinValue <= d && d < maxValue) {
                    return Int64Ops.__hash__((long)d);
                }
                return BigIntegerOps.__hash__((BigInteger)d);
            }

            DecomposeDouble(d, out int sign, out int exponent, out long mantissa);

            // make sure the mantissa is not even
            while ((mantissa & 1) == 0) {
                mantissa >>= 1;
                exponent++;
            }
            Debug.Assert(exponent <= 0);

            var exp = exponent % 31;
            var invmod = exp == 0 ? 1 : (1 << (31 + exp));
            return unchecked((int)(sign * (((mantissa % int.MaxValue) * invmod) % int.MaxValue)));

            void DecomposeDouble(in double x, out int Sign, out int Exponent, out long Mantissa) {
                Debug.Assert(x != 0 && !double.IsInfinity(x) && !double.IsNaN(x));

                var RawBits = (ulong)BitConverter.DoubleToInt64Bits(x);
                var RawSign = (int)(RawBits >> 63);
                var RawExponent = (int)(RawBits >> 52) & 0x7FF;
                var RawMantissa = (long)(RawBits & 0x000FFFFFFFFFFFFF);
                var IsDenormal = RawExponent == 0 && RawMantissa != 0;
                // assumes not infinity, not zero and not NaN
                Sign = 1 - RawSign * 2;
                Mantissa = IsDenormal ? RawMantissa : RawMantissa | 0x0010000000000000;
                Exponent = IsDenormal ? -1074 : RawExponent - 1075;
            }
        }

        #endregion

        [SpecialName]
        public static bool LessThan(double x, double y) {
            return x < y
                && !(Double.IsInfinity(x) && Double.IsNaN(y))
                && !(Double.IsNaN(x) && Double.IsInfinity(y));
        }

        [SpecialName]
        public static bool LessThanOrEqual(double x, double y) {
            if (x == y) {
                return !Double.IsNaN(x);
            }
            return x < y;
        }

        [SpecialName]
        public static bool GreaterThan(double x, double y) {
            return x > y
                && !(Double.IsInfinity(x) && Double.IsNaN(y))
                && !(Double.IsNaN(x) && Double.IsInfinity(y));
        }

        [SpecialName]
        public static bool GreaterThanOrEqual(double x, double y) {
            if (x == y) {
                return !Double.IsNaN(x);
            }
            return x > y;
        }

        [SpecialName]
        public static bool Equals(double x, double y) {
            if (x == y) {
                return !Double.IsNaN(x);
            }
            return false;
        }

        [SpecialName]
        public static bool NotEquals(double x, double y) {
            if (x == y) {
                return Double.IsNaN(x);
            }
            return true;
        }

        [SpecialName]
        public static bool LessThan(double x, BigInteger y) {
            return Compare(x, y) < 0;
        }
        [SpecialName]
        public static bool LessThanOrEqual(double x, BigInteger y) {
            return Compare(x, y) <= 0;
        }
        [SpecialName]
        public static bool GreaterThan(double x, BigInteger y) {
            return Compare(x, y) > 0;
        }
        [SpecialName]
        public static bool GreaterThanOrEqual(double x, BigInteger y) {
            return Compare(x, y) >= 0;
        }
        [SpecialName]
        public static bool Equals(double x, BigInteger y) {
            return Compare(x, y) == 0;
        }
        [SpecialName]
        public static bool NotEquals(double x, BigInteger y) {
            return Compare(x, y) != 0;
        }

        internal const double PositiveZero = 0.0;
        internal const double NegativeZero = -0.0;

        internal static bool IsPositiveZero(double value) {
            return (value == 0.0) && double.IsPositiveInfinity(1.0 / value);
        }

        internal static bool IsNegativeZero(double value) {
            return (value == 0.0) && double.IsNegativeInfinity(1.0 / value);
        }

        internal static int Sign(double value) {
            if (value == 0.0) {
                return double.IsPositiveInfinity(1.0 / value) ? 1 : -1;
            } else {
                // note: NaN intentionally shows up as negative
                return value > 0 ? 1 : -1;
            }
        }

        internal static double CopySign(double value, double sign) {
            return Sign(sign) * Math.Abs(value);
        }

        private static int Compare(double x, BigInteger y) {
            return -Compare(y, x);
        }

        private static int Compare(BigInteger x, double y) {
            if (double.IsNaN(y) || double.IsPositiveInfinity(y)) {
                return -1;
            } else if (y == Double.NegativeInfinity) {
                return 1;
            }

            // BigInts can hold doubles, but doubles can't hold BigInts, so
            // if we're comparing against a BigInt then we should convert ourself
            // to a long and then compare.
            BigInteger by = (BigInteger)y;
            if (by == x) {
                double mod = y % 1;
                if (mod == 0) return 0;
                if (mod > 0) return -1;
                return +1;
            }
            if (by > x) return -1;
            return +1;
        }

        [SpecialName]
        public static bool LessThan(double x, decimal y) {
            return Compare(x, y) < 0;
        }
        [SpecialName]
        public static bool LessThanOrEqual(double x, decimal y) {
            return Compare(x, y) <= 0;
        }
        [SpecialName]
        public static bool GreaterThan(double x, decimal y) {
            return Compare(x, y) > 0;
        }
        [SpecialName]
        public static bool GreaterThanOrEqual(double x, decimal y) {
            return Compare(x, y) >= 0;
        }
        [SpecialName]
        public static bool Equals(double x, decimal y) {
            return Compare(x, y) == 0;
        }
        [SpecialName]
        public static bool NotEquals(double x, decimal y) {
            return Compare(x, y) != 0;
        }

        private static int Compare(double x, decimal y) {
            if (x > (double)decimal.MaxValue) return +1;
            if (x < (double)decimal.MinValue) return -1;
            return ((decimal)x).CompareTo(y);
        }

        [SpecialName]
        public static bool LessThan(Double x, int y) {
            return x < y;
        }
        [SpecialName]
        public static bool LessThanOrEqual(Double x, int y) {
            return x <= y;
        }
        [SpecialName]
        public static bool GreaterThan(Double x, int y) {
            return x > y;
        }
        [SpecialName]
        public static bool GreaterThanOrEqual(Double x, int y) {
            return x >= y;
        }
        [SpecialName]
        public static bool Equals(Double x, int y) {
            return x == y;
        }
        [SpecialName]
        public static bool NotEquals(Double x, int y) {
            return x != y;
        }

        internal static string Repr(CodeContext/*!*/ context, double self, bool trailingZeroAfterWholeFloat) {
            if (double.IsNaN(self)) {
                return "nan";
            }

            // first format using Python's specific formatting rules...
            string res = StringFormatter.Format(context, "%.17g", self, trailingZeroAfterWholeFloat: trailingZeroAfterWholeFloat);
            if (LiteralParser.ParseFloat(res) == self) {
                return res;
            }
#if NETCOREAPP3_1_OR_GREATER
            Debug.Assert(false);
#endif

            // if it's not round trippable though use .NET's round-trip format
            res = self.ToString("R", CultureInfo.InvariantCulture);
            if (trailingZeroAfterWholeFloat && res.IndexOf('.') == -1) {
                res += ".0";
            }
            return res;
        }

        public static string __repr__(CodeContext/*!*/ context, double self)
            => Repr(context, self, trailingZeroAfterWholeFloat: true);

        public static double __float__(double self) {
            return self;
        }

        public static string __getformat__(CodeContext/*!*/ context, string typestr) {
            FloatFormat res;
            switch (typestr) {
                case "float":
                    res = context.LanguageContext.FloatFormat;
                    break;
                case "double":
                    res = context.LanguageContext.DoubleFormat;
                    break;
                default:
                    throw PythonOps.ValueError("__getformat__() argument 1 must be 'double' or 'float'");
            }

            switch (res) {
                case FloatFormat.Unknown:
                    return "unknown";
                case FloatFormat.IEEE_BigEndian:
                    return "IEEE, big-endian";
                case FloatFormat.IEEE_LittleEndian:
                    return "IEEE, little-endian";
                default:
                    return DefaultFloatFormat();
            }
        }

        public static string __format__(CodeContext/*!*/ context, double self, [NotNone] string/*!*/ formatSpec) {
            if (formatSpec == string.Empty) return __str__(context, self);

            StringFormatSpec spec = StringFormatSpec.FromString(formatSpec);
            string digits;

            if (Double.IsPositiveInfinity(self) || Double.IsNegativeInfinity(self)) {
                if (spec.Type != null && char.IsUpper(spec.Type.Value)) {
                    digits = "INF";
                } else {
                    digits = "inf";
                }
            } else if (Double.IsNaN(self)) {
                if (spec.Type != null && char.IsUpper(spec.Type.Value)) {
                    digits = "NAN";
                } else {
                    digits = "nan";
                }
            } else {
                digits = DoubleToFormatString(context, self, spec);
            }

            if (spec.Sign == null) {
                // This is special because its not "-nan", it's nan.
                // Always pass isZero=false so that -0.0 shows up
                return spec.AlignNumericText(digits, false, Double.IsNaN(self) || Sign(self) > 0);
            } else {
                // Always pass isZero=false so that -0.0 shows up
                return spec.AlignNumericText(digits, false, Double.IsNaN(self) ? true : Sign(self) > 0);
            }
        }

        /// <summary>
        /// Returns the digits for the format spec, no sign is included.
        /// </summary>
        internal static string DoubleToFormatString(CodeContext/*!*/ context, double self, StringFormatSpec/*!*/ spec) {
            self = Math.Abs(self);
            const int DefaultPrecision = 6;
            int precision = spec.Precision ?? DefaultPrecision;

            string digits;
            switch (spec.Type) {
                case '%': {
                        string fmt = "0." + new string('0', precision) + "%";
                        if (spec.ThousandsComma) {
                            fmt = "#," + fmt;
                        }
                        digits = self.ToString(fmt, CultureInfo.InvariantCulture);
                        break;
                    }
                case 'f':
                case 'F': {
                        string fmt = "0." + new string('0', precision);
                        if (spec.ThousandsComma) {
                            fmt = "#," + fmt;
                        }
                        digits = self.ToString(fmt, CultureInfo.InvariantCulture);
                        break;
                    }
                case 'e':
                case 'E': {
                        string fmt = "0." + new string('0', precision) + spec.Type + "+00";
                        if (spec.ThousandsComma) {
                            fmt = "#," + fmt;
                        }
                        digits = self.ToString(fmt, CultureInfo.InvariantCulture);
                        break;
                    }
                case '\0':
                case null:
                    if (spec.Precision != null) {
                        // precision applies to the combined digits before and after the decimal point
                        // so we first need find out how many digits we have before...
                        int digitCnt = 1;
                        double cur = self;
                        while (cur >= 10) {
                            cur /= 10;
                            digitCnt++;
                        }

                        // Use exponents if we don't have enough room for all the digits before.  If we
                        // only have as single digit avoid exponents.
                        if (digitCnt > spec.Precision.Value && digitCnt != 1) {
                            // first round off the decimal value
                            self = MathUtils.Round(self, 0 , MidpointRounding.ToEven);

                            // then remove any insignificant digits
                            double pow = Math.Pow(10, digitCnt - Math.Max(spec.Precision.Value, 1));
                            self = self - (self % pow);

                            // finally format w/ the requested precision
                            string fmt = "0.0" + new string('#', spec.Precision.Value);

                            digits = self.ToString(fmt + "e+00", CultureInfo.InvariantCulture);
                        } else {
                            // we're including all the numbers to the right of the decimal we can, we explicitly 
                            // round to match CPython's behavior
                            int decimalPoints = Math.Max(spec.Precision.Value - digitCnt, 0);

                            self = MathUtils.Round(self, decimalPoints, MidpointRounding.ToEven);
                            digits = self.ToString("0.0" + new string('#', decimalPoints), CultureInfo.InvariantCulture);
                        }
                    } else {
                        // just the default formatting
                        if (IncludeExponent(self)) {
                            digits = self.ToString("0.#e+00", CultureInfo.InvariantCulture);
                        } else if (spec.ThousandsComma) {
                            digits = self.ToString("#,0.0###", CultureInfo.InvariantCulture);
                        } else {
                            digits = self.ToString("0.0###", CultureInfo.InvariantCulture);
                        }
                    }
                    break;
                case 'n':
                case 'g':
                case 'G': {
                        // precision applies to the combined digits before and after the decimal point
                        // so we first need find out how many digits we have before...
                        int digitCnt = 1;
                        double cur = self;
                        while (cur >= 10) {
                            cur /= 10;
                            digitCnt++;
                        }

                        // Use exponents if we don't have enough room for all the digits before.  If we
                        // only have as single digit avoid exponents.
                        if (digitCnt > precision && digitCnt != 1) {
                            // first round off the decimal value
                            self = MathUtils.Round(self, 0, MidpointRounding.ToEven);

                            // then remove any insignificant digits
                            double pow = Math.Pow(10, digitCnt - Math.Max(precision, 1));
                            double rest = self / pow;
                            self = self - self % pow;
                            if ((rest % 1) >= .5) {
                                // round up
                                self += pow;
                            }

                            string fmt;
                            if (spec.Precision > 1 || digitCnt > 6) {
                                // include the requested precision to the right of the decimal
                                fmt = "0.#" + new string('#', precision);
                            } else {
                                // zero precision, no decimal
                                fmt = "0";
                            }
                            if (spec.ThousandsComma) {
                                fmt = "#," + fmt;
                            }

                            digits = self.ToString(fmt + (spec.Type == 'G' ? "E+00" : "e+00"), CultureInfo.InvariantCulture);
                        } else {
                            // we're including all the numbers to the right of the decimal we can, we explicitly 
                            // round to match CPython's behavior
                            if (self < 1) {
                                // no implicit 0
                                digitCnt--;
                            }
                            int decimalPoints = Math.Max(precision - digitCnt, 0);

                            self = MathUtils.Round(self, decimalPoints, MidpointRounding.ToEven);

                            if (spec.Type == 'n' && context.LanguageContext.NumericCulture != PythonContext.CCulture) {
                                if (digitCnt != precision && (self % 1) != 0) {
                                    digits = self.ToString("#,0.0" + new string('#', decimalPoints), context.LanguageContext.NumericCulture);
                                } else {
                                    // leave out the decimal if the precision == # of digits or we have a whole number
                                    digits = self.ToString("#,0", context.LanguageContext.NumericCulture);
                                }
                            } else {
                                if (digitCnt != precision && (self % 1) != 0) {
                                    digits = self.ToString("0.0" + new string('#', decimalPoints), CultureInfo.InvariantCulture);
                                } else {
                                    // leave out the decimal if the precision == # of digits or we have a whole number
                                    digits = self.ToString("0", CultureInfo.InvariantCulture);
                                }
                            }
                        }
                    }
                    break;
                default:
                    throw PythonOps.ValueError("Unknown format code '{0}' for object of type 'float'", spec.TypeRepr);
            }

            return digits;
        }

        private static bool IncludeExponent(double self) {
            return self >= 1e12 || (self != 0 && self <= 0.00009);
        }

        private static string DefaultFloatFormat() {
            if (BitConverter.IsLittleEndian) {
                return "IEEE, little-endian";
            }

            return "IEEE, big-endian";
        }

        public static void __setformat__(CodeContext/*!*/ context, string typestr, string fmt) {
            FloatFormat format;
            switch (fmt) {
                case "unknown":
                    format = FloatFormat.Unknown;
                    break;
                case "IEEE, little-endian":
                    if (!BitConverter.IsLittleEndian) {
                        throw PythonOps.ValueError("can only set double format to 'unknown' or the detected platform value");
                    }
                    format = FloatFormat.IEEE_LittleEndian;
                    break;
                case "IEEE, big-endian":
                    if (BitConverter.IsLittleEndian) {
                        throw PythonOps.ValueError("can only set double format to 'unknown' or the detected platform value");
                    }
                    format = FloatFormat.IEEE_BigEndian;
                    break;
                default:
                    throw PythonOps.ValueError(" __setformat__() argument 2 must be 'unknown', 'IEEE, little-endian' or 'IEEE, big-endian'");
            }

            switch (typestr) {
                case "float":
                    context.LanguageContext.FloatFormat = format;
                    break;
                case "double":
                    context.LanguageContext.DoubleFormat = format;
                    break;
                default:
                    throw PythonOps.ValueError("__setformat__() argument 1 must be 'double' or 'float'");
            }
        }

        public static object __round__(double self) {
            if (double.IsNaN(self)) {
                throw PythonOps.ValueError("cannot convert float NaN to integer");
            }

            if (double.IsInfinity(self)) {
                throw PythonOps.OverflowError("cannot convert float infinity to integer");
            }

            var result = new BigInteger(__round__(self, 0));
            if (result.AsInt32(out var intResult)) {
                return intResult;
            }

            return result;
        }

        public static double __round__(double self, int ndigits) {
            if (double.IsNaN(self) || double.IsInfinity(self)) {
                return self;
            }

            var result = MathUtils.Round(self, ndigits, MidpointRounding.ToEven);
            
            if (double.IsInfinity(result)) {
                throw PythonOps.OverflowError("rounded value too large to represent");
            }

            return result;
        }

        public static double __round__(double self, BigInteger ndigits) {
            if (ndigits.AsInt32(out var n)) {
                return __round__(self, n);
            }

            if (double.IsNaN(self) || double.IsInfinity(self)) {
                return self;
            }

            return ndigits > 0 ? self : 0.0;
        }

        public static double __round__(double self, object ndigits) {
            var index = PythonOps.Index(ndigits);
            switch (index)
            {
                case int i:
                    return __round__(self, i);

                case BigInteger bi:
                    return __round__(self, bi);
            }

            throw PythonOps.RuntimeError(
                "Unreachable code was reached. "
                + "PythonOps.Index is guaranteed to either throw or return an integral value.");
        }
    }

    internal enum FloatFormat {
        None,
        Unknown,
        IEEE_LittleEndian,
        IEEE_BigEndian
    }

    public partial class SingleOps {
        [SpecialName]
        public static bool LessThan(float x, float y) {
            return x < y;
        }
        [SpecialName]
        public static bool LessThanOrEqual(float x, float y) {
            if (x == y) {
                return !Single.IsNaN(x);
            }

            return x < y;
        }

        [SpecialName]
        public static bool GreaterThan(float x, float y) {
            return x > y;
        }

        [SpecialName]
        public static bool GreaterThanOrEqual(float x, float y) {
            if (x == y) {
                return !Single.IsNaN(x);
            }

            return x > y;
        }

        [SpecialName]
        public static bool Equals(float x, float y) {
            if (x == y) {
                return !Single.IsNaN(x);
            }
            return x == y;
        }

        [SpecialName]
        public static bool NotEquals(float x, float y) {
            return !Equals(x, y);
        }

        [SpecialName]
        public static float Mod(float x, float y) {
            return (float)DoubleOps.Mod(x, y);
        }

        [SpecialName]
        public static object Power(float x, float y) {
            // the return value is complex when x is negative and y is fractional. 
            return DoubleOps.Power(x, y);
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext/*!*/ context, PythonType cls) {
            if (cls == TypeCache.Single) return (float)0.0;

            return cls.CreateInstance(context);
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext/*!*/ context, PythonType cls, object x) {
            if (Converter.TryConvertToDouble(x, out double doubleVal)) return (float)doubleVal;

            return (float)(double)DoubleOps.NewFloat(context, TypeCache.Single, x);
        }

        public static string __str__(CodeContext/*!*/ context, float x) {
            // Python does not natively support System.Single. However, we try to provide
            // formatting consistent with System.Double.
            return StringFormatter.Format(context, "%.6g", x, trailingZeroAfterWholeFloat: true);
        }

        public static string __repr__(CodeContext/*!*/ context, float self) {
            return __str__(context, self);
        }

        public static string __format__(CodeContext/*!*/ context, float self, [NotNone] string/*!*/ formatSpec) {
            return DoubleOps.__format__(context, self, formatSpec);
        }

        public static int __hash__(float x) {
            return DoubleOps.__hash__((double)x);
        }

        public static double __float__(float x) {
            return x;
        }
    }
}
