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
                            if (i < length && TryConvertDigit(data[i].ToChar(null), 8, out int onechar)) {
                                val = val * 8 + onechar;
                                i++;
                                if (i < length && TryConvertDigit(data[i].ToChar(null), 8, out onechar)) {
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

#nullable enable

        private static bool TryParseExpression(Parser parser, ReadOnlySpan<char> data, [NotNullWhen(true)] out Expression? expression, [NotNullWhen(false)] out string? error) {
            if (data.TrimStart(" \t\f\r\n".AsSpan()).Length == 0) {
                expression = null;
                error = "f-string: empty expression not allowed";
                return false;
            }

            if (parser.TryParseExpression("(" + data.ToString() + ")", out expression)) {
                error = default;
                return true;
            }

            error = "f-string: invalid syntax";
            return false;
        }

        private static bool TryReadFStringValue(Parser parser, ReadOnlySpan<char> data, out int consumed, [NotNullWhen(true)] out Expression? value, [NotNullWhen(false)] out string? error) {
            consumed = default;
            value = default;
            error = default;

            bool inString = false;
            bool isTriple = false;
            char quote = default;
            string parentheses = string.Empty;

            int i = 0;
            while (i < data.Length) {
                char ch = data[i++];
                if (ch == '\\') {
                    error = "f-string expression part cannot include a backslash";
                    return false;
                }

                // read until to end of string
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
                    // start of string
                    inString = true;
                    quote = ch;
                    if (i + 1 < data.Length && data[i] == ch && data[i + 1] == ch) {
                        i += 2;
                        isTriple = true;
                    }
                } else if (ch == '}' || ch == ']' || ch == ')') {
                    // closing parenthesis
                    if (parentheses.Length == 0) {
                        if (ch == '}') {
                            // parse expression
                            if (TryParseExpression(parser, data.Slice(0, i - 1), out value, out error)) {
                                consumed = i - 1;
                                return true;
                            }
                            return false;
                        } else {
                            error = $"f-string: unmatched '{ch}'";
                            return false;
                        }
                    } else {
                        char opening = parentheses[parentheses.Length - 1];
                        // matching parentheses
                        if (opening == '{' && ch == '}' || opening == '[' && ch == ']' || opening == '(' && ch == ')') {
                            parentheses = parentheses.Substring(0, parentheses.Length - 1);
                        } else {
                            error = $"f-string: closing parenthesis '{ch}' does not match opening parenthesis '{opening}'";
                            return false;
                        }
                    }
                } else if (ch == '{' || ch == '[' || ch == '(') {
                    // opening parenthesis
                    parentheses += ch;
                } else if (ch == '!' && parentheses.Length == 0) {
                    // special case for !=
                    if (i < data.Length && data[i] == '=') {
                        i++;
                        continue;
                    }

                    // parse expression
                    if (TryParseExpression(parser, data.Slice(0, i - 1), out value, out error)) {
                        consumed = i - 1;
                        return true;
                    }
                    return false;
                } else if (ch == ':' && parentheses.Length == 0) {
                    // parse expression
                    if (TryParseExpression(parser, data.Slice(0, i - 1), out value, out error)) {
                        consumed = i - 1;
                        return true;
                    }
                    return false;
                } else if (ch == '#') {
                    error = "f-string expression part cannot include '#'";
                    return false;
                }
            }

            if (inString) {
                error = "f-string: unterminated string";
            } else {
                error = "f-string: expecting '}'";
            }
            return false;
        }

        private static bool TryReadFStringConversion(ReadOnlySpan<char> data, out int consumed, out char conversion, [NotNullWhen(false)] out string? error) {
            consumed = default;
            conversion = default;
            error = default;

            // we must have at least a character and : or }
            if (data.Length == 0) {
                error = "f-string: expecting '}'";
                return false;
            }

            var ch = data[0];
            if (ch == 's' || ch == 'r' || ch == 'a') {
                conversion = ch;

                // no more data
                if (data.Length == 1) {
                    error = "f-string: expecting '}'";
                    return false;
                }

                ch = data[1];
                if (ch == ':' || ch == '}') {
                    consumed = 1;
                    return true;
                } else {
                    error = "f-string: expecting '}'";
                    return false;
                }
            } else {
                error = "f-string: invalid conversion character: expected 's', 'r', or 'a'";
                return false;
            }
        }

        private static bool TryParseFString(Parser parser, ReadOnlySpan<char> data, bool isRaw, int depth, out int consumed, [NotNullWhen(true)] out JoinedStringExpression? joinedStringExpression, [NotNullWhen(false)] out string? error) {
            string str;

            var expressions = new List<Expression>();
            var buf = new StringBuilder(data.Length);

            consumed = default;
            joinedStringExpression = default;
            error = default;

            int i = 0;
            while (i < data.Length) {
                char ch = data[i++];
                if (ch == '{') {
                    if (depth == 0 && i < data.Length && data[i] == '{') {
                        i++;
                        buf.Append(ch);
                        continue;
                    }
                    if (depth == 2) {
                        error = "f-string: expressions nested too deeply";
                        return false;
                    }

                    str = buf.ToString();
                    if (!string.IsNullOrEmpty(str)) {
                        expressions.Add(new ConstantExpression(str));
                        buf.Clear();
                    }

                    if (!TryReadFStringValue(parser, data.Slice(i), out consumed, out Expression? expression, out error)) return false;
                    i += consumed;
                    ch = data[i++];

                    char conversion = default;
                    if (ch == '!') {
                        if (!TryReadFStringConversion(data.Slice(i), out consumed, out conversion, out error)) return false;
                        i += consumed;
                        ch = data[i++];
                    }

                    JoinedStringExpression? formatSpecExpression = default;
                    if (ch == ':') {
                        if (!TryParseFString(parser, data.Slice(i), isRaw, depth: depth + 1, out consumed, out formatSpecExpression, out error)) return false;
                        i += consumed - 1;
                        ch = data[i++];
                    }

                    if (ch != '}') {
                        error = "f-string: expecting '}'";
                        return false;
                    }

                    expressions.Add(new FormattedValueExpression(expression, conversion == default ? null : conversion, formatSpecExpression));
                    continue;
                } else if (ch == '}') {
                    if (depth != 0) {
                        break;
                    }
                    if (i < data.Length && data[i] == '}') {
                        i++;
                        buf!.Append(ch);
                        continue;
                    }
                    error = "f-string: single '}' is not allowed";
                    return false;
                } else if (ch == '\\') {
                    if (isRaw) {
                        buf.Append(ch);
                    } else {
                        HandleEscape(data, ref i, buf, isRaw, isUniEscape: !isRaw, isFormatted: true, normalizeLineEndings: true, handleUnicodeError);
                    }
                } else if (ch == '\r') {
                    // normalize line endings
                    if (i < data.Length && data[i] == '\n') {
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

            consumed = i;
            joinedStringExpression = new JoinedStringExpression(expressions);
            return true;

            void handleUnicodeError(in ReadOnlySpan<char> data, int start, int end, string reason) {
                throw new SyntaxErrorException($"(unicode error) {reason}");
            }
        }

        internal static JoinedStringExpression DoParseFString(this Parser parser, ReadOnlySpan<char> data, bool isRaw) {
            if (TryParseFString(parser, data, isRaw, depth: 0, out int consumed, out JoinedStringExpression? joinedStringExpression, out string? error)) {
                Debug.Assert(consumed == data.Length);
                return joinedStringExpression;
            } else {
                throw new SyntaxErrorException(error);
            }
        }

#nullable restore

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
                                if (i < length && TryConvertDigit(data[i].ToChar(null), 8, out int onechar)) {
                                    val = val * 8 + onechar;
                                    i++;
                                    if (i < length && TryConvertDigit(data[i].ToChar(null), 8, out onechar)) {
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

        private static bool TryConvertDigit(char ch, int b, out int value) {
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
            return value < b;
        }

        private static bool TryParseInt<T>(in ReadOnlySpan<T> text, int start, int length, int b, out int value, out int consumed) where T : IConvertible {
            value = 0;
            for (int i = start, end = start + length; i < end; i++) {
                if (i < text.Length && TryConvertDigit(text[i].ToChar(null), b, out int onechar)) {
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
            if (TryParseInteger(text.AsSpan(), b, false, out object val)) {
                return val;
            }
            throw new ValueErrorException($"invalid literal with base {b}: {text}");
        }

        internal static bool TryParseIntegerSign(ReadOnlySpan<char> text, int b, out object val) {
            if (b < 0 || b == 1 || b > 36) {
                throw new ValueErrorException("int() base must be >= 2 and <= 36, or 0");
            }

            text = text.Trim();

            if (TryParseIntegerStart(text, ref b, out bool isNegative, out int consumed)) {
                text = text.Slice(consumed);
            } else {
                val = default;
                return false;
            }

            Debug.Assert(!text.IsEmpty);

            return TryParseInteger(text, b, isNegative, out val);
        }

        private static bool TryParseInteger(ReadOnlySpan<char> text, int b, bool isNegative, out object val) {
            long ret = 0;
            int underscore = 1;

            for (int i = 0; i < text.Length; i++) {
                var ch = text[i];

                if (ch == '_') {
                    underscore++;
                    if (underscore > 1) {
                        val = default;
                        return false;
                    }
                    continue;
                } else {
                    underscore = 0;
                }

                if (!TryConvertDigit(ch, b, out int digit)) {
                    val = default;
                    return false;
                }

                ret = ret * b + digit;

                if (ret > int.MaxValue) {
                    BigInteger retBi = ret;

                    // Repeated integer multiplication is expensive so use a grouping strategy.
                    // We pick group sizes that ensure our numbers stay in the Int32 range.
                    int groupMax = 5; // zzzzzz (base 36) = 2_176_782_335 > int.MaxValue
                    if (b <= 10) groupMax = 9; // 2_147_483_647

                    int buffer = 0;
                    int cnt = 0;
                    int smallMult = 1;

                    for (i++; i < text.Length; i++) {
                        ch = text[i];

                        if (ch == '_') {
                            underscore++;
                            if (underscore > 1) {
                                val = default;
                                return false;
                            }
                            continue;
                        } else {
                            underscore = 0;
                        }

                        if (!TryConvertDigit(ch, b, out digit)) {
                            val = default;
                            return false;
                        }

                        buffer = buffer * b + digit;
                        cnt++;
                        smallMult *= b;

                        Debug.Assert(smallMult > 0); // no overflows!

                        if (cnt >= groupMax) {
                            retBi = retBi * smallMult + buffer;
                            // reset buffer
                            buffer = 0;
                            cnt = 0;
                            smallMult = 1;
                        }
                    }

                    if (cnt > 0) {
                        retBi = retBi * smallMult + buffer;
                    }

                    if (isNegative) {
                        if (retBi == (BigInteger)int.MaxValue + 1) {
                            val = ScriptingRuntimeHelpers.Int32ToObject(int.MinValue);
                            return true;
                        }
                        val = -retBi;
                        return true;
                    }

                    if (underscore != 0) {
                        val = default;
                        return false;
                    }

                    val = retBi;
                    return true;
                }
            }

            if (underscore != 0) {
                val = default;
                return false;
            }

            int res = unchecked((int)ret);
            res = isNegative ? -res : res;
            val = ScriptingRuntimeHelpers.Int32ToObject(res);
            return true;
        }

        private static bool TryParseIntegerStart(ReadOnlySpan<char> text, ref int b, out bool isNegative, out int consumed) {
            // set defaults
            isNegative = false;
            consumed = 0;

            if (text.IsEmpty) return false;

            var start = 0;
            var end = text.Length;

            // assumes a Trim has already been preformed
            Debug.Assert(!char.IsWhiteSpace(text[start]));

            // sign?
            switch (text[start]) {
                case '-':
                    isNegative = true;
                    if (++start >= end) return false;
                    break;
                case '+':
                    if (++start >= end) return false;
                    break;
            }

            // determine base
            if (b == 0) {
                if (start + 1 < end && text[start] == '0') {
                    char ch = text[++start];
                    switch (ch) {
                        case 'x':
                        case 'X':
                            b = 16;
                            break;
                        case 'o':
                        case 'O':
                            b = 8;
                            break;
                        case 'b':
                        case 'B':
                            b = 2;
                            break;
                        default:
                            b = 1;
                            consumed = start - 1;
                            return true;
                    }
                    if (++start >= end) return false;
                    if (text[start] == '_') {
                        if (++start >= end) return false;
                    }
                } else {
                    b = 10;
                }
            }

            consumed = start;
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
            } catch (FormatException) {
                res = default;
                return false;
            }
            return true;
        }

        public static double ParseFloat(string text) {
            //
            // Strings that end with '\0' is the specific case that CLR libraries allow,
            // however Python doesn't. Since we use CLR floating point number parser,
            // we must check explicitly for the strings that end with '\0'
            //
            if (text != null && text.Length > 0 && text[text.Length - 1] == '\0') {
                throw PythonOps.ValueError("null byte in float literal");
            }
            return ParseFloatNoCatch(text);
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
                    double res;
                    try {
                        res = double.Parse(s, NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
                    } catch (OverflowException) {
                        res = text.lstrip().StartsWith("-", StringComparison.Ordinal) ? double.NegativeInfinity : double.PositiveInfinity;
                    }
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
            string text = s.Trim().ToLowerInvariant();

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
                    return MathUtils.MakeReal(ParseFloat(text));
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
                        return MathUtils.MakeImaginary((len == 1) ? 1 : ParseFloat(text.Substring(0, len - 1)));
                    }

                    string real = text.Substring(0, signPos);
                    string imag = text.Substring(signPos, len - signPos - 1);
                    if (imag.Length == 1) {
                        imag += "1"; // convert +/- to +1/-1
                    }

                    return new Complex(string.IsNullOrEmpty(real) ? 0 : ParseFloat(real), ParseFloat(imag));
                } else {
                    throw ExnMalformed();
                }
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
                return new Complex(0, double.PositiveInfinity);
            }
        }
    }
}
