using IronPython.Runtime.Operations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace IronPython.Runtime {
    class TypecodeOps {

        public static bool TryGetTypecodeWidth(string typecode, out int width) {
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

        public static bool TryGetFromBytes(string typecode, byte[] bytes, int offset, out object result) {
            switch (typecode) {
                case "c":
                    result = (char)bytes[offset];
                    return true;
                case "b":
                    result = (sbyte)bytes[offset];
                    return true;
                case "B":
                    result = bytes[offset];
                    return true;
                case "u":
                    result = BitConverter.ToChar(bytes, offset);
                    return true;
                case "h":
                    result = BitConverter.ToInt16(bytes, offset);
                    return true;
                case "H":
                    result = BitConverter.ToUInt16(bytes, offset);
                    return true;
                case "l":
                case "i":
                    result = BitConverter.ToInt32(bytes, offset);
                    return true;
                case "L":
                case "I":
                    result = BitConverter.ToUInt32(bytes, offset);
                    return true;
                case "f":
                    result = BitConverter.ToSingle(bytes, offset);
                    return true;
                case "d":
                    result = BitConverter.ToDouble(bytes, offset);
                    return true;
                case "q":
                    result = (BitConverter.ToInt64(bytes, offset));
                    return true;
                case "Q":
                    result = (BitConverter.ToUInt64(bytes, offset));
                    return true;
                default:
                    result = 0;
                    return false;
            }
        }

        public static bool TryGetBytes(string typecode, object obj, out byte[] result) {
            switch (typecode) {
                case "c":
                    result = new[] { (byte)Convert.ToChar(obj) };
                    return true;
                case "b":
                    result = new[] { (byte)Convert.ToSByte(obj) };
                    return true;
                case "B":
                    result = new[] { Convert.ToByte(obj) };
                    return true;
                case "u":
                    result = BitConverter.GetBytes((byte)Convert.ToChar(obj));
                    return true;
                case "h":
                    result = BitConverter.GetBytes(Convert.ToInt16(obj));
                    return true;
                case "H":
                    result = BitConverter.GetBytes(Convert.ToUInt16(obj));
                    return true;
                case "l":
                case "i":
                    result = BitConverter.GetBytes(Convert.ToInt32(obj));
                    return true;
                case "L":
                case "I":
                    result = BitConverter.GetBytes(Convert.ToUInt32(obj));
                    return true;
                case "f":
                    result = BitConverter.GetBytes(Convert.ToSingle(obj));
                    return true;
                case "d":
                    result = BitConverter.GetBytes(Convert.ToDouble(obj));
                    return true;
                case "q":
                    result = BitConverter.GetBytes(Convert.ToInt64(obj));
                    return true;
                case "Q":
                    result = BitConverter.GetBytes(Convert.ToUInt64(obj));
                    return true;
                default:
                    result = null;
                    return false;
            }
        }

        /// <summary>
        /// Verifies that the value being set does not overflow given the
        /// width/sign of the current format.
        /// </summary>
        /// <param name="value">The value to be set.</param>
        public static bool CausesOverflow(object value, string format) {
            ulong maxValue = 0;
            long minValue = 0;

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
