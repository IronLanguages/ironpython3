/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

using System;
using System.Runtime.CompilerServices;

namespace IronPythonTest {
    public class Cmplx {
        private double _r;
        private double _i;

        public Cmplx()
            : this(0, 0) {
        }

        public Cmplx(double r)
            : this(r, 0) {
        }

        public Cmplx(double r, double i) {
            this._r = r;
            this._i = i;
        }

        public override int GetHashCode() {
            return _r.GetHashCode() ^ _i.GetHashCode();
        }

        public override bool Equals(object obj) {
            if (obj is Cmplx) {
                Cmplx o = (Cmplx)obj;
                return o._r == _r && o._i == _i;
            } else if (obj is IConvertible) {
                double o = ((IConvertible)obj).ToDouble(null);
                return o == _r && _i == 0;
            }
            return false;
        }

        public override string ToString() {
            return String.Format("({0} + {1}i)", _r, _i);
        }

        public double Real {
            get {
                return _r;
            }
        }

        public double Imag {
            get {
                return _i;
            }
        }

        public static Cmplx operator *(double x, Cmplx y) {
            return new Cmplx(x * y._r, x * y._i);
        }
        public static Cmplx operator *(Cmplx x, double y) {
            return new Cmplx(x._r * y, x._i * y);
        }
        public static Cmplx operator *(Cmplx x, Cmplx y) {
            return new Cmplx(x._r * y._r - x._i * y._i, x._r * y._i + x._i * y._r);
        }
        public static Cmplx operator /(double x, Cmplx y) {
            return new Cmplx(x) / y;
        }
        public static Cmplx operator /(Cmplx x, double y) {
            return new Cmplx(x._r / y, x._i / y);
        }
        public static Cmplx operator /(Cmplx x, Cmplx y) {
            double div = y._r * y._r + y._i * y._i;
            return new Cmplx((x._r * y._r + x._i * y._i) / div, (x._i * y._r - x._r * y._i) / div);
        }
        public static Cmplx operator +(double x, Cmplx y) {
            return new Cmplx(x + y._r, y._i);
        }
        public static Cmplx operator +(Cmplx x, double y) {
            return new Cmplx(x._r + y, x._i);
        }
        public static Cmplx operator +(Cmplx x, Cmplx y) {
            return new Cmplx(x._r + y._r, x._i + y._i);
        }
        public static Cmplx operator -(double x, Cmplx y) {
            return new Cmplx(x - y._r, -y._i);
        }
        public static Cmplx operator -(Cmplx x, double y) {
            return new Cmplx(x._r - y, x._i);
        }
        public static Cmplx operator -(Cmplx x, Cmplx y) {
            return new Cmplx(x._r - y._r, x._i - y._i);
        }
        public static Cmplx operator -(Cmplx x) {
            return new Cmplx(-x._r, -x._i);
        }

        [SpecialName]
        public static Cmplx op_MultiplicationAssignment(Cmplx x, double y) {
            x._r *= y;
            x._i *= y;
            return x;
        }
        [SpecialName]
        public static Cmplx op_MultiplicationAssignment(Cmplx x, Cmplx y) {
            double r = x._r * y._r - x._i * y._i;
            double i = x._r * y._i + x._i * y._r;
            x._r = r;
            x._i = i;
            return x;
        }
        [SpecialName]
        public static Cmplx op_SubtractionAssignment(Cmplx x, double y) {
            x._r -= y;
            return x;
        }
        [SpecialName]
        public static Cmplx op_SubtractionAssignment(Cmplx x, Cmplx y) {
            x._r -= y._r;
            x._i -= y._i;
            return x;
        }
        [SpecialName]
        public static Cmplx op_AdditionAssignment(Cmplx x, double y) {
            x._r += y;
            return x;
        }
        [SpecialName]
        public static Cmplx op_AdditionAssignment(Cmplx x, Cmplx y) {
            x._r += y._r;
            x._i += y._i;
            return x;
        }
        [SpecialName]
        public static Cmplx op_DivisionAssignment(Cmplx x, double y) {
            x._r /= y;
            x._i /= y;
            return x;
        }
        [SpecialName]
        public static Cmplx op_DivisionAssignment(Cmplx x, Cmplx y) {
            double div = y._r * y._r + y._i * y._i;
            double r = (x._r * y._r + x._i * y._i) / div;
            double i = (x._i * y._r - x._r * y._i) / div;
            x._r = r;
            x._i = i;
            return x;
        }
    }

    public class Cmplx2 {
        private double _r;
        private double _i;

        public Cmplx2()
            : this(0, 0) {
        }

        public Cmplx2(double r)
            : this(r, 0) {
        }

        public Cmplx2(double r, double i) {
            this._r = r;
            this._i = i;
        }

        public static Cmplx2 operator +(Cmplx y, Cmplx2 x) {
            return new Cmplx2(x._r + y.Real, x._i + y.Imag);
        }

        public static Cmplx2 operator +(Cmplx2 x, Cmplx y) {
            return new Cmplx2(x._r + y.Real, x._i + y.Imag);
        }
    }
}
