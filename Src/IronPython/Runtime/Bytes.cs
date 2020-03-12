// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime {
    [PythonType("bytes"), Serializable]
    public class Bytes : IList<byte>, IEquatable<Bytes>, ICodeFormattable, IExpressionSerializable, IBufferProtocol {
        private readonly byte[] _bytes;
        internal static readonly Bytes Empty = new Bytes();

        public Bytes() {
            _bytes = new byte[0];
        }

        public Bytes(object? source) : this(ByteArray.GetBytes(source)) { }

        public Bytes([NotNull]IEnumerable<object?> source) {
            _bytes = source.Select(b => ((int)PythonOps.Index(b)).ToByteChecked()).ToArray();
        }

        public Bytes([BytesConversion, NotNull]IList<byte> bytes) {
            _bytes = bytes.ToArray();
        }

        public Bytes([NotNull]PythonList bytes) {
            _bytes = ByteOps.GetBytes(bytes).ToArray();
        }

        public Bytes(int size) {
            _bytes = new byte[size];
        }

        private Bytes(byte[] bytes) {
            _bytes = bytes;
        }

        public Bytes([NotNull]string @string) {
            throw PythonOps.TypeError("string argument without an encoding");
        }

        public Bytes(CodeContext context, [NotNull]string unicode, [NotNull]string encoding) {
            _bytes = StringOps.encode(context, unicode, encoding, "strict").UnsafeByteArray;
        }

        private static readonly Bytes[] oneByteBytes = Enumerable.Range(0, 256).Select(i => new Bytes(new byte[] { (byte)i })).ToArray();

        internal static Bytes FromByte(byte b) {
            return oneByteBytes[b];
        }

        internal static Bytes Make(byte[] bytes) {
            return new Bytes(bytes);
        }

        internal byte[] UnsafeByteArray {
            [PythonHidden]
            get => _bytes;
        }

        #region Public Python API surface

        public Bytes capitalize() {
            if (Count == 0) {
                return this;
            }

            return new Bytes(_bytes.Capitalize());
        }

        public Bytes center(int width) => center(width, (byte)' ');

        public Bytes center(int width, [BytesConversion, NotNull]IList<byte> fillchar)
            => center(width, fillchar.ToByte("center", 2));

        private Bytes center(int width, byte fillchar) {
            var res = _bytes.TryCenter(width, fillchar);
            return res == null ? this : new Bytes(res);
        }

        public int count([BytesConversion, NotNull]IList<byte> sub) => count(sub, null, null);

        public int count([BytesConversion, NotNull]IList<byte> sub, int? start) => count(sub, start, null);

        public int count([BytesConversion, NotNull]IList<byte> sub, int? start, int? end)
            => _bytes.CountOf(sub, start ?? 0, end ?? Count);

        public int count(int @byte) => count(@byte, null, null);

        public int count(int @byte, int? start) => count(@byte, start, null);

        public int count(int @byte, int? start, int? end)
            => _bytes.CountOf(new[] { @byte.ToByteChecked() }, start ?? 0, end ?? Count);

        public string decode(CodeContext context, [NotNull]string encoding = "utf-8", [NotNull]string errors = "strict") {
            return StringOps.RawDecode(context, this, encoding, errors);
        }

        public string decode(CodeContext context, [NotNull]Encoding encoding, [NotNull]string errors = "strict") {
            return StringOps.DoDecode(context, _bytes, errors, StringOps.GetEncodingName(encoding, normalize: false), encoding);
        }

        public bool endswith([BytesConversion, NotNull]IList<byte> suffix) {
            return _bytes.EndsWith(suffix);
        }

        public bool endswith([BytesConversion, NotNull]IList<byte> suffix, int start) {
            return _bytes.EndsWith(suffix, start);
        }

        public bool endswith([BytesConversion, NotNull]IList<byte> suffix, int start, int end) {
            return _bytes.EndsWith(suffix, start, end);
        }

        public bool endswith([NotNull]PythonTuple suffix) {
            return _bytes.EndsWith(suffix);
        }

        public bool endswith([NotNull]PythonTuple suffix, int start) {
            return _bytes.EndsWith(suffix, start);
        }

        public bool endswith([NotNull]PythonTuple suffix, int start, int end) {
            return _bytes.EndsWith(suffix, start, end);
        }

        public Bytes expandtabs() {
            return expandtabs(8);
        }

        public Bytes expandtabs(int tabsize) {
            return new Bytes(_bytes.ExpandTabs(tabsize));
        }

        public int find([BytesConversion, NotNull]IList<byte> sub) => find(sub, null, null);

        public int find([BytesConversion, NotNull]IList<byte> sub, int? start) => find(sub, start, null);

        public int find([BytesConversion, NotNull]IList<byte> sub, int? start, int? end)
            => _bytes.Find(sub, start, end);

        public int find(int @byte) => find(@byte, null, null);

        public int find(int @byte, int? start) => find(@byte, start, null);

        public int find(int @byte, int? start, int? end)
            => _bytes.IndexOfByte(@byte.ToByteChecked(), start ?? 0, end ?? Count);

        public static Bytes fromhex([NotNull]string @string) {
            return new Bytes(IListOfByteOps.FromHex(@string));
        }

        public int index([BytesConversion, NotNull]IList<byte> sub) => index(sub, null, null);

        public int index([BytesConversion, NotNull]IList<byte> sub, int? start) => index(sub, start, null);

        public int index([BytesConversion, NotNull]IList<byte> sub, int? start, int? end) {
            int res = find(sub, start, end);
            if (res == -1) {
                throw PythonOps.ValueError("subsection not found");
            }

            return res;
        }

        public int index(int @byte) => index(@byte, null, null);

        public int index(int @byte, int? start) => index(@byte, start, null);

        public int index(int @byte, int? start, int? end) {
            int res = find(@byte.ToByteChecked(), start, end);
            if (res == -1) {
                throw PythonOps.ValueError("subsection not found");
            }

            return res;
        }

        public bool isalnum() => _bytes.IsAlphaNumeric();

        public bool isalpha() => _bytes.IsLetter();

        public bool isdigit() => _bytes.IsDigit();

        public bool islower() => _bytes.IsLower();

        public bool isspace() => _bytes.IsWhiteSpace();

        /// <summary>
        /// return true if self is a titlecased string and there is at least one
        /// character in self; also, uppercase characters may only follow uncased
        /// characters (e.g. whitespace) and lowercase characters only cased ones. 
        /// return false otherwise.
        /// </summary>
        public bool istitle() => _bytes.IsTitle();

        public bool isupper() => _bytes.IsUpper();

        /// <summary>
        /// Return a string which is the concatenation of the strings 
        /// in the sequence seq. The separator between elements is the 
        /// string providing this method
        /// </summary>
        public Bytes join(object? sequence) {
            IEnumerator seq = PythonOps.GetEnumerator(sequence);
            if (!seq.MoveNext()) {
                return Empty;
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

            return new Bytes(ret);
        }

        public Bytes join([NotNull]PythonList sequence) {
            if (sequence.__len__() == 0) {
                return new Bytes();
            } else if (sequence.__len__() == 1) {
                return JoinOne(sequence[0]);
            }

            List<byte> ret = new List<byte>();
            ByteOps.AppendJoin(sequence._data[0], 0, ret);
            for (int i = 1; i < sequence._size; i++) {
                ret.AddRange(this);
                ByteOps.AppendJoin(sequence._data[i], i, ret);
            }

            return new Bytes(ret);
        }

        public Bytes ljust(int width) {
            return ljust(width, (byte)' ');
        }

        public Bytes ljust(int width, [BytesConversion, NotNull]IList<byte> fillchar) {
            return ljust(width, fillchar.ToByte("ljust", 2));
        }

        private Bytes ljust(int width, byte fillchar) {
            int spaces = width - Count;
            if (spaces <= 0) {
                return this;
            }

            List<byte> ret = new List<byte>(width);
            ret.AddRange(_bytes);
            for (int i = 0; i < spaces; i++) {
                ret.Add(fillchar);
            }
            return new Bytes(ret);
        }

        public Bytes lower() {
            return new Bytes(_bytes.ToLower());
        }

        public Bytes lstrip() {
            var res = _bytes.LeftStrip();
            return res == null ? this : new Bytes(res);
        }

        public Bytes lstrip([BytesConversion]IList<byte>? chars) {
            if (chars == null) return lstrip();
            var res = _bytes.LeftStrip(chars);
            return res == null ? this : new Bytes(res);
        }

        public static Bytes maketrans([BytesConversion, NotNull]IList<byte> from, [BytesConversion, NotNull]IList<byte> to) {
            if (from.Count != to.Count) throw PythonOps.ValueError("maketrans arguments must have same length");

            var bytes = new byte[256];
            for (var i = 0; i < 256; i++) {
                bytes[i] = (byte)i;
            }
            for (var i = 0; i < from.Count; i++) {
                bytes[from[i]] = to[i];
            }
            return Make(bytes);
        }

        public PythonTuple partition([BytesConversion, NotNull]IList<byte> sep) {
            if (sep.Count == 0) {
                throw PythonOps.ValueError("empty separator");
            }

            object[] obj = new object[3] { Empty, Empty, Empty };

            if (Count != 0) {
                int index = find(sep);
                if (index == -1) {
                    obj[0] = this;
                } else {
                    obj[0] = new Bytes(_bytes.Substring(0, index));
                    obj[1] = sep;
                    obj[2] = new Bytes(_bytes.Substring(index + sep.Count, Count - index - sep.Count));
                }
            }

            return new PythonTuple(obj);
        }

        public Bytes replace([BytesConversion, NotNull]IList<byte> old, [BytesConversion, NotNull]IList<byte> @new, int count = -1) {
            if (count == 0) {
                return this;
            }

            return new Bytes(_bytes.Replace(old, @new, count));
        }

        public int rfind([BytesConversion, NotNull]IList<byte> sub) => rfind(sub, null, null);

        public int rfind([BytesConversion, NotNull]IList<byte> sub, int? start) => rfind(sub, start, null);

        public int rfind([BytesConversion, NotNull]IList<byte> sub, int? start, int? end)
            => _bytes.ReverseFind(sub, start, end);

        public int rfind(int @byte) => rfind(@byte, null, null);

        public int rfind(int @byte, int? start) => rfind(@byte, start, null);

        public int rfind(int @byte, int? start, int? end)
            => rfind(new[] { @byte.ToByteChecked() }, start, end);

        public int rindex([BytesConversion, NotNull]IList<byte> sub) => rindex(sub, null, null);

        public int rindex([BytesConversion, NotNull]IList<byte> sub, int? start) => rindex(sub, start, null);

        public int rindex([BytesConversion, NotNull]IList<byte> sub, int? start, int? end) {
            int ret = rfind(sub, start, end);
            if (ret == -1) {
                throw PythonOps.ValueError("subsection not found");
            }

            return ret;
        }

        public int rindex(int @byte) => rindex(@byte, null, null);

        public int rindex(int @byte, int? start) => rindex(@byte, start, null);

        public int rindex(int @byte, int? start, int? end) => rindex(new[] { @byte.ToByteChecked() }, start, end);

        public Bytes rjust(int width) {
            return rjust(width, (byte)' ');
        }

        public Bytes rjust(int width, [BytesConversion, NotNull]IList<byte> fillchar) {
            return rjust(width, fillchar.ToByte("rjust", 2));
        }

        private Bytes rjust(int width, byte fillchar) {
            int spaces = width - Count;
            if (spaces <= 0) {
                return this;
            }

            List<byte> ret = new List<byte>(width);
            for (int i = 0; i < spaces; i++) {
                ret.Add(fillchar);
            }
            ret.AddRange(_bytes);
            return new Bytes(ret);
        }

        public PythonTuple rpartition([BytesConversion, NotNull]IList<byte> sep) {
            if (sep.Count == 0) {
                throw PythonOps.ValueError("empty separator");
            }

            object[] obj = new object[3] { Empty, Empty, Empty };
            if (Count != 0) {
                int index = rfind(sep);
                if (index == -1) {
                    obj[2] = this;
                } else {
                    obj[0] = new Bytes(_bytes.Substring(0, index));
                    obj[1] = sep;
                    obj[2] = new Bytes(_bytes.Substring(index + sep.Count, Count - index - sep.Count));
                }
            }
            return new PythonTuple(obj);
        }

        public PythonList rsplit() {
            return _bytes.SplitInternal(null, -1, x => new Bytes(x));
        }

        public PythonList rsplit([BytesConversion]IList<byte>? sep) {
            return rsplit(sep, -1);
        }

        public PythonList rsplit([BytesConversion]IList<byte>? sep, int maxsplit) {
            return _bytes.RightSplit(sep, maxsplit, x => new Bytes(new List<byte>(x)));
        }

        public Bytes rstrip() {
            var res = _bytes.RightStrip();
            return res == null ? this : new Bytes(res);
        }

        public Bytes rstrip([BytesConversion]IList<byte>? chars) {
            if (chars == null) return rstrip();
            var res = _bytes.RightStrip(chars);
            return res == null ? this : new Bytes(res);
        }

        public PythonList split() {
            return _bytes.SplitInternal(null, -1, x => new Bytes(x));
        }

        public PythonList split([BytesConversion]IList<byte>? sep) {
            return split(sep, -1);
        }

        public PythonList split([BytesConversion]IList<byte>? sep, int maxsplit) {
            return _bytes.Split(sep, maxsplit, x => new Bytes(x));
        }

        public PythonList splitlines() {
            return splitlines(false);
        }

        public PythonList splitlines(bool keepends) {
            return _bytes.SplitLines(keepends, x => new Bytes(x));
        }

        public bool startswith([BytesConversion, NotNull]IList<byte> prefix) {
            return _bytes.StartsWith(prefix);
        }

        public bool startswith([BytesConversion, NotNull]IList<byte> prefix, int start) {
            int len = Count;
            if (start > len) return false;
            if (start < 0) {
                start += len;
                if (start < 0) start = 0;
            }
            return _bytes.Substring(start).StartsWith(prefix);
        }

        public bool startswith([BytesConversion, NotNull]IList<byte> prefix, int start, int end) {
            return _bytes.StartsWith(prefix, start, end);
        }

        public bool startswith([NotNull]PythonTuple prefix) {
            return _bytes.StartsWith(prefix);
        }

        public bool startswith([NotNull]PythonTuple prefix, int start) {
            return _bytes.StartsWith(prefix, start);
        }

        public bool startswith([NotNull]PythonTuple prefix, int start, int end) {
            return _bytes.StartsWith(prefix, start, end);
        }

        public Bytes strip() {
            var res = _bytes.Strip();
            return res == null ? this : new Bytes(res);
        }

        public Bytes strip([BytesConversion]IList<byte>? chars) {
            if (chars == null) return strip();
            var res = _bytes.Strip(chars);
            return res == null ? this : new Bytes(res);
        }

        public Bytes swapcase() {
            return new Bytes(_bytes.SwapCase());
        }

        public Bytes title() {
            var res = _bytes.Title();
            return res == null ? this : new Bytes(res);
        }

        public Bytes translate([BytesConversion]IList<byte>? table) {
            if (table == null) {
                return this;
            } else if (table.Count != 256) {
                throw PythonOps.ValueError("translation table must be 256 characters long");
            } else if (Count == 0) {
                return this;
            }

            return new Bytes(_bytes.Translate(table, null));
        }

        public Bytes translate([BytesConversion]IList<byte>? table, [BytesConversion, NotNull]IList<byte> delete) {
            if (Count == 0) {
                return this;
            }

            return new Bytes(_bytes.Translate(table, delete));
        }

        public Bytes upper() {
            return new Bytes(_bytes.ToUpper());
        }

        public Bytes zfill(int width) {
            int spaces = width - Count;
            if (spaces <= 0) {
                return this;
            }

            return new Bytes(_bytes.ZeroFill(width, spaces));
        }

        public bool __contains__([BytesConversion, NotNull]IList<byte> bytes) {
            return this.IndexOf(bytes, 0) != -1;
        }

        public bool __contains__(CodeContext context, int value) {
            return IndexOf(value.ToByteChecked()) != -1;
        }

        public bool __contains__(CodeContext context, object? value) {
            switch (value) {
                case Extensible<int> ei:
                    return IndexOf(ei.Value.ToByteChecked()) != -1;
                case BigInteger bi:
                    return IndexOf(bi.ToByteChecked()) != -1;
                case Extensible<BigInteger> ebi:
                    return IndexOf(ebi.Value.ToByteChecked()) != -1;
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
                GetType() == typeof(Bytes) ? null : ObjectOps.ReduceProtocol0(context, this)[2]
            );
        }

        public virtual string __repr__(CodeContext context) {
            return _bytes.BytesRepr();
        }

        public override string ToString() {
            return _bytes.BytesRepr();
        }

        public static Bytes operator +([NotNull]Bytes self, [NotNull]Bytes other) {
            var bytes = new List<byte>(self._bytes);
            bytes.AddRange(other._bytes);

            return new Bytes(bytes);
        }

        public static Bytes operator +([NotNull]Bytes self, [NotNull]ByteArray other) {
            var bytes = new List<byte>(self._bytes);
            lock (other) {
                bytes.AddRange(other);
            }

            return new Bytes(bytes);
        }

        public static Bytes operator +([NotNull]Bytes self, object? other) {
            throw PythonOps.TypeError("can't concat {0} to bytes", PythonTypeOps.GetName(other));
        }

        private static Bytes MultiplyWorker(Bytes self, int count) {
            if (count == 1) {
                return self;
            }

            return new Bytes(self._bytes.Multiply(count));
        }

        public static Bytes operator *([NotNull]Bytes self, int count) => MultiplyWorker(self, count);

        public static object operator *([NotNull]Bytes self, [NotNull]Index count)
            => PythonOps.MultiplySequence(MultiplyWorker, self, count, true);


        public static Bytes operator *([NotNull]Bytes self, object? count) {
            if (Converter.TryConvertToIndex(count, out int index)) {
                return self * index;
            }

            throw PythonOps.TypeErrorForUnIndexableObject(count);
        }

        public static Bytes operator *(int count, [NotNull]Bytes self) => MultiplyWorker(self, count);

        public static object operator *([NotNull]Index count, [NotNull]Bytes self)
            => PythonOps.MultiplySequence(MultiplyWorker, self, count, false);

        public static Bytes operator *(object? count, [NotNull]Bytes self) {
            if (Converter.TryConvertToIndex(count, out int index)) {
                return index * self;
            }

            throw PythonOps.TypeErrorForUnIndexableObject(count);
        }

        public static bool operator >([NotNull]Bytes x, [NotNull]Bytes y) {
            return x._bytes.Compare(y._bytes) > 0;
        }

        public static bool operator <([NotNull]Bytes x, [NotNull]Bytes y) {
            return x._bytes.Compare(y._bytes) < 0;
        }

        public static bool operator >=([NotNull]Bytes x, [NotNull]Bytes y) {
            return x._bytes.Compare(y._bytes) >= 0;
        }

        public static bool operator <=([NotNull]Bytes x, [NotNull]Bytes y) {
            return x._bytes.Compare(y._bytes) <= 0;
        }

        public int this[int index] {
            get {
                return (int)_bytes[PythonOps.FixIndex(index, _bytes.Length)];
            }
            [PythonHidden]
            set {
                throw new InvalidOperationException();
            }
        }

        public int this[BigInteger index] {
            get {
                if (index.AsInt32(out int iVal)) {
                    return this[iVal];
                }

                throw PythonOps.IndexError("cannot fit long in index");
            }
        }

        public Bytes this[[NotNull]Slice slice] {
            get {
                var res = _bytes.Slice(slice);
                return res == null ? Empty : new Bytes(res);
            }
        }

        public int this[object? index] {
            get {
                return this[Converter.ConvertToIndex(index)];
            }
        }

        #endregion

        #region Implementation Details

        private static Bytes JoinOne(object? curVal) {
            if (curVal?.GetType() == typeof(Bytes)) {
                return (Bytes)curVal;
            }
            if (curVal is IList<byte> b) {
                return new Bytes(b);
            }
            throw PythonOps.TypeError("can only join an iterable of bytes");
        }

        internal static Bytes Concat(IList<Bytes> list, int length) {
            byte[] res = new byte[length];
            int count = 0;
            for (int i = 0; i < list.Count; i++) {
                Debug.Assert(count + list[i]._bytes.Length <= length);
                Array.Copy(list[i]._bytes, 0, res, count, list[i]._bytes.Length);
                count += list[i]._bytes.Length;
            }

            return new Bytes(res);
        }

        #endregion

        #region IList<byte> Members

        [PythonHidden]
        public int IndexOf(byte item) {
            for (int i = 0; i < _bytes.Length; i++) {
                if (_bytes[i] == item) {
                    return i;
                }
            }
            return -1;
        }

        [PythonHidden]
        public void Insert(int index, byte item) {
            throw new InvalidOperationException();
        }

        [PythonHidden]
        public void RemoveAt(int index) {
            throw new InvalidOperationException();
        }

        byte IList<byte>.this[int index] {
            get {
                return _bytes[index];
            }
            set {
                throw new InvalidOperationException();
            }
        }

        #endregion

        #region ICollection<byte> Members

        [PythonHidden]
        public void Add(byte item) {
            throw new InvalidOperationException();
        }

        [PythonHidden]
        public void Clear() {
            throw new InvalidOperationException();
        }

        [PythonHidden]
        public bool Contains(byte item) {
            return ((IList<byte>)_bytes).Contains(item);
        }

        [PythonHidden]
        public void CopyTo(byte[] array, int arrayIndex) {
            _bytes.CopyTo(array, arrayIndex);
        }

        public int Count {
            [PythonHidden]
            get { return _bytes.Length; }
        }

        public bool IsReadOnly {
            [PythonHidden]
            get { return true; }
        }

        [PythonHidden]
        public bool Remove(byte item) {
            throw new InvalidOperationException();
        }

        #endregion

        #region IEnumerable<byte> Members

        [PythonHidden]
        public IEnumerator<byte> GetEnumerator() {
            return ((IEnumerable<byte>)_bytes).GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            return _bytes.GetEnumerator();
        }

        #endregion

        #region Equality Members

        public bool __eq__(CodeContext context, [NotNull]Bytes value) => Equals(value);

        [return: MaybeNotImplemented]
        public object __eq__(CodeContext context, object? value) => NotImplementedType.Value;

        public bool __ne__(CodeContext context, [NotNull]Bytes value) => !Equals(value);

        [return: MaybeNotImplemented]
        public object __ne__(CodeContext context, object? value) => NotImplementedType.Value;

        [PythonHidden]
        public bool Equals(Bytes other) => other != null && (ReferenceEquals(this, other) || Enumerable.SequenceEqual(_bytes, other._bytes));

        public override bool Equals(object? obj) => obj is Bytes bytes && Equals(bytes);

        public override int GetHashCode() {
            return PythonOps.MakeString(this).GetHashCode();
        }

        #endregion

        #region IExpressionSerializable Members

        Expression IExpressionSerializable.CreateExpression() {
            return Expression.Call(
                typeof(PythonOps).GetMethod(nameof(PythonOps.MakeBytes)),
                Expression.NewArrayInit(
                    typeof(byte),
                    ArrayUtils.ConvertAll(_bytes, (b) => Expression.Constant(b))
                )
            );
        }

        #endregion

        #region IBufferProtocol Members

        object IBufferProtocol.GetItem(int index) {
            byte res = _bytes[PythonOps.FixIndex(index, _bytes.Length)];
            return (int)res;
        }

        void IBufferProtocol.SetItem(int index, object value) {
            throw new InvalidOperationException();
        }

        void IBufferProtocol.SetSlice(Slice index, object value) {
            throw new InvalidOperationException();
        }

        int IBufferProtocol.ItemCount {
            get {
                return _bytes.Length;
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
            get { return true; }
        }

        IList<BigInteger> IBufferProtocol.GetShape(int start, int? end) {
            if (end != null) {
                return new[] { (BigInteger)end - start };
            }
            return new[] { (BigInteger)_bytes.Length - start };
        }

        PythonTuple IBufferProtocol.Strides {
            get { return PythonTuple.MakeTuple(1); }
        }

        PythonTuple? IBufferProtocol.SubOffsets {
            get { return null; }
        }

        Bytes IBufferProtocol.ToBytes(int start, int? end) {
            if (start == 0 && end == null) {
                return this;
            }

            return this[new Slice(start, end)];
        }

        Span<byte> IBufferProtocol.ToSpan(int start, int? end) {
            return _bytes.AsSpan(start, end ?? _bytes.Length);
        }

        PythonList IBufferProtocol.ToList(int start, int? end) {
            var res = _bytes.Slice(new Slice(start, end));
            return res == null ? new PythonList() : new PythonList(res);
        }

        #endregion
    }
}
