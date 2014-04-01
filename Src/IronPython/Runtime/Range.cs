/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation.
 * Copyright (c) Pawel Jasinski.
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

using System;
using System.Collections;
using System.Collections.Generic;
using IronPython.Runtime.Binding;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using Microsoft.Scripting.Runtime;
#if FEATURE_NUMERICS
using System.Numerics;
#else
using Microsoft.Scripting.Math;
#endif

namespace IronPython.Runtime {
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix")]
    [PythonType("range")]
    [DontMapIEnumerableToContains]
    public sealed class Range : IEnumerable<int>, ICodeFormattable, IList, IReversible {
        private int _start, _stop, _step, _length;

        public Range(object stop) : this(0, stop, 1) { }
        public Range(object start, object stop) : this(start, stop, 1) { }

        public Range(object start, object stop, object step) {
            Initialize(start, stop, step);
        }


        private void Initialize(object ostart, object ostop, object ostep) {
            _stop = Converter.ConvertToIndex(ostop);
            _start = Converter.ConvertToIndex(ostart);
            _step = Converter.ConvertToIndex(ostep);
            if (_step == 0) {
                throw PythonOps.ValueError("step must not be zero");
            }
            long temp = 0;
            if (_step > 0) {
                if (_start < _stop) {
                    temp = (0L + _stop - _start + _step - 1) / _step;
                }
            } else {
                if (_start > _stop) {
                    temp = (0L + _stop - _start + _step + 1) / _step;
                }
            }
            if (temp > Int32.MaxValue) {
                throw PythonOps.OverflowError("range() result has too many items");
            }
            _length = (int) temp;
        }

        public int start {
            get {
                return _start;
            }
        }

        public int stop {
            get {
                return _stop;
            }
        }

        public int step {
            get {
                return _step;
            }
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

                int ind = index * _step + _start;
                return ScriptingRuntimeHelpers.Int32ToObject(ind);
            }
        }

        public object this[object index] {
            get {
                return this[Converter.ConvertToIndex(index)];
            }
        }

        private int Compute(int index) {
            if (index < 0) {
                index += _length;
            }
            return index * _step + _start;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public object this[Slice slice] {
            get {
                int ostart, ostop, ostep;
                slice.indices(_length, out ostart, out ostop, out ostep);
                return new Range(Compute(ostart), Compute(ostop), _step * ostep);
            }
        }

        public bool __eq__(Range other) {
            if (_length != other._length) {
                return false;
            }
            if (_length == 0) {
                return true;
            }
            if (_start != other._start) {
                return false;
            }
            if (_length == 1) {
                return true;
            }
            if (Last() != other.Last()) {
                return false;
            }
            return _step == other._step;
        }

        public bool __ne__(Range other) {
            return !__eq__(other);
        }

        public int __hash__() {
            if (_length == 0) {
                return 0;
            }
            var hash = _start.GetHashCode();
            hash ^= _length.GetHashCode();
            if (_length > 1) {
                hash ^= _step.GetHashCode();
            }
            return hash;
        }

        public bool __lt__(Range other) {
            throw new TypeErrorException("range does not support < operator");
        }

        public bool __le__(Range other) {
            throw new TypeErrorException("range does not support <= operator");
        }

        public bool __gt__(Range other) {
            throw new TypeErrorException("range does not support > operator");
        }

        public bool __ge__(Range other) {
            throw new TypeErrorException("range does not support >= operator");
        }

        public bool __contains__(CodeContext context, object item) {
            int tmp;
            if (TryConvertToInt(item, out tmp)) {
                return IndexOf(context, tmp) != -1;
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
            if (_start < _stop) {
                if (value < _start || value >= _stop) {
                    return 0;
                }
            } else if (_start > _stop) {
                if (value > _start || value <= _stop) {
                    return 0;
                }
            }
            return (value - _start) % _step == 0 ? 1 : 0;
        }

        private int CountOf(CodeContext context, object obj) {
            var pythonContext = PythonContext.GetContext(context);
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
            var pythonContext = PythonContext.GetContext(context);
            foreach (var i in this) {
                if ((bool)pythonContext.Operation(PythonOperationKind.Equal, obj, i)) {
                    return idx;
                }
                idx++;
            }
            return -1;
        }

        public object count(CodeContext context, object value) {
            int tmp;
            return Converter.TryConvertToIndex(value, out tmp) ? CountOf(tmp) : CountOf(context, value);
        }

        public object index(CodeContext context, object value) {
            int tmp;
            if (Converter.TryConvertToIndex(value, out tmp)) {
                if (CountOf(tmp) == 0) {
                    throw PythonOps.ValueError("{0} is not in range", tmp);
                }
                return (tmp - _start) / _step;
            }
            var idx = IndexOf(context, value);
            if (idx == -1) {
                throw PythonOps.ValueError("{0} is not in range");
            }
            return idx;
        }

        #endregion

        private int Last() {
            return _start + (_length - 1) * _step;
        }

        public IEnumerator __reversed__() {
            return new RangeIterator(new Range(Last(), _start - _step, -_step));
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
            return _step == 1 ?
                string.Format("range({0}, {1})", _start, _stop) :
                string.Format("range({0}, {1}, {2})", _start, _stop, _step);
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

    [PythonType("rangeiterator")]
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
    }
}
