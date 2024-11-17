// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.InteropServices;

using IronPython.Runtime.Operations;

namespace IronPython.Runtime {
    internal static class TypecodeOps {
        // TODO: integrate with PythonStruct

        public const string ValidByteorder = "@=<>!";
        public const string ValidCodes = "xcbB?uhHiIlLqQnNfdspPrR";

        public static bool TryDecomposeTypecode(string format, out char byteorder, out char code) {
            if (format.Length == 0 || format.Length > 2) {
                byteorder = code = default;
                return false;
            }

            if (format.Length == 1) {
                byteorder = '@';
                code = format[0];
            } else {
                byteorder = format[0];
                code = format[1];
            }
            // TODO: add validation of combinations
            return ValidByteorder.IndexOf(byteorder) >= 0 && ValidCodes.IndexOf(code) >= 0;
        }

        public static void DecomposeTypecode(string format, out char byteorder, out char code) {
            if (!TryDecomposeTypecode(format, out byteorder, out code)) {
                throw PythonOps.ValueError("invalid typecode");
            }
        }

        public static bool IsByteCode(char typecode)
            => typecode == 'B' || typecode == 'b' || typecode == 'c';

        public static bool IsFloatCode(char typecode)
            => typecode == 'f' || typecode == 'd';

        public static int GetTypecodeWidth(char typecode) {
            switch (typecode) {
                case 'c': // bytechar
                case 'b': // signed byte
                case 'B': // unsigned byte
                case '?': // bool
                    return 1;
                case 'h': // signed short
                case 'H': // unsigned short
                    return 2;
                case 'i': // signed int
                case 'I': // unsigned int
                case 'l': // signed long
                case 'L': // unsigned long
                case 'f': // float
                case 'n': // signed index
                case 'N': // unsigned index
                    return 4;
                case 'q': // signed long long
                case 'Q': // unsigned long long
                case 'd': // double
                    return 8;
                case 's': // char pointer
                case 'p': // char pointer
                case 'P': // void pointer
                case 'r': // .NET signed pointer
                case 'R': // .NET unsigned pointer
                    return UIntPtr.Size;
                default:
                    throw new InvalidOperationException();
            }
        }

        public static bool TryGetFromBytes(char typecode, ReadOnlySpan<byte> bytes, [NotNullWhen(true)] out object? result) {
            switch (typecode) {
                case 'c':
                    result = Bytes.FromByte(bytes[0]);
                    return true;
                case 'b':
                    result = (sbyte)bytes[0];
                    return true;
                case 'B':
                    result = bytes[0];
                    return true;
                case '?':
                    result = bytes[0] != 0;
                    return true;
                case 'h':
                    result = MemoryMarshal.Read<short>(bytes);
                    return true;
                case 'H':
                    result = MemoryMarshal.Read<ushort>(bytes);
                    return true;
                case 'l':
                case 'i':
                case 'n':
                    result = MemoryMarshal.Read<int>(bytes);
                    return true;
                case 'L':
                case 'I':
                case 'N':
                    result = MemoryMarshal.Read<uint>(bytes);
                    return true;
                case 'f':
                    result = MemoryMarshal.Read<float>(bytes);
                    return true;
                case 'd':
                    result = MemoryMarshal.Read<double>(bytes);
                    return true;
                case 'q':
                    result = MemoryMarshal.Read<long>(bytes);
                    return true;
                case 'Q':
                    result = MemoryMarshal.Read<ulong>(bytes);
                    return true;
                case 'P':
                    if (UIntPtr.Size == 4) goto case 'L';
                    else goto case 'Q';
                case 'r':
                    result = MemoryMarshal.Read<IntPtr>(bytes);
                    return true;
                case 'R':
                    result = MemoryMarshal.Read<UIntPtr>(bytes);
                    return true;
                default:
                    result = null;
                    return false;
            }
        }

#pragma warning disable CS9191 // The 'ref' modifier for an argument corresponding to 'in' parameter is equivalent to 'in'. Consider using 'in' instead.
        public static bool TryGetBytes(char typecode, object obj, Span<byte> dest) {
            switch (typecode) {
                case 'c':
                    var bytecharVal = (byte)((Bytes)obj)[0];
                    return MemoryMarshal.TryWrite(dest, ref bytecharVal);
                case 'b':
                    var sbyteVal = (byte)Convert.ToSByte(obj);
                    return MemoryMarshal.TryWrite(dest, ref sbyteVal);
                case 'B':
                    var byteVal = Convert.ToByte(obj);
                    return MemoryMarshal.TryWrite(dest, ref byteVal);
                case '?':
                    var boolVal = PythonOps.IsTrue(obj);
                    return MemoryMarshal.TryWrite(dest, ref boolVal);
                case 'h':
                    var shortVal = Convert.ToInt16(obj);
                    return MemoryMarshal.TryWrite(dest, ref shortVal);
                case 'H':
                    var ushortVal = Convert.ToUInt16(obj);
                    return MemoryMarshal.TryWrite(dest, ref ushortVal);
                case 'l':
                case 'i':
                case 'n':
                    var intVal = Convert.ToInt32(obj);
                    return MemoryMarshal.TryWrite(dest, ref intVal);
                case 'L':
                case 'I':
                case 'N':
                    var uintVal = Convert.ToUInt32(obj);
                    return MemoryMarshal.TryWrite(dest, ref uintVal);
                case 'f':
                    var singleVal = Convert.ToSingle(obj);
                    return MemoryMarshal.TryWrite(dest, ref singleVal);
                case 'd':
                    var doubleVal = Convert.ToDouble(obj);
                    return MemoryMarshal.TryWrite(dest, ref doubleVal);
                case 'q':
                    var longVal = Convert.ToInt64(obj);
                    return MemoryMarshal.TryWrite(dest, ref longVal);
                case 'Q':
                    var ulongVal = Convert.ToUInt64(obj);
                    return MemoryMarshal.TryWrite(dest, ref ulongVal);
                case 'P':
                    var bi = (BigInteger)obj;
                    if (UIntPtr.Size == 4) {
                        if (bi < 0) {
                            bi += new BigInteger(UInt32.MaxValue) + 1;
                        }
                        var ptrVal = (uint)bi;
                        return MemoryMarshal.TryWrite(dest, ref ptrVal);
                    } else {
                        if (bi < 0) {
                            bi += new BigInteger(UInt64.MaxValue) + 1;
                        }
                        var ptrVal = (ulong)bi;
                        return MemoryMarshal.TryWrite(dest, ref ptrVal);
                    }
                case 'r':
                    var iptrVal = (IntPtr)obj;
                    return MemoryMarshal.TryWrite(dest, ref iptrVal);
                case 'R':
                    var uptrVal = (UIntPtr)obj;
                    return MemoryMarshal.TryWrite(dest, ref uptrVal);
                default:
                    return false;
            }
        }
#pragma warning restore CS9191 // The 'ref' modifier for an argument corresponding to 'in' parameter is equivalent to 'in'. Consider using 'in' instead.

        /// <summary>
        /// Checks if the given value does overflow a single element field with the
        /// width/sign of the field as specified by the given format.
        /// </summary>
        /// <param name="value">The value to be checked.</param>
        /// <param name="typecode">Valid struct-style single element typecode.</param>
        public static bool CausesOverflow(object value, char typecode) {
            ulong maxValue;
            long minValue;

            switch (typecode) {
                case 'b': // signed byte
                    minValue = sbyte.MinValue;
                    maxValue = (ulong)sbyte.MaxValue;
                    break;
                case 'B': // unsigned byte
                    minValue = byte.MinValue;
                    maxValue = byte.MaxValue;
                    break;
                case '?': // bool
                    return false; // bool never causes overflow but is coerced to 0/1
                case 'h': // signed short
                    minValue = short.MinValue;
                    maxValue = (ulong)short.MaxValue;
                    break;
                case 'H': // unsigned short
                    minValue = ushort.MinValue;
                    maxValue = ushort.MaxValue;
                    break;
                case 'i': // signed int
                case 'l': // signed long
                case 'n': // signed index
                    minValue = int.MinValue;
                    maxValue = int.MaxValue;
                    break;
                case 'I': // unsigned int
                case 'L': // unsigned long
                case 'N': // unsigned index
                    minValue = uint.MinValue;
                    maxValue = uint.MaxValue;
                    break;
                case 'q': // signed long long
                    minValue = long.MinValue;
                    maxValue = long.MaxValue;
                    break;
                case 'Q': // unsigned long long
                    minValue = (long)ulong.MinValue;
                    maxValue = ulong.MaxValue;
                    break;
                case 'P': // pointer
                    minValue = UIntPtr.Size == 4 ? Int32.MinValue : Int64.MinValue;
                    maxValue = UIntPtr.Size == 4 ? UInt32.MaxValue : UInt64.MaxValue;
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
