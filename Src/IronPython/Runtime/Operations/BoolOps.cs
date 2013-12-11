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
using Microsoft.Scripting.Runtime;
using SpecialNameAttribute = System.Runtime.CompilerServices.SpecialNameAttribute;

#if FEATURE_NUMERICS
using System.Numerics;
#else
using Microsoft.Scripting.Math;
using Complex = Microsoft.Scripting.Math.Complex64;
#endif

namespace IronPython.Runtime.Operations {

    public static class BoolOps {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "cls")]
        [StaticExtensionMethod]
        public static object __new__(object cls) {
            return ScriptingRuntimeHelpers.False;
        }

        [StaticExtensionMethod]
        public static bool __new__(object cls, object o) {
            return PythonOps.IsTrue(o);
        }

        [SpecialName]
        public static bool BitwiseAnd(bool x, bool y) {
            return (bool)(x & y);
        }
        
        [SpecialName]
        public static bool BitwiseOr(bool x, bool y) {
            return (bool)(x | y);
        }

        [SpecialName]
        public static bool ExclusiveOr(bool x, bool y) {
            return (bool)(x ^ y);
        }

        [SpecialName]
        public static int BitwiseAnd(int x, bool y) {
            return Int32Ops.BitwiseAnd(y ? 1 : 0, x);
        }

        [SpecialName]
        public static int BitwiseAnd(bool x, int y) {
            return Int32Ops.BitwiseAnd(x ? 1 : 0, y);
        }

        [SpecialName]
        public static int BitwiseOr(int x, bool y) {
            return Int32Ops.BitwiseOr(y ? 1 : 0, x);
        }

        [SpecialName]
        public static int BitwiseOr(bool x, int y) {
            return Int32Ops.BitwiseOr(x ? 1 : 0, y);
        }

        [SpecialName]
        public static int ExclusiveOr(int x, bool y) {
            return Int32Ops.ExclusiveOr(y ? 1 : 0, x);
        }

        [SpecialName]
        public static int ExclusiveOr(bool x, int y) {
            return Int32Ops.ExclusiveOr(x ? 1 : 0, y);
        }

        public static string/*!*/ __repr__(bool self) {
            return self ? "True" : "False";
        }

        public static string/*!*/ __format__(CodeContext/*!*/ context, bool self, [NotNull]string/*!*/ formatSpec)
        {
            return __repr__(self);
        }

        // Binary Operations - Comparisons
        [SpecialName]
        public static bool Equals(bool x, bool y) {
            return x == y;
        }
        [SpecialName]
        public static bool NotEquals(bool x, bool y) {
            return x != y;
        }
        [SpecialName]
        public static bool Equals(bool x, int y) {
            return (x ? 1 : 0) == y;
        }
        [SpecialName]
        public static bool NotEquals(bool x, int y) {
            return (x ? 1 : 0) != y;
        }
        [SpecialName]
        public static bool Equals(int x, bool y) {
            return Equals(y, x);
        }
        [SpecialName]
        public static bool NotEquals(int x, bool y) {
            return NotEquals(y, x);
        }

        // Conversion operators
        [SpecialName, ImplicitConversionMethod]
        public static SByte ConvertToSByte(Boolean x) {
            return (SByte)(x ? 1 : 0);
        }
        [SpecialName, ImplicitConversionMethod]
        public static Byte ConvertToByte(Boolean x) {
            return (Byte)(x ? 1 : 0);
        }
        [SpecialName, ImplicitConversionMethod]
        public static Int16 ConvertToInt16(Boolean x) {
            return (Int16)(x ? 1 : 0);
        }
        [SpecialName, ImplicitConversionMethod]
        public static UInt16 ConvertToUInt16(Boolean x) {
            return (UInt16)(x ? 1 : 0);
        }

        public static Int32 __int__(Boolean x) {
            return (Int32)(x ? 1 : 0);
        }

        [SpecialName, ImplicitConversionMethod]
        public static Int32 ConvertToInt32(Boolean x) {
            return (Int32)(x ? 1 : 0);
        }
        [SpecialName, ImplicitConversionMethod]
        public static UInt32 ConvertToUInt32(Boolean x) {
            return (UInt32)(x ? 1 : 0);
        }
        [SpecialName, ImplicitConversionMethod]
        public static Int64 ConvertToInt64(Boolean x) {
            return (Int64)(x ? 1 : 0);
        }
        [SpecialName, ImplicitConversionMethod]
        public static UInt64 ConvertToUInt64(Boolean x) {
            return (UInt64)(x ? 1 : 0);
        }
        [SpecialName, ImplicitConversionMethod]
        public static Single ConvertToSingle(Boolean x) {
            return (Single)(x ? 1 : 0);
        }
        [SpecialName, ImplicitConversionMethod]
        public static Double ConvertToDouble(Boolean x) {
            return (Double)(x ? 1 : 0);
        }
        [SpecialName, ImplicitConversionMethod]
        public static Complex ConvertToComplex(Boolean x) {
            return x ? Complex.One : Complex.Zero;
        }
        [SpecialName, ImplicitConversionMethod]
        public static decimal ConvertToDecimal(Boolean x) {
            return x ? (decimal)1 : (decimal)0;
        }
    }
}
