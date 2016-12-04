/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using IronPython.Runtime.Operations;
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

#if FEATURE_NUMERICS
using System.Numerics;
#else
using Microsoft.Scripting.Math;
#endif

namespace IronPython.Runtime {
    /// <summary>
    /// StringFormatter provides Python's % style string formatting services.
    /// </summary>
    internal class StringFormatter {
        const int UnspecifiedPrecision = -1; // Use the default precision

        private readonly CodeContext/*!*/ _context;
        private readonly bool _isUnicodeString; // true if the LHS is Unicode and we should call __unicode__ for formatting objects on the RHS
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
        
        public StringFormatter(CodeContext/*!*/ context, string str, object data)
            : this(context, str, data, false) {
        }

        public StringFormatter(CodeContext/*!*/ context, string str, object data, bool isUnicode) {
            _str = str;
            _data = data;
            _context = context;
            _isUnicodeString = isUnicode;
            _nfi = nfil;
        }

        #endregion

        #region Public API Surface
        public string Format() {
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
        #endregion

        #region Private APIs

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
                res = PythonContext.GetContext(_context).ConvertToInt32(GetData(_dataIndex++));
            } else {
                if (Char.IsDigit(_curCh)) {
                    res = 0;
                    while (Char.IsDigit(_curCh) && _index < this._str.Length) {
                        res = res * 10 + ((int)(_curCh - '0'));
                        _curCh = _str[_index++];
                    }
                }
            }
            return res;
        }

        private void ReadMinimumFieldWidth() {
            int fieldWidth = ReadNumberOrStar();;
            if (fieldWidth < 0) {
                _opts.FieldWidth = fieldWidth * -1;
                _opts.LeftAdj = true;
            } else {
                _opts.FieldWidth = fieldWidth;
            }

            if (_opts.FieldWidth == Int32.MaxValue) {
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
            PythonTuple dt = _data as PythonTuple;
            if (dt != null) {
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
            IDictionary<object, object> map = _data as IDictionary<object, object>;
            if (map == null) {
                PythonDictionary dict = _data as PythonDictionary;
                if (dict == null) {
                    if (PythonOps.IsMappingType(DefaultContext.Default, _data) == ScriptingRuntimeHelpers.True) {
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

        private object GetIntegerValue(out bool fPos) {
            object val;
            int intVal;

            if (PythonContext.GetContext(_context).TryConvertToInt32(_opts.Value, out intVal)) {
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
            if (PythonOps.IsMappingType(DefaultContext.Default, _data) == ScriptingRuntimeHelpers.False) {
                if ((!(_data is PythonTuple) && _dataIndex != 1) ||
                    (_data is PythonTuple && _dataIndex != ((PythonTuple)_data).__len__())) {
                    throw PythonOps.TypeError("not all arguments converted during string formatting");
                }
            }
        }

        private void AppendInt() {
            bool fPos;
            object val = GetIntegerValue(out fPos);

            if (_opts.LeftAdj) {
                AppendLeftAdj(val, fPos, 'D');
            } else if (_opts.Precision > 0) {
                // in this case the precision means
                // the minimum number of characters
                _opts.FieldWidth = (_opts.Space || _opts.SignChar) ?
                     _opts.Precision + 1 : _opts.Precision;
                AppendZeroPad(val, fPos, 'D');
            } else if (_opts.ZeroPad) {
                AppendZeroPad(val, fPos, 'D');
            } else {
                AppendNumeric(val, fPos, 'D');
            }
        }

        private static readonly char[] zero = new char[] { '0' };

        // Return the new type char to use
        private char AdjustForG(char type, double v) {
            if (type != 'G' && type != 'g')
                return type;
            if (Double.IsNaN(v) || Double.IsInfinity(v))
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
                int fractionDigitsRequired = (_opts.Precision - 1);
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
                // "0.000ddddd" is allowed when the precision is 5. The 3 leading zeros are not counted
                int numberDecimalDigits = _opts.Precision;
                if (absV < 1e-3) numberDecimalDigits += 3;
                else if (absV < 1e-2) numberDecimalDigits += 2;
                else if (absV < 1e-1) numberDecimalDigits += 1;

                string fixedPointForm = absV.ToString("F" + numberDecimalDigits, CultureInfo.InvariantCulture).TrimEnd(zero);
                bool convertType = true;
                if (numberDecimalDigits > 15) {
                    // System.Double(0.33333333333333331).ToString("F17") == "0.33333333333333300"
                    string fixedPointFormG = absV.ToString("G" + numberDecimalDigits, CultureInfo.InvariantCulture).TrimEnd(zero);
                    if (fixedPointFormG.Length > fixedPointForm.Length) {
                        fixedPointForm = fixedPointFormG;
                        convertType = false;
                    }
                }
                if (convertType) {
                    type = (type == 'G') ? 'F' : 'f';

                    // For f/F formatting, precision means the number of digits after the decimal point.
                    string fraction = fixedPointForm.Substring(fixedPointForm.IndexOf('.') + 1);
                    if (absV < 1.0) {
                        _opts.Precision = fraction.Length;
                    } else {
                        int digitsBeforeDecimalPoint = 1 + (int)Math.Log10(absV);
                        _opts.Precision = Math.Min(_opts.Precision - digitsBeforeDecimalPoint, fraction.Length);
                    }
                }
            }

            return type;
        }

        private void AppendFloat(char type) {
            double v;
            if (!Converter.TryConvertToDouble(_opts.Value, out v))
                throw PythonOps.TypeError("float argument required");

            // scientific exponential format 
            Debug.Assert(type == 'E' || type == 'e' ||
                // floating point decimal
                         type == 'F' || type == 'f' ||
                // Same as "e" if exponent is less than -4 or more than precision, "f" otherwise.
                         type == 'G' || type == 'g');

            bool forceDot = false;
            // update our precision first...
            if (_opts.Precision != UnspecifiedPrecision) {
                if (_opts.Precision == 0 && _opts.AltForm) forceDot = true;
                if (_opts.Precision > 50)
                    _opts.Precision = 50;
            } else {
                // alternate form (#) specified, set precision to zero...
                if (_opts.AltForm) {
                    _opts.Precision = 0;
                    forceDot = true;
                } else _opts.Precision = 6;
            }

            type = AdjustForG(type, v);
            _nfi.NumberDecimalDigits = _opts.Precision;

            // then append
            if (_opts.LeftAdj) {
                AppendLeftAdj(v, DoubleOps.Sign(v) >= 0, type);
            } else if (_opts.ZeroPad) {
                AppendZeroPadFloat(v, type);
            } else {
                AppendNumeric(v, DoubleOps.Sign(v) >= 0, type);
            }
            if (DoubleOps.Sign(v) < 0 && v > -1 && _buf[0] != '-') {
                FixupFloatMinus();
            }

            if (forceDot) {
                FixupAltFormDot(v);
            }
        }

        private void FixupAltFormDot(double v) {
            if (!double.IsInfinity(v) && !double.IsNaN(v)) {
                _buf.Append('.');
            }
            if (_opts.FieldWidth != 0) {
                // try and remove the extra character we're adding.
                for (int i = 0; i < _buf.Length; i++) {
                    if (_buf[i] == ' ' || _buf[i] == '0') {
                        _buf.Remove(i, 1);
                        break;
                    } else if (_buf[i] != '-' && _buf[i] != '+') {
                        break;
                    }
                }
            }
        }

        private void FixupFloatMinus() {
            // Python always appends a - even if precision is 0 and the value would appear to be zero.
            bool fNeedMinus = true;
            for (int i = 0; i < _buf.Length; i++) {
                if (_buf[i] != '.' && _buf[i] != '0' && _buf[i] != ' ') {
                    fNeedMinus = false;
                    break;
                }
            }

            if (fNeedMinus) {
                if (_opts.FieldWidth != 0) {
                    // trim us back down to the correct field width...
                    if (_buf[_buf.Length - 1] == ' ') {
                        _buf.Insert(0, "-");
                        _buf.Remove(_buf.Length - 1, 1);
                    } else {
                        int index = 0;
                        while (_buf[index] == ' ') index++;
                        if (index > 0) index--;
                        _buf[index] = '-';
                    }
                } else {
                    _buf.Insert(0, "-");
                }
            }
        }

        private void AppendZeroPad(object val, bool fPos, char format) {
            if (fPos && (_opts.SignChar || _opts.Space)) {
                // produce [' '|'+']0000digits
                // first get 0 padded number to field width
                string res = String.Format(_nfi, "{0:" + format + _opts.FieldWidth.ToString() + "}", val);

                char signOrSpace = _opts.SignChar ? '+' : ' ';
                // then if we ended up with a leading zero replace it, otherwise
                // append the space / + to the front.
                if (res[0] == '0' && res.Length > 1) {
                    res = signOrSpace + res.Substring(1);
                } else {
                    res = signOrSpace + res;
                }
                _buf.Append(res);
            } else {
                string res = String.Format(_nfi, "{0:" + format + _opts.FieldWidth.ToString() + "}", val);

                // Difference: 
                //   System.String.Format("{0:D3}", -1)      '-001'
                //   "%03d" % -1                             '-01'

                if (res[0] == '-') {
                    // negative
                    _buf.Append("-");
                    if (res[1] != '0') {
                        _buf.Append(res.Substring(1));
                    } else {
                        _buf.Append(res.Substring(2));
                    }
                } else {
                    // positive
                    _buf.Append(res);
                }
            }
        }

        private void AppendZeroPadFloat(double val, char format) {
            if (val >= 0) {
                StringBuilder res = new StringBuilder(val.ToString(format.ToString(), _nfi));
                if (res.Length < _opts.FieldWidth) {
                    res.Insert(0, new string('0', _opts.FieldWidth - res.Length));
                }
                if (_opts.SignChar || _opts.Space) {
                    char signOrSpace = _opts.SignChar ? '+' : ' ';
                    // then if we ended up with a leading zero replace it, otherwise
                    // append the space / + to the front.
                    if (res[0] == '0' && res[1] != '.') {
                        res[0] = signOrSpace;
                    } else {
                        res.Insert(0, signOrSpace.ToString());
                    }
                }
                _buf.Append(res);
            } else {
                StringBuilder res = new StringBuilder(val.ToString(format.ToString(), _nfi));
                if (res.Length < _opts.FieldWidth) {
                    res.Insert(1, new string('0', _opts.FieldWidth - res.Length));
                }
                _buf.Append(res);
            }
        }

        private void AppendNumeric(object val, bool fPos, char format) {
            if (format == 'e' || format == 'E') {
                AppendNumericExp(val, fPos, format);
            } else {
                // f, F, g, D
                AppendNumericDecimal(val, fPos, format);
            }
        }

        private void AppendNumericExp(object val, bool fPos, char format) {
            Debug.Assert(_opts.Precision != UnspecifiedPrecision);
            var forceMinus = false;
            double doubleVal = (double)val;
            if (IsNegativeZero(doubleVal)) {
                forceMinus = true;
            }
            if (fPos && (_opts.SignChar || _opts.Space)) {
                string strval = (_opts.SignChar ? "+" : " ") + String.Format(_nfi, "{0:" + format + _opts.Precision + "}", val);
                strval = adjustExponent(strval);
                if (strval.Length < _opts.FieldWidth) {
                    _buf.Append(' ', _opts.FieldWidth - strval.Length);
                }
                if (forceMinus) {
                    _buf.Append('-');
                }
                _buf.Append(strval);
            } else if (_opts.Precision < 100) {
                //CLR formatting has a maximum precision of 100.
                string num = String.Format(_nfi, "{0," + _opts.FieldWidth + ":" + format + _opts.Precision + "}", val);
                num = adjustExponent(num);
                if (num.Length < _opts.FieldWidth) {
                    _buf.Append(' ', _opts.FieldWidth - num.Length);
                }
                if (forceMinus)
                    _buf.Append('-');
                _buf.Append(num);
            } else {
                AppendNumericCommon(val, format);
            }
        }

        private void AppendNumericDecimal(object val, bool fPos, char format) {
            if (fPos && (_opts.SignChar || _opts.Space)) {
                string strval = (_opts.SignChar ? "+" : " ") + String.Format(_nfi, "{0:" + format + "}", val);
                if (strval.Length < _opts.FieldWidth) {
                    _buf.Append(' ', _opts.FieldWidth - strval.Length);
                }
                _buf.Append(strval);
            } else if (_opts.Precision == UnspecifiedPrecision) {
                _buf.AppendFormat(_nfi, "{0," + _opts.FieldWidth + ":" + format + "}", val);
            } else if (_opts.Precision < 100) {
                //CLR formatting has a maximum precision of 100.
                _buf.Append(String.Format(_nfi, "{0," + _opts.FieldWidth + ":" + format + _opts.Precision + "}", val));
            } else {
                AppendNumericCommon(val, format);
            }

            // If AdjustForG() sets opts.Precision == 0, it means that no significant digits should be displayed after
            // the decimal point. ie. 123.4 should be displayed as "123", not "123.4". However, we might still need a 
            // decorative ".0". ie. to display "123.0"
            if (_TrailingZeroAfterWholeFloat && (format == 'f' || format == 'F') && _opts.Precision == 0)
                _buf.Append(".0");
        }

        private void AppendNumericCommon(object val, char format) {
            StringBuilder res = new StringBuilder();
            res.AppendFormat("{0:" + format + "}", val);
            if (res.Length < _opts.Precision) {
                res.Insert(0, new String('0', _opts.Precision - res.Length));
            }
            if (res.Length < _opts.FieldWidth) {
                res.Insert(0, new String(' ', _opts.FieldWidth - res.Length));
            }
            _buf.Append(res.ToString());
        }

        // A strange string formatting bug requires that we use Standard Numeric Format and
        // not Custom Numeric Format. Standard Numeric Format produces always a 3 digit exponent
        // which needs to be taken care off.
        // Example: 9.3126672485384569e+23, precision=16
        //  format string "e16" ==> "9.3126672485384569e+023", but we want "e+23", not "e+023"
        //  format string "0.0000000000000000e+00" ==> "9.3126672485384600e+23", which is a precision error
        //  so, we have to format with "e16" and strip the zero manually
        private string adjustExponent(string val) {
            if (val[val.Length - 3] == '0') {
                return val.Substring(0, val.Length - 3) + val.Substring(val.Length - 2, 2);
            } else {
                return val;
            }
        }

        private void AppendLeftAdj(object val, bool fPos, char type) {
            var str = (type == 'e' || type == 'E') ?
                adjustExponent(String.Format(_nfi, "{0:" + type + _opts.Precision + "}", val)):
                String.Format(_nfi, "{0:" + type + "}", val);
            if (fPos) {
                if (_opts.SignChar) str = '+' + str;
                else if (_opts.Space) str = ' ' + str;
            }
            _buf.Append(str);
            if (str.Length < _opts.FieldWidth) _buf.Append(' ', _opts.FieldWidth - str.Length);
        }

        private static bool NeedsAltForm(char format, char last) {
            if (format == 'X' || format == 'x') return true;

            if (last == '0') return false;
            return true;
        }

        private static string GetAltFormPrefixForRadix(char format, int radix) {
            switch (radix) {
                case 8: return "0";
                case 16: return format + "0";
            }
            return "";
        }

        /// <summary>
        /// AppendBase appends an integer at the specified radix doing all the
        /// special forms for Python.  We have a copy and paste version of this
        /// for BigInteger below that should be kept in sync.
        /// </summary>
        private void AppendBase(char format, int radix) {
            bool fPos;
            object intVal = GetIntegerValue(out fPos);
            if (intVal is BigInteger) {
                AppendBaseBigInt((BigInteger)intVal, format, radix);
                return;
            }
            int origVal = (int)intVal;
            int val = origVal;
            if (val < 0) {
                val *= -1;

                // if negative number, the leading space has no impact
                _opts.Space = false;
            }
            // we build up the number backwards inside a string builder,
            // and after we've finished building this up we append the
            // string to our output buffer backwards.

            // convert value to string 
            StringBuilder str = new StringBuilder();
            if (val == 0) str.Append('0');
            while (val != 0) {
                int digit = val % radix;
                if (digit < 10) str.Append((char)((digit) + '0'));
                else if (Char.IsLower(format)) str.Append((char)((digit - 10) + 'a'));
                else str.Append((char)((digit - 10) + 'A'));

                val /= radix;
            }

            // pad out for additional precision
            if (str.Length < _opts.Precision) {
                int len = _opts.Precision - str.Length;
                str.Append('0', len);
            }

            // pad result to minimum field width
            if (_opts.FieldWidth != 0) {
                int signLen = (origVal < 0 || _opts.SignChar) ? 1 : 0;
                int spaceLen = _opts.Space ? 1 : 0;
                int len = _opts.FieldWidth - (str.Length + signLen + spaceLen);

                if (len > 0) {
                    // we account for the size of the alternate form, if we'll end up adding it.
                    if (_opts.AltForm && NeedsAltForm(format, (!_opts.LeftAdj && _opts.ZeroPad) ? '0' : str[str.Length - 1])) {
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
            if (_opts.AltForm && NeedsAltForm(format, str[str.Length - 1]))
                str.Append(GetAltFormPrefixForRadix(format, radix));


            // add any sign if necessary
            if (origVal < 0) {
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
        }

        /// <summary>
        /// BigInteger version of AppendBase.  Should be kept in sync w/ AppendBase
        /// </summary>
        private void AppendBaseBigInt(BigInteger origVal, char format, int radix) {
            BigInteger val = origVal;
            if (val < 0) val *= -1;

            // convert value to octal

            StringBuilder str = new StringBuilder();
            // use .NETs faster conversion if we can
            if (radix == 16) {
                AppendNumberReversed(str, Char.IsLower(format) ? val.ToString("x") : val.ToString("X"));
            } else if (radix == 10) {
                AppendNumberReversed(str, val.ToString());
            } else {
                if (val == 0) str.Append('0');
                while (val != 0) {
                    int digit = (int)(val % radix);
                    if (digit < 10) str.Append((char)((digit) + '0'));
                    else if (Char.IsLower(format)) str.Append((char)((digit - 10) + 'a'));
                    else str.Append((char)((digit - 10) + 'A'));

                    val /= radix;
                }
            }

            // pad out for additional precision
            if (str.Length < _opts.Precision) {
                int len = _opts.Precision - str.Length;
                str.Append('0', len);
            }

            // pad result to minimum field width
            if (_opts.FieldWidth != 0) {
                int signLen = (origVal < 0 || _opts.SignChar) ? 1 : 0;
                int len = _opts.FieldWidth - (str.Length + signLen);
                if (len > 0) {
                    // we account for the size of the alternate form, if we'll end up adding it.
                    if (_opts.AltForm && NeedsAltForm(format, (!_opts.LeftAdj && _opts.ZeroPad) ? '0' : str[str.Length - 1])) {
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
            if (_opts.AltForm && NeedsAltForm(format, str[str.Length - 1]))
                str.Append(GetAltFormPrefixForRadix(format, radix));


            // add any sign if necessary
            if (origVal < 0) {
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
            string s;
            if (!_isUnicodeString) {
                s = PythonOps.ToString(_context, _opts.Value);
            } else {
                object o = StringOps.FastNewUnicode(_context, _opts.Value);
                s = o as string;
                if (s == null) {
                    s = ((Extensible<string>)o).Value;
                }
            }

            if (s == null) s = "None";
            AppendString(s);
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

        private static readonly long NegativeZeroBits =  BitConverter.DoubleToInt64Bits(-0.0);

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
