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
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Binding;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

#if FEATURE_NUMERICS
using System.Numerics;
#else
using Microsoft.Scripting.Math;
#endif

using SpecialNameAttribute = System.Runtime.CompilerServices.SpecialNameAttribute;

[assembly: PythonModule("_collections", typeof(IronPython.Modules.PythonCollections))]
namespace IronPython.Modules {
    public class PythonCollections {
        public const string __doc__ = "High performance data structures\n";

        [PythonType]
        [DontMapIEnumerableToContains, DebuggerDisplay("deque, {__len__()} items"), DebuggerTypeProxy(typeof(CollectionDebugProxy))]
        public class deque : IEnumerable, IComparable, ICodeFormattable, IStructuralEquatable, IStructuralComparable, ICollection, IReversible
#if CLR2
            , IValueEquality
#endif
        {
            private object[] _data;
            private object _lockObj = new object();
            private int _head, _tail;
            private int _itemCnt, _maxLen, _version;

            public deque() {
                _maxLen = -1;
                clear();
            }

            // extra overloads just so that __init__ and __new__ are compatible and __new__ can accept any arguments
            public deque(object iterable)
                : this() {
            }

            public deque(object iterable, object maxLen)
                : this() {
            }

            public deque(params object[] args)
                : this() {
            }

            public deque([ParamDictionary]IDictionary<object, object> dict, params object[] args)
                : this() {
            }

            private deque(int maxLen) {
                // internal private constructor accepts maxlen < 0
                _maxLen = maxLen;
                clear();
            }

            public void __init__() {
                _maxLen = -1;
                clear();
            }

            public void __init__([ParamDictionary]IDictionary<object, object> dict) {
                _maxLen = VerifyMaxLen(dict);
                clear();
            }

            public void __init__(object iterable) {
                _maxLen = -1;
                clear();
                extend(iterable);
            }

            public void __init__(object iterable, object maxLen) {
                _maxLen = VerifyMaxLenValue(maxLen);
                
                clear();
                extend(iterable);
            }

            public void __init__(object iterable, [ParamDictionary]IDictionary<object, object> dict) {
                if (VerifyMaxLen(dict) < 0) {
                    __init__(iterable);
                } else {
                    __init__(iterable, VerifyMaxLen(dict));
                }
            }

            private static int VerifyMaxLen(IDictionary<object, object> dict) {
                if (dict.Count != 1) {
                    throw PythonOps.TypeError("deque() takes at most 1 keyword argument ({0} given)", dict.Count);
                }
                
                object value;
                if (!dict.TryGetValue("maxlen", out value)) {
                    IEnumerator<object> e = dict.Keys.GetEnumerator();
                    if (e.MoveNext()) {
                        throw PythonOps.TypeError("deque(): '{0}' is an invalid keyword argument", e.Current);
                    }
                }

                return VerifyMaxLenValue(value);
            }

            private static int VerifyMaxLenValue(object value) {
                if (value == null) {
                    return -1;
                } else if (value is int || value is BigInteger || value is double) {
                    int val = (int)value;
                    if (val < 0) throw PythonOps.ValueError("maxlen must be non-negative");
                    return val;
                } else if (value is Extensible<int>) {
                    int val = ((Extensible<int>)value).Value;
                    if (val < 0) throw PythonOps.ValueError("maxlen must be non-negative");
                    return val;
                }
                throw PythonOps.TypeError("deque(): keyword argument 'maxlen' requires integer");
            }

            #region core deque APIs

            public void append(object x) {
                lock (_lockObj) {
                    _version++;

                    // overwrite head if queue is at max length
                    if (_itemCnt == _maxLen) {
                        if (_maxLen == 0) {
                            return;
                        }
                        _data[_tail++] = x;
                        if (_tail == _data.Length) {
                            _tail = 0;
                        }
                        _head = _tail;
                        return;
                    }

                    if (_itemCnt == _data.Length) {
                        GrowArray();
                    }

                    _itemCnt++;
                    _data[_tail++] = x;
                    if (_tail == _data.Length) {
                        _tail = 0;
                    }
                }
            }

            public void appendleft(object x) {
                lock (_lockObj) {
                    _version++;

                    // overwrite tail if queue is full
                    if (_itemCnt == _maxLen) {
                        _head--;
                        if (_head < 0) {
                            _head = _data.Length - 1;
                        }
                        _tail = _head;
                        _data[_head] = x;
                        return;
                    }

                    if (_itemCnt == _data.Length) {
                        GrowArray();
                    }

                    _itemCnt++;
                    --_head;
                    if (_head < 0) {
                        _head = _data.Length - 1;
                    }

                    _data[_head] = x;
                }
            }

            public void clear() {
                lock (_lockObj) {
                    _version++;

                    _head = _tail = 0;
                    _itemCnt = 0;
                    if (_maxLen < 0) _data = new object[8];
                    else _data = new object[Math.Min(_maxLen, 8)];
                }
            }

            public void extend(object iterable) {
                IEnumerator e = PythonOps.GetEnumerator(iterable);
                while (e.MoveNext()) {
                    append(e.Current);
                }
            }

            public void extendleft(object iterable) {
                IEnumerator e = PythonOps.GetEnumerator(iterable);
                while (e.MoveNext()) {
                    appendleft(e.Current);
                }
            }

            public object pop() {
                lock (_lockObj) {
                    if (_itemCnt == 0) {
                        throw PythonOps.IndexError("pop from an empty deque");
                    }

                    _version++;
                    if (_tail != 0) {
                        _tail--;
                    } else {
                        _tail = _data.Length - 1;
                    }
                    _itemCnt--;

                    object res = _data[_tail];
                    _data[_tail] = null;
                    return res;
                }
            }

            public object popleft() {
                lock (_lockObj) {
                    if (_itemCnt == 0) {
                        throw PythonOps.IndexError("pop from an empty deque");
                    }

                    _version++;
                    object res = _data[_head];
                    _data[_head] = null;

                    if (_head != _data.Length - 1) {
                        _head++;
                    } else {
                        _head = 0;
                    }
                    _itemCnt--;
                    return res;
                }
            }

            public void remove(object value) {
                lock (_lockObj) {
                    int found = -1;
                    int startVersion = _version;
                    WalkDeque(delegate(int index) {
                        if (PythonOps.EqualRetBool(_data[index], value)) {
                            found = index;
                            return false;
                        }
                        return true;
                    });
                    if (_version != startVersion) {
                        throw PythonOps.IndexError("deque mutated during remove().");
                    }

                    if (found == _head) {
                        popleft();
                    } else if (found == (_tail > 0 ? _tail - 1 : _data.Length - 1)) {
                        pop();
                    } else if (found == -1) {
                        throw PythonOps.ValueError("deque.remove(value): value not in deque");
                    } else {
                        // otherwise we're removing from the middle and need to slide the values over...
                        _version++;

                        int start;
                        if (_head >= _tail) {
                            start = 0;
                        } else {
                            start = _head;
                        }

                        bool finished = false;
                        object copying = _tail != 0 ? _data[_tail - 1] : _data[_data.Length - 1];
                        for (int i = _tail - 2; i >= start; i--) {
                            object tmp = _data[i];
                            _data[i] = copying;
                            if (i == found) {
                                finished = true;
                                break;
                            }
                            copying = tmp;
                        }
                        if (_head >= _tail && !finished) {
                            for (int i = _data.Length - 1; i >= _head; i--) {
                                object tmp = _data[i];
                                _data[i] = copying;
                                if (i == found) break;
                                copying = tmp;
                            }
                        }

                        // we're one smaller now
                        _tail--;
                        _itemCnt--;
                        if (_tail < 0) {
                            // and tail just wrapped to the beginning
                            _tail = _data.Length - 1;
                        }
                    }
                }
            }

            public void rotate(CodeContext/*!*/ context) {
                rotate(context, 1);
            }

            public void rotate(CodeContext/*!*/ context, object n) {
                lock (_lockObj) {
                    // rotation is easy if we have no items!
                    if (_itemCnt == 0) return;

                    // set rot to the appropriate positive int
                    int rot = PythonContext.GetContext(context).ConvertToInt32(n) % _itemCnt;
                    rot = rot % _itemCnt;
                    if (rot == 0) return; // no need to rotate if we'll end back up where we started
                    if (rot < 0) rot += _itemCnt;

                    _version++;
                    if (_itemCnt == _data.Length) {
                        // if all indices are filled no moves are required
                        _head = _tail = (_tail - rot + _data.Length) % _data.Length;
                    } else {
                        // too bad, we got gaps, looks like we'll be doing some real work.
                        object[] newData = new object[_itemCnt]; // we re-size to itemCnt so that future rotates don't require work
                        int curWriteIndex = rot;
                        WalkDeque(delegate(int curIndex) {
                            newData[curWriteIndex] = _data[curIndex];
                            curWriteIndex = (curWriteIndex + 1) % _itemCnt;
                            return true;
                        });
                        _head = _tail = 0;
                        _data = newData;
                    }
                }
            }

            public object this[CodeContext/*!*/ context, object index] {
                get {
                    lock (_lockObj) {
                        return _data[IndexToSlot(context, index)];
                    }
                }
                set {
                    lock (_lockObj) {
                        _version++;
                        _data[IndexToSlot(context, index)] = value;
                    }
                }
            }
            #endregion

            public object __copy__(CodeContext/*!*/ context) {
                if (GetType() == typeof(deque)) {
                    deque res = new deque(_maxLen);
                    res.extend(((IEnumerable)this).GetEnumerator());
                    return res;
                } else {
                    return PythonCalls.Call(context, DynamicHelpers.GetPythonType(this), ((IEnumerable)this).GetEnumerator());
                }
            }

            public void __delitem__(CodeContext/*!*/ context, object index) {
                lock (_lockObj) {
                    int realIndex = IndexToSlot(context, index);

                    _version++;
                    if (realIndex == _head) {
                        popleft();
                    } else if (realIndex == (_tail - 1) ||
                        (realIndex == (_data.Length - 1) && _tail == _data.Length)) {
                        pop();
                    } else {
                        // we're removing an index from the middle, what a pain...
                        // we'll just recreate our data by walking the data once.
                        object[] newData = new object[_data.Length];
                        int writeIndex = 0;
                        WalkDeque(delegate(int curIndex) {
                            if (curIndex != realIndex) {
                                newData[writeIndex++] = _data[curIndex];
                            }

                            return true;
                        });

                        _head = 0;
                        _tail = writeIndex;
                        _data = newData;

                        _itemCnt--;
                    }
                }
            }

            public PythonTuple __reduce__() {
                lock (_lockObj) {
                    object[] items = new object[_itemCnt];
                    int curItem = 0;
                    WalkDeque(delegate(int curIndex) {
                        items[curItem++] = _data[curIndex];
                        return true;
                    });

                    return PythonTuple.MakeTuple(
                        DynamicHelpers.GetPythonTypeFromType(GetType()),
                        PythonTuple.MakeTuple(List.FromArrayNoCopy(items)),
                        null
                    );
                }
            }

            public int __len__() {
                return _itemCnt;
            }

            #region IComparable Members

            int IComparable.CompareTo(object obj) {
                deque otherDeque = obj as deque;
                if (otherDeque == null) {
                    throw new ValueErrorException("expected deque");
                }

                return CompareToWorker(otherDeque);
            }

            private int CompareToWorker(deque otherDeque) {
                return CompareToWorker(otherDeque, null);
            }

            private int CompareToWorker(deque otherDeque, IComparer comparer) {
                Assert.NotNull(otherDeque);

                if (otherDeque._itemCnt == 0 && _itemCnt == 0) {
                    // comparing two empty deques
                    return 0;
                }

                if (CompareUtil.Check(this)) return 0;

                CompareUtil.Push(this);
                try {
                    int otherIndex = otherDeque._head, ourIndex = _head;

                    for (; ; ) {
                        int result;
                        if (comparer == null) {
                            result = PythonOps.Compare(_data[ourIndex], otherDeque._data[otherIndex]);
                        } else {
                            result = comparer.Compare(_data[ourIndex], otherDeque._data[otherIndex]);
                        }
                        if (result != 0) {
                            return result;
                        }

                        // advance both indexes
                        otherIndex++;
                        if (otherIndex == otherDeque._data.Length) {
                            otherIndex = 0;
                        }
                        if (otherIndex == otherDeque._tail) {
                            break;
                        }

                        ourIndex++;
                        if (ourIndex == _data.Length) {
                            ourIndex = 0;
                        }
                        if (ourIndex == _tail) {
                            break;
                        }
                    }
                    // all items are equal, but # of items may be different.

                    if (otherDeque._itemCnt == _itemCnt) {
                        // same # of items, all items are equal
                        return 0;
                    }

                    return _itemCnt > otherDeque._itemCnt ? 1 : -1;
                } finally {
                    CompareUtil.Pop(this);
                }
            }

            #endregion

            #region IStructuralComparable Members

            int IStructuralComparable.CompareTo(object other, IComparer comparer) {
                deque otherDeque = other as deque;
                if (otherDeque == null) {
                    throw new ValueErrorException("expected deque");
                }

                return CompareToWorker(otherDeque, comparer);
            }

            #endregion

            #region IEnumerable Members

            IEnumerator IEnumerable.GetEnumerator() {
                return new DequeIterator(this);
            }

            [PythonType("deque_iterator")]
            private sealed class DequeIterator : IEnumerable, IEnumerator {
                private readonly deque _deque;
                private int _curIndex, _moveCnt, _version;

                public DequeIterator(deque d) {
                    lock (d._lockObj) {
                        _deque = d;
                        _curIndex = d._head - 1;
                        _version = d._version;
                    }
                }

                #region IEnumerator Members

                object IEnumerator.Current {
                    get {
                        return _deque._data[_curIndex];
                    }
                }

                bool IEnumerator.MoveNext() {
                    lock (_deque._lockObj) {
                        if (_version != _deque._version) {
                            throw PythonOps.RuntimeError("deque mutated during iteration");
                        }

                        if (_moveCnt < _deque._itemCnt) {
                            _curIndex++;
                            _moveCnt++;
                            if (_curIndex == _deque._data.Length) {
                                _curIndex = 0;
                            }
                            return true;
                        }
                        return false;
                    }
                }

                void IEnumerator.Reset() {
                    _moveCnt = 0;
                    _curIndex = _deque._head - 1;
                }

                #endregion

                #region IEnumerable Members

                public IEnumerator GetEnumerator() {
                    return this;
                }

                #endregion
            }

            #endregion

            #region __reverse__ implementation

            public virtual IEnumerator __reversed__() {
                return new deque_reverse_iterator(this);
            }

            [PythonType]
            private class deque_reverse_iterator : IEnumerator {
                private readonly deque _deque;
                private int _curIndex, _moveCnt, _version;

                public deque_reverse_iterator(deque d) {
                    lock (d._lockObj) {
                        _deque = d;
                        _curIndex = d._tail;
                        _version = d._version;
                    }
                }

                #region IEnumerator Members

                object IEnumerator.Current {
                    get {
                        return _deque._data[_curIndex];
                    }
                }

                bool IEnumerator.MoveNext() {
                    lock (_deque._lockObj) {
                        if (_version != _deque._version) {
                            throw PythonOps.RuntimeError("deque mutated during iteration");
                        }

                        if (_moveCnt < _deque._itemCnt) {
                            _curIndex--;
                            _moveCnt++;
                            if (_curIndex < 0) {
                                _curIndex = _deque._data.Length - 1;
                            }
                            return true;
                        }
                        return false;
                    }
                }

                void IEnumerator.Reset() {
                    _moveCnt = 0;
                    _curIndex = _deque._tail;
                }

                #endregion
            }

            #endregion

            #region private members

            private void GrowArray() {
                // do nothing if array is already at its max length
                if (_data.Length == _maxLen) return;

                object[] newData;
                if (_maxLen < 0) newData = new object[_data.Length * 2];
                else newData = new object[Math.Min(_maxLen, _data.Length * 2)];

                // make the array completely sequential again
                // by starting head back at 0.
                int cnt1, cnt2;
                if (_head >= _tail) {
                    cnt1 = _data.Length - _head;
                    cnt2 = _data.Length - cnt1;
                } else {
                    cnt1 = _tail - _head;
                    cnt2 = _data.Length - cnt1;
                }

                Array.Copy(_data, _head, newData, 0, cnt1);
                Array.Copy(_data, 0, newData, cnt1, cnt2);

                _head = 0;
                _tail = _data.Length;
                _data = newData;
            }

            private int IndexToSlot(CodeContext/*!*/ context, object index) {
                if (_itemCnt == 0) {
                    throw PythonOps.IndexError("deque index out of range");
                }

                int intIndex = PythonContext.GetContext(context).ConvertToInt32(index);
                if (intIndex >= 0) {
                    if (intIndex >= _itemCnt) {
                        throw PythonOps.IndexError("deque index out of range");
                    }

                    int realIndex = _head + intIndex;
                    if (realIndex >= _data.Length) {
                        realIndex -= _data.Length;
                    }

                    return realIndex;
                } else {
                    if ((intIndex * -1) > _itemCnt) {
                        throw PythonOps.IndexError("deque index out of range");
                    }

                    int realIndex = _tail + intIndex;
                    if (realIndex < 0) {
                        realIndex += _data.Length;
                    }

                    return realIndex;
                }
            }

            private delegate bool DequeWalker(int curIndex);

            /// <summary>
            /// Walks the queue calling back to the specified delegate for
            /// each populated index in the queue.
            /// </summary>
            private void WalkDeque(DequeWalker walker) {
                if (_itemCnt != 0) {
                    int end;
                    if (_head >= _tail) {
                        end = _data.Length;
                    } else {
                        end = _tail;
                    }

                    for (int i = _head; i < end; i++) {
                        if (!walker(i)) {
                            return;
                        }
                    }
                    if (_head >= _tail) {
                        for (int i = 0; i < _tail; i++) {
                            if (!walker(i)) {
                                return;
                            }
                        }
                    }
                }
            }
            #endregion

            #region ICodeFormattable Members

            public virtual string/*!*/ __repr__(CodeContext/*!*/ context) {
                List<object> infinite = PythonOps.GetAndCheckInfinite(this);
                if (infinite == null) {
                    return "[...]";
                }

                int infiniteIndex = infinite.Count;
                infinite.Add(this);
                try {
                    StringBuilder sb = new StringBuilder();
                    sb.Append("deque([");
                    string comma = "";

                    lock (_lockObj) {
                        WalkDeque(delegate(int index) {
                            sb.Append(comma);
                            sb.Append(PythonOps.Repr(context, _data[index]));
                            comma = ", ";
                            return true;
                        });
                    }

                    if (_maxLen < 0) {
                        sb.Append("])");
                    } else {
                        sb.Append("], maxlen=");
                        sb.Append(_maxLen);
                        sb.Append(')');
                    }

                    return sb.ToString();
                } finally {
                    System.Diagnostics.Debug.Assert(infiniteIndex == infinite.Count - 1);
                    infinite.RemoveAt(infiniteIndex);
                }
            }

            #endregion

            #region IValueEquality Members
#if CLR2
            int IValueEquality.GetValueHashCode() {
                throw PythonOps.TypeError("deque objects are unhashable");
            }

            bool IValueEquality.ValueEquals(object other) {
                if (!(other is deque)) return false;

                return EqualsWorker((deque)other);
            }
#endif
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
                if (!(other is deque)) return false;

                return EqualsWorker((deque)other, comparer);
            }

            private bool EqualsWorker(deque other) {
                return EqualsWorker(other, null);
            }

            private bool EqualsWorker(deque otherDeque, IEqualityComparer comparer) {
                Assert.NotNull(otherDeque);

                if (otherDeque._itemCnt != _itemCnt) {
                    // number of items is different, deques can't be equal
                    return false;
                } else if (otherDeque._itemCnt == 0) {
                    // two empty deques are equal
                    return true;
                }

                if (CompareUtil.Check(this)) return true;

                CompareUtil.Push(this);
                try {
                    for (int otherIndex = otherDeque._head, ourIndex = _head; ourIndex != _tail; ) {
                        bool result;
                        if (comparer == null) {
                            result = PythonOps.EqualRetBool(_data[ourIndex], otherDeque._data[otherIndex]);
                        } else {
                            result = comparer.Equals(_data[ourIndex], otherDeque._data[otherIndex]);
                        }
                        if (!result) {
                            return false;
                        }

                        // advance both indices
                        otherIndex++;
                        if (otherIndex == otherDeque._data.Length) {
                            otherIndex = 0;
                        }

                        ourIndex++;
                        if (ourIndex == _data.Length) {
                            ourIndex = 0;
                        }
                    }

                    // same # of items, all items are equal
                    return true;
                } finally {
                    CompareUtil.Pop(this);
                }
            }

            #endregion

            #region Rich Comparison Members

            [SpecialName]
            [return: MaybeNotImplemented]
            public static object operator >(deque self, object other) {
                deque otherDeque = other as deque;
                if (otherDeque == null) return NotImplementedType.Value;

                return ScriptingRuntimeHelpers.BooleanToObject(self.CompareToWorker(otherDeque) > 0);
            }

            [SpecialName]
            [return: MaybeNotImplemented]
            public static object operator <(deque self, object other) {
                deque otherDeque = other as deque;
                if (otherDeque == null) return NotImplementedType.Value;

                return ScriptingRuntimeHelpers.BooleanToObject(self.CompareToWorker(otherDeque) < 0);
            }

            [SpecialName]
            [return: MaybeNotImplemented]
            public static object operator >=(deque self, object other) {
                deque otherDeque = other as deque;
                if (otherDeque == null) return NotImplementedType.Value;

                return ScriptingRuntimeHelpers.BooleanToObject(self.CompareToWorker(otherDeque) >= 0);
            }

            [SpecialName]
            [return: MaybeNotImplemented]
            public static object operator <=(deque self, object other) {
                deque otherDeque = other as deque;
                if (otherDeque == null) return NotImplementedType.Value;

                return ScriptingRuntimeHelpers.BooleanToObject(self.CompareToWorker(otherDeque) <= 0);
            }

            #endregion

            #region ICollection Members

            void ICollection.CopyTo(Array array, int index) {
                int i = 0;
                foreach (object o in this) {
                    array.SetValue(o, index + i++);
                }
            }

            int ICollection.Count {
                get { return this._itemCnt; }
            }

            bool ICollection.IsSynchronized {
                get { return false; }
            }

            object ICollection.SyncRoot {
                get { return this; }
            }

            #endregion
        }

        [PythonType]
        public class defaultdict : PythonDictionary {
            private object _factory;
            private CallSite<Func<CallSite, CodeContext, object, object>> _missingSite;

            public defaultdict(CodeContext/*!*/ context) {
                _missingSite = CallSite<Func<CallSite, CodeContext, object, object>>.Create(
                    new PythonInvokeBinder(
                        PythonContext.GetContext(context),
                        new CallSignature(0)
                    )
                );
            }

            public void __init__(object default_factory) {
                _factory = default_factory;
            }

            public void __init__(CodeContext/*!*/ context, object default_factory, params object[] args) {
                _factory = default_factory;
                foreach (object o in args) {
                    update(context, o);
                }
            }

            public void __init__(CodeContext/*!*/ context, object default_factory, [ParamDictionary]IDictionary<object, object> dict, params object[] args) {
                __init__(context, default_factory, args);

                foreach (KeyValuePair<object , object> kvp in dict) {
                    this[kvp.Key] = kvp.Value;
                }
            }

            public object default_factory {
                get {
                    return _factory;
                }
                set {
                    _factory = value;
                }
            }

            public object __missing__(CodeContext context, object key) {
                object factory = _factory;

                if (factory == null) {
                    throw PythonOps.KeyError(key);
                }

                return this[key] = _missingSite.Target.Invoke(_missingSite, context, factory);
            }

            public object __copy__(CodeContext/*!*/ context) {
                return copy(context);
            }

            public override PythonDictionary copy(CodeContext/*!*/ context) {
                defaultdict res = new defaultdict(context);
                res.default_factory = this.default_factory;
                res.update(context, this);
                return res;
            }


            public override string __repr__(CodeContext context) {
                return String.Format("defaultdict({0}, {1})", PythonOps.Repr(context, default_factory), base.__repr__(context));
            }

            public PythonTuple __reduce__() {
                return PythonTuple.MakeTuple(
                    DynamicHelpers.GetPythonType(this),
                    PythonTuple.MakeTuple(default_factory),
                    null,
                    null,
                    items()
                );
            }
        }
    }
}
