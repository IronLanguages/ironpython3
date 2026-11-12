// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;

using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;

using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime {
    /// <summary>
    /// StringFormatter provides Python's % style string formatting services.
    /// </summary>
    internal class StringFormatter {
        private const int UnspecifiedPrecision = -1; // Use the default precision

        private readonly CodeContext/*!*/ _context;
        private readonly object? _data;
        private int _dataIndex;

        private readonly string _str;
        private int _index;
        private char _curCh;

        // The options for formatting the current formatting specifier in the format string
        private FormatSettings _opts;

        private bool _asBytes;

        private StringBuilder _buf;

        // This is a ThreadStatic since so that formatting operations on one thread do not interfere with other threads
        [ThreadStatic]
        private static NumberFormatInfo? NumberFormatInfoForThreadLower;

        private static NumberFormatInfo nfil {
            get {
                if (NumberFormatInfoForThreadLower == null) {
                    NumberFormatInfo numberFormatInfo = ((CultureInfo)CultureInfo.InvariantCulture.Clone()).NumberFormat;
                    // The CLI formats as "Infinity", but CPython formats differently
                    numberFormatInfo.PositiveInfinitySymbol = "inf";
                    numberFormatInfo.NegativeInfinitySymbol = "-inf";
                    numberFormatInfo.NaNSymbol = "nan";

                    NumberFormatInfoForThreadLower = numberFormatInfo;
                }
                return NumberFormatInfoForThreadLower;
            }
        }

        private NumberFormatInfo _nfi;

        #region Constructors

        private StringFormatter(CodeContext/*!*/ context, string str, object? data) {
            _str = str;
            _data = data;
            _context = context;
            _nfi = nfil;
            _buf = null!;
        }

        #endregion

        #region Public API Surface

        public static string Format(CodeContext/*!*/ context, string str, object? data)
            => new StringFormatter(context, str, data).Format();

        internal static byte[] FormatBytes(CodeContext/*!*/ context, ReadOnlySpan<byte> str, object? data)
            => new StringFormatter(context, str.MakeString(), data) { _asBytes = true }.Format().MakeByteArray();

        #endregion

        #region Private APIs

        private string Format() {
            _index = 0;
            _buf = new StringBuilder(_str.Length * 2);
            int modIndex;

            while ((modIndex = _str.IndexOf('%', _index)) != -1) {
                _buf.Append(_str, _index, modIndex - _index);
                _index = modIndex + 1;
                DoFormatCode();
            }
            _buf.Append(_str, _index, _str.Length - _index);

            CheckDataUsed();

            return _buf.ToString();
        }

        private void DoFormatCode() {
            // we already pulled the first %
            if (_index == _str.Length)
                throw PythonOps.ValueError("incomplete format, expected format character at index {0}", _index);

            // Index is placed right after the %.
            Debug.Assert(_str[_index - 1] == '%');

            _curCh = _str[_index++];

            if (_curCh == '%') {
                // Escaped '%' character using "%%". Just print it and we are done
                _buf.Append('%');
                return;
            }

            var key = ReadMappingKey();

            _opts = new FormatSettings();

            ReadConversionFlags();

            ReadMinimumFieldWidth();

            ReadPrecision();

            ReadLengthModifier();

            // use the key (or lack thereof) to get the value
            object? value;
            if (key == null) {
                value = GetData(_dataIndex++);
            } else {
                value = GetKey(key);
            }
            _opts.Value = value;

            WriteConversion();
        }


        /// <summary>
        /// Read a possible mapping key for %(key)s.
        /// </summary>
        /// <returns>The key name enclosed between the '%(key)s',
        /// or null if there are no paranthesis such as '%s'.</returns>
        private object? ReadMappingKey() {
            // Caller has set _curCh to the character past the %, and
            // _index to 2 characters past the original '%'.
            Debug.Assert(_curCh == _str[_index - 1]);
            Debug.Assert(_str[_index - 2] == '%');


            if (_curCh != '(') {
                // No parenthesized key.
                return null;
            }


            // CPython supports nested parenthesis (See "S3.6.2:String Formatting Operations").
            // Keywords inbetween %(...)s can contain parenthesis.
            //
            // For example, here are the keys returned for various format strings:
            // %(key)s        - return 'key'
            // %((key))s      - return '(key)'
            // %()s           - return ''
            // %((((key))))s  - return '(((key)))'
            // %((%)s)s       - return '(%)s'
            // %((%s))s       - return (%s)
            // %(a(b)c)s      - return a(b)c
            // %((a)s)s       - return (a)s
            // %(((a)s))s     - return ((a)s)


            // Use a counter rule.
            int nested = 1; // already passed the 1st '('
            int start = _index; // character index after 1st opening '('
            int end = start;

            while (end < _str.Length) {
                if (_str[end] == '(') {
                    nested++;
                } else if (_str[end] == ')') {
                    nested--;
                }

                if (nested == 0) {
                    // Found final matching closing parent
                    string key = _str.Substring(_index, end - start);

                    // Update fields
                    _index = end + 1;
                    if (_index == _str.Length) {
                        // This error could happen with a format string like '%((key))'
                        throw PythonOps.ValueError("incomplete format");
                    }
                    _curCh = _str[_index++];

                    if (_asBytes) return Bytes.Make(key.MakeByteArray());
                    return key;
                }

                end++;
            }

            // Error: missing closing ')'.
            // This could happen with '%((key)s'
            throw PythonOps.ValueError("incomplete format key");
        }

        private void ReadConversionFlags() {
            bool fFoundConversion;
            do {
                fFoundConversion = true;
                switch (_curCh) {
                    case '#': _opts.AltForm = true; break;
                    case '-': _opts.LeftAdj = true; _opts.ZeroPad = false; break;
                    case '0': if (!_opts.LeftAdj) _opts.ZeroPad = true; break;
                    case '+': _opts.SignChar = true; _opts.Space = false; break;
                    case ' ': if (!_opts.SignChar) _opts.Space = true; break;
                    default: fFoundConversion = false; break;
                }
                if (fFoundConversion) _curCh = _str[_index++];
            } while (fFoundConversion);
        }

        private int ReadNumberOrStar() {
            return ReadNumberOrStar(0);
        }
        private int ReadNumberOrStar(int noValSpecified) {
            int res = noValSpecified;
            if (_curCh == '*') {
                if (!(_data is PythonTuple)) { throw PythonOps.TypeError("* requires a tuple for values"); }
                _curCh = _str[_index++];
                res = _context.LanguageContext.ConvertToInt32(GetData(_dataIndex++));
            } else {
                if (char.IsDigit(_curCh)) {
                    res = 0;
                    try {
                        while (char.IsDigit(_curCh) && _index < this._str.Length) {
                            res = checked(res * 10 + ((int)(_curCh - '0')));
                            _curCh = _str[_index++];
                        }
                    } catch (OverflowException) {
                        throw PythonOps.ValueError("width too big");
                    }
                }
            }
            return res;
        }

        private void ReadMinimumFieldWidth() {
            int fieldWidth = ReadNumberOrStar();
            if (fieldWidth < 0) {
                _opts.FieldWidth = fieldWidth * -1;
                _opts.LeftAdj = true;
            } else {
                _opts.FieldWidth = fieldWidth;
            }

            if (_opts.FieldWidth == int.MaxValue) {
                throw PythonOps.MemoryError("not enough memory for field width");
            }
        }

        private void ReadPrecision() {
            if (_curCh == '.') {
                _curCh = _str[_index++];
                // possibility: "8.f", "8.0f", or "8.2f"
                _opts.Precision = ReadNumberOrStar();
                if (_opts.Precision > 1048575) throw PythonOps.OverflowError("precision too large"); // CPython allows for larger precision values but what's the point...
            } else {
                _opts.Precision = UnspecifiedPrecision;
            }
        }

        private void ReadLengthModifier() {
            switch (_curCh) {
                // ignored, not necessary for Python
                case 'h':
                case 'l':
                case 'L':
                    _curCh = _str[_index++];
                    break;
            }
        }

        private void WriteConversion() {
            // conversion type (required)
            switch (_curCh) {
                // string (ascii() version)
                case 'a': AppendAscii(); return;
                // signed integer decimal
                case 'd':
                case 'i': AppendInt(_curCh); return;
                // unsigned octal
                case 'o': AppendBase(_curCh, 8); return;
                // unsigned decimal
                case 'u': AppendInt(_curCh); return;
                // unsigned hexadecimal
                case 'x':
                case 'X': AppendBase(_curCh, 16); return;
                // floating point exponential format
                case 'e':
                // floating point decimal
                case 'f':
                // Same as "e" if exponent is less than -4 or more than precision, "f" otherwise.
                case 'g':
                // same as 3 above but uppercase
                case 'E':
                case 'F':
                case 'G': AppendFloat(_curCh); return;
                // single character (int or single char str)
                case 'c': AppendChar(); return;
                // string (repr() version)
                case 'r':
                    if (_asBytes) goto case 'a';
                    AppendRepr(); return;
                // string (str() version)
                case 's':
                    if (_asBytes) goto case 'b';
                    AppendString(); return;
                // bytes
                case 'b':
                    if (!_asBytes) goto default;
                    AppendBytes(); return;
                default:
                    if (_curCh > 0xff)
                        throw PythonOps.ValueError("unsupported format character '{0}' (0x{1:X}) at index {2}", '?', (int)_curCh, _index - 1);
                    else
                        throw PythonOps.ValueError("unsupported format character '{0}' (0x{1:X}) at index {2}", _curCh, (int)_curCh, _index - 1);
            }
        }

        private object? GetData(int index) {
            if (_data is PythonTuple dt) {
                if (index < dt.__len__()) {
                    return dt[index];
                }
            } else {
                if (index == 0) {
                    return _data;
                }
            }

            throw PythonOps.TypeError("not enough arguments for format string");
        }

        private object? GetKey(object key) {
            if (_data is IDictionary<object, object> map) {
                if (map.TryGetValue(key, out object? res)) {
                    return res;
                }
            } else if (_data is PythonDictionary dict) {
                if (dict.TryGetValue(key, out object? res)) {
                    return res;
                }
            } else {
                if (PythonOps.IsMappingType(DefaultContext.Default, _data)) {
                    return PythonOps.GetIndex(_context, _data, key);
                }

                throw PythonOps.TypeError("format requires a mapping");
            }
            throw PythonOps.KeyError(key);
        }

        private object GetIntegerValue(char format, out bool fPos, bool allowDouble = true) {
            if (!allowDouble && (_opts.Value is float || _opts.Value is double || _opts.Value is Extensible<double>)) {
#if PYTHON_34
                PythonOps.Warn(_context, PythonExceptions.DeprecationWarning, "automatic int conversions have been deprecated");
#else
                throw PythonOps.TypeError($"an integer is required, not {PythonOps.GetPythonTypeName(_opts.Value)}");
#endif
            }

            switch (_opts.Value) {
                case float:
                case double:
                case Extensible<double>:
                    if (_context.LanguageContext.TryConvertToInt32(_opts.Value, out int intVal)) {
                        fPos = intVal >= 0;
                        return intVal;
                    }
                    if (Converter.TryConvertToBigInteger(_opts.Value, out BigInteger bigInt)) {
                        fPos = bigInt >= BigInteger.Zero;
                        return bigInt;
                    }
                    break;
            }

            try {
                if (PythonOps.TryToIndex(_opts.Value, out object? index)) {
                    fPos = index switch {
                        int i => i >= 0,
                        BigInteger bi => bi >= BigInteger.Zero,
                        _ => throw new InvalidOperationException(), // unreachable
                    };
                    return index;
                }
            } catch (TypeErrorException) { }

            if (allowDouble) {
                throw PythonOps.TypeError("%{0} format: a number is required, not {1}", format, PythonOps.GetPythonTypeName(_opts.Value));
            } else {
                throw PythonOps.TypeError("%{0} format: an integer is required, not {1}", format, PythonOps.GetPythonTypeName(_opts.Value));
            }
        }

        private void AppendChar() {
            char val;
            if (_asBytes) {
                if (_opts.Value is Bytes bytes && bytes.Count == 1) {
                    val = (char)bytes[0];
                } else if (_opts.Value is ByteArray byteArray && byteArray.Count == 1) {
                    val = (char)(int)byteArray[0];
                } else {
                    try {
                        val = PythonOps.Index(_opts.Value) switch {
                            int i => (char)checked((byte)i),
                            BigInteger bi => (char)checked((byte)bi),
                            _ => throw new InvalidOperationException(), // unreachable
                        };
                    } catch (OverflowException) {
                        throw PythonOps.OverflowError("%c arg not in range(256)");
                    } catch (TypeErrorException) {
                        throw PythonOps.TypeError("%c requires an integer in range(256) or a single byte");
                    }
                }
            } else {
                val = Converter.ExplicitConvertToChar(_opts.Value);
            }
            if (_opts.FieldWidth > 1) {
                if (!_opts.LeftAdj) {
                    _buf.Append(' ', _opts.FieldWidth - 1);
                }
                _buf.Append(val);
                if (_opts.LeftAdj) {
                    _buf.Append(' ', _opts.FieldWidth - 1);
                }
            } else {
                _buf.Append(val);
            }
        }

        private void CheckDataUsed() {
            if (!PythonOps.IsMappingType(DefaultContext.Default, _data)) {
                if ((!(_data is PythonTuple) && _dataIndex != 1) ||
                    (_data is PythonTuple && _dataIndex != ((PythonTuple)_data).__len__())) {
                    throw PythonOps.TypeError("not all arguments converted during {0} formatting", _asBytes ? "bytes" : "string");
                }
            }
        }

        private void AppendInt(char format) {
            object val = GetIntegerValue(format, out bool fPos);

            if (_opts.LeftAdj) {
                string str = ZeroPadInt(val, fPos, _opts.Precision);

                var pad = _opts.FieldWidth - str.Length;
                if (fPos && (_opts.SignChar || _opts.Space)) {
                    _buf.Append(_opts.SignChar ? '+' : ' ');
                    pad--;
                }
                _buf.Append(str);
                if (pad > 0) _buf.Append(' ', pad);
            } else if (_opts.ZeroPad || _opts.Precision > 0) {
                int minNumDigits = _opts.Precision;
                if (_opts.ZeroPad && _opts.FieldWidth > minNumDigits) {
                    minNumDigits = _opts.FieldWidth;
                    if (!fPos || _opts.SignChar || _opts.Space) minNumDigits--;
                }

                var str = ZeroPadInt(val, fPos, minNumDigits);

                if (fPos && (_opts.SignChar || _opts.Space)) {
                    var pad = _opts.FieldWidth - str.Length - 1;
                    if (pad > 0) _buf.Append(' ', pad);
                    _buf.Append(_opts.SignChar ? '+' : ' ');
                } else {
                    var pad = _opts.FieldWidth - str.Length;
                    if (pad > 0) _buf.Append(' ', pad);
                }

                _buf.Append(str);
            } else {
                if (fPos && (_opts.SignChar || _opts.Space)) {
                    var str = string.Format(_nfi, "{0:D}", val);
                    var pad = _opts.FieldWidth - (str.Length + 1);
                    if (pad > 0) _buf.Append(' ', pad);
                    _buf.Append(_opts.SignChar ? '+' : ' ');
                    _buf.Append(str);
                } else {
                    _buf.AppendFormat(_nfi, "{0," + _opts.FieldWidth + ":D}", val);
                }
            }
        }

        private static readonly bool supportsPrecisionGreaterThan99 = 0.ToString("D100", CultureInfo.InvariantCulture) != "D100"; // support is new in .NET 6

        private string ZeroPadInt(object val, bool fPos, int minNumDigits) {
            if (minNumDigits < 2) {
                return string.Format(_nfi, "{0:D}", val);
            }

            if (minNumDigits < 100 || supportsPrecisionGreaterThan99) {
                return string.Format(_nfi, "{0:D" + minNumDigits + "}", val);
            }

            var res = string.Format(_nfi, "{0:D}", val);
            if (fPos) {
                var zeroPad = minNumDigits - res.Length;
                if (zeroPad > 0) {
                    res = new string('0', zeroPad) + res;
                }
            } else {
                var zeroPad = minNumDigits - res.Length + 1; // '-' does not count
                if (zeroPad > 0) {
                    res = '-' + new string('0', zeroPad) + res.Substring(1);
                }
            }
            return res;
        }

        private void AppendFloat(char format) {
            double val;
            if (!Converter.TryConvertToDouble(_opts.Value, out val))
                throw PythonOps.TypeError("float argument required");

            Debug.Assert(format == 'E' || format == 'e' || // scientific exponential format
                         format == 'F' || format == 'f' || // floating point decimal
                         format == 'G' || format == 'g');  // Same as "e" if exponent is less than -4 or more than precision, "f" otherwise.

            // update our precision first...
            if (_opts.Precision == UnspecifiedPrecision) {
                _opts.Precision = 6;
            }

            var fPos = DoubleOps.Sign(val) >= 0 || double.IsNaN(val);
            var str = FloatFormatter.Format(val, format, _opts.Precision, _opts.AltForm);

            if (!fPos) {
                str = "-" + str;
            } else if (_opts.SignChar) {
                str = "+" + str;
            } else if (_opts.Space) {
                str = " " + str;
            }

            var pad = _opts.FieldWidth - str.Length;

            // then append
            if (_opts.LeftAdj) {
                _buf.Append(str);
                if (pad > 0) _buf.Append(' ', pad);
            } else if (_opts.ZeroPad) {
                if (pad > 0) {
                    if (!fPos || _opts.SignChar || _opts.Space) {
                        _buf.Append(str[0]);
                        _buf.Append('0', pad);
                        _buf.Append(str, 1, str.Length - 1);
                    } else {
                        _buf.Append('0', pad);
                        _buf.Append(str);
                    }
                } else {
                    _buf.Append(str);
                }
            } else {
                if (pad > 0) _buf.Append(' ', pad);
                _buf.Append(str);
            }
        }

        private static string GetAltFormPrefixForRadix(char format, int radix) {
            return radix switch {
                8 => format + "0",
                16 => format + "0",
                _ => "",
            };
        }

        /// <summary>
        /// AppendBase appends an integer at the specified radix doing all the
        /// special forms for Python.
        /// </summary>
        private void AppendBase(char format, int radix) {
            var str = ProcessNumber(format, radix, ref _opts, GetIntegerValue(format, out bool fPos, allowDouble: false));

            if (!fPos) {
                // if negative number, the leading space has no impact
                _opts.Space = false;
            }

            // pad out for additional precision
            if (str.Length < _opts.Precision) {
                int len = _opts.Precision - str.Length;
                str.Append('0', len);
            }

            // pad result to minimum field width
            if (_opts.FieldWidth != 0) {
                int signLen = (!fPos || _opts.SignChar) ? 1 : 0;
                int spaceLen = _opts.Space ? 1 : 0;
                int len = _opts.FieldWidth - (str.Length + signLen + spaceLen);

                if (len > 0) {
                    // we account for the size of the alternate form, if we'll end up adding it.
                    if (_opts.AltForm) {
                        len -= GetAltFormPrefixForRadix(format, radix).Length;
                    }

                    if (len > 0) {
                        // and finally append the right form
                        if (_opts.LeftAdj) {
                            str.Insert(0, " ", len);
                        } else {
                            if (_opts.ZeroPad) {
                                str.Append('0', len);
                            } else {
                                _buf.Append(' ', len);
                            }
                        }
                    }
                }
            }

            // append the alternate form
            if (_opts.AltForm)
                str.Append(GetAltFormPrefixForRadix(format, radix));


            // add any sign if necessary
            if (!fPos) {
                _buf.Append('-');
            } else if (_opts.SignChar) {
                _buf.Append('+');
            } else if (_opts.Space) {
                _buf.Append(' ');
            }

            // append the final value
            for (int i = str.Length - 1; i >= 0; i--) {
                _buf.Append(str[i]);
            }

            static StringBuilder ProcessNumber(char format, int radix, ref FormatSettings _opts, object intVal) {
                StringBuilder str;

                // we build up the number backwards inside a string builder,
                // and after we've finished building this up we append the
                // string to our output buffer backwards.

                if (intVal is BigInteger bi) {
                    BigInteger val = bi;
                    if (val < 0) val *= -1;

                    str = new StringBuilder();

                    // use .NETs faster conversion if we can
                    if (radix == 16) {
                        AppendNumberReversed(str, char.IsLower(format) ? val.ToString("x") : val.ToString("X"));
                    } else if (radix == 10) {
                        AppendNumberReversed(str, val.ToString());
                    } else {
                        if (val == 0) str.Append('0');
                        while (val != 0) {
                            int digit = (int)(val % radix);
                            if (digit < 10) str.Append((char)((digit) + '0'));
                            else if (char.IsLower(format)) str.Append((char)((digit - 10) + 'a'));
                            else str.Append((char)((digit - 10) + 'A'));

                            val /= radix;
                        }
                    }
                } else {
                    int val = (int)intVal;
                    if (val == int.MinValue) return ProcessNumber(format, radix, ref _opts, (BigInteger)val);
                    if (val < 0) val *= -1;

                    str = new StringBuilder();

                    if (val == 0) str.Append('0');
                    while (val != 0) {
                        int digit = val % radix;
                        if (digit < 10) str.Append((char)((digit) + '0'));
                        else if (char.IsLower(format)) str.Append((char)((digit - 10) + 'a'));
                        else str.Append((char)((digit - 10) + 'A'));
                        val /= radix;
                    }
                }

                return str;
            }
        }

        private static void AppendNumberReversed(StringBuilder str, string res) {
            int start = 0;
            while (start < (res.Length - 1) && res[start] == '0') {
                start++;
            }
            for (int i = res.Length - 1; i >= start; i--) {
                str.Append(res[i]);
            }
        }

        private void AppendBytes() {
            Debug.Assert(_asBytes);
            if (_opts.Value is Bytes bytes || Bytes.TryInvokeBytesOperator(_context, _opts.Value, out bytes!)) {
                AppendString(StringOps.Latin1Encoding.GetString(bytes.UnsafeByteArray));
            } else if (_opts.Value is IBufferProtocol bufferProtocol) {
                using var buffer = bufferProtocol.GetBuffer(BufferFlags.FullRO);
                var span = buffer.IsCContiguous() ? buffer.AsReadOnlySpan() : buffer.ToArray();
                AppendString(StringOps.Latin1Encoding.GetString(span));
            } else {
                throw PythonOps.TypeError($"%b requires a bytes-like object, or an object that implements __bytes__, not '{PythonOps.GetPythonTypeName(_opts.Value)}'");
            }
        }

        private void AppendString() {
            AppendString(PythonOps.ToString(_context, _opts.Value));
        }

        private void AppendAscii() {
            AppendString(PythonOps.Ascii(_context, _opts.Value));
        }

        private void AppendRepr() {
            AppendString(PythonOps.Repr(_context, _opts.Value));
        }

        private void AppendString(string s) {
            if (_opts.Precision != UnspecifiedPrecision && s.Length > _opts.Precision) s = s.Substring(0, _opts.Precision);
            if (!_opts.LeftAdj && _opts.FieldWidth > s.Length) {
                _buf.Append(' ', _opts.FieldWidth - s.Length);
            }
            _buf.Append(s);
            if (_opts.LeftAdj && _opts.FieldWidth > s.Length) {
                _buf.Append(' ', _opts.FieldWidth - s.Length);
            }
        }

        #endregion

        #region Private data structures

        // The conversion specifier format is as follows:
        //   % (mappingKey) conversionFlags fieldWidth . precision lengthModifier conversionType
        // where:
        //   mappingKey - value to be formatted
        //   conversionFlags - # 0 - + <space>
        //   lengthModifier - h, l, and L. Ignored by Python
        //   conversionType - d i o u x X e E f F g G c r s %
        // Ex:
        //   %(varName)#4o - Display "varName" as octal and prepend with leading 0 if necessary, for a total of atleast 4 characters

        [Flags]
        private enum FormatOptions {
            ZeroPad = 0x01, // Use zero-padding to fit FieldWidth
            LeftAdj = 0x02, // Use left-adjustment to fit FieldWidth. Overrides ZeroPad
            AltForm = 0x04, // Add a leading 0 if necessary for octal, or add a leading 0x or 0X for hex
            Space = 0x08, // Leave a white-space
            SignChar = 0x10 // Force usage of a sign char even if the value is positive
        }

        private struct FormatSettings {

            #region FormatOptions property accessors

            public bool ZeroPad {
                get {
                    return ((Options & FormatOptions.ZeroPad) != 0);
                }
                set {
                    if (value) {
                        Options |= FormatOptions.ZeroPad;
                    } else {
                        Options &= (~FormatOptions.ZeroPad);
                    }
                }
            }
            public bool LeftAdj {
                get {
                    return ((Options & FormatOptions.LeftAdj) != 0);
                }
                set {
                    if (value) {
                        Options |= FormatOptions.LeftAdj;
                    } else {
                        Options &= (~FormatOptions.LeftAdj);
                    }
                }
            }
            public bool AltForm {
                get {
                    return ((Options & FormatOptions.AltForm) != 0);
                }
                set {
                    if (value) {
                        Options |= FormatOptions.AltForm;
                    } else {
                        Options &= (~FormatOptions.AltForm);
                    }
                }
            }
            public bool Space {
                get {
                    return ((Options & FormatOptions.Space) != 0);
                }
                set {
                    if (value) {
                        Options |= FormatOptions.Space;
                    } else {
                        Options &= (~FormatOptions.Space);
                    }
                }
            }
            public bool SignChar {
                get {
                    return ((Options & FormatOptions.SignChar) != 0);
                }
                set {
                    if (value) {
                        Options |= FormatOptions.SignChar;
                    } else {
                        Options &= (~FormatOptions.SignChar);
                    }
                }
            }
            #endregion

            internal FormatOptions Options;

            // Minimum number of characters that the entire formatted string should occupy.
            // Smaller results will be left-padded with white-space or zeros depending on Options
            internal int FieldWidth;

            // Number of significant digits to display, before and after the decimal point.
            // For floats (except G/g format specification), it gets adjusted to the number of
            // digits to display after the decimal point since that is the value required by
            // the .NET string formatting.
            // For clarity, we should break this up into the two values - the precision specified by the
            // format string, and the value to be passed in to StringBuilder.AppendFormat
            internal int Precision;

            internal object? Value;
        }
        #endregion
    }
}
