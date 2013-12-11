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

#if FEATURE_CORE_DLR
using System.Linq.Expressions;
#else
using Microsoft.Scripting.Ast;
#endif

#if FEATURE_NUMERICS
using System.Numerics;
#else
using Microsoft.Scripting.Math;
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime {
    [PythonType("bytes")]
    public class Bytes : IList<byte>, ICodeFormattable, IExpressionSerializable, IBufferProtocol {
        internal byte[]/*!*/ _bytes;
        internal static Bytes/*!*/ Empty = new Bytes();

        public Bytes() {
            _bytes = new byte[0];
        }

        public Bytes([BytesConversion, NotNull]IList<byte>/*!*/ bytes) {
            _bytes = ArrayUtils.ToArray(bytes);
        }

        public Bytes([NotNull]List bytes) {
            _bytes = ByteOps.GetBytes(bytes, ByteOps.GetByteListOk).ToArray();
        }

        public Bytes(int size) {
            _bytes = new byte[size];
        }

        private Bytes(byte[] bytes) {
            _bytes = bytes;
        }

        public Bytes(CodeContext/*!*/ context, [NotNull]string/*!*/ unicode, [NotNull]string/*!*/ encoding) {
            _bytes = StringOps.encode(context, unicode, encoding, "strict").MakeByteArray();
        }
        
        internal static Bytes Make(byte[] bytes) {
            return new Bytes(bytes);
        }

        #region Public Python API surface

        public Bytes capitalize() {
            if (Count == 0) {
                return this;
            }

            return new Bytes(_bytes.Capitalize());
        }

        public Bytes/*!*/ center(int width) {
            return center(width, " ");
        }

        public Bytes/*!*/ center(int width, [NotNull]string/*!*/ fillchar) {
            List<byte> res = _bytes.TryCenter(width, fillchar.ToByte("center", 2));

            if (res == null) {
                return this;
            }

            return new Bytes(res);
        }

        public Bytes/*!*/ center(int width, [BytesConversion]IList<byte>/*!*/ fillchar) {
            List<byte> res = _bytes.TryCenter(width, fillchar.ToByte("center", 2));

            if (res == null) {
                return this;
            }

            return new Bytes(res);
        }

        // necessary to avoid bad conversion of List -> IList<Byte>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public ByteArray/*!*/ center(int width, List fillchar) {
            throw PythonOps.TypeError("center() argument 2 must be byte, not list");
        }

        public int count([BytesConversion]IList<byte>/*!*/ sub) {
            return count(sub, 0, Count);
        }

        public int count([BytesConversion]IList<byte>/*!*/ sub, int start) {
            return count(sub, start, Count);
        }

        public int count([BytesConversion]IList<byte/*!*/> ssub, int start, int end) {
            return _bytes.CountOf(ssub, start, end);
        }

        // overloads to avoid automatic generic conversion
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public int count(List/*!*/ sub) {
            throw PythonOps.TypeError("expected bytes or bytearray, got list");
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public int count(List/*!*/ sub, int start) {
            throw PythonOps.TypeError("expected bytes or bytearray, got list");
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public int count(List/*!*/ ssub, int start, int end) {
            throw PythonOps.TypeError("expected bytes or bytearray, got list");
        }

        public string decode(CodeContext/*!*/ context, [Optional]string/*!*/ encoding, [DefaultParameterValue("strict")][NotNull]string/*!*/ errors) {
            return StringOps.decode(context, _bytes.MakeString(), encoding, errors);
        }

        public bool endswith([BytesConversion]IList<byte>/*!*/ suffix) {
            return _bytes.EndsWith(suffix);
        }

        public bool endswith([BytesConversion]IList<byte>/*!*/ suffix, int start) {
            return _bytes.EndsWith(suffix, start);
        }

        public bool endswith([BytesConversion]IList<byte>/*!*/ suffix, int start, int end) {
            return _bytes.EndsWith(suffix, start, end);
        }

        // overloads to avoid automatic generic conversion
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public bool endswith(List/*!*/ suffix) {
            throw PythonOps.TypeError("expected bytes or bytearray, got list");
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public bool endswith(List/*!*/ suffix, int start) {
            throw PythonOps.TypeError("expected bytes or bytearray, got list");
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public bool endswith(List/*!*/ suffix, int start, int end) {
            throw PythonOps.TypeError("expected bytes or bytearray, got list");
        }

        public bool endswith(PythonTuple/*!*/ suffix) {
            return _bytes.EndsWith(suffix);
        }

        public bool endswith(PythonTuple/*!*/ suffix, int start) {
            return _bytes.EndsWith(suffix, start);
        }

        public bool endswith(PythonTuple/*!*/ suffix, int start, int end) {
            return _bytes.EndsWith(suffix, start, end);
        }

        public Bytes/*!*/ expandtabs() {
            return expandtabs(8);
        }

        public Bytes/*!*/ expandtabs(int tabsize) {
            return new Bytes(_bytes.ExpandTabs(tabsize));
        }

        public int find([BytesConversion]IList<byte>/*!*/ sub) {
            return _bytes.Find(sub);
        }

        public int find([BytesConversion]IList<byte>/*!*/ sub, int? start) {
            return _bytes.Find(sub, start);
        }

        public int find([BytesConversion]IList<byte>/*!*/ sub, int? start, int? end) {
            return _bytes.Find(sub, start, end);
        }

        public static Bytes/*!*/ fromhex(string/*!*/ @string) {
            return new Bytes(IListOfByteOps.FromHex(@string).ToArray());
        }

        public int index([BytesConversion]IList<byte>/*!*/ item) {
            return index(item, 0, Count);
        }

        public int index([BytesConversion]IList<byte>/*!*/ item, int? start) {
            return index(item, start, Count);
        }

        public int index([BytesConversion]IList<byte>/*!*/ item, int? start, int? stop) {
            int res = find(item, start, stop);
            if (res == -1) {
                throw PythonOps.ValueError("bytes.index(item): item not in bytes");
            }

            return res;
        }

        public bool isalnum() {
            return _bytes.IsAlphaNumeric();
        }

        public bool isalpha() {
            return _bytes.IsLetter();
        }

        public bool isdigit() {
            return _bytes.IsDigit();
        }

        public bool islower() {
            return _bytes.IsLower();
        }

        public bool isspace() {
            return _bytes.IsWhiteSpace();
        }

        /// <summary>
        /// return true if self is a titlecased string and there is at least one
        /// character in self; also, uppercase characters may only follow uncased
        /// characters (e.g. whitespace) and lowercase characters only cased ones. 
        /// return false otherwise.
        /// </summary>
        public bool istitle() {
            return _bytes.IsTitle();
        }

        public bool isupper() {
            return _bytes.IsUpper();
        }

        /// <summary>
        /// Return a string which is the concatenation of the strings 
        /// in the sequence seq. The separator between elements is the 
        /// string providing this method
        /// </summary>
        public Bytes join(object/*!*/ sequence) {
            IEnumerator seq = PythonOps.GetEnumerator(sequence);
            if (!seq.MoveNext()) {
                return Empty;
            }

            // check if we have just a sequnce of just one value - if so just
            // return that value.
            object curVal = seq.Current;
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

        public Bytes join([NotNull]List/*!*/ sequence) {
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

        public Bytes ljust(int width, [NotNull]string/*!*/ fillchar) {
            return ljust(width, fillchar.ToByte("ljust", 2));
        }

        public Bytes ljust(int width, [BytesConversion]IList<byte>/*!*/ fillchar) {
            return ljust(width, fillchar.ToByte("ljust", 2));
        }

        private Bytes/*!*/ ljust(int width, byte fillchar) {
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

        public Bytes/*!*/ lower() {
            return new Bytes(_bytes.ToLower());
        }

        public Bytes/*!*/ lstrip() {
            List<byte> res = _bytes.LeftStrip();
            if (res == null) {
                return this;

            }
            return new Bytes(res);
        }

        public Bytes/*!*/ lstrip([BytesConversion]IList<byte> bytes) {
            lock (this) {
                List<byte> res = _bytes.LeftStrip(bytes);
                if (res == null) {
                    return this;
                }

                return new Bytes(res);
            }
        }

        public PythonTuple partition([BytesConversion]IList<byte>/*!*/ sep) {
            if (sep == null) {
                throw PythonOps.TypeError("expected string, got NoneType");
            } else if (sep.Count == 0) {
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

        public Bytes replace([BytesConversion]IList<byte>/*!*/ old, [BytesConversion]IList<byte>/*!*/ new_) {
            if (old == null) {
                throw PythonOps.TypeError("expected bytes or bytearray, got NoneType");
            }

            return replace(old, new_, _bytes.Length);
        }

        public Bytes replace([BytesConversion]IList<byte>/*!*/ old, [BytesConversion]IList<byte>/*!*/ new_, int maxsplit) {
            if (old == null) {
                throw PythonOps.TypeError("expected bytes or bytearray, got NoneType");
            } else if (maxsplit == 0) {
                return this;
            }

            return new Bytes(_bytes.Replace(old, new_, maxsplit));
        }


        public int rfind([BytesConversion]IList<byte>/*!*/ sub) {
            return rfind(sub, 0, Count);
        }

        public int rfind([BytesConversion]IList<byte>/*!*/ sub, int? start) {
            return rfind(sub, start, Count);
        }

        public int rfind([BytesConversion]IList<byte>/*!*/ sub, int? start, int? end) {
            return _bytes.ReverseFind(sub, start, end);
        }

        public int rindex([BytesConversion]IList<byte>/*!*/ sub) {
            return rindex(sub, 0, Count);
        }

        public int rindex([BytesConversion]IList<byte>/*!*/ sub, int? start) {
            return rindex(sub, start, Count);
        }

        public int rindex([BytesConversion]IList<byte>/*!*/ sub, int? start, int? end) {
            int ret = rfind(sub, start, end);

            if (ret == -1) {
                throw PythonOps.ValueError("substring {0} not found in {1}", sub, this);
            }

            return ret;
        }

        public Bytes/*!*/ rjust(int width) {
            return rjust(width, (byte)' ');
        }

        public Bytes/*!*/ rjust(int width, [NotNull]string/*!*/ fillchar) {
            return rjust(width, fillchar.ToByte("rjust", 2));
        }

        public Bytes/*!*/ rjust(int width, [BytesConversion]IList<byte>/*!*/ fillchar) {
            return rjust(width, fillchar.ToByte("rjust", 2));
        }

        private Bytes/*!*/ rjust(int width, byte fillchar) {
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

        public PythonTuple/*!*/ rpartition([BytesConversion]IList<byte>/*!*/ sep) {
            if (sep == null) {
                throw PythonOps.TypeError("expected string, got NoneType");
            } else if (sep.Count == 0) {
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

        public List/*!*/ rsplit() {
            return _bytes.SplitInternal((byte[])null, -1, x => new Bytes(x));
        }

        public List/*!*/ rsplit([BytesConversion]IList<byte> sep) {
            return rsplit(sep, -1);
        }

        public List/*!*/ rsplit([BytesConversion]IList<byte> sep, int maxsplit) {
            return _bytes.RightSplit(sep, maxsplit, x => new Bytes(new List<byte>(x)));
        }

        public Bytes/*!*/ rstrip() {
            List<byte> res = _bytes.RightStrip();
            if (res == null) {
                return this;
            }
            return new Bytes(res);
        }

        public Bytes/*!*/ rstrip([BytesConversion]IList<byte> bytes) {
            lock (this) {
                List<byte> res = _bytes.RightStrip(bytes);
                if (res == null) {
                    return this;
                }

                return new Bytes(res);
            }
        }
        
        public List/*!*/ split() {
            return _bytes.SplitInternal((byte[])null, -1, x => new Bytes(x));
        }

        public List/*!*/ split([BytesConversion]IList<byte> sep) {
            return split(sep, -1);
        }

        public List/*!*/ split([BytesConversion]IList<byte> sep, int maxsplit) {
            return _bytes.Split(sep, maxsplit, x => new Bytes(x));
        }

        public List/*!*/ splitlines() {
            return splitlines(false);
        }

        public List/*!*/ splitlines(bool keepends) {
            return _bytes.SplitLines(keepends, x => new Bytes(x));
        }

        public bool startswith([BytesConversion]IList<byte>/*!*/ prefix) {
            return _bytes.StartsWith(prefix);
        }

        public bool startswith([BytesConversion]IList<byte>/*!*/ prefix, int start) {
            int len = Count;
            if (start > len) return false;
            if (start < 0) {
                start += len;
                if (start < 0) start = 0;
            }
            return _bytes.Substring(start).StartsWith(prefix);
        }

        public bool startswith([BytesConversion]IList<byte>/*!*/ prefix, int start, int end) {
            return _bytes.StartsWith(prefix, start, end);
        }

        public bool startswith(PythonTuple/*!*/ prefix) {
            return _bytes.StartsWith(prefix);
        }

        public bool startswith(PythonTuple/*!*/ prefix, int start) {
            return _bytes.StartsWith(prefix, start);
        }

        public bool startswith(PythonTuple/*!*/ prefix, int start, int end) {
            return _bytes.StartsWith(prefix, start, end);
        }

        public Bytes/*!*/ strip() {
            List<byte> res = _bytes.Strip();
            if (res == null) {
                return this;
            }
            return new Bytes(res);
        }

        public Bytes/*!*/ strip([BytesConversion]IList<byte> chars) {
            lock (this) {
                List<byte> res = _bytes.Strip(chars);
                if (res == null) {
                    return this;
                }

                return new Bytes(res);
            }
        }

        public Bytes/*!*/ swapcase() {
            return new Bytes(_bytes.SwapCase());
        }

        public Bytes/*!*/ title() {
            lock (this) {
                List<byte> res = _bytes.Title();

                if (res == null) {
                    return this;
                }

                return new Bytes(res.ToArray());
            }
        }

        public Bytes/*!*/ translate([BytesConversion]IList<byte> table) {
            if (table == null) {
                return this;
            } else if (table.Count != 256) {
                throw PythonOps.ValueError("translation table must be 256 characters long");
            } else if (Count == 0) {
                return this;
            }

            return new Bytes(_bytes.Translate(table, null));
        }

        public Bytes/*!*/ translate([BytesConversion]IList<byte> table, [BytesConversion]IList<byte>/*!*/ deletechars) {
            if (deletechars == null) {
                throw PythonOps.TypeError("expected bytes or bytearray, got None");
            } else if (Count == 0) {
                return this;
            }

            return new Bytes(_bytes.Translate(table, deletechars));
        }

        public Bytes/*!*/ upper() {
            return new Bytes(_bytes.ToUpper());
        }

        public Bytes/*!*/ zfill(int width) {
            int spaces = width - Count;
            if (spaces <= 0) {
                return this;
            }

            return new Bytes(_bytes.ZeroFill(width, spaces));
        }

        public bool __contains__([BytesConversion]IList<byte> bytes) {
            return this.IndexOf(bytes, 0) != -1;
        }

        public bool __contains__(CodeContext/*!*/ context, int value) {
            if (!PythonContext.GetContext(context).PythonOptions.Python30) {
                throw PythonOps.TypeError("'in <bytes>' requires string or bytes as left operand, not int");
            }

            return IndexOf(value.ToByteChecked()) != -1;
        }

        public bool __contains__(CodeContext/*!*/ context, object value) {
            if (!PythonContext.GetContext(context).PythonOptions.Python30) {
                throw PythonOps.TypeError("'in <bytes>' requires string or bytes as left operand, not {0}", PythonTypeOps.GetName(value));
            }

            if (value is Extensible<int>) {
                return IndexOf(((Extensible<int>)value).Value.ToByteChecked()) != -1;
            } else if (value is BigInteger) {
                return IndexOf(((BigInteger)value).ToByteChecked()) != -1;
            } else if (value is Extensible<BigInteger>) {
                return IndexOf(((Extensible<BigInteger>)value).Value.ToByteChecked()) != -1;
            }

            // 3.0 error message
            throw PythonOps.TypeError("Type {0} doesn't support the buffer API", PythonTypeOps.GetOldName(value));
        }

        public PythonTuple __reduce__(CodeContext/*!*/ context) {
            return PythonTuple.MakeTuple(
                DynamicHelpers.GetPythonType(this),
                PythonTuple.MakeTuple(
                    PythonOps.MakeString(this),
                    "latin-1"
                ),
                GetType() == typeof(Bytes) ? null : ObjectOps.ReduceProtocol0(context, this)[2]
            );
        }

        public virtual string/*!*/ __repr__(CodeContext context) {
            return _bytes.BytesRepr();
        }
        
        public override string/*!*/ ToString() {
            return PythonOps.MakeString(this);
        }

        public static Bytes/*!*/ operator +(Bytes/*!*/ self, Bytes/*!*/ other) {
            if (self == null) {
                throw PythonOps.TypeError("expected bytes, got None");
            }
            
            List<byte> bytes;

            bytes = new List<byte>(self._bytes);
            bytes.AddRange(other._bytes);

            return new Bytes(bytes);
        }


        public static ByteArray/*!*/ operator +(Bytes/*!*/ self, ByteArray/*!*/ other) {
            List<byte> bytes;

            bytes = new List<byte>(self._bytes);
            lock (other) {
                bytes.AddRange(other);
            }

            return new ByteArray(bytes);
        }

        public static string/*!*/ operator +(Bytes/*!*/ self, string/*!*/ other) {
            return self.ToString() + other;
        }

        public static string/*!*/ operator +(string/*!*/ other, Bytes/*!*/ self) {
            return other + self.ToString();
        }

        public static Bytes/*!*/ operator *(Bytes/*!*/ x, int y) {
            if (y == 1) {
                return x;
            }

            return new Bytes(x._bytes.Multiply(y));
        }

        public static Bytes/*!*/ operator *(int x, Bytes/*!*/ y) {
            return y * x;
        }

        public static bool operator >(Bytes/*!*/ x, Bytes/*!*/ y) {
            if (y == null) {
                return true;
            }
            return x._bytes.Compare(y._bytes) > 0;
        }

        public static bool operator <(Bytes/*!*/ x, Bytes/*!*/ y) {
            if (y == null) {
                return false;
            }
            return x._bytes.Compare(y._bytes) < 0;
        }

        public static bool operator >=(Bytes/*!*/ x, Bytes/*!*/ y) {
            if (y == null) {
                return true;
            }
            return x._bytes.Compare(y._bytes) >= 0;
        }

        public static bool operator <=(Bytes/*!*/ x, Bytes/*!*/ y) {
            if (y == null) {
                return false;
            }
            return x._bytes.Compare(y._bytes) <= 0;
        }

        public object this[CodeContext/*!*/ context, int index] {
            get {
                byte res = _bytes[PythonOps.FixIndex(index, _bytes.Length)];
                if (PythonContext.GetContext(context).PythonOptions.Python30) {
                    return (int)res;
                }

                return new Bytes(new byte[] { res });
            }
            [PythonHidden]
            set {
                throw new InvalidOperationException();
            }
        }

        public object this[CodeContext/*!*/ context, BigInteger index] {
            get {
                int iVal;
                if (index.AsInt32(out iVal)) {
                    return this[context, iVal];
                }

                throw PythonOps.IndexError("cannot fit long in index");
            }
        }

        public Bytes this[Slice slice] {
            get {
                List<byte> res = _bytes.Slice(slice);
                if (res == null) {
                    return Empty;
                }

                return new Bytes(res.ToArray());
            }
        }

        /// <summary>
        /// Returns a copy of the internal byte array.
        /// </summary>
        /// <returns>
        /// System.Byte[]
        /// </returns>
        [PythonHidden]
        public byte[] ToByteArray() {
            byte[] res = null;
            if(_bytes != null) {
                res = new byte[_bytes.Length];
                _bytes.CopyTo(res, 0);
            }
            return res;
        }

        /// <summary>
        /// This method returns the underlying byte array directly.
        /// It should be used sparingly!
        /// </summary>
        /// <returns>
        /// System.Byte[]
        /// </returns>
        [PythonHidden]
        public byte[] GetUnsafeByteArray() {
            return _bytes;
        }

        #endregion

        #region Implementation Details

        private static Bytes/*!*/ JoinOne(object curVal) {
            if (!(curVal is IList<byte>)) {
                throw PythonOps.TypeError("can only join an iterable of bytes");
            }

            return curVal as Bytes ?? new Bytes(curVal as IList<byte>);
        }

        internal static Bytes/*!*/ Concat(IList<Bytes> list, int length) {
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
        public IEnumerator<byte>/*!*/ GetEnumerator() {
            return ((IEnumerable<byte>)_bytes).GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator/*!*/ System.Collections.IEnumerable.GetEnumerator() {
            return _bytes.GetEnumerator();
        }

        #endregion

        #region Equality Members

        public override bool Equals(object obj) {
            IList<byte> bytes = obj as IList<byte>;
            if (bytes != null) {
                return _bytes.Compare(bytes) == 0;
            }

            string s = obj as string;
            if (s == null) {
                Extensible<string> es = obj as Extensible<string>;
                if (es != null) {
                    s = es.Value;
                }
            }

            if (s != null) {
                return ToString() == s;
            }

            return false;
        }

        public override int GetHashCode() {
            return ToString().GetHashCode();
        }

        #endregion

        #region IExpressionSerializable Members

        Expression IExpressionSerializable.CreateExpression() {
            return Expression.Call(
                typeof(PythonOps).GetMethod("MakeBytes"),
                Expression.NewArrayInit(
                    typeof(byte),
                    ArrayUtils.ConvertAll(_bytes, (b) => Expression.Constant(b))
                )
            );
        }

        #endregion

        #region IBufferProtocol Members
        
        Bytes IBufferProtocol.GetItem(int index) {
            byte res = _bytes[PythonOps.FixIndex(index, _bytes.Length)];
            return new Bytes(new byte[] { res });
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
            return new[] { (BigInteger)this._bytes.Length - start };
        }

        PythonTuple IBufferProtocol.Strides {
            get { return PythonTuple.MakeTuple(1); }
        }

        object IBufferProtocol.SubOffsets {
            get { return null; }
        }

        Bytes IBufferProtocol.ToBytes(int start, int? end) {
            if (start == 0 && end == null) {
                return this;
            }

            return this[new Slice(start, end)];
        }

        List IBufferProtocol.ToList(int start, int? end) {
            List<byte> res = _bytes.Slice(new Slice(start, end));
            if (res == null) {
                return new List();
            }

            return new List(res.ToArray());
        }

        #endregion
    }
}
