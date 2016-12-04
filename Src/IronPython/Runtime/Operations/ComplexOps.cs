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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Types;

#if !FEATURE_NUMERICS
using Microsoft.Scripting.Math;
using Complex = Microsoft.Scripting.Math.Complex64;
#else
using System.Numerics;
#endif

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
        public static object __new__(
            CodeContext context, 
            PythonType cls,
            [DefaultParameterValue(null)]object real,
            [DefaultParameterValue(null)]object imag
           ) {
            Complex real2, imag2;
            real2 = imag2 = new Complex();

            if (real == null && imag == null && cls == TypeCache.Complex) throw PythonOps.TypeError("argument must be a string or a number");

            if (imag != null) {
                if (real is string) throw PythonOps.TypeError("complex() can't take second arg if first is a string");
                if (imag is string) throw PythonOps.TypeError("complex() second arg can't be a string");
                imag2 = Converter.ConvertToComplex(imag);
            }

            if (real != null) {
                if (real is string) {
                    real2 = LiteralParser.ParseComplex((string)real);
                } else if (real is Extensible<string>) {
                    real2 = LiteralParser.ParseComplex(((Extensible<string>)real).Value);
                } else if (real is Complex) {
                    if (imag == null && cls == TypeCache.Complex) return real;
                    else real2 = (Complex)real;
                } else {
                    real2 = Converter.ConvertToComplex(real);
                }
            }

            double real3 = real2.Real - imag2.Imaginary();
            double imag3 = real2.Imaginary() + imag2.Real;
            if (cls == TypeCache.Complex) {
                return new Complex(real3, imag3);
            } else {
                return cls.CreateInstance(context, real3, imag3);
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
            return self.Imaginary();
        }

        #region Binary operators

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
        public static Complex Divide(Complex x, Complex y) {
            if (y.IsZero()) {
                throw new DivideByZeroException("complex division by zero");
            }

            return x / y;
        }

        [SpecialName]
        public static Complex TrueDivide(Complex x, Complex y) {
            return Divide(x, y);
        }

        [SpecialName]
        public static Complex op_Power(Complex x, Complex y) {
            if (x.IsZero()) {
                if (y.Real < 0.0 || y.Imaginary() != 0.0) {
                    throw PythonOps.ZeroDivisionError("0.0 to a negative or complex power");
                }
                return y.IsZero() ? Complex.One : Complex.Zero;
            }

#if FEATURE_NUMERICS
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
                            res = res * factor;
                        }
                        factor = factor * factor;
                        power >>= 1;
                    }
                    return res;
                }
            }
#endif

            return x.Pow(y);
        }

        [PythonHidden]
        public static Complex Power(Complex x, Complex y) {
            return op_Power(x, y);
        }

        // floordiv for complex numbers is deprecated in the Python 2.
        // specification; this function implements the observable
        // functionality in CPython 2.4: 
        //   Let x, y be complex.
        //   Re(x//y) := floor(Re(x/y))
        //   Im(x//y) := 0
        [SpecialName]
        public static Complex FloorDivide(CodeContext context, Complex x, Complex y) {
            Complex quotient = Divide(x, y);
            return MathUtils.MakeReal(PythonOps.CheckMath(Math.Floor(quotient.Real)));
        }

        // mod for complex numbers is also deprecated. IronPython
        // implements the CPython semantics, that is:
        // x % y = x - (y * (x//y)).
        [SpecialName]
        public static Complex Mod(CodeContext context, Complex x, Complex y) {
            Complex quotient = FloorDivide(context, x, y);
            return x - (quotient * y);
        }

        [SpecialName]
        public static PythonTuple DivMod(CodeContext context, Complex x, Complex y) {
            Complex quotient = FloorDivide(context, x, y);
            return PythonTuple.MakeTuple(quotient, x - (quotient * y));
        }

        #endregion

        #region Unary operators

        public static int __hash__(Complex x) {
            if (x.Imaginary() == 0) {
                return DoubleOps.__hash__(x.Real);
            }
            return x.GetHashCode();
        }

        public static bool __nonzero__(Complex x) {
            return !x.IsZero();
        }

        public static Complex conjugate(Complex x) {
            return x.Conjugate();
        }

        public static object __getnewargs__(CodeContext context, Complex self) {
#if CLR2
            if (!Object.ReferenceEquals(self, null)) {
#endif
                return PythonTuple.MakeTuple(
                    PythonOps.GetBoundAttr(context, self, "real"),
                    PythonOps.GetBoundAttr(context, self, "imag")
                );
#if CLR2
            }
            throw PythonOps.TypeErrorForBadInstance("__getnewargs__ requires a 'complex' object but received a '{0}'", self);
#endif
        }

#if !CLR2
        public static object __pos__(Complex x) {
            return x;
        }
#endif

        #endregion

        public static object __coerce__(Complex x, object y) {
            Complex right;
            if (Converter.TryConvertToComplex(y, out right)) {
#if !CLR2
                if (double.IsInfinity(right.Real) && (y is BigInteger || y is Extensible<BigInteger>)) {
                    throw new OverflowException("long int too large to convert to float");
                }
#endif
                return PythonTuple.MakeTuple(x, right);
            }

            return NotImplementedType.Value;
        }

        public static string __str__(CodeContext/*!*/ context, Complex x) {
            if (x.Real != 0) {
                if (x.Imaginary() < 0 || DoubleOps.IsNegativeZero(x.Imaginary())) {
                    return "(" + FormatComplexValue(context, x.Real) + FormatComplexValue(context, x.Imaginary()) + "j)";
                } else /* x.Imaginary() is NaN or >= +0.0 */ {
                    return "(" + FormatComplexValue(context, x.Real) + "+" + FormatComplexValue(context, x.Imaginary()) + "j)";
                }
            }

            return FormatComplexValue(context, x.Imaginary()) + "j";
        }

        public static string __repr__(CodeContext/*!*/ context, Complex x) {
            return __str__(context, x);
        }

        // report the same errors as CPython for these invalid conversions
        public static double __float__(Complex self) {
            throw PythonOps.TypeError("can't convert complex to float; use abs(z)");
        }

        public static int __int__(Complex self) {
            throw PythonOps.TypeError(" can't convert complex to int; use int(abs(z))");
        }

        public static BigInteger __long__(Complex self) {
            throw PythonOps.TypeError("can't convert complex to long; use long(abs(z))");
        }

        private static string FormatComplexValue(CodeContext/*!*/ context, double x) {
            StringFormatter sf = new StringFormatter(context, "%.6g", x);
            return sf.Format();
        }
        
        // Unary Operations
        [SpecialName]
        public static double Abs(Complex x) {
            double res = x.Abs();

            if (double.IsInfinity(res) && !double.IsInfinity(x.Real) && !double.IsInfinity(x.Imaginary())) {
                throw PythonOps.OverflowError("absolute value too large");
            }

            return res;
        }

        // Binary Operations - Comparisons (eq & ne defined on Complex type as operators)

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "y"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "x"), SpecialName]
        public static bool LessThan(Complex x, Complex y) {
            throw PythonOps.TypeError("complex is not an ordered type");
        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "y"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "x"), SpecialName]
        public static bool LessThanOrEqual(Complex x, Complex y) {
            throw PythonOps.TypeError("complex is not an ordered type");
        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "x"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "y"), SpecialName]
        public static bool GreaterThan(Complex x, Complex y) {
            throw PythonOps.TypeError("complex is not an ordered type");
        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "y"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "x"), SpecialName]
        public static bool GreaterThanOrEqual(Complex x, Complex y) {
            throw PythonOps.TypeError("complex is not an ordered type");
        }

    }
}
