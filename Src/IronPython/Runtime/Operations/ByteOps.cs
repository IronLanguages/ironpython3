// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime.Operations {
    public static partial class ByteOps {
        internal static byte ToByteChecked(this int item) {
            try {
                return checked((byte)item);
            } catch (OverflowException) {
                throw PythonOps.ValueError("byte must be in range(0, 256)");
            }
        }

        internal static byte ToByteChecked(this BigInteger item) {
            int val;
            if (item.AsInt32(out val)) {
                return ToByteChecked(val);
            }
            throw PythonOps.ValueError("byte must be in range(0, 256)");
        }

        internal static byte ToByteChecked(this double item) {
            try {
                return checked((byte)item);
            } catch (OverflowException) {
                throw PythonOps.ValueError("byte must be in range(0, 256)");
            }
        }

        internal static bool IsSign(this byte ch) {
            return ch == '+' || ch == '-';
        }

        internal static byte ToUpper(this byte p) {
            if (p >= 'a' && p <= 'z') {
                p -= ('a' - 'A');
            }
            return p;
        }

        internal static byte ToLower(this byte p) {
            if (p >= 'A' && p <= 'Z') {
                p += ('a' - 'A');
            }
            return p;
        }

        internal static bool IsLower(this byte p) {
            return p >= 'a' && p <= 'z';
        }

        internal static bool IsUpper(this byte p) {
            return p >= 'A' && p <= 'Z';
        }

        internal static bool IsDigit(this byte b) {
            return b >= '0' && b <= '9';
        }

        internal static bool IsLetter(this byte b) {
            return IsLower(b) || IsUpper(b);
        }

        internal static bool IsWhiteSpace(this byte b) {
            return b == ' ' ||
                    b == '\t' ||
                    b == '\n' ||
                    b == '\r' ||
                    b == '\f' ||
                    b == 11;
        }

        internal static void AppendJoin(object value, int index, List<byte> byteList) {
            if (value is IList<byte> bytesValue) {
                byteList.AddRange(bytesValue);
            } else {
                throw PythonOps.TypeError("sequence item {0}: expected bytes or byte array, {1} found", index.ToString(), PythonOps.GetPythonTypeName(value));
            }
        }

        internal static IList<byte> CoerceBytes(object obj) {
            if (!(obj is IList<byte> ret)) {
                throw PythonOps.TypeError("expected string, got {0} Type", PythonTypeOps.GetName(obj));
            }
            return ret;
        }

        internal static List<byte> GetBytes(ICollection bytes) {
            return GetBytes(bytes, GetByte);
        }

        internal static List<byte> GetBytes(ICollection bytes, Func<object, byte> conversion) {
            List<byte> res = new List<byte>(bytes.Count);
            foreach (object o in bytes) {
                res.Add(conversion.Invoke(o));
            }
            return res;
        }

        internal static byte GetByteStringOk(object o) {
            string s;
            Extensible<string> es;
            if (!Object.ReferenceEquals(s = o as string, null)) {
                if (s.Length == 1) {
                    return ((int)s[0]).ToByteChecked();
                } else {
                    throw PythonOps.TypeError("an integer or string of size 1 is required");
                }
            } else if (!Object.ReferenceEquals(es = o as Extensible<string>, null)) {
                if (es.Value.Length == 1) {
                    return ((int)es.Value[0]).ToByteChecked();
                } else {
                    throw PythonOps.TypeError("an integer or string of size 1 is required");
                }
            } else {
                return GetByteListOk(o);
            }
        }

        internal static byte GetByteListOk(object o) {
            if (o is IList<byte> lbval) {
                if (lbval.Count == 1) {
                    return lbval[0];
                }
                throw PythonOps.ValueError("an integer or string of size 1 is required");
            }

            return GetByte(o);
        }

        internal static byte GetByte(object o) {
            // TODO: verify usage and clean up
            Extensible<int> ei;
            Extensible<BigInteger> ebi;
            Extensible<double> ed;
            int i;
            if (o is int) {
                return ((int)o).ToByteChecked();
            } else if (o is BigInteger) {
                return ((BigInteger)o).ToByteChecked();
            } else if (o is double) {
                return ((double)o).ToByteChecked();
            } else if ((ei = o as Extensible<int>) != null) {
                return ei.Value.ToByteChecked();
            } else if (!Object.ReferenceEquals(ebi = o as Extensible<BigInteger>, null)) {
                return ebi.Value.ToByteChecked();
            } else if (!Object.ReferenceEquals(ed = o as Extensible<double>, null)) {
                return ed.Value.ToByteChecked();
            } else if (o is byte) {
                return (byte)o;
            } else if (o is sbyte) {
                return ((int)(sbyte)o).ToByteChecked();
            } else if (o is char) {
                return ((int)(char)o).ToByteChecked();
            } else if (o is short) {
                return ((int)(short)o).ToByteChecked();
            } else if (o is ushort) {
                return ((int)(ushort)o).ToByteChecked();
            } else if (o is uint) {
                return ((BigInteger)(uint)o).ToByteChecked();
            } else if (o is float) {
                return ((double)(float)o).ToByteChecked();
            } else if (Converter.TryConvertToIndex(o, out i)) {
                return i.ToByteChecked();
            } else if (o is string str && str.Length == 1) {
                return ((int)str[0]).ToByteChecked();
            } else {
                throw PythonOps.TypeError("an integer or string of size 1 is required");
            }
        }
    }
}
