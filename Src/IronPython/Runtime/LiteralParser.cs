// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;

using IronPython.Compiler;
using IronPython.Compiler.Ast;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime {
    public static class LiteralParser {
        internal delegate IReadOnlyList<char> ParseStringErrorHandler<T>(in ReadOnlySpan<T> data, int start, int end, string reason);

        internal static string ParseString(in ReadOnlySpan<char> text, bool isRaw)
            => DoParseString(text, isRaw, isUniEscape: !isRaw, normalizeLineEndings: true) ?? text.ToString();

        internal static string ParseString(in ReadOnlySpan<byte> bytes, bool isRaw, ParseStringErrorHandler<byte> errorHandler)
            => DoParseString(bytes, isRaw, isUniEscape: true, normalizeLineEndings: false, errorHandler) ?? bytes.MakeString();

#nullable enable

        private static bool TryFetchUnicode(string name, [NotNullWhen(true)] out string? val) {
            Modules.unicodedata.EnsureInitialized();
            try {
                val = Modules.unicodedata.lookup(name);
                return true;
            } catch (KeyNotFoundException) {
                val = default;
                return false;
            }
        }

#nullable restore

        private delegate void HandleUnicodeError<T>(in ReadOnlySpan<T> data, int start, int end, string reason);

        private static void HandleEscape<T>(ReadOnlySpan<T> data, ref int i, StringBuilder buf, bool isRaw, bool isUniEscape, bool isFormatted, bool normalizeLineEndings, HandleUnicodeError<T> handleError) where T : unmanaged, IConvertible {
            var length = data.Length;
            int val;

            if (i >= length) {
                if (isRaw) {
                    buf.Append('\\');
                } else {
                    handleError(data, i - 1, i, "\\ at end of string");
                }
                return;
            }
            char ch = data[i++].ToChar(null);

            if (isUniEscape && (ch == 'u' || ch == 'U')) {
                int len = (ch == 'u') ? 4 : 8;
                int max = 16;
                if (TryParseInt(data, i, len, max, out val, out int consumed)) {
                    if (val < 0 || val > 0x10ffff) {
                        handleError(data, i - 2, i + consumed, isRaw ? @"\Uxxxxxxxx out of range" : "illegal Unicode character");
                    } else if (val < 0x010000) {
                        buf.Append((char)val);
                    } else {
                        buf.Append(char.ConvertFromUtf32(val));
                    }
                } else {
                    handleError(data, i - 2, i + consumed, ch == 'u' ? @"truncated \uXXXX escape" : @"truncated \UXXXXXXXX escape");
                }
                i += consumed;
            } else if (isFormatted && (ch == '{' || ch == '}')) {
                i--;
                buf.Append('\\');
            } else if (isRaw) {
                buf.Append('\\');
                buf.Append(ch);
            } else {
                switch (ch) {
                    case 'a': buf.Append('\a'); break;
                    case 'b': buf.Append('\b'); break;
                    case 'f': buf.Append('\f'); break;
                    case 'n': buf.Append('\n'); break;
                    case 'r': buf.Append('\r'); break;
                    case 't': buf.Append('\t'); break;
                    case 'v': buf.Append('\v'); break;
                    case '\\': buf.Append('\\'); break;
                    case '\'': buf.Append('\''); break;
                    case '\"': buf.Append('\"'); break;
                    case '\n': break;
                    case '\r':
                        if (!normalizeLineEndings) {
                            buf.Append('\\');
                            buf.Append(ch);
                        } else if (i < length && data[i].ToChar(null) == '\n') {
                            i++;
                        }
                        break;
                    case 'N': {
                            StringBuilder namebuf = new StringBuilder();
                            bool namestarted = false;
                            bool namecomplete = false;
                            if (i < length && data[i].ToChar(null) == '{') {
                                namestarted = true;
                                i++;
                                while (i < length) {
                                    char namech = data[i++].ToChar(null);
                                    if (namech != '}') {
                                        namebuf.Append(namech);
                                    } else {
                                        namecomplete = true;
                                        break;
                                    }
                                }
                            }
                            if (!namecomplete || namebuf.Length == 0) {
                                handleError(data, i - 2 - (namestarted ? 1 : 0) - namebuf.Length - (namecomplete ? 1 : 0), // 2 for \N  and 1 for { and 1 for }
                                            i - (namecomplete ? 1 : 0), // 1 for }
                                            @"malformed \N character escape");
                                if (namecomplete) {
                                    buf.Append('}');
                                }
                            } else {
                                if (TryFetchUnicode(namebuf.ToString(), out string uval)) {
                                    buf.Append(uval);
                                } else {
                                    handleError(data, i - 4 - namebuf.Length, // 4 for \N{}
                                                i,
                                                "unknown Unicode character name");
                                }
                            }
                        }
                        break;
                    case 'x': //hex
                        if (!TryParseInt(data, i, 2, 16, out val, out int consumed)) {
                            handleError(data, i - 2, i + consumed, @"truncated \xXX escape");
                        } else {
                            buf.Append((char)val);
                        }
                        i += consumed;
                        break;
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7': {
                            val = ch - '0';
                            if (i < length && HexValue(data[i].ToChar(null), out int onechar) && onechar < 8) {
                                val = val * 8 + onechar;
                                i++;
                                if (i < length && HexValue(data[i].ToChar(null), out onechar) && onechar < 8) {
                                    val = val * 8 + onechar;
                                    i++;
                                }
                            }
                        }

                        buf.Append((char)val);
                        break;
                    default:
                        // PythonOps.Warn(DefaultContext.Default, PythonExceptions.DeprecationWarning, $"invalid escape sequence \\{ch}"); // TODO: enable in 3.6 - currently warning twice???
                        buf.Append('\\');
                        buf.Append(ch);
                        break;
                }
            }
        }

        private static string DoParseString<T>(ReadOnlySpan<T> data, bool isRaw, bool isUniEscape, bool normalizeLineEndings, ParseStringErrorHandler<T> errorHandler = default)
            where T : unmanaged, IConvertible {

            StringBuilder buf = null;
            int i = 0;
            int length = data.Length;
            while (i < length) {
                char ch = data[i++].ToChar(null);
                if ((!isRaw || isUniEscape) && ch == '\\') {
                    StringBuilderInit(ref buf, data, i - 1);
                    HandleEscape(data, ref i, buf, isRaw, isUniEscape, isFormatted: false, normalizeLineEndings, handleError);
                } else if (ch == '\r' && normalizeLineEndings) {
                    StringBuilderInit(ref buf, data, i - 1);

                    // normalize line endings
                    if (i < length && data[i].ToChar(null) == '\n') {
                        i++;
                    }
                    buf.Append('\n');
                } else {
                    buf?.Append(ch);
                }
            }
            return buf?.ToString();

            void handleError(in ReadOnlySpan<T> data, int start, int end, string reason) {
                if (errorHandler == null) {
                    Bytes bytesData = null;
                    if (typeof(T) == typeof(byte)) {
                        bytesData = Bytes.Make(data.ToArray() as byte[]);
                    }
                    throw PythonExceptions.CreateThrowable(PythonExceptions.UnicodeDecodeError, isRaw ? "rawunicodeescape" : "unicodeescape", bytesData, start, end, reason);
                }
                var substitute = errorHandler(data, start, end, reason);
                if (substitute != null) {
                    buf.Append(substitute.ToArray());
                }
            }
        }

        private static bool TryFinishFString(ReadOnlySpan<char> data, out string res, out char? conversion, out string formatSpec, out int consumed) {
            bool inString = false;
            bool isTriple = false;
            char quote = default;
            string parentheses = string.Empty;

            formatSpec = string.Empty;
            consumed = default;
            conversion = default;

            int i = 0;
            while (i < data.Length) {
                char ch = data[i++];
                if (ch == '\\') {
                    res = "f-string expression part cannot include a backslash";
                    return false;
                }
                if (inString) {
                    if (ch == quote) {
                        if (isTriple) {
                            if (i + 1 < data.Length && data[i] == ch && data[i + 1] == ch) {
                                i += 2;
                                inString = false;
                                isTriple = false;
                                quote = default;
                            }
                        } else {
                            inString = false;
                            isTriple = false;
                            quote = default;
                        }
                    }
                    continue;
                }

                if (ch == '\'' || ch == '"') {
                    inString = true;
                    quote = ch;
                    if (i + 1 < data.Length && data[i] == ch && data[i + 1] == ch) {
                        i += 2;
                        isTriple = true;
                    }
                } else if (ch == '}' || ch == ']' || ch == ')') {
                    if (parentheses.Length == 0) {
                        if (ch == '}') {
                            res = data.Slice(0, i - 1).Trim().ToString();
                            if (string.IsNullOrEmpty(res)) {
                                res = "f-string: empty expression not allowed";
                                return false;
                            }

                            consumed = i;
                            return true;
                        } else {
                            res = $"f-string: unmatched '{ch}'";
                            return false;
                        }
                    } else {
                        char opening = parentheses[parentheses.Length - 1];
                        if (opening == '{' && ch == '}' || opening == '[' && ch == ']' || opening == '(' && ch == ')') {
                            parentheses = parentheses.Substring(0, parentheses.Length - 1);
                        } else {
                            res = $"f-string: closing parenthesis '{ch}' does not match opening parenthesis '{opening}'";
                            return false;
                        }
                    }
                } else if (ch == '{' || ch == '[' || ch == '(') {
                    parentheses += ch;
                } else if (ch == '!' && parentheses.Length == 0) {
                    if (i == data.Length) {
                        break; // f-string: expecting '}'
                    }
                    res = data.Slice(0, i - 1).Trim().ToString();
                    if (string.IsNullOrEmpty(res)) {
                        res = "f-string: empty expression not allowed";
                        return false;
                    }

                    ch = data[i++];
                    if (ch == 's' || ch == 'r' || ch == 'a') {
                        conversion = ch;

                        if (i == data.Length) {
                            break; // f-string: expecting '}'
                        }
                        ch = data[i++];
                        if (ch == ':') {
                            var end = data.Slice(i).IndexOf('}');
                            if (end != -1) {
                                formatSpec = data.Slice(i, end).ToString();
                                consumed = i + end + 1;
                                return true;
                            }
                            break; // f-string: expecting '}'
                        } else if (ch == '}') {
                            consumed = i;
                            return true;
                        }
                    } else {
                        res = "f-string: invalid conversion character: expected 's', 'r', or 'a'";
                        return false;
                    }
                } else if (ch == ':' && parentheses.Length == 0) {
                    res = data.Slice(0, i - 1).Trim().ToString();
                    if (string.IsNullOrEmpty(res)) {
                        res = "f-string: empty expression not allowed";
                        return false;
                    }

                    var end = data.Slice(i).IndexOf('}');
                    if (end != -1) {
                        formatSpec = data.Slice(i, end).ToString();
                        consumed = i + end + 1;
                        return true;
                    }
                    break; // f-string: expecting '}'
                } else if (ch == '#') {
                    res = "f-string expression part cannot include '#'";
                    return false;
                }
            }

            if (inString) {
                res = "f-string: unterminated string";
            } else {
                res = "f-string: expecting '}'";
            }
            return false;
        }

        internal static List<Expression> DoParseFString(ReadOnlySpan<char> data, bool isRaw, bool isUniEscape, bool normalizeLineEndings, bool isFormatted, Parser parser) {
            int length = data.Length;
            string str;

            var expressions = new List<Expression>();
            StringBuilder buf = new StringBuilder(data.Length);

            int i = 0;
            while (i < length) {
                char ch = data[i++];
                if (isFormatted) {
                    if (ch == '{') {
                        if (i >= length) {
                            handleSyntaxError("f-string: expecting '}'");
                            break;
                        }
                        ch = data[i++];
                        if (ch == '{') {
                            buf!.Append(ch);
                            continue;
                        } else {
                            i--;
                            if (TryFinishFString(data.Slice(i), out string res, out char? conversion, out string formatSpec, out int consumed)) {
                                str = buf.ToString();
                                if (!string.IsNullOrEmpty(str)) {
                                    expressions.Add(new ConstantExpression(str));
                                    buf.Clear();
                                }
                                expressions.Add(new FormattedValueExpression(parser.ParseFString(res), conversion, formatSpec));
                                i += consumed;
                                continue;
                            } else {
                                handleSyntaxError(res);
                                break;
                            }
                        }
                    } else if (ch == '}') {
                        if (i >= length) {
                            handleSyntaxError("f-string: single '}' is not allowed");
                            break;
                        }
                        ch = data[i++];
                        if (ch == '}') {
                            buf!.Append(ch);
                            continue;
                        } else {
                            handleSyntaxError("f-string: single '}' is not allowed");
                            break;
                        }
                    }
                }
                if ((!isRaw || isUniEscape) && ch == '\\') {
                    StringBuilderInit(ref buf, data, i - 1);
                    HandleEscape(data, ref i, buf, isRaw, isUniEscape, isFormatted: isFormatted, normalizeLineEndings, handleUnicodeError);
                } else if (ch == '\r' && normalizeLineEndings) {
                    StringBuilderInit(ref buf, data, i - 1);

                    // normalize line endings
                    if (i < length && data[i] == '\n') {
                        i++;
                    }
                    buf.Append('\n');
                } else {
                    buf.Append(ch);
                }
            }

            str = buf.ToString();
            if (!string.IsNullOrEmpty(str)) {
                expressions.Add(new ConstantExpression(str));
            }

            return expressions;

            void handleSyntaxError(string reason) {
                throw new SyntaxErrorException(reason);
            }

            void handleUnicodeError(in ReadOnlySpan<char> data, int start, int end, string reason) {
                throw PythonExceptions.CreateThrowable(PythonExceptions.UnicodeDecodeError, isRaw ? "rawunicodeescape" : "unicodeescape", null, start, end, reason);
            }
        }

        private static void StringBuilderInit<T>(ref StringBuilder sb, in ReadOnlySpan<T> data, int toCopy) where T : unmanaged, IConvertible {
            Debug.Assert(toCopy <= data.Length);

            if (sb != null) return;

            sb = new StringBuilder(data.Length);
            unsafe {
                if (sizeof(T) == sizeof(char)) {
                    fixed (T* cp = data) {
                        sb.Append((char*)cp, toCopy);
                    }
                    return;
                }
            }

            // T is not char
            for (int i = 0; i < toCopy; i++) {
                sb.Append(data[i].ToChar(null));
            }
        }

        internal delegate IReadOnlyList<byte> ParseBytesErrorHandler<T>(in ReadOnlySpan<T> data, int start, int end, string reason);

        internal static List<byte> ParseBytes<T>(ReadOnlySpan<T> data, bool isRaw, bool isAscii, bool normalizeLineEndings, ParseBytesErrorHandler<T> errorHandler = default) where T : IConvertible {
            List<byte> buf = new List<byte>(data.Length);
            int i = 0;
            int length = data.Length;
            int val;
            while (i < length) {
                char ch = data[i++].ToChar(null);
                if (!isRaw && ch == '\\') {
                    if (i >= length) {
                        throw PythonOps.ValueError("Trailing \\ in string");
                    }
                    ch = data[i++].ToChar(null);
                    switch (ch) {
                        case 'a': buf.Add((byte)'\a'); continue;
                        case 'b': buf.Add((byte)'\b'); continue;
                        case 'f': buf.Add((byte)'\f'); continue;
                        case 'n': buf.Add((byte)'\n'); continue;
                        case 'r': buf.Add((byte)'\r'); continue;
                        case 't': buf.Add((byte)'\t'); continue;
                        case 'v': buf.Add((byte)'\v'); continue;
                        case '\\': buf.Add((byte)'\\'); continue;
                        case '\'': buf.Add((byte)'\''); continue;
                        case '\"': buf.Add((byte)'\"'); continue;
                        case '\n': continue;
                        case '\r':
                            if (!normalizeLineEndings) {
                                goto default;
                            } else if (i < length && data[i].ToChar(null) == '\n') {
                                i++;
                            }
                            continue;
                        case 'x': //hex
                            if (!TryParseInt(data, i, 2, 16, out val, out int consumed)) {
                                int pos = i - 2;
                                string message = $"invalid \\x escape at position {pos}";
                                if (errorHandler == null) {
                                    throw PythonOps.ValueError(message);
                                }
                                var substitute = errorHandler(data, pos, pos + consumed, message);
                                if (substitute != null) {
                                    buf.AddRange(substitute);
                                }
                            } else {
                                buf.Add((byte)val);
                            }
                            i += consumed;
                            continue;
                        case '0':
                        case '1':
                        case '2':
                        case '3':
                        case '4':
                        case '5':
                        case '6':
                        case '7': {
                                val = ch - '0';
                                if (i < length && HexValue(data[i].ToChar(null), out int onechar) && onechar < 8) {
                                    val = val * 8 + onechar;
                                    i++;
                                    if (i < length && HexValue(data[i].ToChar(null), out onechar) && onechar < 8) {
                                        val = val * 8 + onechar;
                                        i++;
                                    }
                                }
                            }

                            buf.Add((byte)val);
                            continue;
                        default:
                            if (isAscii && ch >= 0x80) {
                                throw PythonOps.SyntaxError("bytes can only contain ASCII literal characters.");
                            }
                            buf.Add((byte)'\\');
                            buf.Add((byte)ch);
                            continue;
                    }
                } else if (ch == '\r' && normalizeLineEndings) {
                    // normalize line endings
                    if (i < length && data[i].ToChar(null) == '\n') {
                        i++;
                    }
                    buf.Add((byte)'\n');
                } else if (isAscii && ch >= 0x80) {
                    throw PythonOps.SyntaxError("bytes can only contain ASCII literal characters.");
                } else {
                    buf.Add((byte)ch);
                }
            }

            return buf;
        }

        private static bool HexValue(char ch, out int value) {
            switch (ch) {
                case '0':
                case '\x660': value = 0; break;
                case '1':
                case '\x661': value = 1; break;
                case '2':
                case '\x662': value = 2; break;
                case '3':
                case '\x663': value = 3; break;
                case '4':
                case '\x664': value = 4; break;
                case '5':
                case '\x665': value = 5; break;
                case '6':
                case '\x666': value = 6; break;
                case '7':
                case '\x667': value = 7; break;
                case '8':
                case '\x668': value = 8; break;
                case '9':
                case '\x669': value = 9; break;
                default:
                    if (ch >= 'a' && ch <= 'z') {
                        value = ch - 'a' + 10;
                    } else if (ch >= 'A' && ch <= 'Z') {
                        value = ch - 'A' + 10;
                    } else {
                        value = -1;
                        return false;
                    }
                    break;
            }
            return true;
        }

        private static int HexValue(char ch) {
            int value;
            if (!HexValue(ch, out value)) {
                throw new ValueErrorException("bad char for integer value: " + ch);
            }
            return value;
        }

        private static int CharValue(char ch, int b) {
            int val = HexValue(ch);
            if (val >= b) {
                throw new ValueErrorException(String.Format("bad char for the integer value: '{0}' (base {1})", ch, b));
            }
            return val;
        }

        private static bool ParseInt(string text, int b, out int ret) {
            ret = 0;
            long m = 1;
            for (int i = text.Length - 1; i >= 0; i--) {
                // avoid the exception here.  Not only is throwing it expensive,
                // but loading the resources for it is also expensive 
                long lret = ret + m * CharValue(text[i], b);
                if (Int32.MinValue <= lret && lret <= Int32.MaxValue) {
                    ret = (int)lret;
                } else {
                    return false;
                }

                m *= b;
                if (Int32.MinValue > m || m > Int32.MaxValue) {
                    return false;
                }
            }
            return true;
        }

        private static bool TryParseInt<T>(in ReadOnlySpan<T> text, int start, int length, int b, out int value, out int consumed) where T : IConvertible {
            value = 0;
            for (int i = start, end = start + length; i < end; i++) {
                if (i < text.Length && HexValue(text[i].ToChar(null), out int onechar) && onechar < b) {
                    value = value * b + onechar;
                } else {
                    consumed = i - start;
                    return false;
                }
            }
            consumed = length;
            return true;
        }

        public static object ParseInteger(string text, int b) {
            Debug.Assert(b != 0);
            int iret;
            if (!ParseInt(text, b, out iret)) {
                BigInteger ret = ParseBigInteger(text, b);
                if (!ret.AsInt32(out iret)) {
                    return ret;
                }
            }
            return ScriptingRuntimeHelpers.Int32ToObject(iret);
        }

        public static object ParseIntegerSign(string text, int b, int start = 0) {
            if (TryParseIntegerSign(text, b, start, out object val))
                return val;

            throw new ValueErrorException(string.Format("invalid literal for int() with base {0}: {1}", b, StringOps.__repr__(text)));
        }

        internal static bool TryParseIntegerSign(string text, int b, int start, out object val) {
            int end = text.Length, saveb = b, savestart = start;
            if (start < 0 || start > end) throw new ArgumentOutOfRangeException(nameof(start));
            short sign = 1;

            if (b < 0 || b == 1 || b > 36) {
                throw new ValueErrorException("int() base must be >= 2 and <= 36, or 0");
            }

            ParseIntegerStart(text, ref b, ref start, end, ref sign);

            if (start < end && char.IsWhiteSpace(text, start)) {
                val = default;
                return false;
            }

            int ret = 0;
            try {
                int saveStart = start;
                for (; ; ) {
                    int digit;
                    if (start >= end) {
                        if (saveStart == start) {
                            val = default;
                            return false;
                        }
                        break;
                    }
                    if (!HexValue(text[start], out digit)) break;
                    if (!(digit < b)) {
                        val = default;
                        return false;
                    }

                    checked {
                        // include sign here so that System.Int32.MinValue won't overflow
                        ret = ret * b + sign * digit;
                    }
                    start++;
                }
            } catch (OverflowException) {
                if (TryParseBigIntegerSign(text, saveb, savestart, out var bi)) {
                    val = bi;
                    return true;
                }
                val = default;
                return false;
            }

            ParseIntegerEnd(text, ref start, ref end);

            if (start < end) {
                val = default;
                return false;
            }

            val = ScriptingRuntimeHelpers.Int32ToObject(ret);
            return true;
        }

        private static void ParseIntegerStart(string text, ref int b, ref int start, int end, ref short sign) {
            //  Skip whitespace
            while (start < end && Char.IsWhiteSpace(text, start)) start++;
            //  Sign?
            if (start < end) {
                switch (text[start]) {
                    case '-':
                        sign = -1;
                        goto case '+';
                    case '+':
                        start++;
                        break;
                }
            }

            //  Determine base
            if (b == 0) {
                if (start < end && text[start] == '0') {
                    // Hex, oct, or bin
                    if (++start < end) {
                        switch(text[start]) {
                            case 'x':
                            case 'X':
                                start++;
                                b = 16;
                                break;
                            case 'o':
                            case 'O':
                                b = 8;
                                start++;
                                break;
                            case 'b':
                            case 'B':
                                start++;
                                b = 2;
                                break;
                        }
                    }

                    if (b == 0) {
                        // Keep the leading zero
                        start--;
                        b = 8;
                    }
                } else {
                    b = 10;
                }
            }
        }

        private static void ParseIntegerEnd(string text, ref int start, ref int end) {
            //  Skip whitespace
            while (start < end && char.IsWhiteSpace(text, start)) start++;
        }

        internal static BigInteger ParseBigInteger(string text, int b) {
            Debug.Assert(b != 0);
            BigInteger ret = BigInteger.Zero;
            BigInteger m = BigInteger.One;

            int i = text.Length - 1;

            int groupMax = 7;
            if (b <= 10) groupMax = 9;// 2 147 483 647

            while (i >= 0) {
                // extract digits in a batch
                int smallMultiplier = 1;
                uint uval = 0;

                for (int j = 0; j < groupMax && i >= 0; j++) {
                    uval = (uint)(CharValue(text[i--], b) * smallMultiplier + uval);
                    smallMultiplier *= b;
                }

                // this is more generous than needed
                ret += m * (BigInteger)uval;
                if (i >= 0) m = m * (smallMultiplier);
            }

            return ret;
        }

        internal static BigInteger ParseBigIntegerSign(string text, int b, int start = 0) {
            if (TryParseBigIntegerSign(text, b, start, out var val))
                return val;

            throw new ValueErrorException(string.Format("invalid literal for int() with base {0}: {1}", b, StringOps.__repr__(text)));
        }

        private static bool TryParseBigIntegerSign(string text, int b, int start, out BigInteger val) {
            int end = text.Length;
            if (start < 0 || start > end) throw new ArgumentOutOfRangeException(nameof(start));
            short sign = 1;

            if (b < 0 || b == 1 || b > 36) {
                throw new ValueErrorException("int() base must be >= 2 and <= 36, or 0");
            }

            ParseIntegerStart(text, ref b, ref start, end, ref sign);

            if (start < end && char.IsWhiteSpace(text, start)) {
                val = default;
                return false;
            }

            BigInteger ret = BigInteger.Zero;
            int saveStart = start;
            for (; ; ) {
                int digit;
                if (start >= end) {
                    if (start == saveStart) {
                        val = default;
                        return false;
                    }
                    break;
                }
                if (!HexValue(text[start], out digit)) break;
                if (!(digit < b)) {
                    val = default;
                    return false;
                }
                ret = ret * b + digit;
                start++;
            }

            ParseIntegerEnd(text, ref start, ref end);

            if (start < end) {
                val = default;
                return false;
            }

            val = sign < 0 ? -ret : ret;
            return true;
        }

        internal static bool TryParseFloat(string text, out double res, bool replaceUnicode) {
            try {
                //
                // Strings that end with '\0' is the specific case that CLR libraries allow,
                // however Python doesn't. Since we use CLR floating point number parser,
                // we must check explicitly for the strings that end with '\0'
                //
                if (text != null && text.Length > 0 && text[text.Length - 1] == '\0') {
                    res = default;
                    return false;
                }
                res = ParseFloatNoCatch(text, replaceUnicode: replaceUnicode);
            } catch (OverflowException) {
                res = text.lstrip().StartsWith("-", StringComparison.Ordinal) ? Double.NegativeInfinity : Double.PositiveInfinity;
            } catch(FormatException) {
                res = default;
                return false;
            }
            return true;
        }

        public static double ParseFloat(string text) {
            try {
                //
                // Strings that end with '\0' is the specific case that CLR libraries allow,
                // however Python doesn't. Since we use CLR floating point number parser,
                // we must check explicitly for the strings that end with '\0'
                //
                if (text != null && text.Length > 0 && text[text.Length - 1] == '\0') {
                    throw PythonOps.ValueError("null byte in float literal");
                }
                return ParseFloatNoCatch(text);
            } catch (OverflowException) {
                return text.lstrip().StartsWith("-", StringComparison.Ordinal) ? Double.NegativeInfinity : Double.PositiveInfinity;
            }
        }

        private static double ParseFloatNoCatch(string text, bool replaceUnicode = true) {
            string s = replaceUnicode ? ReplaceUnicodeCharacters(text) : text;
            switch (s.ToLowerAsciiTriggered().lstrip()) {
                case "nan":
                case "+nan":
                case "-nan":
                    return double.NaN;
                case "inf":
                case "+inf":
                case "infinity":
                case "+infinity":
                    return double.PositiveInfinity;
                case "-inf":
                case "-infinity":
                    return double.NegativeInfinity;
                default:
                    // pass NumberStyles to disallow ,'s in float strings.
                    double res = double.Parse(s, NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
                    return (res == 0.0 && text.lstrip().StartsWith("-", StringComparison.Ordinal)) ? DoubleOps.NegativeZero : res;
            }
        }

        private static string ReplaceUnicodeCharacters(string text) {
            StringBuilder replacement = null;
            for (int i = 0; i < text.Length; i++) {
                char ch = text[i];
                if (ch >= '\x660' && ch <= '\x669') {
                    // replace unicode digits
                    if (replacement == null) replacement = new StringBuilder(text);
                    replacement[i] = (char)(ch - '\x660' + '0');
                } else if (ch >= '\x80' && char.IsWhiteSpace(ch)) {
                    // replace unicode whitespace
                    if (replacement == null) replacement = new StringBuilder(text);
                    replacement[i] = ' ';
                }
            }
            if (replacement != null) {
                text = replacement.ToString();
            }
            return text;
        }

        // ParseComplex helpers
        private static char[] signs = new char[] { '+', '-' };
        private static Exception ExnMalformed() {
            return PythonOps.ValueError("complex() arg is a malformed string");
        }

        public static Complex ParseComplex(string s) {
            // remove no-meaning spaces and convert to lowercase
            string text = s.Trim().ToLower();

            // remove 1 layer of parens
            if (text.StartsWith("(", StringComparison.Ordinal) && text.EndsWith(")", StringComparison.Ordinal)) {
                text = text.Substring(1, text.Length - 2);
            }

            text = text.Trim();

            if (string.IsNullOrEmpty(text) || text.IndexOf(' ') != -1) {
                throw ExnMalformed();
            }

            try {
                int len = text.Length;
                var idx = text.IndexOf('j');
                if (idx == -1) {
                    return MathUtils.MakeReal(ParseFloatNoCatch(text));
                } else if (idx == len - 1) {
                    // last sign delimits real and imaginary...
                    int signPos = text.LastIndexOfAny(signs);
                    // ... unless it's after 'e', so we bypass up to 2 of those here
                    for (int i = 0; signPos > 0 && text[signPos - 1] == 'e'; i++) {
                        if (i == 2) {
                            // too many 'e's
                            throw ExnMalformed();
                        }
                        signPos = text.Substring(0, signPos - 1).LastIndexOfAny(signs);
                    }

                    // no real component
                    if (signPos < 0) {
                        return MathUtils.MakeImaginary((len == 1) ? 1 : ParseFloatNoCatch(text.Substring(0, len - 1)));
                    }

                    string real = text.Substring(0, signPos);
                    string imag = text.Substring(signPos, len - signPos - 1);
                    if (imag.Length == 1) {
                        imag += "1"; // convert +/- to +1/-1
                    }

                    return new Complex(String.IsNullOrEmpty(real) ? 0 : ParseFloatNoCatch(real), ParseFloatNoCatch(imag));
                } else {
                    throw ExnMalformed();
                }
            } catch (OverflowException) {
                throw PythonOps.ValueError("complex() literal too large to convert");
            } catch {
                throw ExnMalformed();
            }
        }

        public static Complex ParseImaginary(string text) {
            try {
                return MathUtils.MakeImaginary(double.Parse(
                    text.Substring(0, text.Length - 1),
                    System.Globalization.CultureInfo.InvariantCulture.NumberFormat
                    ));
            } catch (OverflowException) {
                return new Complex(0, Double.PositiveInfinity);
            }
        }
    }
}
