﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Text;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;
using NotNullWhenAttribute = System.Diagnostics.CodeAnalysis.NotNullWhenAttribute;

namespace IronPython.Runtime {
    [PythonType("bytes"), Serializable]
    public class Bytes : IList<byte>, IReadOnlyList<byte>, IEquatable<Bytes>, ICodeFormattable, IExpressionSerializable, IBufferProtocol {
        private readonly byte[] _bytes;
        internal static readonly Bytes Empty = new Bytes();

        public Bytes() {
            _bytes = Array.Empty<byte>();
        }

        public Bytes([NotNone] Bytes bytes) {
            _bytes = bytes._bytes;
        }

        public Bytes([NotNone] IEnumerable<byte> bytes) {
            _bytes = bytes.ToArray();
        }

        public Bytes([NotNone] IBufferProtocol source) {
            using IPythonBuffer buffer = source.GetBuffer(BufferFlags.FullRO);
            _bytes = buffer.ToArray();
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext context, [NotNone] PythonType cls) {
            if (cls == TypeCache.Bytes) {
                return Empty;
            } else {
                return cls.CreateInstance(context);
            }
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext context, [NotNone] PythonType cls, [NotNone] IBufferProtocol source) {
            if (cls == TypeCache.Bytes) {
                if (source.GetType() == typeof(Bytes)) {
                    return source;
                } else if (TryInvokeBytesOperator(context, source, out Bytes? res)) {
                    return res;
                } else if (Converter.TryConvertToIndex(source, out int size, throwNonInt: false)) {
                    if (size < 0) throw PythonOps.ValueError("negative count");
                    return new Bytes(new byte[size]);
                } else {
                    return new Bytes(source);
                }
            } else {
                return cls.CreateInstance(context, __new__(context, TypeCache.Bytes, source));
            }
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext context, [NotNone] PythonType cls, object? @object) {
            if (cls == TypeCache.Bytes) {
                if (@object?.GetType() == typeof(Bytes)) {
                    return @object;
                } else if (TryInvokeBytesOperator(context, @object, out Bytes? res)) {
                    return res;
                } else if (Converter.TryConvertToIndex(@object, out int size, throwNonInt: false)) {
                    if (size < 0) throw PythonOps.ValueError("negative count");
                    return new Bytes(new byte[size]);
                } else {
                    return new Bytes(ByteOps.GetBytes(@object, useHint: true, context).ToArray());
                }
            } else {
                return cls.CreateInstance(context, __new__(context, TypeCache.Bytes, @object));
            }
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext context, [NotNone] PythonType cls, int size) {
            if (cls == TypeCache.Bytes) {
                if (size < 0) throw PythonOps.ValueError("negative count");
                return new Bytes(new byte[size]);
            } else {
                return cls.CreateInstance(context, __new__(context, TypeCache.Bytes, size));
            }
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext context, [NotNone] PythonType cls, BigInteger size) {
            if (cls == TypeCache.Bytes) {
                if (size.AsInt32(out int i32)) {
                    return __new__(context, cls, i32);
                } else {
                    throw PythonOps.OverflowError("cannot fit 'int' into an index-sized integer");
                }
            } else {
                return cls.CreateInstance(context, __new__(context, TypeCache.Bytes, size));
            }
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext context, [NotNone] PythonType cls, [NotNone] Extensible<BigInteger> size) {
            if (cls == TypeCache.Bytes) {
                if (TryInvokeBytesOperator(context, size, out Bytes? res)) {
                    return res;
                } else if (size.Value.AsInt32(out int i32)) {
                    return __new__(context, cls, i32);
                } else {
                    throw PythonOps.OverflowError("cannot fit '{0}' into an index-sized integer", PythonOps.GetPythonTypeName(size));
                }
            } else {
                return cls.CreateInstance(context, __new__(context, TypeCache.Bytes, size));
            }
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext context, [NotNone] PythonType cls, [NotNone] ExtensibleString @string) {
            if (cls == TypeCache.Bytes) {
                if (TryInvokeBytesOperator(context, @string, out Bytes? res)) {
                    return res;
                } else {
                    throw PythonOps.TypeError("string argument without an encoding");
                }
            } else {
                return cls.CreateInstance(context, __new__(context, TypeCache.Bytes, @string));
            }
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext context, [NotNone] PythonType cls, [NotNone] string @string) {
            throw PythonOps.TypeError("string argument without an encoding");
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext context, [NotNone] PythonType cls, [NotNone] string @string, [NotNone] string encoding) {
            if (cls == TypeCache.Bytes) {
                return StringOps.encode(context, @string, encoding);
            } else {
                return cls.CreateInstance(context, __new__(context, TypeCache.Bytes, @string, encoding));
            }
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext context, [NotNone] PythonType cls, [NotNone] string @string, [NotNone] string encoding, [NotNone] string errors) {
            if (cls == TypeCache.Bytes) {
                return StringOps.encode(context, @string, encoding, errors);
            } else {
                return cls.CreateInstance(context, __new__(context, TypeCache.Bytes, @string, encoding, errors));
            }
        }

        private Bytes(byte[] bytes) {
            _bytes = bytes;
        }

        private static readonly IReadOnlyList<Bytes> oneByteBytes = Enumerable.Range(0, 256).Select(i => new Bytes(new byte[] { (byte)i })).ToArray();

        internal static Bytes FromByte(byte b)
            => oneByteBytes[b];

        internal static Bytes FromObject(CodeContext context, object? o) {
            if (o == null) {
                throw PythonOps.TypeError("cannot convert 'NoneType' object to bytes");
            } else if (o.GetType() == typeof(Bytes)) {
                return (Bytes)o;
            } else if (TryInvokeBytesOperator(context, o, out Bytes? res)) {
                return res;
            } else if (o is IBufferProtocol bp) {
                return new Bytes(bp);
            } else if (o is string || o is ExtensibleString) {
                throw PythonOps.TypeError("cannot convert unicode object to bytes");
            } else {
                return new Bytes(ByteOps.GetBytes(o, useHint: true, context).ToArray());
            }
        }

        internal static Bytes Make(byte[] bytes)
            => new Bytes(bytes);

        internal byte[] UnsafeByteArray {
            [PythonHidden]
            get => _bytes;
        }

        private Bytes AsBytes()
            => this.GetType() == typeof(Bytes) ? this : new Bytes(_bytes);

        #region Public Python API surface

        public Bytes capitalize() {
            if (Count == 0) {
                return this.AsBytes();
            }

            return new Bytes(_bytes.Capitalize());
        }

        public Bytes center(int width)
            => center(width, (byte)' ');

        public Bytes center(int width, [BytesLike, NotNone] IList<byte> fillchar)
            => center(width, fillchar.ToByte("center", 2));

        private Bytes center(int width, byte fillchar) {
            var res = _bytes.TryCenter(width, fillchar);
            return res == null ? this.AsBytes() : new Bytes(res);
        }

        public int count([BytesLike, NotNone] IList<byte> sub)
            => _bytes.CountOf(sub, 0, _bytes.Length);

        public int count([BytesLike, NotNone] IList<byte> sub, int start)
            => _bytes.CountOf(sub, start, _bytes.Length);

        public int count([BytesLike, NotNone] IList<byte> sub, int start, int end)
            => _bytes.CountOf(sub, start, end);

        public int count([BytesLike, NotNone] IList<byte> sub, object? start)
            => count(sub, start, null);

        public int count([BytesLike, NotNone] IList<byte> sub, object? start, object? end) {
            int istart = start != null ? Converter.ConvertToIndex(start) : 0;
            int iend = end != null ? Converter.ConvertToIndex(end) : _bytes.Length;
            return _bytes.CountOf(sub, istart, iend);
        }

        public int count(BigInteger @byte)
            => _bytes.CountOf(Bytes.FromByte(@byte.ToByteChecked()), 0, _bytes.Length);

        public int count(BigInteger @byte, int start)
            => _bytes.CountOf(Bytes.FromByte(@byte.ToByteChecked()), start, _bytes.Length);

        public int count(BigInteger @byte, int start, int end)
            => _bytes.CountOf(Bytes.FromByte(@byte.ToByteChecked()), start, end);

        public int count(BigInteger @byte, object? start)
            => count(Bytes.FromByte(@byte.ToByteChecked()), start);

        public int count(BigInteger @byte, object? start, object? end)
            => count(Bytes.FromByte(@byte.ToByteChecked()), start, end);

        public string decode(CodeContext context, [NotNone] string encoding = "utf-8", [NotNone] string errors = "strict") {
            using var mv = new MemoryView(this);
            return StringOps.RawDecode(context, mv, encoding, errors);
        }

        public string decode(CodeContext context, [NotNone] Encoding encoding, [NotNone] string errors = "strict") {
            using var buffer = ((IBufferProtocol)this).GetBuffer();
            return StringOps.DoDecode(context, buffer, errors, StringOps.GetEncodingName(encoding, normalize: false), encoding);
        }

        public bool endswith([BytesLike, NotNone] IList<byte> suffix) {
            return _bytes.EndsWith(suffix);
        }

        public bool endswith([BytesLike, NotNone] IList<byte> suffix, int start) {
            return _bytes.EndsWith(suffix, start, _bytes.Length);
        }

        public bool endswith([BytesLike, NotNone] IList<byte> suffix, int start, int end) {
            return _bytes.EndsWith(suffix, start, end);
        }

        public bool endswith([BytesLike, NotNone] IList<byte> suffix, object? start) {
            return endswith(suffix, start, null);
        }

        public bool endswith([BytesLike, NotNone] IList<byte> suffix, object? start, object? end) {
            int istart = start != null ? Converter.ConvertToIndex(start) : 0;
            int iend = end != null ? Converter.ConvertToIndex(end) : _bytes.Length;
            return _bytes.EndsWith(suffix, istart, iend);
        }

        public bool endswith([NotNone] PythonTuple suffix) {
            return _bytes.EndsWith(suffix);
        }

        public bool endswith([NotNone] PythonTuple suffix, int start) {
            return _bytes.EndsWith(suffix, start, _bytes.Length);
        }

        public bool endswith([NotNone] PythonTuple suffix, int start, int end) {
            return _bytes.EndsWith(suffix, start, end);
        }

        public bool endswith([NotNone] PythonTuple suffix, object? start) {
            return endswith(suffix, start, null);
        }

        public bool endswith([NotNone] PythonTuple suffix, object? start, object? end) {
            int istart = start != null ? Converter.ConvertToIndex(start) : 0;
            int iend = end != null ? Converter.ConvertToIndex(end) : _bytes.Length;
            return _bytes.EndsWith(suffix, istart, iend);
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

        public Bytes expandtabs()
            => expandtabs(8);

        public Bytes expandtabs(int tabsize) {
            if (Count == 0) {
                return this.AsBytes();
            }

            return new Bytes(_bytes.ExpandTabs(tabsize));
        }

        public int find([BytesLike, NotNone] IList<byte> sub)
            => _bytes.Find(sub, 0, _bytes.Length);

        public int find([BytesLike, NotNone] IList<byte> sub, int start)
            => _bytes.Find(sub, start, _bytes.Length);

        public int find([BytesLike, NotNone] IList<byte> sub, int start, int end)
            => _bytes.Find(sub, start, end);

        public int find([BytesLike, NotNone] IList<byte> sub, object? start)
            => find(sub, start, null);

        public int find([BytesLike, NotNone] IList<byte> sub, object? start, object? end) {
            int istart = start != null ? Converter.ConvertToIndex(start) : 0;
            int iend = end != null ? Converter.ConvertToIndex(end) : _bytes.Length;
            return _bytes.Find(sub, istart, iend);
        }

        public int find(BigInteger @byte)
            => _bytes.IndexOfByte(@byte.ToByteChecked(), 0, _bytes.Length);

        public int find(BigInteger @byte, int start)
            => _bytes.IndexOfByte(@byte.ToByteChecked(), start, _bytes.Length);

        public int find(BigInteger @byte, int start, int end)
            => _bytes.IndexOfByte(@byte.ToByteChecked(), start, end);

        public int find(BigInteger @byte, object? start)
            => find(@byte, start, null);

        public int find(BigInteger @byte, object? start, object? end) {
            int istart = start != null ? Converter.ConvertToIndex(start) : 0;
            int iend = end != null ? Converter.ConvertToIndex(end) : _bytes.Length;
            return _bytes.IndexOfByte(@byte.ToByteChecked(), istart, iend);
        }

        [ClassMethod]
        public static object fromhex(CodeContext context, [NotNone] PythonType cls, [NotNone] string @string)
            => __new__(context, cls, IListOfByteOps.FromHex(@string));

        public string hex() => ToHex(_bytes.AsSpan()); // new in CPython 3.5

        internal static string ToHex(ReadOnlySpan<byte> bytes) {
            if (bytes.Length == 0) return string.Empty;

            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) {
                builder.Append(ToAscii(b >> 4));
                builder.Append(ToAscii(b & 0xf));
            }
            return builder.ToString();

            static char ToAscii(int b) {
                return (char)(b < 10 ? '0' + b : 'a' + (b - 10));
            }
        }

        public int index([BytesLike, NotNone] IList<byte> sub)
            => index(sub, 0, _bytes.Length);

        public int index([BytesLike, NotNone] IList<byte> sub, int start)
            => index(sub, start, _bytes.Length);

        public int index([BytesLike, NotNone] IList<byte> sub, int start, int end) {
            int res = find(sub, start, end);
            if (res == -1) {
                throw PythonOps.ValueError("subsection not found");
            }

            return res;
        }

        public int index([BytesLike, NotNone] IList<byte> sub, object? start)
            => index(sub, start, null);

        public int index([BytesLike, NotNone] IList<byte> sub, object? start, object? end) {
            int istart = start != null ? Converter.ConvertToIndex(start) : 0;
            int iend = end != null ? Converter.ConvertToIndex(end) : _bytes.Length;
            return index(sub, istart, iend);
        }

        public int index(BigInteger @byte)
            => index(Bytes.FromByte(@byte.ToByteChecked()));

        public int index(BigInteger @byte, int? start)
            => index(Bytes.FromByte(@byte.ToByteChecked()), start);

        public int index(BigInteger @byte, int start, int end)
            => index(Bytes.FromByte(@byte.ToByteChecked()), start, end);

        public int index(BigInteger @byte, object? start)
            => index(Bytes.FromByte(@byte.ToByteChecked()), start, null);

        public int index(BigInteger @byte, object? start, object? end)
            => index(Bytes.FromByte(@byte.ToByteChecked()), start, end);

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

        public Bytes join([NotNone] PythonList sequence) {
            if (sequence.__len__() == 0) {
                return Empty;
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

        public Bytes ljust(int width, [BytesLike, NotNone] IList<byte> fillchar) {
            return ljust(width, fillchar.ToByte("ljust", 2));
        }

        private Bytes ljust(int width, byte fillchar) {
            int spaces = width - Count;
            if (spaces <= 0) {
                return this.AsBytes();
            }

            List<byte> ret = new List<byte>(width);
            ret.AddRange(_bytes);
            for (int i = 0; i < spaces; i++) {
                ret.Add(fillchar);
            }
            return new Bytes(ret);
        }

        public Bytes lower() {
            if (Count == 0) {
                return this.AsBytes();
            }

            return new Bytes(_bytes.ToLower());
        }

        public Bytes lstrip() {
            var res = _bytes.LeftStrip();
            return res == null ? this.AsBytes() : new Bytes(res);
        }

        public Bytes lstrip([BytesLike]IList<byte>? chars) {
            if (chars == null) return lstrip();
            var res = _bytes.LeftStrip(chars);
            return res == null ? this.AsBytes() : new Bytes(res);
        }

        public static Bytes maketrans([BytesLike, NotNone] IList<byte> from, [BytesLike, NotNone] IList<byte> to) {
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

        [return: SequenceTypeInfo(typeof(Bytes))]
        public PythonTuple partition([BytesLike, NotNone] IList<byte> sep) {
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

            return PythonTuple.MakeTuple(obj);
        }

        public Bytes replace([BytesLike, NotNone] IList<byte> old, [BytesLike, NotNone] IList<byte> @new)
            => replace(old, @new, -1);

        public Bytes replace([BytesLike, NotNone] IList<byte> old, [BytesLike, NotNone] IList<byte> @new, int count) {
            if (count == 0) {
                return AsBytes();
            }

            var res = _bytes.Replace(old, @new, ref count);
            if (count == 0) return AsBytes();
            return new Bytes(res);
        }

        public int rfind([BytesLike, NotNone] IList<byte> sub)
            => _bytes.ReverseFind(sub, 0, _bytes.Length);

        public int rfind([BytesLike, NotNone] IList<byte> sub, int start)
            => _bytes.ReverseFind(sub, start, _bytes.Length);

        public int rfind([BytesLike, NotNone] IList<byte> sub, int start, int end)
            => _bytes.ReverseFind(sub, start, end);

        public int rfind([BytesLike, NotNone] IList<byte> sub, object? start)
            => rfind(sub, start, null);

        public int rfind([BytesLike, NotNone] IList<byte> sub, object? start, object? end) {
            int istart = start != null ? Converter.ConvertToIndex(start) : 0;
            int iend = end != null ? Converter.ConvertToIndex(end) : _bytes.Length;
            return _bytes.ReverseFind(sub, istart, iend);
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

        public int rindex([BytesLike, NotNone] IList<byte> sub)
            => rindex(sub, 0, _bytes.Length);

        public int rindex([BytesLike, NotNone] IList<byte> sub, int start)
            => rindex(sub, start, _bytes.Length);

        public int rindex([BytesLike, NotNone] IList<byte> sub, int start, int end) {
            int ret = rfind(sub, start, end);
            if (ret == -1) {
                throw PythonOps.ValueError("subsection not found");
            }

            return ret;
        }

        public int rindex([BytesLike, NotNone] IList<byte> sub, object? start)
            => rindex(sub, start, null);

        public int rindex([BytesLike, NotNone] IList<byte> sub, object? start, object? end) {
            int istart = start != null ? Converter.ConvertToIndex(start) : 0;
            int iend = end != null ? Converter.ConvertToIndex(end) : _bytes.Length;
            return rindex(sub, istart, iend);
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

        public Bytes rjust(int width) {
            return rjust(width, (byte)' ');
        }

        public Bytes rjust(int width, [BytesLike, NotNone] IList<byte> fillchar) {
            return rjust(width, fillchar.ToByte("rjust", 2));
        }

        private Bytes rjust(int width, byte fillchar) {
            int spaces = width - Count;
            if (spaces <= 0) {
                return this.AsBytes();
            }

            List<byte> ret = new List<byte>(width);
            for (int i = 0; i < spaces; i++) {
                ret.Add(fillchar);
            }
            ret.AddRange(_bytes);
            return new Bytes(ret);
        }

        [return: SequenceTypeInfo(typeof(Bytes))]
        public PythonTuple rpartition([BytesLike, NotNone] IList<byte> sep) {
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
            return PythonTuple.MakeTuple(obj);
        }

        [return: SequenceTypeInfo(typeof(Bytes))]
        public PythonList rsplit([BytesLike]IList<byte>? sep = null, int maxsplit = -1) {
            return _bytes.RightSplit(sep, maxsplit, x => new Bytes(new List<byte>(x)));
        }

        public Bytes rstrip() {
            var res = _bytes.RightStrip();
            return res == null ? this.AsBytes() : new Bytes(res);
        }

        public Bytes rstrip([BytesLike]IList<byte>? chars) {
            if (chars == null) return rstrip();
            var res = _bytes.RightStrip(chars);
            return res == null ? this.AsBytes() : new Bytes(res);
        }

        [return: SequenceTypeInfo(typeof(Bytes))]
        public PythonList split([BytesLike]IList<byte>? sep = null, int maxsplit = -1) {
            return _bytes.Split(sep, maxsplit, x => new Bytes(x));
        }

        [return: SequenceTypeInfo(typeof(Bytes))]
        public PythonList splitlines() {
            return splitlines(false);
        }

        [return: SequenceTypeInfo(typeof(Bytes))]
        public PythonList splitlines(bool keepends) {
            return _bytes.SplitLines(keepends, x => new Bytes(x));
        }

        public bool startswith([BytesLike, NotNone] IList<byte> prefix) {
            return _bytes.StartsWith(prefix);
        }

        public bool startswith([BytesLike, NotNone] IList<byte> prefix, int start) {
            return _bytes.StartsWith(prefix, start, _bytes.Length);
        }

        public bool startswith([BytesLike, NotNone] IList<byte> prefix, int start, int end) {
            return _bytes.StartsWith(prefix, start, end);
        }

        public bool startswith([BytesLike, NotNone] IList<byte> prefix, object? start) {
            return startswith(prefix, start, null);
        }

        public bool startswith([BytesLike, NotNone] IList<byte> prefix, object? start, object? end) {
            int istart = start != null ? Converter.ConvertToIndex(start) : 0;
            int iend = end != null ? Converter.ConvertToIndex(end) : _bytes.Length;
            return _bytes.StartsWith(prefix, istart, iend);
        }

        public bool startswith([NotNone] PythonTuple prefix) {
            return _bytes.StartsWith(prefix);
        }

        public bool startswith([NotNone] PythonTuple prefix, int start) {
            return _bytes.StartsWith(prefix, start, _bytes.Length);
        }

        public bool startswith([NotNone] PythonTuple prefix, int start, int end) {
            return _bytes.StartsWith(prefix, start, end);
        }

        public bool startswith([NotNone] PythonTuple prefix, object? start) {
            return startswith(prefix, start, null);
        }

        public bool startswith([NotNone] PythonTuple prefix, object? start, object? end) {
            int istart = start != null ? Converter.ConvertToIndex(start) : 0;
            int iend = end != null ? Converter.ConvertToIndex(end) : _bytes.Length;
            return _bytes.StartsWith(prefix, istart, iend);
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

        public Bytes strip() {
            var res = _bytes.Strip();
            return res == null ? this.AsBytes() : new Bytes(res);
        }

        public Bytes strip([BytesLike]IList<byte>? chars) {
            if (chars == null) return strip();
            var res = _bytes.Strip(chars);
            return res == null ? this.AsBytes() : new Bytes(res);
        }

        public Bytes swapcase() {
            if (Count == 0) {
                return this.AsBytes();
            }

            return new Bytes(_bytes.SwapCase());
        }

        public Bytes title() {
            var res = _bytes.Title();
            return res == null ? this.AsBytes() : new Bytes(res);
        }

        private void ValidateTable(IList<byte>? table) {
            if (table is not null && table.Count != 256) {
                throw PythonOps.ValueError("translation table must be 256 characters long");
            }
        }

        public Bytes translate([BytesLike]IList<byte>? table) {
            ValidateTable(table);
            if (table is null) return AsBytes();
            var res = _bytes.Translate(table, null, out bool changed);
            if (changed) return Make(res.ToArray());
            return AsBytes();
        }

        public Bytes translate([BytesLike]IList<byte>? table, [BytesLike, NotNone] IList<byte> delete) {
            ValidateTable(table);
            if (table is null && delete.Count == 0) return AsBytes();
            var res = _bytes.Translate(table, delete, out bool changed);
            if (changed) return Make(res.ToArray());
            return AsBytes();
        }

        public Bytes translate([BytesLike]IList<byte>? table, object? delete) {
            if (delete is IBufferProtocol bufferProtocol) {
                using var buffer = bufferProtocol.GetBuffer();
                return translate(table, buffer.AsReadOnlySpan().ToArray());
            }
            ValidateTable(table);
            throw PythonOps.TypeError("a bytes-like object is required, not '{0}", PythonOps.GetPythonTypeName(delete));
        }

        public Bytes upper() {
            if (Count == 0) {
                return this.AsBytes();
            }

            return new Bytes(_bytes.ToUpper());
        }

        public Bytes zfill(int width) {
            int spaces = width - Count;
            if (spaces <= 0) {
                return this.AsBytes();
            }

            return new Bytes(_bytes.ZeroFill(width, spaces));
        }

        public bool __contains__([BytesLike, NotNone] IList<byte> bytes) {
            return this.IndexOf(bytes, 0) != -1;
        }

        public bool __contains__(CodeContext context, int value) {
            return IndexOf(value.ToByteChecked()) != -1;
        }

        public bool __contains__(CodeContext context, object? value) {
            switch (value) {
                case BigInteger bi:
                    return IndexOf(bi.ToByteChecked()) != -1;
                case Extensible<BigInteger> ebi:
                    return IndexOf(ebi.Value.ToByteChecked()) != -1;
            }

            throw PythonOps.TypeError("Type {0} doesn't support the buffer API", PythonOps.GetPythonTypeName(value));
        }

        public PythonTuple __getnewargs__()
            => PythonTuple.MakeTuple(AsBytes());

        public IEnumerator<int> __iter__()
            => new BytesIterator(this);

        public Bytes __mod__(CodeContext context, object? value)
            => Make(StringFormatter.FormatBytes(context, UnsafeByteArray, value));

        public Bytes __rmod__(CodeContext context, [NotNone] Bytes value)
            => Make(StringFormatter.FormatBytes(context, value.UnsafeByteArray, this));

        [return: MaybeNotImplemented]
        public NotImplementedType __rmod__(CodeContext context, object? value) => NotImplementedType.Value;

        public virtual string __str__(CodeContext context) {
            if (context.LanguageContext.PythonOptions.BytesWarning != Microsoft.Scripting.Severity.Ignore) {
                PythonOps.Warn(context, PythonExceptions.BytesWarning, "str() on a bytes instance");
            }
            return _bytes.BytesRepr();
        }

        public virtual string __repr__(CodeContext context) {
            return _bytes.BytesRepr();
        }

        public override string ToString() {
            return _bytes.BytesRepr();
        }

        public static Bytes operator +([NotNone] Bytes self, [NotNone] Bytes other) {
            var bytes = new List<byte>(self._bytes);
            bytes.AddRange(other._bytes);

            return new Bytes(bytes);
        }

        public static Bytes operator +([NotNone] Bytes self, [NotNone] ByteArray other) {
            var bytes = new List<byte>(self._bytes);
            lock (other) {
                bytes.AddRange(other);
            }

            return new Bytes(bytes);
        }

        public static Bytes operator +([NotNone] Bytes self, [NotNone] IBufferProtocol other) {
            using var buffer = other.GetBufferNoThrow();
            if (buffer is null) throw PythonOps.TypeError("can't concat {0} to bytes", PythonOps.GetPythonTypeName(other));
            var span = buffer.AsReadOnlySpan();
            var bytes = new byte[self._bytes.Length + span.Length];
            self._bytes.CopyTo(bytes, 0);
            span.CopyTo(bytes.AsSpan(self._bytes.Length));
            return new Bytes(bytes);
        }

        public static Bytes operator +([NotNone] Bytes self, object? other) {
            throw PythonOps.TypeError("can't concat {0} to bytes", PythonOps.GetPythonTypeName(other));
        }

        private static Bytes MultiplyWorker(Bytes self, int count) {
            if (count == 1) {
                return self;
            }

            return new Bytes(self._bytes.Multiply(count));
        }

        public static Bytes operator *([NotNone] Bytes self, int count) => MultiplyWorker(self, count);

        public static object operator *([NotNone] Bytes self, [NotNone] Index count)
            => PythonOps.MultiplySequence(MultiplyWorker, self, count, true);


        public static Bytes operator *([NotNone] Bytes self, object? count) {
            if (Converter.TryConvertToIndex(count, out int index)) {
                return self * index;
            }

            throw PythonOps.TypeErrorForUnIndexableObject(count);
        }

        public static Bytes operator *(int count, [NotNone] Bytes self) => MultiplyWorker(self, count);

        public static object operator *([NotNone] Index count, [NotNone] Bytes self)
            => PythonOps.MultiplySequence(MultiplyWorker, self, count, false);

        public static Bytes operator *(object? count, [NotNone] Bytes self) {
            if (Converter.TryConvertToIndex(count, out int index)) {
                return index * self;
            }

            throw PythonOps.TypeErrorForUnIndexableObject(count);
        }

        public static bool operator >([NotNone] Bytes x, [NotNone] Bytes y) {
            return x._bytes.Compare(y._bytes) > 0;
        }

        public static bool operator <([NotNone] Bytes x, [NotNone] Bytes y) {
            return x._bytes.Compare(y._bytes) < 0;
        }

        public static bool operator >=([NotNone] Bytes x, [NotNone] Bytes y) {
            return x._bytes.Compare(y._bytes) >= 0;
        }

        public static bool operator <=([NotNone] Bytes x, [NotNone] Bytes y) {
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

                throw PythonOps.IndexError("cannot fit 'int' into an index-sized integer");
            }
        }

        public Bytes this[[NotNone] Slice slice] {
            get {
                var res = _bytes.Slice(slice);
                return res == null ? Empty : new Bytes(res);
            }
        }

        public int this[object? index] {
            get {
                if (Converter.TryConvertToIndex(index, out int res)) {
                    return this[res];
                }

                throw PythonOps.TypeError("byte indices must be integers or slices, not {0}", PythonOps.GetPythonTypeName(index));
            }
        }

        #endregion

        #region Implementation Details

        internal ReadOnlyMemory<byte> AsMemory() => _bytes.AsMemory();

        internal ReadOnlySpan<byte> AsSpan() => _bytes.AsSpan();

        internal static bool TryInvokeBytesOperator(CodeContext context, object? obj, [NotNullWhen(true)] out Bytes? bytes) {
            if (PythonTypeOps.TryInvokeUnaryOperator(context, obj, "__bytes__", out object? res)) {
                if (res is Bytes b) {
                    bytes = b;
                    return true;
                } else {
                    throw PythonOps.TypeError("__bytes__ returned non-bytes (got '{0}' from type '{1}')", PythonOps.GetPythonTypeName(res), PythonOps.GetPythonTypeName(obj));
                }
            } else {
                bytes = null;
                return false;
            }
        }

        private static Bytes JoinOne(object? curVal) {
            if (curVal?.GetType() == typeof(Bytes)) {
                return (Bytes)curVal;
            }
            if (curVal is IList<byte> b) {
                return new Bytes(b);
            }
            if (curVal is IBufferProtocol bp) {
                return new Bytes(bp);
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

            return Bytes.Make(res);
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

        #region IReadOnlyList<byte> Members

        byte IReadOnlyList<byte>.this[int index] => _bytes[index];

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
            // workaround for https://github.com/IronLanguages/ironpython3/issues/1519
            if (GetType() != typeof(Bytes) && PythonTypeOps.TryInvokeUnaryOperator(DefaultContext.Default, this, "__iter__", out object? iter)) {
                return new PythonEnumerator(iter);
            }
            return _bytes.GetEnumerator();
        }

        #endregion

        #region Equality Members

        public bool __eq__(CodeContext context, [NotNone] Bytes value) => Equals(value);

        public bool __eq__(CodeContext context, [NotNone] string value) {
            if (context.LanguageContext.PythonOptions.BytesWarning != Microsoft.Scripting.Severity.Ignore) {
                PythonOps.Warn(context, PythonExceptions.BytesWarning, "Comparison between bytes and string");
            }
            return false;
        }

        public bool __eq__(CodeContext context, [NotNone] Extensible<string> value) => __eq__(context, value.Value);

        [return: MaybeNotImplemented]
        public NotImplementedType __eq__(CodeContext context, object? value) => NotImplementedType.Value;

        public bool __ne__(CodeContext context, [NotNone] Bytes value) => !Equals(value);

        public bool __ne__(CodeContext context, [NotNone] string value) => !__eq__(context, value);

        public bool __ne__(CodeContext context, [NotNone] Extensible<string> value) => !__eq__(context, value);

        [return: MaybeNotImplemented]
        public NotImplementedType __ne__(CodeContext context, object? value) => NotImplementedType.Value;

        [PythonHidden]
        public bool Equals(Bytes? other) => other != null && (ReferenceEquals(this, other) || Enumerable.SequenceEqual(_bytes, other._bytes));

        public override bool Equals(object? obj) => obj is Bytes bytes && Equals(bytes);

        public override int GetHashCode() {
            return PythonOps.MakeString(this).GetHashCode();
        }

        #endregion

        #region IExpressionSerializable Members

        Expression IExpressionSerializable.CreateExpression() {
            return Expression.Call(
                typeof(PythonOps).GetMethod(nameof(PythonOps.MakeBytes))!,
                Expression.NewArrayInit(
                    typeof(byte),
                    ArrayUtils.ConvertAll(_bytes, (b) => Expression.Constant(b))
                )
            );
        }

        #endregion

        #region IBufferProtocol Support

        IPythonBuffer IBufferProtocol.GetBuffer(BufferFlags flags) {
            if (flags.HasFlag(BufferFlags.Writable))
                throw PythonOps.BufferError("Object is not writable.");

            return new BytesView(this, flags);
        }

        private sealed class BytesView : IPythonBuffer {
            private readonly BufferFlags _flags;
            private readonly Bytes _exporter;

            public BytesView(Bytes bytes, BufferFlags flags) {
                _exporter = bytes;
                _flags = flags;
            }

            public void Dispose() { }

            public object Object => _exporter;

            public bool IsReadOnly => true;

            public ReadOnlySpan<byte> AsReadOnlySpan() => _exporter._bytes;

            public Span<byte> AsSpan()
                => throw new InvalidOperationException("bytes object is not writable");

            public MemoryHandle Pin() => _exporter._bytes.AsMemory().Pin();

            public int Offset => 0;

            public string? Format => _flags.HasFlag(BufferFlags.Format) ? "B" : null;

            public int ItemCount => _exporter._bytes.Length;

            public int ItemSize => 1;

            public int NumOfDims => 1;

            public IReadOnlyList<int>? Shape => null;

            public IReadOnlyList<int>? Strides => null;

            public IReadOnlyList<int>? SubOffsets => null;
        }

        #endregion
    }
}
