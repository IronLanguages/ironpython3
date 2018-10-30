// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Types;
using IronPython.Modules;

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
            object real=null,
            object imag=null
           ) {
            var real2 = new Complex();
            var imag2 = new Complex();
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
        public static Complex TrueDivide(Complex x, Complex y) {
            if (y.IsZero()) {
                throw new DivideByZeroException("complex division by zero");
            }

            return x / y;
        }

        [SpecialName]
        public static Complex op_Power(Complex x, Complex y) {
            if (x.IsZero()) {
                if (y.Real < 0.0 || y.Imaginary() != 0.0) {
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
                            res = res * factor;
                        }
                        factor = factor * factor;
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
        public static Complex FloorDivide(CodeContext context, Complex x, Complex y) {
            throw PythonOps.TypeError("can't take floor of complex number");
        }

        [SpecialName]
        public static Complex Mod(CodeContext context, Complex x, Complex y) {
            throw PythonOps.TypeError("can't mod complex numbers");
        }

        [SpecialName]
        public static PythonTuple DivMod(CodeContext context, Complex x, Complex y) {
            throw PythonOps.TypeError("can't take floor or mod of complex number");
        }

        #endregion

        #region Unary operators

        public static int __hash__(Complex x) {
            if (x.Imaginary() == 0) {
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
