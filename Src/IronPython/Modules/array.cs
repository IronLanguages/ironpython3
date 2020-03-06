// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using SpecialName = System.Runtime.CompilerServices.SpecialNameAttribute;
using DisallowNull = System.Diagnostics.CodeAnalysis.DisallowNullAttribute;

[assembly: PythonModule("array", typeof(IronPython.Modules.ArrayModule))]
namespace IronPython.Modules {
    public static class ArrayModule {
        public const string __doc__ = "Provides arrays for native data types.  These can be used for compact storage or native interop via ctypes";
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly PythonType/*!*/ ArrayType = DynamicHelpers.GetPythonTypeFromType(typeof(array));

        public static readonly string typecodes = "bBuhHiIlLqQfd";

        [PythonType]
        public class array : IEnumerable, IWeakReferenceable, ICollection, ICodeFormattable, IList<object>, IStructuralEquatable, IBufferProtocol
        {
            private ArrayData _data;
            private readonly char _typeCode;
            private WeakRefTracker? _tracker;

            public array([NotNull]string type, [NotNull, DisallowNull]object? initializer = null) {
                if (type == null || type.Length != 1) {
                    throw PythonOps.TypeError("expected character, got {0}", PythonTypeOps.GetName(type));
                }

                _typeCode = type[0];

                if (_typeCode != 'u' && (initializer is string || initializer is Extensible<string>)) {
                    throw PythonOps.TypeError("cannot use a str to initialize an array with typecode '{0}'", _typeCode);
                }

                _data = CreateData(_typeCode);

                if (initializer != Missing.Value) extend(initializer);
            }

            private array(char typeCode, ArrayData data) {                
                _typeCode = typeCode;
                _data = data;
            }

            private static ArrayData CreateData(char typecode) {
                ArrayData data;
                switch (typecode) {
                    case 'b': data = new ArrayData<sbyte>(); break;
                    case 'B': data = new ArrayData<byte>(); break;
                    case 'u': data = new ArrayData<char>(); break;
                    case 'h': data = new ArrayData<short>(); break;
                    case 'H': data = new ArrayData<ushort>(); break;
                    case 'i': data = new ArrayData<int>(); break;
                    case 'I': data = new ArrayData<uint>(); break;
                    case 'l': data = new ArrayData<int>(); break;
                    case 'L': data = new ArrayData<uint>(); break;
                    case 'q': data = new ArrayData<long>(); break;
                    case 'Q': data = new ArrayData<ulong>(); break;
                    case 'f': data = new ArrayData<float>(); break;
                    case 'd': data = new ArrayData<double>(); break;
                    default:
                        throw PythonOps.ValueError("bad typecode (must be b, B, u, h, H, i, I, l, L, q, Q, f or d)");
                }
                return data;
            }

            [SpecialName]
            public array InPlaceAdd([NotNull]array other) {
                if (typecode != other.typecode) throw PythonOps.TypeError("cannot add different typecodes");

                if (other._data.Count != 0) {
                    extend(other);
                }

                return this;
            }

            public static array operator +([NotNull]array self, [NotNull]array other) {
                if (self.typecode != other.typecode) throw PythonOps.TypeError("cannot add different typecodes");

                array res = new array(self.typecode, Missing.Value);
                foreach (object o in self) {
                    res.append(o);
                }

                foreach (object o in other) {
                    res.append(o);
                }

                return res;
            }

            [SpecialName]
            public array InPlaceMultiply(int value) {
                if (value <= 0) {
                    _data.Clear();
                } else {
                    PythonList myData = tolist();

                    for (int i = 0; i < (value - 1); i++) {
                        extend(myData);
                    }
                }
                return this;
            }

            public static array operator *([NotNull]array array, int value) {
                if ((BigInteger)value * array.__len__() * array.itemsize > SysModule.maxsize) {
                    throw PythonOps.MemoryError("");
                }

                if (value <= 0) {
                    return new array(array.typecode, Missing.Value);
                }
                
                return new array(array._typeCode, array._data.Multiply(value));
            }

            public static array operator *([NotNull]array array, BigInteger value) {
                int intValue;
                if (!value.AsInt32(out intValue)) {
                    throw PythonOps.OverflowError("cannot fit 'long' into an index-sized integer");
                } else if (value * array.__len__() * array.itemsize > SysModule.maxsize) {
                    throw PythonOps.MemoryError("");
                }

                return array * intValue;
            }

            public static array operator *(int value, [NotNull]array array) {
                return array * value;
            }

            public static array operator *(BigInteger value, [NotNull]array array) {
                return array * value;
            }

            public void append(object? iterable) {
                _data.Add(iterable);
            }

            internal IntPtr GetArrayAddress() {
                return _data.GetAddress();
            }

            public PythonTuple buffer_info() {
                return PythonTuple.MakeTuple(
                    _data.GetAddress().ToPython(),
                    _data.Count
                );
            }

            public void byteswap() {
                Stream s = ToStream();
                byte[] bytes = new byte[s.Length];
                s.Read(bytes, 0, bytes.Length);

                byte[] tmp = new byte[itemsize];
                for (int i = 0; i < bytes.Length; i += itemsize) {
                    for (int j = 0; j < itemsize; j++) {
                        tmp[j] = bytes[i + j];
                    }
                    for (int j = 0; j < itemsize; j++) {
                        bytes[i + j] = tmp[itemsize - (j + 1)];
                    }
                }
                _data.Clear();
                MemoryStream ms = new MemoryStream(bytes);
                FromStream(ms);
            }

            public int count(object? x) {
                if (x == null) return 0;

                return _data.CountValues(x);
            }

            public void extend(object? iterable) {
                if (iterable is array pa) {
                    if (typecode != pa.typecode) {
                        throw PythonOps.TypeError("cannot extend with different typecode");
                    }
                    int l = pa._data.Count;
                    for (int i = 0; i < l; i++) {
                        _data.Add(pa._data[i]);
                    }
                    return;
                }

                if (iterable is Bytes bytes) {
                    FromBytes(bytes);
                    return;
                }

                if (_typeCode == 'u' && iterable is string str) {
                    FromUnicode(str);
                    return;
                }

                IEnumerator ie = PythonOps.GetEnumerator(iterable);
                while (ie.MoveNext()) {
                    append(ie.Current);
                }
            }

            public void fromlist([NotNull]PythonList iterable) {
                IEnumerator ie = PythonOps.GetEnumerator(iterable);

                List<object> items = new List<object>();
                while (ie.MoveNext()) {
                    if (!_data.CanStore(ie.Current)) {
                        throw PythonOps.TypeError("expected {0}, got {1}",
                            DynamicHelpers.GetPythonTypeFromType(_data.StorageType).Name,
                            DynamicHelpers.GetPythonType(ie.Current).Name);
                    }
                    items.Add(ie.Current);
                }

                extend(items);
            }

            public void fromfile(CodeContext/*!*/ context, [NotNull]PythonIOModule._IOBase f, int n) {
                int bytesNeeded = n * itemsize;
                Bytes bytes = (Bytes)f.read(context, bytesNeeded);
                if (bytes.Count < bytesNeeded) throw PythonOps.EofError("file not large enough");

                frombytes(bytes);
            }

            public void frombytes([BytesConversion, NotNull]IList<byte> s) {
                if ((s.Count % itemsize) != 0) throw PythonOps.ValueError("bytes length not a multiple of itemsize");

                if (s is Bytes b) {
                    FromBytes(b);
                    return;
                }

                FromStream(new MemoryStream(s.ToArray(), false));
            }

            private void FromBytes(Bytes b) {
                FromStream(new MemoryStream(b.UnsafeByteArray, false));
            }

            public void fromstring(CodeContext/*!*/ context, [NotNull]Bytes b) {
                PythonOps.Warn(context, PythonExceptions.DeprecationWarning, "fromstring() is deprecated. Use frombytes() instead.");
                if ((b.Count % itemsize) != 0) throw PythonOps.ValueError("bytes length not a multiple of itemsize");
                FromBytes(b);
            }

            public void fromstring(CodeContext/*!*/ context, [NotNull]string s) {
                PythonOps.Warn(context, PythonExceptions.DeprecationWarning, "fromstring() is deprecated. Use frombytes() instead.");
                if ((s.Length % itemsize) != 0) throw PythonOps.ValueError("bytes length not a multiple of itemsize");
                byte[] bytes = new byte[s.Length];
                for (int i = 0; i < bytes.Length; i++) {
                    bytes[i] = checked((byte)s[i]);
                }
                MemoryStream ms = new MemoryStream(bytes);

                FromStream(ms);
            }

            public void fromunicode(CodeContext/*!*/ context, [NotNull]string s) {
                if (_typeCode != 'u') {
                    throw PythonOps.ValueError("fromunicode() may only be called on type 'u' arrays");
                }

                FromUnicode(s);
            }

            private void FromUnicode(string s) {
                ArrayData<char> data = (ArrayData<char>)_data;
                data.AddRange(s);
            }

            public int index(object? x) {
                int res = _data.IndexOf(x);
                if (res == -1) throw PythonOps.ValueError("x not found");
                return res;
            }

            public void insert(int i, [NotNull]object x) {
                if (i > _data.Count) i = _data.Count;
                if (i < 0) i = _data.Count + i;
                if (i < 0) i = 0;

                _data.Insert(i, x);
            }

            public int itemsize {
                get {
                    switch (_typeCode) {
                        case 'b': // signed byte
                        case 'B': // unsigned byte
                            return 1;
                        case 'u': // unicode char
                        case 'h': // signed short
                        case 'H': // unsigned short
                            return 2;
                        case 'i': // signed int
                        case 'I': // unsigned int
                        case 'l': // signed long
                        case 'L': // unsigned long
                        case 'f': // float
                            return 4;
                        case 'q': // signed long long
                        case 'Q': // unsigned long long
                        case 'd': // double
                            return 8;
                        default:
                            return 0;
                    }
                }
            }

            public object pop() {
                return pop(-1);
            }

            public object pop(int i) {
                i = PythonOps.FixIndex(i, _data.Count);
                object res = _data[i];
                _data.RemoveAt(i);
                return res;
            }

            public void remove([NotNull]object value) {
                if(!_data.Remove(value)) throw PythonOps.ValueError("couldn't find value to remove");
            }

            public void reverse() {
                for (int index = 0; index < _data.Count / 2; index++) {
                    int left = index, right = _data.Count - (index + 1);

                    Debug.Assert(left != right);
                    _data.Swap(left, right);
                }
            }

            public virtual object this[int index] {
                get {
                    object val = _data[PythonOps.FixIndex(index, _data.Count)];
                    switch (_typeCode) {
                        case 'b': return (int)(sbyte)val;
                        case 'B': return (int)(byte)val;
                        case 'u': return new string((char)val, 1);
                        case 'h': return (int)(short)val;
                        case 'H': return (int)(ushort)val;
                        case 'i': return val;
                        case 'I': return (BigInteger)(uint)val;
                        case 'l': return val;
                        case 'L': return (BigInteger)(uint)val;
                        case 'q': return (BigInteger)(long)val;
                        case 'Q': return (BigInteger)(ulong)val;
                        case 'f': return (double)(float)val;
                        case 'd': return val;
                        default:
                            throw PythonOps.ValueError("bad typecode (must be b, B, u, h, H, i, I, l, L, q, Q, f or d)");
                    }
                }
                set {
                    _data[PythonOps.FixIndex(index, _data.Count)] = value;
                }
            }

            internal byte[] RawGetItem(int index) {
                MemoryStream ms = new MemoryStream();
                BinaryWriter bw = new BinaryWriter(ms);
                switch (_typeCode) {
                    case 'c': bw.Write((byte)(char)_data[index]); break;
                    case 'b': bw.Write((sbyte)_data[index]); break;
                    case 'B': bw.Write((byte)_data[index]); break;
                    case 'u': bw.Write((char)_data[index]); break;
                    case 'h': bw.Write((short)_data[index]); break;
                    case 'H': bw.Write((ushort)_data[index]); break;
                    case 'l':
                    case 'i': bw.Write((int)_data[index]); break;
                    case 'L':
                    case 'I': bw.Write((uint)_data[index]); break;
                    case 'f': bw.Write((float)_data[index]); break;
                    case 'd': bw.Write((double)_data[index]); break;
                }
                return ms.ToArray();
            }

            public void __delitem__(int index) {
                _data.RemoveAt(PythonOps.FixIndex(index, _data.Count));
            }

            public void __delitem__([NotNull]Slice slice) {
                int start, stop, step;
                // slice is sealed, indices can't be user code...
                slice.indices(_data.Count, out start, out stop, out step);

                if (step > 0 && (start >= stop)) return;
                if (step < 0 && (start <= stop)) return;

                if (step == 1) {
                    int i = start;
                    for (int j = stop; j < _data.Count; j++, i++) {
                        _data[i] = _data[j];
                    }
                    for (i = 0; i < stop - start; i++) {
                        _data.RemoveAt(_data.Count - 1);
                    }
                    return;
                }
                if (step == -1) {
                    int i = stop + 1;
                    for (int j = start + 1; j < _data.Count; j++, i++) {
                        _data[i] = _data[j];
                    }
                    for (i = 0; i < stop - start; i++) {
                        _data.RemoveAt(_data.Count - 1);
                    }
                    return;
                }

                if (step < 0) {
                    // find "start" we will skip in the 1,2,3,... order
                    int i = start;
                    while (i > stop) {
                        i += step;
                    }
                    i -= step;

                    // swap start/stop, make step positive
                    stop = start + 1;
                    start = i;
                    step = -step;
                }

                int curr, skip, move;
                // skip: the next position we should skip
                // curr: the next position we should fill in data
                // move: the next position we will check
                curr = skip = move = start;

                while (curr < stop && move < stop) {
                    if (move != skip) {
                        _data[curr++] = _data[move];
                    } else
                        skip += step;
                    move++;
                }
                while (stop < _data.Count) {
                    _data[curr++] = _data[stop++];
                }
                while (_data.Count > curr) {
                    _data.RemoveAt(_data.Count - 1);
                }
            }

            [System.Diagnostics.CodeAnalysis.NotNull]
            public object? this[[NotNull]Slice index] {
                get {
                    if (index == null) throw PythonOps.TypeError("expected Slice, got None");

                    int start, stop, step;
                    index.indices(_data.Count, out start, out stop, out step);

                    array pa = new array(new string(_typeCode, 1), Missing.Value);
                    if (step < 0) {
                        for (int i = start; i > stop; i += step) {
                            pa._data.Add(_data[i]);
                        }
                    } else {
                        for (int i = start; i < stop; i += step) {
                            pa._data.Add(_data[i]);
                        }
                    }
                    return pa;
                }
                set {
                    if (index == null) throw PythonOps.TypeError("expected Slice, got None");

                    CheckSliceAssignType(value);

                    if (index.step != null) {
                        if (Object.ReferenceEquals(value, this)) value = this.tolist();

                        index.DoSliceAssign(SliceAssign, _data.Count, value);
                    } else {
                        int start, stop, step;
                        index.indices(_data.Count, out start, out stop, out step);
                        if (stop < start) {
                            stop = start;
                        }

                        SliceNoStep(value, start, stop);
                    }
                }
            }

            private void CheckSliceAssignType([System.Diagnostics.CodeAnalysis.NotNull]object? value) {
                if (!(value is array pa)) {
                    throw PythonOps.TypeError("can only assign array (not \"{0}\") to array slice", PythonTypeOps.GetName(value));
                } else if (pa._typeCode != _typeCode) {
                    throw PythonOps.TypeError("bad argument type for built-in operation");
                }
            }

            private void SliceNoStep(object value, int start, int stop) {
                // replace between start & stop w/ values
                IEnumerator ie = PythonOps.GetEnumerator(value);

                ArrayData newData = CreateData(_typeCode);
                for (int i = 0; i < start; i++) {
                    newData.Add(_data[i]);
                }

                while (ie.MoveNext()) {
                    newData.Add(ie.Current);
                }

                for (int i = Math.Max(stop, start); i < _data.Count; i++) {
                    newData.Add(_data[i]);
                }

                _data = newData;
            }

            public PythonTuple __reduce__() {
                return PythonOps.MakeTuple(
                    DynamicHelpers.GetPythonType(this),
                    PythonOps.MakeTuple(
                        typecode,
                        tolist()
                    ),
                    null
                );
            }

            public array __copy__() {
                return new array(typecode, this);
            }

            public array __deepcopy__([NotNull]array arg) {
                // we only have simple data so this is the same as a copy
                return arg.__copy__();
            }

            public PythonTuple __reduce_ex__(int version) {
                return __reduce__();
            }

            public PythonTuple __reduce_ex__() {
                return __reduce__();
            }

            private void SliceAssign(int index, object? value) {
                _data[index] = value;
            }

            public void tofile(CodeContext context, [NotNull]PythonIOModule._IOBase f) {
                f.write(context, tobytes());
            }

            public PythonList tolist() {
                PythonList res = new PythonList();
                for (int i = 0; i < _data.Count; i++) {
                    res.AddNoLock(this[i]);
                }
                return res;
            }

            public Bytes tobytes() {
                Stream s = ToStream();
                byte[] bytes = new byte[s.Length];
                s.Read(bytes, 0, (int)s.Length);
                return Bytes.Make(bytes);
            }

            public Bytes tostring(CodeContext/*!*/ context) {
                PythonOps.Warn(context, PythonExceptions.DeprecationWarning, "tostring() is deprecated. Use tobytes() instead.");
                return tobytes();
            }

            public string tounicode(CodeContext/*!*/ context) {
                if (_typeCode != 'u') throw PythonOps.ValueError("only 'u' arrays can be converted to unicode");

                return new string(((ArrayData<char>)_data).Data, 0, _data.Count);
            }

            public string/*!*/ typecode {
                get { return ScriptingRuntimeHelpers.CharToString(_typeCode); }
            }

            internal MemoryStream ToStream() {
                MemoryStream ms = new MemoryStream();
                ToStream(ms);
                ms.Seek(0, SeekOrigin.Begin);
                return ms;
            }

            internal void ToStream(Stream ms) {
                BinaryWriter bw = new BinaryWriter(ms, Encoding.Unicode);
                for (int i = 0; i < _data.Count; i++) {
                    switch (_typeCode) {
                        case 'c': bw.Write((byte)(char)_data[i]); break;
                        case 'b': bw.Write((sbyte)_data[i]); break;
                        case 'B': bw.Write((byte)_data[i]); break;
                        case 'u': bw.Write((char)_data[i]); break;
                        case 'h': bw.Write((short)_data[i]); break;
                        case 'H': bw.Write((ushort)_data[i]); break;
                        case 'l':
                        case 'i': bw.Write((int)_data[i]); break;
                        case 'L':
                        case 'I': bw.Write((uint)_data[i]); break;
                        case 'f': bw.Write((float)_data[i]); break;
                        case 'd': bw.Write((double)_data[i]); break;
                    }
                }
            }

            internal byte[] ToByteArray() {
                if (_data is ArrayData<byte> data) {
                    Debug.Assert(_typeCode == 'B');
                    var res = new byte[data.Count];
                    Array.Copy(data.Data, res, data.Count);
                    return res;
                }

                return ToStream().ToArray();
            }

            internal void Clear() {
                _data = CreateData(_typeCode);
            }

            internal void FromStream(Stream ms) {
                BinaryReader br = new BinaryReader(ms);

                if (_data is ArrayData<byte> data) {
                    Debug.Assert(_typeCode == 'B');
                    var length = (int)ms.Length;
                    data.AddRange(br.ReadBytes(length));
                    return;
                }

                for (int i = 0; i < ms.Length / itemsize; i++) {
                    object value;
                    switch (_typeCode) {
                        case 'c': value = (char)br.ReadByte(); break;
                        case 'b': value = (sbyte)br.ReadByte(); break;
                        case 'B': value = br.ReadByte(); break;
                        case 'u': value = ReadBinaryChar(br); break;
                        case 'h': value = br.ReadInt16(); break;
                        case 'H': value = br.ReadUInt16(); break;
                        case 'i': value = br.ReadInt32(); break;
                        case 'I': value = br.ReadUInt32(); break;
                        case 'l': value = br.ReadInt32(); break;
                        case 'L': value = br.ReadUInt32(); break;
                        case 'f': value = br.ReadSingle(); break;
                        case 'd': value = br.ReadDouble(); break;
                        default: throw new InvalidOperationException(); // should never happen
                    }
                    _data.Add(value);
                }
            }

            // a version of FromStream that overwrites starting at 'index'
            internal void FromStream(Stream ms, int index) {
                BinaryReader br = new BinaryReader(ms);

                for (int i = index; i < ms.Length / itemsize + index; i++) {
                    object value;
                    switch (_typeCode) {
                        case 'c': value = (char)br.ReadByte(); break;
                        case 'b': value = (sbyte)br.ReadByte(); break;
                        case 'B': value = br.ReadByte(); break;
                        case 'u': value = ReadBinaryChar(br); break;
                        case 'h': value = br.ReadInt16(); break;
                        case 'H': value = br.ReadUInt16(); break;
                        case 'i': value = br.ReadInt32(); break;
                        case 'I': value = br.ReadUInt32(); break;
                        case 'l': value = br.ReadInt32(); break;
                        case 'L': value = br.ReadUInt32(); break;
                        case 'f': value = br.ReadSingle(); break;
                        case 'd': value = br.ReadDouble(); break;
                        default: throw new InvalidOperationException(); // should never happen
                    }
                    _data[i] = value;
                }
            }

            // a version of FromStream that overwrites up to 'nbytes' bytes, starting at 'index' 
            // Returns the number of bytes written.
            internal long FromStream(Stream ms, int index, int nbytes) {
                BinaryReader br = new BinaryReader(ms);

                if (nbytes <= 0) {
                    return 0;
                }

                int len = Math.Min((int)(ms.Length - ms.Position), nbytes);
                for (int i = index; i < len / itemsize + index; i++) {
                    object value;
                    switch (_typeCode) {
                        case 'c': value = (char)br.ReadByte(); break;
                        case 'b': value = (sbyte)br.ReadByte(); break;
                        case 'B': value = br.ReadByte(); break;
                        case 'u':
                            value = ReadBinaryChar(br);
                            break;
                        case 'h': value = br.ReadInt16(); break;
                        case 'H': value = br.ReadUInt16(); break;
                        case 'i': value = br.ReadInt32(); break;
                        case 'I': value = br.ReadUInt32(); break;
                        case 'l': value = br.ReadInt32(); break;
                        case 'L': value = br.ReadUInt32(); break;
                        case 'f': value = br.ReadSingle(); break;
                        case 'd': value = br.ReadDouble(); break;
                        default: throw new InvalidOperationException(); // should never happen
                    }
                    _data[i] = value;
                }

                if (len % itemsize > 0) {
                    // we have some extra bytes that we need to do a partial read on.                  
                    byte[] curBytes = ToBytes(len / itemsize + index);
                    for (int i = 0; i < len % itemsize; i++) {
                        curBytes[i] = br.ReadByte();
                    }

                    _data[len / itemsize + index] = FromBytes(curBytes);
                }

                return len;
            }

            // br.ReadChar() doesn't read 16-bit chars, it reads 8-bit chars.
            private static object ReadBinaryChar(BinaryReader br) {
                byte byteVal = br.ReadByte();
                object value = (char)((br.ReadByte() << 8) | byteVal);
                return value;
            }

            private byte[] ToBytes(int index) {
                switch(_typeCode) {
                    case 'b': return new[] { (byte)(sbyte)_data[index] };
                    case 'B': return new[] { (byte)_data[index] };
                    case 'u': return BitConverter.GetBytes((char)_data[index]);
                    case 'h': return BitConverter.GetBytes((short)_data[index]);
                    case 'H': return BitConverter.GetBytes((ushort)_data[index]);
                    case 'i': return BitConverter.GetBytes((int)_data[index]);
                    case 'I': return BitConverter.GetBytes((uint)_data[index]);
                    case 'l': return BitConverter.GetBytes((int)_data[index]);
                    case 'L': return BitConverter.GetBytes((uint)_data[index]);
                    case 'q': return BitConverter.GetBytes((long)_data[index]);
                    case 'Q': return BitConverter.GetBytes((ulong)_data[index]);
                    case 'f': return BitConverter.GetBytes((float)_data[index]); 
                    case 'd': return BitConverter.GetBytes((double)_data[index]); 
                    default:
                        throw PythonOps.ValueError("bad typecode (must be b, B, u, h, H, i, I, l, L, q, Q, f or d)");
                }
            }

            private object FromBytes(byte[] bytes) {
                switch (_typeCode) {
                    case 'b': return (sbyte)bytes[0];
                    case 'B': return bytes[0];
                    case 'u': return BitConverter.ToChar(bytes, 0);
                    case 'h': return BitConverter.ToInt16(bytes, 0);
                    case 'H': return BitConverter.ToUInt16(bytes, 0);
                    case 'i': return BitConverter.ToInt32(bytes, 0);
                    case 'I': return BitConverter.ToUInt32(bytes, 0);
                    case 'l': return BitConverter.ToInt32(bytes, 0);
                    case 'L': return BitConverter.ToInt32(bytes, 0);
                    case 'q': return BitConverter.ToInt64(bytes, 0);
                    case 'Q': return BitConverter.ToInt64(bytes, 0);
                    case 'f': return BitConverter.ToSingle(bytes, 0);
                    case 'd': return BitConverter.ToDouble(bytes, 0);
                    default:
                        throw PythonOps.ValueError("bad typecode (must be b, B, u, h, H, i, I, l, L, q, Q, f or d)");
                }
            }

            #region IStructuralEquatable Members

            public const object __hash__ = null;

            int IStructuralEquatable.GetHashCode(IEqualityComparer comparer) {
                IStructuralEquatable dataTuple;

                switch(_typeCode) {
                    case 'b': dataTuple = PythonTuple.MakeTuple(((ArrayData<sbyte>)_data).Data); break;
                    case 'B': dataTuple = PythonTuple.MakeTuple(((ArrayData<byte>)_data).Data); break;
                    case 'u': dataTuple = PythonTuple.MakeTuple(((ArrayData<char>)_data).Data); break;
                    case 'h': dataTuple = PythonTuple.MakeTuple(((ArrayData<short>)_data).Data); break;
                    case 'H': dataTuple = PythonTuple.MakeTuple(((ArrayData<ushort>)_data).Data); break;
                    case 'i': dataTuple = PythonTuple.MakeTuple(((ArrayData<int>)_data).Data); break;
                    case 'I': dataTuple = PythonTuple.MakeTuple(((ArrayData<uint>)_data).Data); break;
                    case 'l': dataTuple = PythonTuple.MakeTuple(((ArrayData<int>)_data).Data); break;
                    case 'L': dataTuple = PythonTuple.MakeTuple(((ArrayData<uint>)_data).Data); break;
                    case 'q': dataTuple = PythonTuple.MakeTuple(((ArrayData<long>)_data).Data); break;
                    case 'Q': dataTuple = PythonTuple.MakeTuple(((ArrayData<ulong>)_data).Data); break;
                    case 'f': dataTuple = PythonTuple.MakeTuple(((ArrayData<float>)_data).Data); break;
                    case 'd': dataTuple = PythonTuple.MakeTuple(((ArrayData<double>)_data).Data); break;
                    default:
                        throw PythonOps.ValueError("bad typecode (must be b, B, u, h, H, i, I, l, L, q, Q, f or d)");
                }

                return dataTuple.GetHashCode(comparer);
            }

            bool IStructuralEquatable.Equals(object? other, IEqualityComparer comparer) {
                if (!(other is array pa)) return false;

                if (_data.Count != pa._data.Count) return false;

                for (int i = 0; i < _data.Count; i++) {
                    if (!comparer.Equals(_data[i], pa._data[i])) {
                        return false;
                    }
                }

                return true;
            }

            #endregion

            #region IEnumerable Members

            IEnumerator IEnumerable.GetEnumerator() {
                for (int i = 0; i < _data.Count; i++) {
                    yield return _data[i];
                }
            }

            #endregion

            #region ICodeFormattable Members

            public virtual string/*!*/ __repr__(CodeContext/*!*/ context) {
                string res = "array('" + typecode.ToString() + "'";
                if (_data.Count == 0) {
                    return res + ")";
                }

                StringBuilder sb = new StringBuilder(res);
                if (_typeCode == 'c' || _typeCode == 'u') {
                    char quote = '\'';
                    string s = new string(((ArrayData<char>)_data).Data, 0, _data.Count);
                    if (s.IndexOf('\'') != -1 && s.IndexOf('\"') == -1) {
                        quote = '\"';
                    }

                    if (_typeCode == 'u') {
                        sb.Append(", u");
                    } else {
                        sb.Append(", ");
                    }
                    sb.Append(quote);
                    
                    sb.Append(StringOps.ReprEncode(s, quote));
                    sb.Append(quote);
                    sb.Append(")");
                } else {
                    sb.Append(", [");
                    for (int i = 0; i < _data.Count; i++) {
                        if (i > 0) {
                            sb.Append(", ");
                        }

                        sb.Append(PythonOps.Repr(context, this[i]));
                    }
                    sb.Append("])");
                }

                return sb.ToString();
            }

            #endregion

            #region IWeakReferenceable Members

            WeakRefTracker? IWeakReferenceable.GetWeakRef() {
                return _tracker;
            }

            bool IWeakReferenceable.SetWeakRef(WeakRefTracker value) {
                _tracker = value;
                return true;
            }

            void IWeakReferenceable.SetFinalizer(WeakRefTracker value) {
                _tracker = value;
            }

            #endregion

            #region IPythonContainer Members

            public int __len__() {
                return _data.Count;
            }

            public bool __contains__(object? value) {
                return _data.Contains(value);
            }

            #endregion

            #region IRichComparable Members

            private bool TryCompare(object? other, out int res) {
                if (!(other is array pa) || pa.typecode != typecode) {
                    res = 0;
                    return false;
                }

                if (pa._data.Count != _data.Count) {
                    res = _data.Count - pa._data.Count;
                } else {
                    res = 0;
                    for (int i = 0; i < pa._data.Count && res == 0; i++) {
                        res = PythonOps.Compare(_data[i], pa._data[i]);
                    }
                }

                return true;
            }

            [return: MaybeNotImplemented]
            public static object operator >([NotNull]array self, object? other) {
                int res;
                if (!self.TryCompare(other, out res)) {
                    return NotImplementedType.Value;
                }

                return ScriptingRuntimeHelpers.BooleanToObject(res > 0);
            }

            [return: MaybeNotImplemented]
            public static object operator <([NotNull]array self, object? other) {
                int res;
                if (!self.TryCompare(other, out res)) {
                    return NotImplementedType.Value;
                }

                return ScriptingRuntimeHelpers.BooleanToObject(res < 0);
            }

            [return: MaybeNotImplemented]
            public static object operator >=([NotNull]array self, object? other) {
                int res;
                if (!self.TryCompare(other, out res)) {
                    return NotImplementedType.Value;
                }

                return ScriptingRuntimeHelpers.BooleanToObject(res >= 0);
            }

            [return: MaybeNotImplemented]
            public static object operator <=([NotNull]array self, object? other) {
                int res;
                if (!self.TryCompare(other, out res)) {
                    return NotImplementedType.Value;
                }

                return ScriptingRuntimeHelpers.BooleanToObject(res <= 0);
            }

            #endregion

            #region ICollection Members

            void ICollection.CopyTo(Array array, int index) {
                throw new NotImplementedException();
            }

            int ICollection.Count {
                get { return __len__(); }
            }

            bool ICollection.IsSynchronized {
                get { return false; }
            }

            object ICollection.SyncRoot {
                get { return this; }
            }

            #endregion

            #region IList<object> Members

            int IList<object>.IndexOf(object item) {
                return _data.IndexOf(item);
            }

            void IList<object>.Insert(int index, object item) {
                insert(index, item);
            }

            void IList<object>.RemoveAt(int index) {
                __delitem__(index);
            }

            #endregion

            #region ICollection<object> Members

            void ICollection<object>.Add(object item) {
                append(item);
            }

            void ICollection<object>.Clear() {
                __delitem__(new Slice(null, null));
            }

            bool ICollection<object>.Contains(object item) {
                return __contains__(item);
            }

            void ICollection<object>.CopyTo(object[] array, int arrayIndex) {
                throw new NotImplementedException();
            }

            int ICollection<object>.Count {
                get { return __len__(); }
            }

            bool ICollection<object>.IsReadOnly {
                get { return false; }
            }

            bool ICollection<object>.Remove(object item) {
                try {
                    remove(item);
                    return true;
                } catch (ArgumentException) {
                    return false;
                }
            }

            #endregion

            #region IEnumerable<object> Members

            IEnumerator<object> IEnumerable<object>.GetEnumerator() {
                for (int i = 0; i < _data.Count; i++) {
                    yield return _data[i];
                }
            }

            #endregion

            #region IBufferProtocol Members

            object IBufferProtocol.GetItem(int index) => this[index];

            void IBufferProtocol.SetItem(int index, object value) {
                this[index] = value;
            }

            void IBufferProtocol.SetSlice(Slice index, object value) {
                this[index] = value;
            }

            int IBufferProtocol.ItemCount => _data.Count;

            string IBufferProtocol.Format => _typeCode.ToString();

            BigInteger IBufferProtocol.ItemSize => itemsize;

            BigInteger IBufferProtocol.NumberDimensions => 1;

            bool IBufferProtocol.ReadOnly => false;

            IList<BigInteger> IBufferProtocol.GetShape(int start, int? end) => new[] { (BigInteger)(end ?? _data.Count) - start };

            PythonTuple IBufferProtocol.Strides => PythonTuple.MakeTuple(itemsize);

            PythonTuple? IBufferProtocol.SubOffsets => null;

            Bytes IBufferProtocol.ToBytes(int start, int? end) {
                if (start == 0 && end == null) {
                    return tobytes();
                }

                return ((array)this[new Slice(start, end)]).tobytes();
            }

            PythonList IBufferProtocol.ToList(int start, int? end) {
                if (start == 0 && end == null) {
                    return tolist();
                }

                return ((array)this[new Slice(start, end)]).tolist();
            }

            #endregion
        }
    }
}
