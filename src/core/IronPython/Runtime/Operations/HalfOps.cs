// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

#if NET6_0_OR_GREATER

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Types;

namespace IronPython.Runtime.Operations {
    public static class HalfOps {

        [StaticExtensionMethod]
        public static object __new__(PythonType cls) {
            CheckHalfType(cls);
            return default(Half);
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext/*!*/ context, PythonType cls, object x) {
            CheckHalfType(cls);
            if (x is Half h) return h;
            if (Converter.TryConvertToDouble(x, out double doubleVal)) return (Half)doubleVal;

            return (Half)(double)DoubleOps.NewFloat(context, TypeCache.Double, x);
        }

        public static PythonTuple __getnewargs__(Half self) => PythonTuple.MakeTuple((double)self);

        [PropertyMethod, SpecialName]
        public static Half Getreal(Half self) => self;

        [PropertyMethod, SpecialName]
        public static Half Getimag(Half self) => default;

        public static Half conjugate(Half self) => self;

        public static string __format__(CodeContext/*!*/ context, Half self, [NotNone] string/*!*/ formatSpec) {
            // format(x, '') must be equal to str(x), which is not the same as str((double)x)
            if (formatSpec.Length == 0) return __str__(self);

            return DoubleOps.__format__(context, (double)self, formatSpec);
        }

        public static string __str__(Half self) => __repr__(self);

        /// <summary>
        ///   Returns the shortest decimal representation that round-trips to the same <see cref="Half"/> value.
        /// </summary>
        /// <remarks>
        ///   <para>The layout follows NumPy's <c>str(numpy.float16)</c>: fixed notation for zero and for
        ///   magnitudes in [1e-4, 1e3), scientific notation otherwise, e.g. <c>0.1</c>, <c>999.5</c>,
        ///   <c>1e+03</c>, <c>6.55e+04</c>, <c>6e-08</c>, <c>-0.0</c>, <c>inf</c>, <c>nan</c>.</para>
        /// 
        ///   <para>Note: <c>repr(Half.MaxValue) == '6.55e+04'</c> and <c>Half('6.55e+04') == Half.MaxValue</c>
        ///   although <c>float(Half.MaxValue) == 65504</c>.</para>
        /// </remarks>
        public static string __repr__(Half self) {
            if (Half.IsNaN(self)) return "nan";
            if (Half.IsPositiveInfinity(self)) return "inf";
            if (Half.IsNegativeInfinity(self)) return "-inf";

            // .NET produces the shortest round-trippable digits, in the form [-]ddd[.ddd][E-dd]
            string r = self.ToString("R", CultureInfo.InvariantCulture);
            bool isNegative = r[0] == '-';
            if (isNegative) r = r.Substring(1);

            int exp = 0;
            int ePos = r.IndexOf('E');
            if (ePos >= 0) {
                exp = int.Parse(r.Substring(ePos + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                r = r.Substring(0, ePos);
            }
            int dotPos = r.IndexOf('.');
            if (dotPos < 0) dotPos = r.Length;

            // value is 0.digits * 10**decPoint
            string digits = r.Remove(dotPos, Math.Min(1, r.Length - dotPos));
            int decPoint = dotPos + exp;
            int numLeadingZeros = digits.Length - digits.TrimStart('0').Length;
            digits = digits.Trim('0');
            decPoint -= numLeadingZeros;

            var sb = new StringBuilder(16);
            if (isNegative) sb.Append('-');

            double abs = Math.Abs((double)self);
            if (digits.Length == 0) {
                sb.Append("0.0");
            } else if (1e-4 <= abs && abs < 1e3) {
                if (decPoint <= 0) {
                    sb.Append("0.").Append('0', -decPoint).Append(digits);
                } else if (decPoint >= digits.Length) {
                    sb.Append(digits).Append('0', decPoint - digits.Length).Append(".0");
                } else {
                    sb.Append(digits, 0, decPoint).Append('.').Append(digits, decPoint, digits.Length - decPoint);
                }
            } else {
                sb.Append(digits[0]);
                if (digits.Length > 1) sb.Append('.').Append(digits, 1, digits.Length - 1);
                int sciExp = decPoint - 1;
                sb.Append(sciExp < 0 ? "e-" : "e+").Append(Math.Abs(sciExp).ToString("00", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        private static void CheckHalfType(PythonType cls) {
            if (cls != DynamicHelpers.GetPythonTypeFromType(typeof(Half))) {
                throw PythonOps.TypeError("Half.__new__: first argument must be Half type.");
            }
        }
    }
}

#endif
