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
using System.Runtime.CompilerServices;
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

namespace IronPython.Runtime.Operations {

    public static partial class BigIntegerOps {
        [StaticExtensionMethod]
        public static object __new__(CodeContext context, PythonType cls, string s, int radix) {
            if (radix == 16 || radix == 8 || radix == 2) {
                s = Int32Ops.TrimRadix(s, radix);
            }

            if (cls == TypeCache.BigInteger) {
                return ParseBigIntegerSign(s, radix);
            } else {
                BigInteger res = ParseBigIntegerSign(s, radix);
                return cls.CreateInstance(context, res);
            }
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext/*!*/ context, PythonType cls, IList<byte> s) {
            object value;
            IPythonObject po = s as IPythonObject;
            if (po == null ||
                !PythonTypeOps.TryInvokeUnaryOperator(DefaultContext.Default, po, "__long__", out value)) {
                    value = ParseBigIntegerSign(s.MakeString(), 10);
            }

            if (cls == TypeCache.BigInteger) {
                return value;
            } else {
                // derived long creation...
                return cls.CreateInstance(context, value);
            }
        }

        private static BigInteger ParseBigIntegerSign(string s, int radix) {
            try {
                return LiteralParser.ParseBigIntegerSign(s, radix);
            } catch (ArgumentException e) {
                throw PythonOps.ValueError(e.Message);
            }
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext context, PythonType cls, object x) {
            Extensible<string> es;

            if (x is string) {
                return ReturnObject(context, cls, ParseBigIntegerSign((string)x, 10));
            } else if ((es = x as Extensible<string>) != null) {
                object value;
                if (PythonTypeOps.TryInvokeUnaryOperator(context, x, "__long__", out value)) {
                    return ReturnObject(context, cls, (BigInteger)value);
                }

                return ReturnObject(context, cls, ParseBigIntegerSign(es.Value, 10));
            }
            if (x is double) return ReturnObject(context, cls, DoubleOps.__long__((double)x));
            if (x is int) return ReturnObject(context, cls, (BigInteger)(int)x);
            if (x is BigInteger) return ReturnObject(context, cls, x);
            
            if (x is Complex) throw PythonOps.TypeError("can't convert complex to long; use long(abs(z))");

            if (x is decimal) {
                return ReturnObject(context, cls, (BigInteger)(decimal)x);
            }

            object result;
            int intRes;
            BigInteger bigintRes;
            if (PythonTypeOps.TryInvokeUnaryOperator(context, x, "__long__", out result) &&
                !Object.ReferenceEquals(result, NotImplementedType.Value) ||
                x is OldInstance &&
                PythonTypeOps.TryInvokeUnaryOperator(context, x, "__int__", out result) &&
                !Object.ReferenceEquals(result, NotImplementedType.Value)) {
                if (result is int || result is BigInteger ||
                    result is Extensible<int> || result is Extensible<BigInteger>) {
                    return ReturnObject(context, cls, result);
                } else {
                    throw PythonOps.TypeError("__long__ returned non-long (type {0})", PythonTypeOps.GetOldName(result));
                }
            } else if (PythonOps.TryGetBoundAttr(context, x, "__trunc__", out result)) {
                result = PythonOps.CallWithContext(context, result);
                if (Converter.TryConvertToInt32(result, out intRes)) {
                    return ReturnObject(context, cls, (BigInteger)intRes);
                } else if (Converter.TryConvertToBigInteger(result, out bigintRes)) {
                    return ReturnObject(context, cls, bigintRes);
                } else {
                    throw PythonOps.TypeError("__trunc__ returned non-Integral (type {0})", PythonTypeOps.GetOldName(result));
                }
            }

            if (x is OldInstance) {
                throw PythonOps.AttributeError("{0} instance has no attribute '__trunc__'",
                    ((OldInstance)x)._class.Name);
            } else {
                throw PythonOps.TypeError("long() argument must be a string or a number, not '{0}'",
                    DynamicHelpers.GetPythonType(x).Name);
            }
        }

        private static object ReturnObject(CodeContext context, PythonType cls, object value) {
            if (cls == TypeCache.BigInteger) {
                return value;
            } else {
                return cls.CreateInstance(context, value);
            }
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext context, PythonType cls) {
            if (cls == TypeCache.BigInteger) {
                return BigInteger.Zero;
            } else {
                return cls.CreateInstance(context, BigInteger.Zero);
            }
        }

        #region Binary operators

        [SpecialName]
        public static object Power(BigInteger x, object y, object z) {
            if (y is int) {
                return Power(x, (int)y, z);
            } else if (y is long) {
                return Power(x, (BigInteger)(long)y, z);
            } else if (y is BigInteger) {
                return Power(x, (BigInteger)y, z);
            }
            return NotImplementedType.Value;
        }

        [SpecialName]
        public static object Power(BigInteger x, int y, object z) {
            if (z is int) {
                return Power(x, y, (BigInteger)(int)z);
            } else if (z is long) {
                return Power(x, y, (BigInteger)(long)z);
            } else if (z is BigInteger) {
                return Power(x, y, (BigInteger)z);
            } else if (z == null) {
                return Power(x, y);
            }
            return NotImplementedType.Value;
        }

        [SpecialName]
        public static object Power(BigInteger x, BigInteger y, object z) {
            if (z is int) {
                return Power(x, y, (BigInteger)(int)z);
            } else if (z is long) {
                return Power(x, y, (BigInteger)(long)z);
            } else if (z is BigInteger) {
                return Power(x, y, (BigInteger)z);
            } else if (z == null) {
                return Power(x, y);
            }
            return NotImplementedType.Value;
        }

        [SpecialName]
        public static object Power(BigInteger x, int y, BigInteger z) {
            if (y < 0) {
                throw PythonOps.TypeError("power", y, "power must be >= 0");
            }
            if (z == BigInteger.Zero) {
                throw PythonOps.ZeroDivisionError();
            }

            BigInteger result = x.ModPow(y, z);

            // fix the sign for negative moduli or negative mantissas
            if ((z < BigInteger.Zero && result > BigInteger.Zero)
                || (z > BigInteger.Zero && result < BigInteger.Zero)) {
                result += z;
            }
            return result;
        }

        [SpecialName]
        public static object Power(BigInteger x, BigInteger y, BigInteger z) {
            if (y < BigInteger.Zero) {
                throw PythonOps.TypeError("power", y, "power must be >= 0");
            }
            if (z == BigInteger.Zero) {
                throw PythonOps.ZeroDivisionError();
            }

            BigInteger result = x.ModPow(y, z);

            // fix the sign for negative moduli or negative mantissas
            if ((z < BigInteger.Zero && result > BigInteger.Zero)
                || (z > BigInteger.Zero && result < BigInteger.Zero)) {
                result += z;
            }
            return result;
        }


        [SpecialName]
        public static object Power([NotNull]BigInteger x, int y) {
            if (y < 0) {
                return DoubleOps.Power(x.ToFloat64(), y);
            }
            return x.Power(y);
        }

        [SpecialName]
        public static object Power([NotNull]BigInteger x, [NotNull]BigInteger y) {
            int yl;
            if (y.AsInt32(out yl)) {
                return Power(x, yl);
            } else {
                if (x == BigInteger.Zero) {
                    if (y.Sign < 0)
                        throw PythonOps.ZeroDivisionError("0.0 cannot be raised to a negative power");
                    return BigInteger.Zero;
                } else if (x == BigInteger.One) {
                    return BigInteger.One;
                } else {
                    throw PythonOps.ValueError("Number too big");
                }
            }
        }

        private static BigInteger DivMod(BigInteger x, BigInteger y, out BigInteger r) {
            BigInteger rr;
            BigInteger qq;

#if !FEATURE_NUMERICS
            if (Object.ReferenceEquals(x, null)) throw PythonOps.TypeError("unsupported operands for div/mod: NoneType and long");
            if (Object.ReferenceEquals(y, null)) throw PythonOps.TypeError("unsupported operands for div/mod: long and NoneType");
#endif

            qq = BigInteger.DivRem(x, y, out rr);

            if (x >= BigInteger.Zero) {
                if (y > BigInteger.Zero) {
                    r = rr;
                    return qq;
                } else {
                    if (rr == BigInteger.Zero) {
                        r = rr;
                        return qq;
                    } else {
                        r = rr + y;
                        return qq - BigInteger.One;
                    }
                }
            } else {
                if (y > BigInteger.Zero) {
                    if (rr == BigInteger.Zero) {
                        r = rr;
                        return qq;
                    } else {
                        r = rr + y;
                        return qq - BigInteger.One;
                    }
                } else {
                    r = rr;
                    return qq;
                }
            }
        }

#if !FEATURE_NUMERICS
        [SpecialName]
        public static BigInteger Add([NotNull]BigInteger x, [NotNull]BigInteger y) {
            return x + y;
        }
        [SpecialName]
        public static BigInteger Subtract([NotNull]BigInteger x, [NotNull]BigInteger y) {
            return x - y;
        }
        [SpecialName]
        public static BigInteger Multiply([NotNull]BigInteger x, [NotNull]BigInteger y) {
            return x * y;
        }
#else
        [PythonHidden]
        public static BigInteger Add(BigInteger x, BigInteger y) {
            return x + y;
        }
        [PythonHidden]
        public static BigInteger Subtract(BigInteger x, BigInteger y) {
            return x - y;
        }
        [PythonHidden]
        public static BigInteger Multiply(BigInteger x, BigInteger y) {
            return x * y;
        }
#endif

        [SpecialName]
        public static BigInteger FloorDivide([NotNull]BigInteger x, [NotNull]BigInteger y) {
            return Divide(x, y);
        }

        [SpecialName]
        public static double TrueDivide([NotNull]BigInteger x, [NotNull]BigInteger y) {
            if (y == BigInteger.Zero) {
                throw new DivideByZeroException();
            }

            // first see if we can keep the two inputs as floats to give a precise result
            double fRes, fDiv;
            if (x.TryToFloat64(out fRes) && y.TryToFloat64(out fDiv)) {
                return fRes / fDiv;
            }

            // otherwise give the user the truncated result if the result fits in a float
            BigInteger rem;
            BigInteger res = BigInteger.DivRem(x, y, out rem);
            if (res.TryToFloat64(out fRes)) {                
                if(rem != BigInteger.Zero) {
                    // try and figure out the fractional portion
                    BigInteger fraction = y / rem;
                    if (fraction.TryToFloat64(out fDiv)) {
                        if (fDiv != 0) {
                            fRes += 1 / fDiv;
                        }
                    }
                }

                return fRes;
            }            

            // otherwise report an error
            throw PythonOps.OverflowError("long/long too large for a float");
        }

#if !FEATURE_NUMERICS
        [SpecialName]
        public static BigInteger Divide([NotNull]BigInteger x, [NotNull]BigInteger y) {
            BigInteger r;
            return DivMod(x, y, out r);
        }

        [SpecialName]
        public static BigInteger Mod([NotNull]BigInteger x, [NotNull]BigInteger y) {
            BigInteger r;
            DivMod(x, y, out r);
            return r;
        }

        [SpecialName]
        public static BigInteger LeftShift([NotNull]BigInteger x, int y) {
            if (y < 0) {
                throw PythonOps.ValueError("negative shift count");
            }
            return x << y;
        }

        [SpecialName]
        public static BigInteger RightShift([NotNull]BigInteger x, int y) {
            if (y < 0) {
                throw PythonOps.ValueError("negative shift count");
            }
            return x >> y;
        }

        [SpecialName]
        public static BigInteger LeftShift([NotNull]BigInteger x, [NotNull]BigInteger y) {
            return LeftShift(x, (int)y);
        }

        [SpecialName]
        public static BigInteger RightShift([NotNull]BigInteger x, [NotNull]BigInteger y) {
            return RightShift(x, (int)y);
        }
#else
        // The op_* nomenclature is required here to avoid name collisions with the
        // PythonHidden methods Divide, Mod, and [Left,Right]Shift.

        [SpecialName]
        public static BigInteger op_Division(BigInteger x, BigInteger y) {
            BigInteger r;
            return DivMod(x, y, out r);
        }

        [SpecialName]
        public static BigInteger op_Modulus(BigInteger x, BigInteger y) {
            BigInteger r;
            DivMod(x, y, out r);
            return r;
        }

        [SpecialName]
        public static BigInteger op_LeftShift(BigInteger x, int y) {
            if (y < 0) {
                throw PythonOps.ValueError("negative shift count");
            }
            return x << y;
        }

        [SpecialName]
        public static BigInteger op_RightShift(BigInteger x, int y) {
            if (y < 0) {
                throw PythonOps.ValueError("negative shift count");
            }
            return x >> y;
        }

        [SpecialName]
        public static BigInteger op_LeftShift(BigInteger x, BigInteger y) {
            return op_LeftShift(x, (int)y);
        }

        [SpecialName]
        public static BigInteger op_RightShift(BigInteger x, BigInteger y) {
            return op_RightShift(x, (int)y);
        }
#endif

        #endregion

        [SpecialName]
        public static PythonTuple DivMod(BigInteger x, BigInteger y) {
            BigInteger div, mod;
            div = DivMod(x, y, out mod);
            return PythonTuple.MakeTuple(div, mod);
        }

        #region Unary operators

        public static object __abs__(BigInteger x) {
            return x.Abs();
        }

        public static bool __nonzero__(BigInteger x) {
            return !x.IsZero();
        }

        [SpecialName]
        public static object Negate(BigInteger x) {
            return -x;
        }

        public static object __pos__(BigInteger x) {
            return x;
        }

        public static object __int__(BigInteger x) {
            // The python spec says __int__  should return a long if needed, rather than overflow.
            int i32;
            if (x.AsInt32(out i32)) {
                return Microsoft.Scripting.Runtime.ScriptingRuntimeHelpers.Int32ToObject(i32);
            }

            return x;
        }

        public static object __float__(BigInteger self) {
            return self.ToFloat64();
        }

        public static string __oct__(BigInteger x) {
            if (x == BigInteger.Zero) {
                return "0L";
            } else if (x > 0) {
                return "0" + x.ToString(8) + "L";
            } else {
                return "-0" + (-x).ToString(8) + "L";
            }
        }

        public static string __hex__(BigInteger x) {
            // CPython 2.5 prints letters in lowercase, with a capital L. 
            if (x < 0) {
                return "-0x" + (-x).ToString(16).ToLower() + "L";
            } else {
                return "0x" + x.ToString(16).ToLower() + "L";
            }
        }

        public static object __getnewargs__(CodeContext context, BigInteger self) {
#if !FEATURE_NUMERICS
            if (!Object.ReferenceEquals(self, null)) {
                return PythonTuple.MakeTuple(BigIntegerOps.__new__(context, TypeCache.BigInteger, self));
            }
            throw PythonOps.TypeErrorForBadInstance("__getnewargs__ requires a 'long' object but received a '{0}'", self);
#else
            return PythonTuple.MakeTuple(BigIntegerOps.__new__(context, TypeCache.BigInteger, self));
#endif
        }

        #endregion

        // These functions make the code generation of other types more regular
#if !FEATURE_NUMERICS
        internal
#else
        [PythonHidden] public
#endif
        static BigInteger OnesComplement(BigInteger x) {
            return ~x;
        }

        internal static BigInteger FloorDivideImpl(BigInteger x, BigInteger y) {
            return FloorDivide(x, y);
        }

#if !FEATURE_NUMERICS
        [SpecialName]
        public static BigInteger BitwiseAnd([NotNull]BigInteger x, [NotNull]BigInteger y) {
            return x & y;
        }
        [SpecialName]
        public static BigInteger BitwiseOr([NotNull]BigInteger x, [NotNull]BigInteger y) {
            return x | y;
        }
        [SpecialName]
        public static BigInteger ExclusiveOr([NotNull]BigInteger x, [NotNull]BigInteger y) {
            return x ^ y;
        }
#else
        [PythonHidden]
        public static BigInteger BitwiseAnd(BigInteger x, BigInteger y) {
            return x & y;
        }
        [PythonHidden]
        public static BigInteger BitwiseOr(BigInteger x, BigInteger y) {
            return x | y;
        }
        [PythonHidden]
        public static BigInteger ExclusiveOr(BigInteger x, BigInteger y) {
            return x ^ y;
        }
#endif

        [PropertyMethod, SpecialName]
        public static BigInteger Getreal(BigInteger self) {
            return self;
        }
        [PropertyMethod, SpecialName]
        public static BigInteger Getimag(BigInteger self) {
            return (BigInteger)0;
        }
        public static BigInteger conjugate(BigInteger self) {
            return self;
        }
        [PropertyMethod, SpecialName]
        public static BigInteger Getnumerator(BigInteger self) {
            return self;
        }
        [PropertyMethod, SpecialName]
        public static BigInteger Getdenominator(BigInteger self) {
            return (BigInteger)1;
        }

        public static int bit_length(BigInteger self) {
            return MathUtils.BitLength(self);
        }

        public static BigInteger __trunc__(BigInteger self) {
            return self;
        }

        [SpecialName, ImplicitConversionMethod]
        public static double ConvertToDouble(BigInteger self) {
            return self.ToFloat64();
        }

        [SpecialName, ExplicitConversionMethod]
        public static int ConvertToInt32(BigInteger self) {
            int res;
            if (self.AsInt32(out res)) return res;

            throw Converter.CannotConvertOverflow("int", self);
        }

        [SpecialName, ExplicitConversionMethod]
        public static Complex ConvertToComplex(BigInteger self) {
            return MathUtils.MakeReal(ConvertToDouble(self));
        }

        [SpecialName, ImplicitConversionMethod]
        public static BigInteger ConvertToBigInteger(bool self) {
            return self ? BigInteger.One : BigInteger.Zero;
        }

        [SpecialName]
        public static int Compare(BigInteger x, BigInteger y) {
            return x.CompareTo(y);
        }

        [SpecialName]
        public static int Compare(BigInteger x, int y) {
            int ix;
            if (x.AsInt32(out ix)) {                
                return ix == y ? 0 : ix > y ? 1 : -1;
            }

            return BigInteger.Compare(x, y);
        }

        [SpecialName]
        public static int Compare(BigInteger x, uint y) {
            uint ix;
            if (x.AsUInt32(out ix)) {
                return ix == y ? 0 : ix > y ? 1 : -1;
            }

            return BigInteger.Compare(x, y);
        }

        [SpecialName]
        public static int Compare(BigInteger x, double y) {
            return -((int)DoubleOps.Compare(y, x));
        }

        [SpecialName]
        public static int Compare(BigInteger x, [NotNull]Extensible<double> y) {
            return -((int)DoubleOps.Compare(y.Value, x));
        }

        [SpecialName]
        public static int Compare(BigInteger x, decimal y) {            
            return DecimalOps.__cmp__(x, y);
        }

        [SpecialName]
        public static int Compare(BigInteger x, bool y) {
            return Compare(x, y ? 1 : 0);
        }

        public static BigInteger __long__(BigInteger self) {
            return self;
        }

        public static BigInteger __index__(BigInteger self) {
            return self;
        }

        public static int __hash__(BigInteger self) {
#if CLR4 // TODO: we might need our own hash code implementation. This avoids assertion failure.
            if (self == -2147483648) {
                return -2147483648;
            }
#endif

            // check if it's in the Int64 or UInt64 range, and use the built-in hashcode for that instead
            // this ensures that objects added to dictionaries as (U)Int64 can be looked up with Python longs
            Int64 i64;
            if (self.AsInt64(out i64)) {
                return Int64Ops.__hash__(i64);
            } else {
                UInt64 u64;
                if (self.AsUInt64(out u64)) {
                    return UInt64Ops.__hash__(u64);
                }
            }

            // Call the DLR's BigInteger hash function, which will return an int32 representation of
            // b if b is within the int32 range. We use that as an optimization for hashing, and 
            // assert the assumption below.
            int hash = self.GetHashCode();
#if DEBUG
            int i;
            if (self.AsInt32(out i)) {
                Debug.Assert(i == hash, String.Format("hash({0}) == {1}", i, hash));
            }
#endif
            return hash;
        }

        public static string __repr__([NotNull]BigInteger/*!*/ self) {
            return self.ToString() + "L";
        }

        public static object __coerce__(CodeContext context, BigInteger self, object o) {
            // called via builtin.coerce()
            BigInteger val;
            if (Converter.TryConvertToBigInteger(o, out val)) {
                return PythonTuple.MakeTuple(self, val);
            }
            return NotImplementedType.Value;
        }

        #region Backwards compatibility with BigIntegerV2

        [PythonHidden]
        public static float ToFloat(BigInteger/*!*/ self) {
            return checked((float)self.ToFloat64());
        }

#if FEATURE_NUMERICS
        #region Binary Ops
        
        [PythonHidden]
        public static BigInteger Xor(BigInteger x, BigInteger y) {
            return x ^ y;
        }

        [PythonHidden]
        public static BigInteger Divide(BigInteger x, BigInteger y) {
            return op_Division(x, y);
        }

        [PythonHidden]
        public static BigInteger Mod(BigInteger x, BigInteger y) {
            return op_Modulus(x, y);
        }

        [PythonHidden]
        public static BigInteger LeftShift(BigInteger x, int y) {
            return op_LeftShift(x, y);
        }

        [PythonHidden]
        public static BigInteger RightShift(BigInteger x, int y) {
            return op_RightShift(x, y);
        }

        [PythonHidden]
        public static BigInteger LeftShift(BigInteger x, BigInteger y) {
            return op_LeftShift(x, y);
        }

        [PythonHidden]
        public static BigInteger RightShift(BigInteger x, BigInteger y) {
            return op_RightShift(x, y);
        }

        #endregion

        #region 'As' Conversions

        [PythonHidden]
        public static bool AsDecimal(BigInteger self, out decimal res) {
            if (self <= (BigInteger)decimal.MaxValue && self >= (BigInteger)decimal.MinValue) {
                res = (decimal)self;
                return true;
            }
            res = default(decimal);
            return false;
        }

        [PythonHidden]
        public static bool AsInt32(BigInteger self, out int res) {
            return self.AsInt32(out res);
        }

        [PythonHidden]
        public static bool AsInt64(BigInteger self, out long res) {
            return self.AsInt64(out res);
        }

        [CLSCompliant(false), PythonHidden]
        public static bool AsUInt32(BigInteger self, out uint res) {
            return self.AsUInt32(out res);
        }

        [CLSCompliant(false), PythonHidden]
        public static bool AsUInt64(BigInteger self, out ulong res) {
            return self.AsUInt64(out res);
        }
        
        #endregion

        #region Direct Conversions

        [PythonHidden]
        public static int ToInt32(BigInteger self) {
            return (int)self;
        }

        [PythonHidden]
        public static long ToInt64(BigInteger self) {
            return (long)self;
        }

        [CLSCompliant(false), PythonHidden]
        public static uint ToUInt32(BigInteger self) {
            return (uint)self;
        }

        [CLSCompliant(false), PythonHidden]
        public static ulong ToUInt64(BigInteger self) {
            return (ulong)self;
        }

        #endregion

        #region Mimic some IConvertible members

        [PythonHidden]
        public static bool ToBoolean(BigInteger self, IFormatProvider provider) {
            return !self.IsZero;
        }

        [PythonHidden]
        public static byte ToByte(BigInteger self, IFormatProvider provider) {
            return (byte)self;
        }

        [CLSCompliant(false), PythonHidden]
        public static sbyte ToSByte(BigInteger self, IFormatProvider provider) {
            return (sbyte)self;
        }

        [PythonHidden]
        public static char ToChar(BigInteger self, IFormatProvider provider) {
            int res;
            if (self.AsInt32(out res) && res <= Char.MaxValue && res >= Char.MinValue) {
                return (char)res;
            }
            throw new OverflowException("big integer won't fit into char");
        }

        [PythonHidden]
        public static decimal ToDecimal(BigInteger self, IFormatProvider provider) {
            return (decimal)self;
        }

        [PythonHidden]
        public static double ToDouble(BigInteger self, IFormatProvider provider) {
            return ConvertToDouble(self);
        }

        [PythonHidden]
        public static float ToSingle(BigInteger self, IFormatProvider provider) {
            return ToFloat(self);
        }

        [PythonHidden]
        public static short ToInt16(BigInteger self, IFormatProvider provider) {
            return (short)self;
        }

        [PythonHidden]
        public static int ToInt32(BigInteger self, IFormatProvider provider) {
            return (int)self;
        }

        [PythonHidden]
        public static long ToInt64(BigInteger self, IFormatProvider provider) {
            return (long)self;
        }

        [CLSCompliant(false), PythonHidden]
        public static ushort ToUInt16(BigInteger self, IFormatProvider provider) {
            return (ushort)self;
        }

        [CLSCompliant(false), PythonHidden]
        public static uint ToUInt32(BigInteger self, IFormatProvider provider) {
            return (uint)self;
        }

        [CLSCompliant(false), PythonHidden]
        public static ulong ToUInt64(BigInteger self, IFormatProvider provider) {
            return (ulong)self;
        }

        [PythonHidden]
        public static object ToType(BigInteger self, Type conversionType, IFormatProvider provider) {
            if (conversionType == typeof(BigInteger)) {
                return self;
            }
            throw new NotImplementedException();
        }

        [PythonHidden]
        public static TypeCode GetTypeCode(BigInteger self) {
            return TypeCode.Object;
        }

        #endregion

        [PythonHidden]
        public static BigInteger Square(BigInteger self) {
            return self * self;
        }

        [PythonHidden]
        public static bool IsNegative(BigInteger self) {
            return self.Sign < 0;
        }

        [PythonHidden]
        public static bool IsPositive(BigInteger self) {
            return self.Sign > 0;
        }

        [PythonHidden]
        public static int GetBitCount(BigInteger self) {
            return self.GetBitCount();
        }

        [PythonHidden]
        public static int GetByteCount(BigInteger self) {
            return self.GetByteCount();
        }

        #region 'Create' Methods

        [PythonHidden]
        public static BigInteger Create(byte[] v) {
            return new BigInteger(v);
        }

        [PythonHidden]
        public static BigInteger Create(int v) {
            return new BigInteger(v);
        }

        [PythonHidden]
        public static BigInteger Create(long v) {
            return new BigInteger(v);
        }

        [CLSCompliant(false), PythonHidden]
        public static BigInteger Create(uint v) {
            return new BigInteger(v);
        }

        [CLSCompliant(false), PythonHidden]
        public static BigInteger Create(ulong v) {
            return (BigInteger)v;
        }

        [PythonHidden]
        public static BigInteger Create(decimal v) {
            return new BigInteger(v);
        }

        [PythonHidden]
        public static BigInteger Create(double v) {
            return new BigInteger(v);
        }

        #endregion

        #region Expose BigIntegerV2-style uint data

        [CLSCompliant(false), PythonHidden]
        public static uint[] GetWords(BigInteger self) {
            return self.GetWords();
        }

        [CLSCompliant(false), PythonHidden]
        public static uint GetWord(BigInteger self, int index) {
            return self.GetWord(index);
        }

        [PythonHidden]
        public static int GetWordCount(BigInteger self) {
            return self.GetWordCount();
        }

        #endregion
#endif

        #endregion

        public static string/*!*/ __format__(CodeContext/*!*/ context, BigInteger/*!*/ self, [NotNull]string/*!*/ formatSpec) {
            StringFormatSpec spec = StringFormatSpec.FromString(formatSpec);

            if (spec.Precision != null) {
                throw PythonOps.ValueError("Precision not allowed in integer format specifier");
            }

            BigInteger val = self;
            if (self < 0) {
                val = -self;
            }
            string digits;
            
            switch (spec.Type) {
                case 'n':
                    CultureInfo culture = PythonContext.GetContext(context).NumericCulture;

                    if (culture == CultureInfo.InvariantCulture) {
                        // invariant culture maps to CPython's C culture, which doesn't
                        // include any formatting info.
                        goto case 'd';
                    }

                    digits = FormattingHelper.ToCultureString(val, PythonContext.GetContext(context).NumericCulture.NumberFormat, spec);
                    break;
#if !FEATURE_NUMERICS
                case null:
                case 'd':
                    digits = val.ToString();
                    break;
                case '%':
                    if (val == BigInteger.Zero) {
                        digits = "0.000000%";
                    } else {
                        digits = val.ToString() + "00.000000%";
                    }
                    break;
                case 'e': digits = ToExponent(val, true, 6, 7); break;
                case 'E': digits = ToExponent(val, false, 6, 7); break;
                case 'f':
                    if (val != BigInteger.Zero) {
                        digits = val.ToString() + ".000000";
                    } else {
                        digits = "0.000000";
                    }
                    break;
                case 'F':
                    if (val != BigInteger.Zero) {
                        digits = val.ToString() + ".000000";
                    } else {
                        digits = "0.000000";
                    }
                    break;
                case 'g':
                    if (val >= 1000000) {
                        digits = ToExponent(val, true, 0, 6);
                    } else {
                        digits = val.ToString();
                    }
                    break;
                case 'G':                    
                    if (val >= 1000000) {
                        digits = ToExponent(val, false, 0, 6);
                    } else {
                        digits = val.ToString();
                    }
                    break;
#else
                case null:
                case 'd':
                    if (spec.ThousandsComma) {
                        var width = spec.Width ?? 0;
                        // If we're inserting commas, and we're padding with leading zeros.
                        // AlignNumericText won't know where to place the commas,
                        // so force .Net to help us out here.
                        if (spec.Fill.HasValue && spec.Fill.Value == '0' && width > 1) {
                            digits = val.ToString(FormattingHelper.ToCultureString(self, FormattingHelper.InvariantCommaNumberInfo, spec));
                        }
                        else {
                        digits = val.ToString("#,0", CultureInfo.InvariantCulture);
                        }
                    }
                    else {
                        digits = val.ToString("D", CultureInfo.InvariantCulture);
                    }
                    break;
                case '%':
                    if (spec.ThousandsComma) {
                        digits = val.ToString("#,0.000000%", CultureInfo.InvariantCulture);
                    } else {
                        digits = val.ToString("0.000000%", CultureInfo.InvariantCulture);
                    }
                    break;
                case 'e':
                    if (spec.ThousandsComma) {
                        digits = val.ToString("#,0.000000e+00", CultureInfo.InvariantCulture);
                    } else {
                        digits = val.ToString("0.000000e+00", CultureInfo.InvariantCulture);
                    }
                    break;
                case 'E':
                    if (spec.ThousandsComma) {
                        digits = val.ToString("#,0.000000E+00", CultureInfo.InvariantCulture);
                    } else {
                        digits = val.ToString("0.000000E+00", CultureInfo.InvariantCulture);
                    }
                    break;
                case 'f':
                case 'F':
                    if (spec.ThousandsComma) {
                        digits = val.ToString("#,########0.000000", CultureInfo.InvariantCulture);
                    } else {
                        digits = val.ToString("#########0.000000", CultureInfo.InvariantCulture);
                    }
                    break;
                case 'g':
                    if (val >= 1000000) {
                        digits = val.ToString("0.#####e+00", CultureInfo.InvariantCulture);
                    } else if (spec.ThousandsComma) {
                        goto case 'd';
                    } else {
                        digits = val.ToString(CultureInfo.InvariantCulture);
                    }
                    break;
                case 'G':
                    if (val >= 1000000) {
                        digits = val.ToString("0.#####E+00", CultureInfo.InvariantCulture);
                    } else if (spec.ThousandsComma) {
                        goto case 'd';
                    } else {
                        digits = val.ToString(CultureInfo.InvariantCulture);
                    }
                    break;
#endif
                case 'X':
                    digits = AbsToHex(val, false);
                    break;
                case 'x':
                    digits = AbsToHex(val, true);
                    break;
                case 'o': // octal
                    digits = ToOctal(val, true);
                    break;
                case 'b': // binary
                    digits = ToBinary(val, false, true);
                    break;
                case 'c': // single char
                    int iVal;
                    if (spec.Sign != null) {
                        throw PythonOps.ValueError("Sign not allowed with integer format specifier 'c'");
                    } else if (!self.AsInt32(out iVal)) {
                        throw PythonOps.OverflowError("long int too large to convert to int");
                    } else if(iVal < 0 || iVal > 0xFF) {
                        throw PythonOps.OverflowError("%c arg not in range(0x10000)");
                    }

                    digits = ScriptingRuntimeHelpers.CharToString((char)iVal);
                    break;
                default:
                    throw PythonOps.ValueError("Unknown format code '{0}'", spec.Type.ToString());
            }

            Debug.Assert(digits[0] != '-');

            return spec.AlignNumericText(digits, self.IsZero(), self.IsPositive());
        }

        internal static string AbsToHex(BigInteger val, bool lowercase) {
            return ToDigits(val, 16, lowercase);
        }

        private static string ToOctal(BigInteger val, bool lowercase) {
            return ToDigits(val, 8, lowercase);
        }

        internal static string ToBinary(BigInteger val) {            
            string res = ToBinary(val.Abs(), true, true);
            if (val.IsNegative()) {
                res = "-" + res;
            }
            return res;
        }

        private static string ToBinary(BigInteger val, bool includeType, bool lowercase) {
            Debug.Assert(!val.IsNegative());

            string digits;
            digits = ToDigits(val, 2, lowercase);
            
            if (includeType) {
                digits = (lowercase ? "0b" : "0B") + digits;
            }
            return digits;
        }

        private static string/*!*/ ToExponent(BigInteger/*!*/ self, bool lower, int minPrecision, int maxPrecision) {
            Debug.Assert(minPrecision <= maxPrecision);

            // get all the digits
            string digits = self.ToString();

            StringBuilder tmp = new StringBuilder();
            tmp.Append(digits[0]);
            
            for (int i = 1; i < maxPrecision && i < digits.Length; i++) {
                // append if we have a significant digit or if we are forcing a minimum precision
                if (digits[i] != '0' || i <= minPrecision) {
                    if (tmp.Length == 1) {
                        // first time we've appended, add the decimal point now
                        tmp.Append('.');
                    }

                    while (i > tmp.Length - 1) {
                        // add any digits that we skipped before
                        tmp.Append('0');
                    }

                    // round up last digit if necessary
                    if (i == maxPrecision - 1 && i != digits.Length - 1 && digits[i + 1] >= '5') {
                        tmp.Append((char)(digits[i] + 1));
                    } else {
                        tmp.Append(digits[i]);
                    }
                }
            }

            if (digits.Length <= minPrecision) {
                if (tmp.Length == 1) {
                    // first time we've appended, add the decimal point now
                    tmp.Append('.');
                }

                while (minPrecision >= tmp.Length - 1) {
                    tmp.Append('0');
                }
            }

            tmp.Append(lower ? "e+" : "E+");
            int digitCnt = digits.Length - 1;
            if (digitCnt < 10) {
                tmp.Append('0');
                tmp.Append((char)('0' + digitCnt));
            } else {
                tmp.Append(digitCnt.ToString());
            }

            digits = tmp.ToString();
            return digits;
        }

        private static string/*!*/ ToDigits(BigInteger/*!*/ val, int radix, bool lower) {
            if (val.IsZero()) {
                return "0";
            }

            StringBuilder str = new StringBuilder();

            while (val != 0) {
                int digit = (int)(val % radix);
                if (digit < 10) str.Append((char)((digit) + '0'));
                else if (lower) str.Append((char)((digit - 10) + 'a'));
                else str.Append((char)((digit - 10) + 'A'));

                val /= radix;
            }

            StringBuilder res = new StringBuilder(str.Length);
            for (int i = str.Length - 1; i >= 0; i--) {
                res.Append(str[i]);
            }
            
            return res.ToString();
        }
    }
}
