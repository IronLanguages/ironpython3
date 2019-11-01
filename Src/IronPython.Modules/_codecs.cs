// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;

[assembly: PythonModule("_codecs", typeof(IronPython.Modules.PythonCodecs))]
namespace IronPython.Modules {
    public static class PythonCodecs {
        public const string __doc__ = "Provides access to various codecs (ASCII, UTF7, UTF8, etc...)";

        internal const int EncoderIndex = 0;
        internal const int DecoderIndex = 1;
        internal const int StreamReaderIndex = 2;
        internal const int StreamWriterIndex = 3;

        private static Encoding MbcsEncoding;

        static PythonCodecs() {
#if NETCOREAPP || NETSTANDARD
            // This ensures that Encoding.GetEncoding(0) will return the default Windows ANSI code page
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
            // Use Encoding.GetEncoding(0) instead of Encoding.Default (which returns UTF-8 with .NET Core)
            MbcsEncoding = Encoding.GetEncoding(0);
        }

        public static PythonTuple lookup(CodeContext/*!*/ context, string encoding) => PythonOps.LookupEncoding(context, encoding);

        [LightThrowing]
        public static object lookup_error(CodeContext/*!*/ context, string name) => PythonOps.LookupEncodingError(context, name);

        public static void register(CodeContext/*!*/ context, object search_function)
            => PythonOps.RegisterEncoding(context, search_function);

        public static void register_error(CodeContext/*!*/ context, string name, object handler)
            => PythonOps.RegisterEncodingError(context, name, handler);

        #region ASCII Encoding

        public static PythonTuple ascii_decode(CodeContext context, [BytesConversion]IList<byte> input, string errors = "strict")
            => DoDecode(context, "ascii", PythonAsciiEncoding.Instance, input, errors, input.Count).ToPythonTuple();

        public static PythonTuple ascii_encode(CodeContext context, string input, string errors = "strict")
            => DoEncode(context, "ascii", PythonAsciiEncoding.Instance, input, errors).ToPythonTuple();

        #endregion

        #region Charmap Encoding

        /// <summary>
        /// Creates an optimized encoding mapping that can be consumed by an optimized version of charmap_encode.
        /// </summary>
        public static EncodingMap charmap_build(string decoding_table) {
            if (decoding_table.Length != 256) {
                throw PythonOps.TypeError("charmap_build expected 256 character string");
            }

            EncodingMap map = new EncodingMap();
            for (int i = 0; i < decoding_table.Length; i++) {
                map.Mapping[(int)decoding_table[i]] = (char)i;
            }
            return map;
        }

        /// <summary>
        /// Encodes the input string with the specified optimized encoding map.
        /// </summary>
        public static PythonTuple charmap_encode(CodeContext context, string input, string errors, [NotNull]EncodingMap map) {
            return CharmapEncodeWorker(context, input, errors, new EncodingMapEncoding(map, errors));
        }

        public static PythonTuple charmap_encode(CodeContext context, string input, string errors = "strict", IDictionary<object, object> map = null) {
            Encoding e = map != null ? new CharmapEncoding(map, errors) : null;
            return CharmapEncodeWorker(context, input, errors, e);
        }

        private static PythonTuple CharmapEncodeWorker(CodeContext context, string input, string errors, Encoding e) {
            if (input.Length == 0) {
                return PythonTuple.MakeTuple(Bytes.Empty, 0);
            }

            string encoding = "charmap";

            // default to latin-1 if an encoding is not specified
            if (e == null) {
                e = Encoding.GetEncoding("iso-8859-1");
                encoding = "latin-1";
            }

            var res = StringOps.DoEncode(context, input, errors, encoding, e, true);
            return PythonTuple.MakeTuple(res, res.Count);
        }

        /// <summary>
        /// Decodes the input string using the provided string mapping.
        /// </summary>
        public static PythonTuple charmap_decode(CodeContext context, [BytesConversion]IList<byte> input, string errors, [NotNull]string map) {
            EncodingMap m = new EncodingMap();
            for (int i = 0; i < map.Length; i++) {
                m.Mapping[i] = map[i];
            }
            return CharmapDecodeWorker(context, input, errors, new EncodingMapEncoding(m, errors));
        }

        public static PythonTuple charmap_decode(CodeContext context, [BytesConversion]IList<byte> input, string errors = "strict", IDictionary<object, object> map = null) {
            Encoding e = map != null ? new CharmapEncoding(map, errors) : null;
            return CharmapDecodeWorker(context, input, errors, e);
        }

        private static PythonTuple CharmapDecodeWorker(CodeContext context, IList<byte> input, string errors, Encoding e) {
            if (input.Count == 0) {
                return PythonTuple.MakeTuple(string.Empty, 0);
            }

            string encoding = "charmap";

            // default to latin-1 if an encoding is not specified
            if (e == null) {
                e = Encoding.GetEncoding("iso-8859-1");
                encoding = "latin-1";
            }

            var res = StringOps.DoDecode(context, input, errors, encoding, e);
            return PythonTuple.MakeTuple(res, res.Length);
        }

        #endregion

        #region Generic Encoding

        public static object decode(CodeContext/*!*/ context, object obj, string encoding = null, string errors = "strict") {
            if (encoding == null) {
                if (obj is IList<byte> bytesLikeObj) {
                    PythonContext lc = context.LanguageContext;
                    return StringOps.DoDecode(context, bytesLikeObj, errors, lc.GetDefaultEncodingName(), lc.DefaultEncoding);
                } else {
                    throw PythonOps.TypeError("expected bytes-like object, got {0}", PythonTypeOps.GetName(obj));
                }
            }
            PythonTuple t = lookup(context, encoding);

            return PythonOps.GetIndex(context, PythonCalls.Call(context, t[DecoderIndex], obj, errors), 0);
        }

        public static object encode(CodeContext/*!*/ context, object obj, string encoding = null, string errors = "strict") {
            if (encoding == null) {
                if (obj is string str) {
                    PythonContext lc = context.LanguageContext;
                    return StringOps.DoEncode(context, str, errors, lc.GetDefaultEncodingName(), lc.DefaultEncoding, includePreamble: true);
                } else {
                    throw PythonOps.TypeError("expected str, got {0}", PythonTypeOps.GetName(obj));
                }
            }
            PythonTuple t = lookup(context, encoding);

            return PythonOps.GetIndex(context, PythonCalls.Call(context, t[EncoderIndex], obj, errors), 0);
        }

        #endregion

        #region Escape Encoding

        public static PythonTuple escape_decode(CodeContext/*!*/ context, string data, string errors = "strict")
            => escape_decode(DoEncode(context, "utf-8", Encoding.UTF8, data, "strict").Item1, errors);

        public static PythonTuple escape_decode([BytesConversion]IList<byte> data, string errors = "strict") {
            var res = new StringBuilder();
            for (int i = 0; i < data.Count; i++) {
                if (data[i] == '\\') {
                    if (i == data.Count - 1) throw PythonOps.ValueError("\\ at end of string");

                    switch ((char)data[++i]) {
                        case 'a': res.Append((char)0x07); break;
                        case 'b': res.Append((char)0x08); break;
                        case 't': res.Append('\t'); break;
                        case 'n': res.Append('\n'); break;
                        case 'r': res.Append('\r'); break;
                        case '\\': res.Append('\\'); break;
                        case 'f': res.Append((char)0x0c); break;
                        case 'v': res.Append((char)0x0b); break;
                        case '\n': break;
                        case 'x':
                            if (++i < data.Count && CharToInt((char)data[i], out int dig1)
                                    && ++i < data.Count && CharToInt((char)data[i], out int dig2)) {
                                res.Append((char)(dig1 * 16 + dig2));
                            } else {
                                switch (errors) {
                                    case "strict":
                                        throw PythonOps.ValueError("invalid \\x escape at position {0}", i);
                                    case "replace":
                                        res.Append("?");
                                        i--;
                                        break;
                                    default:
                                        throw PythonOps.ValueError("decoding error; unknown error handling code: " + errors);
                                }
                            }
                            break;
                        default:
                            res.Append("\\" + (char)data[i]);
                            break;
                    }
                } else {
                    res.Append((char)data[i]);
                }

            }
            return PythonTuple.MakeTuple(Bytes.Make(res.ToString().MakeByteArray()), data.Count);
        }

        private static bool CharToInt(char ch, out int val) {
            if (char.IsDigit(ch)) {
                val = ch - '0';
                return true;
            }
            ch = char.ToUpper(ch);
            if (ch >= 'A' && ch <= 'F') {
                val = ch - 'A' + 10;
                return true;
            }

            val = 0;
            return false;
        }

        public static PythonTuple/*!*/ escape_encode([BytesConversion]IList<byte> text, string errors = "strict") {
            StringBuilder res = new StringBuilder();
            for (int i = 0; i < text.Count; i++) {
                switch (text[i]) {
                    case (byte)'\n': res.Append("\\n"); break;
                    case (byte)'\r': res.Append("\\r"); break;
                    case (byte)'\t': res.Append("\\t"); break;
                    case (byte)'\\': res.Append("\\\\"); break;
                    case (byte)'\'': res.Append("\\'"); break;
                    default:
                        if (text[i] < 0x20 || text[i] >= 0x7f) {
                            res.AppendFormat("\\x{0:x2}", text[i]);
                        } else {
                            res.Append((char)text[i]);
                        }
                        break;
                }
            }
            return PythonTuple.MakeTuple(Bytes.Make(res.ToString().MakeByteArray()), text.Count);
        }

        #endregion

        #region Latin-1 Functions

        public static PythonTuple latin_1_decode(CodeContext context, [BytesConversion]IList<byte> input, string errors = "strict")
            => DoDecode(context, "latin-1", Encoding.GetEncoding("iso-8859-1"), input, errors, input.Count).ToPythonTuple();

        public static PythonTuple latin_1_encode(CodeContext context, string input, string errors = "strict")
            => DoEncode(context, "latin-1", Encoding.GetEncoding("iso-8859-1"), input, errors).ToPythonTuple();

        #endregion

        #region MBCS Functions
#if FEATURE_ENCODING

        [PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        public static PythonTuple mbcs_decode(CodeContext/*!*/ context, [BytesConversion]IList<byte> input, string errors = "strict", bool final = false)
            => DoDecode(context, "mbcs", MbcsEncoding, input, errors, input.Count).ToPythonTuple();

        [PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        public static PythonTuple mbcs_encode(CodeContext/*!*/ context, string input, string errors = "strict")
            => DoEncode(context, "mbcs", MbcsEncoding, input, errors).ToPythonTuple();

#endif
        #endregion

        #region Raw Unicode Escape Encoding Functions

        public static PythonTuple raw_unicode_escape_decode(CodeContext/*!*/ context, string input, string errors = "strict")
            => raw_unicode_escape_decode(context, DoEncode(context, "utf-8", Encoding.UTF8, input, "strict").Item1, errors);

        public static PythonTuple raw_unicode_escape_decode(CodeContext/*!*/ context, [BytesConversion]IList<byte> input, string errors = "strict") {
            return PythonTuple.MakeTuple(
                StringOps.RawDecode(context, input, "raw-unicode-escape", errors),
                input.Count
            );
        }

        public static PythonTuple raw_unicode_escape_encode(CodeContext/*!*/ context, string input, string errors = "strict") {
            return PythonTuple.MakeTuple(
                StringOps.RawEncode(context, input, "raw-unicode-escape", errors),
                input.Length
            );
        }

        #endregion

        public static PythonTuple readbuffer_encode(CodeContext/*!*/ context, string input, string errors = null)
            => readbuffer_encode(DoEncode(context, "utf-8", Encoding.UTF8, input, "strict").Item1, errors);

        public static PythonTuple readbuffer_encode([BytesConversion]IList<byte> input, string errors = null)
            => PythonTuple.MakeTuple(new Bytes(input), input.Count);

        #region Unicode Escape Encoding Functions

        public static PythonTuple unicode_escape_decode(string input) => throw PythonOps.NotImplementedError("unicode_escape_decode");

        public static PythonTuple unicode_escape_encode(string input) => throw PythonOps.NotImplementedError("unicode_escape_encode");

        public static PythonTuple unicode_internal_decode(CodeContext context, [BytesConversion]IList<byte> input, string errors = "strict") {
            PythonOps.Warn(context, PythonExceptions.DeprecationWarning, "unicode_internal codec has been deprecated");
            return DoDecode(context, "unicode-internal", Encoding.Unicode, input, errors, input.Count).ToPythonTuple();
        }

        public static PythonTuple unicode_internal_encode(CodeContext context, string input, string errors = "strict") {
            PythonOps.Warn(context, PythonExceptions.DeprecationWarning, "unicode_internal codec has been deprecated");
            return DoEncode(context, "unicode-internal", Encoding.Unicode, input, errors, false).ToPythonTuple();
        }

        public static PythonTuple unicode_internal_encode(CodeContext context, [BytesConversion]IList<byte> input, string errors = "strict") {
            PythonOps.Warn(context, PythonExceptions.DeprecationWarning, "unicode_internal codec has been deprecated");
            return PythonTuple.MakeTuple(new Bytes(input), input.Count);
        }

        #endregion

        #region Utf-16 Functions

        public static PythonTuple utf_16_decode(CodeContext context, [BytesConversion]IList<byte> input, string errors = "strict", bool final = false) {
            PythonTuple res = utf_16_ex_decode(context, input, errors, 0, final);
            return PythonTuple.MakeTuple(res[0], res[1]);
        }

        public static PythonTuple utf_16_encode(CodeContext context, string input, string errors = "strict")
            => DoEncode(context, "utf-16", Utf16LeBomEncoding, input, errors, true).ToPythonTuple();

        public static PythonTuple utf_16_ex_decode(CodeContext context, [BytesConversion]IList<byte> input, string errors = "strict", int byteorder = 0, bool final = false) {

            Tuple<string, int> res;

            if (byteorder != 0) {
                res = (byteorder > 0) ?
                    DoDecode(context, "utf-16-be", Utf16BeEncoding, input, errors, NumEligibleUtf16Bytes(input, final, false))
                :
                    DoDecode(context, "utf-16-le", Utf16LeEncoding, input, errors, NumEligibleUtf16Bytes(input, final, true));

            } else {
                byteorder = Utf16DetectByteorder(input);
                res = (byteorder > 0) ?
                    DoDecode(context, "utf-16-be", Utf16BeBomEncoding, input, errors, NumEligibleUtf16Bytes(input, final, false))
                :
                    DoDecode(context, "utf-16-le", Utf16LeBomEncoding, input, errors, NumEligibleUtf16Bytes(input, final, true));
            }

            return PythonTuple.MakeTuple(res.Item1, res.Item2, byteorder);
        }

        private static int Utf16DetectByteorder(IList<byte> input) {
            if (input.StartsWith(BOM_UTF16_LE)) return -1;
            if (input.StartsWith(BOM_UTF16_BE)) return 1;
            return 0;
        }

        private static int NumEligibleUtf16Bytes(IList<byte> input, bool final, bool isLE) {
            int numBytes = input.Count;
            if (!final) {
                numBytes -= numBytes % 2;
                if (numBytes >= 2 && (input[numBytes - (isLE ? 1 : 2)] & 0xFC) == 0xD8) { // high surrogate
                    numBytes -= 2;
                }
            }
            return numBytes;
        }

        #endregion

        #region Utf-16-LE Functions

        private static Encoding Utf16LeEncoding => _utf16LeEncoding ??= new UnicodeEncoding(bigEndian: false, byteOrderMark: false);
        private static Encoding _utf16LeEncoding = null;

        private static Encoding Utf16LeBomEncoding => Encoding.Unicode; // same as new UnicodeEncoding(bigEndian: false, byteOrderMark: true);

        private static byte[] BOM_UTF16_LE => _bom_utf16_le ??= Utf16LeBomEncoding.GetPreamble();
        private static byte[] _bom_utf16_le = null;

        public static PythonTuple utf_16_le_decode(CodeContext context, [BytesConversion]IList<byte> input, string errors = "strict", bool final = false)
            => DoDecode(context, "utf-16-le", Utf16LeEncoding, input, errors, NumEligibleUtf16Bytes(input, final, isLE: true)).ToPythonTuple();

        public static PythonTuple utf_16_le_encode(CodeContext context, string input, string errors = "strict")
            => DoEncode(context, "utf-16-le", Utf16LeEncoding, input, errors).ToPythonTuple();

        #endregion

        #region Utf-16-BE Functions

        private static Encoding Utf16BeEncoding => _utf16BeEncoding ??= new UnicodeEncoding(bigEndian: true, byteOrderMark: false);
        private static Encoding _utf16BeEncoding = null;

        private static Encoding Utf16BeBomEncoding => Encoding.BigEndianUnicode; // same as new UnicodeEncoding(bigEndian: true, byteOrderMark: true);

        private static byte[] BOM_UTF16_BE => _bom_utf16_be ??= Utf16BeBomEncoding.GetPreamble();
        private static byte[] _bom_utf16_be = null;

        public static PythonTuple utf_16_be_decode(CodeContext context, [BytesConversion]IList<byte> input, string errors = "strict", bool final = false)
            => DoDecode(context, "utf-16-be", Utf16BeEncoding, input, errors, NumEligibleUtf16Bytes(input, final, isLE: false)).ToPythonTuple();

        public static PythonTuple utf_16_be_encode(CodeContext context, string input, string errors = "strict")
            => DoEncode(context, "utf-16-be", Utf16BeEncoding, input, errors).ToPythonTuple();

        #endregion

        #region Utf-7 Functions

#if FEATURE_ENCODING

        public static PythonTuple utf_7_decode(CodeContext context, [BytesConversion]IList<byte> input, string errors = "strict", bool final = false)
            => DoDecode(context, "utf-7", Encoding.UTF7, input, errors, NumEligibleUtf7Bytes(input, final)).ToPythonTuple();

        public static PythonTuple utf_7_encode(CodeContext context, string input, string errors = "strict")
            => DoEncode(context, "utf-7", Encoding.UTF7, input, errors).ToPythonTuple();

        private static int NumEligibleUtf7Bytes(IList<byte> input, bool final) {
            int numBytes = input.Count;
            if (!final) {
                int blockStart = -1;
                for (int i = 0; i < numBytes; i++) {
                    char c = (char)input[i]; // to ASCII
                    if (blockStart < 0 && c == '+') {
                        blockStart = i;
                    } else if (blockStart >= 0 && !char.IsLetterOrDigit(c) && c != '+' && c != '/' && !char.IsWhiteSpace(c)) {
                        blockStart = -1;
                    }
                }
                if (blockStart >= 0) numBytes = blockStart;
            }
            return numBytes;
        }

#endif

        #endregion

        #region Utf-8 Functions

        private static Encoding Utf8Encoding => _utf8Encoding ??= new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private static Encoding _utf8Encoding = null;

        public static PythonTuple utf_8_decode(CodeContext context, [BytesConversion]IList<byte> input, string errors = "strict", bool final = false)
            => DoDecode(context, "utf-8", Utf8Encoding, input, errors, NumEligibleUtf8Bytes(input, final)).ToPythonTuple();

        public static PythonTuple utf_8_encode(CodeContext context, string input, string errors = "strict")
            => DoEncode(context, "utf-8", Encoding.UTF8, input, errors).ToPythonTuple();

        private static int NumEligibleUtf8Bytes(IList<byte> input, bool final) {
            int numBytes = input.Count;
            if (!final) {
                // scan for incomplete but valid sequence at the end
                for (int i = 1; i < 4; i++) { // 4 is the max length of a valid sequence
                    int pos = numBytes - i;
                    if (pos < 0) break;

                    byte b = input[pos];
                    if ((b & 0b10000000) == 0) return numBytes; // ASCII
                    if ((b & 0b11000000) == 0b11000000) { // start byte
                        if ((b | 0b00011111) == 0b11011111 && i < 2) return pos; // 2-byte seq start
                        if ((b | 0b00001111) == 0b11101111 && i < 3) return pos; // 3-byte seq start
                        if ((b | 0b00000111) == 0b11110111) { // 4-byte seq start
                            if (b < 0b11110100) return pos; // chars up to U+FFFFF
                            if ((b == 0b11110100) && (i == 1 || input[numBytes - i + 1] < 0x90)) return pos; // U+100000 to U+10FFFF
                        }
                        return numBytes; // invalid sequence or valid but complete
                    }
                    // else continuation byte (0b10xxxxxx) hence continue scanning
                }
            }
            return numBytes;
        }

        #endregion

#if FEATURE_ENCODING

        #region Utf-32 Functions

        public static PythonTuple utf_32_decode(CodeContext context, [BytesConversion]IList<byte> input, string errors = "strict", bool final = false) {
            PythonTuple res = utf_32_ex_decode(context, input, errors, 0, final);
            return PythonTuple.MakeTuple(res[0], res[1]);
        }

        public static PythonTuple utf_32_encode(CodeContext context, string input, string errors = "strict")
            => DoEncode(context, "utf-32", Utf32LeBomEncoding, input, errors, includePreamble: true).ToPythonTuple();

        public static PythonTuple utf_32_ex_decode(CodeContext context, [BytesConversion]IList<byte> input, string errors = "strict", int byteorder = 0, bool final = false) {

            int numBytes = NumEligibleUtf32Bytes(input, final);
            Tuple<string, int> res;

            if (byteorder != 0) {
                res = (byteorder > 0) ?
                    DoDecode(context, "utf-32-be", Utf32BeEncoding, input, errors, numBytes)
                :
                    DoDecode(context, "utf-32-le", Utf32LeEncoding, input, errors, numBytes);

            } else {
                byteorder = Utf32DetectByteorder(input);
                res = (byteorder > 0) ?
                    DoDecode(context, "utf-32-be", Utf32BeBomEncoding, input, errors, numBytes)
                :
                    DoDecode(context, "utf-32-le", Utf32LeBomEncoding, input, errors, numBytes);
            }

            return PythonTuple.MakeTuple(res.Item1, res.Item2, byteorder);
        }

        private static int Utf32DetectByteorder(IList<byte> input) {
            if (input.StartsWith(BOM_UTF32_LE)) return -1;
            if (input.StartsWith(BOM_UTF32_BE)) return 1;
            return 0;
        }

        private static int NumEligibleUtf32Bytes(IList<byte> input, bool final) {
            int numBytes = input.Count;
            if (!final) numBytes -= numBytes % 4;
            return numBytes;
        }

        #endregion

        #region Utf-32-LE Functions

        private static Encoding Utf32LeEncoding => _utf32LeEncoding ??= new UTF32Encoding(bigEndian: false, byteOrderMark: false);
        private static Encoding _utf32LeEncoding = null;

        private static Encoding Utf32LeBomEncoding => Encoding.UTF32; // same as new UTF32Encoding(bigEndian: false, byteOrderMark: true);

        private static byte[] BOM_UTF32_LE => _bom_utf32_le ??= Utf32LeBomEncoding.GetPreamble();
        private static byte[] _bom_utf32_le = null;

        public static PythonTuple utf_32_le_decode(CodeContext context, [BytesConversion]IList<byte> input, string errors = "strict", bool final = false)
            => DoDecode(context, "utf-32-le", Utf32LeEncoding, input, errors, NumEligibleUtf32Bytes(input, final)).ToPythonTuple();

        public static PythonTuple utf_32_le_encode(CodeContext context, string input, string errors = "strict")
            => DoEncode(context, "utf-32-le", Utf32LeEncoding, input, errors).ToPythonTuple();

        #endregion

        #region Utf-32-BE Functions

        private static Encoding Utf32BeEncoding => _utf32BeEncoding ??= new UTF32Encoding(bigEndian: true, byteOrderMark: false);
        private static Encoding _utf32BeEncoding = null;

        private static Encoding Utf32BeBomEncoding => _utf32BeBomEncoding ??= new UTF32Encoding(bigEndian: true, byteOrderMark: true);
        private static Encoding _utf32BeBomEncoding = null;

        private static byte[] BOM_UTF32_BE => _bom_utf32_be ??= Utf32BeBomEncoding.GetPreamble();
        private static byte[] _bom_utf32_be = null;

        public static PythonTuple utf_32_be_decode(CodeContext context, [BytesConversion]IList<byte> input, string errors = "strict", bool final = false)
            => DoDecode(context, "utf-32-be", Utf32BeEncoding, input, errors, NumEligibleUtf32Bytes(input, final)).ToPythonTuple();

        public static PythonTuple utf_32_be_encode(CodeContext context, string input, string errors = "strict")
            => DoEncode(context, "utf-32-be", Utf32BeEncoding, input, errors).ToPythonTuple();

        #endregion

#endif

        #region Private implementation

        private static Tuple<string, int> DoDecode(CodeContext context, string encodingName, Encoding encoding, [BytesConversion]IList<byte> input, string errors, int numBytes) {
            var decoded = StringOps.DoDecode(context, input, errors, encodingName, encoding, numBytes, out int numConsumed);
            return Tuple.Create(decoded, numConsumed);
        }

        private static Tuple<Bytes, int> DoEncode(CodeContext context, string encodingName, Encoding encoding, string input, string errors, bool includePreamble = false) {
            var res = StringOps.DoEncode(context, input, errors, encodingName, encoding, includePreamble);
            return Tuple.Create(res, input.Length);
        }

        #endregion
    }

    /// <summary>
    /// Optimized encoding mapping that can be consumed by charmap_encode.
    /// </summary>
    [PythonHidden]
    public class EncodingMap {
        internal Dictionary<int, char> Mapping = new Dictionary<int, char>();
    }

#if FEATURE_ENCODING    // Encoding

    internal class EncodingMapEncoding : Encoding {
        private readonly EncodingMap _map;
        private readonly string _errors;

        public EncodingMapEncoding(EncodingMap map, string errors) {
            _map = map;
            _errors = errors;
        }

        public override int GetByteCount(char[] chars, int index, int count) {
            int byteCount = 0;
            int charEnd = index + count;
            while (index < charEnd) {
                char c = chars[index];

                if (!_map.Mapping.TryGetValue(c, out _)) {
                    EncoderFallbackBuffer efb = EncoderFallback.CreateFallbackBuffer();
                    if (efb.Fallback(c, index)) {
                        byteCount += efb.Remaining;
                    }
                } else {
                    byteCount++;
                }
                index++;
            }
            return byteCount;
        }

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex) {
            int charEnd = charIndex + charCount;
            int outputBytes = 0;
            while (charIndex < charEnd) {
                char c = chars[charIndex];
                char val;

                if (!_map.Mapping.TryGetValue((int)c, out val)) {
                    EncoderFallbackBuffer efb = EncoderFallback.CreateFallbackBuffer();
                    if (efb.Fallback(c, charIndex)) {
                        while (efb.Remaining != 0) {
                            bytes[byteIndex++] = (byte)_map.Mapping[(int)efb.GetNextChar()];
                            outputBytes++;
                        }
                    }
                } else {
                    bytes[byteIndex++] = (byte)val;
                    outputBytes++;
                }
                charIndex++;
            }
            return outputBytes;
        }

        public override int GetCharCount(byte[] bytes, int index, int count) {
            int byteEnd = index + count;
            int outputChars = 0;
            while (index < byteEnd) {
                byte b = bytes[index];

                if (!_map.Mapping.TryGetValue(b, out _)) {
                    DecoderFallbackBuffer dfb = DecoderFallback.CreateFallbackBuffer();
                    if (dfb.Fallback(new[] { b }, 0)) {
                        outputChars += dfb.Remaining;
                    }
                } else {
                    outputChars++;
                }
                index++;
            }
            return outputChars;
        }

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex) {
            int byteEnd = byteIndex + byteCount;
            int outputChars = 0;
            while (byteIndex < byteEnd) {
                byte b = bytes[byteIndex];
                char val;

                if (!_map.Mapping.TryGetValue(b, out val)) {
                    DecoderFallbackBuffer dfb = DecoderFallback.CreateFallbackBuffer();
                    if (dfb.Fallback(new[] { b }, 0)) {
                        while (dfb.Remaining != 0) {
                            chars[charIndex++] = (char)((int)_map.Mapping[(int)dfb.GetNextChar()]);
                            outputChars++;
                        }
                    }
                } else {
                    chars[charIndex++] = val;
                    outputChars++;
                }
                byteIndex++;
            }
            return outputChars;
        }

        public override int GetMaxByteCount(int charCount) {
            // TODO: revisit
            return charCount * 4;
        }

        public override int GetMaxCharCount(int byteCount) {
            // TODO: revisit
            return byteCount;
        }
    }

    internal class CharmapEncoding : Encoding {
        private readonly IDictionary<object, object> _map;
        private readonly string _errors;

        public CharmapEncoding(IDictionary<object, object> map, string errors) {
            _map = map;
            _errors = errors;
            FixupMap();
        }

        private void FixupMap() {
            // this is required if someone passes in a mapping like { 'a' : None }
            foreach (var k in _map) {
                if (k.Key is string s && s.Length == 1) {
                    _map[(int)s[0]] = k.Value;
                }
            }
        }

        public override int GetByteCount(char[] chars, int index, int count) {
            int byteCount = 0;
            int charEnd = index + count;
            while (index < charEnd) {
                char c = chars[index];
                object val;
                object charObj = (int)c;

                if (!_map.TryGetValue(charObj, out val) || (val == null && _errors == "strict")) {
                    EncoderFallbackBuffer efb = EncoderFallback.CreateFallbackBuffer();
                    if (efb.Fallback(c, index)) {
                        byteCount += efb.Remaining;
                    }
                } else if (val == null) {
                    throw PythonOps.UnicodeEncodeError("charmap", c.ToString(), index, index + 1, "character maps to <undefined>");
                } else if (val is string) {
                    byteCount += ((string)val).Length;
                } else if (val is int) {
                    byteCount++;
                } else {
                    throw PythonOps.TypeError("charmap must be an int, str, or None");
                }
                index++;
            }
            return byteCount;
        }

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex) {
            int charEnd = charIndex + charCount;
            int outputBytes = 0;
            while (charIndex < charEnd) {
                char c = chars[charIndex];
                object val;
                object obj = (int)c;
                if (!_map.TryGetValue(obj, out val) || (val == null && _errors == "strict")) {
                    EncoderFallbackBuffer efb = EncoderFallback.CreateFallbackBuffer();
                    if (efb.Fallback(c, charIndex)) {
                        while (efb.Remaining != 0) {
                            obj = (int)efb.GetNextChar();
                            bytes[byteIndex++] = (byte)((int)_map[obj]);
                            outputBytes++;
                        }
                    }
                } else if (val == null) {
                    throw PythonOps.UnicodeEncodeError("charmap", c.ToString(), charIndex, charIndex + 1, "character maps to <undefined>");
                } else if (val is string) {
                    string v = val as string;
                    for (int i = 0; i < v.Length; i++) {
                        bytes[byteIndex++] = (byte)v[i];
                        outputBytes++;
                    }
                } else if (val is int) {
                    bytes[byteIndex++] = (byte)(int)val;
                    outputBytes++;
                } else {
                    throw PythonOps.TypeError("charmap must be an int, str, or None");
                }
                charIndex++;
            }
            return outputBytes;
        }

        public override int GetCharCount(byte[] bytes, int index, int count) {
            int byteEnd = index + count;
            int outputChars = 0;
            while (index < byteEnd) {
                byte b = bytes[index];

                object val;
                object byteObj = ScriptingRuntimeHelpers.Int32ToObject((int)b);

                if (!_map.TryGetValue(byteObj, out val) || val == null) {
                    DecoderFallbackBuffer dfb = DecoderFallback.CreateFallbackBuffer();
                    if (dfb.Fallback(new[] { b }, 0)) {
                        outputChars += dfb.Remaining;
                    }
                } else if (val is string) {
                    outputChars += ((string)val).Length;
                } else if (val is int) {
                    outputChars++;
                } else {
                    throw PythonOps.TypeError("charmap must be an int, str, or None");
                }
                index++;
            }
            return outputChars;
        }

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex) {
            int byteEnd = byteIndex + byteCount;
            int outputChars = 0;
            while (byteIndex < byteEnd) {
                byte b = bytes[byteIndex];
                object val;
                object obj = ScriptingRuntimeHelpers.Int32ToObject((int)b);

                if (!_map.TryGetValue(obj, out val) || val == null) {
                    DecoderFallbackBuffer dfb = DecoderFallback.CreateFallbackBuffer();
                    if (dfb.Fallback(new[] { b }, 0)) {
                        while (dfb.Remaining != 0) {
                            chars[charIndex++] = dfb.GetNextChar();
                            outputChars++;
                        }
                    }
                } else if (val is string) {
                    string v = val as string;
                    for (int i = 0; i < v.Length; i++) {
                        chars[charIndex++] = v[i];
                        outputChars++;
                    }
                } else if (val is int) {
                    chars[charIndex++] = (char)(int)val;
                    outputChars++;
                } else {
                    throw PythonOps.TypeError("charmap must be an int, str, or None");
                }
                byteIndex++;
            }
            return outputChars;
        }

        public override int GetMaxByteCount(int charCount) {
            // TODO: revisit
            return charCount * 4;
        }

        public override int GetMaxCharCount(int byteCount) {
            // TODO: revisit
            return byteCount;
        }
    }

#endif
}
