// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace IronPython.Runtime {
    /// <summary>
    /// Shared float formatting for % and format() formatting.
    /// </summary>
    internal static class FloatFormatter {
        // false for .NET Framework, which only produces 15 significant digits and rounds ties away from zero
        private static readonly bool supportsExactDigits = (1.0 / 3).ToString("F17", CultureInfo.InvariantCulture) != "0.33333333333333300";

        // new in .NET 6
        private static readonly bool supportsPrecisionGreaterThan99 = 0.ToString("D100", CultureInfo.InvariantCulture) != "D100";

        private static bool UseExactDigits(int precision) => !supportsExactDigits || !supportsPrecisionGreaterThan99 && precision > 99;

        /// <summary>
        /// Formats the absolute value of <paramref name="value"/>; the caller is responsible for the sign.
        /// </summary>
        /// <param name="type">One of e, E, f, F, g or G.</param>
        public static string Format(double value, char type, int precision, bool altForm = false) {
            Debug.Assert(precision >= 0);

            value = Math.Abs(value);
            bool upper = char.IsUpper(type);

            if (double.IsNaN(value)) return upper ? "NAN" : "nan";
            if (double.IsInfinity(value)) return upper ? "INF" : "inf";

            switch (type) {
                case 'e':
                case 'E': {
                        string mantissa = FormatSignificant(value, precision + 1, out int exp);
                        if (altForm && precision == 0) mantissa += ".";
                        return mantissa + FormatExponent(exp, upper);
                    }
                case 'f':
                case 'F': {
                        string res = FormatFixed(value, precision);
                        if (altForm && precision == 0) res += ".";
                        return res;
                    }
                case 'g':
                case 'G':
                    return FormatGeneral(value, precision, altForm, upper);
                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        /// <summary>
        /// Rounds to <paramref name="precision"/> significant digits and uses exponent notation if the exponent is below -4 or at least the precision.
        /// Trailing zeros are removed unless <paramref name="altForm"/> is set.
        /// </summary>
        private static string FormatGeneral(double value, int precision, bool altForm, bool upper) {
            if (precision == 0) precision = 1;

            string mantissa = FormatSignificant(value, precision, out int exp);

            if (exp < -4 || exp >= precision) {
                if (!altForm) mantissa = TrimFraction(mantissa);
                else if (precision == 1) mantissa += ".";
                return mantissa + FormatExponent(exp, upper);
            }

            string digits = precision == 1 ? mantissa : mantissa.Remove(1, 1);
            string intPart, fraction;
            if (exp >= 0) {
                intPart = digits.Substring(0, exp + 1);
                fraction = digits.Substring(exp + 1);
            } else {
                intPart = "0";
                fraction = new string('0', -exp - 1) + digits;
            }
            if (!altForm) fraction = fraction.TrimEnd('0');

            if (fraction.Length > 0) return intPart + "." + fraction;
            if (altForm) return intPart + ".";
            return intPart;

            static string TrimFraction(string mantissa)
                => mantissa.Length == 1 ? mantissa : mantissa.TrimEnd('0').TrimEnd('.');
        }

        /// <summary>
        /// Returns the exponent suffix with a sign and at least two digits, e.g. "e+05" or "E-123".
        /// </summary>
        private static string FormatExponent(int exp, bool upper)
            => (upper ? "E" : "e") + (exp < 0 ? "-" : "+") + Math.Abs(exp).ToString("D2", CultureInfo.InvariantCulture);

        /// <summary>
        /// Rounds to <paramref name="digits"/> significant digits and returns "d.ddd" with the decimal exponent in <paramref name="exp"/>.
        /// </summary>
        private static string FormatSignificant(double value, int digits, out int exp) {
            Debug.Assert(digits > 0);

            if (UseExactDigits(digits - 1)) {
                string rounded;
                if (value == 0) {
                    rounded = new string('0', digits);
                    exp = 0;
                } else {
                    string exact = ExactDigits(value, out int scale);
                    rounded = RoundDigits(exact, digits);
                    exp = exact.Length - scale - 1;
                    if (rounded.Length > digits) {
                        exp++;
                        rounded = rounded.Substring(0, digits);
                    }
                }
                return digits == 1 ? rounded : rounded.Insert(1, ".");
            }

            string res = value.ToString("E" + (digits - 1), CultureInfo.InvariantCulture);
            int idx = res.IndexOf('E');
            exp = int.Parse(res.AsSpan(idx + 1), CultureInfo.InvariantCulture);
            return res.Substring(0, idx);
        }

        /// <summary>
        /// Rounds to <paramref name="precision"/> digits after the decimal point.
        /// </summary>
        private static string FormatFixed(double value, int precision) {
            if (UseExactDigits(precision)) {
                string rounded;
                if (value == 0) {
                    rounded = "";
                } else {
                    string exact = ExactDigits(value, out int scale);
                    int keep = exact.Length - scale + precision;
                    rounded = keep < 0 ? "" : RoundDigits(exact, keep);
                }
                rounded = rounded.PadLeft(precision + 1, '0');
                return precision == 0 ? rounded : rounded.Insert(rounded.Length - precision, ".");
            }

            return value.ToString("F" + precision, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Returns the exact decimal expansion of a finite positive value: value == digits * 10**-scale.
        /// </summary>
        private static string ExactDigits(double value, out int scale) {
            Debug.Assert(value > 0 && !double.IsInfinity(value));

            long bits = BitConverter.DoubleToInt64Bits(value);
            int exp = (int)((bits >> 52) & 0x7FF);
            ulong mantissa = (ulong)bits & 0xFFFFFFFFFFFFFUL;
            if (exp == 0) {
                exp = 1;
            } else {
                mantissa |= 1UL << 52;
            }
            exp -= 1075;

            // m * 2**-k == m * 5**k * 10**-k
            if (exp >= 0) {
                scale = 0;
                return (new BigInteger(mantissa) << exp).ToString(CultureInfo.InvariantCulture);
            }
            scale = -exp;
            return (mantissa * BigInteger.Pow(5, scale)).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Rounds half to even, keeping the first <paramref name="keep"/> digits. The result has one extra digit if rounding carries.
        /// </summary>
        private static string RoundDigits(string digits, int keep) {
            if (keep >= digits.Length) return digits + new string('0', keep - digits.Length);

            bool up = digits[keep] > '5';
            if (digits[keep] == '5') {
                for (int i = keep + 1; i < digits.Length; i++) {
                    if (digits[i] != '0') {
                        up = true;
                        break;
                    }
                }
                if (!up) up = keep > 0 && (digits[keep - 1] - '0') % 2 == 1;
            }

            var res = new StringBuilder(digits, 0, keep, keep + 1);
            if (up) {
                int i = keep - 1;
                while (i >= 0 && res[i] == '9') res[i--] = '0';
                if (i >= 0) res[i]++;
                else res.Insert(0, '1');
            }
            return res.ToString();
        }
    }
}
