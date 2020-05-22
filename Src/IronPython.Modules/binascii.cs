// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

[assembly: PythonModule("binascii", typeof(IronPython.Modules.PythonBinaryAscii))]
namespace IronPython.Modules {
    public static class PythonBinaryAscii {
        public const string __doc__ = "Provides functions for converting between binary data encoded in various formats and ASCII.";

        private static readonly object _ErrorKey = new object();
        private static readonly object _IncompleteKey = new object();

        private static Exception Error(CodeContext/*!*/ context, params object[] args) {
            return PythonExceptions.CreateThrowable((PythonType)context.LanguageContext.GetModuleState(_ErrorKey), args);
        }
        private static Exception Incomplete(CodeContext/*!*/ context, params object[] args) {
            return PythonExceptions.CreateThrowable((PythonType)context.LanguageContext.GetModuleState(_IncompleteKey), args);
        }

        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, PythonDictionary/*!*/ dict) {
            context.EnsureModuleException(_ErrorKey, PythonExceptions.ValueError, dict, "Error", "binascii");
            context.EnsureModuleException(_IncompleteKey, dict, "Incomplete", "binascii");
        }

        #region uu

        public static Bytes a2b_uu(CodeContext/*!*/ context, [NotNull]IBufferProtocol data)
            => a2b_uu_impl(context, data.tobytes());

        public static Bytes a2b_uu(CodeContext/*!*/ context, [NotNull]MemoryView data)
            => a2b_uu_impl(context, data.tobytes());

        public static Bytes a2b_uu(CodeContext/*!*/ context, [NotNull]string data)
            => a2b_uu_impl(context, data.tobytes());

        private static Bytes a2b_uu_impl(CodeContext/*!*/ context, IList<byte> data) {
            if (data.Count < 1) return Bytes.Make(new byte[32]);

            int lenDec = (data[0] + 32) % 64; // decoded length in bytes
            int lenEnc = (lenDec * 4 + 2) / 3; // encoded length in 6-bit chunks
            List<byte> suffix = null;
            if (data.Count - 1 > lenEnc) {
                suffix = data.Substring(1 + lenEnc);
                data = data.Substring(1, lenEnc);
            } else {
                data = data.Substring(1);
            }

            using MemoryStream res = DecodeWorker(context, data, true, UuDecFunc);
            if (suffix == null) {
                var pad = new byte[lenDec - res.Length];
                res.Write(pad, 0, pad.Length);
            } else {
                ProcessSuffix(context, suffix, UuDecFunc);
            }

            return Bytes.Make(res.ToArray());

            static int UuDecFunc(byte val) {
                if (val > 32 && val < 96) return val - 32;
                switch (val) {
                    case (byte)'\n':
                    case (byte)'\r':
                    case 32:
                    case 96:
                        return EmptyByte;
                    default:
                        return InvalidByte;
                }
            }
        }

        public static Bytes b2a_uu(CodeContext/*!*/ context, [NotNull]IBufferProtocol data)
            => b2a_uu_impl(context, data.tobytes());

        public static Bytes b2a_uu(CodeContext/*!*/ context, [NotNull]MemoryView data)
            => b2a_uu_impl(context, data.tobytes());

        private static Bytes b2a_uu_impl(CodeContext/*!*/ context, IList<byte> data) {
            if (data.Count > 45) throw Error(context, "At most 45 bytes at once");

            using var res = new MemoryStream();
            res.WriteByte((byte)(32 + data.Count));
            res.EncodeData(data, (byte)' ', (int val) => (byte)(32 + val % 64));
            res.WriteByte((byte)'\n');
            return Bytes.Make(res.ToArray());
        }

        #endregion

        #region base64

        public static Bytes a2b_base64(CodeContext/*!*/ context, [NotNull]IBufferProtocol data)
            => a2b_base64_impl(context, data.tobytes());

        public static Bytes a2b_base64(CodeContext/*!*/ context, [NotNull]MemoryView data)
            => a2b_base64_impl(context, data.tobytes());

        public static Bytes a2b_base64(CodeContext/*!*/ context, [NotNull]string data)
            => a2b_base64_impl(context, data.tobytes());

        private static Bytes a2b_base64_impl(CodeContext/*!*/ context, IList<byte> data) {
            data = RemovePrefix(context, data, Base64DecFunc);
            if (data.Count == 0) return Bytes.Empty;
            using MemoryStream res = DecodeWorker(context, data, false, Base64DecFunc);
            return Bytes.Make(res.ToArray());

            static int Base64DecFunc(byte val) {
                if (val >= 'A' && val <= 'Z') return val - 'A';
                if (val >= 'a' && val <= 'z') return val - 'a' + 26;
                if (val >= '0' && val <= '9') return val - '0' + 52;
                switch (val) {
                    case (byte)'+':
                        return 62;
                    case (byte)'/':
                        return 63;
                    case (byte)'=':
                        return PadByte;
                    default:
                        return IgnoreByte;
                }
            }
        }

        public static Bytes b2a_base64([NotNull]IBufferProtocol data)
            => b2a_base64_impl(data.tobytes());

        public static Bytes b2a_base64([NotNull]MemoryView data)
            => b2a_base64_impl(data.tobytes());

        private static Bytes b2a_base64_impl(IList<byte> data) {
            if (data.Count == 0) return Bytes.Empty;
            using var res = new MemoryStream();
            res.EncodeData(data, (byte)'=', EncodeValue);
            res.WriteByte((byte)'\n');
            return Bytes.Make(res.ToArray());

            static byte EncodeValue(int val) {
                if (val < 26) return (byte)('A' + val);
                if (val < 52) return (byte)('a' + val - 26);
                if (val < 62) return (byte)('0' + val - 52);
                switch (val) {
                    case 62:
                        return (byte)'+';
                    case 63:
                        return (byte)'/';
                    default:
                        throw new InvalidOperationException(string.Format("Bad int val: {0}", val));
                }
            }
        }

        #endregion

        #region qp

        private const int MAXLINESIZE = 76;

        public static object a2b_qp(object data) {
            throw new NotImplementedException();
        }

        [LightThrowing]
        public static object a2b_qp(object data, object header) {
            return LightExceptions.Throw(new NotImplementedException());
        }

        [Documentation(@"b2a_qp(data, quotetabs=0, istext=1, header=0) -> s; 
 Encode a string using quoted-printable encoding. 

On encoding, when istext is set, newlines are not encoded, and white 
space at end of lines is.  When istext is not set, \\r and \\n (CR/LF) are 
both encoded.  When quotetabs is set, space and tabs are encoded.")]
        public static object b2a_qp(string data, int quotetabs = 0, int istext = 1, int header = 0) {
            bool crlf = data.Contains("\r\n");
            int linelen = 0, odatalen = 0;
            int incount = 0, outcount = 0;

            bool quotetabs_ = quotetabs != 0;
            bool header_ = header != 0;
            bool istext_ = istext != 0;

            while(incount < data.Length) {
                if ((data[incount] > 126) || (data[incount] == '=') ||
                    (header_ && data[incount] == '_') || ((data[incount] == '.') && (linelen == 0) &&
                    (data[incount+1] == '\n' || data[incount+1] == '\r' || data[incount+1] == 0)) ||
                    (!istext_ && ((data[incount] == '\r') || (data[incount] == '\n'))) ||
                    ((data[incount] == '\t' || data[incount] == ' ') && (incount +1 == data.Length)) ||
                    ((data[incount] < 33) && (data[incount] != '\r') && (data[incount] != '\n') &&
                    (quotetabs_ || (!quotetabs_ && ((data[incount] != '\t') && (data[incount] != ' ')))))) {

                    if ((linelen + 3) >= MAXLINESIZE) {
                        linelen = 0;
                        if (crlf)
                            odatalen += 3;
                        else
                            odatalen += 2;
                    }
                    linelen += 3;
                    odatalen += 3;
                    incount++;
                } else {
                    if (istext_ &&
                        ((data[incount] == '\n') ||
                         ((incount + 1 < data.Length) && (data[incount] == '\r') &&
                         (data[incount + 1] == '\n')))) {
                        linelen = 0;
                        /* Protect against whitespace on end of line */
                        if (incount > 0 && ((data[incount - 1] == ' ') || (data[incount - 1] == '\t')))
                            odatalen += 2;
                        if (crlf)
                            odatalen += 2;
                        else
                            odatalen += 1;
                        if (data[incount] == '\r')
                            incount += 2;
                        else
                            incount++;
                    } else {
                        if ((incount + 1 != data.Length) &&
                           (data[incount + 1] != '\n') &&
                           (linelen + 1) >= MAXLINESIZE) {
                            linelen = 0;
                            if (crlf)
                                odatalen += 3;
                            else
                                odatalen += 2;
                        }
                        linelen++;
                        odatalen++;
                        incount++;
                    }
                }
            }

            StringBuilder odata = new StringBuilder();

            incount = outcount = linelen = 0;
            while (incount < data.Length) {
                if ((data[incount] > 126) || (data[incount] == '=') || (header_ && data[incount] == '_') ||
                    ((data[incount] == '.') && (linelen == 0) && (data[incount + 1] == '\n' || data[incount + 1] == '\r' || data[incount + 1] == 0)) ||
                    (!istext_ && ((data[incount] == '\r') || (data[incount] == '\n'))) ||
                    ((data[incount] == '\t' || data[incount] == ' ') && (incount + 1 == data.Length)) ||
                   ((data[incount] < 33) && (data[incount] != '\r') && (data[incount] != '\n') &&
                    (quotetabs_ || (!quotetabs_ && ((data[incount] != '\t') && (data[incount] != ' ')))))) {

                    if ((linelen + 3) >= MAXLINESIZE) {
                        odata.Append('=');
                        if (crlf) odata.Append('\r');
                        odata.Append('\n');
                        linelen = 0;
                    }
                    odata.Append('=');
                    to_hex(data[incount], odata, outcount);
                    outcount += 2;
                    incount++;
                    linelen += 3;
                } else {
                    if (istext_ &&
                        ((data[incount] == '\n') ||
                         ((incount + 1 < data.Length) && (data[incount] == '\r') &&
                         (data[incount + 1] == '\n')))) {
                        linelen = 0;
                        /* Protect against whitespace on end of line */
                        if (outcount != 0 && ((odata[outcount - 1] == ' ') || (odata[outcount - 1] == '\t'))) {
                            char ch = odata[outcount - 1];
                            odata[outcount - 1] = '=';
                            to_hex(ch, odata, outcount);
                            outcount += 2;
                        }

                        if (crlf) odata[outcount++] = '\r';
                        odata[outcount++] = '\n';
                        if (data[incount] == '\r')
                            incount += 2;
                        else
                            incount++;
                    } else {
                        if ((incount +1 != data.Length) &&
                           (data[incount + 1] != '\n') &&
                           (linelen + 1) >= MAXLINESIZE) {
                            odata[outcount++] = '=';
                            if (crlf) odata[outcount++] = '\r';
                            odata[outcount++] = '\n';
                            linelen = 0;
                        }
                        linelen++;
                        if (header_ && data[incount] == ' ') {
                            odata[outcount++] = '_';
                            incount++;
                        } else {
                            odata[outcount++] = data[incount++];
                        }
                    }
                }
            }

            return odata.ToString();

            static void to_hex(char ch, StringBuilder s, int index) {
                int uvalue = ch, uvalue2 = ch / 16;
                s.Append("0123456789ABCDEF"[uvalue2 % 16]);
                s.Append("0123456789ABCDEF"[uvalue % 16]);
            }
        }

        #endregion

        #region hqx

        public static object a2b_hqx(object data) {
            throw new NotImplementedException();
        }
        public static object rledecode_hqx(object data) {
            throw new NotImplementedException();
        }
        public static object rlecode_hqx(object data) {
            throw new NotImplementedException();
        }
        public static object b2a_hqx(object data) {
            throw new NotImplementedException();
        }

        private static readonly ushort[] crctab_hqx = new ushort[] {
            0x0000, 0x1021, 0x2042, 0x3063, 0x4084, 0x50a5, 0x60c6, 0x70e7,
            0x8108, 0x9129, 0xa14a, 0xb16b, 0xc18c, 0xd1ad, 0xe1ce, 0xf1ef,
            0x1231, 0x0210, 0x3273, 0x2252, 0x52b5, 0x4294, 0x72f7, 0x62d6,
            0x9339, 0x8318, 0xb37b, 0xa35a, 0xd3bd, 0xc39c, 0xf3ff, 0xe3de,
            0x2462, 0x3443, 0x0420, 0x1401, 0x64e6, 0x74c7, 0x44a4, 0x5485,
            0xa56a, 0xb54b, 0x8528, 0x9509, 0xe5ee, 0xf5cf, 0xc5ac, 0xd58d,
            0x3653, 0x2672, 0x1611, 0x0630, 0x76d7, 0x66f6, 0x5695, 0x46b4,
            0xb75b, 0xa77a, 0x9719, 0x8738, 0xf7df, 0xe7fe, 0xd79d, 0xc7bc,
            0x48c4, 0x58e5, 0x6886, 0x78a7, 0x0840, 0x1861, 0x2802, 0x3823,
            0xc9cc, 0xd9ed, 0xe98e, 0xf9af, 0x8948, 0x9969, 0xa90a, 0xb92b,
            0x5af5, 0x4ad4, 0x7ab7, 0x6a96, 0x1a71, 0x0a50, 0x3a33, 0x2a12,
            0xdbfd, 0xcbdc, 0xfbbf, 0xeb9e, 0x9b79, 0x8b58, 0xbb3b, 0xab1a,
            0x6ca6, 0x7c87, 0x4ce4, 0x5cc5, 0x2c22, 0x3c03, 0x0c60, 0x1c41,
            0xedae, 0xfd8f, 0xcdec, 0xddcd, 0xad2a, 0xbd0b, 0x8d68, 0x9d49,
            0x7e97, 0x6eb6, 0x5ed5, 0x4ef4, 0x3e13, 0x2e32, 0x1e51, 0x0e70,
            0xff9f, 0xefbe, 0xdfdd, 0xcffc, 0xbf1b, 0xaf3a, 0x9f59, 0x8f78,
            0x9188, 0x81a9, 0xb1ca, 0xa1eb, 0xd10c, 0xc12d, 0xf14e, 0xe16f,
            0x1080, 0x00a1, 0x30c2, 0x20e3, 0x5004, 0x4025, 0x7046, 0x6067,
            0x83b9, 0x9398, 0xa3fb, 0xb3da, 0xc33d, 0xd31c, 0xe37f, 0xf35e,
            0x02b1, 0x1290, 0x22f3, 0x32d2, 0x4235, 0x5214, 0x6277, 0x7256,
            0xb5ea, 0xa5cb, 0x95a8, 0x8589, 0xf56e, 0xe54f, 0xd52c, 0xc50d,
            0x34e2, 0x24c3, 0x14a0, 0x0481, 0x7466, 0x6447, 0x5424, 0x4405,
            0xa7db, 0xb7fa, 0x8799, 0x97b8, 0xe75f, 0xf77e, 0xc71d, 0xd73c,
            0x26d3, 0x36f2, 0x0691, 0x16b0, 0x6657, 0x7676, 0x4615, 0x5634,
            0xd94c, 0xc96d, 0xf90e, 0xe92f, 0x99c8, 0x89e9, 0xb98a, 0xa9ab,
            0x5844, 0x4865, 0x7806, 0x6827, 0x18c0, 0x08e1, 0x3882, 0x28a3,
            0xcb7d, 0xdb5c, 0xeb3f, 0xfb1e, 0x8bf9, 0x9bd8, 0xabbb, 0xbb9a,
            0x4a75, 0x5a54, 0x6a37, 0x7a16, 0x0af1, 0x1ad0, 0x2ab3, 0x3a92,
            0xfd2e, 0xed0f, 0xdd6c, 0xcd4d, 0xbdaa, 0xad8b, 0x9de8, 0x8dc9,
            0x7c26, 0x6c07, 0x5c64, 0x4c45, 0x3ca2, 0x2c83, 0x1ce0, 0x0cc1,
            0xef1f, 0xff3e, 0xcf5d, 0xdf7c, 0xaf9b, 0xbfba, 0x8fd9, 0x9ff8,
            0x6e17, 0x7e36, 0x4e55, 0x5e74, 0x2e93, 0x3eb2, 0x0ed1, 0x1ef0,
        };

        public static int crc_hqx([NotNull]IBufferProtocol data, int crc)
            => crc_hqx_impl(data.tobytes(), crc);

        public static int crc_hqx([NotNull]MemoryView data, int crc)
            => crc_hqx_impl(data.tobytes(), crc);

        private static int crc_hqx_impl(IList<byte> data, int crc) {
            crc &= 0xffff;
            foreach (var b in data) {
                crc = ((crc << 8) & 0xff00) ^ crctab_hqx[(crc >> 8) ^ b];
            }
            return crc;
        }

        #endregion

        #region crc32

        [Documentation("crc32(string[, value]) -> string\n\nComputes a CRC (Cyclic Redundancy Check) checksum of string.")]
        public static object crc32([NotNull]IBufferProtocol data, uint crc = 0)
            => crc32_impl(data.tobytes(), crc);

        public static object crc32([NotNull]MemoryView data, uint crc = 0)
            => crc32_impl(data.tobytes(), crc);

        private static object crc32_impl(IList<byte> bytes, uint crc) {
            var res = crc32(bytes, 0, bytes.Count, crc);
            if (res <= int.MaxValue) return (int)res;
            return (BigInteger)res;
        }

        internal static uint crc32(IList<byte> buffer, int offset, int count, uint baseValue) {
            uint remainder = (baseValue ^ 0xffffffff);
            for (int i = offset; i < offset + count; i++) {
                remainder ^= buffer[i];
                for (int j = 0; j < 8; j++) {
                    if ((remainder & 0x01) != 0) {
                        remainder = (remainder >> 1) ^ 0xEDB88320;
                    } else {
                        remainder = (remainder >> 1);
                    }
                }
            }
            return (remainder ^ 0xffffffff);
        }

        #endregion

        #region hex

        public static Bytes b2a_hex([NotNull]IBufferProtocol data)
            => b2a_hex_impl(data.tobytes());

        public static Bytes b2a_hex([NotNull]MemoryView data)
            => b2a_hex_impl(data.tobytes());

        private static Bytes b2a_hex_impl(IList<byte> data) {
            byte[] res = new byte[data.Count * 2];
            for (var i = 0; i < data.Count; i++) {
                var b = data[i];
                res[2 * i] = ToAscii(b >> 4);
                res[2 * i + 1] = ToAscii(b & 0xf);
            }
            return Bytes.Make(res);

            static byte ToAscii(int b) {
                return (byte)(b < 10 ? '0' + b : 'a' + (b - 10));
            }
        }

        public static Bytes hexlify([NotNull]IBufferProtocol data)
            => b2a_hex_impl(data.tobytes());

        public static Bytes hexlify([NotNull]MemoryView data)
            => b2a_hex_impl(data.tobytes());

        public static Bytes a2b_hex(CodeContext/*!*/ context, [NotNull]IBufferProtocol data)
            => a2b_hex_impl(context, data.tobytes());

        public static Bytes a2b_hex(CodeContext/*!*/ context, [NotNull]MemoryView data)
            => a2b_hex_impl(context, data.tobytes());

        public static Bytes a2b_hex(CodeContext/*!*/ context, [NotNull]string data)
            => a2b_hex_impl(context, data.tobytes());

        private static Bytes a2b_hex_impl(CodeContext/*!*/ context, IList<byte> data) {
            if ((data.Count & 0x01) != 0) throw Error(context, "Odd-length string");

            byte[] res = new byte[data.Count / 2];
            for (int i = 0; i < res.Length; i++) {
                var b1 = ParseHex(context, data[2 * i]);
                var b2 = ParseHex(context, data[2 * i + 1]);
                res[i] = (byte)(b1 * 16 + b2);
            }
            return Bytes.Make(res);

            static byte ParseHex(CodeContext/*!*/ context, byte b) {
                if (b.IsDigit()) {
                    return (byte)(b - '0');
                } else if (b >= 'A' && b <= 'F') {
                    return (byte)(b - 'A' + 10);
                } else if (b >= 'a' && b <= 'f') {
                    return (byte)(b - 'a' + 10);
                } else {
                    throw Error(context, "Non-hexadecimal digit found");
                }
            }
        }

        public static Bytes unhexlify(CodeContext/*!*/ context, [NotNull]IBufferProtocol hexstr)
            => a2b_hex(context, hexstr);

        public static Bytes unhexlify(CodeContext/*!*/ context, [NotNull]MemoryView hexstr)
            => a2b_hex(context, hexstr);

        public static Bytes unhexlify(CodeContext/*!*/ context, [NotNull]string hexstr)
            => a2b_hex(context, hexstr);

        #endregion

        #region Private implementation

        private delegate int DecodeByte(byte val);

        private static void EncodeData(this MemoryStream res, IList<byte> data, byte empty, Func<int, byte> encFunc) {
            int bits;
            for (int i = 0; i < data.Count; i += 3) {
                switch (data.Count - i) {
                    case 1:
                        // only one char, emit 2 bytes &
                        // padding
                        bits = (data[i] & 0xff) << 16;
                        res.WriteByte(encFunc((bits >> 18) & 0x3f));
                        res.WriteByte(encFunc((bits >> 12) & 0x3f));
                        res.WriteByte(empty);
                        res.WriteByte(empty);
                        break;
                    case 2:
                        // only two chars, emit 3 bytes &
                        // padding
                        bits = ((data[i] & 0xff) << 16) | ((data[i + 1] & 0xff) << 8);
                        res.WriteByte(encFunc((bits >> 18) & 0x3f));
                        res.WriteByte(encFunc((bits >> 12) & 0x3f));
                        res.WriteByte(encFunc((bits >> 6) & 0x3f));
                        res.WriteByte(empty);
                        break;
                    default:
                        // got all 3 bytes, just emit it.
                        bits = ((data[i] & 0xff) << 16) |
                            ((data[i + 1] & 0xff) << 8) |
                            ((data[i + 2] & 0xff));
                        res.WriteByte(encFunc((bits >> 18) & 0x3f));
                        res.WriteByte(encFunc((bits >> 12) & 0x3f));
                        res.WriteByte(encFunc((bits >> 6) & 0x3f));
                        res.WriteByte(encFunc(bits & 0x3f));
                        break;
                }
            }
        }

        private const int IgnoreByte = -1; // skip this byte
        private const int EmptyByte = -2; // byte evaluates to 0 and may appear off the end of the stream
        private const int PadByte = -3; // pad bytes signal the end of the stream, unless there are too few to properly align
        private const int InvalidByte = -4; // raise exception for illegal byte
        private const int NoMoreBytes = -5; // signals end of stream

        private static int NextVal(CodeContext/*!*/ context, IList<byte> data, ref int index, DecodeByte decFunc) {
            int res;
            while (index < data.Count) {
                res = decFunc(data[index++]);
                switch (res) {
                    case EmptyByte:
                        return 0;
                    case InvalidByte:
                        throw Error(context, "Illegal char");
                    case IgnoreByte:
                        break;
                    default:
                        return res;
                }
            }

            return NoMoreBytes;
        }

        private static int CountPadBytes(CodeContext/*!*/ context, IList<byte> data, int bound, ref int index, DecodeByte decFunc) {
            int res = PadByte;
            int count = 0;
            while ((bound < 0 || count < bound) &&
                   (res = NextVal(context, data, ref index, decFunc)) == PadByte) {
                count++;
            }

            // we only want NextVal() to eat PadBytes - not real data
            if (res != PadByte && res != NoMoreBytes) index--;

            return count;
        }

        private static int GetVal(CodeContext/*!*/ context, IList<byte> data, int align, bool bounded, ref int index, DecodeByte decFunc) {
            int res;
            while (true) {
                res = NextVal(context, data, ref index, decFunc);
                switch (res) {
                    case PadByte:
                        switch (align) {
                            case 0:
                            case 1:
                                CountPadBytes(context, data, -1, ref index, decFunc);
                                continue;
                            case 2:
                                if (CountPadBytes(context, data, 1, ref index, decFunc) > 0) {
                                    return NoMoreBytes;
                                } else {
                                    continue;
                                }
                            default:
                                return NoMoreBytes;
                        }
                    case NoMoreBytes:
                        if (bounded || align == 0) {
                            return NoMoreBytes;
                        } else {
                            throw Error(context, "Incorrect padding");
                        }
                    case EmptyByte:
                        return 0;
                    default:
                        return res;
                }
            }
        }

        private static MemoryStream DecodeWorker(CodeContext/*!*/ context, IList<byte> data, bool bounded, DecodeByte decFunc) {
            var res = new MemoryStream();

            int i = 0;
            while (i < data.Count) {
                int intVal;

                int val0 = GetVal(context, data, 0, bounded, ref i, decFunc);
                if (val0 < 0) break;  // no more bytes...

                int val1 = GetVal(context, data, 1, bounded, ref i, decFunc);
                if (val1 < 0) break;  // no more bytes...

                int val2 = GetVal(context, data, 2, bounded, ref i, decFunc);
                if (val2 < 0) {
                    // 2 byte partial
                    intVal = (val0 << 18) | (val1 << 12);

                    res.WriteByte((byte)((intVal >> 16) & 0xff));
                    break;
                }

                int val3 = GetVal(context, data, 3, bounded, ref i, decFunc);
                if (val3 < 0) {
                    // 3 byte partial
                    intVal = (val0 << 18) | (val1 << 12) | (val2 << 6);

                    res.WriteByte((byte)((intVal >> 16) & 0xff));
                    res.WriteByte((byte)((intVal >> 8) & 0xff));
                    break;
                }

                // full 4-bytes
                intVal = (val0 << 18) | (val1 << 12) | (val2 << 6) | (val3);
                res.WriteByte((byte)((intVal >> 16) & 0xff));
                res.WriteByte((byte)((intVal >> 8) & 0xff));
                res.WriteByte((byte)(intVal & 0xff));
            }

            return res;
        }

        private static IList<byte> RemovePrefix(CodeContext/*!*/ context, IList<byte> data, DecodeByte decFunc) {
            int count = 0;
            while (count < data.Count) {
                int current = decFunc(data[count]);
                if (current == InvalidByte) {
                    throw Error(context, "Illegal char");
                }
                if (current >= 0) break;
                count++;
            }
            return count == 0 ? data : data.Substring(count);
        }

        private static void ProcessSuffix(CodeContext/*!*/ context, IList<byte> data, DecodeByte decFunc) {
            for (int i = 0; i < data.Count; i++) {
                int current = decFunc(data[i]);
                if (current >= 0 || current == InvalidByte) {
                    throw Error(context, "Trailing garbage");
                }
            }
        }

        private static byte[] tobytes(this string s) {
            byte[] ret = new byte[s.Length];
            for (int i = 0; i < s.Length; i++) {
                if (s[i] < 0x80) {
                    ret[i] = (byte)s[i];
                } else {
                    throw PythonOps.ValueError("string argument should contain only ASCII characters");
                }
            }
            return ret;
        }

        private static IList<byte> tobytes(this IBufferProtocol buffer) {
            if (buffer is IList<byte> list) return list;

            return new Bytes(buffer);
        }

        #endregion
    }
}
