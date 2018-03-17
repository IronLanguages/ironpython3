/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

[assembly: PythonModule("_struct", typeof(IronPython.Modules.PythonStruct))]
namespace IronPython.Modules {
    public static class PythonStruct {
        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, PythonDictionary/*!*/ dict) {
            context.EnsureModuleException("structerror", dict, "error", "struct");
        }

        #region Public API

        public const string __doc__ = null;
        public const string __version__ = "0.2";

        public const int _PY_STRUCT_FLOAT_COERCE = 0;
        public const int _PY_STRUCT_OVERFLOW_MASKING = 0;
        public const int _PY_STRUCT_RANGE_CHECKING = 0;

        [PythonType, Documentation("Represents a compiled struct pattern")]
        public class Struct : IWeakReferenceable {
            private string _formatString;           // the last format string passed to __init__
            private Format[] _formats;              // the various formatting options for the compiled struct
            private bool _isStandardized;           // true if the format is in standardized mode
            private bool _isLittleEndian;           // true if the format is in little endian mode
            private int _encodingCount = -1;        // the number of objects consumed/produced by the format
            private int _encodingSize = -1;         // the number of bytes read/produced by the format
            private WeakRefTracker _tracker;        // storage for weak proxy's

            private void Initialize(Struct s) {
                _formatString = s._formatString;
                _formats = s._formats;
                _isStandardized = s._isStandardized;
                _isLittleEndian = s._isLittleEndian;
                _encodingCount = s._encodingCount;
                _encodingSize = s._encodingSize;
                _tracker = s._tracker;
            }

            internal Struct(CodeContext/*!*/ context, [NotNull]string/*!*/ fmt) {
                __init__(context, fmt);
            }

            #region Python construction

            [Documentation("creates a new uninitialized struct object - all arguments are ignored")]
            public Struct(params object[] args) {
            }

            [Documentation("creates a new uninitialized struct object - all arguments are ignored")]
            public Struct([ParamDictionary]IDictionary<object, object> kwArgs, params object[] args) {
            }

            [Documentation("initializes or re-initializes the compiled struct object with a new format")]
            public void __init__(CodeContext/*!*/ context, [NotNull]string/*!*/ fmt) {
                ContractUtils.RequiresNotNull(fmt, nameof(fmt));

                _formatString = fmt;

                Struct s;
                bool gotIt;
                lock (_cache) {
                    gotIt = _cache.TryGetValue(_formatString, out s);
                }
                if (gotIt) {
                    Initialize(s);
                } else {
                    Compile(context, fmt);
                }
            }

            #endregion

            #region Public API

            [Documentation("gets the current format string for the compiled Struct")]
            public string format {
                get {
                    return _formatString;
                }
            }

            [Documentation("returns a string consisting of the values serialized according to the format of the struct object")]
            public string/*!*/ pack(CodeContext/*!*/ context, params object[] values) {
                if (values.Length != _encodingCount) {
                    throw Error(context, String.Format("pack requires exactly {0} arguments", _encodingCount));
                }

                int curObj = 0;
                StringBuilder res = new StringBuilder(_encodingSize);

                for (int i = 0; i < _formats.Length; i++) {
                    Format curFormat = _formats[i];
                    if (!_isStandardized) {
                        // In native mode, align to {size}-byte boundaries
                        int nativeSize = curFormat.NativeSize;

                        int alignLength = Align(res.Length, nativeSize);
                        int padLength = alignLength - res.Length;
                        for (int j = 0; j < padLength; j++) {
                            res.Append('\0');
                        }
                    }

                    switch (curFormat.Type) {
                        case FormatType.PadByte:
                            res.Append('\0', curFormat.Count);
                            break;
                        case FormatType.Bool:
                            res.Append(GetBoolValue(context, curObj++, values) ? (char)1 : '\0');
                            break;
                        case FormatType.Char:
                            for (int j = 0; j < curFormat.Count; j++) {
                                res.Append(GetCharValue(context, curObj++, values));
                            }
                            break;
                        case FormatType.SignedChar:
                            for (int j = 0; j < curFormat.Count; j++) {
                                res.Append((char)(byte)GetSByteValue(context, curObj++, values));
                            }
                            break;
                        case FormatType.UnsignedChar:
                            for (int j = 0; j < curFormat.Count; j++) {
                                res.Append((char)GetByteValue(context, curObj++, values));
                            }
                            break;
                        case FormatType.Short:
                            for (int j = 0; j < curFormat.Count; j++) {
                                WriteShort(res, _isLittleEndian, GetShortValue(context, curObj++, values));
                            }
                            break;
                        case FormatType.UnsignedShort:
                            for (int j = 0; j < curFormat.Count; j++) {
                                WriteUShort(res, _isLittleEndian, GetUShortValue(context, curObj++, values));
                            }
                            break;
                        case FormatType.Int:
                            for (int j = 0; j < curFormat.Count; j++) {
                                WriteInt(res, _isLittleEndian, GetIntValue(context, curObj++, values));
                            }
                            break;
                        case FormatType.UnsignedInt:
                            for (int j = 0; j < curFormat.Count; j++) {
                                WriteUInt(res, _isLittleEndian, GetULongValue(context, curObj++, values, "unsigned int"));
                            }
                            break;
                        case FormatType.UnsignedLong:
                            for (int j = 0; j < curFormat.Count; j++) {
                                WriteUInt(res, _isLittleEndian, GetULongValue(context, curObj++, values, "unsigned long"));
                            }
                            break;
                        case FormatType.Pointer:
                            for (int j = 0; j < curFormat.Count; j++) {
                                WritePointer(res, _isLittleEndian, GetPointer(context, curObj++, values));
                            }
                            break;
                        case FormatType.LongLong:
                            for (int j = 0; j < curFormat.Count; j++) {
                                WriteLong(res, _isLittleEndian, GetLongValue(context, curObj++, values));
                            }
                            break;
                        case FormatType.UnsignedLongLong:
                            for (int j = 0; j < curFormat.Count; j++) {
                                WriteULong(res, _isLittleEndian, GetULongLongValue(context, curObj++, values));
                            }
                            break;
                        case FormatType.Double:
                            for (int j = 0; j < curFormat.Count; j++) {
                                WriteDouble(res, _isLittleEndian, GetDoubleValue(context, curObj++, values));
                            }
                            break;
                        case FormatType.Float:
                            for (int j = 0; j < curFormat.Count; j++) {
                                WriteFloat(res, _isLittleEndian, (float)GetDoubleValue(context, curObj++, values));
                            }
                            break;
                        case FormatType.CString:
                            WriteString(res, curFormat.Count, GetStringValue(context, curObj++, values));
                            break;
                        case FormatType.PascalString:
                            WritePascalString(res, curFormat.Count - 1, GetStringValue(context, curObj++, values));
                            break;
                        default:
                            throw Error(context, "bad format string");
                    }
                }

                return res.ToString();
            }

            [Documentation("Stores the deserialized data into the provided array")]
            public void pack_into(CodeContext/*!*/ context, [NotNull]ArrayModule.array/*!*/ buffer, int offset, params object[] args) {
                byte[] existing = buffer.ToByteArray();

                if (offset + size > existing.Length) {
                    throw Error(context, String.Format("pack_into requires a buffer of at least {0} bytes", size));
                }

                string data = pack(context, args);

                for (int i = 0; i < data.Length; i++) {
                    existing[i + offset] = (byte)data[i];
                }

                buffer.Clear();
                buffer.FromStream(new MemoryStream(existing));
            }

            [Documentation("deserializes the string using the structs specified format")]
            public PythonTuple/*!*/ unpack(CodeContext/*!*/ context, [NotNull]string @string) {
                if (@string.Length != size) {
                    throw Error(context, $"unpack requires a string argument of length {size}");
                }

                string data = @string;
                int curIndex = 0;
                var res = new object[_encodingCount];
                var res_idx = 0;

                for (int i = 0; i < _formats.Length; i++) {
                    Format curFormat = _formats[i];

                    if (!_isStandardized) {
                        // In native mode, align to {size}-byte boundaries
                        int nativeSize = curFormat.NativeSize;
                        if (nativeSize > 0) {
                            curIndex = Align(curIndex, nativeSize);
                        }
                    }

                    switch (curFormat.Type) {
                        case FormatType.PadByte:
                            curIndex += curFormat.Count;
                            break;
                        case FormatType.Bool:
                            for (int j = 0; j < curFormat.Count; j++) {
                                res[res_idx++] = CreateBoolValue(context, ref curIndex, data);
                            }
                            break;
                        case FormatType.Char:
                            for (int j = 0; j < curFormat.Count; j++) {
                                res[res_idx++] = CreateCharValue(context, ref curIndex, data).ToString();
                            }
                            break;
                        case FormatType.SignedChar:
                            for (int j = 0; j < curFormat.Count; j++) {
                                res[res_idx++] = (int)(sbyte)CreateCharValue(context, ref curIndex, data);
                            }
                            break;
                        case FormatType.UnsignedChar:
                            for (int j = 0; j < curFormat.Count; j++) {
                                res[res_idx++] = (int)CreateCharValue(context, ref curIndex, data);
                            }
                            break;
                        case FormatType.Short:
                            for (int j = 0; j < curFormat.Count; j++) {
                                res[res_idx++] = (int)CreateShortValue(context, ref curIndex, _isLittleEndian, data);
                            }
                            break;
                        case FormatType.UnsignedShort:
                            for (int j = 0; j < curFormat.Count; j++) {
                                res[res_idx++] = (int)CreateUShortValue(context, ref curIndex, _isLittleEndian, data);
                            }
                            break;
                        case FormatType.Int:
                            for (int j = 0; j < curFormat.Count; j++) {
                                res[res_idx++] = CreateIntValue(context, ref curIndex, _isLittleEndian, data);
                            }
                            break;
                        case FormatType.UnsignedInt:
                        case FormatType.UnsignedLong:
                            for (int j = 0; j < curFormat.Count; j++) {
                                res[res_idx++] = BigIntegerOps.__int__(CreateUIntValue(context, ref curIndex, _isLittleEndian, data));
                            }
                            break;
                        case FormatType.Pointer:
                            for (int j = 0; j < curFormat.Count; j++) {
                                if (IntPtr.Size == 4) {
                                    res[res_idx++] = CreateIntValue(context, ref curIndex, _isLittleEndian, data);
                                } else {
                                    res[res_idx++] = BigIntegerOps.__int__(CreateLongValue(context, ref curIndex, _isLittleEndian, data));
                                }
                            }
                            break;
                        case FormatType.LongLong:
                            for (int j = 0; j < curFormat.Count; j++) {
                                res[res_idx++] = BigIntegerOps.__int__(CreateLongValue(context, ref curIndex, _isLittleEndian, data));
                            }
                            break;
                        case FormatType.UnsignedLongLong:
                            for (int j = 0; j < curFormat.Count; j++) {
                                res[res_idx++] = BigIntegerOps.__int__(CreateULongValue(context, ref curIndex, _isLittleEndian, data));
                            }
                            break;
                        case FormatType.Float:
                            for (int j = 0; j < curFormat.Count; j++) {
                                res[res_idx++] = CreateFloatValue(context, ref curIndex, _isLittleEndian, data);
                            }
                            break;
                        case FormatType.Double:
                            for (int j = 0; j < curFormat.Count; j++) {
                                res[res_idx++] = CreateDoubleValue(context, ref curIndex, _isLittleEndian, data);
                            }
                            break;
                        case FormatType.CString:
                            res[res_idx++] = CreateString(context, ref curIndex, curFormat.Count, data);
                            break;
                        case FormatType.PascalString:
                            res[res_idx++] = CreatePascalString(context, ref curIndex, curFormat.Count - 1, data);
                            break;
                    }
                }

                System.Diagnostics.Debug.Assert(res_idx == res.Length);

                return PythonTuple.MakeTuple(res);
            }

            public PythonTuple/*!*/ unpack(CodeContext/*!*/ context, [BytesConversion][NotNull]IList<byte> buffer) {
                return unpack_from(context, buffer, 0);
            }

            public PythonTuple/*!*/ unpack(CodeContext/*!*/ context, [NotNull]ArrayModule.array/*!*/ buffer) {
                return unpack_from(context, buffer, 0);
            }

            public PythonTuple/*!*/ unpack(CodeContext/*!*/ context, [NotNull]PythonBuffer/*!*/ buffer) {
                return unpack_from(context, buffer, 0);
            }

            [Documentation("reads the current format from the specified string")]
            public PythonTuple/*!*/ unpack_from(CodeContext/*!*/ context, [NotNull]string/*!*/ buffer, int offset=0) {
                int bytesAvail = buffer.Length - offset;
                if (bytesAvail < size) {
                    throw Error(context, String.Format("unpack_from requires a buffer of at least {0} bytes", size));
                }

                return unpack(context, buffer.Substring(offset, size));
            }

            [Documentation("reads the current format from the specified array")]
            public PythonTuple/*!*/ unpack_from(CodeContext/*!*/ context, [BytesConversion][NotNull]IList<byte>/*!*/ buffer, [DefaultParameterValue(0)] int offset) {
                return unpack_from(context, buffer.MakeString(), offset);
            }

            [Documentation("reads the current format from the specified array")]
            public PythonTuple/*!*/ unpack_from(CodeContext/*!*/ context, [NotNull]ArrayModule.array/*!*/ buffer, int offset = 0) {
                return unpack_from(context, buffer.ToByteArray().MakeString(), offset);
            }

            [Documentation("reads the current format from the specified buffer object")]
            public PythonTuple/*!*/ unpack_from(CodeContext/*!*/ context, [NotNull]PythonBuffer/*!*/ buffer, int offset=0) {
                return unpack_from(context, buffer.ToString(), offset);
            }

            [Documentation("gets the number of bytes that the serialized string will occupy or are required to deserialize the data")]
            public int size {
                get {
                    return _encodingSize;
                }
            }

            #endregion

            #region IWeakReferenceable Members

            WeakRefTracker IWeakReferenceable.GetWeakRef() {
                return _tracker;
            }

            bool IWeakReferenceable.SetWeakRef(WeakRefTracker value) {
                return Interlocked.CompareExchange(ref _tracker, value, null) == null;
            }

            void IWeakReferenceable.SetFinalizer(WeakRefTracker value) {
                _tracker = value;
            }

            #endregion

            #region Implementation Details

            private void Compile(CodeContext/*!*/ context, string/*!*/ fmt) {
                List<Format> res = new List<Format>();
                int count = 1;
                bool fLittleEndian = BitConverter.IsLittleEndian;
                bool fStandardized = false;

                for (int i = 0; i < fmt.Length; i++) {
                    switch (fmt[i]) {
                        case 'x': // pad byte
                            res.Add(new Format(FormatType.PadByte, count));
                            count = 1;
                            break;
                        case '?': // bool
                            res.Add(new Format(FormatType.Bool, count));
                            count = 1;
                            break;
                        case 'c': // char
                            res.Add(new Format(FormatType.Char, count));
                            count = 1;
                            break;
                        case 'b': // signed char
                            res.Add(new Format(FormatType.SignedChar, count));
                            count = 1;
                            break;
                        case 'B': // unsigned char
                            res.Add(new Format(FormatType.UnsignedChar, count));
                            count = 1;
                            break;
                        case 'h': // short
                            res.Add(new Format(FormatType.Short, count));
                            count = 1;
                            break;
                        case 'H': // unsigned short
                            res.Add(new Format(FormatType.UnsignedShort, count));
                            count = 1;
                            break;
                        case 'i': // int
                        case 'l': // long
                            res.Add(new Format(FormatType.Int, count));
                            count = 1;
                            break;
                        case 'I': // unsigned int
                            res.Add(new Format(FormatType.UnsignedInt, count));
                            count = 1;
                            break;
                        case 'L': // unsigned long
                            res.Add(new Format(FormatType.UnsignedLong, count));
                            count = 1;
                            break;
                        case 'q': // long long
                            res.Add(new Format(FormatType.LongLong, count));
                            count = 1;
                            break;
                        case 'Q': // unsigned long long
                            res.Add(new Format(FormatType.UnsignedLongLong, count));
                            count = 1;
                            break;
                        case 'f': // float                        
                            res.Add(new Format(FormatType.Float, count));
                            count = 1;
                            break;
                        case 'd': // double
                            res.Add(new Format(FormatType.Double, count));
                            count = 1;
                            break;
                        case 's': // char[]
                            res.Add(new Format(FormatType.CString, count));
                            count = 1;
                            break;
                        case 'p': // pascal char[]
                            res.Add(new Format(FormatType.PascalString, count));
                            count = 1;
                            break;
                        case 'P': // void *
                            res.Add(new Format(FormatType.Pointer, count));
                            count = 1;
                            break;
                        case ' ':   // white space, ignore
                        case '\t':
                            break;
                        case '=': // native
                            if (i != 0) throw Error(context, "unexpected byte order");
                            fStandardized = true;
                            break;
                        case '@': // native
                            if (i != 0) throw Error(context, "unexpected byte order");
                            break;
                        case '<': // little endian
                            if (i != 0) throw Error(context, "unexpected byte order");
                            fLittleEndian = true;
                            fStandardized = true;
                            break;
                        case '>': // big endian
                        case '!': // big endian
                            if (i != 0) throw Error(context, "unexpected byte order");
                            fLittleEndian = false;
                            fStandardized = true;
                            break;
                        default:
                            if (Char.IsDigit(fmt[i])) {
                                count = 0;
                                while (Char.IsDigit(fmt[i])) {
                                    count = count * 10 + (fmt[i] - '0');
                                    i++;
                                }
                                if (Char.IsWhiteSpace(fmt[i])) Error(context, "white space not allowed between count and format");
                                i--;
                                break;
                            }

                            throw Error(context, "bad format string");
                    }
                }

                // store the new formats
                _formats = res.ToArray();
                _isStandardized = fStandardized;
                _isLittleEndian = fLittleEndian;
                _encodingSize = _encodingCount = 0;
                for (int i = 0; i < _formats.Length; i++) {
                    if (_formats[i].Type != FormatType.PadByte) {
                        if (_formats[i].Type != FormatType.CString && _formats[i].Type != FormatType.PascalString) {
                            _encodingCount += _formats[i].Count;
                        } else {
                            _encodingCount++;
                        }
                    }

                    if (!_isStandardized) {
                        // In native mode, align to {size}-byte boundaries
                        _encodingSize = Align(_encodingSize, _formats[i].NativeSize);
                    }

                    _encodingSize += GetNativeSize(_formats[i].Type) * _formats[i].Count;
                }
                lock (_cache) {
                    _cache.Add(fmt, this);
                }
            }

            #endregion

            #region Internal helpers

            internal static Struct Create(string/*!*/ format) {
                Struct res = new Struct();
                res.__init__(DefaultContext.Default, format);   // default context is only used for errors, this better be an error free format.
                return res;
            }

            #endregion
        }

        #endregion

        #region Compiled Format

        /// <summary>
        /// Enum which specifies the format type for a compiled struct
        /// </summary>
        private enum FormatType {
            None,

            PadByte,
            Bool,
            Char,
            SignedChar,
            UnsignedChar,

            Short,
            UnsignedShort,

            Int,
            UnsignedInt,
            UnsignedLong,
            Float,

            LongLong,
            UnsignedLongLong,
            Double,

            CString,
            PascalString,
            Pointer,
        }

        private static int GetNativeSize(FormatType c) {
            switch (c) {
                case FormatType.Char:
                case FormatType.SignedChar:
                case FormatType.UnsignedChar:
                case FormatType.PadByte:
                case FormatType.Bool:
                case FormatType.CString:
                case FormatType.PascalString:
                    return 1;
                case FormatType.Short:
                case FormatType.UnsignedShort:
                    return 2;
                case FormatType.Int:
                case FormatType.UnsignedInt:
                case FormatType.UnsignedLong:
                case FormatType.Float:
                    return 4;
                case FormatType.LongLong:
                case FormatType.UnsignedLongLong:
                case FormatType.Double:
                    return 8;
                case FormatType.Pointer:
                    return IntPtr.Size;
                default:
                    throw new InvalidOperationException(c.ToString());
            }
        }

        /// <summary>
        /// Struct used to store the format and the number of times it should be repeated.
        /// </summary>
        private struct Format {
            public FormatType Type;
            public int Count;

            public Format(FormatType type, int count) {
                Type = type;
                Count = count;
            }

            public int NativeSize {
                get {
                    return GetNativeSize(Type);
                }
            }
        }

        #endregion

        #region Cache of compiled struct patterns

        private const int MAX_CACHE_SIZE = 1024;
        private static CacheDict<string, Struct> _cache = new CacheDict<string, Struct>(MAX_CACHE_SIZE);

        private static Struct GetStructFromCache(CodeContext/*!*/ context, [NotNull] string fmt/*!*/) {
            Struct s;
            bool gotIt;
            lock (_cache) {
                gotIt = _cache.TryGetValue(fmt, out s);
            }
            if (!gotIt) {
                s = new Struct(context, fmt);
            }
            return s;
        }

        [Documentation("Clear the internal cache.")]
        public static void _clearcache() {
            _cache = new CacheDict<string, Struct>(MAX_CACHE_SIZE);
        }

        [Documentation("int(x[, base]) -> integer\n\nConvert a string or number to an integer, if possible.  A floating point\nargument will be truncated towards zero (this does not include a string\nrepresentation of a floating point number!)  When converting a string, use\nthe optional base.  It is an error to supply a base when converting a\nnon-string.  If base is zero, the proper base is guessed based on the\nstring content.  If the argument is outside the integer range a\nlong object will be returned instead.")]
        public static int calcsize(CodeContext/*!*/ context, [BytesConversion][NotNull]string fmt) {
            return GetStructFromCache(context, fmt).size;
        }

        [Documentation("Return string containing values v1, v2, ... packed according to fmt.")]
        public static string/*!*/ pack(CodeContext/*!*/ context, [BytesConversion][NotNull]string fmt/*!*/, params object[] values) {
            return GetStructFromCache(context, fmt).pack(context, values);
        }

        [Documentation("Pack the values v1, v2, ... according to fmt.\nWrite the packed bytes into the writable buffer buf starting at offset.")]
        public static void pack_into(CodeContext/*!*/ context, [BytesConversion][NotNull]string/*!*/ fmt, [NotNull]ArrayModule.array/*!*/ buffer, int offset, params object[] args) {
            GetStructFromCache(context, fmt).pack_into(context, buffer, offset, args);
        }

        [Documentation("Unpack the string containing packed C structure data, according to fmt.\nRequires len(string) == calcsize(fmt).")]
        public static PythonTuple/*!*/ unpack(CodeContext/*!*/ context, [BytesConversion][NotNull]string/*!*/ fmt, [NotNull]string/*!*/ @string) {
            return GetStructFromCache(context, fmt).unpack(context, @string);
        }

        [Documentation("Unpack the string containing packed C structure data, according to fmt.\nRequires len(string) == calcsize(fmt).")]
        public static PythonTuple/*!*/ unpack(CodeContext/*!*/ context, [BytesConversion][NotNull]string/*!*/ fmt, [BytesConversion][NotNull]IList<byte>/*!*/ buffer) {
            return GetStructFromCache(context, fmt).unpack(context, buffer);
        }

        [Documentation("Unpack the string containing packed C structure data, according to fmt.\nRequires len(string) == calcsize(fmt).")]
        public static PythonTuple/*!*/ unpack(CodeContext/*!*/ context, [BytesConversion][NotNull]string/*!*/ fmt, [NotNull]ArrayModule.array/*!*/ buffer) {
            return GetStructFromCache(context, fmt).unpack(context, buffer);
        }

        [Documentation("Unpack the string containing packed C structure data, according to fmt.\nRequires len(string) == calcsize(fmt).")]
        public static PythonTuple/*!*/ unpack(CodeContext/*!*/ context, [BytesConversion][NotNull]string fmt/*!*/, [NotNull]PythonBuffer/*!*/ buffer) {
            return GetStructFromCache(context, fmt).unpack(context, buffer);
        }

        [Documentation("Unpack the buffer, containing packed C structure data, according to\nfmt, starting at offset. Requires len(buffer[offset:]) >= calcsize(fmt).")]
        public static PythonTuple/*!*/ unpack_from(CodeContext/*!*/ context, [BytesConversion][NotNull]string fmt/*!*/, [NotNull]string/*!*/ buffer, int offset = 0) {
            return GetStructFromCache(context, fmt).unpack_from(context, buffer, offset);
        }

        [Documentation("Unpack the buffer, containing packed C structure data, according to\nfmt, starting at offset. Requires len(buffer[offset:]) >= calcsize(fmt).")]
        public static PythonTuple/*!*/ unpack_from(CodeContext/*!*/ context, [BytesConversion][NotNull]string fmt/*!*/, [BytesConversion][NotNull]IList<byte>/*!*/ buffer, int offset = 0) {
            return GetStructFromCache(context, fmt).unpack_from(context, buffer, offset);
        }

        [Documentation("Unpack the buffer, containing packed C structure data, according to\nfmt, starting at offset. Requires len(buffer[offset:]) >= calcsize(fmt).")]
        public static PythonTuple/*!*/ unpack_from(CodeContext/*!*/ context, [BytesConversion][NotNull]string fmt/*!*/, [NotNull]ArrayModule.array/*!*/ buffer, int offset = 0) {
            return GetStructFromCache(context, fmt).unpack_from(context, buffer, offset);
        }

        [Documentation("Unpack the buffer, containing packed C structure data, according to\nfmt, starting at offset. Requires len(buffer[offset:]) >= calcsize(fmt).")]
        public static PythonTuple/*!*/ unpack_from(CodeContext/*!*/ context, [BytesConversion][NotNull]string fmt/*!*/, [NotNull]PythonBuffer/*!*/ buffer, int offset = 0) {
            return GetStructFromCache(context, fmt).unpack_from(context, buffer, offset);
        }

        #endregion

        #region Write Helpers

        private static void WriteShort(StringBuilder res, bool fLittleEndian, short val) {
            if (fLittleEndian) {
                res.Append((char)(val & 0xff));
                res.Append((char)((val >> 8) & 0xff));
            } else {
                res.Append((char)((val >> 8) & 0xff));
                res.Append((char)(val & 0xff));
            }
        }

        private static void WriteUShort(StringBuilder res, bool fLittleEndian, ushort val) {
            if (fLittleEndian) {
                res.Append((char)(val & 0xff));
                res.Append((char)((val >> 8) & 0xff));
            } else {
                res.Append((char)((val >> 8) & 0xff));
                res.Append((char)(val & 0xff));
            }
        }

        private static void WriteInt(StringBuilder res, bool fLittleEndian, int val) {
            if (fLittleEndian) {
                res.Append((char)(val & 0xff));
                res.Append((char)((val >> 8) & 0xff));
                res.Append((char)((val >> 16) & 0xff));
                res.Append((char)((val >> 24) & 0xff));
            } else {
                res.Append((char)((val >> 24) & 0xff));
                res.Append((char)((val >> 16) & 0xff));
                res.Append((char)((val >> 8) & 0xff));
                res.Append((char)(val & 0xff));
            }
        }

        private static void WriteUInt(StringBuilder res, bool fLittleEndian, uint val) {
            if (fLittleEndian) {
                res.Append((char)(val & 0xff));
                res.Append((char)((val >> 8) & 0xff));
                res.Append((char)((val >> 16) & 0xff));
                res.Append((char)((val >> 24) & 0xff));
            } else {
                res.Append((char)((val >> 24) & 0xff));
                res.Append((char)((val >> 16) & 0xff));
                res.Append((char)((val >> 8) & 0xff));
                res.Append((char)(val & 0xff));
            }
        }

        private static void WritePointer(StringBuilder res, bool fLittleEndian, IntPtr val) {
            if (IntPtr.Size == 4) {
                WriteInt(res, fLittleEndian, val.ToInt32());
            } else {
                WriteLong(res, fLittleEndian, val.ToInt64());
            }
        }

        private static void WriteFloat(StringBuilder res, bool fLittleEndian, float val) {
            byte[] bytes = BitConverter.GetBytes(val);
            if (fLittleEndian) {
                res.Append((char)bytes[0]);
                res.Append((char)bytes[1]);
                res.Append((char)bytes[2]);
                res.Append((char)bytes[3]);
            } else {
                res.Append((char)bytes[3]);
                res.Append((char)bytes[2]);
                res.Append((char)bytes[1]);
                res.Append((char)bytes[0]);
            }
        }

        private static void WriteLong(StringBuilder res, bool fLittleEndian, long val) {
            if (fLittleEndian) {
                res.Append((char)(val & 0xff));
                res.Append((char)((val >> 8) & 0xff));
                res.Append((char)((val >> 16) & 0xff));
                res.Append((char)((val >> 24) & 0xff));
                res.Append((char)((val >> 32) & 0xff));
                res.Append((char)((val >> 40) & 0xff));
                res.Append((char)((val >> 48) & 0xff));
                res.Append((char)((val >> 56) & 0xff));
            } else {
                res.Append((char)((val >> 56) & 0xff));
                res.Append((char)((val >> 48) & 0xff));
                res.Append((char)((val >> 40) & 0xff));
                res.Append((char)((val >> 32) & 0xff));
                res.Append((char)((val >> 24) & 0xff));
                res.Append((char)((val >> 16) & 0xff));
                res.Append((char)((val >> 8) & 0xff));
                res.Append((char)(val & 0xff));
            }
        }

        private static void WriteULong(StringBuilder res, bool fLittleEndian, ulong val) {
            if (fLittleEndian) {
                res.Append((char)(val & 0xff));
                res.Append((char)((val >> 8) & 0xff));
                res.Append((char)((val >> 16) & 0xff));
                res.Append((char)((val >> 24) & 0xff));
                res.Append((char)((val >> 32) & 0xff));
                res.Append((char)((val >> 40) & 0xff));
                res.Append((char)((val >> 48) & 0xff));
                res.Append((char)((val >> 56) & 0xff));
            } else {
                res.Append((char)((val >> 56) & 0xff));
                res.Append((char)((val >> 48) & 0xff));
                res.Append((char)((val >> 40) & 0xff));
                res.Append((char)((val >> 32) & 0xff));
                res.Append((char)((val >> 24) & 0xff));
                res.Append((char)((val >> 16) & 0xff));
                res.Append((char)((val >> 8) & 0xff));
                res.Append((char)(val & 0xff));
            }
        }

        private static void WriteDouble(StringBuilder res, bool fLittleEndian, double val) {
            byte[] bytes = BitConverter.GetBytes(val);
            if (fLittleEndian) {
                res.Append((char)bytes[0]);
                res.Append((char)bytes[1]);
                res.Append((char)bytes[2]);
                res.Append((char)bytes[3]);
                res.Append((char)bytes[4]);
                res.Append((char)bytes[5]);
                res.Append((char)bytes[6]);
                res.Append((char)bytes[7]);
            } else {
                res.Append((char)bytes[7]);
                res.Append((char)bytes[6]);
                res.Append((char)bytes[5]);
                res.Append((char)bytes[4]);
                res.Append((char)bytes[3]);
                res.Append((char)bytes[2]);
                res.Append((char)bytes[1]);
                res.Append((char)bytes[0]);
            }
        }

        private static void WriteString(StringBuilder res, int len, string val) {
            for (int i = 0; i < val.Length && i < len; i++) {
                res.Append(val[i]);
            }
            for (int i = val.Length; i < len; i++) {
                res.Append('\0');
            }
        }

        private static void WritePascalString(StringBuilder res, int len, string val) {
            int lenByte = Math.Min(255, Math.Min(val.Length, len));
            res.Append((char)lenByte);

            for (int i = 0; i < val.Length && i < len; i++) {
                res.Append(val[i]);
            }
            for (int i = val.Length; i < len; i++) {
                res.Append('\0');
            }
        }
        #endregion

        #region Data getter helpers

        internal static bool GetBoolValue(CodeContext/*!*/ context, int index, object[] args) {
            object val = GetValue(context, index, args);

            object res;
            if (Converter.TryConvert(val, typeof(bool), out res)) {
                return (bool)res;
            }
            // Should never happen
            throw Error(context, "expected bool value got " + val.ToString());
        }

        internal static char GetCharValue(CodeContext/*!*/ context, int index, object[] args) {
            object val = GetValue(context, index, args);
            string strVal = val as string;
            if (strVal != null && strVal.Length == 1) return strVal[0];

            IList<Byte> byteVal = val as IList<Byte>;
            if (byteVal == null || byteVal.Count != 1) throw Error(context, "char format requires string of length 1");
            else return (char)byteVal[0];
        }

        internal static sbyte GetSByteValue(CodeContext/*!*/ context, int index, object[] args) {
            object val = GetValue(context, index, args);
            sbyte res;
            if (Converter.TryConvertToSByte(val, out res)) {
                return res;
            }
            throw Error(context, "expected sbyte value got " + val.ToString());
        }

        internal static byte GetByteValue(CodeContext/*!*/ context, int index, object[] args) {
            object val = GetValue(context, index, args);

            byte res;
            if (Converter.TryConvertToByte(val, out res)) return res;

            char cres;
            if (Converter.TryConvertToChar(val, out cres)) return (byte)cres;

            throw Error(context, "expected byte value got " + val.ToString());
        }

        internal static short GetShortValue(CodeContext/*!*/ context, int index, object[] args) {
            object val = GetValue(context, index, args);
            short res;
            if (Converter.TryConvertToInt16(val, out res)) return res;
            throw Error(context, "expected short value");
        }

        internal static ushort GetUShortValue(CodeContext/*!*/ context, int index, object[] args) {
            object val = GetValue(context, index, args);
            ushort res;
            if (Converter.TryConvertToUInt16(val, out res)) return res;
            throw Error(context, "expected ushort value");
        }

        internal static int GetIntValue(CodeContext/*!*/ context, int index, object[] args) {
            object val = GetValue(context, index, args);
            int res;
            if (Converter.TryConvertToInt32(val, out res)) return res;
            throw Error(context, "expected int value");
        }

        internal static uint GetULongValue(CodeContext/*!*/ context, int index, object[] args, string type) {
            object val = GetValue(context, index, args);
            if (val is int) {
                CheckRange(context, (int)val, type);
                return (uint)(int)val;
            } else if (val is BigInteger) {
                CheckRange(context, (BigInteger)val, type);
                return (uint)(BigInteger)val;
            } else if (val is Extensible<int>) {
                CheckRange(context, ((Extensible<int>)val).Value, type);
                return (uint)((Extensible<int>)val).Value;
            } else if (val is Extensible<BigInteger>) {
                CheckRange(context, ((Extensible<BigInteger>)val).Value, type);
                return (uint)((Extensible<BigInteger>)val).Value;
            } else {
                object objres;
                if (PythonTypeOps.TryInvokeUnaryOperator(DefaultContext.Default, val, "__int__", out objres)) {
                    if (objres is int) {
                        CheckRange(context, (int)objres, type);
                        return (uint)(int)objres;
                    }
                }

                uint res;
                if (Converter.TryConvertToUInt32(val, out res)) {
                    return res;
                }
            }

            throw Error(context, "cannot convert argument to integer");
        }

        private static void CheckRange(CodeContext context, int val, string type) {
            if (val < 0) {
                OutOfRange(context, type);
            }
        }

        private static void CheckRange(CodeContext context, BigInteger bi, string type) {
            if (bi < 0 || bi > 4294967295) {
                OutOfRange(context, type);
            }
        }

        private static void OutOfRange(CodeContext context, string type) {
            throw Error(context, string.Format("integer out of range for '{0}' format code", type == "unsigned long" ? "L" : "I"));
        }

        internal static IntPtr GetPointer(CodeContext/*!*/ context, int index, object[] args) {
            object val = GetValue(context, index, args);
            if (IntPtr.Size == 4) {
                uint res;
                if (Converter.TryConvertToUInt32(val, out res)) {
                    return new IntPtr(res);
                }
            } else {
                long res;
                if (Converter.TryConvertToInt64(val, out res)) {
                    return new IntPtr(res);
                }
            }
            throw Error(context, "expected pointer value");
        }

        internal static long GetLongValue(CodeContext/*!*/ context, int index, object[] args) {
            object val = GetValue(context, index, args);
            long res;
            if (Converter.TryConvertToInt64(val, out res)) return res;
            throw Error(context, "expected long value");
        }

        internal static ulong GetULongLongValue(CodeContext/*!*/ context, int index, object[] args) {
            object val = GetValue(context, index, args);
            ulong res;
            if (Converter.TryConvertToUInt64(val, out res)) return res;
            throw Error(context, "expected ulong value");
        }

        internal static double GetDoubleValue(CodeContext/*!*/ context, int index, object[] args) {
            object val = GetValue(context, index, args);
            double res;
            if (Converter.TryConvertToDouble(val, out res)) return res;
            throw Error(context, "expected double value");
        }

        internal static string GetStringValue(CodeContext/*!*/ context, int index, object[] args) {
            object val = GetValue(context, index, args);
            string res;
            if (Converter.TryConvertToString(val, out res)) return res;
            throw Error(context, "expected string value");
        }

        internal static object GetValue(CodeContext/*!*/ context, int index, object[] args) {
            if (index >= args.Length) throw Error(context, "not enough arguments");
            return args[index];
        }
        #endregion

        #region Data creater helpers

        internal static bool CreateBoolValue(CodeContext/*!*/ context, ref int index, string data) {
            return (int)ReadData(context, ref index, data) != 0;
        }

        internal static char CreateCharValue(CodeContext/*!*/ context, ref int index, string data) {
            return ReadData(context, ref index, data);
        }

        internal static short CreateShortValue(CodeContext/*!*/ context, ref int index, bool fLittleEndian, string data) {
            byte b1 = (byte)ReadData(context, ref index, data);
            byte b2 = (byte)ReadData(context, ref index, data);

            if (fLittleEndian) {
                return (short)((b2 << 8) | b1);
            } else {
                return (short)((b1 << 8) | b2);
            }
        }

        internal static ushort CreateUShortValue(CodeContext/*!*/ context, ref int index, bool fLittleEndian, string data) {
            byte b1 = (byte)ReadData(context, ref index, data);
            byte b2 = (byte)ReadData(context, ref index, data);

            if (fLittleEndian) {
                return (ushort)((b2 << 8) | b1);
            } else {
                return (ushort)((b1 << 8) | b2);
            }
        }

        internal static float CreateFloatValue(CodeContext/*!*/ context, ref int index, bool fLittleEndian, string data) {
            byte[] bytes = new byte[4];
            if (fLittleEndian) {
                bytes[0] = (byte)ReadData(context, ref index, data);
                bytes[1] = (byte)ReadData(context, ref index, data);
                bytes[2] = (byte)ReadData(context, ref index, data);
                bytes[3] = (byte)ReadData(context, ref index, data);
            } else {
                bytes[3] = (byte)ReadData(context, ref index, data);
                bytes[2] = (byte)ReadData(context, ref index, data);
                bytes[1] = (byte)ReadData(context, ref index, data);
                bytes[0] = (byte)ReadData(context, ref index, data);
            }
            float res = BitConverter.ToSingle(bytes, 0);

            if (context.LanguageContext.FloatFormat == FloatFormat.Unknown) {
                if (Single.IsNaN(res) || Single.IsInfinity(res)) {
                    throw PythonOps.ValueError("can't unpack IEEE 754 special value on non-IEEE platform");
                }
            }

            return res;
        }

        internal static int CreateIntValue(CodeContext/*!*/ context, ref int index, bool fLittleEndian, string data) {
            byte b1 = (byte)ReadData(context, ref index, data);
            byte b2 = (byte)ReadData(context, ref index, data);
            byte b3 = (byte)ReadData(context, ref index, data);
            byte b4 = (byte)ReadData(context, ref index, data);

            if (fLittleEndian)
                return (int)((b4 << 24) | (b3 << 16) | (b2 << 8) | b1);
            else
                return (int)((b1 << 24) | (b2 << 16) | (b3 << 8) | b4);
        }

        internal static uint CreateUIntValue(CodeContext/*!*/ context, ref int index, bool fLittleEndian, string data) {
            byte b1 = (byte)ReadData(context, ref index, data);
            byte b2 = (byte)ReadData(context, ref index, data);
            byte b3 = (byte)ReadData(context, ref index, data);
            byte b4 = (byte)ReadData(context, ref index, data);

            if (fLittleEndian)
                return (uint)((b4 << 24) | (b3 << 16) | (b2 << 8) | b1);
            else
                return (uint)((b1 << 24) | (b2 << 16) | (b3 << 8) | b4);
        }

        internal static long CreateLongValue(CodeContext/*!*/ context, ref int index, bool fLittleEndian, string data) {
            long b1 = (byte)ReadData(context, ref index, data);
            long b2 = (byte)ReadData(context, ref index, data);
            long b3 = (byte)ReadData(context, ref index, data);
            long b4 = (byte)ReadData(context, ref index, data);
            long b5 = (byte)ReadData(context, ref index, data);
            long b6 = (byte)ReadData(context, ref index, data);
            long b7 = (byte)ReadData(context, ref index, data);
            long b8 = (byte)ReadData(context, ref index, data);

            if (fLittleEndian)
                return (long)((b8 << 56) | (b7 << 48) | (b6 << 40) | (b5 << 32) |
                                (b4 << 24) | (b3 << 16) | (b2 << 8) | b1);
            else
                return (long)((b1 << 56) | (b2 << 48) | (b3 << 40) | (b4 << 32) |
                                (b5 << 24) | (b6 << 16) | (b7 << 8) | b8);
        }

        internal static ulong CreateULongValue(CodeContext/*!*/ context, ref int index, bool fLittleEndian, string data) {
            ulong b1 = (byte)ReadData(context, ref index, data);
            ulong b2 = (byte)ReadData(context, ref index, data);
            ulong b3 = (byte)ReadData(context, ref index, data);
            ulong b4 = (byte)ReadData(context, ref index, data);
            ulong b5 = (byte)ReadData(context, ref index, data);
            ulong b6 = (byte)ReadData(context, ref index, data);
            ulong b7 = (byte)ReadData(context, ref index, data);
            ulong b8 = (byte)ReadData(context, ref index, data);
            if (fLittleEndian)
                return (ulong)((b8 << 56) | (b7 << 48) | (b6 << 40) | (b5 << 32) |
                                (b4 << 24) | (b3 << 16) | (b2 << 8) | b1);
            else
                return (ulong)((b1 << 56) | (b2 << 48) | (b3 << 40) | (b4 << 32) |
                                (b5 << 24) | (b6 << 16) | (b7 << 8) | b8);
        }

        internal static double CreateDoubleValue(CodeContext/*!*/ context, ref int index, bool fLittleEndian, string data) {
            byte[] bytes = new byte[8];
            if (fLittleEndian) {
                bytes[0] = (byte)ReadData(context, ref index, data);
                bytes[1] = (byte)ReadData(context, ref index, data);
                bytes[2] = (byte)ReadData(context, ref index, data);
                bytes[3] = (byte)ReadData(context, ref index, data);
                bytes[4] = (byte)ReadData(context, ref index, data);
                bytes[5] = (byte)ReadData(context, ref index, data);
                bytes[6] = (byte)ReadData(context, ref index, data);
                bytes[7] = (byte)ReadData(context, ref index, data);
            } else {
                bytes[7] = (byte)ReadData(context, ref index, data);
                bytes[6] = (byte)ReadData(context, ref index, data);
                bytes[5] = (byte)ReadData(context, ref index, data);
                bytes[4] = (byte)ReadData(context, ref index, data);
                bytes[3] = (byte)ReadData(context, ref index, data);
                bytes[2] = (byte)ReadData(context, ref index, data);
                bytes[1] = (byte)ReadData(context, ref index, data);
                bytes[0] = (byte)ReadData(context, ref index, data);
            }

            double res = BitConverter.ToDouble(bytes, 0);
            if (context.LanguageContext.DoubleFormat == FloatFormat.Unknown) {
                if (Double.IsNaN(res) || Double.IsInfinity(res)) {
                    throw PythonOps.ValueError("can't unpack IEEE 754 special value on non-IEEE platform");
                }
            }

            return res;
        }

        internal static string CreateString(CodeContext/*!*/ context, ref int index, int count, string data) {
            StringBuilder res = new StringBuilder();
            for (int i = 0; i < count; i++) {
                res.Append(ReadData(context, ref index, data));
            }
            return res.ToString();
        }


        internal static string CreatePascalString(CodeContext/*!*/ context, ref int index, int count, string data) {
            int realLen = (int)ReadData(context, ref index, data);
            StringBuilder res = new StringBuilder();
            for (int i = 0; i < realLen; i++) {
                res.Append(ReadData(context, ref index, data));
            }
            for (int i = realLen; i < count; i++) {
                // throw away null bytes
                ReadData(context, ref index, data);
            }
            return res.ToString();
        }

        private static char ReadData(CodeContext/*!*/ context, ref int index, string data) {
            if (index >= data.Length) throw Error(context, "not enough data while reading");

            return data[index++];
        }
        #endregion

        #region Misc. Private APIs

        internal static int Align(int length, int size) {
            return length + (size - 1) & ~(size - 1);
        }

        private static Exception Error(CodeContext/*!*/ context, string msg) {
            return PythonExceptions.CreateThrowable((PythonType)context.LanguageContext.GetModuleState("structerror"), msg);
        }

        #endregion
    }
}
