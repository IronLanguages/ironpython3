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

using System;
using System.Collections;
using System.Collections.Generic;
using IronPython.Runtime.Operations;
using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime {
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix")]
    [PythonType("xrange")]
    [DontMapIEnumerableToContains]
    public sealed class XRange : ICollection, IEnumerable, IEnumerable<int>, ICodeFormattable, IList, IReversible {
        private int _start, _stop, _step, _length;

        public XRange(int stop) : this(0, stop, 1) { }
        public XRange(int start, int stop) : this(start, stop, 1) { }

        public XRange(int start, int stop, int step) {
            Initialize(start, stop, step);
        }

        private void Initialize(int start, int stop, int step) {
            if (step == 0) {
                throw PythonOps.ValueError("step must not be zero");
            } else if (step > 0) {
                if (start > stop) stop = start;
            } else {
                if (start < stop) stop = start;
            }

            _start = start;
            _stop = stop;
            _step = step;
            _length = GetLengthHelper();
            _stop = start + step * _length; // make stop precise
        }

        public int Start {
            [PythonHidden]
            get {
                return _start;
            }
        }

        public int Stop {
            [PythonHidden]
            get {
                return _stop;
            }
        }

        public int Step {
            [PythonHidden]
            get {
                return _step;
            }
        }

        #region ISequence Members

        public int __len__() {
            return _length;
        }

        private int GetLengthHelper() {
            long temp;
            if (_step > 0) {
                temp = (0L + _stop - _start + _step - 1) / _step;
            } else {
                temp = (0L + _stop - _start + _step + 1) / _step;
            }

            if (temp > Int32.MaxValue) {
                throw PythonOps.OverflowError("xrange() result has too many items");
            }
            return (int)temp;
        }

        public object this[int index] {
            get {
                if (index < 0) index += _length;

                if (index >= _length || index < 0)
                    throw PythonOps.IndexError("xrange object index out of range");

                int ind = index * _step + _start;
                return ScriptingRuntimeHelpers.Int32ToObject(ind);
            }
        }

        public object this[object index] {
            get {
                return this[Converter.ConvertToIndex(index)];
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public object this[Slice slice] {
            get {
                throw PythonOps.TypeError("sequence index must be integer");
            }
        }

        #endregion

        public IEnumerator __reversed__() {
            return new XRangeIterator(new XRange(_stop - _step, _start - _step, -_step));
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return new XRangeIterator(this);
        }

        #region IEnumerable<int> Members

        IEnumerator<int> IEnumerable<int>.GetEnumerator() {
            return new XRangeIterator(this);
        }

        #endregion

        #region ICodeFormattable Members

        public string/*!*/ __repr__(CodeContext/*!*/ context) {
            if (_step == 1) {
                if (_start == 0) {
                    return string.Format("xrange({0})", _stop);
                } else {
                    return string.Format("xrange({0}, {1})", _start, _stop);
                }
            } else {
                return string.Format("xrange({0}, {1}, {2})", _start, _stop, _step);
            }
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
    public sealed class XRangeIterator : IEnumerable, IEnumerator, IEnumerator<int> {
        private XRange _xrange;
        private int _value;
        private int _position;

        public XRangeIterator(XRange xrange) {
            _xrange = xrange;
            _value = xrange.Start - xrange.Step; // this could cause overflow, fine
        }

        public object Current {
            get {
                return ScriptingRuntimeHelpers.Int32ToObject(_value);
            }
        }

        public bool MoveNext() {
            if (_position >= _xrange.__len__()) {
                return false;
            }

            _position++;
            _value = _value + _xrange.Step;
            return true;
        }

        public void Reset() {
            _value = _xrange.Start - _xrange.Step;
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
