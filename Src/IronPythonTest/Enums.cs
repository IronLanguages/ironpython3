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
    [Flags]
    public enum DaysInt : int {
        Mon = 0x01,
        Tue = 0x02,
        Wed = 0x04,
        Thu = 0x08,
        Fri = 0x10,
        Sat = 0x20,
        Sun = 0x40,
        None = 0,
        Weekdays = Mon | Tue | Wed | Thu | Fri,
        Weekend = Sat | Sun,
        All = Weekdays | Weekend
    }
    [Flags]
    public enum DaysShort : short {
        Mon = 0x01,
        Tue = 0x02,
        Wed = 0x04,
        Thu = 0x08,
        Fri = 0x10,
        Sat = 0x20,
        Sun = 0x40,
        None = 0,
        Weekdays = Mon | Tue | Wed | Thu | Fri,
        Weekend = Sat | Sun,
        All = Weekdays | Weekend
    }
    [Flags]
    public enum DaysLong : long {
        Mon = 0x01,
        Tue = 0x02,
        Wed = 0x04,
        Thu = 0x08,
        Fri = 0x10,
        Sat = 0x20,
        Sun = 0x40,
        None = 0,
        Weekdays = Mon | Tue | Wed | Thu | Fri,
        Weekend = Sat | Sun,
        All = Weekdays | Weekend
    }
    [Flags]
    public enum DaysSByte : sbyte {
        Mon = 0x01,
        Tue = 0x02,
        Wed = 0x04,
        Thu = 0x08,
        Fri = 0x10,
        Sat = 0x20,
        Sun = 0x40,
        None = 0,
        Weekdays = Mon | Tue | Wed | Thu | Fri,
        Weekend = Sat | Sun,
        All = Weekdays | Weekend
    }
    [Flags]
    public enum DaysByte : byte {
        Mon = 0x01,
        Tue = 0x02,
        Wed = 0x04,
        Thu = 0x08,
        Fri = 0x10,
        Sat = 0x20,
        Sun = 0x40,
        None = 0,
        Weekdays = Mon | Tue | Wed | Thu | Fri,
        Weekend = Sat | Sun,
        All = Weekdays | Weekend
    }
    [Flags]
    public enum DaysUShort : ushort {
        Mon = 0x01,
        Tue = 0x02,
        Wed = 0x04,
        Thu = 0x08,
        Fri = 0x10,
        Sat = 0x20,
        Sun = 0x40,
        None = 0,
        Weekdays = Mon | Tue | Wed | Thu | Fri,
        Weekend = Sat | Sun,
        All = Weekdays | Weekend
    }

    [Flags]
    public enum DaysUInt : uint {
        Mon = 0x01,
        Tue = 0x02,
        Wed = 0x04,
        Thu = 0x08,
        Fri = 0x10,
        Sat = 0x20,
        Sun = 0x40,
        None = 0,
        Weekdays = Mon | Tue | Wed | Thu | Fri,
        Weekend = Sat | Sun,
        All = Weekdays | Weekend
    }

    [Flags]
    public enum DaysULong : ulong {
        Mon = 0x01,
        Tue = 0x02,
        Wed = 0x04,
        Thu = 0x08,
        Fri = 0x10,
        Sat = 0x20,
        Sun = 0x40,
        None = 0,
        Weekdays = Mon | Tue | Wed | Thu | Fri,
        Weekend = Sat | Sun,
        All = Weekdays | Weekend
    }

    public class EnumTest {
        public virtual DaysInt TestDaysInt() {
            return DaysInt.Weekend;
        }
        public virtual DaysShort TestDaysShort() {
            return DaysShort.Weekend;
        }
        public virtual DaysLong TestDaysLong() {
            return DaysLong.Weekend;
        }
        public virtual DaysSByte TestDaysSByte() {
            return DaysSByte.Weekend;
        }
        public virtual DaysByte TestDaysByte() {
            return DaysByte.Weekend;
        }
        public virtual DaysUShort TestDaysUShort() {
            return DaysUShort.Weekend;
        }
        public virtual DaysUInt TestDaysUInt() {
            return DaysUInt.Weekend;
        }
        public virtual DaysULong TestDaysULong() {
            return DaysULong.Weekend;
        }

        public static void TestEnumInt(int arg) {
        }
        public static void TestEnumShort(short arg) {
        }
        public static void TestEnumLong(long arg) {
        }
        public static void TestEnumSByte(sbyte arg) {
        }
        public static void TestEnumUInt(uint arg) {
        }
        public static void TestEnumUShort(ushort arg) {
        }
        public static void TestEnumULong(ulong arg) {
        }
        public static void TestEnumByte(byte arg) {
        }
        public static void TestEnumBoolean(bool arg) {
        }
    }

    public enum EnumSByte : sbyte {
        Zero = 0,
        MinSByte = SByte.MinValue,
        MaxSByte = SByte.MaxValue,
    }
    public enum EnumByte : byte {
        Zero = 0,
        MaxSByte = (byte)SByte.MaxValue,
        MinByte = Byte.MinValue,
        MaxByte = Byte.MaxValue,
    }
    public enum EnumShort : short {
        Zero = 0,
        MinSByte = SByte.MinValue,
        MaxSByte = SByte.MaxValue,
        MinByte = Byte.MinValue,
        MaxByte = Byte.MaxValue,
        MinShort = Int16.MinValue,
        MaxShort = Int16.MaxValue,
    }
    public enum EnumUShort : ushort {
        Zero = 0,
        MaxSByte = (ushort)SByte.MaxValue,
        MinByte = Byte.MinValue,
        MaxByte = Byte.MaxValue,
        MaxShort = (ushort)Int16.MaxValue,
        MinUShort = UInt16.MinValue,
        MaxUShort = UInt16.MaxValue,
    }
    public enum EnumInt : int {
        Zero = 0,
        MinSByte = SByte.MinValue,
        MaxSByte = SByte.MaxValue,
        MinByte = Byte.MinValue,
        MaxByte = Byte.MaxValue,
        MinShort = Int16.MinValue,
        MaxShort = Int16.MaxValue,
        MinUShort = UInt16.MinValue,
        MaxUShort = UInt16.MaxValue,
        MinInt = Int32.MinValue,
        MaxInt = Int32.MaxValue,
    }
    public enum EnumUInt : uint {
        Zero = 0,
        MaxSByte = (uint)SByte.MaxValue,
        MinByte = Byte.MinValue,
        MaxByte = Byte.MaxValue,
        MaxShort = (uint)Int16.MaxValue,
        MinUShort = UInt16.MinValue,
        MaxUShort = UInt16.MaxValue,
        MaxInt = Int32.MaxValue,
        MinUInt = UInt32.MinValue,
        MaxUInt = UInt32.MaxValue,
    }
    public enum EnumLong : long {
        Zero = 0,
        MinSByte = SByte.MinValue,
        MaxSByte = SByte.MaxValue,
        MinByte = Byte.MinValue,
        MaxByte = Byte.MaxValue,
        MinShort = Int16.MinValue,
        MaxShort = Int16.MaxValue,
        MinUShort = UInt16.MinValue,
        MaxUShort = UInt16.MaxValue,
        MinInt = Int32.MinValue,
        MaxInt = Int32.MaxValue,
        MinUInt = UInt32.MinValue,
        MaxUInt = UInt32.MaxValue,
        MinLong = Int64.MinValue,
        MaxLong = Int64.MaxValue,
    }
    public enum EnumULong : ulong {
        Zero = 0,
        MaxSByte = (ulong)SByte.MaxValue,
        MinByte = Byte.MinValue,
        MaxByte = Byte.MaxValue,
        MaxShort = (ulong)Int16.MaxValue,
        MinUShort = UInt16.MinValue,
        MaxUShort = UInt16.MaxValue,
        MaxInt = Int32.MaxValue,
        MinUInt = UInt32.MinValue,
        MaxUInt = UInt32.MaxValue,
        MaxLong = Int64.MaxValue,
        MinULong = UInt64.MinValue,
        MaxULong = UInt64.MaxValue,
    }
}
