using IronPython.Runtime.Operations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IronPython.Runtime {
    class TypecodeOps {

        public static bool IsTypecodeFormat(string format) {
            switch (format) {
                case "c": // char
                case "b": // signed byte
                case "B": // unsigned byte
                case "x": // pad byte
                case "s": // null-terminated string
                case "p": // Pascal string
                case "u": // unicode char
                case "h": // signed short
                case "H": // unsigned short
                case "i": // signed int
                case "I": // unsigned int
                case "l": // signed long
                case "L": // unsigned long
                case "f": // float
                case "P": // pointer
                case "q": // signed long long
                case "Q": // unsigned long long
                case "d": // double
                    return true;
                default:
                    return false;
            }
        }

        public static int GetTypecodeWidth(char typecode) {
            switch (typecode) {
                case 'c': // char
                case 'b': // signed byte
                case 'B': // unsigned byte
                case 'x': // pad byte
                case 's': // null-terminated string
                case 'p': // Pascal string
                    return 1;
                case 'u': // unicode char
                case 'h': // signed short
                case 'H': // unsigned short
                    return 2;
                case 'i': // signed int
                case 'I': // unsigned int
                case 'l': // signed long
                case 'L': // unsigned long
                case 'f': // float
                    return 4;
                case 'P': // pointer
                    return IntPtr.Size;
                case 'q': // signed long long
                case 'Q': // unsigned long long
                case 'd': // double
                    return 8;
                default:
                    throw PythonOps.ValueError("Bad type code (expected one of 'c', 'b', 'B', 'u', 'H', 'h', 'i', 'I', 'l', 'L', 'f', 'd')");
            }
        }

        public static object FromBytes(char typecode, byte[] bytes, int offset) {
            switch (typecode) {
                case 'c': return (char)bytes[offset];
                case 'b': return (sbyte)bytes[offset];
                case 'B': return bytes[offset];
                case 'u': return BitConverter.ToChar(bytes, offset);
                case 'h': return BitConverter.ToInt16(bytes, offset);
                case 'H': return BitConverter.ToUInt16(bytes, offset);
                case 'l':
                case 'i': return BitConverter.ToInt32(bytes, offset);
                case 'L':
                case 'I': return BitConverter.ToUInt32(bytes, offset);
                case 'f': return BitConverter.ToSingle(bytes, offset);
                case 'd': return BitConverter.ToDouble(bytes, offset);
                default:
                    throw PythonOps.ValueError("Bad type code (expected one of 'c', 'b', 'B', 'u', 'H', 'h', 'i', 'I', 'l', 'L', 'f', 'd')");
            }
        }

        public static byte[] ToBytes(char typecode, object obj) {
            switch (typecode) {
                case 'c': return new[] { (byte)Convert.ToChar(obj) };
                case 'b': return new[] { (byte)Convert.ToSByte(obj) };
                case 'B': return new[] { Convert.ToByte(obj) };
                case 'u': return BitConverter.GetBytes((byte)Convert.ToChar(obj));
                case 'h': return BitConverter.GetBytes(Convert.ToInt16(obj));
                case 'H': return BitConverter.GetBytes(Convert.ToUInt16(obj));
                case 'l':
                case 'i': return BitConverter.GetBytes(Convert.ToInt32(obj));
                case 'L':
                case 'I': return BitConverter.GetBytes(Convert.ToUInt32(obj));
                case 'f': return BitConverter.GetBytes(Convert.ToSingle(obj));
                case 'd': return BitConverter.GetBytes(Convert.ToDouble(obj));
                default:
                    throw PythonOps.ValueError("Bad type code (expected one of 'c', 'b', 'B', 'u', 'H', 'h', 'i', 'I', 'l', 'L', 'f', 'd')");
            }
        }
    }
}
