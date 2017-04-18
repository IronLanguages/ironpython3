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

using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

#if FEATURE_NUMERICS
using System.Numerics;
#else
using Microsoft.Scripting.Math;
#endif

namespace IronPython.Runtime
{
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
    /// Construct a zero-ititialized bytearray of the specified length.
    /// (default=0)
    /// </summary>
    [PythonType("bytearray")]
    public class ByteArray : IList<byte>, ICodeFormattable, IBufferProtocol
#if CLR2
        , IValueEquality
#endif
    {
        internal List<byte>/*!*/ _bytes;

        public ByteArray()
        {
            _bytes = new List<byte>(0);
        }

        internal ByteArray(List<byte> bytes)
        {
            _bytes = bytes;
        }

        internal ByteArray(byte[] bytes)
        {
            _bytes = new List<byte>(bytes);
        }

        public void __init__()
        {
            _bytes = new List<byte>();
        }

        public void __init__(int source)
        {
            _bytes = new List<byte>(source);
            for (int i = 0; i < source; i++)
            {
                _bytes.Add(0);
            }
        }

        public void __init__(BigInteger source)
        {
            __init__((int)source);
        }

        public void __init__([NotNull]IList<byte>/*!*/ source)
        {
            _bytes = new List<byte>(source);
        }

        public void __init__(object source)
        {
            __init__(GetBytes(source));
        }

        public void __init__(CodeContext/*!*/ context, string source, string encoding, string errors="strict")
        {
            _bytes = new List<byte>(StringOps.encode(context, source, encoding, errors));
        }

        #region Public Mutable Sequence API

        public void append(int item)
        {
            lock (this)
            {
                _bytes.Add(item.ToByteChecked());
            }
        }

        public void append(object item)
        {
            lock (this)
            {
                _bytes.Add(GetByte(item));
            }
        }

        public void extend([NotNull]IEnumerable<byte>/*!*/ seq)
        {
            using (new OrderedLocker(this, seq))
            {
                // use the original count for if we're extending this w/ this
                _bytes.AddRange(seq);
            }
        }

        public void extend(object seq)
        {
            extend(GetBytes(seq));
        }

        public void insert(int index, int value)
        {
            lock (this)
            {
                if (index >= Count)
                {
                    append(value);
                    return;
                }

                index = PythonOps.FixSliceIndex(index, Count);

                _bytes.Insert(index, value.ToByteChecked());
            }
        }

        public void insert(int index, object value)
        {
            insert(index, Converter.ConvertToIndex(value));
        }

        public int pop()
        {
            lock (this)
            {
                if (Count == 0)
                {
                    throw PythonOps.IndexError("pop off of empty bytearray");
                }

                int res = _bytes[_bytes.Count - 1];
                _bytes.RemoveAt(_bytes.Count - 1);
                return res;
            }
        }

        public int pop(int index)
        {
            lock (this)
            {
                if (Count == 0)
                {
                    throw PythonOps.IndexError("pop off of empty bytearray");
                }

                index = PythonOps.FixIndex(index, Count);

                int ret = _bytes[index];
                _bytes.RemoveAt(index);
                return ret;
            }
        }

        public void remove(int value)
        {
            lock (this)
            {
                _bytes.RemoveAt(_bytes.IndexOfByte(value.ToByteChecked(), 0, _bytes.Count));
            }
        }

        public void remove(object value)
        {
            lock (this)
            {
                if (value is ByteArray)
                {
                    throw PythonOps.TypeError("an integer or string of size 1 is required");
                }
                _bytes.RemoveAt(_bytes.IndexOfByte(GetByte(value), 0, _bytes.Count));
            }
        }

        public void reverse()
        {
            lock (this)
            {
                List<byte> reversed = new List<byte>();
                for (int i = _bytes.Count - 1; i >= 0; i--)
                {
                    reversed.Add(_bytes[i]);
                }
                _bytes = reversed;
            }
        }

        [SpecialName]
        public ByteArray InPlaceAdd(ByteArray other)
        {
            using (new OrderedLocker(this, other))
            {
                _bytes.AddRange(other._bytes);
                return this;
            }
        }

        [SpecialName]
        public ByteArray InPlaceAdd(Bytes other)
        {
            lock (this)
            {
                _bytes.AddRange(other);
                return this;
            }
        }

        [SpecialName]
        public ByteArray InPlaceMultiply(int len)
        {
            lock (this)
            {
                _bytes = (this * len)._bytes;
                return this;
            }
        }

        #endregion

        #region Public Python API surface

        public ByteArray/*!*/ capitalize()
        {
            lock (this)
            {
                return new ByteArray(_bytes.Capitalize());
            }
        }

        public ByteArray/*!*/ center(int width)
        {
            return center(width, " ");
        }

        public ByteArray/*!*/ center(int width, [NotNull]string fillchar)
        {
            lock (this)
            {
                List<byte> res = _bytes.TryCenter(width, fillchar.ToByte("center", 2));

                if (res == null)
                {
                    return CopyThis();
                }

                return new ByteArray(res);
            }
        }

        public ByteArray/*!*/ center(int width, [BytesConversion]IList<byte> fillchar)
        {
            lock (this)
            {
                List<byte> res = _bytes.TryCenter(width, fillchar.ToByte("center", 2));

                if (res == null)
                {
                    return CopyThis();
                }

                return new ByteArray(res);
            }
        }

        public int count([BytesConversion]IList<byte>/*!*/ sub)
        {
            return count(sub, 0, _bytes.Count);
        }

        public int count([BytesConversion]IList<byte>/*!*/ sub, int start)
        {
            return count(sub, start, _bytes.Count);
        }

        public int count([BytesConversion]IList<byte>/*!*/ ssub, int start, int end)
        {
            lock (this)
            {
                return _bytes.CountOf(ssub, start, end);
            }
        }

        public string decode(CodeContext/*!*/ context, [Optional]object encoding, string errors="strict")
        {
            return StringOps.decode(context, _bytes.MakeString(), encoding, errors);
        }

        public bool endswith([BytesConversion]IList<byte>/*!*/ suffix)
        {
            lock (this)
            {
                return _bytes.EndsWith(suffix);
            }
        }

        public bool endswith([BytesConversion]IList<byte>/*!*/ suffix, int start)
        {
            lock (this)
            {
                return _bytes.EndsWith(suffix, start);
            }
        }

        public bool endswith([BytesConversion]IList<byte>/*!*/ suffix, int start, int end)
        {
            lock (this)
            {
                return _bytes.EndsWith(suffix, start, end);
            }
        }

        public bool endswith(PythonTuple/*!*/ suffix)
        {
            lock (this)
            {
                return _bytes.EndsWith(suffix);
            }
        }

        public bool endswith(PythonTuple/*!*/ suffix, int start)
        {
            lock (this)
            {
                return _bytes.EndsWith(suffix, start);
            }
        }

        public bool endswith(PythonTuple/*!*/ suffix, int start, int end)
        {
            lock (this)
            {
                return _bytes.EndsWith(suffix, start, end);
            }
        }

        public ByteArray/*!*/ expandtabs()
        {
            return expandtabs(8);
        }

        public ByteArray/*!*/ expandtabs(int tabsize)
        {
            lock (this)
            {
                return new ByteArray(_bytes.ExpandTabs(tabsize));
            }
        }

        public int find([BytesConversion]IList<byte>/*!*/ sub)
        {
            lock (this)
            {
                return _bytes.Find(sub);
            }
        }

        public int find([BytesConversion]IList<byte>/*!*/ sub, int start)
        {
            lock (this)
            {
                return _bytes.Find(sub, start);
            }
        }

        public int find([BytesConversion]IList<byte>/*!*/ sub, int start, int end)
        {
            lock (this)
            {
                return _bytes.Find(sub, start, end);
            }
        }

        public static ByteArray/*!*/ fromhex(string/*!*/ @string)
        {
            return new ByteArray(IListOfByteOps.FromHex(@string));
        }

        public int index([BytesConversion]IList<byte>/*!*/ item)
        {
            return index(item, 0, _bytes.Count);
        }

        public int index([BytesConversion]IList<byte>/*!*/ item, int start)
        {
            return index(item, start, _bytes.Count);
        }

        public int index([BytesConversion]IList<byte>/*!*/ item, int start, int stop)
        {
            lock (this)
            {
                int res = find(item, start, stop);
                if (res == -1)
                {
                    throw PythonOps.ValueError("bytearray.index(item): item not in bytearray");
                }

                return res;
            }
        }

        public bool isalnum()
        {
            lock (this)
            {
                return _bytes.IsAlphaNumeric();
            }
        }

        public bool isalpha()
        {
            lock (this)
            {
                return _bytes.IsLetter();
            }
        }

        public bool isdigit()
        {
            lock (this)
            {
                return _bytes.IsDigit();
            }
        }

        public bool islower()
        {
            lock (this)
            {
                return _bytes.IsLower();
            }
        }

        public bool isspace()
        {
            lock (this)
            {
                return _bytes.IsWhiteSpace();
            }
        }

        /// <summary>
        /// return true if self is a titlecased string and there is at least one
        /// character in self; also, uppercase characters may only follow uncased
        /// characters (e.g. whitespace) and lowercase characters only cased ones. 
        /// return false otherwise.
        /// </summary>
        public bool istitle()
        {
            lock (this)
            {
                return _bytes.IsTitle();
            }
        }

        public bool isupper()
        {
            lock (this)
            {
                return _bytes.IsUpper();
            }
        }

        /// <summary>
        /// Return a string which is the concatenation of the strings 
        /// in the sequence seq. The separator between elements is the 
        /// string providing this method
        /// </summary>
        public ByteArray/*!*/ join(object/*!*/ sequence)
        {
            IEnumerator seq = PythonOps.GetEnumerator(sequence);
            if (!seq.MoveNext())
            {
                return new ByteArray();
            }

            // check if we have just a sequnce of just one value - if so just
            // return that value.
            object curVal = seq.Current;
            if (!seq.MoveNext())
            {
                return JoinOne(curVal);
            }

            List<byte> ret = new List<byte>();
            ByteOps.AppendJoin(curVal, 0, ret);

            int index = 1;
            do
            {
                ret.AddRange(this);

                ByteOps.AppendJoin(seq.Current, index, ret);

                index++;
            } while (seq.MoveNext());

            return new ByteArray(ret);
        }

        public ByteArray/*!*/ join([NotNull]List/*!*/ sequence)
        {
            if (sequence.__len__() == 0)
            {
                return new ByteArray();
            }

            lock (this)
            {
                if (sequence.__len__() == 1)
                {
                    return JoinOne(sequence[0]);
                }

                List<byte> ret = new List<byte>();
                ByteOps.AppendJoin(sequence._data[0], 0, ret);
                for (int i = 1; i < sequence._size; i++)
                {
                    ret.AddRange(this);
                    ByteOps.AppendJoin(sequence._data[i], i, ret);
                }

                return new ByteArray(ret);
            }
        }

        public ByteArray/*!*/ ljust(int width)
        {
            return ljust(width, (byte)' ');
        }

        public ByteArray/*!*/ ljust(int width, [NotNull]string/*!*/ fillchar)
        {
            return ljust(width, fillchar.ToByte("ljust", 2));
        }

        public ByteArray/*!*/ ljust(int width, IList<byte>/*!*/ fillchar)
        {
            return ljust(width, fillchar.ToByte("ljust", 2));
        }

        private ByteArray/*!*/ ljust(int width, byte fillchar)
        {
            lock (this)
            {
                int spaces = width - _bytes.Count;

                List<byte> ret = new List<byte>(width);
                ret.AddRange(_bytes);
                for (int i = 0; i < spaces; i++)
                {
                    ret.Add(fillchar);
                }
                return new ByteArray(ret);
            }
        }

        public ByteArray/*!*/ lower()
        {
            lock (this)
            {
                return new ByteArray(_bytes.ToLower());
            }
        }

        public ByteArray/*!*/ lstrip()
        {
            lock (this)
            {
                List<byte> res = _bytes.LeftStrip();
                if (res == null)
                {
                    return CopyThis();
                }

                return new ByteArray(res);
            }
        }

        public ByteArray/*!*/ lstrip([BytesConversionNoString]IList<byte> bytes)
        {
            lock (this)
            {
                List<byte> res = _bytes.LeftStrip(bytes);
                if (res == null)
                {
                    return CopyThis();
                }

                return new ByteArray(res);
            }
        }

        public PythonTuple/*!*/ partition(IList<byte>/*!*/ sep)
        {
            if (sep == null)
            {
                throw PythonOps.TypeError("expected string, got NoneType");
            }
            else if (sep.Count == 0)
            {
                throw PythonOps.ValueError("empty separator");
            }

            object[] obj = new object[3] { new ByteArray(), new ByteArray(), new ByteArray() };

            if (_bytes.Count != 0)
            {
                int index = find(sep);
                if (index == -1)
                {
                    obj[0] = CopyThis();
                }
                else
                {
                    obj[0] = new ByteArray(_bytes.Substring(0, index));
                    obj[1] = sep;
                    obj[2] = new ByteArray(_bytes.Substring(index + sep.Count, _bytes.Count - index - sep.Count));
                }
            }

            return new PythonTuple(obj);
        }

        public PythonTuple/*!*/ partition([NotNull]List/*!*/ sep)
        {
            return partition(GetBytes(sep));
        }

        public ByteArray/*!*/ replace([BytesConversion]IList<byte>/*!*/ old, [BytesConversion]IList<byte> @new)
        {
            if (old == null)
            {
                throw PythonOps.TypeError("expected bytes or bytearray, got NoneType");
            }

            return replace(old, @new, _bytes.Count);
        }

        public ByteArray/*!*/ replace([BytesConversion]IList<byte>/*!*/ old, [BytesConversion]IList<byte>/*!*/ @new, int count)
        {
            if (old == null)
            {
                throw PythonOps.TypeError("expected bytes or bytearray, got NoneType");
            }
            else if (count == 0)
            {
                return CopyThis();
            }

            return new ByteArray(_bytes.Replace(old, @new, count));
        }


        public int rfind([BytesConversion]IList<byte>/*!*/ sub)
        {
            return rfind(sub, 0, _bytes.Count);
        }

        public int rfind([BytesConversion]IList<byte>/*!*/ sub, int start)
        {
            return rfind(sub, start, _bytes.Count);
        }

        public int rfind([BytesConversion]IList<byte>/*!*/ sub, int start, int end)
        {
            lock (this)
            {
                return _bytes.ReverseFind(sub, start, end);
            }
        }

        public int rindex([BytesConversion]IList<byte>/*!*/ sub)
        {
            return rindex(sub, 0, _bytes.Count);
        }

        public int rindex([BytesConversion]IList<byte>/*!*/ sub, int start)
        {
            return rindex(sub, start, _bytes.Count);
        }

        public int rindex([BytesConversion]IList<byte>/*!*/ sub, int start, int end)
        {
            int ret = rfind(sub, start, end);

            if (ret == -1)
            {
                throw PythonOps.ValueError("substring {0} not found in {1}", sub, this);
            }

            return ret;
        }

        public ByteArray/*!*/ rjust(int width)
        {
            return rjust(width, (byte)' ');
        }

        public ByteArray/*!*/ rjust(int width, [NotNull]string/*!*/ fillchar)
        {
            return rjust(width, fillchar.ToByte("rjust", 2));
        }

        public ByteArray/*!*/ rjust(int width, [BytesConversion]IList<byte>/*!*/ fillchar)
        {
            return rjust(width, fillchar.ToByte("rjust", 2));
        }

        private ByteArray/*!*/ rjust(int width, int fillchar)
        {
            byte fill = fillchar.ToByteChecked();

            lock (this)
            {
                int spaces = width - _bytes.Count;
                if (spaces <= 0)
                {
                    return CopyThis();
                }

                List<byte> ret = new List<byte>(width);
                for (int i = 0; i < spaces; i++)
                {
                    ret.Add(fill);
                }
                ret.AddRange(_bytes);
                return new ByteArray(ret);
            }
        }

        public PythonTuple/*!*/ rpartition(IList<byte>/*!*/ sep)
        {
            if (sep == null)
            {
                throw PythonOps.TypeError("expected string, got NoneType");
            }
            else if (sep.Count == 0)
            {
                throw PythonOps.ValueError("empty separator");
            }

            lock (this)
            {
                object[] obj = new object[3] { new ByteArray(), new ByteArray(), new ByteArray() };
                if (_bytes.Count != 0)
                {
                    int index = rfind(sep);
                    if (index == -1)
                    {
                        obj[2] = CopyThis();
                    }
                    else
                    {
                        obj[0] = new ByteArray(_bytes.Substring(0, index));
                        obj[1] = new ByteArray(new List<byte>(sep));
                        obj[2] = new ByteArray(_bytes.Substring(index + sep.Count, Count - index - sep.Count));
                    }
                }
                return new PythonTuple(obj);
            }
        }

        public PythonTuple/*!*/ rpartition([NotNull]List/*!*/ sep)
        {
            return rpartition(GetBytes(sep));
        }

        public List/*!*/ rsplit()
        {
            lock (this)
            {
                return _bytes.SplitInternal((byte[])null, -1, x => new ByteArray(x));
            }
        }

        public List/*!*/ rsplit([BytesConversionNoString]IList<byte>/*!*/ sep)
        {
            return rsplit(sep, -1);
        }

        public List/*!*/ rsplit([BytesConversionNoString]IList<byte>/*!*/ sep, int maxsplit)
        {
            return _bytes.RightSplit(sep, maxsplit, x => new ByteArray(new List<byte>(x)));
        }

        public ByteArray/*!*/ rstrip()
        {
            lock (this)
            {
                List<byte> res = _bytes.RightStrip();
                if (res == null)
                {
                    return CopyThis();
                }

                return new ByteArray(res);
            }
        }

        public ByteArray/*!*/  rstrip([BytesConversionNoString]IList<byte> bytes)
        {
            lock (this)
            {
                List<byte> res = _bytes.RightStrip(bytes);
                if (res == null)
                {
                    return CopyThis();
                }

                return new ByteArray(res);
            }
        }

        public List/*!*/ split()
        {
            lock (this)
            {
                return _bytes.SplitInternal((byte[])null, -1, x => new ByteArray(x));
            }
        }

        public List/*!*/ split([BytesConversionNoString]IList<byte> sep)
        {
            return split(sep, -1);
        }

        public List/*!*/ split([BytesConversionNoString]IList<byte> sep, int maxsplit)
        {
            lock (this)
            {
                return _bytes.Split(sep, maxsplit, x => new ByteArray(x));
            }
        }

        public List/*!*/ splitlines()
        {
            return splitlines(false);
        }

        public List/*!*/ splitlines(bool keepends)
        {
            lock (this)
            {
                return _bytes.SplitLines(keepends, x => new ByteArray(x));
            }
        }

        public bool startswith([BytesConversion]IList<byte>/*!*/ prefix)
        {
            lock (this)
            {
                return _bytes.StartsWith(prefix);
            }
        }

        public bool startswith([BytesConversion]IList<byte>/*!*/ prefix, int start)
        {
            lock (this)
            {
                int len = Count;
                if (start > len)
                {
                    return false;
                }
                else if (start < 0)
                {
                    start += len;
                    if (start < 0) start = 0;
                }
                return _bytes.Substring(start).StartsWith(prefix);
            }
        }

        public bool startswith([BytesConversion]IList<byte>/*!*/ prefix, int start, int end)
        {
            lock (this)
            {
                return _bytes.StartsWith(prefix, start, end);
            }
        }

        public bool startswith(PythonTuple/*!*/ prefix)
        {
            lock (this)
            {
                return _bytes.StartsWith(prefix);
            }
        }

        public bool startswith(PythonTuple/*!*/ prefix, int start)
        {
            lock (this)
            {
                return _bytes.StartsWith(prefix, start);
            }
        }

        public bool startswith(PythonTuple/*!*/ prefix, int start, int end)
        {
            lock (this)
            {
                return _bytes.StartsWith(prefix, start, end);
            }
        }

        public ByteArray/*!*/ strip()
        {
            lock (this)
            {
                List<byte> res = _bytes.Strip();
                if (res == null)
                {
                    return CopyThis();
                }

                return new ByteArray(res);
            }
        }

        public ByteArray/*!*/ strip([BytesConversionNoString]IList<byte> chars)
        {
            lock (this)
            {
                List<byte> res = _bytes.Strip(chars);
                if (res == null)
                {
                    return CopyThis();
                }

                return new ByteArray(res);
            }
        }

        public ByteArray/*!*/ swapcase()
        {
            lock (this)
            {
                return new ByteArray(_bytes.SwapCase());
            }
        }

        public ByteArray/*!*/ title()
        {
            lock (this)
            {
                List<byte> res = _bytes.Title();

                if (res == null)
                {
                    return CopyThis();
                }

                return new ByteArray(res);
            }
        }

        public ByteArray/*!*/ translate([BytesConversion]IList<byte>/*!*/ table)
        {

            lock (this)
            {
                if (table != null)
                {
                    if (table.Count != 256)
                    {
                        throw PythonOps.ValueError("translation table must be 256 characters long");
                    }
                    else if (Count == 0)
                    {
                        return CopyThis();
                    }
                }

                return new ByteArray(_bytes.Translate(table, null));
            }
        }


        public ByteArray/*!*/ translate([BytesConversion]IList<byte>/*!*/ table, [BytesConversion]IList<byte>/*!*/ deletechars)
        {
            if (table == null && deletechars == null)
            {
                throw PythonOps.TypeError("expected bytearray or bytes, got NoneType");
            }
            else if (deletechars == null)
            {
                throw PythonOps.TypeError("expected bytes or bytearray, got None");
            }

            lock (this)
            {
                return new ByteArray(_bytes.Translate(table, deletechars));
            }
        }

        public ByteArray/*!*/ upper()
        {
            lock (this)
            {
                return new ByteArray(_bytes.ToUpper());
            }
        }

        public ByteArray/*!*/ zfill(int width)
        {
            lock (this)
            {
                int spaces = width - Count;
                if (spaces <= 0)
                {
                    return CopyThis();
                }

                return new ByteArray(_bytes.ZeroFill(width, spaces));
            }
        }

        public int __alloc__()
        {
            if (_bytes.Count == 0)
            {
                return 0;
            }

            return _bytes.Count + 1;
        }

        public bool __contains__([BytesConversionNoString]IList<byte> bytes)
        {
            return this.IndexOf(bytes, 0) != -1;
        }

        public bool __contains__(int value)
        {
            return IndexOf(value.ToByteChecked()) != -1;
        }

        public bool __contains__(CodeContext/*!*/ context, object value)
        {
            if (value is Extensible<int>)
            {
                return IndexOf(((Extensible<int>)value).Value.ToByteChecked()) != -1;
            }
            else if (value is BigInteger)
            {
                return IndexOf(((BigInteger)value).ToByteChecked()) != -1;
            }
            else if (value is Extensible<BigInteger>)
            {
                return IndexOf(((Extensible<BigInteger>)value).Value.ToByteChecked()) != -1;
            }

            throw PythonOps.TypeError("Type {0} doesn't support the buffer API", PythonTypeOps.GetName(value));
        }

        public PythonTuple __reduce__(CodeContext/*!*/ context)
        {
            return PythonTuple.MakeTuple(
                DynamicHelpers.GetPythonType(this),
                PythonTuple.MakeTuple(
                    PythonOps.MakeString(this),
                    "latin-1"
                ),
                GetType() == typeof(ByteArray) ? null : ObjectOps.ReduceProtocol0(context, this)[2]
            );
        }

        public virtual string/*!*/ __repr__(CodeContext/*!*/ context)
        {
            lock (this)
            {
                return "bytearray(" + _bytes.BytesRepr() + ")";
            }
        }

        public static ByteArray operator +(ByteArray self, ByteArray other)
        {
            if (self == null)
            {
                throw PythonOps.TypeError("expected ByteArray, got None");
            }

            List<byte> bytes;

            lock (self)
            {
                bytes = new List<byte>(self._bytes);
            }
            lock (other)
            {
                bytes.AddRange(other._bytes);
            }

            return new ByteArray(bytes);
        }

        public static ByteArray operator +(ByteArray self, Bytes other)
        {
            List<byte> bytes;

            lock (self)
            {
                bytes = new List<byte>(self._bytes);
            }

            bytes.AddRange(other);

            return new ByteArray(bytes);
        }

        public static ByteArray operator *(ByteArray x, int y)
        {
            lock (x)
            {
                if (y == 1)
                {
                    return x.CopyThis();
                }

                return new ByteArray(x._bytes.Multiply(y));
            }
        }

        public static ByteArray operator *(int x, ByteArray y)
        {
            return y * x;
        }

        public static bool operator >(ByteArray/*!*/ x, ByteArray y)
        {
            if (y == null)
            {
                return true;
            }

            using (new OrderedLocker(x, y))
            {
                return x._bytes.Compare(y._bytes) > 0;
            }
        }

        public static bool operator <(ByteArray/*!*/ x, ByteArray y)
        {
            if (y == null)
            {
                return false;
            }

            using (new OrderedLocker(x, y))
            {
                return x._bytes.Compare(y._bytes) < 0;
            }
        }

        public static bool operator >=(ByteArray/*!*/ x, ByteArray y)
        {
            if (y == null)
            {
                return true;
            }
            using (new OrderedLocker(x, y))
            {
                return x._bytes.Compare(y._bytes) >= 0;
            }
        }

        public static bool operator <=(ByteArray/*!*/ x, ByteArray y)
        {
            if (y == null)
            {
                return false;
            }
            using (new OrderedLocker(x, y))
            {
                return x._bytes.Compare(y._bytes) <= 0;
            }
        }

        public static bool operator >(ByteArray/*!*/ x, Bytes y)
        {
            if (y == null)
            {
                return true;
            }
            lock (x)
            {
                return x._bytes.Compare(y) > 0;
            }
        }

        public static bool operator <(ByteArray/*!*/ x, Bytes y)
        {
            if (y == null)
            {
                return false;
            }
            lock (x)
            {
                return x._bytes.Compare(y) < 0;
            }
        }

        public static bool operator >=(ByteArray/*!*/ x, Bytes y)
        {
            if (y == null)
            {
                return true;
            }
            lock (x)
            {
                return x._bytes.Compare(y) >= 0;
            }
        }

        public static bool operator <=(ByteArray/*!*/ x, Bytes y)
        {
            if (y == null)
            {
                return false;
            }
            lock (x)
            {
                return x._bytes.Compare(y) <= 0;
            }
        }

        public object this[int index]
        {
            get
            {
                lock (this)
                {
                    return ScriptingRuntimeHelpers.Int32ToObject((int)_bytes[PythonOps.FixIndex(index, _bytes.Count)]);
                }
            }
            set
            {
                lock (this)
                {
                    _bytes[PythonOps.FixIndex(index, _bytes.Count)] = GetByte(value);
                }
            }
        }

        public object this[BigInteger index]
        {
            get
            {
                int iVal;
                if (index.AsInt32(out iVal))
                {
                    return this[iVal];
                }

                throw PythonOps.IndexError("cannot fit long in index");
            }
            set
            {
                int iVal;
                if (index.AsInt32(out iVal))
                {
                    this[iVal] = value;
                    return;
                }

                throw PythonOps.IndexError("cannot fit long in index");
            }
        }

        public object this[Slice/*!*/ slice]
        {
            get
            {
                lock (this)
                {
                    List<byte> res = _bytes.Slice(slice);
                    if (res == null)
                    {
                        return new ByteArray();
                    }

                    return new ByteArray(res);
                }
            }
            set
            {
                if (slice == null)
                {
                    throw PythonOps.TypeError("bytearray indices must be integer or slice, not None");
                }

                // get a list of the bytes we're going to assign into the slice.  We accept:
                //      integers, longs, etc... - fill in an array of 0 bytes
                //      list of bytes, indexables, etc...

                IList<byte> list = value as IList<byte>;
                if (list == null)
                {
                    int? iVal = null;
                    if (value is int)
                    {
                        iVal = (int)value;
                    }
                    else if (value is Extensible<int>)
                    {
                        iVal = ((Extensible<int>)value).Value;
                    }
                    else if (value is BigInteger)
                    {
                        int intval;
                        if (((BigInteger)value).AsInt32(out intval))
                        {
                            iVal = intval;
                        }
                    }

                    if (iVal != null)
                    {
                        List<byte> newlist = new List<byte>();
                        newlist.Capacity = iVal.Value;
                        for (int i = 0; i < iVal; i++)
                        {
                            newlist.Add(0);
                        }
                        list = newlist;
                    }
                    else
                    {
                        IEnumerator ie = PythonOps.GetEnumerator(value);
                        list = new List<byte>();
                        while (ie.MoveNext())
                        {
                            list.Add(GetByte(ie.Current));
                        }
                    }
                }

                lock (this)
                {
                    if (slice.step != null)
                    {
                        // try to assign back to self: make a copy first
                        if (this == list)
                        {
                            value = CopyThis();
                        }
                        else if (list.Count == 0)
                        {
                            DeleteItem(slice);
                            return;
                        }

                        IList<byte> castedVal = GetBytes(value);

                        int start, stop, step;
                        slice.indices(_bytes.Count, out start, out stop, out step);

                        int n = (step > 0 ? (stop - start + step - 1) : (stop - start + step + 1)) / step;

                        // we don't use slice.Assign* helpers here because bytearray has different assignment semantics.

                        if (list.Count < n)
                        {
                            throw PythonOps.ValueError("too few items in the enumerator. need {0} have {1}", n, castedVal.Count);
                        }

                        for (int i = 0, index = start; i < castedVal.Count; i++, index += step)
                        {
                            if (i >= n)
                            {
                                if (index == _bytes.Count)
                                {
                                    _bytes.Add(castedVal[i]);
                                }
                                else
                                {
                                    _bytes.Insert(index, castedVal[i]);
                                }
                            }
                            else
                            {
                                _bytes[index] = castedVal[i];
                            }
                        }
                    }
                    else
                    {
                        int start, stop, step;
                        slice.indices(_bytes.Count, out start, out stop, out step);

                        SliceNoStep(start, stop, list);
                    }
                }
            }
        }

        [SpecialName]
        public void DeleteItem(int index)
        {
            _bytes.RemoveAt(PythonOps.FixIndex(index, _bytes.Count));
        }

        [SpecialName]
        public void DeleteItem(Slice/*!*/ slice)
        {
            if (slice == null)
            {
                throw PythonOps.TypeError("list indices must be integers or slices");
            }

            lock (this)
            {
                int start, stop, step;
                // slice is sealed, indices can't be user code...
                slice.indices(_bytes.Count, out start, out stop, out step);

                if (step > 0 && (start >= stop)) return;
                if (step < 0 && (start <= stop)) return;

                if (step == 1)
                {
                    int i = start;
                    for (int j = stop; j < _bytes.Count; j++, i++)
                    {
                        _bytes[i] = _bytes[j];
                    }
                    _bytes.RemoveRange(i, stop - start);
                    return;
                }
                else if (step == -1)
                {
                    int i = stop + 1;
                    for (int j = start + 1; j < _bytes.Count; j++, i++)
                    {
                        _bytes[i] = _bytes[j];
                    }
                    _bytes.RemoveRange(i, start - stop);
                    return;
                }
                else if (step < 0)
                {
                    // find "start" we will skip in the 1,2,3,... order
                    int i = start;
                    while (i > stop)
                    {
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

                while (curr < stop && move < stop)
                {
                    if (move != skip)
                    {
                        _bytes[curr++] = _bytes[move];
                    }
                    else
                        skip += step;
                    move++;
                }
                while (stop < _bytes.Count)
                {
                    _bytes[curr++] = _bytes[stop++];
                }
                _bytes.RemoveRange(curr, _bytes.Count - curr);
            }
        }

        #endregion

        #region Implementation Details

        private static ByteArray/*!*/ JoinOne(object/*!*/ curVal)
        {
            if (!(curVal is IList<byte>))
            {
                throw PythonOps.TypeError("can only join an iterable of bytes");
            }

            return new ByteArray(new List<byte>(curVal as IList<byte>));
        }

        private ByteArray/*!*/ CopyThis()
        {
            return new ByteArray(new List<byte>(_bytes));
        }

        private void SliceNoStep(int start, int stop, IList<byte>/*!*/ value)
        {
            // always copy from a List object, even if it's a copy of some user defined enumerator.  This
            // makes it easy to hold the lock for the duration fo the copy.
            IList<byte> other = GetBytes(value);

            lock (this)
            {
                if (start > stop)
                {
                    int newSize = Count + other.Count;

                    List<byte> newData = new List<byte>(newSize);
                    int reading = 0;
                    for (reading = 0; reading < start; reading++)
                    {
                        newData.Add(_bytes[reading]);
                    }

                    for (int i = 0; i < other.Count; i++)
                    {
                        newData.Add(other[i]);
                    }

                    for (; reading < Count; reading++)
                    {
                        newData.Add(_bytes[reading]);
                    }

                    _bytes = newData;
                }
                else if ((stop - start) == other.Count)
                {
                    // we are simply replacing values, this is fast...
                    for (int i = 0; i < other.Count; i++)
                    {
                        _bytes[i + start] = other[i];
                    }
                }
                else
                {
                    // we are resizing the array (either bigger or smaller), we 
                    // will copy the data array and replace it all at once.
                    int newSize = Count - (stop - start) + other.Count;

                    List<byte> newData = new List<byte>(newSize);
                    for (int i = 0; i < start; i++)
                    {
                        newData.Add(_bytes[i]);
                    }

                    for (int i = 0; i < other.Count; i++)
                    {
                        newData.Add(other[i]);
                    }

                    for (int i = stop; i < Count; i++)
                    {
                        newData.Add(_bytes[i]);
                    }

                    _bytes = newData;
                }
            }
        }

        private static byte GetByte(object/*!*/ value)
        {
            if (value is double || value is Extensible<double> || value is float)
            {
                throw PythonOps.TypeError("an integer or string of size 1 is required");
            }
            return ByteOps.GetByteListOk(value);
        }

        private static IList<byte>/*!*/ GetBytes(object/*!*/ value)
        {
            ListGenericWrapper<byte> genWrapper = value as ListGenericWrapper<byte>;
            if (genWrapper == null && value is IList<byte>)
            {
                return (IList<byte>)value;
            }

            var strValue = value as string;
            if (strValue != null)
            {
                return strValue.MakeByteArray();
            }

            var esValue = value as Extensible<string>;
            if (esValue != null)
            {
                return esValue.Value.MakeByteArray();
            }

            List<byte> ret = new List<byte>();
            IEnumerator ie = PythonOps.GetEnumerator(value);
            while (ie.MoveNext())
            {
                ret.Add(GetByte(ie.Current));
            }
            return ret;
        }

        #endregion

        #region IList<byte> Members

        [PythonHidden]
        public int IndexOf(byte item)
        {
            lock (this)
            {
                return _bytes.IndexOf(item);
            }
        }

        [PythonHidden]
        public void Insert(int index, byte item)
        {
            _bytes.Insert(index, item);
        }

        [PythonHidden]
        public void RemoveAt(int index)
        {
            _bytes.RemoveAt(index);
        }

        byte IList<byte>.this[int index]
        {
            get
            {
                return _bytes[index];
            }
            set
            {
                _bytes[index] = value;
            }
        }

        #endregion

        #region ICollection<byte> Members

        [PythonHidden]
        public void Add(byte item)
        {
            lock (this)
            {
                _bytes.Add(item);
            }
        }

        [PythonHidden]
        public void Clear()
        {
            lock (this)
            {
                _bytes.Clear();
            }
        }

        [PythonHidden]
        public bool Contains(byte item)
        {
            lock (this)
            {
                return _bytes.Contains(item);
            }
        }

        [PythonHidden]
        public void CopyTo(byte[]/*!*/ array, int arrayIndex)
        {
            lock (this)
            {
                _bytes.CopyTo(array, arrayIndex);
            }
        }

        public int Count
        {
            [PythonHidden]
            get
            {
                lock (this)
                {
                    return _bytes.Count;
                }
            }
        }

        public bool IsReadOnly
        {
            [PythonHidden]
            get { return false; }
        }

        [PythonHidden]
        public bool Remove(byte item)
        {
            lock (this)
            {
                return _bytes.Remove(item);
            }
        }

        #endregion

        public IEnumerator __iter__()
        {
            return PythonOps.BytesIntEnumerator(this).Key;
        }

        #region IEnumerable<byte> Members

        [PythonHidden]
        public IEnumerator<byte>/*!*/ GetEnumerator()
        {
            return _bytes.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator/*!*/ System.Collections.IEnumerable.GetEnumerator()
        {
            foreach (var _byte in _bytes)
            {
                yield return (int)_byte;
            }
        }

        #endregion

        #region IValueEquality Members
#if CLR2
        int IValueEquality.GetValueHashCode()
        {
            throw PythonOps.TypeError("bytearray object is unhashable");
        }

        bool IValueEquality.ValueEquals(object other)
        {
            return Equals(other);
        }
#endif

        public const object __hash__ = null;

        public override int GetHashCode()
        {
            return (PythonTuple.MakeTuple(_bytes.ToArray())).GetHashCode();
        }

        public override bool Equals(object other)
        {
            IList<byte> bytes;
            if (other is string)
                bytes = PythonOps.MakeBytes(((string)other).MakeByteArray());
            else if (other is Extensible<string>)
                bytes = PythonOps.MakeBytes(((Extensible<string>)other).Value.MakeByteArray());
            else
                bytes = other as IList<byte>;

            if (bytes == null || Count != bytes.Count)
            {
                return false;
            }
            else if (Count == 0)
            {
                // 2 empty ByteArrays are equal
                return true;
            }

            using (new OrderedLocker(this, other))
            {
                for (int i = 0; i < Count; i++)
                {
                    if (_bytes[i] != bytes[i])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        #endregion

        public override string ToString()
        {
            return _bytes.MakeString();
        }

        #region IBufferProtocol Members

        Bytes IBufferProtocol.GetItem(int index)
        {
            lock (this)
            {
                return new Bytes(new[] { _bytes[PythonOps.FixIndex(index, _bytes.Count)] });
            }
        }

        void IBufferProtocol.SetItem(int index, object value)
        {
            this[index] = value;
        }

        void IBufferProtocol.SetSlice(Slice index, object value)
        {
            this[index] = value;
        }

        int IBufferProtocol.ItemCount
        {
            get
            {
                return _bytes.Count;
            }
        }

        string IBufferProtocol.Format
        {
            get { return "B"; }
        }

        BigInteger IBufferProtocol.ItemSize
        {
            get { return 1; }
        }

        BigInteger IBufferProtocol.NumberDimensions
        {
            get { return 1; }
        }

        bool IBufferProtocol.ReadOnly
        {
            get { return false; }
        }

        IList<BigInteger> IBufferProtocol.GetShape(int start, int? end)
        {
            if (end != null)
            {
                return new[] { (BigInteger)end - start };
            }
            return new[] { (BigInteger)this._bytes.Count - start };
        }

        PythonTuple IBufferProtocol.Strides
        {
            get { return PythonTuple.MakeTuple(1); }
        }

        object IBufferProtocol.SubOffsets
        {
            get { return null; }
        }

        Bytes IBufferProtocol.ToBytes(int start, int? end)
        {
            if (start == 0 && end == null)
            {
                return new Bytes(this);
            }

            return new Bytes((ByteArray)this[new Slice(start, end)]);
        }

        List IBufferProtocol.ToList(int start, int? end)
        {
            List<byte> res = _bytes.Slice(new Slice(start, end));
            if (res == null)
            {
                return new List();
            }

            return new List(res.ToArray());
        }

        #endregion
    }
}
