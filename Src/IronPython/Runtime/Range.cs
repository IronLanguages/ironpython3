// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.
//
// Copyright (c) Pawel Jasinski.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Binding;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime {
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix")]
    [PythonType("range")]
    [DontMapIEnumerableToContains]
    public sealed class Range : IEnumerable<int>, ICodeFormattable, IList, IReversible {
        private int _length;

        public Range(object stop) : this(0, stop, 1) { }
        public Range(object start, object stop) : this(start, stop, 1) { }

        public Range(object start, object stop, object step) {
            Initialize(start, stop, step);
        }

        private void Initialize(object ostart, object ostop, object ostep) {
            stop = Converter.ConvertToIndex(ostop);
            start = Converter.ConvertToIndex(ostart);
            step = Converter.ConvertToIndex(ostep);
            if (step == 0) {
                throw PythonOps.ValueError("step must not be zero");
            }
            _length = GetLengthHelper();
        }

        public int start { get; private set; }

        public int stop { get; private set; }

        public int step { get; private set; }

        private int GetLengthHelper() {
            long length = 0;
            if (step > 0) {
                if (start < stop) {
                    length = (0L + stop - start + step - 1) / step;
                }
            } else {
                if (start > stop) {
                    length = (0L + stop - start + step + 1) / step;
                }
            }
            if (length > Int32.MaxValue) {
                throw PythonOps.OverflowError("range() result has too many items");
            }
            return (int)length;
        }

        public PythonTuple __reduce__() {
            return PythonTuple.MakeTuple(
                DynamicHelpers.GetPythonType(this),
                PythonTuple.MakeTuple(start, stop, step)
            );
        }

        #region ISequence Members

        public int __len__() {
            return _length;
        }

        public object this[int index] {
            get {
                if (index < 0) index += _length;

                if (index >= _length || index < 0)
                    throw PythonOps.IndexError("range object index out of range");

                int ind = index * step + start;
                return ScriptingRuntimeHelpers.Int32ToObject(ind);
            }
        }

        public object this[object index] {
            get {
                return this[Converter.ConvertToIndex(index)];
            }
        }

        private int Compute(int index) {
            return index * step + start;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public object this[[NotNull]Slice slice] {
            get {
                int ostart, ostop, ostep;
                slice.indices(_length, out ostart, out ostop, out ostep);
                return new Range(Compute(ostart), Compute(ostop), step * ostep);
            }
        }

        public bool __eq__(Range other) {
            if (_length != other._length) {
                return false;
            }
            if (_length == 0) {
                return true;
            }
            if (start != other.start) {
                return false;
            }
            if (_length == 1) {
                return true;
            }
            if (Last() != other.Last()) {
                return false;
            }
            return step == other.step;
        }

        public bool __ne__(Range other) {
            return !__eq__(other);
        }

        public int __hash__() {
            if (_length == 0) {
                return 0;
            }
            var hash = start.GetHashCode();
            hash ^= _length.GetHashCode();
            if (_length > 1) {
                hash ^= step.GetHashCode();
            }
            return hash;
        }

        public bool __lt__(Range other) {
            throw new TypeErrorException("unorderable types: range() < range()");
        }

        public bool __le__(Range other) {
            throw new TypeErrorException("unorderable types: range() <= range()");
        }

        public bool __gt__(Range other) {
            throw new TypeErrorException("unorderable types: range() > range()");
        }

        public bool __ge__(Range other) {
            throw new TypeErrorException("unorderable types: range() >= range()");
        }

        public bool __contains__(CodeContext context, object item) {
            int intItem;
            if (TryConvertToInt(item, out intItem)) {
                return IndexOf(context, intItem) != -1;
            }
            return IndexOf(context, item) != -1;
        }

        private static bool TryConvertToInt(object value, out int converted) {
            if (value is BigInteger) {
                converted = (int)(BigInteger)value;
                return true;
            }
            if (value is int) {
                converted = (int)value;
                return true;
            }
            if (value is Int64) {
                converted =  (int)(Int64)value;
                return true;
            }
            converted = 0;
            return false;
        }

        private int CountOf(int value) {
            if (_length == 0) {
                return 0;
            }
            if (start < stop) {
                if (value < start || value >= stop) {
                    return 0;
                }
            } else if (start > stop) {
                if (value > start || value <= stop) {
                    return 0;
                }
            }
            return (value - start) % step == 0 ? 1 : 0;
        }

        private int CountOf(CodeContext context, object obj) {
            var pythonContext = context.LanguageContext;
            var count = 0;
            foreach (var i in this) {
                if ((bool)pythonContext.Operation(PythonOperationKind.Equal, obj, i)) {
                    count++;
                }
            }
            return count;
        }

        private int IndexOf(CodeContext context, object obj) {
            var idx = 0;
            var pythonContext = context.LanguageContext;
            foreach (var i in this) {
                if ((bool)pythonContext.Operation(PythonOperationKind.Equal, obj, i)) {
                    return idx;
                }
                idx++;
            }
            return -1;
        }

        public object count(CodeContext context, object value) {
            int intValue;
            return Converter.TryConvertToIndex(value, out intValue) ? CountOf(intValue) : CountOf(context, value);
        }

        public object index(CodeContext context, object value) {
            int intValue;
            if (Converter.TryConvertToIndex(value, out intValue)) {
                if (CountOf(intValue) == 0) {
                    throw PythonOps.ValueError("{0} is not in range", intValue);
                }
                return (intValue - start) / step;
            }
            var idx = IndexOf(context, value);
            if (idx == -1) {
                throw PythonOps.ValueError("{0} is not in range");
            }
            return idx;
        }

        #endregion

        private int Last() {
            return start + (_length - 1) * step;
        }

        public IEnumerator __reversed__() {
            return new RangeIterator(new Range(Last(), start - step, -step));
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return new RangeIterator(this);
        }

        #region IEnumerable<int> Members

        IEnumerator<int> IEnumerable<int>.GetEnumerator() {
            return new RangeIterator(this);
        }

        #endregion

        #region ICodeFormattable Members

        public string/*!*/ __repr__(CodeContext/*!*/ context) {
            return step == 1 ?
                string.Format("range({0}, {1})", start, stop) :
                string.Format("range({0}, {1}, {2})", start, stop, step);
        }

        #endregion

        #region ICollection Members
        void ICollection.CopyTo(Array array, int index) {
            foreach (object o in this) {
                array.SetValue(o, index++);
            }
        }

        int ICollection.Count {
            get { return _length; }
        }

        bool ICollection.IsSynchronized {
            get { return false; }
        }

        object ICollection.SyncRoot {
            get { return null; }
        }

        #endregion

        #region IList Members

        int IList.Add(object value) {
            throw new InvalidOperationException();
        }

        void IList.Clear() {
            throw new InvalidOperationException();
        }

        bool IList.Contains(object value) {
            return ((IList)this).IndexOf(value) != -1;
        }

        int IList.IndexOf(object value) {
            int index = 0;
            foreach (object o in this) {
                if (o == value) {
                    return index;
                }

                index++;
            }
            return -1;
        }

        void IList.Insert(int index, object value) {
            throw new InvalidOperationException();
        }

        bool IList.IsFixedSize {
            get { return true; }
        }

        bool IList.IsReadOnly {
            get { return true; }
        }

        void IList.Remove(object value) {
            throw new InvalidOperationException();
        }

        void IList.RemoveAt(int index) {
            throw new InvalidOperationException();
        }

        object IList.this[int index] {
            get {
                int curIndex = 0;
                foreach (object o in this) {
                    if (curIndex == index) {
                        return o;
                    }

                    curIndex++;
                }

                throw new IndexOutOfRangeException();
            }
            set {
                throw new InvalidOperationException();
            }
        }

        #endregion
    }

    [PythonType("range_iterator")]
    public sealed class RangeIterator : IEnumerable, IEnumerator<int> {
        private readonly Range _range;
        private int _value;
        private int _position;

        public RangeIterator(Range range) {
            _range = range;
            _value = range.start - range.step; // this could cause overflow, fine
        }

        public object Current {
            get {
                return ScriptingRuntimeHelpers.Int32ToObject(_value);
            }
        }

        public bool MoveNext() {
            if (_position >= _range.__len__()) {
                return false;
            }

            _position++;
            _value = _value + _range.step;
            return true;
        }

        public void Reset() {
            _value = _range.start - _range.step;
            _position = 0;
        }

        public PythonTuple __reduce__(CodeContext/*!*/ context) {
            object iter;
            context.TryLookupBuiltin("iter", out iter);
            return PythonTuple.MakeTuple(
                iter,
                PythonTuple.MakeTuple(_range),
                _position
            );
        }

        public void __setstate__(int position) {
            if (position < 0) position = 0;
            else if (position > _range.__len__()) position = _range.__len__();
            _position = position;
            _value = _range.start + (_position - 1) * _range.step;
        }

        #region IEnumerator<int> Members

        int IEnumerator<int>.Current {
            get { return _value; }
        }

        #endregion

        #region IDisposable Members

        public void Dispose() {
        }

        #endregion

        #region IEnumerable Members

        public IEnumerator GetEnumerator() {
            return this;
        }

        #endregion

        public int __length_hint__() {
            return _range.__len__() - _position;
        }
    }
}
