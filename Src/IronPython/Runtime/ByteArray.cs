// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime {
    /// <summary>
    /// bytearray(string, encoding[, errors]) -> bytearray
    /// bytearray(iterable) -> bytearray
    /// 
    /// Construct a mutable bytearray object from:
    ///  - an iterable yielding values in range(256), including:
    ///     + a list of integer values
    ///     + a bytes, bytearray, buffer, or array object
    ///  - a text string encoded using the specified encoding
    ///  
    /// bytearray([int]) -> bytearray
    /// 
    /// Construct a zero-initialized bytearray of the specified length.
    /// (default=0)
    /// </summary>
    [PythonType("bytearray")]
    public class ByteArray : IList<byte>, IReadOnlyList<byte>, ICodeFormattable, IBufferProtocol {
        //  _bytes is readonly to ensure proper size locking during buffer exports
        private readonly ArrayData<byte> _bytes;

        public ByteArray() {
            _bytes = new ArrayData<byte>(0);
        }

        private ByteArray(ArrayData<byte> bytes) {
            _bytes = bytes;
        }

        private ByteArray(ReadOnlySpan<byte> bytes) {
            _bytes = new ArrayData<byte>(bytes);
        }

        internal ByteArray(IEnumerable<byte> bytes) {
            _bytes = new ArrayData<byte>(bytes);
        }

        public void __init__() {
            lock (this) {
                _bytes.Clear();
            }
        }

        public void __init__(int source) {
            lock (this) {
                if (source < 0) throw PythonOps.ValueError("negative count");
                _bytes.Clear();
                if (source > 0) {
                    _bytes.Add(0);
                    _bytes.InPlaceMultiply(source);
                }
            }
        }

        public void __init__([NotNull]IBufferProtocol source) {
            if (Converter.TryConvertToIndex(source, out int size)) {
                __init__(size);
            } else {
                lock (this) {
                    _bytes.Clear();
                    using IPythonBuffer buffer = source.GetBuffer(BufferFlags.FullRO);
                    _bytes.AddRange(buffer);
                }
            }
        }

        public void __init__(CodeContext context, object? source) {
            if (Converter.TryConvertToIndex(source, out int size)) {
                __init__(size);
            } else if (source is IEnumerable<byte> en) {
                lock (this) {
                    _bytes.Clear();
                    _bytes.AddRange(en);
                }
            } else {
                lock (this) {
                    _bytes.Clear();
                }
                IEnumerator ie = PythonOps.GetEnumerator(context, source);
                while (ie.MoveNext()) {
                    Add(ByteOps.GetByte(ie.Current));
                }
            }
        }

        public void __init__([NotNull]string @string) {
            throw PythonOps.TypeError("string argument without an encoding");
        }

        public void __init__(CodeContext context, [NotNull]string source, [NotNull]string encoding, [NotNull]string errors = "strict") {
            lock (this) {
                _bytes.Clear();
                _bytes.AddRange(StringOps.encode(context, source, encoding, errors));
            }
        }

        [PythonHidden]
        internal ArrayData<byte> UnsafeByteList {
            get => _bytes;
        }

        #region Public Mutable Sequence API

        public void append(int item) {
            lock (this) {
                _bytes.Add(item.ToByteChecked());
            }
        }

        public void append(object? item) {
            lock (this) {
                _bytes.Add(ByteOps.GetByte(item));
            }
        }

        public void extend([NotNull]IEnumerable<byte> seq) {
            using (new OrderedLocker(this, seq)) {
                // use the original count for if we're extending this w/ this
                _bytes.AddRange(seq);
            }
        }

        public void extend(CodeContext context, object? seq) {
            // We don't make use of the length hint when extending the byte array.
            // However, in order to match CPython behavior with invalid length hints we
            // we need to go through the motions and get the length hint and attempt
            // to convert it to an int.

            extend(ByteOps.GetBytes(seq, useHint: true, context));
        }

        public void insert(int index, int value) {
            lock (this) {
                if (index >= Count) {
                    append(value);
                    return;
                }

                index = PythonOps.FixSliceIndex(index, Count);

                _bytes.Insert(index, value.ToByteChecked());
            }
        }

        public void insert(int index, object? value) {
            insert(index, Converter.ConvertToIndex(value));
        }

        public int pop() {
            lock (this) {
                if (Count == 0) {
                    throw PythonOps.IndexError("pop off of empty bytearray");
                }

                int res = _bytes[_bytes.Count - 1];
                _bytes.RemoveAt(_bytes.Count - 1);
                return res;
            }
        }

        public int pop(int index) {
            lock (this) {
                if (Count == 0) {
                    throw PythonOps.IndexError("pop off of empty bytearray");
                }

                index = PythonOps.FixIndex(index, Count);

                int ret = _bytes[index];
                _bytes.RemoveAt(index);
                return ret;
            }
        }

        private void RemoveByte(byte value) {
            var idx = _bytes.IndexOfByte(value, 0, _bytes.Count);
            if (idx == -1)
                throw PythonOps.ValueError("value not found in bytearray");
            _bytes.RemoveAt(idx);
        }

        public void remove(int value) {
            lock (this) {
                RemoveByte(value.ToByteChecked());
            }
        }

        public void remove(object? value) {
            lock (this) {
                RemoveByte(ByteOps.GetByte(value));
            }
        }

        public void reverse() {
            lock (this) {
                _bytes.Reverse();
            }
        }

        [SpecialName]
        public ByteArray InPlaceAdd([NotNull] ByteArray other) {
            using (new OrderedLocker(this, other)) {
                _bytes.AddRange(other._bytes);
                return this;
            }
        }

        [SpecialName]
        public ByteArray InPlaceAdd([NotNull] IBufferProtocol other) {
            lock (this) {
                using var buf = other.GetBufferNoThrow();
                if (buf is null) throw TypeErrorForConcat(this, other);
                _bytes.AddRange(buf);
                return this;
            }
        }

        [SpecialName]
        public ByteArray InPlaceMultiply(int len) {
            lock (this) {
                _bytes.InPlaceMultiply(len);
                return this;
            }
        }

        #endregion

        #region Public Python API surface

        public ByteArray capitalize() {
            lock (this) {
                return new ByteArray(_bytes.Capitalize());
            }
        }

        public ByteArray center(int width) => center(width, (byte)' ');

        public ByteArray center(int width, [BytesLike, NotNull]IList<byte> fillchar)
            => center(width, fillchar.ToByte("center", 2));

        private ByteArray center(int width, byte fillchar) {
            lock (this) {
                var res = _bytes.TryCenter(width, fillchar);
                return res == null ? CopyThis() : new ByteArray(res);
            }
        }

        public void clear() => Clear();

        public ByteArray copy() => CopyThis();

        public int count([BytesLike, NotNull]IList<byte> sub) {
            lock (this) {
                return _bytes.CountOf(sub, 0, _bytes.Count);
            }
        }

        public int count([BytesLike, NotNull]IList<byte> sub, int start) {
            lock (this) {
                return _bytes.CountOf(sub, start, _bytes.Count);
            }
        }

        public int count([BytesLike, NotNull]IList<byte> sub, int start, int end) {
            lock (this) {
                return _bytes.CountOf(sub, start, end);
            }
        }

        public int count([BytesLike, NotNull]IList<byte> sub, object? start)
            => count(sub, start, null);

        public int count([BytesLike, NotNull]IList<byte> sub, object? start, object? end) {
            int istart = start != null ? Converter.ConvertToIndex(start) : 0;
            lock (this) {
                int iend = end != null ? Converter.ConvertToIndex(end) : _bytes.Count;
                return _bytes.CountOf(sub, istart, iend); 
            }
        }

        public int count(BigInteger @byte)
            => count(Bytes.FromByte(@byte.ToByteChecked()));

        public int count(BigInteger @byte, int start)
            => count(Bytes.FromByte(@byte.ToByteChecked()), start);

        public int count(BigInteger @byte, int start, int end)
            => count(Bytes.FromByte(@byte.ToByteChecked()), start, end);

        public int count(BigInteger @byte, object? start)
            => count(Bytes.FromByte(@byte.ToByteChecked()), start);

        public int count(BigInteger @byte, object? start, object? end)
            => count(Bytes.FromByte(@byte.ToByteChecked()), start, end);

        public string decode(CodeContext context, [NotNull]string encoding = "utf-8", [NotNull]string errors = "strict") {
            lock (this) {
                using var mv = new MemoryView(this, readOnly: true);
                return StringOps.RawDecode(context, mv, encoding, errors);
            }
        }

        public string decode(CodeContext context, [NotNull]Encoding encoding, [NotNull]string errors = "strict") {
            lock (this) {
                using var bufer = ((IBufferProtocol)this).GetBuffer();
                return StringOps.DoDecode(context, bufer, errors, StringOps.GetEncodingName(encoding, normalize: false), encoding);
            }
        }

        public bool endswith([BytesLike, NotNull]IList<byte> suffix) {
            lock (this) {
                return _bytes.EndsWith(suffix);
            }
        }

        public bool endswith([BytesLike, NotNull]IList<byte> suffix, int start) {
            lock (this) {
                return _bytes.EndsWith(suffix, start, _bytes.Count);
            }
        }

        public bool endswith([BytesLike, NotNull]IList<byte> suffix, int start, int end) {
            lock (this) {
                return _bytes.EndsWith(suffix, start, end);
            }
        }

        public bool endswith([BytesLike, NotNull]IList<byte> suffix, object? start) {
            return endswith(suffix, start, null);
        }

        public bool endswith([BytesLike, NotNull]IList<byte> suffix, object? start, object? end) {
            int istart = start != null ? Converter.ConvertToIndex(start) : 0;
            lock (this) {
                int iend = end != null ? Converter.ConvertToIndex(end) : _bytes.Count;
                return _bytes.EndsWith(suffix, istart, iend);
            }
        }

        public bool endswith([NotNull]PythonTuple suffix) {
            lock (this) {
                return _bytes.EndsWith(suffix);
            }
        }

        public bool endswith([NotNull]PythonTuple suffix, int start) {
            lock (this) {
                return _bytes.EndsWith(suffix, start, _bytes.Count);
            }
        }

        public bool endswith([NotNull]PythonTuple suffix, int start, int end) {
            lock (this) {
                return _bytes.EndsWith(suffix, start, end);
            }
        }

        public bool endswith([NotNull]PythonTuple suffix, object? start) {
            return endswith(suffix, start, null);
        }

        public bool endswith([NotNull]PythonTuple suffix, object? start, object? end) {
            int istart = start != null ? Converter.ConvertToIndex(start) : 0;
            lock (this) {
                int iend = end != null ? Converter.ConvertToIndex(end) : _bytes.Count;
                return _bytes.EndsWith(suffix, istart, iend);
            }
        }

        [Documentation("\n" + // hidden overload
            "Return True if self ends with the specified suffix, False otherwise.\n" +
            "With optional start, test self beginning at that position.\n" +
            "With optional end, stop comparing self at that position.\n" +
            "suffix can also be a tuple of bytes-like objects to try.")]
        public bool endswith(object? suffix, object? start = null, object? end = null) {
            if (suffix is IList<byte> blist) return endswith(blist, start, end);
            if (suffix is PythonTuple tuple) return endswith(tuple, start, end);
            throw PythonOps.TypeError("{0} first arg must be a bytes-like object or a tuple of bytes-like objects, not {1}", nameof(endswith), PythonOps.GetPythonTypeName(suffix));
        }

        public ByteArray expandtabs() {
            return expandtabs(8);
        }

        public ByteArray expandtabs(int tabsize) {
            lock (this) {
                return new ByteArray(_bytes.ExpandTabs(tabsize));
            }
        }

        public int find([BytesLike, NotNull]IList<byte> sub) {
            lock (this) {
                return _bytes.Find(sub, 0, _bytes.Count);
            }
        }

        public int find([BytesLike, NotNull]IList<byte> sub, int start) {
            lock (this) {
                return _bytes.Find(sub, start, _bytes.Count);
            }
        }

        public int find([BytesLike, NotNull]IList<byte> sub, int start, int end) {
            lock (this) {
                return _bytes.Find(sub, start, end);
            }
        }

        public int find([BytesLike, NotNull]IList<byte> sub, object? start)
            => find(sub, start, null);

        public int find([BytesLike, NotNull]IList<byte> sub, object? start, object? end) {
            int istart = start != null ? Converter.ConvertToIndex(start) : 0;
            lock (this) {
                int iend = end != null ? Converter.ConvertToIndex(end) : _bytes.Count;
                return _bytes.Find(sub, istart, iend);
            }
        }

        public int find(BigInteger @byte) {
            lock (this) {
                return _bytes.IndexOfByte(@byte.ToByteChecked(), 0, _bytes.Count);
            }
        }
        public int find(BigInteger @byte, int start) {
            lock (this) {
                return _bytes.IndexOfByte(@byte.ToByteChecked(), start, _bytes.Count);
            }
        }

        public int find(BigInteger @byte, int start, int end) {
            lock (this) {
                return _bytes.IndexOfByte(@byte.ToByteChecked(), start, end);
            }
        }

        public int find(BigInteger @byte, object? start)
            => find(@byte, start, null);

        public int find(BigInteger @byte, object? start, object? end) {
            int istart = start != null ? Converter.ConvertToIndex(start) : 0;
            lock (this) {
                int iend = end != null ? Converter.ConvertToIndex(end) : _bytes.Count;
                return _bytes.IndexOfByte(@byte.ToByteChecked(), istart, iend);
            }
        }

        public static ByteArray fromhex([NotNull]string @string) {
            return new ByteArray(IListOfByteOps.FromHex(@string));
        }

        public string hex() => Bytes.ToHex(_bytes.AsByteSpan()); // new in CPython 3.5

        public int index([BytesLike, NotNull]IList<byte> sub) {
            lock (this) {
                return index(sub, 0, _bytes.Count);
            }
        }

        public int index([BytesLike, NotNull]IList<byte> sub, int start) {
            lock (this) {
                return index(sub, start, _bytes.Count);
            }
        }

        public int index([BytesLike, NotNull]IList<byte> sub, int start, int end) {
            lock (this) {
                int res = find(sub, start, end);
                if (res == -1) {
                    throw PythonOps.ValueError("subsection not found");
                }

                return res;
            }
        }

        public int index([BytesLike, NotNull]IList<byte> sub, object? start)
            => index(sub, start, null);

        public int index([BytesLike, NotNull]IList<byte> sub, object? start, object? end) {
            int istart = start != null ? Converter.ConvertToIndex(start) : 0;
            lock (this) {
                int iend = end != null ? Converter.ConvertToIndex(end) : _bytes.Count;
                return index(sub, istart, iend);
            }
        }

        public int index(BigInteger @byte)
            => index(Bytes.FromByte(@byte.ToByteChecked()));

        public int index(BigInteger @byte, int start)
            => index(Bytes.FromByte(@byte.ToByteChecked()), start);

        public int index(BigInteger @byte, int start, int end)
            => index(Bytes.FromByte(@byte.ToByteChecked()), start, end);

        public int index(BigInteger @byte, object? start)
            => index(Bytes.FromByte(@byte.ToByteChecked()), start, null);

        public int index(BigInteger @byte, object? start, object? end)
            => index(Bytes.FromByte(@byte.ToByteChecked()), start, end);

        public bool isalnum() {
            lock (this) {
                return _bytes.IsAlphaNumeric();
            }
        }

        public bool isalpha() {
            lock (this) {
                return _bytes.IsLetter();
            }
        }

        public bool isdigit() {
            lock (this) {
                return _bytes.IsDigit();
            }
        }

        public bool islower() {
            lock (this) {
                return _bytes.IsLower();
            }
        }

        public bool isspace() {
            lock (this) {
                return _bytes.IsWhiteSpace();
            }
        }

        /// <summary>
        /// return true if self is a titlecased string and there is at least one
        /// character in self; also, uppercase characters may only follow uncased
        /// characters (e.g. whitespace) and lowercase characters only cased ones. 
        /// return false otherwise.
        /// </summary>
        public bool istitle() {
            lock (this) {
                return _bytes.IsTitle();
            }
        }

        public bool isupper() {
            lock (this) {
                return _bytes.IsUpper();
            }
        }

        /// <summary>
        /// Return a string which is the concatenation of the strings 
        /// in the sequence seq. The separator between elements is the 
        /// string providing this method
        /// </summary>
        public ByteArray join(object? sequence) {
            IEnumerator seq = PythonOps.GetEnumerator(sequence);
            if (!seq.MoveNext()) {
                return new ByteArray();
            }

            // check if we have just a sequnce of just one value - if so just
            // return that value.
            object? curVal = seq.Current;
            if (!seq.MoveNext()) {
                return JoinOne(curVal);
            }

            List<byte> ret = new List<byte>();
            ByteOps.AppendJoin(curVal, 0, ret);

            int index = 1;
            do {
                ret.AddRange(this);

                ByteOps.AppendJoin(seq.Current, index, ret);

                index++;
            } while (seq.MoveNext());

            return new ByteArray(ret);
        }

        public ByteArray join([NotNull]PythonList sequence) {
            if (sequence.__len__() == 0) {
                return new ByteArray();
            }

            lock (this) {
                if (sequence.__len__() == 1) {
                    return JoinOne(sequence[0]);
                }

                List<byte> ret = new List<byte>();
                ByteOps.AppendJoin(sequence._data[0], 0, ret);
                for (int i = 1; i < sequence._size; i++) {
                    ret.AddRange(this);
                    ByteOps.AppendJoin(sequence._data[i], i, ret);
                }

                return new ByteArray(ret);
            }
        }

        public ByteArray ljust(int width) {
            return ljust(width, (byte)' ');
        }

        public ByteArray ljust(int width, [BytesLike, NotNull]IList<byte> fillchar) {
            return ljust(width, fillchar.ToByte("ljust", 2));
        }

        private ByteArray ljust(int width, byte fillchar) {
            lock (this) {
                int spaces = width - _bytes.Count;
                if (spaces <= 0) {
                    return CopyThis();
                }

                List<byte> ret = new List<byte>(width);
                ret.AddRange(_bytes);
                for (int i = 0; i < spaces; i++) {
                    ret.Add(fillchar);
                }
                return new ByteArray(ret);
            }
        }

        public ByteArray lower() {
            lock (this) {
                return new ByteArray(_bytes.ToLower());
            }
        }

        public ByteArray lstrip() {
            lock (this) {
                var res = _bytes.LeftStrip();
                return res == null ? CopyThis() : new ByteArray(res);
            }
        }

        public ByteArray lstrip([BytesLike]IList<byte>? chars) {
            if (chars == null) return lstrip();
            lock (this) {
                var res = _bytes.LeftStrip(chars);
                return res == null ? CopyThis() : new ByteArray(res);
            }
        }

        public static Bytes maketrans([BytesLike, NotNull]IList<byte> from, [BytesLike, NotNull]IList<byte> to)
            => Bytes.maketrans(from, to);

        [return: SequenceTypeInfo(typeof(ByteArray))]
        public PythonTuple partition([BytesLike, NotNull]IList<byte> sep) {
            if (sep.Count == 0) {
                throw PythonOps.ValueError("empty separator");
            }

            object[] obj = new object[3] { new ByteArray(), new ByteArray(), new ByteArray() };

            if (_bytes.Count != 0) {
                int index = find(sep);
                if (index == -1) {
                    obj[0] = CopyThis();
                } else {
                    obj[0] = new ByteArray(_bytes.Substring(0, index));
                    obj[1] = new ByteArray(new List<byte>(sep));
                    obj[2] = new ByteArray(_bytes.Substring(index + sep.Count, _bytes.Count - index - sep.Count));
                }
            }

            return PythonTuple.MakeTuple(obj);
        }

        public ByteArray replace([BytesLike, NotNull]IList<byte> old, [BytesLike, NotNull]IList<byte> @new)
            => replace(old, @new, -1);

        public ByteArray replace([BytesLike, NotNull]IList<byte> old, [BytesLike, NotNull]IList<byte> @new, int count) {
            if (count == 0) {
                return CopyThis();
            }

            return new ByteArray(_bytes.Replace(old, @new, ref count));
        }

        public int rfind([BytesLike, NotNull]IList<byte> sub) {
            lock (this) {
                return _bytes.ReverseFind(sub, 0, _bytes.Count);
            }
        }

        public int rfind([BytesLike, NotNull]IList<byte> sub, int start) {
            lock (this) {
                return _bytes.ReverseFind(sub, start, _bytes.Count);
            }
        }

        public int rfind([BytesLike, NotNull]IList<byte> sub, int start, int end) {
            lock (this) {
                return _bytes.ReverseFind(sub, start, end);
            }
        }

        public int rfind([BytesLike, NotNull]IList<byte> sub, object? start)
            => rfind(sub, start, null);

        public int rfind([BytesLike, NotNull]IList<byte> sub, object? start, object? end) {
            int istart = start != null ? Converter.ConvertToIndex(start) : 0;
            lock (this) {
                int iend = end != null ? Converter.ConvertToIndex(end) : _bytes.Count;
                return _bytes.ReverseFind(sub, istart, iend);
            }
        }

        public int rfind(BigInteger @byte)
            => rfind(Bytes.FromByte(@byte.ToByteChecked()));

        public int rfind(BigInteger @byte, int start)
            => rfind(Bytes.FromByte(@byte.ToByteChecked()), start);

        public int rfind(BigInteger @byte, int start, int end)
            => rfind(Bytes.FromByte(@byte.ToByteChecked()), start, end);

        public int rfind(BigInteger @byte, object? start)
            => rfind(Bytes.FromByte(@byte.ToByteChecked()), start, null);

        public int rfind(BigInteger @byte, object? start, object? end)
            => rfind(Bytes.FromByte(@byte.ToByteChecked()), start, end);

        public int rindex([BytesLike, NotNull]IList<byte> sub) {
            lock (this) {
                return rindex(sub, 0, _bytes.Count);
            }
        }

        public int rindex([BytesLike, NotNull]IList<byte> sub, int start) {
            lock (this) {
                return rindex(sub, start, _bytes.Count);
            }
        }

        public int rindex([BytesLike, NotNull]IList<byte> sub, int start, int end) {
            int ret = rfind(sub, start, end);
            if (ret == -1) {
                throw PythonOps.ValueError("subsection not found");
            }

            return ret;
        }

        public int rindex([BytesLike, NotNull]IList<byte> sub, object? start)
            => rindex(sub, start, null);

        public int rindex([BytesLike, NotNull]IList<byte> sub, object? start, object? end) {
            int istart = start != null ? Converter.ConvertToIndex(start) : 0;
            lock (this) {
                int iend = end != null ? Converter.ConvertToIndex(end) : _bytes.Count;
                return rindex(sub, istart, iend);
            }
        }

        public int rindex(BigInteger @byte)
            => rindex(Bytes.FromByte(@byte.ToByteChecked()));

        public int rindex(BigInteger @byte, int start)
            => rindex(Bytes.FromByte(@byte.ToByteChecked()), start);

        public int rindex(BigInteger @byte, int start, int end)
            => rindex(Bytes.FromByte(@byte.ToByteChecked()), start, end);

        public int rindex(BigInteger @byte, object? start)
            => rindex(Bytes.FromByte(@byte.ToByteChecked()), start, null);

        public int rindex(BigInteger @byte, object? start, object? end)
            => rindex(Bytes.FromByte(@byte.ToByteChecked()), start, end);

        public ByteArray rjust(int width) {
            return rjust(width, (byte)' ');
        }

        public ByteArray rjust(int width, [BytesLike, NotNull]IList<byte> fillchar) {
            return rjust(width, fillchar.ToByte("rjust", 2));
        }

        private ByteArray rjust(int width, int fillchar) {
            byte fill = fillchar.ToByteChecked();

            lock (this) {
                int spaces = width - _bytes.Count;
                if (spaces <= 0) {
                    return CopyThis();
                }

                List<byte> ret = new List<byte>(width);
                for (int i = 0; i < spaces; i++) {
                    ret.Add(fill);
                }
                ret.AddRange(_bytes);
                return new ByteArray(ret);
            }
        }

        [return: SequenceTypeInfo(typeof(ByteArray))]
        public PythonTuple rpartition([BytesLike, NotNull]IList<byte> sep) {
            if (sep.Count == 0) {
                throw PythonOps.ValueError("empty separator");
            }

            lock (this) {
                object[] obj = new object[3] { new ByteArray(), new ByteArray(), new ByteArray() };
                if (_bytes.Count != 0) {
                    int index = rfind(sep);
                    if (index == -1) {
                        obj[2] = CopyThis();
                    } else {
                        obj[0] = new ByteArray(_bytes.Substring(0, index));
                        obj[1] = new ByteArray(new List<byte>(sep));
                        obj[2] = new ByteArray(_bytes.Substring(index + sep.Count, Count - index - sep.Count));
                    }
                }
                return PythonTuple.MakeTuple(obj);
            }
        }

        [return: SequenceTypeInfo(typeof(ByteArray))]
        public PythonList rsplit([BytesLike]IList<byte>? sep = null, int maxsplit = -1) {
            lock (this) {
                return _bytes.RightSplit(sep, maxsplit, x => new ByteArray(new List<byte>(x)));
            }
        }

        public ByteArray rstrip() {
            lock (this) {
                var res = _bytes.RightStrip();
                return res == null ? CopyThis() : new ByteArray(res);
            }
        }

        public ByteArray rstrip([BytesLike]IList<byte>? chars) {
            if (chars == null) return rstrip();
            lock (this) {
                var res = _bytes.RightStrip(chars);
                return res == null ? CopyThis() : new ByteArray(res);
            }
        }

        [return: SequenceTypeInfo(typeof(ByteArray))]
        public PythonList split([BytesLike]IList<byte>? sep = null, int maxsplit = -1) {
            lock (this) {
                return _bytes.Split(sep, maxsplit, x => new ByteArray(x));
            }
        }

        [return: SequenceTypeInfo(typeof(ByteArray))]
        public PythonList splitlines() {
            return splitlines(false);
        }

        [return: SequenceTypeInfo(typeof(ByteArray))]
        public PythonList splitlines(bool keepends) {
            lock (this) {
                return _bytes.SplitLines(keepends, x => new ByteArray(x));
            }
        }

        public bool startswith([BytesLike, NotNull]IList<byte> prefix) {
            lock (this) {
                return _bytes.StartsWith(prefix);
            }
        }

        public bool startswith([BytesLike, NotNull]IList<byte> prefix, int start) {
            lock (this) {
                return _bytes.StartsWith(prefix, start, _bytes.Count);
            }
        }

        public bool startswith([BytesLike, NotNull]IList<byte> prefix, int start, int end) {
            lock (this) {
                return _bytes.StartsWith(prefix, start, end);
            }
        }

        public bool startswith([BytesLike, NotNull]IList<byte> prefix, object? start) {
            return startswith(prefix, start, null);
        }

        public bool startswith([BytesLike, NotNull]IList<byte> prefix, object? start, object? end) {
            int istart = start != null ? Converter.ConvertToIndex(start) : 0;
            lock (this) {
                int iend = end != null ? Converter.ConvertToIndex(end) : _bytes.Count;
                return _bytes.StartsWith(prefix, istart, iend);
            }
        }

        public bool startswith([NotNull]PythonTuple prefix) {
            lock (this) {
                return _bytes.StartsWith(prefix);
            }
        }

        public bool startswith([NotNull]PythonTuple prefix, int start) {
            lock (this) {
                return _bytes.StartsWith(prefix, start, _bytes.Count);
            }
        }

        public bool startswith([NotNull]PythonTuple prefix, int start, int end) {
            lock (this) {
                return _bytes.StartsWith(prefix, start, end);
            }
        }

        public bool startswith([NotNull]PythonTuple prefix, object? start) {
            return startswith(prefix, start, null);
        }

        public bool startswith([NotNull]PythonTuple prefix, object? start, object? end) {
            int istart = start != null ? Converter.ConvertToIndex(start) : 0;
            lock (this) {
                int iend = end != null ? Converter.ConvertToIndex(end) : _bytes.Count;
                return _bytes.StartsWith(prefix, istart, iend);
            }
        }

        [Documentation("\n" + // hidden overload
            "Return True if self starts with the specified prefix, False otherwise.\n" +
            "With optional start, test self beginning at that position.\n" +
            "With optional end, stop comparing self at that position.\n" +
            "prefix can also be a tuple of bytes-like objects to try.")]
        public bool startswith(object? prefix, object? start = null, object? end = null) {
            if (prefix is IList<byte> blist) return startswith(blist, start, end);
            if (prefix is PythonTuple tuple) return startswith(tuple, start, end);
            throw PythonOps.TypeError("{0} first arg must be a bytes-like object or a tuple of bytes-like objects, not {1}", nameof(startswith), PythonOps.GetPythonTypeName(prefix));
        }

        public ByteArray strip() {
            lock (this) {
                var res = _bytes.Strip();
                return res == null ? CopyThis() : new ByteArray(res);
            }
        }

        public ByteArray strip([BytesLike]IList<byte>? chars) {
            if (chars == null) return strip();
            lock (this) {
                var res = _bytes.Strip(chars);
                return res == null ? CopyThis() : new ByteArray(res);
            }
        }

        public ByteArray swapcase() {
            lock (this) {
                return new ByteArray(_bytes.SwapCase());
            }
        }

        public ByteArray title() {
            lock (this) {
                var res = _bytes.Title();
                return res == null ? CopyThis() : new ByteArray(res);
            }
        }

        private void ValidateTable(IList<byte>? table) {
            if (table != null && table.Count != 256) {
                throw PythonOps.ValueError("translation table must be 256 characters long");
            }
        }

        public ByteArray translate([BytesLike]IList<byte>? table) {
            ValidateTable(table);
            lock (this) {
                return new ByteArray(_bytes.Translate(table, null));
            }
        }


        public ByteArray translate([BytesLike]IList<byte>? table, [BytesLike, NotNull]IList<byte> delete) {
            ValidateTable(table);
            lock (this) {
                return new ByteArray(_bytes.Translate(table, delete));
            }
        }

        public ByteArray translate([BytesLike]IList<byte>? table, object? delete) {
            if (delete is IBufferProtocol bufferProtocol) {
                using var buffer = bufferProtocol.GetBuffer();
                return translate(table, buffer.AsReadOnlySpan().ToArray());
            }
            ValidateTable(table);
            throw PythonOps.TypeError("a bytes-like object is required, not '{0}", PythonOps.GetPythonTypeName(delete));
        }

        public ByteArray upper() {
            lock (this) {
                return new ByteArray(_bytes.ToUpper());
            }
        }

        public ByteArray zfill(int width) {
            lock (this) {
                int spaces = width - Count;
                if (spaces <= 0) {
                    return CopyThis();
                }

                return new ByteArray(_bytes.ZeroFill(width, spaces));
            }
        }

        public int __alloc__() {
            if (_bytes.Count == 0) {
                return 0;
            }

            return _bytes.Count + 1;
        }

        public bool __contains__([BytesLike, NotNull]IList<byte> bytes) {
            return this.IndexOf(bytes, 0) != -1;
        }

        public bool __contains__(int value) {
            return IndexOf(value.ToByteChecked()) != -1;
        }

        public bool __contains__(CodeContext context, object? value) {
            if (value is Extensible<int>) {
                return IndexOf(((Extensible<int>)value).Value.ToByteChecked()) != -1;
            } else if (value is BigInteger) {
                return IndexOf(((BigInteger)value).ToByteChecked()) != -1;
            } else if (value is Extensible<BigInteger>) {
                return IndexOf(((Extensible<BigInteger>)value).Value.ToByteChecked()) != -1;
            }

            throw PythonOps.TypeError("Type {0} doesn't support the buffer API", PythonOps.GetPythonTypeName(value));
        }

        public IEnumerator<int> __iter__()
            => new ByteArrayIterator(this);

        public PythonTuple __reduce__(CodeContext context) {
            return PythonTuple.MakeTuple(
                DynamicHelpers.GetPythonType(this),
                PythonTuple.MakeTuple(
                    PythonOps.MakeString(this),
                    "latin-1"
                ),
                GetType() == typeof(ByteArray) ? null : PythonOps.GetBoundAttr(context, this, "__dict__")
            );
        }

        private string Repr() {
            lock (this) {
                return "bytearray(" + _bytes.BytesRepr() + ")";
            }
        }

        public ByteArray __mod__(CodeContext context, object? value)
            => new ByteArray(StringFormatter.FormatBytes(context, UnsafeByteList.AsByteSpan(), value).AsSpan());

        public ByteArray __rmod__(CodeContext context, [NotNull] ByteArray value)
            => new ByteArray(StringFormatter.FormatBytes(context, value.UnsafeByteList.AsByteSpan(), this).AsSpan());

        [return: MaybeNotImplemented]
        public NotImplementedType __rmod__(CodeContext context, object? value) => NotImplementedType.Value;

        public virtual string __str__(CodeContext context) {
            if (context.LanguageContext.PythonOptions.BytesWarning != Microsoft.Scripting.Severity.Ignore) {
                PythonOps.Warn(context, PythonExceptions.BytesWarning, "str() on a bytearray instance");
            }
            return Repr();
        }

        public virtual string __repr__(CodeContext context) => Repr();

        public override string ToString() => Repr();

        public static ByteArray operator +([NotNull] ByteArray self, [NotNull] ByteArray other) {
            ByteArray res;

            lock (self) {
                res = self.CopyThis();
            }
            lock (other) {
                res._bytes.AddRange(other._bytes);
            }

            return res;
        }

        public static ByteArray operator +([NotNull] ByteArray self, [NotNull] IBufferProtocol other) {
            ByteArray res;

            lock (self) {
                res = self.CopyThis();
            }

            using var buf = other.GetBufferNoThrow();
            if (buf is null) throw TypeErrorForConcat(self, other);
            res._bytes.AddRange(buf);

            return res;
        }

        public static ByteArray operator +([NotNull]ByteArray self, object? other) {
            throw TypeErrorForConcat(self, other);
        }

        private static Exception TypeErrorForConcat(object self, object? other)
            => PythonOps.TypeError($"can't concat {PythonOps.GetPythonTypeName(self)} to {PythonOps.GetPythonTypeName(other)}");

        private static ByteArray MultiplyWorker(ByteArray self, int count) {
            lock (self) {
                if (count == 1) {
                    return self.CopyThis();
                }

                return new ByteArray(self._bytes.Multiply(count));
            }
        }

        public static ByteArray operator *([NotNull]ByteArray self, int count) => MultiplyWorker(self, count);

        public static object operator *([NotNull]ByteArray self, [NotNull]Index count)
            => PythonOps.MultiplySequence(MultiplyWorker, self, count, true);

        public static ByteArray operator *([NotNull]ByteArray self, object? count) {
            if (Converter.TryConvertToIndex(count, out int index)) {
                return self * index;
            }

            throw PythonOps.TypeErrorForUnIndexableObject(count);
        }

        public static ByteArray operator *(int count, [NotNull]ByteArray self) => MultiplyWorker(self, count);

        public static object operator *([NotNull]Index count, [NotNull]ByteArray self)
            => PythonOps.MultiplySequence(MultiplyWorker, self, count, false);

        public static ByteArray operator *(object? count, [NotNull]ByteArray self) {
            if (Converter.TryConvertToIndex(count, out int index)) {
                return index * self;
            }

            throw PythonOps.TypeErrorForUnIndexableObject(count);
        }

        public static bool operator >([NotNull]ByteArray x, [NotNull]ByteArray y) {
            using (new OrderedLocker(x, y)) {
                return x._bytes.Compare(y._bytes) > 0;
            }
        }

        public static bool operator <([NotNull]ByteArray x, [NotNull]ByteArray y) {
            using (new OrderedLocker(x, y)) {
                return x._bytes.Compare(y._bytes) < 0;
            }
        }

        public static bool operator >=([NotNull]ByteArray x, [NotNull]ByteArray y) {
            using (new OrderedLocker(x, y)) {
                return x._bytes.Compare(y._bytes) >= 0;
            }
        }

        public static bool operator <=([NotNull]ByteArray x, [NotNull]ByteArray y) {
            using (new OrderedLocker(x, y)) {
                return x._bytes.Compare(y._bytes) <= 0;
            }
        }

        public static bool operator >([NotNull]ByteArray x, [NotNull]Bytes y) {
            lock (x) {
                return x._bytes.Compare(y) > 0;
            }
        }

        public static bool operator <([NotNull]ByteArray x, [NotNull]Bytes y) {
            lock (x) {
                return x._bytes.Compare(y) < 0;
            }
        }

        public static bool operator >=([NotNull]ByteArray x, [NotNull]Bytes y) {
            lock (x) {
                return x._bytes.Compare(y) >= 0;
            }
        }

        public static bool operator <=([NotNull]ByteArray x, [NotNull]Bytes y) {
            lock (x) {
                return x._bytes.Compare(y) <= 0;
            }
        }

        [System.Diagnostics.CodeAnalysis.NotNull]
        public object? this[int index] {
            get {
                lock (this) {
                    return ScriptingRuntimeHelpers.Int32ToObject(_bytes[PythonOps.FixIndex(index, _bytes.Count)]);
                }
            }
            set {
                lock (this) {
                    _bytes[PythonOps.FixIndex(index, _bytes.Count)] = ByteOps.GetByte(value);
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.NotNull]
        public object? this[BigInteger index] {
            get {
                if (index.AsInt32(out int iVal)) {
                    return this[iVal];
                }

                throw PythonOps.IndexError("cannot fit long in index");
            }
            set {
                if (index.AsInt32(out int iVal)) {
                    this[iVal] = value;
                    return;
                }

                throw PythonOps.IndexError("cannot fit long in index");
            }
        }

        [System.Diagnostics.CodeAnalysis.NotNull]
        public object? this[[NotNull]Slice slice] {
            get {
                lock (this) {
                    var res = _bytes.Slice(slice);
                    return res == null ? new ByteArray() : new ByteArray(res);
                }
            }
            set {
                // get a list of the bytes we're going to assign into the slice.  We accept:
                //      integers, longs, etc... - fill in an array of 0 bytes
                //      list of bytes, indexables, etc...

                IList<byte> list = ByteOps.GetBytes(value, useHint: false);

                lock (this) {
                    slice.indices(_bytes.Count, out int start, out int stop, out int step);

                    // try to assign back to self: make a copy first
                    if (ReferenceEquals(this, list)) {
                        list = CopyThis();
                    } else if (list.Count == 0) {
                        DeleteItem(slice);
                        return;
                    }

                    if (step != 1) {
                        int count = PythonOps.GetSliceCount(start, stop, step);

                        // we don't use slice.Assign* helpers here because bytearray has different assignment semantics.

                        if (list.Count != count) {
                            throw PythonOps.ValueError("attempt to assign bytes of size {0} to extended slice of size {1}", list.Count, count);
                        }

                        for (int i = 0, index = start; i < list.Count; i++, index += step) {
                            _bytes[index] = list[i];
                        }
                    } else {
                        if (stop < start) {
                            stop = start;
                        }
                        SliceNoStep(start, stop, list);
                    }
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.NotNull]
        public object? this[object? index] {
            get {
                return this[Converter.ConvertToIndex(index)];
            }
            set {
                this[Converter.ConvertToIndex(index)] = value;
            }
        }

        [SpecialName]
        public void DeleteItem(int index) {
            lock (this) {
                _bytes.RemoveAt(PythonOps.FixIndex(index, _bytes.Count));
            }
        }

        [SpecialName]
        public void DeleteItem([NotNull]Slice slice) {
            lock (this) {
                _bytes.RemoveSlice(slice);
            }
        }

        [SpecialName]
        public void DeleteItem(object? slice) {
            throw PythonOps.TypeError("bytearray indices must be integers or slices, not {0}", PythonOps.GetPythonTypeName(slice));
        }

        #endregion

        #region Implementation Details

        private static ByteArray JoinOne(object? curVal) {
            if (curVal is IList<byte> bytes) {
                return new ByteArray(new List<byte>(bytes));
            }
            if (curVal is IBufferProtocol bp) {
                using (IPythonBuffer buf = bp.GetBuffer()) {
                    return new ByteArray(new ArrayData<byte>(buf.AsReadOnlySpan()));
                }
            }
            throw PythonOps.TypeError("can only join an iterable of bytes");
        }

        private ByteArray CopyThis() {
            lock (this) {
                var res = new ByteArray();
                res._bytes.AddRange(_bytes);
                return res;
            }
        }

        private void SliceNoStep(int start, int stop, IList<byte> other) {
            // replace between start & stop w/ values
            lock (this) {
                int count = stop - start;
                if (count == 0 && other.Count == 0) return;

                _bytes.InsertRange(start, count, other);
            }
        }

        #endregion

        #region IList<byte> Members

        [PythonHidden]
        public int IndexOf(byte item) {
            lock (this) {
                return _bytes.IndexOf(item);
            }
        }

        [PythonHidden]
        public void Insert(int index, byte item) {
            _bytes.Insert(index, item);
        }

        [PythonHidden]
        public void RemoveAt(int index) {
            _bytes.RemoveAt(index);
        }

        byte IList<byte>.this[int index] {
            get {
                return _bytes[index];
            }
            set {
                _bytes[index] = value;
            }
        }

        #endregion

        #region IReadOnlyList<byte> Members

        byte IReadOnlyList<byte>.this[int index] => _bytes[index];

        #endregion

        #region ICollection<byte> Members

        [PythonHidden]
        public void Add(byte item) {
            lock (this) {
                _bytes.Add(item);
            }
        }

        [PythonHidden]
        public void Clear() {
            lock (this) {
                _bytes.Clear();
            }
        }

        [PythonHidden]
        public bool Contains(byte item) {
            lock (this) {
                return _bytes.Contains(item);
            }
        }

        [PythonHidden]
        public void CopyTo(byte[] array, int arrayIndex) {
            lock (this) {
                _bytes.CopyTo(array, arrayIndex);
            }
        }

        public int Count {
            [PythonHidden]
            get {
                lock (this) {
                    return _bytes.Count;
                }
            }
        }

        public bool IsReadOnly {
            [PythonHidden]
            get { return false; }
        }

        [PythonHidden]
        public bool Remove(byte item) {
            lock (this) {
                return _bytes.Remove(item);
            }
        }

        #endregion

        #region IEnumerable<byte> Members

        [PythonHidden]
        public IEnumerator<byte> GetEnumerator() {
            return _bytes.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            foreach (var _byte in _bytes) {
                yield return (int)_byte;
            }
        }

        #endregion

        #region Equality Members

        public const object __hash__ = null;

        public bool __eq__(CodeContext context, [NotNull]ByteArray value) => Equals(value);

        public bool __eq__(CodeContext context, [NotNull]MemoryView value) => Equals(value.tobytes());

        public bool __eq__(CodeContext context, [NotNull]IBufferProtocol value) {
            using (IPythonBuffer buf = value.GetBuffer()) {
                return Equals(buf.AsReadOnlySpan());
            }
        }

        [return: MaybeNotImplemented]
        public NotImplementedType __eq__(CodeContext context, object? value) => NotImplementedType.Value;

        public bool __ne__(CodeContext context, [NotNull]ByteArray value) => !__eq__(context, value);

        public bool __ne__(CodeContext context, [NotNull]MemoryView value) => !__eq__(context, value);

        public bool __ne__(CodeContext context, [NotNull]IBufferProtocol value) => !__eq__(context, value);

        [return: MaybeNotImplemented]
        public NotImplementedType __ne__(CodeContext context, object? value) => NotImplementedType.Value;

        private bool Equals(ByteArray other) {
            if (Count != other.Count) {
                return false;
            } else if (Count == 0) {
                // 2 empty ByteArrays are equal
                return true;
            }

            using (new OrderedLocker(this, other)) {
                for (int i = 0; i < Count; i++) {
                    if (_bytes[i] != other._bytes[i]) {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool Equals(IList<byte> other) {
            if (Count != other.Count) {
                return false;
            } else if (Count == 0) {
                // 2 empty ByteArrays are equal
                return true;
            }

            lock (this) {
                for (int i = 0; i < Count; i++) {
                    if (_bytes[i] != other[i]) {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool Equals(ReadOnlySpan<byte> other) {
            if (Count != other.Length) {
                return false;
            } else if (Count == 0) {
                // 2 empty ByteArrays are equal
                return true;
            }

            lock (this) {
                for (int i = 0; i < Count; i++) {
                    if (_bytes[i] != other[i]) {
                        return false;
                    }
                }
            }

            return true;
        }

        #endregion

        #region IBufferProtocol Members

        IPythonBuffer IBufferProtocol.GetBuffer(BufferFlags flags) {
            return _bytes.GetBuffer(this, "B", flags);
        }

        #endregion
    }
}
