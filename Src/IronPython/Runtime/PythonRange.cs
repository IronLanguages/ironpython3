// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.
//
// Copyright (c) Pawel Jasinski.
//

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

using IronPython.Runtime.Binding;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using NotNullAttribute = Microsoft.Scripting.Runtime.NotNullAttribute;

namespace IronPython.Runtime {
    [PythonType("range")]
    [DontMapIEnumerableToContains]
    public sealed class PythonRange : ICodeFormattable, IReversible, IEnumerable<int>, IEnumerable<BigInteger> {
        internal readonly BigInteger _start;
        internal readonly BigInteger _stop;
        internal readonly BigInteger _step;
        internal readonly BigInteger _length;

        public PythonRange([AllowNull] object stop) : this(0, stop, 1) { }
        public PythonRange([AllowNull] object start, [AllowNull] object stop) : this(start, stop, 1) { }

        public PythonRange([AllowNull] object start, [AllowNull] object stop, [AllowNull] object step) {
            this.start = AsIndex(start, out _start);
            this.stop = AsIndex(stop, out _stop);
            this.step = AsIndex(step, out _step);
            if (_step == 0) {
                throw PythonOps.ValueError("step must not be zero");
            }
            _length = GetLengthHelper(_start, _stop, _step);

            static BigInteger GetLengthHelper(BigInteger start, BigInteger stop, BigInteger step) {
                BigInteger length = 0;
                if (step > 0) {
                    if (start < stop) {
                        length = (stop - start + step - 1) / step;
                    }
                } else {
                    if (start > stop) {
                        length = (stop - start + step + 1) / step;
                    }
                }
                return length;
            }
        }


        private static object AsIndex(object? obj, out BigInteger big) {
            var index = PythonOps.Index(obj);
            if (index is int i) {
                big = i;
            } else if (index is BigInteger bi) {
                big = bi;
            } else {
                throw new InvalidOperationException();
            }
            return index;
        }

        public object? start { get; private set; }

        public object? stop { get; private set; }

        public object? step { get; private set; }

        public PythonTuple __reduce__() {
            return PythonTuple.MakeTuple(
                DynamicHelpers.GetPythonType(this),
                PythonTuple.MakeTuple(start, stop, step)
            );
        }

        public int __len__() {
            return (int)_length;
        }

        public object this[int index] => this[(BigInteger)index];

        public object this[BigInteger index] {
            get {
                if (index < 0) index += _length;

                if (index >= _length || index < 0)
                    throw PythonOps.IndexError("range object index out of range");

                return index * _step + _start;
            }
        }

        public object this[[AllowNull] object index] {
            get {
                if (PythonOps.TryToIndex(index, out BigInteger bi)) {
                    return this[bi];
                }

                throw PythonOps.TypeError("range indices must be integers or slices, not {0}", PythonOps.GetPythonTypeName(index));
            }
        }

        public object this[[NotNull] Slice slice] {
            get {
                slice.indices(_length, out BigInteger ostart, out BigInteger ostop, out BigInteger ostep);
                return new PythonRange(Compute(ostart), Compute(ostop), _step * ostep);

                BigInteger Compute(BigInteger index) {
                    return index * _step + _start;
                }
            }
        }

        public bool __eq__([NotNull] PythonRange other) {
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

        [return: MaybeNotImplemented]
        public object __eq__(object? other) => other is PythonRange range ? __eq__(range) : NotImplementedType.Value;

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

        public bool __contains__(CodeContext context, object? item) {
            if (TryConvertToInt(item, out BigInteger intItem)) {
                return IndexOf(intItem) != -1;
            }
            return IndexOf(context, item) != -1;
        }

        private static bool TryConvertToInt(object? value, out BigInteger converted) {
            if (value is int i) {
                converted = i;
                return true;
            }
            if (value is BigInteger bi) {
                converted = bi;
                return true;
            }
            if (value is long l) {
                converted = l;
                return true;
            }
            converted = 0;
            return false;
        }

        private int CountOf(BigInteger value) {
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

        private int CountOf(CodeContext context, object? obj) {
            var pythonContext = context.LanguageContext;
            var count = 0;
            foreach (var i in (IEnumerable<BigInteger>)this) {
                if ((bool)pythonContext.Operation(PythonOperationKind.Equal, obj, i)) {
                    count++;
                }
            }
            return count;
        }

        private BigInteger IndexOf(BigInteger value) {
            if (CountOf(value) == 0) {
                return -1;
            }
            return (value - _start) / _step;
        }

        private int IndexOf(CodeContext context, object? obj) {
            var idx = 0;
            var pythonContext = context.LanguageContext;
            foreach (var i in (IEnumerable)this) {
                if ((bool)pythonContext.Operation(PythonOperationKind.Equal, obj, i)) {
                    return idx;
                }
                idx++;
            }
            return -1;
        }

        public object count(CodeContext context, object? value) {
            if (TryConvertToInt(value, out BigInteger i)) {
                return CountOf(i);
            }
            return CountOf(context, value);
        }

        public BigInteger index(CodeContext context, object? value) {
            BigInteger idx;
            if (TryConvertToInt(value, out BigInteger intValue)) {
                idx = IndexOf(intValue);
                if (idx == -1) {
                    throw PythonOps.ValueError("{0} is not in range", intValue);
                }
            } else {
                idx = IndexOf(context, value);
                if (idx == -1) {
                    throw PythonOps.ValueError("sequence.index(x): x not in sequence");
                }
            }
            return idx;
        }

        private BigInteger Last() {
            return _start + (_length - 1) * _step;
        }

        public IEnumerator __iter__() {
            if (IsInt(_start) && IsInt(_stop) && IsInt(_step) && IsInt(_length)) {
                return new PythonRangeIterator(this);
            } else {
                return new PythonLongRangeIterator(this);
            }

            static bool IsInt(BigInteger val) => int.MinValue <= val && val <= int.MaxValue;
        }

        public IEnumerator __reversed__()
            => new PythonRange(Last(), _start - _step, -_step).__iter__();

        IEnumerator IEnumerable.GetEnumerator() => __iter__();

        IEnumerator<int> IEnumerable<int>.GetEnumerator() => new PythonRangeIterator(this);

        IEnumerator<BigInteger> IEnumerable<BigInteger>.GetEnumerator() => new PythonLongRangeIterator(this);

        #region ICodeFormattable Members

        public string/*!*/ __repr__(CodeContext/*!*/ context) {
            return _step == 1 ?
                string.Format("range({0}, {1})", start, stop) :
                string.Format("range({0}, {1}, {2})", start, stop, step);
        }

        #endregion
    }

    [PythonType("range_iterator")]
    public sealed class PythonRangeIterator : IEnumerable, IEnumerator<int> {
        private readonly PythonRange _range;
        private int _value;
        private int _position;

        internal PythonRangeIterator(PythonRange range) {
            Debug.Assert(range._start.AsInt32(out _) && range._stop.AsInt32(out _) && range._step.AsInt32(out _) && range._length <= int.MaxValue);
            _range = range;
            _value = unchecked((int)range._start - (int)range._step); // this could overflow but we'll overflow back to the correct value on MoveNext
        }

        [PythonHidden]
        public object Current => ScriptingRuntimeHelpers.Int32ToObject(_value);

        [PythonHidden]
        public bool MoveNext() {
            if (_position >= (int)_range._length) {
                return false;
            }

            _position++;
            _value = unchecked(_value + (int)_range._step);
            return true;
        }

        [PythonHidden]
        public void Reset() {
            _value = unchecked((int)_range._start - (int)_range._step); // this could overflow but we'll overflow back to the correct value on MoveNext
            _position = 0;
        }

        public PythonTuple __reduce__(CodeContext/*!*/ context) {
            context.TryLookupBuiltin("iter", out object? iter);
            return PythonTuple.MakeTuple(
                iter,
                PythonTuple.MakeTuple(_range),
                _position
            );
        }

        public void __setstate__(int position) {
            if (position < 0) position = 0;
            else if (position > (int)_range._length) position = (int)_range._length;
            _position = position;
            _value = unchecked((int)_range._start + (_position - 1) * (int)_range._step); // this could overflow but we'll overflow back to the correct value on MoveNext
        }

        #region IEnumerator<int> Members

        int IEnumerator<int>.Current => _value;

        #endregion

        #region IDisposable Members

        [PythonHidden]
        public void Dispose() { }

        #endregion

        #region IEnumerable Members

        [PythonHidden]
        public IEnumerator GetEnumerator() => this;

        #endregion

        public int __length_hint__() {
            return (int)(_range._length - _position);
        }
    }

    [PythonType("longrange_iterator")]
    public sealed class PythonLongRangeIterator : IEnumerable, IEnumerator<BigInteger> {
        private readonly PythonRange _range;
        private BigInteger _value;
        private BigInteger _position;

        internal PythonLongRangeIterator(PythonRange range) {
            _range = range;
            _value = range._start - range._step;
        }

        [PythonHidden]
        public object Current => _value;

        [PythonHidden]
        public bool MoveNext() {
            if (_position >= _range._length) {
                return false;
            }

            _position++;
            _value = _value + _range._step;
            return true;
        }

        [PythonHidden]
        public void Reset() {
            _value = _range._start - _range._step;
            _position = 0;
        }

        public PythonTuple __reduce__(CodeContext context) {
            context.TryLookupBuiltin("iter", out object? iter);
            return PythonTuple.MakeTuple(
                iter,
                PythonTuple.MakeTuple(_range),
                _position
            );
        }

        public void __setstate__(BigInteger position) {
            if (position < 0) position = 0;
            else if (position > _range._length) position = _range._length;
            _position = position;
            _value = _range._start + (_position - 1) * _range._step;
        }

        #region IEnumerator<BigInteger> Members

        BigInteger IEnumerator<BigInteger>.Current => _value;

        #endregion

        #region IDisposable Members

        [PythonHidden]
        public void Dispose() { }

        #endregion

        #region IEnumerable Members

        [PythonHidden]
        public IEnumerator GetEnumerator() => this;

        #endregion

        public BigInteger __length_hint__() => _range._length - _position;
    }
}
