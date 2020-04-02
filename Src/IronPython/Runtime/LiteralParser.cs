// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;

using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime {
    public static class LiteralParser {
        internal delegate IReadOnlyList<char> ParseStringErrorHandler<T>(in ReadOnlySpan<T> data, int start, int end, string reason);

        internal static string ParseString(char[] text, int start, int length, bool isRaw, bool isUniEscape, bool normalizeLineEndings) {
            Debug.Assert(text != null);
            Debug.Assert(start + length <= text.Length);

            if (isRaw && !isUniEscape && !normalizeLineEndings) return new string(text, start, length);

            string result = DoParseString<char>(text.AsSpan(start, length), isRaw, isUniEscape, normalizeLineEndings);

            return result ?? new string(text, start, length);
        }

        internal static string ParseString(byte[] bytes, int start, int length, bool isRaw, ParseStringErrorHandler<byte> errorHandler) {
            Debug.Assert(bytes != null);
            Debug.Assert(start + length <= bytes.Length);

            string result = DoParseString(bytes.AsSpan(start, length), isRaw, isUniEscape: true, normalizeLineEndings: false, errorHandler);

            return result ?? bytes.AsSpan(start, length).MakeString();
        }

        internal static string ParseString(in ReadOnlySpan<byte> bytes, bool isRaw, ParseStringErrorHandler<byte> errorHandler) {
            string result = DoParseString(bytes, isRaw, isUniEscape: true, normalizeLineEndings: false, errorHandler);

            return result ?? bytes.MakeString();
        }

        private static string DoParseString<T>(ReadOnlySpan<T> data, bool isRaw, bool isUniEscape, bool normalizeLineEndings, ParseStringErrorHandler<T> errorHandler = default)
            where T : unmanaged, IConvertible {

            StringBuilder buf = null;
            int i = 0;
            int length = data.Length;
            int val;
            while (i < length) {
                char ch = data[i++].ToChar(null);
                if ((!isRaw || isUniEscape) && ch == '\\') {
                    StringBuilderInit(ref buf, data, i - 1);

                    if (i >= length) {
                        if (isRaw) {
                            buf.Append('\\');
                        } else {
                            handleError(data, i - 1, i, "\\ at end of string");
                        }
                        break;
                    }
                    ch = data[i++].ToChar(null);

                    if ((ch == 'u' || ch == 'U') && isUniEscape) {
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
                    } else {
                        if (isRaw) {
                            buf.Append('\\');
                            buf.Append(ch);
                            continue;
                        }
                        switch (ch) {
                            case 'a': buf.Append('\a'); continue;
                            case 'b': buf.Append('\b'); continue;
                            case 'f': buf.Append('\f'); continue;
                            case 'n': buf.Append('\n'); continue;
                            case 'r': buf.Append('\r'); continue;
                            case 't': buf.Append('\t'); continue;
                            case 'v': buf.Append('\v'); continue;
                            case '\\': buf.Append('\\'); continue;
                            case '\'': buf.Append('\''); continue;
                            case '\"': buf.Append('\"'); continue;
                            case '\n': continue;
                            case '\r':
                                if (!normalizeLineEndings) {
                                    goto default;
                                } else if (i < length && data[i].ToChar(null) == '\n') {
                                    i++;
                                }
                                continue;
                            case 'N': {
                                    IronPython.Modules.unicodedata.PerformModuleReload(null, null);
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
                                        try {
                                            string uval = IronPython.Modules.unicodedata.lookup(namebuf.ToString());
                                            buf.Append(uval);
                                        } catch (KeyNotFoundException) {
                                            handleError(data, i - 4 - namebuf.Length, // 4 for \N{}
                                                        i,
                                                        "unknown Unicode character name");
                                        }
                                    }
                                }
                                continue;
                            case 'x': //hex
                                if (!TryParseInt(data, i, 2, 16, out val, out int consumed)) {
                                    handleError(data, i - 2, i + consumed, @"truncated \xXX escape");
                                } else {
                                    buf.Append((char)val);
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

                                buf.Append((char)val);
                                continue;
                            default:
                                buf.Append("\\");
                                buf.Append(ch);
                                continue;
                        }
                    }
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
                        bytesData = new Bytes(data.ToArray());
                    }
                    throw PythonExceptions.CreateThrowable(PythonExceptions.UnicodeDecodeError, isRaw ? "rawunicodeescape" : "unicodeescape", bytesData, start, end, reason);
                }
                var substitute = errorHandler(data, start, end, reason);
                if (substitute != null) {
                    buf.Append(substitute.ToArray());
                }
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
            int end = text.Length, saveb = b, savestart = start;
            if (start < 0 || start > end) throw new ArgumentOutOfRangeException(nameof(start));
            short sign = 1;

            if (b < 0 || b == 1 || b > 36) {
                throw new ValueErrorException("base must be >= 2 and <= 36");
            }

            ParseIntegerStart(text, ref b, ref start, end, ref sign);

            int ret = 0;
            try {
                int saveStart = start;
                for (; ; ) {
                    int digit;
                    if (start >= end) {
                        if (saveStart == start) {
                            throw new ValueErrorException(string.Format("invalid literal for int() with base {0}: {1}", b, StringOps.__repr__(text)));
                        }
                        break;
                    }
                    if (!HexValue(text[start], out digit)) break;
                    if (!(digit < b)) {
                        if (text[start] == 'l' || text[start] == 'L') {
                            break;
                        }
                        throw new ValueErrorException(string.Format("invalid literal for int() with base {0}: {1}", b, StringOps.__repr__(text)));
                    }

                    checked {
                        // include sign here so that System.Int32.MinValue won't overflow
                        ret = ret * b + sign * digit;
                    }
                    start++;
                }
            } catch (OverflowException) {
                return ParseBigIntegerSign(text, saveb, savestart);
            }

            ParseIntegerEnd(text, start, end);

            return ScriptingRuntimeHelpers.Int32ToObject(ret);
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
            //  Skip whitespace
            while (start < end && Char.IsWhiteSpace(text, start)) start++;

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

        private static void ParseIntegerEnd(string text, int start, int end) {
            //  Skip whitespace
            while (start < end && Char.IsWhiteSpace(text, start)) start++;

            if (start < end) {
                throw new ValueErrorException("invalid integer number literal");
            }
        }

        public static BigInteger ParseBigInteger(string text, int b) {
            Debug.Assert(b != 0);
            BigInteger ret = BigInteger.Zero;
            BigInteger m = BigInteger.One;

            int i = text.Length - 1;
            if (text[i] == 'l' || text[i] == 'L') i -= 1;

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

        public static BigInteger ParseBigIntegerSign(string text, int b, int start = 0) {
            int end = text.Length;
            if (start < 0 || start > end) throw new ArgumentOutOfRangeException(nameof(start));
            short sign = 1;

            if (b < 0 || b == 1 || b > 36) {
                throw new ValueErrorException("base must be >= 2 and <= 36");
            }

            ParseIntegerStart(text, ref b, ref start, end, ref sign);

            BigInteger ret = BigInteger.Zero;
            int saveStart = start;
            for (; ; ) {
                int digit;
                if (start >= end) {
                    if (start == saveStart) {
                        throw new ValueErrorException(string.Format("invalid literal for int() with base {0}: {1}", b, StringOps.__repr__(text)));
                    }
                    break;
                }
                if (!HexValue(text[start], out digit)) break;
                if (!(digit < b)) {
                    if (text[start] == 'l' || text[start] == 'L') {
                        break;
                    }
                    throw new ValueErrorException(string.Format("invalid literal for int() with base {0}: {1}", b, StringOps.__repr__(text)));
                }
                ret = ret * b + digit;
                start++;
            }

            if (start < end && (text[start] == 'l' || text[start] == 'L')) {
                start++;
            }

            ParseIntegerEnd(text, start, end);

            return sign < 0 ? -ret : ret;
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
                return text.lstrip().StartsWith("-") ? Double.NegativeInfinity : Double.PositiveInfinity;
            }
        }

        private static double ParseFloatNoCatch(string text) {
            string s = ReplaceUnicodeDigits(text);
            switch (s.ToLowerAsciiTriggered().lstrip()) {
                case "nan":
                case "+nan":
                case "-nan":
                    return double.NaN;
                case "inf":
                case "+inf":
                    return double.PositiveInfinity;
                case "-inf":
                    return double.NegativeInfinity;
                default:
                    // pass NumberStyles to disallow ,'s in float strings.
                    double res = double.Parse(s, NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
                    return (res == 0.0 && text.lstrip().StartsWith("-")) ? DoubleOps.NegativeZero : res;
            }
        }

        private static string ReplaceUnicodeDigits(string text) {
            StringBuilder replacement = null;
            for (int i = 0; i < text.Length; i++) {
                if (text[i] >= '\x660' && text[i] <= '\x669') {
                    if (replacement == null) replacement = new StringBuilder(text);
                    replacement[i] = (char)(text[i] - '\x660' + '0');
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
            if (String.IsNullOrEmpty(text) || text.IndexOf(' ') != -1) {
                throw ExnMalformed();
            }

            // remove 1 layer of parens
            if (text.StartsWith("(") && text.EndsWith(")")) {
                text = text.Substring(1, text.Length - 2);
            }

            try {
                int len = text.Length;
                string real, imag;

                if (text[len - 1] == 'j') {
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

                    real = text.Substring(0, signPos);
                    imag = text.Substring(signPos, len - signPos - 1);
                    if (imag.Length == 1) {
                        imag += "1"; // convert +/- to +1/-1
                    }
                } else {
                    // 'j' delimits real and imaginary
                    string[] splitText = text.Split(new char[] { 'j' });

                    // no imaginary component
                    if (splitText.Length == 1) {
                        return MathUtils.MakeReal(ParseFloatNoCatch(text));
                    }

                    // there should only be one j
                    if (splitText.Length != 2) {
                        throw ExnMalformed();
                    }
                    real = splitText[1];
                    imag = splitText[0];

                    // a sign must follow the 'j'
                    if (!(real.StartsWith("+") || real.StartsWith("-"))) {
                        throw ExnMalformed();
                    }
                }

                return new Complex(String.IsNullOrEmpty(real) ? 0 : ParseFloatNoCatch(real), ParseFloatNoCatch(imag));
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
