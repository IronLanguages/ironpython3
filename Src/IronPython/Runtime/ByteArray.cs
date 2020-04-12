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
        private ArrayData<byte> _bytes;

        public ByteArray() {
            _bytes = new ArrayData<byte>(0);
        }

        private ByteArray(ArrayData<byte> bytes) {
            _bytes = bytes;
        }

        internal ByteArray(IEnumerable<byte> bytes) {
            _bytes = new ArrayData<byte>(bytes);
        }

        public void __init__() {
            _bytes = new ArrayData<byte>();
        }

        public void __init__(int source) {
            _bytes = new ArrayData<byte>(source);
            for (int i = 0; i < source; i++) {
                _bytes.Add(0);
            }
        }

        public void __init__(BigInteger source) {
            __init__((int)source);
        }

        public void __init__([NotNull]IList<byte> source) {
            _bytes = new ArrayData<byte>(source);
        }

        public void __init__([BytesLike, NotNull]ReadOnlyMemory<byte> source) {
            _bytes = new ArrayData<byte>(source);
        }

        public void __init__(object? source) {
            __init__(GetBytes(source));
        }

        public void __init__([NotNull]string @string) {
            throw PythonOps.TypeError("string argument without an encoding");
        }

        public void __init__(CodeContext context, [NotNull]string source, [NotNull]string encoding, [NotNull]string errors = "strict") {
            _bytes = new ArrayData<byte>(StringOps.encode(context, source, encoding, errors));
        }

        internal static ByteArray Make(List<byte> bytes) {
            return new ByteArray(bytes);
        }

        internal ArrayData<byte> UnsafeByteList {
            [PythonHidden]
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
                _bytes.Add(GetByte(item));
            }
        }

        public void extend([NotNull]IEnumerable<byte> seq) {
            using (new OrderedLocker(this, seq)) {
                // use the original count for if we're extending this w/ this
                _bytes.AddRange(seq);
            }
        }

        public void extend(object? seq) {
            // We don't make use of the length hint when extending the byte array.
            // However, in order to match CPython behavior with invalid length hints we
            // we need to go through the motions and get the length hint and attempt
            // to convert it to an int.
            PythonOps.TryInvokeLengthHint(DefaultContext.Default, seq, out int len);

            extend(GetBytes(seq));
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
                RemoveByte(GetByte(value));
            }
        }

        public void reverse() {
            lock (this) {
                var reversed = new ArrayData<byte>();
                for (int i = _bytes.Count - 1; i >= 0; i--) {
                    reversed.Add(_bytes[i]);
                }
                _bytes = reversed;
            }
        }

        [SpecialName]
        public ByteArray InPlaceAdd([NotNull]ByteArray other) {
            using (new OrderedLocker(this, other)) {
                _bytes.AddRange(other._bytes);
                return this;
            }
        }

        [SpecialName]
        public ByteArray InPlaceAdd([NotNull]Bytes other) {
            lock (this) {
                _bytes.AddRange(other);
                return this;
            }
        }

        [SpecialName]
        public ByteArray InPlaceAdd([NotNull]MemoryView other) {
            lock (this) {
                _bytes.AddRange(other.tobytes());
                return this;
            }
        }

        [SpecialName]
        public ByteArray InPlaceMultiply(int len) {
            lock (this) {
                _bytes = (this * len)._bytes;
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

        public int count([BytesLike, NotNull]IList<byte> sub) => count(sub, null, null);

        public int count([BytesLike, NotNull]IList<byte> sub, int? start) => count(sub, start, null);

        public int count([BytesLike, NotNull]IList<byte> sub, int? start, int? end) {
            lock (this) {
                return _bytes.CountOf(sub, start ?? 0, end ?? _bytes.Count);
            }
        }

        public int count(int @byte) => count(@byte, null, null);

        public int count(int @byte, int? start) => count(@byte, start, null);

        public int count(int @byte, int? start, int? end) => count(Bytes.FromByte(@byte.ToByteChecked()), start, end);

        public string decode(CodeContext context, [NotNull]string encoding = "utf-8", [NotNull]string errors = "strict") {
            lock (this) {
                return StringOps.RawDecode(context, new MemoryView(this, 0, null, 1, "B", PythonTuple.MakeTuple(_bytes.Count), readonlyView: true), encoding, errors);
            }
        }

        public string decode(CodeContext context, [NotNull]Encoding encoding, [NotNull]string errors = "strict") {
            lock (this) {
                return StringOps.DoDecode(context, ((IBufferProtocol)this).ToMemory(), errors, StringOps.GetEncodingName(encoding, normalize: false), encoding);
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

            return new PythonTuple(obj);
        }

        public ByteArray replace([BytesLike, NotNull]IList<byte> old, [BytesLike, NotNull]IList<byte> @new, int count = -1) {
            if (count == 0) {
                return CopyThis();
            }

            return new ByteArray(_bytes.Replace(old, @new, count));
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
                return new PythonTuple(obj);
            }
        }

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

        public PythonList split([BytesLike]IList<byte>? sep = null, int maxsplit = -1) {
            lock (this) {
                return _bytes.Split(sep, maxsplit, x => new ByteArray(x));
            }
        }

        public PythonList splitlines() {
            return splitlines(false);
        }

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

        public ByteArray translate([BytesLike]IList<byte>? table) {
            lock (this) {
                if (table != null) {
                    if (table.Count != 256) {
                        throw PythonOps.ValueError("translation table must be 256 characters long");
                    } else if (Count == 0) {
                        return CopyThis();
                    }
                }

                return new ByteArray(_bytes.Translate(table, null));
            }
        }


        public ByteArray translate([BytesLike]IList<byte>? table, [BytesLike, NotNull]IList<byte> delete) {
            lock (this) {
                return new ByteArray(_bytes.Translate(table, delete));
            }
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

            throw PythonOps.TypeError("Type {0} doesn't support the buffer API", PythonTypeOps.GetName(value));
        }

        public PythonTuple __reduce__(CodeContext context) {
            return PythonTuple.MakeTuple(
                DynamicHelpers.GetPythonType(this),
                PythonTuple.MakeTuple(
                    PythonOps.MakeString(this),
                    "latin-1"
                ),
                GetType() == typeof(ByteArray) ? null : ObjectOps.ReduceProtocol0(context, this)[2]
            );
        }

        private string Repr() {
            lock (this) {
                return "bytearray(" + _bytes.BytesRepr() + ")";
            }
        }

        public virtual string __repr__(CodeContext context) => Repr();

        public override string ToString() => Repr();

        public static ByteArray operator +([NotNull]ByteArray self, [NotNull]ByteArray other) {
            List<byte> bytes;

            lock (self) {
                bytes = new List<byte>(self._bytes);
            }
            lock (other) {
                bytes.AddRange(other._bytes);
            }

            return new ByteArray(bytes);
        }

        public static ByteArray operator +([NotNull]ByteArray self, [NotNull]Bytes other) {
            List<byte> bytes;

            lock (self) {
                bytes = new List<byte>(self._bytes);
            }

            bytes.AddRange(other);

            return new ByteArray(bytes);
        }

        public static ByteArray operator +([NotNull]ByteArray self, [NotNull]MemoryView other) {
            List<byte> bytes;

            lock (self) {
                bytes = new List<byte>(self._bytes);
            }

            bytes.AddRange(other.tobytes());

            return new ByteArray(bytes);
        }

        public static ByteArray operator +([NotNull]ByteArray self, object? other) {
            throw PythonOps.TypeError("can't concat {0} to bytearray", PythonTypeOps.GetName(other));
        }

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
                    _bytes[PythonOps.FixIndex(index, _bytes.Count)] = GetByte(value);
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

                if (!(value is IList<byte> list)) {
                    IEnumerator ie = PythonOps.GetEnumerator(value);
                    list = new List<byte>();
                    while (ie.MoveNext()) {
                        list.Add(GetByte(ie.Current));
                    }
                }

                lock (this) {
                    if (slice.step != null) {
                        // try to assign back to self: make a copy first
                        if (this == list) {
                            value = CopyThis();
                        } else if (list.Count == 0) {
                            DeleteItem(slice);
                            return;
                        }

                        IList<byte> castedVal = GetBytes(value);

                        int start, stop, step, count;
                        slice.GetIndicesAndCount(_bytes.Count, out start, out stop, out step, out count);

                        // we don't use slice.Assign* helpers here because bytearray has different assignment semantics.

                        if (list.Count < count) {
                            throw PythonOps.ValueError("too few items in the enumerator. need {0} have {1}", count, castedVal.Count);
                        }

                        for (int i = 0, index = start; i < castedVal.Count; i++, index += step) {
                            if (i >= count) {
                                if (index == _bytes.Count) {
                                    _bytes.Add(castedVal[i]);
                                } else {
                                    _bytes.Insert(index, castedVal[i]);
                                }
                            } else {
                                _bytes[index] = castedVal[i];
                            }
                        }
                    } else {
                        int start, stop, step;
                        slice.indices(_bytes.Count, out start, out stop, out step);

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
            _bytes.RemoveAt(PythonOps.FixIndex(index, _bytes.Count));
        }

        [SpecialName]
        public void DeleteItem([NotNull]Slice slice) {
            lock (this) {
                int start, stop, step;
                // slice is sealed, indices can't be user code...
                slice.indices(_bytes.Count, out start, out stop, out step);

                if (step > 0 && (start >= stop)) return;
                if (step < 0 && (start <= stop)) return;

                if (step == 1) {
                    int i = start;
                    for (int j = stop; j < _bytes.Count; j++, i++) {
                        _bytes[i] = _bytes[j];
                    }
                    _bytes.RemoveRange(i, stop - start);
                    return;
                } else if (step == -1) {
                    int i = stop + 1;
                    for (int j = start + 1; j < _bytes.Count; j++, i++) {
                        _bytes[i] = _bytes[j];
                    }
                    _bytes.RemoveRange(i, start - stop);
                    return;
                } else if (step < 0) {
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
                        _bytes[curr++] = _bytes[move];
                    } else
                        skip += step;
                    move++;
                }
                while (stop < _bytes.Count) {
                    _bytes[curr++] = _bytes[stop++];
                }
                _bytes.RemoveRange(curr, _bytes.Count - curr);
            }
        }

        [SpecialName]
        public void DeleteItem(object? slice) {
            throw PythonOps.TypeError("bytearray indices must be integers or slices, not {0}", PythonTypeOps.GetName(slice));
        }

        #endregion

        #region Implementation Details

        private static ByteArray JoinOne(object? curVal) {
            if (curVal is IList<byte> bytes) {
                return new ByteArray(new List<byte>(bytes));
            }
            throw PythonOps.TypeError("can only join an iterable of bytes");
        }

        private ByteArray CopyThis() {
            return new ByteArray(new List<byte>(_bytes));
        }

        private void SliceNoStep(int start, int stop, IList<byte> other) {
            lock (this) {
                if (start > stop) {
                    int newSize = Count + other.Count;

                    var newData = new ArrayData<byte>(newSize);
                    int reading = 0;
                    for (reading = 0; reading < start; reading++) {
                        newData.Add(_bytes[reading]);
                    }

                    for (int i = 0; i < other.Count; i++) {
                        newData.Add(other[i]);
                    }

                    for (; reading < Count; reading++) {
                        newData.Add(_bytes[reading]);
                    }

                    _bytes = newData;
                } else if ((stop - start) == other.Count) {
                    // we are simply replacing values, this is fast...
                    for (int i = 0; i < other.Count; i++) {
                        _bytes[i + start] = other[i];
                    }
                } else {
                    // we are resizing the array (either bigger or smaller), we 
                    // will copy the data array and replace it all at once.
                    int newSize = Count - (stop - start) + other.Count;

                    var newData = new ArrayData<byte>(newSize);
                    for (int i = 0; i < start; i++) {
                        newData.Add(_bytes[i]);
                    }

                    for (int i = 0; i < other.Count; i++) {
                        newData.Add(other[i]);
                    }

                    for (int i = stop; i < Count; i++) {
                        newData.Add(_bytes[i]);
                    }

                    _bytes = newData;
                }
            }
        }

        private static byte GetByte(object? value) {
            if (Converter.TryConvertToIndex(value, out object index)) {
                switch (index) {
                    case int i: return i.ToByteChecked();
                    case BigInteger bi: return bi.ToByteChecked();
                    default: throw new InvalidOperationException(); // unreachable
                }
            }
            throw PythonOps.TypeError("an integer is required");
        }

        internal static IList<byte> GetBytes(object? value) {
            switch (value) {
                case IList<byte> lob when !(lob is ListGenericWrapper<byte>):
                    return lob;
                case IBufferProtocol buffer:
                    return buffer.ToBytes(0, null);
                case ReadOnlyMemory<byte> rom:
                    return rom.ToArray();
                case Memory<byte> mem:
                    return mem.ToArray();
                default:
                    List<byte> ret = new List<byte>();
                    IEnumerator ie = PythonOps.GetEnumerator(value);
                    while (ie.MoveNext()) {
                        ret.Add(GetByte(ie.Current));
                    }
                    return ret;
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

        public IEnumerator __iter__() {
            return PythonOps.BytesEnumerator(this).Key;
        }

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

        public bool __eq__(CodeContext context, [NotNull]IBufferProtocol value) => Equals(value.ToBytes(0, null));

        [return: MaybeNotImplemented]
        public object __eq__(CodeContext context, object? value) => NotImplementedType.Value;

        public bool __ne__(CodeContext context, [NotNull]ByteArray value) => !__eq__(context, value);

        public bool __ne__(CodeContext context, [NotNull]MemoryView value) => !__eq__(context, value);

        public bool __ne__(CodeContext context, [NotNull]IBufferProtocol value) => !__eq__(context, value);

        [return: MaybeNotImplemented]
        public object __ne__(CodeContext context, object? value) => NotImplementedType.Value;

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

        #endregion

        #region IBufferProtocol Members

        object IBufferProtocol.GetItem(int index) {
            lock (this) {
                return (int)_bytes[PythonOps.FixIndex(index, _bytes.Count)];
            }
        }

        void IBufferProtocol.SetItem(int index, object value) {
            this[index] = value;
        }

        void IBufferProtocol.SetSlice(Slice index, object value) {
            this[index] = value;
        }

        int IBufferProtocol.ItemCount {
            get {
                return _bytes.Count;
            }
        }

        string IBufferProtocol.Format {
            get { return "B"; }
        }

        BigInteger IBufferProtocol.ItemSize {
            get { return 1; }
        }

        BigInteger IBufferProtocol.NumberDimensions {
            get { return 1; }
        }

        bool IBufferProtocol.ReadOnly {
            get { return false; }
        }

        IList<BigInteger> IBufferProtocol.GetShape(int start, int? end) {
            if (end != null) {
                return new[] { (BigInteger)end - start };
            }
            return new[] { (BigInteger)_bytes.Count - start };
        }

        PythonTuple IBufferProtocol.Strides => PythonTuple.MakeTuple(1);

        PythonTuple? IBufferProtocol.SubOffsets => null;

        Bytes IBufferProtocol.ToBytes(int start, int? end) {
            if (start == 0 && end == null) {
                return new Bytes(this);
            }

            return new Bytes((ByteArray)this[new Slice(start, end)]);
        }

        PythonList IBufferProtocol.ToList(int start, int? end) {
            var res = _bytes.Slice(new Slice(start, end));
            return res == null ? new PythonList() : new PythonList(res);
        }

        ReadOnlyMemory<byte> IBufferProtocol.ToMemory() {
            return _bytes.Data.AsMemory(0, _bytes.Count);
        }

        #endregion
    }
}
