// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Operations;

namespace IronPython.Runtime {
    /// <summary>
    /// Provides a representation and parsing for the default formatting specification.  This is used
    /// by object.__format__, int.__format__, long.__format__, and float.__format__ to do the common
    /// format spec parsing.
    /// 
    /// The default specification is:
    /// 
    /// format_spec =  [[fill]align][sign][#][0][width][,][.precision][type]
    /// fill        =  a character other than }
    /// align       =  "&lt;" | "&gt;" | "=" | "^"
    /// sign        =  "+" | "-" | " "
    /// width       =  integer
    /// precision   =  integer
    /// type        =  "b" | "c" | "d" | "e" | "E" | "f" | "F" | "g" | "G" | "n" | "o" | "x" | "X" | "%"
    /// </summary>
    internal class StringFormatSpec {
        internal readonly char? Fill, Alignment, Sign, Type;
        internal readonly int? Width, Precision;
        internal readonly bool AlternateForm, ThousandsComma;
        internal readonly bool IsEmpty;
        internal readonly bool AlignmentIsZeroPad;

        private StringFormatSpec(char? fill, char? alignment, char? sign, int? width, bool thousandsComma, int? precision, char? type, bool alternateForm, bool isEmpty, bool alignmentIsZeroPad) {
            Fill = fill;
            Alignment = alignment;
            Sign = sign;
            Width = width;
            ThousandsComma = thousandsComma;
            Precision = precision;
            Type = type;
            AlternateForm = alternateForm;
            IsEmpty = isEmpty;
            AlignmentIsZeroPad = alignmentIsZeroPad;
        }

        internal string TypeRepr => (0x20 < Type && Type < 0x80) ? Type.ToString() : $"\\x{(int)Type:x}";

        /// <summary>
        /// Parses a format spec and returns a new StringFormatSpec object.
        /// </summary>
        internal static StringFormatSpec/*!*/ FromString(string/*!*/ formatSpec) {
            Assert.NotNull(formatSpec);

            char? fill = null, sign = null, align = null, type = null;
            int? width = null, precision = null;
            bool alternateForm = false, thousandsComma = false;
            bool isEmpty = formatSpec.Length == 0;
            bool alignmentIsZeroPad = false;

            int curOffset = 0;
            if (formatSpec.Length >= 2) {
                // check 2nd character for alignment first, then 1st character
                switch (formatSpec[1]) {
                    case '<':
                    case '>':
                    case '=':
                    case '^':
                        fill = formatSpec[0];
                        align = formatSpec[1];
                        curOffset = 2;
                        break;
                    default:
                        switch (formatSpec[0]) {
                            case '<':
                            case '>':
                            case '=':
                            case '^':
                                align = formatSpec[0];
                                curOffset = 1;
                                break;
                        }
                        break;
                }
            }

            // read sign
            if (curOffset != formatSpec.Length &&
                (formatSpec[curOffset] == '+' ||
                 formatSpec[curOffset] == '-' ||
                 formatSpec[curOffset] == ' ')) {
                sign = formatSpec[curOffset++];
            }

            // include 0b, 0x, etc...
            if (curOffset != formatSpec.Length &&
                formatSpec[curOffset] == '#') {
                alternateForm = true;
                curOffset++;
            }

            // zero padding
            if (curOffset != formatSpec.Length &&
                formatSpec[curOffset] == '0') {
                if (align == null) {
                    align = '=';
                }
                if (fill == null) {
                    fill = '0';
                    alignmentIsZeroPad = true;
                }
                curOffset++;
            }

            // read width
            if (curOffset != formatSpec.Length &&
                char.IsDigit(formatSpec[curOffset])) {
                width = ParseInt(formatSpec, ref curOffset);
            }

            // read optional comma
            if (curOffset != formatSpec.Length &&
                formatSpec[curOffset] == ',') {
                curOffset++;
                thousandsComma = true;
            }

            // read precision
            if (curOffset != formatSpec.Length &&
                formatSpec[curOffset] == '.') {
                curOffset++;

                if (curOffset == formatSpec.Length ||
                    !char.IsDigit(formatSpec[curOffset])) {
                    throw PythonOps.ValueError("Format specifier missing precision");
                }

                try {
                    precision = ParseInt(formatSpec, ref curOffset);
                } catch (OverflowException) {
                    throw PythonOps.ValueError("Too many decimal digits in format string");
                }
            }

            // read type
            if (curOffset != formatSpec.Length) {
                type = formatSpec[curOffset++];

                if (thousandsComma) {
                    switch (type) {
                        case 'b':
                        case 'c':
                        case 'n':
                        case 'o':
                        case 'x':
                        case 'X':
                            throw PythonOps.ValueError("Cannot specify ',' with '{0}'", type);
                    }
                }
            }

            return new StringFormatSpec(
                fill,
                align,
                sign,
                width,
                thousandsComma,
                precision,
                type,
                alternateForm,
                isEmpty,
                alignmentIsZeroPad
            );
        }

        internal string/*!*/ AlignText(string/*!*/ text) {
            Assert.NotNull(text);

            if (Width != null) {
                int width = Width.Value;

                if (text.Length < width) {
                    char fill = Fill ?? ' ';
                    int headerLen = 0, footerLen = 0;
                    int fillLength = width - text.Length;

                    switch (Alignment) {
                        case null:
                        case '<': footerLen = fillLength; break;
                        case '=':
                        case '>': headerLen = fillLength; break;
                        case '^':
                            headerLen = footerLen = fillLength / 2;
                            if ((fillLength & 0x01) != 0) {
                                footerLen++;
                            }
                            break;
                    }

                    if (headerLen != 0) {
                        text = new string(fill, headerLen) + text;
                    }

                    if (footerLen != 0) {
                        text = text + new string(fill, footerLen);
                    }
                }
            }
            return text;
        }

        internal string/*!*/ AlignNumericText(string/*!*/ text, bool isZero, bool isPos) {
            Assert.NotNull(text);

            char? sign = GetSign(isZero, isPos);
            string type = GetTypeString();

            if (Width != null) {
                int width = Width.Value;

                if (text.Length < width) {
                    char fill = Fill ?? ' ';
                    int headerLen = 0, footerLen = 0;
                    int fillLength = width - text.Length;

                    if (sign != null) {
                        fillLength--;
                    }
                    if (type != null) {
                        fillLength -= type.Length;
                    }

                    if (Alignment != '=' && sign != null) {
                        text = sign.Value + type + text;
                        sign = null;
                        type = null;
                    }

                    switch (Alignment) {
                        case '<': footerLen = fillLength; break;
                        case '=': headerLen = fillLength; break;
                        case null:
                        case '>': headerLen = fillLength; break;
                        case '^':
                            headerLen = footerLen = fillLength / 2;
                            if ((fillLength & 0x01) != 0) {
                                footerLen++;
                            }
                            break;
                    }

                    if (headerLen != 0) {
                        string ssign = sign.HasValue ? sign.Value.ToString() : "";
                        text = ssign + type + new string(fill, headerLen) + text;
                    } else {
                        if (sign != null) {
                            text = sign.Value + text;
                        }
                        if (type != null) {
                            text = type + text;
                        }
                    }

                    if (footerLen != 0) {
                        text = text + new string(fill, footerLen);
                    }
                } else {
                    text = FinishText(text, sign, type);
                }
            } else {
                text = FinishText(text, sign, type);
            }

            
            return text;
        }

        private static string FinishText(string text, char? sign, string type) {
            if (sign != null) {
                text = sign.Value + type + text;
            } else if (type != null) {
                text = type + text;
            }
            return text;
        }

        private string GetTypeString() {
            string type = null;
            if (AlternateForm) {
                switch (Type) {
                    case 'X': type = "0X"; break;
                    case 'x': type = "0x"; break;
                    case 'o': type = "0o"; break;
                    case 'b': type = "0b"; break;
                }
            }
            return type;
        }

        private char? GetSign(bool isZero, bool isPos) {
            switch (Sign) {
                case ' ':
                    if (isPos || isZero) {
                        return ' ';
                    } else {
                        return '-';
                    }
                case '+':
                    if (isPos || isZero) {
                        return '+';
                    } else {
                        return '-';
                    }
                default:
                case '-':
                    if (!isPos && !isZero) {
                        return '-';
                    }
                    break;
            }
            return null;
        }

        private static int? ParseInt(string/*!*/ formatSpec, ref int curOffset) {
            int? value = null;
            int start = curOffset;
            do {
                curOffset++;
            } while (curOffset < formatSpec.Length && char.IsDigit(formatSpec[curOffset]));

            if (start != curOffset) {
                try {
                    value = int.Parse(formatSpec.Substring(start, curOffset - start));
                } catch (OverflowException) {
                    throw PythonOps.ValueError("Too many decimal digits in format string");
                }
            }
            return value;
        }
    }
}
