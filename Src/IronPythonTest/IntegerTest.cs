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

namespace IronPythonTest {
    public class IntegerTest {

        public static Int32 Int32Int32MaxValue = (Int32)Int32.MaxValue;
        public static Int32 Int32Int32MinValue = (Int32)Int32.MinValue;
        public static Int32 Int32UInt32MinValue = (Int32)UInt32.MinValue;
        public static Int32 Int32Int16MaxValue = (Int32)Int16.MaxValue;
        public static Int32 Int32Int16MinValue = (Int32)Int16.MinValue;
        public static Int32 Int32UInt16MaxValue = (Int32)UInt16.MaxValue;
        public static Int32 Int32UInt16MinValue = (Int32)UInt16.MinValue;
        public static Int32 Int32UInt64MinValue = (Int32)UInt64.MinValue;
        public static Int32 Int32ByteMaxValue = (Int32)Byte.MaxValue;
        public static Int32 Int32ByteMinValue = (Int32)Byte.MinValue;
        public static Int32 Int32SByteMaxValue = (Int32)SByte.MaxValue;
        public static Int32 Int32SByteMinValue = (Int32)SByte.MinValue;
        public static Int32 Int32CharMaxValue = (Int32)Char.MaxValue;
        public static Int32 Int32CharMinValue = (Int32)Char.MinValue;
        public static Int32 Int32Val0 = (Int32)('a');
        public static Int32 Int32Val1 = (Int32)(10);
        public static Int32 Int32Val2 = (Int32)(42);
        public static Int32 Int32Val3 = (Int32)(-24);
        public static Int32 Int32Val6 = (Int32)(0);
        public static Int32 Int32Val7 = (Int32)(1);
        public static Int32 Int32Val8 = (Int32)(-1);
        public static UInt32 UInt32Int32MaxValue = (UInt32)Int32.MaxValue;
        public static UInt32 UInt32UInt32MaxValue = (UInt32)UInt32.MaxValue;
        public static UInt32 UInt32UInt32MinValue = (UInt32)UInt32.MinValue;
        public static UInt32 UInt32Int16MaxValue = (UInt32)Int16.MaxValue;
        public static UInt32 UInt32UInt16MaxValue = (UInt32)UInt16.MaxValue;
        public static UInt32 UInt32UInt16MinValue = (UInt32)UInt16.MinValue;
        public static UInt32 UInt32UInt64MinValue = (UInt32)UInt64.MinValue;
        public static UInt32 UInt32ByteMaxValue = (UInt32)Byte.MaxValue;
        public static UInt32 UInt32ByteMinValue = (UInt32)Byte.MinValue;
        public static UInt32 UInt32SByteMaxValue = (UInt32)SByte.MaxValue;
        public static UInt32 UInt32CharMaxValue = (UInt32)Char.MaxValue;
        public static UInt32 UInt32CharMinValue = (UInt32)Char.MinValue;
        public static UInt32 UInt32Val0 = (UInt32)('a');
        public static UInt32 UInt32Val1 = (UInt32)(10);
        public static UInt32 UInt32Val2 = (UInt32)(42);
        public static UInt32 UInt32Val6 = (UInt32)(0);
        public static UInt32 UInt32Val7 = (UInt32)(1);
        public static Int16 Int16UInt32MinValue = (Int16)UInt32.MinValue;
        public static Int16 Int16Int16MaxValue = (Int16)Int16.MaxValue;
        public static Int16 Int16Int16MinValue = (Int16)Int16.MinValue;
        public static Int16 Int16UInt16MinValue = (Int16)UInt16.MinValue;
        public static Int16 Int16UInt64MinValue = (Int16)UInt64.MinValue;
        public static Int16 Int16ByteMaxValue = (Int16)Byte.MaxValue;
        public static Int16 Int16ByteMinValue = (Int16)Byte.MinValue;
        public static Int16 Int16SByteMaxValue = (Int16)SByte.MaxValue;
        public static Int16 Int16SByteMinValue = (Int16)SByte.MinValue;
        public static Int16 Int16CharMinValue = (Int16)Char.MinValue;
        public static Int16 Int16Val0 = (Int16)('a');
        public static Int16 Int16Val1 = (Int16)(10);
        public static Int16 Int16Val2 = (Int16)(42);
        public static Int16 Int16Val3 = (Int16)(-24);
        public static Int16 Int16Val6 = (Int16)(0);
        public static Int16 Int16Val7 = (Int16)(1);
        public static Int16 Int16Val8 = (Int16)(-1);
        public static UInt16 UInt16UInt32MinValue = (UInt16)UInt32.MinValue;
        public static UInt16 UInt16Int16MaxValue = (UInt16)Int16.MaxValue;
        public static UInt16 UInt16UInt16MaxValue = (UInt16)UInt16.MaxValue;
        public static UInt16 UInt16UInt16MinValue = (UInt16)UInt16.MinValue;
        public static UInt16 UInt16UInt64MinValue = (UInt16)UInt64.MinValue;
        public static UInt16 UInt16ByteMaxValue = (UInt16)Byte.MaxValue;
        public static UInt16 UInt16ByteMinValue = (UInt16)Byte.MinValue;
        public static UInt16 UInt16SByteMaxValue = (UInt16)SByte.MaxValue;
        public static UInt16 UInt16CharMaxValue = (UInt16)Char.MaxValue;
        public static UInt16 UInt16CharMinValue = (UInt16)Char.MinValue;
        public static UInt16 UInt16Val0 = (UInt16)('a');
        public static UInt16 UInt16Val1 = (UInt16)(10);
        public static UInt16 UInt16Val2 = (UInt16)(42);
        public static UInt16 UInt16Val6 = (UInt16)(0);
        public static UInt16 UInt16Val7 = (UInt16)(1);
        public static Int64 Int64Int32MaxValue = (Int64)Int32.MaxValue;
        public static Int64 Int64Int32MinValue = (Int64)Int32.MinValue;
        public static Int64 Int64UInt32MaxValue = (Int64)UInt32.MaxValue;
        public static Int64 Int64UInt32MinValue = (Int64)UInt32.MinValue;
        public static Int64 Int64Int16MaxValue = (Int64)Int16.MaxValue;
        public static Int64 Int64Int16MinValue = (Int64)Int16.MinValue;
        public static Int64 Int64UInt16MaxValue = (Int64)UInt16.MaxValue;
        public static Int64 Int64UInt16MinValue = (Int64)UInt16.MinValue;
        public static Int64 Int64Int64MaxValue = (Int64)Int64.MaxValue;
        public static Int64 Int64Int64MinValue = (Int64)Int64.MinValue;
        public static Int64 Int64UInt64MinValue = (Int64)UInt64.MinValue;
        public static Int64 Int64ByteMaxValue = (Int64)Byte.MaxValue;
        public static Int64 Int64ByteMinValue = (Int64)Byte.MinValue;
        public static Int64 Int64SByteMaxValue = (Int64)SByte.MaxValue;
        public static Int64 Int64SByteMinValue = (Int64)SByte.MinValue;
        public static Int64 Int64CharMaxValue = (Int64)Char.MaxValue;
        public static Int64 Int64CharMinValue = (Int64)Char.MinValue;
        public static Int64 Int64Val0 = (Int64)('a');
        public static Int64 Int64Val1 = (Int64)(10);
        public static Int64 Int64Val2 = (Int64)(42);
        public static Int64 Int64Val3 = (Int64)(-24);
        public static Int64 Int64Val6 = (Int64)(0);
        public static Int64 Int64Val7 = (Int64)(1);
        public static Int64 Int64Val8 = (Int64)(-1);
        public static UInt64 UInt64Int32MaxValue = (UInt64)Int32.MaxValue;
        public static UInt64 UInt64UInt32MaxValue = (UInt64)UInt32.MaxValue;
        public static UInt64 UInt64UInt32MinValue = (UInt64)UInt32.MinValue;
        public static UInt64 UInt64Int16MaxValue = (UInt64)Int16.MaxValue;
        public static UInt64 UInt64UInt16MaxValue = (UInt64)UInt16.MaxValue;
        public static UInt64 UInt64UInt16MinValue = (UInt64)UInt16.MinValue;
        public static UInt64 UInt64Int64MaxValue = (UInt64)Int64.MaxValue;
        public static UInt64 UInt64UInt64MaxValue = (UInt64)UInt64.MaxValue;
        public static UInt64 UInt64UInt64MinValue = (UInt64)UInt64.MinValue;
        public static UInt64 UInt64ByteMaxValue = (UInt64)Byte.MaxValue;
        public static UInt64 UInt64ByteMinValue = (UInt64)Byte.MinValue;
        public static UInt64 UInt64SByteMaxValue = (UInt64)SByte.MaxValue;
        public static UInt64 UInt64CharMaxValue = (UInt64)Char.MaxValue;
        public static UInt64 UInt64CharMinValue = (UInt64)Char.MinValue;
        public static UInt64 UInt64Val0 = (UInt64)('a');
        public static UInt64 UInt64Val1 = (UInt64)(10);
        public static UInt64 UInt64Val2 = (UInt64)(42);
        public static UInt64 UInt64Val6 = (UInt64)(0);
        public static UInt64 UInt64Val7 = (UInt64)(1);
        public static Byte ByteUInt32MinValue = (Byte)UInt32.MinValue;
        public static Byte ByteUInt16MinValue = (Byte)UInt16.MinValue;
        public static Byte ByteUInt64MinValue = (Byte)UInt64.MinValue;
        public static Byte ByteByteMaxValue = (Byte)Byte.MaxValue;
        public static Byte ByteByteMinValue = (Byte)Byte.MinValue;
        public static Byte ByteSByteMaxValue = (Byte)SByte.MaxValue;
        public static Byte ByteCharMinValue = (Byte)Char.MinValue;
        public static Byte ByteVal0 = (Byte)('a');
        public static Byte ByteVal1 = (Byte)(10);
        public static Byte ByteVal2 = (Byte)(42);
        public static Byte ByteVal6 = (Byte)(0);
        public static Byte ByteVal7 = (Byte)(1);
        public static SByte SByteUInt32MinValue = (SByte)UInt32.MinValue;
        public static SByte SByteUInt16MinValue = (SByte)UInt16.MinValue;
        public static SByte SByteUInt64MinValue = (SByte)UInt64.MinValue;
        public static SByte SByteByteMinValue = (SByte)Byte.MinValue;
        public static SByte SByteSByteMaxValue = (SByte)SByte.MaxValue;
        public static SByte SByteSByteMinValue = (SByte)SByte.MinValue;
        public static SByte SByteCharMinValue = (SByte)Char.MinValue;
        public static SByte SByteVal0 = (SByte)('a');
        public static SByte SByteVal1 = (SByte)(10);
        public static SByte SByteVal2 = (SByte)(42);
        public static SByte SByteVal3 = (SByte)(-24);
        public static SByte SByteVal6 = (SByte)(0);
        public static SByte SByteVal7 = (SByte)(1);
        public static SByte SByteVal8 = (SByte)(-1);
        public static Char CharUInt32MinValue = (Char)UInt32.MinValue;
        public static Char CharInt16MaxValue = (Char)Int16.MaxValue;
        public static Char CharUInt16MaxValue = (Char)UInt16.MaxValue;
        public static Char CharUInt16MinValue = (Char)UInt16.MinValue;
        public static Char CharUInt64MinValue = (Char)UInt64.MinValue;
        public static Char CharByteMaxValue = (Char)Byte.MaxValue;
        public static Char CharByteMinValue = (Char)Byte.MinValue;
        public static Char CharSByteMaxValue = (Char)SByte.MaxValue;
        public static Char CharCharMaxValue = (Char)Char.MaxValue;
        public static Char CharCharMinValue = (Char)Char.MinValue;
        public static Char CharVal0 = (Char)('a');
        public static Char CharVal1 = (Char)(10);
        public static Char CharVal2 = (Char)(42);
        public static Char CharVal6 = (Char)(0);
        public static Char CharVal7 = (Char)(1);
        public static Boolean BooleanInt32MaxValue = (Boolean)true;
        public static Boolean BooleanInt32MinValue = (Boolean)true;
        public static Boolean BooleanUInt32MaxValue = (Boolean)true;
        public static Boolean BooleanUInt32MinValue = (Boolean)false;
        public static Boolean BooleanInt16MaxValue = (Boolean)true;
        public static Boolean BooleanInt16MinValue = (Boolean)true;
        public static Boolean BooleanUInt16MaxValue = (Boolean)true;
        public static Boolean BooleanUInt16MinValue = (Boolean)false;
        public static Boolean BooleanInt64MaxValue = (Boolean)true;
        public static Boolean BooleanInt64MinValue = (Boolean)true;
        public static Boolean BooleanUInt64MaxValue = (Boolean)true;
        public static Boolean BooleanUInt64MinValue = (Boolean)false;
        public static Boolean BooleanByteMaxValue = (Boolean)true;
        public static Boolean BooleanByteMinValue = (Boolean)false;
        public static Boolean BooleanSByteMaxValue = (Boolean)true;
        public static Boolean BooleanSByteMinValue = (Boolean)true;
        public static Boolean BooleanVal1 = (Boolean)(true);
        public static Boolean BooleanVal2 = (Boolean)(true);
        public static Boolean BooleanVal3 = (Boolean)(true);
        public static Boolean BooleanVal4 = (Boolean)(true);
        public static Boolean BooleanVal5 = (Boolean)(false);
        public static Boolean BooleanVal6 = (Boolean)(false);
        public static Boolean BooleanVal7 = (Boolean)(true);
        public static Boolean BooleanVal8 = (Boolean)(true);
        public static uint uintT(uint val) {
            return val;
        }
        public static ushort ushortT(ushort val) {
            return val;
        }
        public static ulong ulongT(ulong val) {
            return val;
        }
        public static int intT(int val) {
            return val;
        }
        public static short shortT(short val) {
            return val;
        }
        public static long longT(long val) {
            return val;
        }
        public static byte byteT(byte val) {
            return val;
        }
        public static sbyte sbyteT(sbyte val) {
            return val;
        }
        public static char charT(char val) {
            return val;
        }
        public static bool boolT(bool val) {
            return val;
        }

        public static bool AreEqual(object x, object y) {
            if (x == y) return true;
            if (x == null || y == null) return false;
            return x.ToString() == y.ToString();
        }

    }
}
