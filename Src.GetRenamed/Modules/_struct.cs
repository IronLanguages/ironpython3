// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

[assembly: PythonModule("_struct", typeof(IronPython.Modules.PythonStruct))]
namespace IronPython.Modules {
    public static class PythonStruct {
        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, PythonDictionary/*!*/ dict) {
            context.EnsureModuleException("structerror", dict, "error", "struct");
        }

        #region Public API

        public const string __doc__ = null;

        [PythonType, Documentation("Represents a compiled struct pattern")]
        public class Struct : IWeakReferenceable {
            private Format[] _formats;              // the various formatting options for the compiled struct
            private bool _isStandardized;           // true if the format is in standardized mode
            private bool _isLittleEndian;           // true if the format is in little endian mode
            private int _encodingCount = -1;        // the number of objects consumed/produced by the format
            private int _encodingSize = -1;         // the number of bytes read/produced by the format
            private WeakRefTracker _tracker;        // storage for weak proxy's

            private void InitializeFrom(Struct s) {
                format = s.format;
                _formats = s._formats;
                _isStandardized = s._isStandardized;
                _isLittleEndian = s._isLittleEndian;
                _encodingCount = s._encodingCount;
                _encodingSize = s._encodingSize;
                _tracker = s._tracker;
            }

            internal Struct(CodeContext/*!*/ context, [NotNone] string/*!*/ fmt) {
                __init__(context, fmt);
            }

            #region Python construction

            [Documentation("creates a new uninitialized struct object - all arguments are ignored")]
            public Struct(params object[] args) {
            }

            [Documentation("creates a new uninitialized struct object - all arguments are ignored")]
            public Struct([ParamDictionary] IDictionary<object, object> kwArgs, params object[] args) {
            }

            [Documentation("initializes or re-initializes the compiled struct object with a new format")]
            public void __init__(CodeContext/*!*/ context, object fmt) {
                format = FormatObjectToString(fmt);

                Struct s;
                bool gotIt;
                lock (_cache) {
                    gotIt = _cache.TryGetValue(format, out s);
                }
                InitializeFrom(gotIt ? s : CompileAndCache(context, format));
            }

            #endregion

            #region Public API

            [Documentation("gets the current format string for the compiled Struct")]
            public string format { get; private set; }

            [Documentation("returns a string consisting of the values serialized according to the format of the struct object")]
            public Bytes/*!*/ pack(CodeContext/*!*/ context, params object[] values) {
                if (values.Length != _encodingCount) {
                    throw Error(context, $"pack requires exactly {_encodingCount} arguments");
                }

                int curObj = 0;
                var buffer = new byte[_encodingSize];
                using var res = new MemoryStream(buffer);

                foreach (Format curFormat in _formats) {
                    if (!_isStandardized) {
                        // In native mode, align to {size}-byte boundaries
                        int nativeSize = curFormat.NativeSize;

                        int alignLength = Align((int)res.Position, nativeSize);
                        int padLength = alignLength - (int)res.Position;
                        res.WriteByteCount((byte)'\0', padLength);
                    }

                    switch (curFormat.Type) {
                        case FormatType.PadByte:
                            res.WriteByteCount((byte)'\0', curFormat.Count);
                            break;
                        case FormatType.Bool:
                            res.WriteByte((byte)(GetBoolValue(context, curObj++, values) ? 1 : 0));
                            break;
                        case FormatType.Char:
                            for (int j = 0; j < curFormat.Count; j++) {
                                res.WriteByte((byte)GetCharValue(context, curObj++, values));
                            }
                            break;
                        case FormatType.SignedChar:
                            for (int j = 0; j < curFormat.Count; j++) {
                                res.WriteByte((byte)GetSByteValue(context, curObj++, values));
                            }
                            break;
                        case FormatType.UnsignedChar:
                            for (int j = 0; j < curFormat.Count; j++) {
                                res.WriteByte(GetByteValue(context, curObj++, values));
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
                                WriteUInt(res, _isLittleEndian, GetULongValue(context, curObj++, values));
                            }
                            break;
                        case FormatType.UnsignedLong:
                            for (int j = 0; j < curFormat.Count; j++) {
                                WriteUInt(res, _isLittleEndian, GetULongValue(context, curObj++, values));
                            }
                            break;
                        case FormatType.Pointer:
                            for (int j = 0; j < curFormat.Count; j++) {
                                WritePointer(res, _isLittleEndian, GetPointer(context, curObj++, values));
                            }
                            break;
                        case FormatType.SignedNetPointer:
                            for (int j = 0; j < curFormat.Count; j++) {
                                WriteSignedNetPointer(res, _isLittleEndian, GetSignedNetPointer(context, curObj++, values));
                            }
                            break;
                        case FormatType.UnsignedNetPointer:
                            for (int j = 0; j < curFormat.Count; j++) {
                                WriteUnsignedNetPointer(res, _isLittleEndian, GetUnsignedNetPointer(context, curObj++, values));
                            }
                            break;
                        case FormatType.SignedSizeT:
                            for (int j = 0; j < curFormat.Count; j++) {
                                WriteInt(res, _isLittleEndian, GetSignedSizeT(context, curObj++, values));
                            }
                            break;
                        case FormatType.SizeT:
                            for (int j = 0; j < curFormat.Count; j++) {
                                WriteUInt(res, _isLittleEndian, GetSizeT(context, curObj++, values));
                            }
                            break;
                        case FormatType.LongLong:
                            for (int j = 0; j < curFormat.Count; j++) {
                                WriteLong(res, _isLittleEndian, GetLongLongValue(context, curObj++, values));
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
                    }
                }

                return Bytes.Make(buffer);
            }

            [Documentation("Stores the deserialized data into the provided array")]
            public void pack_into(CodeContext/*!*/ context, [NotNone] ArrayModule.array/*!*/ buffer, int offset, params object[] args) {
                byte[] existing = buffer.ToByteArray();

                if (offset + size > existing.Length) {
                    throw Error(context, $"pack_into requires a buffer of at least {size} bytes");
                }

                var data = pack(context, args).UnsafeByteArray;

                for (int i = 0; i < data.Length; i++) {
                    existing[i + offset] = data[i];
                }

                buffer.Clear();
                buffer.FromStream(new MemoryStream(existing));
            }

            public void pack_into(CodeContext/*!*/ context, [NotNone] ByteArray/*!*/ buffer, int offset, params object[] args) {
                IList<byte> existing = buffer.UnsafeByteList;

                if (offset + size > existing.Count) {
                    throw Error(context, $"pack_into requires a buffer of at least {size} bytes");
                }

                var data = pack(context, args).UnsafeByteArray;

                for (int i = 0; i < data.Length; i++) {
                    existing[i + offset] = data[i];
                }
            }

            [Documentation("deserializes the string using the structs specified format")]
            public PythonTuple/*!*/ unpack(CodeContext/*!*/ context, [BytesLike][NotNone] IList<byte> @string) {
                if (@string.Count != size) {
                    throw Error(context, $"unpack requires a bytes object of length {size}");
                }

                var data = @string;
                int curIndex = 0;
                var res = new object[_encodingCount];
                var res_idx = 0;

                foreach (Format curFormat in _formats) {
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
                                res[res_idx++] = Bytes.FromByte(CreateCharValue(context, ref curIndex, data));
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
                        case FormatType.SignedNetPointer:
                            for (int j = 0; j < curFormat.Count; j++) {
                                if (IntPtr.Size == 4) {
                                    res[res_idx++] = new IntPtr(CreateIntValue(context, ref curIndex, _isLittleEndian, data));
                                } else {
                                    res[res_idx++] = new IntPtr(CreateLongValue(context, ref curIndex, _isLittleEndian, data));
                                }
                            }
                            break;
                        case FormatType.UnsignedNetPointer:
                            for (int j = 0; j < curFormat.Count; j++) {
                                if (IntPtr.Size == 4) {
                                    res[res_idx++] = new UIntPtr(CreateUIntValue(context, ref curIndex, _isLittleEndian, data));
                                } else {
                                    res[res_idx++] = new UIntPtr(CreateULongValue(context, ref curIndex, _isLittleEndian, data));
                                }
                            }
                            break;
                        case FormatType.SignedSizeT:
                            for (int j = 0; j < curFormat.Count; j++) {
                                res[res_idx++] = CreateIntValue(context, ref curIndex, _isLittleEndian, data);
                            }
                            break;
                        case FormatType.SizeT:
                            for (int j = 0; j < curFormat.Count; j++) {
                                res[res_idx++] = CreateUIntValue(context, ref curIndex, _isLittleEndian, data);
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

            public PythonTuple/*!*/ unpack(CodeContext/*!*/ context, [NotNone] ArrayModule.array/*!*/ buffer)
                => unpack(context, buffer.ToByteArray());

            [Documentation("reads the current format from the specified array")]
            public PythonTuple/*!*/ unpack_from(CodeContext/*!*/ context, [BytesLike][NotNone] IList<byte>/*!*/ buffer, int offset = 0) {
                int bytesAvail = buffer.Count - offset;
                if (bytesAvail < size) {
                    throw Error(context, $"unpack_from requires a buffer of at least {size} bytes");
                }

                return unpack(context, buffer.Substring(offset, size));
            }

            [Documentation("reads the current format from the specified array")]
            public PythonTuple/*!*/ unpack_from(CodeContext/*!*/ context, [NotNone] ArrayModule.array/*!*/ buffer, int offset = 0) {
                return unpack_from(context, buffer.ToByteArray(), offset);
            }

            [Documentation("iteratively unpack the current format from the specified array.")]
            public PythonUnpackIterator iter_unpack(CodeContext/*!*/ context, [BytesLike][NotNone] IList<byte>/*!*/ buffer, int offset = 0) {
                return new PythonUnpackIterator(this, context, buffer, offset);
            }

            [Documentation("iteratively unpack the current format from the specified array.")]
            public PythonUnpackIterator iter_unpack(CodeContext/*!*/ context, [NotNone] ArrayModule.array/*!*/ buffer, int offset = 0) {
                return new PythonUnpackIterator(this, context, buffer, offset);
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

            private static Struct CompileAndCache(CodeContext/*!*/ context, string/*!*/ fmt) {
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
                        case 'r': // IntPtr
                            if (fStandardized) {
                                // r and R don't exist in standard sizes
                                throw Error(context, "bad char in struct format");
                            }
                            res.Add(new Format(FormatType.SignedNetPointer, count));
                            count = 1;
                            break;
                        case 'R': // UIntPtr
                            if (fStandardized) {
                                // r and R don't exist in standard sizes
                                throw Error(context, "bad char in struct format");
                            }
                            res.Add(new Format(FormatType.UnsignedNetPointer, count));
                            count = 1;
                            break;
                        case 'n': // ssize_t
                            if (fStandardized) {
                                // n and N don't exist in standard sizes
                                throw Error(context, "bad char in struct format");
                            }
                            res.Add(new Format(FormatType.SignedSizeT, count));
                            count = 1;
                            break;
                        case 'N': // size_t
                            if (fStandardized) {
                                // n and N don't exist in standard sizes
                                throw Error(context, "bad char in struct format");
                            }
                            res.Add(new Format(FormatType.SizeT, count));
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
                            if (char.IsDigit(fmt[i])) {
                                count = 0;
                                while (char.IsDigit(fmt[i])) {
                                    count = count * 10 + (fmt[i] - '0');
                                    i++;
                                }
                                if (char.IsWhiteSpace(fmt[i])) Error(context, "white space not allowed between count and format");
                                i--;
                                break;
                            }

                            throw Error(context, "bad char in struct format");
                    }
                }

                // store the new formats
                var s = new Struct() {
                    format = fmt,
                    _formats = res.ToArray(),
                    _isStandardized = fStandardized,
                    _isLittleEndian = fLittleEndian,
                };
                s.InitCountAndSize();

                lock (_cache) {
                    _cache.Add(fmt, s);
                }
                return s;
            }

            private void InitCountAndSize() {
                var encodingCount = 0;
                var encodingSize = 0;
                foreach (Format format in _formats) {
                    if (format.Type != FormatType.PadByte) {
                        if (format.Type != FormatType.CString && format.Type != FormatType.PascalString) {
                            encodingCount += format.Count;
                        } else {
                            encodingCount++;
                        }
                    }

                    if (!_isStandardized) {
                        // In native mode, align to {size}-byte boundaries
                        encodingSize = Align(encodingSize, format.NativeSize);
                    }

                    encodingSize += GetNativeSize(format.Type) * format.Count;
                }
                _encodingCount = encodingCount;
                _encodingSize = encodingSize;
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

        [PythonType("unpack_iterator"), Documentation("Represents an iterator returned by _struct.iter_unpack()")]
        public class PythonUnpackIterator : System.Collections.IEnumerator, System.Collections.IEnumerable {
            private object _iter_current;
            private int _next_offset;

            private readonly CodeContext _context;
            private readonly IList<byte> _buffer;
            private readonly int _start_offset;
            private readonly Struct _owner;

            private PythonUnpackIterator() { }

            internal PythonUnpackIterator(Struct/*!*/ owner, CodeContext/*!*/ context, IList<byte>/*!*/ buffer, int offset) {
                _context = context;
                _buffer = buffer;
                _start_offset = offset;
                _owner = owner;

                Reset();
                ValidateBufferLength();
            }

            internal PythonUnpackIterator(Struct/*!*/ owner, CodeContext/*!*/ context, ArrayModule.array/*!*/ buffer, int offset) {
                _context = context;
                _buffer = buffer.ToByteArray();
                _start_offset = offset;
                _owner = owner;

                Reset();
                ValidateBufferLength();
            }

            private void ValidateBufferLength() {
                if (_buffer.Count - _start_offset < _owner.size) {
                    throw Error(_context, $"iterative unpacking requires a buffer of a multiple of {_owner.size} bytes");
                }
            }

            #region IEnumerable
            [PythonHidden]
            public System.Collections.IEnumerator GetEnumerator() {
                return this;
            }
            #endregion

            #region IEnumerator
            [PythonHidden]
            public object Current => _iter_current;

            [PythonHidden]
            public bool MoveNext() {
                if (_buffer.Count - _next_offset < _owner.size) {
                    return false;
                }

                _iter_current = _owner.unpack_from(_context, _buffer, _next_offset);
                _next_offset += _owner.size;
                return true;
            }

            [PythonHidden]
            public void Reset() {
                _iter_current = null;
                _next_offset = _start_offset;
            }
            #endregion

            public object __iter__() {
                return this;
            }

            public object __next__() {
                if (!MoveNext()) {
                    throw PythonOps.StopIteration();
                }
                return Current;
            }

            public int __length_hint__() {
                return (_buffer.Count - _next_offset) / _owner.size;
            }
        }
        #endregion

        #region Compiled Format

        /// <summary>
        /// Enum which specifies the format type for a compiled struct
        /// </summary>
        private enum FormatType {
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

            SignedNetPointer,
            UnsignedNetPointer,

            SignedSizeT,
            SizeT,
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
                case FormatType.SignedSizeT:
                case FormatType.SizeT:
                    return 4;
                case FormatType.LongLong:
                case FormatType.UnsignedLongLong:
                case FormatType.Double:
                    return 8;
                case FormatType.Pointer:
                case FormatType.SignedNetPointer:
                case FormatType.UnsignedNetPointer:
                    return UIntPtr.Size;
                default:
                    throw new InvalidOperationException(c.ToString());
            }
        }

        /// <summary>
        /// Struct used to store the format and the number of times it should be repeated.
        /// </summary>
        private readonly struct Format {
            public readonly FormatType Type;
            public readonly int Count;

            public Format(FormatType type, int count) {
                Type = type;
                Count = count;
            }

            public int NativeSize => GetNativeSize(Type);
        }

        #endregion

        #region Cache of compiled struct patterns

        private const int MAX_CACHE_SIZE = 1024;
        private static CacheDict<string, Struct> _cache = new CacheDict<string, Struct>(MAX_CACHE_SIZE);

        private static string FormatObjectToString(object fmt) {
            if (Converter.TryConvertToString(fmt, out string res)) {
                return res;
            }
            if (fmt is IList<byte> b) {
                return PythonOps.MakeString(b);
            }
            throw PythonOps.TypeError("Struct() argument 1 must be a str or bytes object, not {0}", PythonOps.GetPythonTypeName(fmt));
        }

        private static Struct GetStructFromCache(CodeContext/*!*/ context, object fmt) {
            var formatString = FormatObjectToString(fmt);

            Struct s;
            bool gotIt;
            lock (_cache) {
                gotIt = _cache.TryGetValue(formatString, out s);
            }
            if (!gotIt) {
                s = new Struct(context, formatString);
            }
            return s;
        }

        [Documentation("Clear the internal cache.")]
        public static void _clearcache() {
            _cache = new CacheDict<string, Struct>(MAX_CACHE_SIZE);
        }

        [Documentation("int(x[, base]) -> integer\n\nConvert a string or number to an integer, if possible.  A floating point\nargument will be truncated towards zero (this does not include a string\nrepresentation of a floating point number!)  When converting a string, use\nthe optional base.  It is an error to supply a base when converting a\nnon-string.  If base is zero, the proper base is guessed based on the\nstring content.  If the argument is outside the integer range a\nlong object will be returned instead.")]
        public static int calcsize(CodeContext/*!*/ context, object fmt) {
            return GetStructFromCache(context, fmt).size;
        }

        [Documentation("Return string containing values v1, v2, ... packed according to fmt.")]
        public static Bytes/*!*/ pack(CodeContext/*!*/ context, object fmt/*!*/, params object[] values) {
            return GetStructFromCache(context, fmt).pack(context, values);
        }

        [Documentation("Pack the values v1, v2, ... according to fmt.\nWrite the packed bytes into the writable buffer buf starting at offset.")]
        public static void pack_into(CodeContext/*!*/ context, object fmt, [NotNone] ArrayModule.array/*!*/ buffer, int offset, params object[] args) {
            GetStructFromCache(context, fmt).pack_into(context, buffer, offset, args);
        }

        public static void pack_into(CodeContext/*!*/ context, object fmt, [NotNone] ByteArray/*!*/ buffer, int offset, params object[] args) {
            GetStructFromCache(context, fmt).pack_into(context, buffer, offset, args);
        }

        [Documentation("Unpack the string containing packed C structure data, according to fmt.\nRequires len(string) == calcsize(fmt).")]
        public static PythonTuple/*!*/ unpack(CodeContext/*!*/ context, object fmt, [BytesLike][NotNone] IList<byte>/*!*/ buffer) {
            return GetStructFromCache(context, fmt).unpack(context, buffer);
        }

        [Documentation("Unpack the string containing packed C structure data, according to fmt.\nRequires len(string) == calcsize(fmt).")]
        public static PythonTuple/*!*/ unpack(CodeContext/*!*/ context, object fmt, [NotNone] ArrayModule.array/*!*/ buffer) {
            return GetStructFromCache(context, fmt).unpack(context, buffer);
        }

        [Documentation("Unpack the buffer, containing packed C structure data, according to\nfmt, starting at offset. Requires len(buffer[offset:]) >= calcsize(fmt).")]
        public static PythonTuple/*!*/ unpack_from(CodeContext/*!*/ context, object fmt, [BytesLike][NotNone] IList<byte>/*!*/ buffer, int offset = 0) {
            return GetStructFromCache(context, fmt).unpack_from(context, buffer, offset);
        }

        [Documentation("Unpack the buffer, containing packed C structure data, according to\nfmt, starting at offset. Requires len(buffer[offset:]) >= calcsize(fmt).")]
        public static PythonTuple/*!*/ unpack_from(CodeContext/*!*/ context, object fmt, [NotNone] ArrayModule.array/*!*/ buffer, int offset = 0) {
            return GetStructFromCache(context, fmt).unpack_from(context, buffer, offset);
        }

        [Documentation("Iteratively unpack the buffer, containing packed C structure data, according to\nfmt, starting at offset. Requires len(buffer[offset:]) >= calcsize(fmt).")]
        public static PythonUnpackIterator/*!*/ iter_unpack(CodeContext/*!*/ context, object fmt, [BytesLike][NotNone] IList<byte>/*!*/ buffer, int offset = 0) {
            return GetStructFromCache(context, fmt).iter_unpack(context, buffer, offset);
        }

        [Documentation("Iteratively unpack the buffer, containing packed C structure data, according to\nfmt, starting at offset. Requires len(buffer[offset:]) >= calcsize(fmt).")]
        public static PythonUnpackIterator/*!*/ iter_unpack(CodeContext/*!*/ context, object fmt, [NotNone] ArrayModule.array/*!*/ buffer, int offset = 0) {
            return GetStructFromCache(context, fmt).iter_unpack(context, buffer, offset);
        }
        #endregion

        #region Write Helpers

        private static void WriteByteCount(this MemoryStream stream, byte value, int repeatCount) {
            while (repeatCount > 0) {
                stream.WriteByte(value);
                --repeatCount;
            }
        }

        private static void WriteShort(this MemoryStream res, bool fLittleEndian, short val) {
            if (fLittleEndian) {
                res.WriteByte((byte)(val & 0xff));
                res.WriteByte((byte)((val >> 8) & 0xff));
            } else {
                res.WriteByte((byte)((val >> 8) & 0xff));
                res.WriteByte((byte)(val & 0xff));
            }
        }

        private static void WriteUShort(this MemoryStream res, bool fLittleEndian, ushort val) {
            if (fLittleEndian) {
                res.WriteByte((byte)(val & 0xff));
                res.WriteByte((byte)((val >> 8) & 0xff));
            } else {
                res.WriteByte((byte)((val >> 8) & 0xff));
                res.WriteByte((byte)(val & 0xff));
            }
        }

        private static void WriteInt(this MemoryStream res, bool fLittleEndian, int val) {
            if (fLittleEndian) {
                res.WriteByte((byte)(val & 0xff));
                res.WriteByte((byte)((val >> 8) & 0xff));
                res.WriteByte((byte)((val >> 16) & 0xff));
                res.WriteByte((byte)((val >> 24) & 0xff));
            } else {
                res.WriteByte((byte)((val >> 24) & 0xff));
                res.WriteByte((byte)((val >> 16) & 0xff));
                res.WriteByte((byte)((val >> 8) & 0xff));
                res.WriteByte((byte)(val & 0xff));
            }
        }

        private static void WriteUInt(this MemoryStream res, bool fLittleEndian, uint val) {
            if (fLittleEndian) {
                res.WriteByte((byte)(val & 0xff));
                res.WriteByte((byte)((val >> 8) & 0xff));
                res.WriteByte((byte)((val >> 16) & 0xff));
                res.WriteByte((byte)((val >> 24) & 0xff));
            } else {
                res.WriteByte((byte)((val >> 24) & 0xff));
                res.WriteByte((byte)((val >> 16) & 0xff));
                res.WriteByte((byte)((val >> 8) & 0xff));
                res.WriteByte((byte)(val & 0xff));
            }
        }

        private static void WritePointer(this MemoryStream res, bool fLittleEndian, ulong val) {
            if (UIntPtr.Size == 4) {
                res.WriteUInt(fLittleEndian, (uint)val);
            } else {
                res.WriteULong(fLittleEndian, val);
            }
        }

        private static void WriteUnsignedNetPointer(this MemoryStream res, bool fLittleEndian, UIntPtr val) {
            res.WritePointer(fLittleEndian, val.ToUInt64());
        }

        private static void WriteSignedNetPointer(this MemoryStream res, bool fLittleEndian, IntPtr val) {
            res.WritePointer(fLittleEndian, unchecked((ulong)val.ToInt64()));
        }

        private static void WriteFloat(this MemoryStream res, bool fLittleEndian, float val) {
            byte[] bytes = BitConverter.GetBytes(val);
            if (BitConverter.IsLittleEndian == fLittleEndian) {
                res.Write(bytes, 0, bytes.Length);
            } else {
                res.WriteByte(bytes[3]);
                res.WriteByte(bytes[2]);
                res.WriteByte(bytes[1]);
                res.WriteByte(bytes[0]);
            }
        }

        private static void WriteLong(this MemoryStream res, bool fLittleEndian, long val) {
            if (fLittleEndian) {
                res.WriteByte((byte)(val & 0xff));
                res.WriteByte((byte)((val >> 8) & 0xff));
                res.WriteByte((byte)((val >> 16) & 0xff));
                res.WriteByte((byte)((val >> 24) & 0xff));
                res.WriteByte((byte)((val >> 32) & 0xff));
                res.WriteByte((byte)((val >> 40) & 0xff));
                res.WriteByte((byte)((val >> 48) & 0xff));
                res.WriteByte((byte)((val >> 56) & 0xff));
            } else {
                res.WriteByte((byte)((val >> 56) & 0xff));
                res.WriteByte((byte)((val >> 48) & 0xff));
                res.WriteByte((byte)((val >> 40) & 0xff));
                res.WriteByte((byte)((val >> 32) & 0xff));
                res.WriteByte((byte)((val >> 24) & 0xff));
                res.WriteByte((byte)((val >> 16) & 0xff));
                res.WriteByte((byte)((val >> 8) & 0xff));
                res.WriteByte((byte)(val & 0xff));
            }
        }

        private static void WriteULong(this MemoryStream res, bool fLittleEndian, ulong val) {
            if (fLittleEndian) {
                res.WriteByte((byte)(val & 0xff));
                res.WriteByte((byte)((val >> 8) & 0xff));
                res.WriteByte((byte)((val >> 16) & 0xff));
                res.WriteByte((byte)((val >> 24) & 0xff));
                res.WriteByte((byte)((val >> 32) & 0xff));
                res.WriteByte((byte)((val >> 40) & 0xff));
                res.WriteByte((byte)((val >> 48) & 0xff));
                res.WriteByte((byte)((val >> 56) & 0xff));
            } else {
                res.WriteByte((byte)((val >> 56) & 0xff));
                res.WriteByte((byte)((val >> 48) & 0xff));
                res.WriteByte((byte)((val >> 40) & 0xff));
                res.WriteByte((byte)((val >> 32) & 0xff));
                res.WriteByte((byte)((val >> 24) & 0xff));
                res.WriteByte((byte)((val >> 16) & 0xff));
                res.WriteByte((byte)((val >> 8) & 0xff));
                res.WriteByte((byte)(val & 0xff));
            }
        }

        private static void WriteDouble(this MemoryStream res, bool fLittleEndian, double val) {
            byte[] bytes = BitConverter.GetBytes(val);
            if (BitConverter.IsLittleEndian == fLittleEndian) {
                res.Write(bytes, 0, bytes.Length);
            } else {
                res.WriteByte(bytes[7]);
                res.WriteByte(bytes[6]);
                res.WriteByte(bytes[5]);
                res.WriteByte(bytes[4]);
                res.WriteByte(bytes[3]);
                res.WriteByte(bytes[2]);
                res.WriteByte(bytes[1]);
                res.WriteByte(bytes[0]);
            }
        }

        private static void WriteString(this MemoryStream res, int len, IList<byte> val) {
            for (int i = 0; i < val.Count && i < len; i++) {
                res.WriteByte(val[i]);
            }
            for (int i = val.Count; i < len; i++) {
                res.WriteByte(0);
            }
        }

        private static void WritePascalString(this MemoryStream res, int len, IList<byte> val) {
            byte lenByte = (byte)Math.Min(255, Math.Min(val.Count, len));
            res.WriteByte(lenByte);

            for (int i = 0; i < val.Count && i < len; i++) {
                res.WriteByte(val[i]);
            }
            for (int i = val.Count; i < len; i++) {
                res.WriteByte(0);
            }
        }
        #endregion

        #region Data getter helpers

        internal static bool GetBoolValue(CodeContext/*!*/ context, int index, object[] args) {
            object val = GetValue(context, index, args);

            if (Converter.TryConvert(val, typeof(bool), out object res)) {
                return (bool)res;
            }
            // Should never happen
            throw Error(context, "expected bool value got " + val.ToString());
        }

        internal static byte GetCharValue(CodeContext/*!*/ context, int index, object[] args) {
            object val = GetValue(context, index, args);
            if (val is IList<byte> byteVal && byteVal.Count == 1) return byteVal[0];
            throw Error(context, "char format requires string of length 1");
        }

        internal static sbyte GetSByteValue(CodeContext/*!*/ context, int index, object[] args) {
            BigInteger val = GetIntegerValue(context, index, args);
            if (!val.AsInt32(out int res))
                throw Error(context, "argument out of range");
            CheckRange(context, res, sbyte.MinValue, sbyte.MaxValue, "byte");
            return (sbyte)res;
        }

        internal static byte GetByteValue(CodeContext/*!*/ context, int index, object[] args) {
            BigInteger val = GetIntegerValue(context, index, args);
            if (!val.AsInt32(out int res))
                throw Error(context, "argument out of range");
            CheckRange(context, res, byte.MinValue, byte.MaxValue, "ubyte");
            return (byte)res;
        }

        internal static short GetShortValue(CodeContext/*!*/ context, int index, object[] args) {
            BigInteger val = GetIntegerValue(context, index, args);
            if (!val.AsInt32(out int res))
                throw Error(context, "argument out of range");
            CheckRange(context, res, short.MinValue, short.MaxValue, "short");
            return (short)res;
        }

        internal static ushort GetUShortValue(CodeContext/*!*/ context, int index, object[] args) {
            BigInteger val = GetIntegerValue(context, index, args);
            if (!val.AsInt32(out int res))
                throw Error(context, "argument out of range");
            CheckRange(context, res, ushort.MinValue, ushort.MaxValue, "ushort");
            return (ushort)res;
        }

        internal static int GetIntValue(CodeContext/*!*/ context, int index, object[] args) {
            BigInteger val = GetIntegerValue(context, index, args);
            if (!val.AsInt32(out int res))
                throw Error(context, "argument out of range");
            return res;
        }

        internal static uint GetULongValue(CodeContext/*!*/ context, int index, object[] args) {
            BigInteger val = GetIntegerValue(context, index, args);
            if (!val.AsUInt32(out uint res))
                throw Error(context, "argument out of range");
            return res;
        }

        internal static int GetSignedSizeT(CodeContext/*!*/ context, int index, object[] args) {
            return GetIntValue(context, index, args);
        }

        internal static uint GetSizeT(CodeContext/*!*/ context, int index, object[] args) {
            return GetULongValue(context, index, args);
        }

        internal static ulong GetPointer(CodeContext/*!*/ context, int index, object[] args) {
            BigInteger val = GetIntegerValue(context, index, args);
            if (UIntPtr.Size == 4) {
                if (val < 0) {
                    val += new BigInteger(UInt32.MaxValue) + 1;
                }
                if (Converter.TryConvertToUInt32(val, out uint res)) {
                    return res;
                }
            } else {
                if (val < 0) {
                    val += new BigInteger(UInt64.MaxValue) + 1;
                }
                if (Converter.TryConvertToUInt64(val, out ulong res)) {
                    return res;
                }
            }
            throw Error(context, "int too large to convert");
        }

        internal static IntPtr GetSignedNetPointer(CodeContext/*!*/ context, int index, object[] args) {
            object val = GetValue(context, index, args);
            if (val is IntPtr iptr) {
                return iptr;
            } else if (val is UIntPtr uptr) {
                return new IntPtr(unchecked((long)uptr.ToUInt64()));
            }
            throw Error(context, "expected .NET pointer value");
        }

        internal static UIntPtr GetUnsignedNetPointer(CodeContext/*!*/ context, int index, object[] args) {
            object val = GetValue(context, index, args);
            if (val is UIntPtr uptr) {
                return uptr;
            } else if (val is IntPtr iptr) {
                return new UIntPtr(unchecked((ulong)iptr.ToInt64()));
            }
            throw Error(context, "expected .NET pointer value");
        }

        internal static long GetLongLongValue(CodeContext/*!*/ context, int index, object[] args) {
            BigInteger val = GetIntegerValue(context, index, args);
            if (!val.AsInt64(out long res))
                throw Error(context, "argument out of range");
            return res;
        }

        internal static ulong GetULongLongValue(CodeContext/*!*/ context, int index, object[] args) {
            BigInteger val = GetIntegerValue(context, index, args);
            if (!val.AsUInt64(out ulong res))
                throw Error(context, "argument out of range");
            return res;
        }

        internal static double GetDoubleValue(CodeContext/*!*/ context, int index, object[] args) {
            object val = GetValue(context, index, args);
            if (Converter.TryConvertToDouble(val, out double res)) return res;
            throw Error(context, "expected double value");
        }

        internal static IList<byte> GetStringValue(CodeContext/*!*/ context, int index, object[] args) {
            object val = GetValue(context, index, args);
            if (val is IList<byte> res) return res;
            throw Error(context, "expected bytes value");
        }

        internal static object GetValue(CodeContext/*!*/ context, int index, object[] args) {
            if (index >= args.Length) throw Error(context, "not enough arguments");
            return args[index];
        }

        private static BigInteger GetIntegerValue(CodeContext/*!*/ context, int index, object[] args) {
            object val = GetValue(context, index, args);
            if (PythonOps.TryToIndex(val, out BigInteger res)) return res;
            throw Error(context, "required argument is not an integer");
        }

        private static void CheckRange(CodeContext context, int val, int min, int max, string format) {
            if (val < min || val > max) {
                throw Error(context, $"{format} format requires {min} <= number <= {max}");
            }
        }

        #endregion

        #region Data creater helpers

        internal static bool CreateBoolValue(CodeContext/*!*/ context, ref int index, IList<byte> data) {
            return (int)ReadData(context, ref index, data) != 0;
        }

        internal static byte CreateCharValue(CodeContext/*!*/ context, ref int index, IList<byte> data) {
            return ReadData(context, ref index, data);
        }

        internal static short CreateShortValue(CodeContext/*!*/ context, ref int index, bool fLittleEndian, IList<byte> data) {
            byte b1 = (byte)ReadData(context, ref index, data);
            byte b2 = (byte)ReadData(context, ref index, data);

            if (fLittleEndian) {
                return (short)((b2 << 8) | b1);
            } else {
                return (short)((b1 << 8) | b2);
            }
        }

        internal static ushort CreateUShortValue(CodeContext/*!*/ context, ref int index, bool fLittleEndian, IList<byte> data) {
            byte b1 = (byte)ReadData(context, ref index, data);
            byte b2 = (byte)ReadData(context, ref index, data);

            if (fLittleEndian) {
                return (ushort)((b2 << 8) | b1);
            } else {
                return (ushort)((b1 << 8) | b2);
            }
        }

        internal static float CreateFloatValue(CodeContext/*!*/ context, ref int index, bool fLittleEndian, IList<byte> data) {
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
                if (float.IsNaN(res) || float.IsInfinity(res)) {
                    throw PythonOps.ValueError("can't unpack IEEE 754 special value on non-IEEE platform");
                }
            }

            return res;
        }

        internal static int CreateIntValue(CodeContext/*!*/ context, ref int index, bool fLittleEndian, IList<byte> data) {
            byte b1 = (byte)ReadData(context, ref index, data);
            byte b2 = (byte)ReadData(context, ref index, data);
            byte b3 = (byte)ReadData(context, ref index, data);
            byte b4 = (byte)ReadData(context, ref index, data);

            if (fLittleEndian)
                return (int)((b4 << 24) | (b3 << 16) | (b2 << 8) | b1);
            else
                return (int)((b1 << 24) | (b2 << 16) | (b3 << 8) | b4);
        }

        internal static uint CreateUIntValue(CodeContext/*!*/ context, ref int index, bool fLittleEndian, IList<byte> data) {
            byte b1 = (byte)ReadData(context, ref index, data);
            byte b2 = (byte)ReadData(context, ref index, data);
            byte b3 = (byte)ReadData(context, ref index, data);
            byte b4 = (byte)ReadData(context, ref index, data);

            if (fLittleEndian)
                return (uint)((b4 << 24) | (b3 << 16) | (b2 << 8) | b1);
            else
                return (uint)((b1 << 24) | (b2 << 16) | (b3 << 8) | b4);
        }

        internal static long CreateLongValue(CodeContext/*!*/ context, ref int index, bool fLittleEndian, IList<byte> data) {
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

        internal static ulong CreateULongValue(CodeContext/*!*/ context, ref int index, bool fLittleEndian, IList<byte> data) {
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

        internal static double CreateDoubleValue(CodeContext/*!*/ context, ref int index, bool fLittleEndian, IList<byte> data) {
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
                if (double.IsNaN(res) || double.IsInfinity(res)) {
                    throw PythonOps.ValueError("can't unpack IEEE 754 special value on non-IEEE platform");
                }
            }

            return res;
        }

        internal static Bytes CreateString(CodeContext/*!*/ context, ref int index, int count, IList<byte> data) {
            using var res = new MemoryStream();
            for (int i = 0; i < count; i++) {
                res.WriteByte(ReadData(context, ref index, data));
            }
            return Bytes.Make(res.ToArray());
        }


        internal static Bytes CreatePascalString(CodeContext/*!*/ context, ref int index, int count, IList<byte> data) {
            int realLen = (int)ReadData(context, ref index, data);
            using var res = new MemoryStream();
            for (int i = 0; i < realLen; i++) {
                res.WriteByte(ReadData(context, ref index, data));
            }
            for (int i = realLen; i < count; i++) {
                // throw away null bytes
                ReadData(context, ref index, data);
            }
            return Bytes.Make(res.ToArray());
        }

        private static byte ReadData(CodeContext/*!*/ context, ref int index, IList<byte> data) {
            if (index >= data.Count) throw Error(context, "not enough data while reading");

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
