// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using DisallowNullAttribute = System.Diagnostics.CodeAnalysis.DisallowNullAttribute;

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

        private static readonly Encoding MbcsEncoding;

        static PythonCodecs() {
#if NETCOREAPP || NETSTANDARD
            // This ensures that Encoding.GetEncoding(0) will return the default Windows ANSI code page
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
            // Use Encoding.GetEncoding(0) instead of Encoding.Default (which returns UTF-8 with .NET Core)
            MbcsEncoding = Encoding.GetEncoding(0);
        }

        public static PythonTuple lookup(CodeContext/*!*/ context, [NotNull]string encoding)
            => PythonOps.LookupEncoding(context, encoding);

        [LightThrowing]
        public static object lookup_error(CodeContext/*!*/ context, [NotNull]string name)
            => PythonOps.LookupEncodingError(context, name);

        public static void register(CodeContext/*!*/ context, object? search_function)
            => PythonOps.RegisterEncoding(context, search_function);

        public static void register_error(CodeContext/*!*/ context, [NotNull]string name, object? handler)
            => PythonOps.RegisterEncodingError(context, name, handler);

        #region ASCII Encoding

        public static PythonTuple ascii_decode(CodeContext context, [BytesConversion,NotNull]IList<byte> input, string? errors = null)
            => DoDecode(context, "ascii", PythonAsciiEncoding.Instance, input, errors, input.Count).ToPythonTuple();

        public static PythonTuple ascii_encode(CodeContext context, [NotNull]string input, string? errors = null)
            => DoEncode(context, "ascii", PythonAsciiEncoding.Instance, input, errors).ToPythonTuple();

        #endregion

        #region Charmap Encoding
#if FEATURE_ENCODING

        /// <summary>
        /// Creates an optimized encoding mapping that can be consumed by an optimized version of charmap_encode/charmap_decode.
        /// </summary>
        public static EncodingMap charmap_build([NotNull]string decoding_table) {
            if (decoding_table.Length == 0) {
                throw PythonOps.TypeError("charmap_build expected non-empty string");
            }

            return new EncodingMap(decoding_table, compileForDecoding: false, compileForEncoding: true);
        }

        /// <summary>
        /// Encodes the input string with the specified optimized encoding map.
        /// </summary>
        public static PythonTuple charmap_encode(CodeContext context, [NotNull]string input, string? errors, [NotNull]EncodingMap map) {
            return DoEncode(context, "charmap", new EncodingMapEncoding(map), input, errors).ToPythonTuple();
        }

        public static PythonTuple charmap_encode(CodeContext context, [NotNull]string input, string? errors = null, IDictionary<object, object>? map = null) {
            if (map != null) {
                return DoEncode(context, "charmap", new CharmapEncoding(map), input, errors).ToPythonTuple();
            } else {
                return latin_1_encode(context, input, errors);
            }
        }

        /// <summary>
        /// Decodes the input string using the provided string mapping.
        /// </summary>
        public static PythonTuple charmap_decode(CodeContext context, [BytesConversion,NotNull]IList<byte> input, string? errors, [NotNull]string map) {
            EncodingMap em = new EncodingMap(map, compileForDecoding: true, compileForEncoding: false);
            return DoDecode(context, "charmap", new EncodingMapEncoding(em), input, errors, input.Count).ToPythonTuple();
        }

        public static PythonTuple charmap_decode(CodeContext context, [BytesConversion,NotNull]IList<byte> input, string? errors = null, IDictionary<object, object>? map = null) {
            if (map != null) {
                return DoDecode(context, "charmap", new CharmapEncoding(map), input, errors, input.Count).ToPythonTuple();
            } else {
                return latin_1_decode(context, input, errors);
            }
        }

#endif
        #endregion

        #region Generic Encoding

        public static object decode(CodeContext/*!*/ context, object? obj, [NotNull, DisallowNull]string? encoding = null, [NotNull]string errors = "strict") {
            if (encoding == null) {
                if (obj is IList<byte> bytesLikeObj) {
                    PythonContext lc = context.LanguageContext;
                    return StringOps.DoDecode(context, bytesLikeObj, errors, lc.GetDefaultEncodingName(), lc.DefaultEncoding);
                } else {
                    throw PythonOps.TypeError("expected bytes-like object, got {0}", PythonTypeOps.GetName(obj));
                }
            } else {
                object? decoder = lookup(context, encoding)[DecoderIndex];
                if (!PythonOps.IsCallable(context, decoder)) {
                    throw PythonOps.TypeError("decoding with '{0}' codec failed; decoder must be callable ('{1}' object is not callable)", encoding, PythonTypeOps.GetName(decoder));
                }
                return PythonOps.GetIndex(context, PythonCalls.Call(context, decoder, obj, errors), 0);
            }
        }

        public static object encode(CodeContext/*!*/ context, object? obj, [NotNull, DisallowNull]string? encoding = null, [NotNull]string errors = "strict") {
            if (encoding == null) {
                if (obj is string str) {
                    PythonContext lc = context.LanguageContext;
                    return StringOps.DoEncode(context, str, errors, lc.GetDefaultEncodingName(), lc.DefaultEncoding, includePreamble: true);
                } else {
                    throw PythonOps.TypeError("expected str, got {0}", PythonTypeOps.GetName(obj));
                }
            } else {
                object? encoder = lookup(context, encoding)[EncoderIndex];
                if (!PythonOps.IsCallable(context, encoder)) {
                    throw PythonOps.TypeError("encoding with '{0}' codec failed; encoder must be callable ('{1}' object is not callable)", encoding, PythonTypeOps.GetName(encoder));
                }
                return PythonOps.GetIndex(context, PythonCalls.Call(context, encoder, obj, errors), 0);
            }
        }

        #endregion

        #region Escape Encoding

        public static PythonTuple escape_decode(CodeContext/*!*/ context, [NotNull]string data, string? errors = null)
            => escape_decode(StringOps.DoEncodeUtf8(context, data), errors);

        public static PythonTuple escape_decode([BytesConversion,NotNull]IList<byte> data, string? errors = null) {
            var span = data.ToArray().AsSpan(); // TODO: remove when signature changed
            var res = LiteralParser.ParseBytes(span, isRaw: false, normalizeLineEndings: false, getErrorHandler(errors));

            return PythonTuple.MakeTuple(Bytes.Make(res.ToArray()), data.Count);

            static LiteralParser.ParseBytesErrorHandler<byte>? getErrorHandler(string? errors) {
                if (errors == null) return default;

                Func<int, IReadOnlyList<byte>>? eh = null;

                return delegate (in ReadOnlySpan<byte> data, int start, int end, string message) {
                    eh ??= errors switch
                    {
                        "strict" => idx => throw PythonOps.ValueError(@"invalid \x escape at position {0}", idx),
                        "replace" => idx => _replacementMarker ??= new[] { (byte)'?' },
                        "ignore" => idx => null,
                        _ => idx => throw PythonOps.ValueError("decoding error; unknown error handling code: " + errors),
                    };
                    return eh(start);
                };
            }
        }
        [DisallowNull]
        private static byte[]? _replacementMarker;

        public static PythonTuple/*!*/ escape_encode([BytesConversion,NotNull]IList<byte> data, string? errors = null) {
            List<byte> buf = new List<byte>(data.Count);
            foreach (byte b in data) {
                switch (b) {
                    case (byte)'\n': buf.Add((byte)'\\'); buf.Add((byte)'n'); break;
                    case (byte)'\r': buf.Add((byte)'\\'); buf.Add((byte)'r'); break;
                    case (byte)'\t': buf.Add((byte)'\\'); buf.Add((byte)'t'); break;
                    case (byte)'\\': buf.Add((byte)'\\'); buf.Add((byte)'\\'); break;
                    case (byte)'\'': buf.Add((byte)'\\'); buf.Add((byte)'\''); break;
                    default:
                        if (b < 0x20 || b >= 0x7f) {
                            buf.AddRange($"\\x{b:x2}".Select(c => unchecked((byte)c)));
                        } else {
                            buf.Add(b);
                        }
                        break;
                }
            }
            return PythonTuple.MakeTuple(Bytes.Make(buf.ToArray()), data.Count);
        }

        #endregion

        #region Latin-1 Functions

        public static PythonTuple latin_1_decode(CodeContext context, [BytesConversion,NotNull]IList<byte> input, string? errors = null)
            => DoDecode(context, "latin-1", Encoding.GetEncoding("iso-8859-1"), input, errors, input.Count).ToPythonTuple();

        public static PythonTuple latin_1_encode(CodeContext context, [NotNull]string input, string? errors = null)
            => DoEncode(context, "latin-1", Encoding.GetEncoding("iso-8859-1"), input, errors).ToPythonTuple();

        #endregion

        #region MBCS Functions
#if FEATURE_ENCODING

        [PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        public static PythonTuple mbcs_decode(CodeContext/*!*/ context, [BytesConversion,NotNull]IList<byte> input, string? errors = null, bool final = false)
            => DoDecode(context, "mbcs", MbcsEncoding, input, errors, input.Count).ToPythonTuple();

        [PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        public static PythonTuple mbcs_encode(CodeContext/*!*/ context, [NotNull]string input, string? errors = null)
            => DoEncode(context, "mbcs", MbcsEncoding, input, errors).ToPythonTuple();

#endif
        #endregion

        #region Code Page Functions
#if FEATURE_ENCODING

        [PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        public static PythonTuple code_page_decode(CodeContext context, int codepage, [BytesConversion, NotNull]IList<byte> input, string? errors = null, bool final = false) {
            // TODO: Use Win32 API MultiByteToWideChar https://docs.microsoft.com/en-us/windows/win32/api/stringapiset/nf-stringapiset-multibytetowidechar
            string encodingName = $"cp{codepage}";
            Encoding encoding = Encoding.GetEncoding(codepage);
            return DoDecode(context, encodingName, encoding, input, errors, input.Count).ToPythonTuple();
        }

        [PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        public static PythonTuple code_page_encode(CodeContext context, int codepage, [NotNull]string input, string? errors = null) {
            // TODO: Use Win32 API WideCharToMultiByte https://docs.microsoft.com/en-us/windows/win32/api/stringapiset/nf-stringapiset-widechartomultibyte
            string encodingName = $"cp{codepage}";
            Encoding encoding = Encoding.GetEncoding(codepage);
            return DoEncode(context, encodingName, encoding, input, errors, includePreamble: true).ToPythonTuple();
        }

#endif
        #endregion

        #region Raw Unicode Escape Encoding Functions

        public static PythonTuple raw_unicode_escape_decode(CodeContext/*!*/ context, [NotNull]string input, string? errors = null) {
            // Encoding with UTF-8 is probably a bug or at least a mistake, as it mutilates non-ASCII characters,
            // but this is what CPython does. Probably encoding with "raw-unicode-escape" would be more reasonable.
            return raw_unicode_escape_decode(context, DoEncode(context, "utf-8", Encoding.UTF8, input, "strict").Item1, errors);
        }

        public static PythonTuple raw_unicode_escape_decode(CodeContext/*!*/ context, [BytesConversion,NotNull]IList<byte> input, string? errors = null) {
            return PythonTuple.MakeTuple(
                StringOps.RawDecode(context, input, "raw-unicode-escape", errors),
                input.Count
            );
        }

        public static PythonTuple raw_unicode_escape_encode(CodeContext/*!*/ context, [NotNull]string input, string? errors = null) {
            return PythonTuple.MakeTuple(
                StringOps.RawEncode(context, input, "raw-unicode-escape", errors),
                input.Length
            );
        }

        #endregion

        #region Unicode Escape Encoding Functions

        public static PythonTuple unicode_escape_decode(CodeContext/*!*/ context, [NotNull]string input, string? errors = null) {
            // Encoding with UTF-8 is probably a bug or at least a mistake, as it mutilates non-ASCII characters,
            // but this is what CPython does. Probably encoding with "unicode-escape" would be more reasonable.
            return unicode_escape_decode(context, DoEncode(context, "utf-8", Encoding.UTF8, input, "strict").Item1, errors);
        }

        public static PythonTuple unicode_escape_decode(CodeContext/*!*/ context, [BytesConversion,NotNull]IList<byte> input, string? errors = null) {
            return PythonTuple.MakeTuple(
                StringOps.RawDecode(context, input, "unicode-escape", errors),
                input.Count
            );
        }

        public static PythonTuple unicode_escape_encode(CodeContext/*!*/ context, [NotNull]string input, string? errors = null) {
            return PythonTuple.MakeTuple(
                StringOps.RawEncode(context, input, "unicode-escape", errors),
                input.Length
            );
        }

        #endregion

        #region Readbuffer Functions

        public static PythonTuple readbuffer_encode(CodeContext/*!*/ context, [NotNull]string input, string? errors = null)
            => readbuffer_encode(DoEncode(context, "utf-8", Encoding.UTF8, input, "strict").Item1, errors);

        public static PythonTuple readbuffer_encode([BytesConversion,NotNull]IList<byte> input, string? errors = null)
            => PythonTuple.MakeTuple(new Bytes(input), input.Count);

        #endregion

        #region Unicode Internal Encoding Functions

        public static PythonTuple unicode_internal_decode(CodeContext context, [NotNull]string input, string? errors = null) {
            PythonOps.Warn(context, PythonExceptions.DeprecationWarning, "unicode_internal codec has been deprecated");
            return PythonTuple.MakeTuple(input, input.Length);
        }

        public static PythonTuple unicode_internal_decode(CodeContext context, [BytesConversion,NotNull]IList<byte> input, string? errors = null) {
            PythonOps.Warn(context, PythonExceptions.DeprecationWarning, "unicode_internal codec has been deprecated");
            return DoDecode(context, "unicode-internal", Encoding.Unicode, input, errors, input.Count).ToPythonTuple();
        }

        public static PythonTuple unicode_internal_encode(CodeContext context, [NotNull]string input, string? errors = null) {
            PythonOps.Warn(context, PythonExceptions.DeprecationWarning, "unicode_internal codec has been deprecated");
            return DoEncode(context, "unicode-internal", Encoding.Unicode, input, errors, false).ToPythonTuple();
        }

        public static PythonTuple unicode_internal_encode(CodeContext context, [BytesConversion,NotNull]IList<byte> input, string? errors = null) {
            PythonOps.Warn(context, PythonExceptions.DeprecationWarning, "unicode_internal codec has been deprecated");
            return PythonTuple.MakeTuple(new Bytes(input), input.Count);
        }

        #endregion

        #region Utf-16 Functions

        public static PythonTuple utf_16_decode(CodeContext context, [BytesConversion,NotNull]IList<byte> input, string? errors = null, bool final = false) {
            PythonTuple res = utf_16_ex_decode(context, input, errors, 0, final);
            return PythonTuple.MakeTuple(res[0], res[1]);
        }

        public static PythonTuple utf_16_encode(CodeContext context, [NotNull]string input, string? errors = null)
            => DoEncode(context, "utf-16", Utf16LeBomEncoding, input, errors, true).ToPythonTuple();

        public static PythonTuple utf_16_ex_decode(CodeContext context, [BytesConversion,NotNull]IList<byte> input, string? errors = null, int byteorder = 0, bool final = false) {

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
        [DisallowNull] private static Encoding? _utf16LeEncoding;

        private static Encoding Utf16LeBomEncoding => Encoding.Unicode; // same as new UnicodeEncoding(bigEndian: false, byteOrderMark: true);

        private static byte[] BOM_UTF16_LE => _bom_utf16_le ??= Utf16LeBomEncoding.GetPreamble();
        [DisallowNull] private static byte[]? _bom_utf16_le;

        public static PythonTuple utf_16_le_decode(CodeContext context, [BytesConversion,NotNull]IList<byte> input, string? errors = null, bool final = false)
            => DoDecode(context, "utf-16-le", Utf16LeEncoding, input, errors, NumEligibleUtf16Bytes(input, final, isLE: true)).ToPythonTuple();

        public static PythonTuple utf_16_le_encode(CodeContext context, [NotNull]string input, string? errors = null)
            => DoEncode(context, "utf-16-le", Utf16LeEncoding, input, errors).ToPythonTuple();

        #endregion

        #region Utf-16-BE Functions

        private static Encoding Utf16BeEncoding => _utf16BeEncoding ??= new UnicodeEncoding(bigEndian: true, byteOrderMark: false);
        [DisallowNull] private static Encoding? _utf16BeEncoding;

        private static Encoding Utf16BeBomEncoding => Encoding.BigEndianUnicode; // same as new UnicodeEncoding(bigEndian: true, byteOrderMark: true);

        private static byte[] BOM_UTF16_BE => _bom_utf16_be ??= Utf16BeBomEncoding.GetPreamble();
        [DisallowNull] private static byte[]? _bom_utf16_be;

        public static PythonTuple utf_16_be_decode(CodeContext context, [BytesConversion,NotNull]IList<byte> input, string? errors = null, bool final = false)
            => DoDecode(context, "utf-16-be", Utf16BeEncoding, input, errors, NumEligibleUtf16Bytes(input, final, isLE: false)).ToPythonTuple();

        public static PythonTuple utf_16_be_encode(CodeContext context, [NotNull]string input, string? errors = null)
            => DoEncode(context, "utf-16-be", Utf16BeEncoding, input, errors).ToPythonTuple();

        #endregion

        #region Utf-7 Functions

#if FEATURE_ENCODING

        public static PythonTuple utf_7_decode(CodeContext context, [BytesConversion,NotNull]IList<byte> input, string? errors = null, bool final = false)
            => DoDecode(context, "utf-7", Encoding.UTF7, input, errors, NumEligibleUtf7Bytes(input, final)).ToPythonTuple();

        public static PythonTuple utf_7_encode(CodeContext context, [NotNull]string input, string? errors = null)
            => DoEncode(context, "utf-7", Encoding.UTF7, input, errors).ToPythonTuple();

        private static int NumEligibleUtf7Bytes(IList<byte> input, bool final) {
            int numBytes = input.Count;
            if (!final) {
                int blockStart = -1;
                for (int i = 0; i < numBytes; i++) {
                    byte b = input[i];
                    if (blockStart < 0 && b == '+') {
                        blockStart = i;
                    } else if (blockStart >= 0 && !b.IsLetter() && !b.IsDigit() && b != '+' && b != '/' && !b.IsWhiteSpace()) {
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
        [DisallowNull] private static Encoding? _utf8Encoding;

        public static PythonTuple utf_8_decode(CodeContext context, [BytesConversion,NotNull]IList<byte> input, string? errors = null, bool final = false)
            => DoDecode(context, "utf-8", Utf8Encoding, input, errors, NumEligibleUtf8Bytes(input, final)).ToPythonTuple();

        public static PythonTuple utf_8_encode(CodeContext context, [NotNull]string input, string? errors = null)
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

        public static PythonTuple utf_32_decode(CodeContext context, [BytesConversion,NotNull]IList<byte> input, string? errors = null, bool final = false) {
            PythonTuple res = utf_32_ex_decode(context, input, errors, 0, final);
            return PythonTuple.MakeTuple(res[0], res[1]);
        }

        public static PythonTuple utf_32_encode(CodeContext context, [NotNull]string input, string? errors = null)
            => DoEncode(context, "utf-32", Utf32LeBomEncoding, input, errors, includePreamble: true).ToPythonTuple();

        public static PythonTuple utf_32_ex_decode(CodeContext context, [BytesConversion,NotNull]IList<byte> input, string? errors = null, int byteorder = 0, bool final = false) {

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
        [DisallowNull] private static Encoding? _utf32LeEncoding;

        private static Encoding Utf32LeBomEncoding => Encoding.UTF32; // same as new UTF32Encoding(bigEndian: false, byteOrderMark: true);

        private static byte[] BOM_UTF32_LE => _bom_utf32_le ??= Utf32LeBomEncoding.GetPreamble();
        [DisallowNull] private static byte[]? _bom_utf32_le;

        public static PythonTuple utf_32_le_decode(CodeContext context, [BytesConversion,NotNull]IList<byte> input, string? errors = null, bool final = false)
            => DoDecode(context, "utf-32-le", Utf32LeEncoding, input, errors, NumEligibleUtf32Bytes(input, final)).ToPythonTuple();

        public static PythonTuple utf_32_le_encode(CodeContext context, [NotNull]string input, string? errors = null)
            => DoEncode(context, "utf-32-le", Utf32LeEncoding, input, errors).ToPythonTuple();

        #endregion

        #region Utf-32-BE Functions

        private static Encoding Utf32BeEncoding => _utf32BeEncoding ??= new UTF32Encoding(bigEndian: true, byteOrderMark: false);
        [DisallowNull] private static Encoding? _utf32BeEncoding;

        private static Encoding Utf32BeBomEncoding => _utf32BeBomEncoding ??= new UTF32Encoding(bigEndian: true, byteOrderMark: true);
        [DisallowNull] private static Encoding? _utf32BeBomEncoding;

        private static byte[] BOM_UTF32_BE => _bom_utf32_be ??= Utf32BeBomEncoding.GetPreamble();
        [DisallowNull] private static byte[]? _bom_utf32_be;

        public static PythonTuple utf_32_be_decode(CodeContext context, [BytesConversion,NotNull]IList<byte> input, string? errors = null, bool final = false)
            => DoDecode(context, "utf-32-be", Utf32BeEncoding, input, errors, NumEligibleUtf32Bytes(input, final)).ToPythonTuple();

        public static PythonTuple utf_32_be_encode(CodeContext context, [NotNull]string input, string? errors = null)
            => DoEncode(context, "utf-32-be", Utf32BeEncoding, input, errors).ToPythonTuple();

        #endregion

#endif

        #region Private implementation

        private static Tuple<string, int> DoDecode(CodeContext context, string encodingName, Encoding encoding, [BytesConversion]IList<byte> input, string? errors, int numBytes) {
            var decoded = StringOps.DoDecode(context, input, errors, encodingName, encoding, numBytes, out int numConsumed);
            return Tuple.Create(decoded, numConsumed);
        }

        private static Tuple<Bytes, int> DoEncode(CodeContext context, string encodingName, Encoding encoding, string input, string? errors, bool includePreamble = false) {
            var res = StringOps.DoEncode(context, input, errors, encodingName, encoding, includePreamble);
            return Tuple.Create(res, input.Length);
        }

        #endregion
    }

#if FEATURE_ENCODING    // Encoding

    /// <summary>
    /// Optimized encoding mapping that can be consumed by charmap_encode/EncodingMapEncoding.
    /// </summary>
    [PythonHidden]
    public class EncodingMap {
        private readonly string _smap;
        [DisallowNull] private Dictionary<byte, int>? _dmap;
        [DisallowNull] private Dictionary<int, byte>? _emap;

        internal EncodingMap(string stringMap, bool compileForDecoding, bool compileForEncoding) {
            _smap = stringMap;
            if (compileForDecoding) CompileDecodingMap();
            if (compileForEncoding) CompileEncodingMap();
        }

        private void CompileEncodingMap() {
            if (_emap == null) {
                _emap = new Dictionary<int, byte>(Math.Min(_smap.Length, 256));
                for (int i = 0, cp = 0; i < _smap.Length && cp < 256; i++, cp++) {
                    if (char.IsHighSurrogate(_smap[i]) && i < _smap.Length - 1 && char.IsLowSurrogate(_smap[i + 1])) {
                        _emap[char.ConvertToUtf32(_smap[i], _smap[i + 1])] = unchecked((byte)cp);
                        i++;
                    } else if (_smap[i] != '\uFFFE') {
                        _emap[_smap[i]] = unchecked((byte)cp);
                    }
                }
            }
        }

        private void CompileDecodingMap() {
            // scan for a surrogate pair
            bool spFound = false;
            for (int i = 0; i < _smap.Length && !spFound; i++) {
                spFound = char.IsHighSurrogate(_smap[i]) && i < _smap.Length - 1 && char.IsLowSurrogate(_smap[i + 1]);
            }
            if (spFound) {
                _dmap = new Dictionary<byte, int>(Math.Min(_smap.Length, 256));
                for (int i = 0, cp = 0; i < _smap.Length && cp < 256; i++, cp++) {
                    if (char.IsHighSurrogate(_smap[i]) && i < _smap.Length - 1 && char.IsLowSurrogate(_smap[i + 1])) {
                        _dmap[unchecked((byte)cp)] = char.ConvertToUtf32(_smap[i], _smap[i + 1]);
                        i++;
                    } else if (_smap[i] != '\uFFFE') {
                        _dmap[unchecked((byte)cp)] = _smap[i];
                    }
                }
            }
        }

        public bool TryGetCharValue(byte b, out int val) {
            if (_dmap != null) {
                return _dmap.TryGetValue(b, out val);
            } else if (b < _smap.Length) {
                val = _smap[b];
                return val != '\uFFFE';
            } else {
                val = '\0';
                return false;
            }
        }

        public bool TryGetByteValue(int c, out byte val) {
            CompileEncodingMap();
            if (_emap != null) {
                return _emap.TryGetValue(c, out val);
            } else {
                val = 0;
                return false;
            }
        }
    }

    internal class EncodingMapEncoding : Encoding {
        private readonly EncodingMap _map;

        public EncodingMapEncoding(EncodingMap map) {
            _map = map;
        }

        public override int GetByteCount(char[] chars, int index, int count)
            => GetBytes(chars, index, count, null, 0);

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[]? bytes, int byteIndex) {
            if (chars == null) throw new ArgumentNullException(nameof(chars));

            int charEnd = charIndex + charCount;
            int byteStart = byteIndex;
            EncoderFallbackBuffer? efb = null;

            while (charIndex < charEnd) {
                int codepoint;
                char c = chars[charIndex];
                int nextIndex = charIndex + 1;
                if (char.IsHighSurrogate(c) && nextIndex < charEnd && char.IsLowSurrogate(chars[nextIndex])) {
                    codepoint = char.ConvertToUtf32(c, chars[nextIndex++]);
                } else {
                    codepoint = c;
                }

                if (!_map.TryGetByteValue(codepoint, out byte val)) {
                    efb ??= EncoderFallback.CreateFallbackBuffer();
                    try {
                        if (efb.Fallback(c, charIndex)) {
                            while (efb.Remaining != 0) {
                                c = efb.GetNextChar();
                                int fbCodepoint = c;
                                if (char.IsHighSurrogate(c) && efb.Remaining != 0) {
                                    char d = efb.GetNextChar();
                                    if (char.IsLowSurrogate(d)) {
                                        fbCodepoint = char.ConvertToUtf32(c, d);
                                    } else {
                                        efb.MovePrevious();
                                    }
                                }
                                if (!_map.TryGetByteValue(fbCodepoint, out val)) {
                                    throw new EncoderFallbackException();  // no recursive fallback
                                }
                                if (bytes != null) bytes[byteIndex] = val;
                                byteIndex++;
                            }
                        }
                    } catch (EncoderFallbackException) {
                        throw PythonOps.UnicodeEncodeError("charmap", new string(chars), charIndex, charIndex + 1, "character maps to <undefined>");
                    }
                } else {
                    if (bytes != null) bytes[byteIndex] = val;
                    byteIndex++;
                }
                charIndex = nextIndex;
            }
            return byteIndex - byteStart;
        }

        public override int GetCharCount(byte[] bytes, int index, int count)
            => GetChars(bytes, index, count, null, 0);

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[]? chars, int charIndex) {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));

            int byteEnd = byteIndex + byteCount;
            int charStart = charIndex;
            DecoderFallbackBuffer? dfb = null;

            while (byteIndex < byteEnd) {
                byte b = bytes[byteIndex];
                if (!_map.TryGetCharValue(b, out int val)) {
                    dfb ??= DecoderFallback.CreateFallbackBuffer();
                    byte[] bytesUnknown = new[] { b };
                    try {
                        if (dfb.Fallback(bytesUnknown, byteIndex)) {
                            while (dfb.Remaining != 0) {
                                char c = dfb.GetNextChar();
                                if (chars != null) {
                                    chars[charIndex] = c;
                                }
                                charIndex++;
                            }
                        }
                    } catch (DecoderFallbackException) {
                        throw PythonOps.UnicodeDecodeError("character maps to <undefined>", bytesUnknown, byteIndex);
                    }
                } else {
                    if (val >= 0x10000) {
                        string s32 = char.ConvertFromUtf32(val);
                        if (chars != null) {
                            chars[charIndex] = s32[0];
                            chars[charIndex + 1] = s32[1];
                        }
                        charIndex += 2;
                    } else {
                        if (chars != null) chars[charIndex] = unchecked((char)val);
                        charIndex++;
                    }
                }
                byteIndex++;
            }
            return charIndex - charStart;
        }

        public override int GetMaxByteCount(int charCount) {
            return charCount;
        }

        public override int GetMaxCharCount(int byteCount) {
            return byteCount * 2;  // account for surrogate pairs
        }
    }

    internal class CharmapEncoding : Encoding {
        private readonly IDictionary<object, object> _map;
        private int _maxEncodingReplacementLength;
        private int _maxDecodingReplacementLength;

        public CharmapEncoding(IDictionary<object, object> map) {
            _map = map;
        }

        public override int GetByteCount(char[] chars, int index, int count)
            => GetBytes(chars, index, count, null, 0);

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[]? bytes, int byteIndex) {
            if (chars == null) throw new ArgumentNullException(nameof(chars));

            int charEnd = charIndex + charCount;
            int byteStart = byteIndex;
            EncoderFallbackBuffer? efb = null;

            while (charIndex < charEnd) {
                object charObj;
                char c = chars[charIndex];
                int nextIndex = charIndex + 1;
                if (char.IsHighSurrogate(c) && nextIndex < charEnd && char.IsLowSurrogate(chars[nextIndex])) {
                    charObj = ScriptingRuntimeHelpers.Int32ToObject(char.ConvertToUtf32(c, chars[nextIndex++]));
                } else {
                    charObj = ScriptingRuntimeHelpers.Int32ToObject(c);
                }

                if (!_map.TryGetValue(charObj, out object? val) || val == null) {
                    efb ??= EncoderFallback.CreateFallbackBuffer();
                    try {
                        for (int idx = charIndex; idx < nextIndex; idx++) {
                            if (efb.Fallback(chars[idx], idx)) {
                                while (efb.Remaining != 0) {
                                    c = efb.GetNextChar();
                                    object fbCharObj = ScriptingRuntimeHelpers.Int32ToObject(c);
                                    if (char.IsHighSurrogate(c) && efb.Remaining != 0) {
                                        char d = efb.GetNextChar();
                                        if (char.IsLowSurrogate(d)) {
                                            fbCharObj = ScriptingRuntimeHelpers.Int32ToObject(char.ConvertToUtf32(c, d));
                                        } else {
                                            efb.MovePrevious();
                                        }
                                    }
                                    if (!_map.TryGetValue(fbCharObj, out val) || val == null) {
                                        throw new EncoderFallbackException();  // no recursive fallback
                                    }
                                    byteIndex += ProcessEncodingReplacementValue(val, bytes, byteIndex);
                                }
                            }

                        }
                    } catch (EncoderFallbackException) {
                        throw PythonOps.UnicodeEncodeError("charmap", new string(chars), charIndex, nextIndex, "character maps to <undefined>");
                    }
                    charIndex = nextIndex;
                } else {
                    byteIndex += ProcessEncodingReplacementValue(val, bytes, byteIndex);
                    charIndex = nextIndex;
                }
            }
            return byteIndex - byteStart;
        }

        private static int ProcessEncodingReplacementValue(object replacement, byte[]? bytes, int byteIndex) {
            Debug.Assert(replacement != null);

            switch (replacement) {
                case IList<byte> b:
                    if (bytes != null) {
                        for (int i = 0; i < b.Count; i++, byteIndex++) {
                            bytes[byteIndex] = b[i];
                        } 
                    }
                    return b.Count;

                case int n:
                    if (n < 0 || n > 0xFF) throw PythonOps.TypeError("character mapping must be in range(256)");
                    if (bytes != null) {
                        bytes[byteIndex] = unchecked((byte)n);
                    }
                    return 1;

                default:
                    throw PythonOps.TypeError("character mapping must return integer, bytes or None, not {0}", PythonTypeOps.GetName(replacement));
            }
        }

        public override int GetCharCount(byte[] bytes, int index, int count)
            => GetChars(bytes, index, count, null, 0);

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[]? chars, int charIndex) {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));

            int byteEnd = byteIndex + byteCount;
            int charStart = charIndex;
            DecoderFallbackBuffer? dfb = null;

            while (byteIndex < byteEnd) {
                byte b = bytes[byteIndex++];
                object byteObj = ScriptingRuntimeHelpers.Int32ToObject(b);

                if (_map.TryGetValue(byteObj, out object? val) && val != null) {
                    if (val is string s) {
                        if (s.Length == 0 || s[0] != '\uFFFE') {
                            for (int i = 0; i < s.Length; i++) {
                                if (chars != null) chars[charIndex] = s[i];
                                charIndex++;
                            }
                            continue;
                        }
                    } else if (val is int n) {
                        if (n < 0 || n > 0x10FFFF) {
                            throw PythonOps.TypeError("character mapping must be in range(0x110000)");
                        } else if (n > 0xFFFF) {
                            var sp = char.ConvertFromUtf32(n);
                            if (chars != null) chars[charIndex] = sp[0];
                            charIndex++;
                            if (chars != null) chars[charIndex] = sp[1];
                            charIndex++;
                            continue;
                        } else if (n != 0xFFFE) {
                            if (chars != null) chars[charIndex] = unchecked((char)n);
                            charIndex++;
                            continue;
                        }
                    } else {
                        throw PythonOps.TypeError("character mapping must return integer, None or str, not {0}", PythonTypeOps.GetName(val));
                    }
                }

                // byte unhandled, try fallback
                dfb ??= DecoderFallback.CreateFallbackBuffer();
                byte[] bytesUnknown = new[] { b };
                try {
                    if (dfb.Fallback(bytesUnknown, byteIndex - 1)) {
                        while (dfb.Remaining != 0) {
                            char c = dfb.GetNextChar();
                            if (chars != null) chars[charIndex] = c;
                            charIndex++;
                        }
                    }
                } catch (DecoderFallbackException) {
                    throw PythonOps.UnicodeDecodeError("character maps to <undefined>", bytesUnknown, byteIndex - 1);
                }
            }
            return charIndex - charStart;
        }

        public override int GetMaxByteCount(int charCount) {
            if (_maxEncodingReplacementLength == 0) {
                _maxEncodingReplacementLength = 1;
                foreach (object val in _map.Values) {
                    if (val is IList<byte> b && b.Count > _maxEncodingReplacementLength) {
                        _maxEncodingReplacementLength = b.Count;
                    }
                }
            }
            return charCount * _maxEncodingReplacementLength;
        }

        public override int GetMaxCharCount(int byteCount) {
            if (_maxDecodingReplacementLength == 0) {
                _maxDecodingReplacementLength = 2; // surrogate pair for codepoint
                foreach (object val in _map.Values) {
                    if (val is string s && s.Length > _maxDecodingReplacementLength) {
                        _maxDecodingReplacementLength = s.Length;
                    };
                }
            }
            return byteCount * _maxDecodingReplacementLength;
        }
    }

#endif
}
