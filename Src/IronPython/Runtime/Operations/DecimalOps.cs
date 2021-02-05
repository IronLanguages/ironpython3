// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;

using IronPython.Runtime.Types;

using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime.Operations {
    public static class DecimalOps {

        internal static int __cmp__(CodeContext context, decimal x, decimal other) {
            return x.CompareTo(other);
        }

        public static bool __bool__(decimal x) {
            return x != 0;
        }

        public static string __repr__(decimal x) {
            return x.ToString(CultureInfo.InvariantCulture);
        }

        [SpecialName]
        public static bool LessThan(decimal x, decimal y) => x < y;
        [SpecialName]
        public static bool LessThanOrEqual(decimal x, decimal y) => x <= y;
        [SpecialName]
        public static bool GreaterThan(decimal x, decimal y) => x > y;
        [SpecialName]
        public static bool GreaterThanOrEqual(decimal x, decimal y) => x >= y;
        [SpecialName]
        public static bool Equals(decimal x, decimal y) => x == y;
        [SpecialName]
        public static bool NotEquals(decimal x, decimal y) => x != y;

        [SpecialName]
        public static bool LessThan(decimal x, BigInteger y) => __cmp__(x, y) < 0;
        [SpecialName]
        public static bool LessThanOrEqual(decimal x, BigInteger y) => __cmp__(x, y) <= 0;
        [SpecialName]
        public static bool GreaterThan(decimal x, BigInteger y) => __cmp__(x, y) > 0;
        [SpecialName]
        public static bool GreaterThanOrEqual(decimal x, BigInteger y) => __cmp__(x, y) >= 0;
        [SpecialName]
        public static bool Equals(decimal x, BigInteger y) => __cmp__(x, y) == 0;
        [SpecialName]
        public static bool NotEquals(decimal x, BigInteger y) => __cmp__(x, y) != 0;

        internal static int __cmp__(BigInteger x, decimal y) {
            return -__cmp__(y, x);
        }

        internal static int __cmp__(decimal x, BigInteger y) {
            BigInteger bx = (BigInteger)x;
            if (bx == y) {
                decimal mod = x % 1;
                if (mod == 0) return 0;
                if (mod > 0) return +1;
                else return -1;
            }
            return bx > y ? +1 : -1;
        }

        [return: MaybeNotImplemented]
        internal static object __cmp__(object x, decimal y) {
            return __cmp__(y, x);
        }

        [return: MaybeNotImplemented]
        internal static object __cmp__(decimal x, object y) {
            if (y is null) {
                return ScriptingRuntimeHelpers.Int32ToObject(+1);
            }
            return NotImplementedType.Value;
        }

        public static int __hash__(decimal x) {
            // TODO: This should be made consistent with Python number hashing
            return ((BigInteger)x).GetHashCode();
        }

        public static string __format__(CodeContext/*!*/ context, decimal self, [NotNull]string/*!*/ formatSpec) {
            StringFormatSpec spec = StringFormatSpec.FromString(formatSpec);
            // default to the normal
            if (spec.IsEmpty) {
                return self.ToString();
            }

            string digits = DecimalToFormatString(context, self, spec);
            return spec.AlignNumericText(digits, self != 0, self > 0);
        }

        /// <summary>
        /// Returns the digits for the format spec, no sign is included.
        /// </summary>
        private static string DecimalToFormatString(CodeContext/*!*/ context, decimal self, StringFormatSpec/*!*/ spec) {
            self = Math.Abs(self);
            const int DefaultPrecision = 12;
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
                        decimal cur = self;
                        while (cur >= 10) {
                            cur /= 10;
                            digitCnt++;
                        }

                        // Use exponents if we don't have enough room for all the digits before.  If we
                        // only have as single digit avoid exponents.
                        if (digitCnt > spec.Precision.Value && digitCnt != 1) {
                            // first round off the decimal value
                            self = decimal.Round(self, 0, MidpointRounding.AwayFromZero);

                            // then remove any insignificant digits
                            double pow = Math.Pow(10, digitCnt - Math.Max(spec.Precision.Value, 1));
                            self -= (self % (decimal)pow);

                            // finally format w/ the requested precision
                            string fmt = "0.0" + new string('#', spec.Precision.Value);

                            digits = self.ToString(fmt + "e+00", CultureInfo.InvariantCulture);
                        } else {
                            // we're including all the numbers to the right of the decimal we can, we explicitly 
                            // round to match CPython's behavior
                            int decimalPoints = Math.Max(spec.Precision.Value - digitCnt, 0);

                            self = decimal.Round(self, decimalPoints, MidpointRounding.AwayFromZero);
                            digits = self.ToString("0.0" + new string('#', decimalPoints));
                        }
                    } else {
                        // just the default formatting
                        if (self >= (decimal)1e12 || (self != 0 && self <= (decimal)0.00009)) {
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
                        decimal cur = self;
                        while (cur >= 10) {
                            cur /= 10;
                            digitCnt++;
                        }

                        // Use exponents if we don't have enough room for all the digits before.  If we
                        // only have as single digit avoid exponents.
                        if (digitCnt > precision && digitCnt != 1) {
                            // first round off the decimal value
                            self = decimal.Round(self, 0, MidpointRounding.AwayFromZero);

                            // then remove any insignificant digits
                            double pow = Math.Pow(10, digitCnt - Math.Max(precision, 1));
                            decimal rest = self / (decimal)pow;
                            self -= self % (decimal)pow;
                            if ((rest % 1) >= (decimal).5) {
                                // round up
                                self += (decimal)pow;
                            }

                            string fmt;
                            if (spec.Type == 'n' && context.LanguageContext.NumericCulture != PythonContext.CCulture) {
                                // we've already figured out, we don't have any digits for decimal points, so just format as a number + exponent
                                fmt = "0";
                            } else if (spec.Precision > 1 || digitCnt > 6) {
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

                            self = decimal.Round(self, decimalPoints, MidpointRounding.AwayFromZero);

                            if (spec.Type == 'n' && context.LanguageContext.NumericCulture != PythonContext.CCulture) {
                                if (digitCnt != precision && (self % 1) != 0) {
                                    digits = self.ToString("#,0.0" + new string('#', decimalPoints));
                                } else {
                                    // leave out the decimal if the precision == # of digits or we have a whole number
                                    digits = self.ToString("#,0");
                                }
                            } else {
                                if (digitCnt != precision && (self % 1) != 0) {
                                    digits = self.ToString("0.0" + new string('#', decimalPoints));
                                } else {
                                    // leave out the decimal if the precision == # of digits or we have a whole number
                                    digits = self.ToString("0");
                                }
                            }
                        }
                    }
                    break;
                default:
                    throw PythonOps.ValueError("Unknown format code '{0}' for object of type 'decimal'", spec.TypeRepr);
            }

            return digits;
        }
    }
}
