// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.InteropServices;

namespace IronPython.Runtime {
    internal static class TypecodeOps {

        public static bool TryGetTypecodeWidth([NotNullWhen(true)]string? typecode, out int width) {
            // TODO: accept optional '@' byteorder char
            // this applies to all methods in this class
            switch (typecode) {
                case "c": // char
                case "b": // signed byte
                case "B": // unsigned byte
                    width = 1;
                    return true;
                case "u": // unicode char
                case "h": // signed short
                case "H": // unsigned short
                    width = 2;
                    return true;
                case "i": // signed int
                case "I": // unsigned int
                case "l": // signed long
                case "L": // unsigned long
                case "f": // float
                    width = 4;
                    return true;
                case "P": // pointer
                    width = IntPtr.Size;
                    return true;
                case "q": // signed long long
                case "Q": // unsigned long long
                case "d": // double
                    width = 8;
                    return true;
                default:
                    width = 0;
                    return false;
            }
        }

        public static bool TryGetFromBytes(string? typecode, ReadOnlySpan<byte> bytes, [NotNullWhen(true)]out object? result) {
            switch (typecode) {
                case "c":
                    result = (char)bytes[0];
                    return true;
                case "b":
                    result = (sbyte)bytes[0];
                    return true;
                case "B":
                    result = bytes[0];
                    return true;
                case "u":
                    result = MemoryMarshal.Read<char>(bytes);
                    return true;
                case "h":
                    result = MemoryMarshal.Read<short>(bytes);
                    return true;
                case "H":
                    result = MemoryMarshal.Read<ushort>(bytes);
                    return true;
                case "l":
                case "i":
                    result = MemoryMarshal.Read<int>(bytes);
                    return true;
                case "L":
                case "I":
                    result = MemoryMarshal.Read<uint>(bytes);
                    return true;
                case "f":
                    result = MemoryMarshal.Read<float>(bytes);
                    return true;
                case "d":
                    result = MemoryMarshal.Read<double>(bytes);
                    return true;
                case "q":
                    result = MemoryMarshal.Read<long>(bytes);
                    return true;
                case "Q":
                    result = MemoryMarshal.Read<ulong>(bytes);
                    return true;
                default:
                    result = null;
                    return false;
            }
        }

        public static bool TryGetBytes(string? typecode, object obj, Span<byte> dest) {
            switch (typecode) {
                case "c":
                    var cbyteVal = (byte)Convert.ToChar(obj);
                    return MemoryMarshal.TryWrite(dest, ref cbyteVal);
                case "b":
                    var sbyteVal = (byte)Convert.ToSByte(obj);
                    return MemoryMarshal.TryWrite(dest, ref sbyteVal);
                case "B":
                    var byteVal = Convert.ToByte(obj);
                    return MemoryMarshal.TryWrite(dest, ref byteVal);
                case "u":
                    var charVal = Convert.ToChar(obj);
                    return MemoryMarshal.TryWrite(dest, ref charVal);
                case "h":
                    var shortVal = Convert.ToInt16(obj);
                    return MemoryMarshal.TryWrite(dest, ref shortVal);
                case "H":
                    var ushortVal = Convert.ToUInt16(obj);
                    return MemoryMarshal.TryWrite(dest, ref ushortVal);
                case "l":
                case "i":
                    var intVal = Convert.ToInt32(obj);
                    return MemoryMarshal.TryWrite(dest, ref intVal);
                case "L":
                case "I":
                    var uintVal = Convert.ToUInt32(obj);
                    return MemoryMarshal.TryWrite(dest, ref uintVal);
                case "f":
                    var singleVal = Convert.ToSingle(obj);
                    return MemoryMarshal.TryWrite(dest, ref singleVal);
                case "d":
                    var doubleVal = Convert.ToDouble(obj);
                    return MemoryMarshal.TryWrite(dest, ref doubleVal);
                case "q":
                    var longVal = Convert.ToInt64(obj);
                    return MemoryMarshal.TryWrite(dest, ref longVal);
                case "Q":
                    var ulongVal = Convert.ToUInt64(obj);
                    return MemoryMarshal.TryWrite(dest, ref ulongVal);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Verifies that the value being set does not overflow given the
        /// width/sign of the current format.
        /// </summary>
        /// <param name="value">The value to be set.</param>
        public static bool CausesOverflow(object value, string format) {
            ulong maxValue;
            long minValue;

            switch (format) {
                case "c": // char
                    minValue = char.MinValue;
                    maxValue = char.MaxValue;
                    break;
                case "b": // signed byte
                    minValue = sbyte.MinValue;
                    maxValue = (ulong)sbyte.MaxValue;
                    break;
                case "B": // unsigned byte
                    minValue = byte.MinValue;
                    maxValue = byte.MaxValue;
                    break;
                case "u": // unicode char
                case "h": // signed short
                    minValue = short.MinValue;
                    maxValue = (ulong)short.MaxValue;
                    break;
                case "H": // unsigned short
                    minValue = ushort.MinValue;
                    maxValue = ushort.MaxValue;
                    break;
                case "i": // signed int
                case "l": // signed long
                    minValue = int.MinValue;
                    maxValue = int.MaxValue;
                    break;
                case "I": // unsigned int
                case "L": // unsigned long
                    minValue = uint.MinValue;
                    maxValue = uint.MaxValue;
                    break;
                case "q": // signed long long
                    minValue = long.MinValue;
                    maxValue = long.MaxValue;
                    break;
                case "P": // pointer
                case "Q": // unsigned long long
                    minValue = (long)ulong.MinValue;
                    maxValue = ulong.MaxValue;
                    break;
                default:
                    return false; // All non-numeric types will not cause overflow.
            }
            switch (value) {
                case int i:
                    return i < minValue || (maxValue < int.MaxValue && i > (int)maxValue);
                case BigInteger bi:
                    return bi < minValue || bi > maxValue;
                default:
                    BigInteger convertedValue = Converter.ConvertToBigInteger(value);
                    return convertedValue < minValue || convertedValue > maxValue;
            }
        }
    }
}
