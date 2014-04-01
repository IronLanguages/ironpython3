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
using System.Linq;
using IronPython.Runtime.Binding;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;
#if FEATURE_NUMERICS
using System.Numerics;
#else
using Microsoft.Scripting.Math;
#endif

namespace IronPython.Runtime {
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix")]
    [PythonType("range")]
    [DontMapIEnumerableToContains]
    public sealed class Range : IEnumerable<BigInteger>, ICodeFormattable, IList, IReversible {
        private BigInteger _start, _stop, _step, _length;

        public Range(object stop) : this(BigInteger.Zero, stop, BigInteger.One) { }
        public Range(object start, object stop) : this(start, stop, BigInteger.One) { }

        public Range(object start, object stop, object step) {
            Initialize(start, stop, step);
        }

        private static BigInteger ConvertToBigintIndex(object value) {
            var res = ConvertToBigintIndexHelper(value);
            if (res.HasValue) {
                return res.Value;
            }
            object callable;
            if (!PythonOps.TryGetBoundAttr(value, "__index__", out callable)) {
                throw PythonOps.TypeError("expected index value, got {0}", DynamicHelpers.GetPythonType(value).Name);
            }
            var index = PythonCalls.Call(callable);
            res = ConvertToBigintIndexHelper(index);
            if (res.HasValue) {
                return res.Value;
            }
            throw PythonOps.TypeError("__index__ returned bad value: {0}", DynamicHelpers.GetPythonType(index).Name);
        }

        private static BigInteger? ConvertToBigintIndexHelper(object value) {
            return ConvertToBigintIndexHelper(value, true);
        }

        private static BigInteger? ConvertToBigintIndexHelper(object value, bool includeExtensible) {
            if (value is BigInteger) {
                return (BigInteger)value;
            }
            if (value is int) {
                return new BigInteger((int)value);
            }
            if (value is Int64) {
                return new BigInteger((Int64)value);
            }
            if (!includeExtensible) {
                return null;
            }
            Extensible<BigInteger> ebi;
            if ((ebi = value as Extensible<BigInteger>) != null) {
                return ebi.Value;
            }
            Extensible<int> eint;
            if ((eint = value as Extensible<int>) != null) {
                return new BigInteger(eint.Value);
            }
            return null;
        }

        private void Initialize(object ostart, object ostop, object ostep) {
            _stop = ConvertToBigintIndex(ostop);
            _start = ConvertToBigintIndex(ostart);
            _step = ConvertToBigintIndex(ostep);
            var stepSign = _step.Sign;
            if (stepSign == 0) {
                throw PythonOps.ValueError("step must not be zero");
            }
            _length = BigInteger.Zero;
            if (stepSign == 1) {
                if (_start < _stop) {
                    _length = (_stop - _start + _step - BigInteger.One) / _step;
                }
            } else {
                if (_start > _stop) {
                    _length = (_stop - _start + _step + BigInteger.One) / _step;
                }
            }
        }

        public BigInteger start {
            get {
                return _start;
            }
        }

        public BigInteger stop {
            get {
                return _stop;
            }
        }

        public BigInteger step {
            get {
                return _step;
            }
        }

        #region ISequence Members

        public BigInteger __len__() {
            return _length;
        }

        public object this[BigInteger index] {
            get {
                if (index.Sign == -1) {
                    index += _length;
                }
                if (index >= _length || index.Sign == -1) {
                    throw PythonOps.IndexError("range object index out of range");
                }
                return index * _step + _start;
            }
        }


        public object this[object index] {
            get {
                return this[ConvertToBigintIndex(index)];
            }
        }

        private BigInteger Compute(BigInteger index) {
            if (index.Sign == -1) {
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
            if (_length == BigInteger.Zero) {
                return true;
            }
            if (_start != other._start) {
                return false;
            }
            if (_length == BigInteger.One) {
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
            if (_length == BigInteger.Zero) {
                return 0;
            }
            var hash = _start.GetHashCode();
            hash ^= _length.GetHashCode();
            if (_length > BigInteger.One) {
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
            var tmp = ConvertToBigintIndexHelper(item, false);
            if (tmp.HasValue) {
                return IndexOf(context, tmp.Value) != BigInteger.MinusOne;
            }
            return IndexOf(context, item) != BigInteger.MinusOne;
        }

        private int CountOf(BigInteger value) {
            if (_length == BigInteger.Zero) {
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
            return (value - _start) % _step == BigInteger.Zero ? 1 : 0;
        }

        private BigInteger CountOf(CodeContext context, object obj) {
            var pythonContext = PythonContext.GetContext(context);
            return this.Count(i => (bool) pythonContext.Operation(PythonOperationKind.Equal, obj, i));
        }

        private BigInteger IndexOf(CodeContext context, object obj) {
            var idx = 0;
            var pythonContext = PythonContext.GetContext(context);
            foreach (var i in this) {
                if ((bool)pythonContext.Operation(PythonOperationKind.Equal, obj, i)) {
                    return idx;
                }
                idx++;
            }
            return BigInteger.MinusOne;
        }

        public object count(CodeContext context, object value) {
            var tmp = ConvertToBigintIndexHelper(value);
            return tmp.HasValue ? CountOf(tmp.Value) : CountOf(context, value);
        }

        public object index(CodeContext context, object value) {
            var tmp = ConvertToBigintIndexHelper(value);
            if (tmp.HasValue) {
                if (CountOf(tmp.Value) == 0) {
                    throw PythonOps.ValueError("{0} is not in range", tmp.Value);
                }
                return (tmp.Value - _start) / _step;
            }

            var idx = IndexOf(context, value);
            if (idx == BigInteger.MinusOne) {
                throw PythonOps.ValueError("{0} is not in range");
            }
            return idx;
        }

        #endregion

        private BigInteger Last() {
            return _start + (_length - BigInteger.One) * _step;
        }

        public IEnumerator __reversed__() {
            return new RangeIterator(new Range(Last(), _start - _step, -_step));
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return new RangeIterator(this);
        }

        #region IEnumerable<BigInteger> Members

        IEnumerator<BigInteger> IEnumerable<BigInteger>.GetEnumerator() {
            return new RangeIterator(this);
        }

        #endregion

        #region ICodeFormattable Members

        public string/*!*/ __repr__(CodeContext/*!*/ context) {
            return _step == BigInteger.One ?
                string.Format("range({0}, {1})", _start, _stop) :
                string.Format("range({0}, {1}, {2})", _start, _stop, _step);
        }

        #endregion

        #region ICollection Members
        void ICollection.CopyTo(Array array, int index) {
            if (_length > int.MaxValue) {
                throw new OverflowException("Number of range elements exceeds maximum array size");
            }
            foreach (var o in this) {
                array.SetValue(o, index++);
            }
        }

        int ICollection.Count {
            get {
                if (_length > int.MaxValue) {
                    throw new OverflowException("Number of range elements exceeds maximum array size");
                }
                return (int)_length;
            }
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
            var index = 0;
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
                var curIndex = 0;
                foreach (var o in this) {
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
    public sealed class RangeIterator : IEnumerable<BigInteger>, IEnumerator<BigInteger> {
        private readonly Range _range;
        private BigInteger _value;
        private BigInteger _position;

        public RangeIterator(Range range) {
            _range = range;
            _value = range.start - range.step;
        }

        public object Current {
            get {
                return _value;
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
            _position = BigInteger.Zero;
        }

        #region IEnumerator<BigInteger> Members

        BigInteger IEnumerator<BigInteger>.Current {
            get { return _value; }
        }

        #endregion

        #region IDisposable Members

        public void Dispose() {
        }

        #endregion

        #region IEnumerable Members

        IEnumerator<BigInteger> IEnumerable<BigInteger>.GetEnumerator() {
            return this;
        }

        public IEnumerator GetEnumerator() {
            return this;
        }

        #endregion
    }
}
