// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

[assembly: PythonModule("_codecs", typeof(IronPython.Modules.PythonCodecs))]
namespace IronPython.Modules {
    public static class PythonCodecs {
        public const string __doc__ = "Provides access to various codecs (ASCII, UTF7, UTF8, etc...)";

        internal const int EncoderIndex = 0;
        internal const int DecoderIndex = 1;
        internal const int StreamReaderIndex = 2;
        internal const int StreamWriterIndex = 3;

        #region ASCII Encoding

        public static PythonTuple ascii_decode(CodeContext context, [BytesConversion]IList<byte> input, string errors = "strict")
            => DoDecode(context, "ascii", PythonAsciiEncoding.Instance, input, errors, true).ToPythonTuple();

        public static PythonTuple ascii_encode(CodeContext context, string input, string errors = "strict")
            => DoEncode(context, "ascii", PythonAsciiEncoding.Instance, input, errors).ToPythonTuple();

        #endregion

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

            var res = StringOps.DoEncode(context, input, errors, encoding, e);
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

        public static object decode(CodeContext/*!*/ context, object obj, string encoding = null, string errors = "strict") {
            if (encoding == null) {
                encoding = context.LanguageContext.DefaultEncoding.EncodingName;
            }
            PythonTuple t = lookup(context, encoding);

            return PythonOps.GetIndex(context, PythonCalls.Call(context, t[DecoderIndex], obj, errors), 0);
        }

        public static object encode(CodeContext/*!*/ context, object obj, string encoding = null, string errors = "strict") {
            if (encoding == null) {
                encoding = context.LanguageContext.DefaultEncoding.EncodingName;
            }
            PythonTuple t = lookup(context, encoding);

            return PythonOps.GetIndex(context, PythonCalls.Call(context, t[EncoderIndex], obj, errors), 0);
        }

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
                            if (i >= data.Count - 2 || !CharToInt((char)data[i + 1], out int dig1) || !CharToInt((char)data[i + 2], out int dig2)) {
                                switch (errors) {
                                    case "strict":
                                        if (i >= data.Count - 2) {
                                            throw PythonOps.ValueError("invalid character value");
                                        } else {
                                            throw PythonOps.ValueError("invalid hexadecimal digit");
                                        }
                                    case "replace":
                                        res.Append("?");
                                        while (i < (data.Count - 2)) {
                                            res.Append((char)data[++i]);
                                        }
                                        continue;
                                    default:
                                        throw PythonOps.ValueError("decoding error; unknown error handling code: " + errors);
                                }
                            }
                            res.Append((char)(dig1 * 16 + dig2));
                            i += 2;
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

        public static PythonTuple/*!*/ escape_encode(string text, string errors = "strict") {
            StringBuilder res = new StringBuilder();
            for (int i = 0; i < text.Length; i++) {
                switch (text[i]) {
                    case '\n': res.Append("\\n"); break;
                    case '\r': res.Append("\\r"); break;
                    case '\t': res.Append("\\t"); break;
                    case '\\': res.Append("\\\\"); break;
                    case '\'': res.Append("\\'"); break;
                    default:
                        if (text[i] < 0x20 || text[i] >= 0x7f) {
                            res.AppendFormat("\\x{0:x2}", (int)text[i]);
                        } else {
                            res.Append(text[i]);
                        }
                        break;
                }
            }
            return PythonTuple.MakeTuple(res.ToString(), text.Length);
        }

        #region Latin-1 Functions

        public static PythonTuple latin_1_decode(CodeContext context, [BytesConversion]IList<byte> input, string errors = "strict")
            => DoDecode(context, "latin-1", Encoding.GetEncoding("iso-8859-1"), input, errors, true).ToPythonTuple();

        public static PythonTuple latin_1_encode(CodeContext context, string input, string errors = "strict")
            => DoEncode(context, "latin-1", Encoding.GetEncoding("iso-8859-1"), input, errors).ToPythonTuple();

        #endregion

        public static PythonTuple lookup(CodeContext/*!*/ context, string encoding) => PythonOps.LookupEncoding(context, encoding);

        [LightThrowing]
        public static object lookup_error(CodeContext/*!*/ context, string name) => PythonOps.LookupEncodingError(context, name);

#if FEATURE_ENCODING
        #region MBCS Functions

        [PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        public static PythonTuple mbcs_decode(CodeContext/*!*/ context, [BytesConversion]IList<byte> input, string errors = "strict", bool final = false)
            => DoDecode(context, "mbcs", Encoding.Default, input, errors, final).ToPythonTuple();

        [PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        public static PythonTuple mbcs_encode(CodeContext/*!*/ context, string input, string errors = "strict")
            => DoEncode(context, "mbcs", Encoding.Default, input, errors).ToPythonTuple();

        #endregion
#endif

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

        public static PythonTuple readbuffer_encode(CodeContext/*!*/ context, string input, string errors = null)
            => readbuffer_encode(DoEncode(context, "utf-8", Encoding.UTF8, input, "strict").Item1, errors);

        public static PythonTuple readbuffer_encode([BytesConversion]IList<byte> input, string errors = null)
            => PythonTuple.MakeTuple(new Bytes(input), input.Count);

        public static void register(CodeContext/*!*/ context, object search_function)
            => PythonOps.RegisterEncoding(context, search_function);

        public static void register_error(CodeContext/*!*/ context, string name, object handler)
            => PythonOps.RegisterEncodingError(context, name, handler);

        #region Unicode Escape Encoding

        public static PythonTuple unicode_escape_decode(string input) => throw PythonOps.NotImplementedError("unicode_escape_decode");

        public static PythonTuple unicode_escape_encode(string input) => throw PythonOps.NotImplementedError("unicode_escape_encode");

        public static PythonTuple unicode_internal_decode(CodeContext context, [BytesConversion]IList<byte> input, string errors = "strict")
            => DoDecode(context, "unicode-internal", Encoding.Unicode, input, errors, false).ToPythonTuple();

        public static PythonTuple unicode_internal_encode(CodeContext context, string input, [Optional]string errors)
            => DoEncode(context, "unicode-internal", Encoding.Unicode, input, errors, false).ToPythonTuple();

        public static PythonTuple unicode_internal_encode([BytesConversion]IList<byte> input, [Optional]string errors)
            => PythonTuple.MakeTuple(new Bytes(input), input.Count);

        #endregion

        #region Utf-16 Big Endian Functions

        public static PythonTuple utf_16_be_decode(CodeContext context, [BytesConversion]IList<byte> input, string errors = "strict", bool final = false)
            => DoDecode(context, "utf-16-be", Encoding.BigEndianUnicode, input, errors, final).ToPythonTuple();

        public static PythonTuple utf_16_be_encode(CodeContext context, string input, string errors = "strict")
            => DoEncode(context, "utf-16-be", Encoding.BigEndianUnicode, input, errors).ToPythonTuple();

        #endregion

        #region Utf-16 Functions

        public static PythonTuple utf_16_decode(CodeContext context, [BytesConversion]IList<byte> input, string errors = "strict", bool final = false)
            => DoDecode(context, "utf-16", Encoding.Unicode, input, errors, final).ToPythonTuple();

        public static PythonTuple utf_16_encode(CodeContext context, string input, string errors = "strict")
            => DoEncode(context, "utf-16", Encoding.Unicode, input, errors, true).ToPythonTuple();

        #endregion

        public static PythonTuple utf_16_ex_decode(CodeContext context, [BytesConversion]IList<byte> input, string errors = "strict")
            => utf_16_ex_decode(context, input, errors, null, null);

        public static PythonTuple utf_16_ex_decode(CodeContext context, [BytesConversion]IList<byte> input, string errors, object unknown1, object unknown2) {
            byte[] lePre = Encoding.Unicode.GetPreamble();
            byte[] bePre = Encoding.BigEndianUnicode.GetPreamble();

            string instr = Converter.ConvertToString(input);
            bool match = true;
            if (instr.Length > lePre.Length) {
                for (int i = 0; i < lePre.Length; i++) {
                    if ((byte)instr[i] != lePre[i]) {
                        match = false;
                        break;
                    }
                }
                if (match) {
                    return PythonTuple.MakeTuple(string.Empty, lePre.Length, -1);
                }
                match = true;
            }

            if (instr.Length > bePre.Length) {
                for (int i = 0; i < bePre.Length; i++) {
                    if ((byte)instr[i] != bePre[i]) {
                        match = false;
                        break;
                    }
                }

                if (match) {
                    return PythonTuple.MakeTuple(string.Empty, bePre.Length, 1);
                }
            }

            PythonTuple res = utf_16_decode(context, input, errors, false) as PythonTuple;
            return PythonTuple.MakeTuple(res[0], res[1], 0);
        }

        #region Utf-16 Le Functions

        public static PythonTuple utf_16_le_decode(CodeContext context, [BytesConversion]IList<byte> input, string errors = "strict", bool final = false)
            => DoDecode(context, "utf-16-le", Encoding.Unicode, input, errors, final).ToPythonTuple();

        public static PythonTuple utf_16_le_encode(CodeContext context, string input, string errors = "strict")
            => DoEncode(context, "utf-16-le", Encoding.Unicode, input, errors).ToPythonTuple();

        #endregion

        #region Utf-7 Functions

#if FEATURE_ENCODING

        public static PythonTuple utf_7_decode(CodeContext context, [BytesConversion]IList<byte> input, string errors = "strict", bool final = false)
            => DoDecode(context, "utf-7", Encoding.UTF7, input, errors, final).ToPythonTuple();

        public static PythonTuple utf_7_encode(CodeContext context, string input, string errors = "strict")
            => DoEncode(context, "utf-7", Encoding.UTF7, input, errors).ToPythonTuple();

#endif

        #endregion

        #region Utf-8 Functions

        public static PythonTuple utf_8_decode(CodeContext context, [BytesConversion]IList<byte> input, string errors = "strict", bool final = false)
            => DoDecode(context, "utf-8", Encoding.UTF8, input, errors, final).ToPythonTuple();

        public static PythonTuple utf_8_encode(CodeContext context, string input, string errors = "strict")
            => DoEncode(context, "utf-8", Encoding.UTF8, input, errors).ToPythonTuple();

        #endregion

#if FEATURE_ENCODING

        #region Utf-32 Functions

        public static PythonTuple utf_32_decode(CodeContext context, [BytesConversion]IList<byte> input, string errors = "strict", bool final = false)
            => DoDecode(context, "utf-32", Encoding.UTF32, input, errors, final).ToPythonTuple();

        public static PythonTuple utf_32_encode(CodeContext context, string input, string errors = "strict")
            => DoEncode(context, "utf-32", Encoding.UTF32, input, errors, true).ToPythonTuple();

        #endregion

        public static PythonTuple utf_32_ex_decode(CodeContext context, [BytesConversion]IList<byte> input, string errors = "strict")
            => utf_32_ex_decode(context, input, errors, null, null);

        public static PythonTuple utf_32_ex_decode(CodeContext context, [BytesConversion]IList<byte> input, string errors, object byteorder, object final) {
            byte[] lePre = Encoding.UTF32.GetPreamble();

            string instr = Converter.ConvertToString(input);
            bool match = true;
            if (instr.Length > lePre.Length) {
                for (int i = 0; i < lePre.Length; i++) {
                    if ((byte)instr[i] != lePre[i]) {
                        match = false;
                        break;
                    }
                }
                if (match) {
                    return PythonTuple.MakeTuple(string.Empty, lePre.Length, -1);
                }
            }

            PythonTuple res = utf_32_decode(context, input, errors) as PythonTuple;
            return PythonTuple.MakeTuple(res[0], res[1], 0);
        }

        #region Utf-32 Le Functions

        public static PythonTuple utf_32_le_decode(CodeContext context, [BytesConversion]IList<byte> input, string errors = "strict", bool final = false)
            => DoDecode(context, "utf-32-le", Encoding.UTF32, input, errors, final).ToPythonTuple();

        public static PythonTuple utf_32_le_encode(CodeContext context, string input, string errors = "strict")
            => DoEncode(context, "utf-32-le", Encoding.UTF32, input, errors).ToPythonTuple();

        #endregion

        #region Utf-32 Be Functions

        private static Encoding utf32BeEncoding = null;
        private static Encoding UTF32BE {
            get {
                if (utf32BeEncoding == null) utf32BeEncoding = new UTF32Encoding(true, true);
                return utf32BeEncoding;
            }
        }

        public static PythonTuple utf_32_be_decode(CodeContext context, [BytesConversion]IList<byte> input, string errors = "strict", bool final = false)
            => DoDecode(context, "utf-32-be", UTF32BE, input, errors, final).ToPythonTuple();

        public static PythonTuple utf_32_be_encode(CodeContext context, string input, string errors = "strict")
            => DoEncode(context, "utf-32-be", UTF32BE, input, errors).ToPythonTuple();

        #endregion

#endif

        #region Private implementation

        private static Tuple<string, int> DoDecode(CodeContext context, string encodingName, Encoding encoding, [BytesConversion]IList<byte> input, string errors, bool final) {
            var decoded = StringOps.DoDecode(context, input, errors, encodingName, encoding, final, out int numBytes);
            return Tuple.Create(decoded, numBytes);
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
                    throw PythonOps.UnicodeEncodeError("charmap", c, index, "'charmap' codec can't encode character u'\\x{0:x}' in position {1}: character maps to <undefined>", (int)c, index);
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
                    throw PythonOps.UnicodeEncodeError("charmap", c, charIndex, "'charmap' codec can't encode character u'\\x{0:x}' in position {1}: character maps to <undefined>", (int)c, charIndex);
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
