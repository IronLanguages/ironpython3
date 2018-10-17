// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using System.Threading;

using Microsoft.Scripting;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using SpecialNameAttribute = System.Runtime.CompilerServices.SpecialNameAttribute;

namespace IronPython.Runtime {

    [PythonType("list"), Serializable, System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix")]
    [DebuggerTypeProxy(typeof(ObjectCollectionDebugProxy)), DebuggerDisplay("list, {Count} items")]
    public class PythonList : IList, ICodeFormattable, IList<object>, IReversible, IStructuralEquatable, IStructuralComparable, IReadOnlyList<object> {
        private const int INITIAL_SIZE = 20;

        internal int _size;
        internal volatile object[] _data;

        public void __init__() {
            _data = new object[8];
            _size = 0;
        }

        public void __init__([NotNull] IEnumerable enumerable) {
            __init__();

            foreach (object o in enumerable) {
                AddNoLock(o);
            }
        }

        public void __init__([NotNull] ICollection sequence) {
            _data = new object[sequence.Count];
            int i = 0;
            foreach (object item in sequence) {
                _data[i++] = item;
            }
            _size = i;
        }

        public void __init__([NotNull] SetCollection sequence) {
            PythonList list = sequence._items.GetItems();
            _size = list._size;
            _data = list._data;
        }

        public void __init__([NotNull] FrozenSetCollection sequence) {
            PythonList list = sequence._items.GetItems();
            _size = list._size;
            _data = list._data;
        }

        public void __init__([NotNull] PythonList sequence) {
            if (this == sequence) {
                // list.__init__(l, l) resets l
                _size = 0;
                return;
            }

            _data = new object[sequence._size];

            object[] data = sequence._data;
            for (int i = 0; i < _data.Length; i++) {
                _data[i] = data[i];
            }
            _size = _data.Length;
        }

        public void __init__([NotNull] string sequence) {
            _data = new object[sequence.Length];
            _size = sequence.Length;

            for (int i = 0; i < sequence.Length; i++) {
                _data[i] = ScriptingRuntimeHelpers.CharToString(sequence[i]);
            }
        }

        public void __init__(CodeContext context, object sequence) {
            int len;
            try {
                if (!PythonOps.TryInvokeLengthHint(context, sequence, out len)) {
                    len = INITIAL_SIZE;
                }
            } catch (MissingMemberException) {
                len = INITIAL_SIZE;
            }

            _data = new object[len];
            _size = 0;
            extend_no_length_check(sequence);
        }

        public static object __new__(CodeContext/*!*/ context, PythonType cls) {
            if (cls == TypeCache.PythonList) {
                return new PythonList();
            }

            return cls.CreateInstance(context);
        }

        public static object __new__(CodeContext/*!*/ context, PythonType cls, object arg) {
            return __new__(context, cls);
        }

        public static object __new__(CodeContext/*!*/ context, PythonType cls, params object[] args\u00F8) {
            return __new__(context, cls);
        }

        public static object __new__(CodeContext/*!*/ context, PythonType cls, [ParamDictionary]IDictionary<object, object> kwArgs\u00F8, params object[] args\u00F8) {
            return __new__(context, cls);
        }

        private PythonList(IEnumerator e)
            : this(10) {
            while (e.MoveNext()) AddNoLock(e.Current);
        }

        internal PythonList(int capacity) {
            if (capacity == 0) {
                _data = ArrayUtils.EmptyObjects;
            } else {
                _data = new object[capacity];
            }
        }

        private PythonList(params object[] items) {
            _data = items; 
            _size = _data.Length; 
        }

        public PythonList()
            : this(0) {
        }

#if ALLOC_DEBUG
        private static int total, totalSize, cnt, growthCnt, growthSize;
        ~PythonList() {
            total += _data.Length;
            totalSize += _size;
            cnt++;

            Console.Error.WriteLine("PythonList: allocated {0} used {1} total wasted {2} - grand total wasted {3}", _data.Length, _size, total-totalSize, growthSize + total - totalSize);
            Console.Error.WriteLine("       Growing {0} {1} avg {2}", growthCnt, growthSize, growthSize / growthCnt);
        }
#endif

        internal PythonList(object sequence) {
            if (sequence is ICollection items) {
                _data = new object[items.Count];
                int i = 0;
                foreach (object item in items) {
                    _data[i++] = item;
                }
                _size = i;
            } else {
                if (!PythonOps.TryInvokeLengthHint(DefaultContext.Default, sequence, out int len)) {
                    len = INITIAL_SIZE;
                }

                _data = new object[len];
                extend_no_length_check(sequence);
            }
        }

        internal PythonList(ICollection items)
            : this(items.Count) {

            int i = 0;
            foreach (object item in items) {
                _data[i++] = item;
            }
            _size = i;
        }

        /// <summary>
        /// Creates a new list with the data in the array and a size
        /// the same as the length of the array.  The array is held
        /// onto and may be mutated in the future by the list.
        /// </summary>
        /// <param name="data">params array to use for lists storage</param>
        internal static PythonList FromArrayNoCopy(params object[] data) {
            return new PythonList(data);
        }

        internal object[] GetObjectArray() {
            lock (this) {
                return ArrayOps.CopyArray(_data, _size);
            }
        }

        #region binary operators

        public static PythonList operator +([NotNull]PythonList l1, [NotNull]PythonList l2) {
            object[] ret;
            int size;
            lock (l1) {
                ret = ArrayOps.CopyArray(l1._data, GetAddSize(l1._size, l2._size));
                size = l1._size;
            }

            lock (l2) {
                if (l2._size + size > ret.Length) {
                    ret = ArrayOps.CopyArray(ret, GetAddSize(size, l2._size));
                }
                Array.Copy(l2._data, 0, ret, size, l2._size);

                PythonList lret = new PythonList(ret);
                lret._size = size + l2._size;
                return lret;
            }
        }

        /// <summary>
        /// Gets a reasonable size for the addition of two arrays.  We round
        /// to a power of two so that we usually have some extra space if
        /// the resulting array gets added to.
        /// </summary>
        private static int GetAddSize(int s1, int s2) {
            int length = s1 + s2;

            return GetNewSize(length);
        }

        private static int GetNewSize(int length) {
            if (length > 256) {
                return length + (128 - 1) & ~(128 - 1);
            }

            return length + (16 - 1) & ~(16 - 1);
        }

        public static PythonList operator *([NotNull]PythonList l, int count) {
            return MultiplyWorker(l, count);
        }

        public static PythonList operator *(int count, PythonList l) {
            return MultiplyWorker(l, count);
        }

        public static object operator *([NotNull]PythonList self, [NotNull]Index count) {
            return PythonOps.MultiplySequence<PythonList>(MultiplyWorker, self, count, true);
        }

        public static object operator *([NotNull]Index count, [NotNull]PythonList self) {
            return PythonOps.MultiplySequence<PythonList>(MultiplyWorker, self, count, false);
        }

        public static object operator *([NotNull]PythonList self, object count) {
            int index;
            if (Converter.TryConvertToIndex(count, out index)) {
                return self * index;
            }

            throw PythonOps.TypeErrorForUnIndexableObject(count);
        }

        public static object operator *(object count, [NotNull]PythonList self) {
            int index;
            if (Converter.TryConvertToIndex(count, out index)) {
                return index * self;
            }

            throw PythonOps.TypeErrorForUnIndexableObject(count);
        }

        private static PythonList MultiplyWorker(PythonList self, int count) {
            if (count <= 0) return PythonOps.MakeEmptyList(0);

            int n, newCount;
            object[] ret;
            lock (self) {
                n = self._size;
                //??? is this useful optimization
                //???if (n == 1) return new PythonList(Array.ArrayList.Repeat(this[0], count));
                newCount = checked(n * count);
                ret = ArrayOps.CopyArray(self._data, newCount);                
            }

            // this should be extremely fast for large count as it uses the same algoithim as efficient integer powers
            // ??? need to test to see how large count and n need to be for this to be fastest approach
            int block = n;
            int pos = n;
            while (pos < newCount) {
                Array.Copy(ret, 0, ret, pos, Math.Min(block, newCount - pos));
                pos += block;
                block *= 2;
            }
            return new PythonList(ret);
        }

        #endregion

        public virtual int __len__() {
            return _size;
        }

        public virtual IEnumerator __iter__() {
            // return type is strongly typed to IEnumerator so that
            // we can call it w/o requiring an explicit conversion.  If the
            // user overrides this we'll place a conversion in the wrapper
            // helper
            return new ListIterator(this);
        }

        public virtual IEnumerator __reversed__() {
            return new ListReverseIterator(this);
        }

        public virtual bool __contains__(object value) {
            return ContainsWorker(value);
        }

        internal bool ContainsWorker(object value) {
            bool lockTaken = false;
            try {
                MonitorUtils.Enter(this, ref lockTaken);

                for (int i = 0; i < _size; i++) {
                    object thisIndex = _data[i];

                    // release the lock while we may call user code...
                    MonitorUtils.Exit(this, ref lockTaken);
                    try {
                        if (PythonOps.IsOrEqualsRetBool(thisIndex, value))
                            return true;
                    } finally {
                        MonitorUtils.Enter(this, ref lockTaken);
                    }
                }
            } finally {
                if (lockTaken) {
                    Monitor.Exit(this);
                }
            }
            return false;
        }

        #region ISequence Members

        internal void AddRange<T>(ICollection<T> otherList) {
            foreach (object o in otherList) append(o);
        }

        [SpecialName]
        public virtual object InPlaceAdd(object other) {
            if (!Object.ReferenceEquals(this, other)) {
                IEnumerator e = PythonOps.GetEnumerator(other);
                while (e.MoveNext()) {
                    append(e.Current);
                }
            } else {
                InPlaceMultiply(2);                
            }

            return this;
        }

        [SpecialName]
        public PythonList InPlaceMultiply(int count) {
            lock (this) {
                int n = _size;
                int newCount = checked(n * count);
                EnsureSize(newCount);

                int block = n;
                int pos = n;
                while (pos < newCount) {
                    Array.Copy(_data, 0, _data, pos, Math.Min(block, newCount - pos));
                    pos += block;
                    block *= 2;
                }
                _size = newCount;
            }
            return this;
        }

        [SpecialName]
        public object InPlaceMultiply(Index count) {
            return PythonOps.MultiplySequence<PythonList>(InPlaceMultiplyWorker, this, count, true);
        }

        [SpecialName]
        public object InPlaceMultiply(object count) {
            int index;
            if (Converter.TryConvertToIndex(count, out index)) {
                return InPlaceMultiply(index);
            }

            throw PythonOps.TypeErrorForUnIndexableObject(count);
        }

        private static PythonList InPlaceMultiplyWorker(PythonList self, int count) {
            return self.InPlaceMultiply(count);
        }

        internal object[] GetSliceAsArray(int start, int stop) {
            if (start < 0) start = 0;
            if (stop > Count) stop = Count;

            lock (this) return ArrayOps.GetSlice(_data, start, stop);
        }

        private static readonly object _boxedOne = ScriptingRuntimeHelpers.Int32ToObject(1);

        public virtual object this[Slice slice] {
            get {
                if (slice == null) throw PythonOps.TypeError("list indices must be integer or slice, not None");

                int start, stop, step;
                slice.indices(_size, out start, out stop, out step);

                if ((step > 0 && start >= stop) || (step < 0 && start <= stop)) return new PythonList();

                if (step == 1) {
                    object[] ret;
                    lock (this) ret = ArrayOps.GetSlice(_data, start, stop);
                    return new PythonList(ret);
                } else {
                    // start/stop/step could be near Int32.MaxValue, and simply addition could cause overflow
                    int n = (int)(step > 0 ? (0L + stop - start + step - 1) / step : (0L + stop - start + step + 1) / step);
                    object[] ret = new object[n];
                    lock (this) {
                        int ri = 0;
                        for (int i = 0, index = start; i < n; i++, index += step) {
                            ret[ri++] = _data[index];
                        }
                    }
                    return new PythonList(ret);

                }
            }
            set {
                if (slice == null) throw PythonOps.TypeError("list indices must be integer or slice, not None");

                if (slice.step != null && (!(slice.step is int) || !slice.step.Equals(_boxedOne))) {
                    // try to assign back to self: make a copy first
                    if (this == value) value = new PythonList(value);

                    if (ValueRequiresNoLocks(value)) {
                        // we don't need to worry about lock ordering of accesses to the 
                        // RHS & ourselves.  We can lock once and avoid repeatedly locking/unlocking
                        // on each assign.
                        lock (this) {
                            slice.DoSliceAssign(SliceAssignNoLock, _size, value);
                        }
                    } else {
                        slice.DoSliceAssign(SliceAssign, _size, value);
                    }

                } else {
                    int start, stop, step;
                    slice.indices(_size, out start, out stop, out step);

                    PythonList lstVal = value as PythonList;
                    if (lstVal != null) {
                        SliceNoStep(start, stop, lstVal);
                    } else {
                        SliceNoStep(start, stop, value);
                    }
                }
            }
        }

        private static bool ValueRequiresNoLocks(object value) {
            return value is PythonTuple || value is Array || value is FrozenSetCollection;
        }

        private void SliceNoStep(int start, int stop, PythonList other) {
            // We don't lock other here - instead we read it's object array
            // and size therefore having a stable view even if it resizes.
            // This means if we had a multithreaded app like:
            // 
            //  T1                   T2                     T3
            //  l1[:] = [1] * 100    l1[:] = [2] * 100      l3[:] = l1[:]
            //
            // we can end up with both 1s and 2s in the array.  This is the
            // same as if our set was implemented on top of get/set item where
            // we'd take and release the locks repeatedly.
            int otherSize = other._size;
            object[] otherData = other._data;
            
            lock (this) {
                if ((stop - start) == otherSize) {
                    // we are simply replacing values, this is fast...
                    for (int i = 0; i < otherSize; i++) {
                        _data[i + start] = otherData[i];
                    }
                } else {
                    // we are resizing the array (either bigger or smaller), we 
                    // will copy the data array and replace it all at once.
                    stop = Math.Max(stop, start);
                    int newSize = _size - (stop - start) + otherSize;

                    object[] newData = new object[GetNewSize(newSize)];
                    for (int i = 0; i < start; i++) {
                        newData[i] = _data[i];
                    }

                    for (int i = 0; i < otherSize; i++) {
                        newData[i + start] = otherData[i];
                    }

                    int writeOffset = otherSize - (stop - start);
                    for (int i = stop; i < _size; i++) {
                        newData[i + writeOffset] = _data[i];
                    }

                    _size = newSize;
                    _data = newData;
                }
            }
        }

        private void SliceNoStep(int start, int stop, object value) {
            // always copy from a List object, even if it's a copy of some user defined enumerator.  This
            // makes it easy to hold the lock for the duration fo the copy.
            IList<object> other = value as IList<object> ?? new PythonList(PythonOps.GetEnumerator(value));

            lock (this) {
                if ((stop - start) == other.Count) {
                    // we are simply replacing values, this is fast...
                    for (int i = 0; i < other.Count; i++) {
                        _data[i + start] = other[i];
                    }
                } else {
                    // we are resizing the array (either bigger or smaller), we 
                    // will copy the data array and replace it all at once.
                    stop = Math.Max(stop, start);
                    int newSize = _size - (stop - start) + other.Count;

                    object[] newData = new object[GetNewSize(newSize)];
                    for (int i = 0; i < start; i++) {
                        newData[i] = _data[i];
                    }
                    
                    for (int i = 0; i < other.Count; i++) {
                        newData[i + start] = other[i];
                    }

                    int writeOffset = other.Count - (stop - start);
                    for (int i = stop; i < _size; i++) {
                        newData[i + writeOffset] = _data[i];
                    }

                    _size = newSize;
                    _data = newData;
                }
            }        
        }

        private void SliceAssign(int index, object value) {
            this[index] = value;
        }

        private void SliceAssignNoLock(int index, object value) {
            _data[index] = value;
        }

        public virtual void __delitem__(int index) {
            lock (this) RawDelete(PythonOps.FixIndex(index, _size));
        }

        public virtual void __delitem__(object index) {
            __delitem__(Converter.ConvertToIndex(index));
        }

        public void __delitem__(Slice slice) {
            if (slice == null) throw PythonOps.TypeError("list indices must be integers or slices");

            lock (this) {
                int start, stop, step;
                // slice is sealed, indices can't be user code...
                slice.indices(_size, out start, out stop, out step);

                if (step > 0 && (start >= stop)) return;
                if (step < 0 && (start <= stop)) return;

                if (step == 1) {
                    int i = start;
                    for (int j = stop; j < _size; j++, i++) {
                        _data[i] = _data[j];
                    }
                    _size -= stop - start;
                    return;
                }
                if (step == -1) {
                    int i = stop + 1;
                    for (int j = start + 1; j < _size; j++, i++) {
                        _data[i] = _data[j];
                    }
                    _size -= start - stop;
                    return;
                }

                if (step < 0) {
                    // find "start" we will skip in the 1,2,3,... order
                    int i = start;
                    while (i > stop) {
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

                while (curr < stop && move < stop) {
                    if (move != skip) {
                        _data[curr++] = _data[move];
                    } else
                        skip += step;
                    move++;
                }
                while (stop < _size) {
                    _data[curr++] = _data[stop++];
                }
                _size = curr;
            }
        }

        #endregion

        private void RawDelete(int index) {
            int len = _size - 1;
            _size = len;
            object[] tempData = _data;
            for (int i = index; i < len; i++) {
                tempData[i] = tempData[i + 1];
            }
            tempData[len] = null;
        }


        internal void EnsureSize(int needed) {
            if (_data.Length >= needed) return;

            if (_data.Length == 0) {
                // free growth, we wasted nothing
                _data = new object[4];
                return;
            }

            int newSize = Math.Max(_size * 3, 10);
            while (newSize < needed) newSize *= 2;
#if ALLOC_DEBUG
            growthCnt++;
            growthSize += _size;
            Console.Error.WriteLine("Growing {3} {0} {1} avg {2}", growthCnt, growthSize, growthSize/growthCnt, newSize - _size);
#endif
            _data = ArrayOps.CopyArray(_data, newSize);
        }

        public void append(object item) {
            lock (this) {
                AddNoLock(item);
            }
        }

        /// <summary>
        /// Non-thread safe adder, should only be used by internal callers that
        /// haven't yet exposed their list.
        /// </summary>
        internal void AddNoLock(object item) {
            EnsureSize(_size + 1);
            _data[_size] = item;
            _size += 1;
        }

        internal void AddNoLockNoDups(object item) {
            for (int i = 0; i < _size; i++) {
                if (PythonOps.IsOrEqualsRetBool(_data[i], item)) {
                    return;
                }
            }

            AddNoLock(item);
        }

        internal void AppendListNoLockNoDups(PythonList list) {
            if (list != null) {
                foreach (object item in list) {
                    AddNoLockNoDups(item);
                }
            }
        }

        public void clear() {
            Clear();
        }

        public int count(object item) {
            bool lockTaken = false;
            try {
                MonitorUtils.Enter(this, ref lockTaken);
                int cnt = 0;
                for (int i = 0, len = _size; i < len; i++) {
                    object val = _data[i];

                    MonitorUtils.Exit(this, ref lockTaken);
                    try {
                        if (PythonOps.IsOrEqualsRetBool(val, item)) cnt++;
                    } finally {
                        MonitorUtils.Enter(this, ref lockTaken);
                    }
                }
                return cnt;
            } finally {
                if (lockTaken) {
                    Monitor.Exit(this);
                }
            }
        }

        public void extend([NotNull]PythonList/*!*/ seq) {
            using(new OrderedLocker(this, seq)) {
                // use the original count for if we're extending this w/ this
                int count = seq.Count;  
                EnsureSize(Count + count);

                for (int i = 0; i < count; i++) {
                    AddNoLock(seq[i]);
                }
            }
        }

        public void extend([NotNull]PythonTuple/*!*/ seq) {
            lock (this) {
                EnsureSize(Count + seq.Count);

                for (int i = 0; i < seq.Count; i++) {
                    AddNoLock(seq[i]);
                }
            }
        }

        public void extend(object seq) {
            if (PythonOps.TryInvokeLengthHint(DefaultContext.Default, seq, out int len)) {
                EnsureSize(len);
            }

            extend_no_length_check(seq);
        }

        [PythonHidden]
        private void extend_no_length_check(object seq) {
            IEnumerator i = PythonOps.GetEnumerator(seq);
            if (seq == (object)this) {
                PythonList other = new PythonList(i);
                i = ((IEnumerable)other).GetEnumerator();
            }
            while (i.MoveNext()) append(i.Current);
        }

        public int index(object item, int start = 0) {
            return index(item, start, _size);
        }

        public int index(object item, int start, int stop) {
            // CPython behavior for index is to only look at the 
            // original items.  If new items are added they
            // are ignored, but if items are removed they
            // aren't iterated.  We therefore get a stable view
            // of our data, and then go with the minimum between
            // our starting size and ending size.

            object[] locData;
            int locSize;
            lock (this) {
                // get a stable view on size / data...
                locData = _data;
                locSize = _size;
            }

            start = PythonOps.FixSliceIndex(start, locSize);
            stop = PythonOps.FixSliceIndex(stop, locSize);

            for (int i = start; i < Math.Min(stop, Math.Min(locSize, _size)); i++) {
                if (PythonOps.IsOrEqualsRetBool(locData[i], item)) return i;
            }

            throw PythonOps.ValueError("list.index(item): item not in list");
        }

        public int index(object item, object start) {
            return index(item, Converter.ConvertToIndex(start), _size);
        }

        public int index(object item, object start, object stop) {
            return index(item, Converter.ConvertToIndex(start), Converter.ConvertToIndex(stop));
        }

        public void insert(int index, object value) {
            if (index >= _size) {
                append(value);
                return;
            }

            lock (this) {
                index = PythonOps.FixSliceIndex(index, _size);

                EnsureSize(_size + 1);
                _size += 1;
                for (int i = _size - 1; i > index; i--) {
                    _data[i] = _data[i - 1];
                }
                _data[index] = value;
            }
        }

        [PythonHidden]
        public void Insert(int index, object value) {
            insert(index, value);
        }

        public object pop() {
            if (_size == 0) throw PythonOps.IndexError("pop off of empty list");

            lock (this) {
                _size -= 1;
                return _data[_size];
            }
        }

        public object pop(int index) {
            lock (this) {
                index = PythonOps.FixIndex(index, _size);
                if (_size == 0) throw PythonOps.IndexError("pop off of empty list");

                object ret = _data[index];
                _size -= 1;
                for (int i = index; i < _size; i++) {
                    _data[i] = _data[i + 1];
                }
                return ret;
            }
        }

        public void remove(object value) {
            lock (this) RawDelete(index(value));
        }

        void IList.Remove(object value) {
            remove(value);
        }

        public void reverse() {
            lock (this) Array.Reverse(_data, 0, _size);
        }

        internal void reverse(int index, int count) {
            lock (this) Array.Reverse(_data, index, count);
        }

        public void sort(CodeContext/*!*/ context, 
                         object key=null,
                         bool reverse=false) {
            // the empty list is already sorted
            if (_size != 0) {                
                IComparer comparer = context.LanguageContext.GetComparer(
                    null,
                    GetComparisonType());

                DoSort(context, comparer, key, reverse, 0, _size);
            }
        }

        private Type GetComparisonType() {
            if (_size >= 4000) {
                // we're big, we can afford a custom comparison call site.
                return null;
            }

            if (_data.Length > 0) {
                // use the 1st index to determine the type - we're assuming lists are
                // homogeneous
                return CompilerHelpers.GetType(_data[0]);
            }

            return typeof(object);
        }

        private void DoSort(CodeContext/*!*/ context, IComparer cmp, object key, bool reverse, int index, int count) {
            lock (this) {
                object[] sortData = _data;
                int sortSize = _size;

                try {
                    // make the list appear empty for the duration of the sort...
                    _data = ArrayUtils.EmptyObjects;
                    _size = 0;

                    if (key != null) {
                        object[] keys = new object[sortSize];
                        for (int i = 0; i < sortSize; i++) {
                            Debug.Assert(_data.Length == 0);
                            keys[i] = PythonCalls.Call(context, key, sortData[i]);
                            if (_data.Length != 0) throw PythonOps.ValueError("list mutated while determining keys");
                        }

                        sortData = ListMergeSort(sortData, keys, cmp, index, count, reverse);
                    } else {
                        sortData = ListMergeSort(sortData, null, cmp, index, count, reverse);
                    }
                } finally {
                    // restore the list to it's old data & size (which is now supported appropriately)
                    _data = sortData;
                    _size = sortSize;
                }

            }
        }

        internal object[] ListMergeSort(object[] sortData, object[] keys, IComparer cmp, int index, int count, bool reverse) {
            if (count - index < 2) return sortData;  // 1 or less items, we're sorted, quit now...

            if (keys == null) keys = sortData;
            // list merge sort - stable sort w/ a minimum # of comparisons.

            int len = count - index;
            // prepare the two lists.
            int[] lists = new int[len + 2];    //0 and count + 1 are auxiliary fields

            lists[0] = 1;
            lists[len + 1] = 2;
            for (int i = 1; i <= len - 2; i++) {
                lists[i] = -(i + 2);
            }

            lists[len - 1] = lists[len] = 0;

            // new pass
            for (; ; ) {
                // p & q  traverse the lists during each pass.  
                //  s is usually the most most recently processed record of the current sublist
                //  t points to the end of the previously output sublist
                int s = 0;
                int t = len + 1;
                int p = lists[s];
                int q = lists[t];

                if (q == 0) break;  // we're done
                for (; ; ) {
                    // Indexes into the array here are 1 based.  0 is a 
                    // virtual element and so is (len - 1) - they only exist in
                    // the length array.

                    if ((p < 1) || (q <= len && DoCompare(keys, cmp, p + index - 1, q + index - 1, reverse))) {
                        // advance p
                        if (lists[s] < 0) lists[s] = Math.Abs(p) * -1;
                        else lists[s] = Math.Abs(p);

                        s = p;
                        p = lists[p];

                        if (p > 0) continue;

                        // complete the sublist
                        lists[s] = q;
                        s = t;
                        do {
                            t = q;
                            q = lists[q];
                        } while (q > 0);
                    } else {
                        // advance q
                        if (lists[s] < 0) lists[s] = Math.Abs(q) * -1;
                        else lists[s] = Math.Abs(q);

                        s = q;
                        q = lists[q];

                        if (q > 0) continue;

                        // Complete the sublist
                        lists[s] = p;
                        s = t;

                        do {
                            t = p;
                            p = lists[p];
                        } while (p > 0);
                    }

                    Debug.Assert(p <= 0);
                    Debug.Assert(q <= 0);
                    p *= -1;
                    q *= -1;

                    if (q == 0) {
                        if (lists[s] < 0) lists[s] = Math.Abs(p) * -1;
                        else lists[s] = Math.Abs(p);
                        lists[t] = 0;
                        // go back to new pass
                        break;
                    } // else keep going
                }
            }

            // use the resulting indices to
            // extract the order.
            object[] newData = new object[len];
            int start = lists[0];
            int outIndex = 0;
            while (start != 0) {
                newData[outIndex++] = sortData[start + index - 1];
                start = lists[start];
            }

            if (sortData.Length != count || index != 0) {
                for (int j = 0; j < count; j++) {
                    sortData[j + index] = newData[j];
                }
            } else {
                sortData = newData;
            }

            return sortData;
        }

        /// <summary>
        /// Compares the two specified keys
        /// </summary>
        private bool DoCompare(object[] keys, IComparer cmp, int p, int q, bool reverse) {
            Debug.Assert(_data.Length == 0);

            int result = cmp.Compare(keys[p], keys[q]);
            bool ret = reverse ? (result >= 0) : (result <= 0);

            if (_data.Length != 0) throw PythonOps.ValueError("list mutated during sort");
            return ret;
        }

        internal int BinarySearch(int index, int count, object value, IComparer comparer) {
            lock (this) return Array.BinarySearch(_data, index, count, value, comparer);
        }

        internal bool EqualsWorker(PythonList l, IEqualityComparer comparer) {
            using (new OrderedLocker(this, l)) {
                if (comparer == null) {
                    return PythonOps.ArraysEqual(_data, _size, l._data, l._size);
                } else {
                    return PythonOps.ArraysEqual(_data, _size, l._data, l._size, comparer);
                }
            }
        }

        internal int CompareToWorker(PythonList l) {
            return CompareToWorker(l, null);
        }

        internal int CompareToWorker(PythonList l, IComparer comparer) {
            using (new OrderedLocker(this, l)) {
                if (comparer == null) {
                    return PythonOps.CompareArrays(_data, _size, l._data, l._size);
                } else {
                    return PythonOps.CompareArrays(_data, _size, l._data, l._size, comparer);
                }
            }
        }

        internal bool FastSwap(int i, int j) {
            // ensure i <= j
            if (i > j) {
                int tmp = i;
                i = j;
                j = tmp;
            }

            // bounds checking
            if (i < 0 || j >= _size) {
                return false;
            } else if (i == j) {
                return true;
            }

            object temp = _data[i];
            _data[i] = _data[j];
            _data[j] = temp;
            return true;
        }

        public PythonList copy() {
            return new PythonList(this);
        }

        #region IList Members

        bool IList.IsReadOnly {
            get { return false; }
        }

        public virtual object this[int index] {
            get {
                // no locks works here, we either return an
                // old item (as if we were called first) or return
                // a current item...        

                // force reading the array first, _size can change after
                object[] data = GetData();  

                return data[PythonOps.FixIndex(index, _size)];
            }
            set {
                // but we need a lock here incase we're assigning
                // while re-sizing.
                lock (this) _data[PythonOps.FixIndex(index, _size)] = value;
            }
        }

        public virtual object this[BigInteger index] {
            get {
                return this[(int)index];
            }
            set {
                this[(int)index] = value;
            }
        }

        /// <summary>
        /// Supports __index__ on arbitrary types, also prevents __float__
        /// </summary>
        public virtual object this[object index] {
            get {
                return this[Converter.ConvertToIndex(index)];
            }
            set {
                this[Converter.ConvertToIndex(index)] = value;
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private object[] GetData() {
            return _data;
        }

        [PythonHidden]
        public void RemoveAt(int index) {
            lock (this) RawDelete(index);
        }

        [PythonHidden]
        public bool Contains(object value) {
            return __contains__(value);
        }

        [PythonHidden]
        public void Clear() {
            lock (this) _size = 0;
        }

        [PythonHidden]
        public int IndexOf(object value) {
            // we get a stable view of the list, and if user code
            // clears it then we'll stop iterating.
            object[] locData;
            int locSize;
            lock (this) {
                locData = _data;
                locSize = _size;
            }

            for (int i = 0; i < Math.Min(locSize, _size); i++) {
                if (PythonOps.IsOrEqualsRetBool(locData[i], value)) return i;
            }
            return -1;
        }

        [PythonHidden]
        public int Add(object value) {
            lock (this) {
                AddNoLock(value);
                return _size - 1;
            }
        }

        bool IList.IsFixedSize {
            get { return false; }
        }

        #endregion

        #region ICollection Members

        bool ICollection.IsSynchronized {
            get { return false; }
        }

        public int Count {
            [PythonHidden]
            get { return _size; }
        }

        [PythonHidden]
        public void CopyTo(Array array, int index) {
            Array.Copy(_data, 0, array, index, _size);
        }

        internal void CopyTo(Array array, int index, int arrayIndex, int count) {
            Array.Copy(_data, index, array, arrayIndex, count);
        }

        object ICollection.SyncRoot {
            get {
                return this;
            }
        }

        #endregion

        #region IEnumerable Members

        [PythonHidden]
        public IEnumerator GetEnumerator() {
            return __iter__();
        }

        #endregion

        #region ICodeFormattable Members

        public virtual string/*!*/ __repr__(CodeContext/*!*/ context) {
            List<object> infinite = PythonOps.GetAndCheckInfinite(this);
            if (infinite == null) {
                return "[...]";
            }

            int index = infinite.Count;
            infinite.Add(this);
            try {
                StringBuilder buf = new StringBuilder();
                buf.Append("[");
                for (int i = 0; i < _size; i++) {
                    if (i > 0) buf.Append(", ");
                    buf.Append(PythonOps.Repr(context, _data[i]));
                }
                buf.Append("]");
                return buf.ToString();
            } finally {
                System.Diagnostics.Debug.Assert(index == infinite.Count - 1);
                infinite.RemoveAt(index);
            }
        }

        #endregion

        #region IStructuralEquatable Members

        public const object __hash__ = null;

        int IStructuralEquatable.GetHashCode(IEqualityComparer comparer) {
            if (CompareUtil.Check(this)) {
                return 0;
            }

            int res; 
            CompareUtil.Push(this);
            try {
                res = ((IStructuralEquatable)new PythonTuple(this)).GetHashCode(comparer);
            } finally {
                CompareUtil.Pop(this);
            }

            return res;
        }

        bool IStructuralEquatable.Equals(object other, IEqualityComparer comparer) {
            if (Object.ReferenceEquals(this, other)) return true;

            if (other is PythonList l && l.Count == Count)
                return Equals(l, comparer);
            return false;
        }

        #endregion

        #region ICollection<object> Members

        void ICollection<object>.Add(object item) {
            append(item);
        }

        public void CopyTo(object[] array, int arrayIndex) {
            for (int i = 0; i < Count; i++) {
                array[arrayIndex + i] = this[i];
            }
        }

        bool ICollection<object>.IsReadOnly {
            get { return ((IList)this).IsReadOnly; }
        }

        [PythonHidden]
        public bool Remove(object item) {
            if (__contains__(item)) {
                remove(item);
                return true;
            }
            return false;
        }

        #endregion

        #region IEnumerable<object> Members

        IEnumerator<object> IEnumerable<object>.GetEnumerator() {
            return new IEnumeratorOfTWrapper<object>(((IEnumerable)this).GetEnumerator());
        }

        #endregion

        private bool Equals(PythonList other) {
            return Equals(other, null);
        }

        private bool Equals(PythonList other, IEqualityComparer comparer) {
            CompareUtil.Push(this, other);
            try {
                return EqualsWorker(other, comparer);
            } finally {
                CompareUtil.Pop(this, other);
            }
        }

        internal int CompareTo(PythonList other) {
            return CompareTo(other, null);
        }

        internal int CompareTo(PythonList other, IComparer comparer) {
            CompareUtil.Push(this, other);
            try {
                return CompareToWorker(other, comparer);
            } finally {
                CompareUtil.Pop(this, other);
            }
        }

        #region Rich Comparison Members

        [return: MaybeNotImplemented]
        public static object operator > (PythonList self, object other) {
            PythonList l = other as PythonList;
            if (l == null) return NotImplementedType.Value;

            return self.CompareTo(l) > 0 ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
        }

        [return: MaybeNotImplemented]
        public static object operator <(PythonList self, object other) {
            PythonList l = other as PythonList;
            if (l == null) return NotImplementedType.Value;

            return self.CompareTo(l) < 0 ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
        }

        [return: MaybeNotImplemented]
        public static object operator >=(PythonList self, object other) {
            PythonList l = other as PythonList;
            if (l == null) return NotImplementedType.Value;

            return self.CompareTo(l) >= 0 ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
        }

        [return: MaybeNotImplemented]
        public static object operator <=(PythonList self, object other) {
            PythonList l = other as PythonList;
            if (l == null) return NotImplementedType.Value;

            return self.CompareTo(l) <= 0 ? ScriptingRuntimeHelpers.True : ScriptingRuntimeHelpers.False;
        }

        #endregion

        #region IStructuralComparable Members

        int IStructuralComparable.CompareTo(object other, IComparer comparer) {
            PythonList l = other as PythonList;
            if (l == null) {
                throw new ValueErrorException("expected List");
            }

            return CompareTo(l, comparer);
        }

        #endregion
    }

    [PythonType("list_iterator")]
    public sealed class ListIterator : IEnumerator, IEnumerable, IEnumerable<object>, IEnumerator<object> {
        private int _index;
        private readonly PythonList _list;
        private bool _iterating;

        public ListIterator(PythonList l) {
            _list = l;
            Reset();
        }

        #region Pickling

        public object __reduce__(CodeContext context) {
            object iter;
            context.TryLookupBuiltin("iter", out iter);
            if (_iterating) {
                return PythonTuple.MakeTuple(iter, PythonTuple.MakeTuple(_list), _index + 1);
            }
            return PythonTuple.MakeTuple(iter, PythonTuple.MakeTuple(new PythonList()));
        }

        public void __setstate__(int state) {
            _index = state - 1;
            _iterating = _index < _list._size;
        }

        #endregion

        #region IEnumerator Members

        public void Reset() {
            _index = -1;
            _iterating = true;
        }

        public object Current {
            get {
                return _list._data[_index];
            }
        }

        public bool MoveNext() {
            if (_iterating) {
                _index++;
                _iterating = (_index < _list._size);
            }
            return _iterating;
        }

        #endregion

        #region IEnumerable Members

        public IEnumerator GetEnumerator() {
            return this;
        }

        #endregion

        #region IDisposable Members

        public void Dispose() {
        }

        #endregion

        #region IEnumerable<object> Members

        IEnumerator<object> IEnumerable<object>.GetEnumerator() {
            return this;
        }

        #endregion

        public int __length_hint__() {
            return _iterating ? _list._size - _index - 1 : 0;
        }
    }

    [PythonType("list_reverseiterator")]
    public sealed class ListReverseIterator : IEnumerator, IEnumerable, IEnumerable<object>, IEnumerator<object> {
        private int _index;
        private readonly PythonList _list;
        private bool _iterating;

        public ListReverseIterator(PythonList l) {
            _list = l;
            Reset();
        }

        #region Pickling

        public object __reduce__(CodeContext context) {
            if (_iterating) {
                return PythonTuple.MakeTuple(Modules.Builtin.reversed, PythonTuple.MakeTuple(_list), _list._size - _index - 1);
            }
            object iter;
            context.TryLookupBuiltin("iter", out iter);
            return PythonTuple.MakeTuple(iter, PythonTuple.MakeTuple(new PythonList()));
        }

        public void __setstate__(int state) {
            _index = _list._size - state - 1;
            _iterating = _index <= _list._size;
        }

        #endregion

        #region IEnumerator Members

        public void Reset() {
            _index = 0;
            _iterating = true;
        }

        public object Current {
            get {
                return _list._data[_list._size - _index];
            }
        }

        public bool MoveNext() {
            if (_iterating) {
                _index++;
                _iterating = (_index <= _list._size);
            }
            return _iterating;
        }

        #endregion

        #region IEnumerable Members

        public IEnumerator GetEnumerator() {
            return this;
        }

        #endregion

        #region IDisposable Members

        public void Dispose() {
        }
        
        #endregion

        #region IEnumerable<object> Members

        IEnumerator<object> IEnumerable<object>.GetEnumerator() {
            return this;
        }

        #endregion

        public int __length_hint__() {
            return _iterating ? _list._size - _index : 0;
        }
    }

    /// <summary>
    /// we need to lock both objects (or copy all of one's data w/ it's lock held, and
    /// then compare, which is bad).  Therefore we have a strong order for locking on 
    /// the two objects based upon the hash code or object identity in case of a collision
    /// </summary>
    public struct OrderedLocker : IDisposable {
        private readonly object _one, _two;
        private bool _oneLocked, _twoLocked;

        public OrderedLocker(object/*!*/ one, object/*!*/ two) {
            Debug.Assert(one != null); Debug.Assert(two != null);

            _one = one;
            _two = two;
            _oneLocked = false;
            _twoLocked = false;

            if (one == two) {
                try { } finally {
                    MonitorUtils.Enter(one, ref _oneLocked);
                }
                return;
            }

            int hc1 = ReferenceEqualityComparer<object>.Instance.GetHashCode(_one);
            int hc2 = ReferenceEqualityComparer<object>.Instance.GetHashCode(_two);

            if (hc1 < hc2) {
                MonitorUtils.Enter(_one, ref _oneLocked);
                MonitorUtils.Enter(_two, ref _twoLocked);
            } else if (hc1 != hc2) {
                MonitorUtils.Enter(_two, ref _twoLocked);
                MonitorUtils.Enter(_one, ref _oneLocked);
            } else {
                // rare, but possible.  We need a second opinion
                if (IdDispenser.GetId(_one) < IdDispenser.GetId(_two)) {
                    MonitorUtils.Enter(_one, ref _oneLocked);
                    MonitorUtils.Enter(_two, ref _twoLocked);
                } else {
                    MonitorUtils.Enter(_two, ref _twoLocked);
                    MonitorUtils.Enter(_one, ref _oneLocked);
                }
            }
        }

        #region IDisposable Members

        public void Dispose() {
            MonitorUtils.Exit(_one, ref _oneLocked);
            if (_one != _two) {
                MonitorUtils.Exit(_two, ref _twoLocked);
            }
        }

        #endregion
    }
}
