// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting.Runtime;

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

        public static Bytes a2b_uu(CodeContext/*!*/ context, [NotNone] IBufferProtocol data) {
            using var buffer = data.GetBufferNoThrow();
            if (buffer is null) {
                throw PythonOps.TypeError($"argument should be bytes, buffer or ASCII string, not '{PythonOps.GetPythonTypeName(data)}'");
            }
            return a2b_uu_impl(context, buffer.AsReadOnlySpan());

            static Bytes a2b_uu_impl(CodeContext/*!*/ context, ReadOnlySpan<byte> data) {
                if (data.Length < 1) return Bytes.Make(new byte[32]);

                int lenDec = (data[0] + 32) % 64; // decoded length in bytes
                int lenEnc = (lenDec * 4 + 2) / 3; // encoded length in 6-bit chunks
                ReadOnlySpan<byte> suffix = null;
                if (data.Length - 1 > lenEnc) {
                    suffix = data.Slice(1 + lenEnc);
                    data = data.Slice(1, lenEnc);
                } else {
                    data = data.Slice(1);
                }

                using MemoryStream res = DecodeWorker(context, data, true, UuDecFunc);
                if (suffix.IsEmpty) {
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
        }

        public static Bytes a2b_uu(CodeContext/*!*/ context, [NotNone] string data)
            => a2b_uu(context, data.ToBytes());

        public static Bytes b2a_uu(CodeContext/*!*/ context, [NotNone] IBufferProtocol data) {
            using var buffer = data.GetBuffer();
            return b2a_uu_impl(context, buffer.AsReadOnlySpan());

            static Bytes b2a_uu_impl(CodeContext/*!*/ context, ReadOnlySpan<byte> data) {
                if (data.Length > 45) throw Error(context, "At most 45 bytes at once");

                using var res = new MemoryStream();
                res.WriteByte((byte)(32 + data.Length));
                res.EncodeData(data, (byte)' ', (int val) => (byte)(32 + val % 64));
                res.WriteByte((byte)'\n');
                return Bytes.Make(res.ToArray());
            }
        }

        #endregion

        #region base64

        public static Bytes a2b_base64(CodeContext/*!*/ context, [NotNone] IBufferProtocol data) {
            using var buffer = data.GetBufferNoThrow();
            if (buffer is null) {
                throw PythonOps.TypeError($"argument should be bytes, buffer or ASCII string, not '{PythonOps.GetPythonTypeName(data)}'");
            }
            return a2b_base64_impl(context, buffer.AsReadOnlySpan());

            static Bytes a2b_base64_impl(CodeContext/*!*/ context, ReadOnlySpan<byte> data) {
                data = RemovePrefix(context, data, Base64DecFunc);
                if (data.Length == 0) return Bytes.Empty;
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
        }

        public static Bytes a2b_base64(CodeContext/*!*/ context, [NotNone] string data)
            => a2b_base64(context, data.ToBytes());

        public static Bytes b2a_base64([NotNone] IBufferProtocol data, bool newline = true) {
            // TODO: newline should be a keyword only argument
            using var buffer = data.GetBuffer();
            return b2a_base64_impl(buffer.AsReadOnlySpan(), newline);

            static Bytes b2a_base64_impl(ReadOnlySpan<byte> data, bool newline) {
                if (data.Length == 0) return Bytes.Empty;
                using var res = new MemoryStream();
                res.EncodeData(data, (byte)'=', EncodeValue);
                if (newline)
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
        }

        #endregion

        #region qp

        private const int MAXLINESIZE = 76;

        [Documentation("a2b_qp(data, header=False)\n    Decode a string of qp-encoded data.")]
        public static Bytes a2b_qp([NotNone] IBufferProtocol data, bool header = false) {
            using var buffer = data.GetBufferNoThrow();
            if (buffer is null) {
                throw PythonOps.TypeError($"argument should be bytes, buffer or ASCII string, not '{PythonOps.GetPythonTypeName(data)}'");
            }
            return a2b_qp_impl(buffer.AsReadOnlySpan(), header);

            static Bytes a2b_qp_impl(ReadOnlySpan<byte> ascii_data, bool header) {
                var datalen = ascii_data.Length;

                var incount = 0;

                MemoryStream odata = new MemoryStream();

                while (incount < datalen) {
                    if (ascii_data[incount] == '=') {
                        incount++;
                        if (incount >= datalen) break;
                        // Soft line breaks
                        if ((ascii_data[incount] == '\n') || (ascii_data[incount] == '\r')) {
                            if (ascii_data[incount] != '\n') {
                                while (incount < datalen && ascii_data[incount] != '\n') incount++;
                            }
                            if (incount < datalen) incount++;
                        } else if (ascii_data[incount] == '=') {
                            // broken case from broken python qp
                            odata.WriteByte((byte)'=');
                            incount++;
                        } else if ((incount + 1 < datalen) && TryParseHex(ascii_data[incount], out byte x) && TryParseHex(ascii_data[incount + 1], out byte x2)) {
                            // hexval
                            odata.WriteByte(unchecked((byte)((x << 4) | x2)));
                            incount += 2;
                        } else {
                            odata.WriteByte((byte)'=');
                        }
                    } else if (header && ascii_data[incount] == '_') {
                        odata.WriteByte((byte)' ');
                        incount++;
                    } else {
                        odata.WriteByte(ascii_data[incount]);
                        incount++;
                    }
                }

                return Bytes.Make(odata.ToArray());
            }
        }

        public static Bytes a2b_qp([NotNone] string data, bool header = false)
            => a2b_qp(data.ToBytes(), header);

        [Documentation(@"b2a_qp(data, quotetabs=False, istext=True, header=False) -> s;
 Encode a string using quoted-printable encoding.

On encoding, when istext is set, newlines are not encoded, and white
space at end of lines is.  When istext is not set, \\r and \\n (CR/LF) are
both encoded.  When quotetabs is set, space and tabs are encoded.")]
        public static Bytes b2a_qp([NotNone] IBufferProtocol data, bool quotetabs = false, bool istext = true, bool header = false) {
            using var buffer = data.GetBuffer();
            return b2a_qp_impl(buffer.AsReadOnlySpan().MakeString(), quotetabs, istext, header);
        }

        private static Bytes b2a_qp_impl(string data, bool quotetabs, bool istext, bool header) {
            bool crlf = data.Contains("\r\n");

            StringBuilder odata = new StringBuilder();

            var incount = 0;
            var outcount = 0;
            var linelen = 0;

            while (incount < data.Length) {
                if ((data[incount] > 126) || (data[incount] == '=') || (header && data[incount] == '_') ||
                    ((data[incount] == '.') && (linelen == 0) && (incount + 1 == data.Length || data[incount + 1] == '\n' || data[incount + 1] == '\r' || data[incount + 1] == 0)) ||
                    (!istext && ((data[incount] == '\r') || (data[incount] == '\n'))) ||
                    ((data[incount] == '\t' || data[incount] == ' ') && (incount + 1 == data.Length)) ||
                   ((data[incount] < 33) && (data[incount] != '\r') && (data[incount] != '\n') &&
                    (quotetabs || (!quotetabs && ((data[incount] != '\t') && (data[incount] != ' ')))))) {

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
                    if (istext &&
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

                        if (crlf) { odata.Append('\r'); outcount++; }
                        odata.Append('\n'); outcount++;
                        if (data[incount] == '\r')
                            incount += 2;
                        else
                            incount++;
                    } else {
                        if ((incount + 1 != data.Length) &&
                           (data[incount + 1] != '\n') &&
                           (linelen + 1) >= MAXLINESIZE) {
                            odata.Append('='); outcount++;
                            if (crlf) { odata.Append('\r'); outcount++; }
                            odata.Append('\n'); outcount++;
                            linelen = 0;
                        }
                        linelen++;
                        if (header && data[incount] == ' ') {
                            odata.Append('_'); outcount++;
                            incount++;
                        } else {
                            odata.Append(data[incount++]); outcount++;
                        }
                    }
                }
            }

            return odata.ToString().ToBytes();

            static void to_hex(char ch, StringBuilder s, int index) {
                int uvalue = ch, uvalue2 = ch / 16;
                s.Append("0123456789ABCDEF"[uvalue2 % 16]);
                s.Append("0123456789ABCDEF"[uvalue % 16]);
            }
        }

        #endregion

        #region hqx

        public static object a2b_hqx(object? data) {
            throw new NotImplementedException();
        }
        public static object rledecode_hqx(object? data) {
            throw new NotImplementedException();
        }
        public static object rlecode_hqx(object? data) {
            throw new NotImplementedException();
        }
        public static object b2a_hqx(object? data) {
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

        public static int crc_hqx([NotNone] IBufferProtocol data, int crc) {
            using var buffer = data.GetBuffer();
            return crc_hqx_impl(buffer.AsReadOnlySpan(), crc);

            static int crc_hqx_impl(ReadOnlySpan<byte> data, int crc) {
                crc &= 0xffff;
                foreach (var b in data) {
                    crc = ((crc << 8) & 0xff00) ^ crctab_hqx[(crc >> 8) ^ b];
                }
                return crc;
            }
        }

        #endregion

        #region crc32

        [Documentation("crc32(data[, crc]) -> string\n\nComputes a CRC (Cyclic Redundancy Check) checksum of data.")]
        public static object crc32([NotNone] IBufferProtocol data, uint crc = 0) {
            // TODO: [PythonIndex(overflow=mask)] uint crc = 0
            using var buffer = data.GetBuffer();
            var res = crc32(buffer.AsReadOnlySpan(), crc);
            if (res <= int.MaxValue) return (int)res;
            return (BigInteger)res;
        }

        private static uint crc32(ReadOnlySpan<byte> buffer, uint baseValue) {
            uint remainder = (baseValue ^ 0xffffffff);
            foreach (byte val in buffer) {
                remainder ^= val;
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

        public static Bytes b2a_hex([NotNone] IBufferProtocol data) {
            using var buffer = data.GetBuffer();
            return b2a_hex_impl(buffer.AsReadOnlySpan());

            static Bytes b2a_hex_impl(ReadOnlySpan<byte> data) {
                byte[] res = new byte[data.Length * 2];
                for (var i = 0; i < data.Length; i++) {
                    var b = data[i];
                    res[2 * i] = ToAscii(b >> 4);
                    res[2 * i + 1] = ToAscii(b & 0xf);
                }
                return Bytes.Make(res);

                static byte ToAscii(int b) {
                    return (byte)(b < 10 ? '0' + b : 'a' + (b - 10));
                }
            }
        }

        public static Bytes hexlify([NotNone] IBufferProtocol data)
            => b2a_hex(data);

        public static Bytes a2b_hex(CodeContext/*!*/ context, [NotNone] IBufferProtocol data) {
            using var buffer = data.GetBufferNoThrow();
            if (buffer is null) {
                throw PythonOps.TypeError($"argument should be bytes, buffer or ASCII string, not '{PythonOps.GetPythonTypeName(data)}'");
            }
            return a2b_hex_impl(context, buffer.AsReadOnlySpan());

            static Bytes a2b_hex_impl(CodeContext/*!*/ context, ReadOnlySpan<byte> data) {
                if ((data.Length & 0x01) != 0) throw Error(context, "Odd-length string");

                byte[] res = new byte[data.Length / 2];
                for (int i = 0; i < res.Length; i++) {
                    var b1 = ParseHex(context, data[2 * i]);
                    var b2 = ParseHex(context, data[2 * i + 1]);
                    res[i] = (byte)(b1 * 16 + b2);
                }
                return Bytes.Make(res);

                static byte ParseHex(CodeContext/*!*/ context, byte b) {
                    if (TryParseHex(b, out byte x)) {
                        return x;
                    }
                    throw Error(context, "Non-hexadecimal digit found");
                }
            }
        }

        public static Bytes a2b_hex(CodeContext/*!*/ context, [NotNone] string data)
            => a2b_hex(context, data.ToBytes());

        public static Bytes unhexlify(CodeContext/*!*/ context, [NotNone] IBufferProtocol hexstr)
            => a2b_hex(context, hexstr);

        public static Bytes unhexlify(CodeContext/*!*/ context, [NotNone] string hexstr)
            => a2b_hex(context, hexstr);

        #endregion

        #region Private implementation

        private delegate int DecodeByte(byte val);

        private static void EncodeData(this MemoryStream res, ReadOnlySpan<byte> data, byte empty, Func<int, byte> encFunc) {
            int bits;
            for (int i = 0; i < data.Length; i += 3) {
                switch (data.Length - i) {
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

        private static int NextVal(CodeContext/*!*/ context, ReadOnlySpan<byte> data, ref int index, DecodeByte decFunc) {
            int res;
            while (index < data.Length) {
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

        private static int CountPadBytes(CodeContext/*!*/ context, ReadOnlySpan<byte> data, int bound, ref int index, DecodeByte decFunc) {
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

        private static int GetVal(CodeContext/*!*/ context, ReadOnlySpan<byte> data, int align, bool bounded, ref int index, DecodeByte decFunc) {
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

        private static MemoryStream DecodeWorker(CodeContext/*!*/ context, ReadOnlySpan<byte> data, bool bounded, DecodeByte decFunc) {
            var res = new MemoryStream();

            int i = 0;
            while (i < data.Length) {
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

        private static ReadOnlySpan<byte> RemovePrefix(CodeContext/*!*/ context, ReadOnlySpan<byte> data, DecodeByte decFunc) {
            int count = 0;
            while (count < data.Length) {
                int current = decFunc(data[count]);
                if (current == InvalidByte) {
                    throw Error(context, "Illegal char");
                }
                if (current >= 0) break;
                count++;
            }
            return count == 0 ? data : data.Slice(count);
        }

        private static void ProcessSuffix(CodeContext/*!*/ context, ReadOnlySpan<byte> data, DecodeByte decFunc) {
            for (int i = 0; i < data.Length; i++) {
                int current = decFunc(data[i]);
                if (current >= 0 || current == InvalidByte) {
                    throw Error(context, "Trailing garbage");
                }
            }
        }

        private static Bytes ToBytes(this string s) {
            if (StringOps.TryEncodeAscii(s, out Bytes? ascii))
                return ascii;
            throw PythonOps.ValueError("string argument should contain only ASCII characters");
        }

        private static bool TryParseHex(byte b, out byte x) {
            if (b.IsDigit()) {
                x = (byte)(b - '0');
            } else if (b >= 'A' && b <= 'F') {
                x = (byte)(b - 'A' + 10);
            } else if (b >= 'a' && b <= 'f') {
                x = (byte)(b - 'a' + 10);
            } else {
                x = default;
                return false;
            }
            return true;
        }
        #endregion
    }
}
