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
    public sealed class PythonRange : IEnumerable<int>, ICodeFormattable, IList, IReversible {
        private int _length;

        public PythonRange(object stop) : this(0, stop, 1) { }
        public PythonRange(object start, object stop) : this(start, stop, 1) { }

        public PythonRange(object start, object stop, object step) {
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
                return new PythonRange(Compute(ostart), Compute(ostop), step * ostep);
            }
        }

        public bool __eq__([NotNull]PythonRange other) {
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

        [return: MaybeNotImplemented]
        public NotImplementedType __eq__(object other) => NotImplementedType.Value;

        public bool __ne__([NotNull]PythonRange other) => !__eq__(other);

        [return: MaybeNotImplemented]
        public NotImplementedType __ne__(object other) => NotImplementedType.Value;

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

        [return: MaybeNotImplemented]
        public NotImplementedType __lt__(object other) => NotImplementedType.Value;

        [return: MaybeNotImplemented]
        public NotImplementedType __le__(object other) => NotImplementedType.Value;

        [return: MaybeNotImplemented]
        public NotImplementedType __gt__(object other) => NotImplementedType.Value;

        [return: MaybeNotImplemented]
        public NotImplementedType __ge__(object other) => NotImplementedType.Value;

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
            return new PythonRangeIterator(new PythonRange(Last(), start - step, -step));
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return new PythonRangeIterator(this);
        }

        #region IEnumerable<int> Members

        IEnumerator<int> IEnumerable<int>.GetEnumerator() {
            return new PythonRangeIterator(this);
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

        int ICollection.Count => _length;

        bool ICollection.IsSynchronized => false;

        object ICollection.SyncRoot => null;

        #endregion

        #region IList Members

        int IList.Add(object value) => throw new InvalidOperationException();

        void IList.Clear() => throw new InvalidOperationException();

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

        void IList.Insert(int index, object value) => throw new InvalidOperationException();

        bool IList.IsFixedSize => true;

        bool IList.IsReadOnly => true;

        void IList.Remove(object value) => throw new InvalidOperationException();

        void IList.RemoveAt(int index) => throw new InvalidOperationException();

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
    public sealed class PythonRangeIterator : IEnumerable, IEnumerator<int> {
        private readonly PythonRange _range;
        private int _value;
        private int _position;

        internal PythonRangeIterator(PythonRange range) {
            _range = range;
            _value = range.start - range.step; // this could cause overflow, fine
        }

        [PythonHidden]
        public object Current {
            get {
                return ScriptingRuntimeHelpers.Int32ToObject(_value);
            }
        }

        [PythonHidden]
        public bool MoveNext() {
            if (_position >= _range.__len__()) {
                return false;
            }

            _position++;
            _value = _value + _range.step;
            return true;
        }

        [PythonHidden]
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

        [PythonHidden]
        public void Dispose() { }

        #endregion

        #region IEnumerable Members

        [PythonHidden]
        public IEnumerator GetEnumerator() {
            return this;
        }

        #endregion

        public int __length_hint__() {
            return _range.__len__() - _position;
        }
    }
}
