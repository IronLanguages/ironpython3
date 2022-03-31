// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

using IronPython.Modules;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Types;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime.Operations {
    public class ExtensibleComplex : Extensible<Complex> {
        public ExtensibleComplex() : base() { }
        public ExtensibleComplex(double real) : base(MathUtils.MakeReal(real)) { }
        public ExtensibleComplex(double real, double imag) : base(new Complex(real, imag)) { }
    }

    public static partial class ComplexOps {
        [StaticExtensionMethod]
        public static object __new__(CodeContext context, PythonType cls) {
            if (cls == TypeCache.Complex) return new Complex();
            return cls.CreateInstance(context);
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext context, PythonType cls, object? real, [Optional] object? imag) {

            // Fast-track for a single argument when type(arg) is complex and no subclasses are involved.
            if (real is Complex && imag is Missing && cls == TypeCache.Complex) return real;

            if (real is string s) return ParseComplex(s, imag);
            if (real is Extensible<string> es) return ParseComplex(es.Value, imag);

            if (imag is string || imag is Extensible<string>) throw PythonOps.TypeError("complex() second arg can't be a string");

            Complex real2;
            if (real is Complex z) {
                real2 = z;
            } else if (!TryInvokeComplex(context, real, out real2)) {
                if (real is Extensible<Complex> ez) {
                    real2 = ez.Value;
                } else if (DoubleOps.TryToFloat(context, real, out double res)) {
                    real2 = res;
                } else {
                    throw PythonOps.TypeErrorForBadInstance("complex() first argument must be a string or a number, not '{0}'", real);
                }
            }

            if (imag is Missing && cls == TypeCache.Complex) return real2;

            Complex imag2;
            if (imag is Missing) {
                imag2 = Complex.Zero;
            } else if (imag is Complex z2) {
                imag2 = z2;
            // surprisingly, no TryInvokeComplex here
            } else if (imag is Extensible<Complex> ez2) {
                imag2 = ez2.Value;
            } else if (DoubleOps.TryToFloat(context, imag, out double res)) {
                imag2 = res;
            } else {
                throw PythonOps.TypeErrorForBadInstance("complex() second argument must be a number, not '{0}'", imag);
            }

            double real3 = real2.Real - imag2.Imaginary;
            double imag3 = real2.Imaginary + imag2.Real;
            if (cls == TypeCache.Complex) {
                return new Complex(real3, imag3);
            } else {
                return cls.CreateInstance(context, real3, imag3);
            }

            static Complex ParseComplex(string s, object? imag) {
                if (imag is Missing) {
                    return LiteralParser.ParseComplex(s);
                } else {
                    throw PythonOps.TypeError($"complex() can't take second arg if first is a string");
                }
            }

            static bool TryInvokeComplex(CodeContext context, object? o, out Complex complex) {
                if (PythonTypeOps.TryInvokeUnaryOperator(context, o, "__complex__", out object? result)) {
                    switch (result) {
                        case Complex z:
                            complex = z;
                            return true;
                        case Extensible<Complex> ez:
                            Warn(context, result);
                            complex = ez.Value;
                            return true;
                        default:
                            throw PythonOps.TypeErrorForBadInstance("__complex__ returned non-complex (type {0})", result);
                    }

                    static void Warn(CodeContext context, object result) {
                        PythonOps.Warn(context, PythonExceptions.DeprecationWarning, $"__complex__ returned non-complex (type {PythonOps.GetPythonTypeName(result)}).  The ability to return an instance of a strict subclass of complex is deprecated, and may be removed in a future version of Python.");
                    }
                }
                complex = default;
                return false;
            }
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext context, PythonType cls, double real) {
            if (cls == TypeCache.Complex) {
                return new Complex(real, 0.0);
            } else {
                return cls.CreateInstance(context, real, 0.0);
            }
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext context, PythonType cls, double real, double imag) {
            if (cls == TypeCache.Complex) {
                return new Complex(real, imag);
            } else {
                return cls.CreateInstance(context, real, imag);
            }
        }

        [SpecialName, PropertyMethod]
        public static double Getreal(Complex self) {
            return self.Real;
        }

        [SpecialName, PropertyMethod]
        public static double Getimag(Complex self) {
            return self.Imaginary;
        }

        #region Binary operators

        [SpecialName]
        public static bool Equals(Complex x, int y) => x.Imaginary == 0 && DoubleOps.Equals(x.Real, y);

        [SpecialName]
        public static bool Equals(Complex x, BigInteger y) => x.Imaginary == 0 && DoubleOps.Equals(x.Real, y);

        [SpecialName]
        public static bool Equals(Complex x, double y) => x.Imaginary == 0 && DoubleOps.Equals(x.Real, y);

        [SpecialName]
        public static bool Equals(Complex x, Complex y) => x == y;

        [SpecialName]
        public static Complex Add(Complex x, Complex y) {
            return x + y;
        }

        [SpecialName]
        public static Complex Subtract(Complex x, Complex y) {
            return x - y;
        }

        [SpecialName]
        public static Complex Multiply(Complex x, Complex y) {
            return x * y;
        }

        [SpecialName]
        public static Complex TrueDivide(Complex x, Complex y) {
            if (y.IsZero()) {
                throw new DivideByZeroException("complex division by zero");
            }

            return x / y;
        }

        [SpecialName]
        public static Complex op_Power(Complex x, Complex y) {
            if (x.IsZero()) {
                if (y.Real < 0.0 || y.Imaginary != 0.0) {
                    throw PythonOps.ZeroDivisionError("0.0 to a negative or complex power");
                }
                return y.IsZero() ? Complex.One : Complex.Zero;
            }

            // Special case for higher precision with real integer powers
            // TODO: A similar check may get added to CLR 4 upon resolution of Dev10 bug 863171,
            // in which case this code should go away.
            if (y.Imaginary == 0.0) {
                int power = (int)y.Real;
                if (power >= 0 && y.Real == power) {
                    Complex res = Complex.One;
                    if (power == 0) {
                        return res;
                    }
                    Complex factor = x;
                    while (power != 0) {
                        if ((power & 1) != 0) {
                            res *= factor;
                        }
                        factor *= factor;
                        power >>= 1;
                    }
                    return res;
                }
            }

            return x.Pow(y);
        }

        [PythonHidden]
        public static Complex Power(Complex x, Complex y) {
            return op_Power(x, y);
        }

        [SpecialName]
        public static Complex FloorDivide(CodeContext context, Complex self, object value)
            => throw PythonOps.TypeError("can't take floor of complex number");

        [SpecialName]
        public static Complex Mod(CodeContext context, Complex self, object value)
            => throw PythonOps.TypeError("can't mod complex numbers");

        [SpecialName]
        public static PythonTuple DivMod(CodeContext context, Complex self, object value)
            => throw PythonOps.TypeError("can't take floor or mod of complex number");

        #endregion

        #region Unary operators

        public static int __hash__(Complex x) {
            if (x.Imaginary == 0) {
                return DoubleOps.__hash__(x.Real);
            }

            int hash = (DoubleOps.__hash__(x.Real) + SysModule.hash_info.imag * DoubleOps.__hash__(x.Imaginary)) % SysModule.hash_info.modulus;
            if (hash == -1) {
                hash = -2;
            }
            return hash;
        }

        public static bool __bool__(Complex x) {
            return !x.IsZero();
        }

        public static Complex conjugate(Complex x) {
            return x.Conjugate();
        }

        public static object __getnewargs__(CodeContext context, Complex self) {
            return PythonTuple.MakeTuple(
                PythonOps.GetBoundAttr(context, self, "real"),
                PythonOps.GetBoundAttr(context, self, "imag")
            );
        }

        public static object __pos__(Complex x) {
            return x;
        }

        #endregion

        public static string __str__(CodeContext/*!*/ context, Complex x) {
            return __repr__(context, x);
        }

        public static string __repr__(CodeContext/*!*/ context, Complex x) {
            if (x.Real != 0 || DoubleOps.IsNegativeZero(x.Real)) {
                if (x.Imaginary < 0 || DoubleOps.IsNegativeZero(x.Imaginary)) {
                    return "(" + FormatComplexValue(context, x.Real) + FormatComplexValue(context, x.Imaginary) + "j)";
                } else /* x.Imaginary is NaN or >= +0.0 */ {
                    return "(" + FormatComplexValue(context, x.Real) + "+" + FormatComplexValue(context, x.Imaginary) + "j)";
                }
            }

            return FormatComplexValue(context, x.Imaginary) + "j";
        }

        public static string __format__(CodeContext/*!*/ context, Complex self, string formatSpec) {
            StringFormatSpec spec = StringFormatSpec.FromString(formatSpec);
            if (spec.AlignmentIsZeroPad) throw PythonOps.ValueError("Zero padding is not allowed in complex format specifier");
            if (spec.Alignment == '=') throw PythonOps.ValueError("'=' alignment flag is not allowed in complex format specifier");

            var sb = new StringBuilder();
            if (spec.AlternateForm) sb.Append('#');
            if (spec.ThousandsComma) sb.Append(',');
            if (spec.Precision.HasValue) {
                sb.Append('.');
                sb.Append(spec.Precision.Value);
            }
            sb.Append(spec.Type ?? 'g');
            var numberformat = sb.ToString();

            string res;
            if (!spec.Type.HasValue) {
                if (self.Real == 0) {
                    res = DoubleOps.__format__(context, self.Imaginary, spec.Sign.HasValue ? spec.Sign.Value + numberformat : numberformat) + "j";
                } else {
                    var real = DoubleOps.__format__(context, self.Real, spec.Sign.HasValue ? spec.Sign.Value + numberformat : numberformat);
                    var imag = DoubleOps.__format__(context, self.Imaginary, "+" + numberformat);
                    res = "(" + real + imag + "j)";
                }
            } else {
                var real = DoubleOps.__format__(context, self.Real, spec.Sign.HasValue ? spec.Sign.Value + numberformat : numberformat);
                var imag = DoubleOps.__format__(context, self.Imaginary, "+" + numberformat);
                res = real + imag + "j";
            }

            if (spec.Width is null) return res;
            return StringOps.__format__(context, res, $"{spec.Fill ?? ' '}{spec.Alignment ?? '>'}{spec.Width}s");
        }

        // report the same errors as CPython for these invalid conversions
        // these operators disappear on complex in Python 3.10
        public static double __float__(Complex self) {
            throw PythonOps.TypeError("can't convert complex to float");
        }

        public static int __int__(Complex self) {
            throw PythonOps.TypeError("can't convert complex to int");
        }

        private static string FormatComplexValue(CodeContext/*!*/ context, double x)
            => DoubleOps.Repr(context, x, trailingZeroAfterWholeFloat: false);

        // Unary Operations
        [SpecialName]
        public static double Abs(Complex x) {
            // CPython returns inf even if one of the values is NaN
            if (double.IsInfinity(x.Real) || double.IsInfinity(x.Imaginary)) return double.PositiveInfinity;

            double res = x.Abs();

            if (double.IsInfinity(res) && !double.IsInfinity(x.Real) && !double.IsInfinity(x.Imaginary)) {
                throw PythonOps.OverflowError("absolute value too large");
            }

            return res;
        }
    }
}
