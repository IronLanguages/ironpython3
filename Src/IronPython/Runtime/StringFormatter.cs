// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

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
        private object _data;
        private int _dataIndex;

        private string _str;
        private int _index;
        private char _curCh;

        // The options for formatting the current formatting specifier in the format string
        internal FormatSettings _opts;
        // Should ddd.0 be displayed as "ddd" or "ddd.0". "'%g' % ddd.0" needs "ddd", but str(ddd.0) needs "ddd.0"
        internal bool _TrailingZeroAfterWholeFloat = false;

        private StringBuilder _buf;

        // This is a ThreadStatic since so that formatting operations on one thread do not interfere with other threads
        [ThreadStatic]
        private static NumberFormatInfo NumberFormatInfoForThreadLower;
        [ThreadStatic]
        private static NumberFormatInfo NumberFormatInfoForThreadUpper;

        internal static NumberFormatInfo nfil {
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
        internal static NumberFormatInfo nfiu {
            get {
                if (NumberFormatInfoForThreadUpper == null) {
                    NumberFormatInfo numberFormatInfo = ((CultureInfo)CultureInfo.InvariantCulture.Clone()).NumberFormat;
                    // The CLI formats as "Infinity", but CPython formats differently
                    numberFormatInfo.PositiveInfinitySymbol = "INF";
                    numberFormatInfo.NegativeInfinitySymbol = "-INF";
                    numberFormatInfo.NaNSymbol = "NAN";

                    NumberFormatInfoForThreadUpper = numberFormatInfo;
                }
                return NumberFormatInfoForThreadUpper;
            }
        }

        private NumberFormatInfo _nfi;

        #region Constructors

        private StringFormatter(CodeContext/*!*/ context, string str, object data) {
            _str = str;
            _data = data;
            _context = context;
            _nfi = nfil;
        }

        #endregion

        #region Public API Surface

        public static string Format(CodeContext/*!*/ context, string str, object data, bool trailingZeroAfterWholeFloat = false)
            => new StringFormatter(context, str, data) { _TrailingZeroAfterWholeFloat = trailingZeroAfterWholeFloat }.Format();

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

            string key = ReadMappingKey();

            _opts = new FormatSettings();

            ReadConversionFlags();

            ReadMinimumFieldWidth();

            ReadPrecision();

            ReadLengthModifier();

            // use the key (or lack thereof) to get the value
            object value;
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
        private string ReadMappingKey() {
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
                if (_opts.Precision > 116) throw PythonOps.OverflowError("formatted integer is too long (precision too large?)");
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
                case 'i': AppendInt(); return;
                // unsigned octal
                case 'o': AppendOctal(); return;
                // unsigned decimal
                case 'u': AppendInt(); return;
                // unsigned hexadecimal
                case 'x': AppendHex(_curCh); return;
                case 'X': AppendHex(_curCh); return;
                // floating point exponential format
                case 'e':
                // floating point decimal
                case 'f':
                // Same as "e" if exponent is less than -4 or more than precision, "f" otherwise.
                case 'g': AppendFloat(_curCh); return;
                // same as 3 above but uppercase
                case 'E':
                case 'F':
                case 'G': _nfi = nfiu; AppendFloat(_curCh); _nfi = nfil; return;
                // single character (int or single char str)
                case 'c': AppendChar(); return;
                // string (repr() version)
                case 'r': AppendRepr(); return;
                // string (str() version)
                case 's': AppendString(); return;
                default:
                    if (_curCh > 0xff)
                        throw PythonOps.ValueError("unsupported format character '{0}' (0x{1:X}) at index {2}", '?', (int)_curCh, _index - 1);
                    else
                        throw PythonOps.ValueError("unsupported format character '{0}' (0x{1:X}) at index {2}", _curCh, (int)_curCh, _index - 1);
            }
        }

        private object GetData(int index) {
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

        private object GetKey(string key) {
            if (!(_data is IDictionary<object, object> map)) {
                if (!(_data is PythonDictionary dict)) {
                    if (PythonOps.IsMappingType(DefaultContext.Default, _data)) {
                        return PythonOps.GetIndex(_context, _data, key);
                    }

                    throw PythonOps.TypeError("format requires a mapping");
                }

                object res;
                if (dict.TryGetValue(key, out res)) return res;
            } else {
                object res;
                if (map.TryGetValue(key, out res)) {
                    return res;
                }
            }

            throw PythonOps.KeyError(key);
        }

        private object GetIntegerValue(out bool fPos, bool allowDouble = true) {
            object val;
            int intVal;

            if (!allowDouble && _opts.Value is float || _opts.Value is double || _opts.Value is Extensible<double>) {
                // TODO: this should fail in 3.5
                PythonOps.Warn(_context, PythonExceptions.DeprecationWarning, "automatic int conversions have been deprecated");
            }

            if (_context.LanguageContext.TryConvertToInt32(_opts.Value, out intVal)) {
                val = intVal;
                fPos = intVal >= 0;
            } else {
                BigInteger bigInt;
                if (Converter.TryConvertToBigInteger(_opts.Value, out bigInt)) {
                    val = bigInt;
                    fPos = bigInt >= BigInteger.Zero;
                } else {
                    throw PythonOps.TypeError("int argument required");
                }
            }
            return val;
        }

        private void AppendChar() {
            char val = Converter.ExplicitConvertToChar(_opts.Value);
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
                    throw PythonOps.TypeError("not all arguments converted during string formatting");
                }
            }
        }

        private void AppendInt() {
            object val = GetIntegerValue(out bool fPos);

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

        private static readonly char[] zero = new char[] { '0' };

        // With .NET Framework "F" formatting is truncated after 15 digits:
        private static bool truncatedToString = (1.0 / 3).ToString("F17", CultureInfo.InvariantCulture) == "0.33333333333333300";

        // Return the new type char to use
        private char AdjustForG(char type, double v) {
            if (type != 'G' && type != 'g')
                return type;
            if (double.IsNaN(v) || double.IsInfinity(v))
                return type;

            double absV = Math.Abs(v);

            if (_opts.Precision == 0) {
                _opts.Precision = 1;
            }

            if ((v != 0.0) && // 0.0 should not be displayed as scientific notation
                absV < 1e-4 || // Values less than 0.0001 will need scientific notation
                absV >= Math.Pow(10, _opts.Precision)) { // Values bigger than 1e<precision> will need scientific notation

                type = (type == 'G') ? 'E' : 'e';

                // For e/E formatting, precision means the number of digits after the decimal point.
                // One digit is displayed before the decimal point.
                int fractionDigitsRequired = _opts.Precision - 1;
                string expForm = absV.ToString("E" + fractionDigitsRequired, CultureInfo.InvariantCulture);
                string mantissa = expForm.Substring(0, expForm.IndexOf('E')).TrimEnd(zero);
                if (mantissa.Length == 1) {
                    _opts.Precision = 0;
                } else {
                    // We do -2 to ignore the digit before the decimal point and the decimal point itself
                    Debug.Assert(mantissa[1] == '.');
                    _opts.Precision = mantissa.Length - 2;
                }
            } else {
                string fixedPointForm;
                bool convertType = true;
                if (truncatedToString) {
                    // "0.000ddddd" is allowed when the precision is 5. The 3 leading zeros are not counted
                    int numberDecimalDigits = _opts.Precision;
                    if (absV < 1e-3) numberDecimalDigits += 3;
                    else if (absV < 1e-2) numberDecimalDigits += 2;
                    else if (absV < 1e-1) numberDecimalDigits += 1;

                    fixedPointForm = absV.ToString("F" + numberDecimalDigits, CultureInfo.InvariantCulture).TrimEnd(zero);
                    if (numberDecimalDigits > 15) {
                        // System.Double(0.33333333333333331).ToString("F17") == "0.33333333333333300"
                        string fixedPointFormG = absV.ToString("G" + _opts.Precision, CultureInfo.InvariantCulture);
                        if (fixedPointFormG.Length > fixedPointForm.Length) {
                            fixedPointForm = fixedPointFormG;
                            convertType = false;
                        }
                    }
                } else {
                    fixedPointForm = absV.ToString("G" + _opts.Precision, CultureInfo.InvariantCulture);
                }
                if (convertType) {
                    type = (type == 'G') ? 'F' : 'f';

                    // For f/F formatting, precision means the number of digits after the decimal point.
                    var decimalPointIdx = fixedPointForm.IndexOf('.');
                    string fraction = decimalPointIdx == -1 ? string.Empty : fixedPointForm.Substring(decimalPointIdx + 1);
                    if (absV < 1.0) {
                        _opts.Precision = fraction.Length;
                    } else {
                        int digitsBeforeDecimalPoint = 1 + (int)Math.Log10(absV);
                        if (_opts.AltForm) {
                            _opts.Precision = _opts.Precision - digitsBeforeDecimalPoint;
                        } else {
                            _opts.Precision = Math.Min(_opts.Precision - digitsBeforeDecimalPoint, fraction.Length);
                        }
                    }
                }
            }

            return type;
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
            } else {
                if (_opts.Precision > 50)
                    _opts.Precision = 50; // TODO: remove this restriction
            }

            format = AdjustForG(format, val);

            var fPos = DoubleOps.Sign(val) >= 0 || double.IsNaN(val);
            var str = FormatWithPrecision(val, fPos, format);
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
                        _buf.Append(str.Substring(1));
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

        private static readonly bool needsFixupFloatMinus = $"{-0.1:f0}" == "0"; // fixed in .NET Core 3.1

        private string FormatWithPrecision(double val, bool fPos, char format) {
            string res;
            if (double.IsNaN(val) || double.IsInfinity(val)) {
                res = val.ToString(_nfi);
                if (fPos) {
                    if (_opts.SignChar) {
                        res = "+" + res;
                    } else if (_opts.Space) {
                        res = " " + res;
                    }
                }
                return res;
            }
            else {
                res = val.ToString($"{format}{_opts.Precision}", _nfi);
                res = FixupFloatMinus(val, fPos, res);
                if (fPos) {
                    if (_opts.SignChar) {
                        res = "+" + res;
                    } else if (_opts.Space) {
                        res = " " + res;
                    }
                }
            }

            if (format == 'e' || format == 'E') {
                res = AdjustExponent(res);
                if (_opts.Precision == 0 && _opts.AltForm) {
                    res = res.Insert(res.IndexOf(format), ".");
                }
            } else {
                if (_opts.Precision == 0 && _opts.AltForm) {
                    res += ".";
                }
            }

            // If AdjustForG() sets opts.Precision == 0, it means that no significant digits should be displayed after
            // the decimal point. ie. 123.4 should be displayed as "123", not "123.4". However, we might still need a 
            // decorative ".0". ie. to display "123.0"
            if (_TrailingZeroAfterWholeFloat && (format == 'f' || format == 'F') && _opts.Precision == 0)
                res += ".0";

            return res;

            // Ensure negative values rounded to 0 show up as -0.
            static string FixupFloatMinus(double val, bool fPos, string x) {
                if (needsFixupFloatMinus && !fPos && val >= -0.5 && x[0] != '-') {
                    Debug.Assert(x[0] == '0');
                    return "-" + x;
                }
                return x;
            }


            // A strange string formatting bug requires that we use Standard Numeric Format and
            // not Custom Numeric Format. Standard Numeric Format produces always a 3 digit exponent
            // which needs to be taken care off.
            // Example: 9.3126672485384569e+23, precision=16
            //  format string "e16" ==> "9.3126672485384569e+023", but we want "e+23", not "e+023"
            //  format string "0.0000000000000000e+00" ==> "9.3126672485384600e+23", which is a precision error
            //  so, we have to format with "e16" and strip the zero manually
            static string AdjustExponent(string val) {
                if (val[val.Length - 3] == '0') {
                    return val.Substring(0, val.Length - 3) + val.Substring(val.Length - 2, 2);
                } else {
                    return val;
                }
            }
        }

        private static string GetAltFormPrefixForRadix(char format, int radix) {
            switch (radix) {
                case 8: return format + "0";
                case 16: return format + "0";
            }
            return "";
        }

        /// <summary>
        /// AppendBase appends an integer at the specified radix doing all the
        /// special forms for Python.
        /// </summary>
        private void AppendBase(char format, int radix) {
            var str = ProcessNumber(format, radix, ref _opts, GetIntegerValue(out bool fPos, allowDouble: false));

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

        private void AppendHex(char format) {
            AppendBase(format, 16);
        }

        private void AppendOctal() {
            AppendBase('o', 8);
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

        private static readonly long NegativeZeroBits = BitConverter.DoubleToInt64Bits(-0.0);

        internal static bool IsNegativeZero(double x) {
            // -0.0 == 0.0 is True, so detecting -0.0 uses memory representation
            return BitConverter.DoubleToInt64Bits(x) == NegativeZeroBits;
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
        internal enum FormatOptions {
            ZeroPad = 0x01, // Use zero-padding to fit FieldWidth
            LeftAdj = 0x02, // Use left-adjustment to fit FieldWidth. Overrides ZeroPad
            AltForm = 0x04, // Add a leading 0 if necessary for octal, or add a leading 0x or 0X for hex
            Space = 0x08, // Leave a white-space
            SignChar = 0x10 // Force usage of a sign char even if the value is positive
        }

        internal struct FormatSettings {

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

            internal object Value;
        }
        #endregion
    }
}
