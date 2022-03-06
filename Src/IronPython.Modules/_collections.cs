// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
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

[assembly: PythonModule("_collections", typeof(IronPython.Modules.PythonCollections))]
namespace IronPython.Modules {
    public class PythonCollections {
        public const string __doc__ = "High performance data structures\n";

        [PythonType]
        [DontMapIEnumerableToContains, DebuggerDisplay("deque, {__len__()} items"), DebuggerTypeProxy(typeof(CollectionDebugProxy))]
        public class deque : IEnumerable, ICodeFormattable, IStructuralEquatable, ICollection, IReversible, IWeakReferenceable {
            private object[] _data;
            private readonly object _lockObj = new object();
            private int _head, _tail;
            private int _itemCnt, _maxLen, _version;

            public deque() : this(-1) { }

            private deque(int maxlen) {
                // internal private constructor accepts maxlen < 0
                _maxLen = maxlen;
                _data = _maxLen < 0 ? new object[8] : new object[Math.Min(_maxLen, 8)];
            }

            public static object __new__(CodeContext/*!*/ context, PythonType cls, [ParamDictionary]IDictionary<object, object> dict, params object[] args) {
                if (cls == DynamicHelpers.GetPythonTypeFromType(typeof(deque))) return new deque();
                return cls.CreateInstance(context);
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

            public void __init__(object iterable, object maxlen) {
                _maxLen = VerifyMaxLenValue(maxlen);
                
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
                if (value is null) {
                    return -1;
                }

                int res = value switch {
                    int i32 => i32,
                    BigInteger bi => (int)bi,
                    Extensible<BigInteger> ebi => (int)ebi.Value,
                    _ => throw PythonOps.TypeError("an integer is required")
                };
                
                if (res < 0) throw PythonOps.ValueError("maxlen must be non-negative");

                return res;
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
                        if (_maxLen == 0) {
                            return;
                        }
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

            public object copy(CodeContext context)
                => __copy__(context);

            public void extend(object iterable) {
                // d.extend(d)
                if (ReferenceEquals(iterable, this)) {
                    WalkDeque(idx => {
                        append(_data[idx]);
                        return true;
                    });
                    return;
                }

                IEnumerator e = PythonOps.GetEnumerator(iterable);
                while (e.MoveNext()) {
                    append(e.Current);
                }
            }

            public void extendleft(object iterable) {
                // d.extendleft(d)
                if (ReferenceEquals(iterable, this)) {
                    WalkDeque(idx => {
                        appendleft(_data[idx]);
                        return true;
                    });
                    return;
                }

                IEnumerator e = PythonOps.GetEnumerator(iterable);
                while (e.MoveNext()) {
                    appendleft(e.Current);
                }
            }

            public int index(CodeContext context, object value) {
                lock (_lockObj) {
                    return index(context, value, 0, _itemCnt);
                }
            }

            public int index(CodeContext context, object value, int start) {
                lock (_lockObj) {
                    return index(context, value, start, _itemCnt);
                }
            }

            public int index(CodeContext context, object value, int start, int stop) {
                lock (_lockObj) {
                    if (start < 0) {
                        start += _itemCnt;
                        if (start < 0) start = 0;
                    }
                    if (stop < 0) {
                        stop += _itemCnt;
                        if (stop < 0) stop = 0;
                    }
                    int found = -1;
                    int cnt = 0;
                    var startVersion = _version;
                    try {
                        WalkDeque((int index) => {
                            if (cnt >= start) {
                                if (cnt >= stop) return false;
                                if (PythonOps.IsOrEqualsRetBool(_data[index], value)) {
                                    found = index;
                                    return false;
                                }
                            }
                            cnt += 1;
                            return true;
                        });
                    } catch (IndexOutOfRangeException) {
                        Debug.Assert(startVersion != _version);
                    }
                    if (startVersion != _version) {
                        throw PythonOps.RuntimeError("deque mutated during iteration");
                    }
                    if (found == -1) {
                        throw PythonOps.ValueError($"{value} not in deque");
                    }
                    return cnt;
                }
            }

            public int index(CodeContext context, object item, object start)
                => index(context, item, Converter.ConvertToIndex(start));

            public int index(CodeContext context, object item, object start, object stop)
                => index(context, item, Converter.ConvertToIndex(start), Converter.ConvertToIndex(stop));

            public void insert(CodeContext context, int index, object @object) {
                lock (_lockObj) {
                    if (_itemCnt == _maxLen) throw PythonOps.IndexError("deque already at its maximum size");
                    if (index >= _itemCnt) {
                        append(@object);
                    } else if (index <= -_itemCnt || index == 0) {
                        appendleft(@object);
                    } else {
                        rotate(context, -index);
                        if (index < 0) {
                            append(@object);
                        } else {
                            appendleft(@object);
                        }
                        rotate(context, index);
                    }
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
                    try {
                        WalkDeque((int index) => {
                            if (PythonOps.IsOrEqualsRetBool(_data[index], value)) {
                                found = index;
                                return false;
                            }
                            return true;
                        });
                    } catch (IndexOutOfRangeException) {
                        Debug.Assert(_version != startVersion);
                    }
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
                    int rot = context.LanguageContext.ConvertToInt32(n) % _itemCnt;
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
                        _data[IndexToSlot(context, index)] = value;
                    }
                }
            }

            public int count(CodeContext/*!*/ context, object x) {
                int cnt = 0;
                foreach (var o in this) {
                    if (PythonOps.IsOrEqualsRetBool(o, x)) {
                        cnt++;
                    }
                }
                return cnt;
            }

            public object reverse(CodeContext/*!*/ context) {
                lock (_lockObj) {
                    if (_itemCnt == 0) return null;

                    _version++;

                    var cnt = _itemCnt >> 1;
                    var newIndex = _tail;

                    WalkDeque((curIndex) => {
                        if (--cnt < 0) return false;
                        newIndex--;
                        if (newIndex < 0) {
                            newIndex = _data.Length - 1;
                        }
                        var tmp = _data[curIndex];
                        _data[curIndex] = _data[newIndex];
                        _data[newIndex] = tmp;
                        return true;
                    });
                }
                return null;
            }

            public object maxlen => _maxLen == -1 ? null : (object)_maxLen;

            #endregion

            public bool __contains__(CodeContext/*!*/ context, object key) {
                lock (_lockObj) {
                    int found = -1;
                    var startVersion = _version;
                    try {
                        WalkDeque((int index) => {
                            if (PythonOps.IsOrEqualsRetBool(_data[index], key)) {
                                found = index;
                                return false;
                            }
                            return true;
                        });
                    } catch (IndexOutOfRangeException) {
                        Debug.Assert(startVersion != _version);
                    }
                    if (startVersion != _version) {
                        throw PythonOps.RuntimeError("deque mutated during iteration");
                    }
                    return found != -1;
                }
            }

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
                        DynamicHelpers.GetPythonType(this),
                        PythonTuple.MakeTuple(PythonList.FromArrayNoCopy(items))
                    );
                }
            }

            public int __len__() {
                return _itemCnt;
            }

            [SpecialName]
            public deque InPlaceAdd(object other) {
                extend(other);
                return this;
            }


            #region binary operators

            public static deque operator +([NotNull] deque x, object y) {
                if (y is deque t) return x + t;
                throw PythonOps.TypeError($"can only concatenate deque (not \"{PythonOps.GetPythonTypeName(y)}\") to deque");
            }

            public static deque operator +([NotNull] deque x, [NotNull] deque y) {
                var d = new deque(x._maxLen);
                d.extend(x);
                d.extend(y);
                return d;
            }

            private static deque MultiplyWorker(deque self, int count) {
                var d = new deque(self._maxLen);
                if (count <= 0 || self._itemCnt == 0) return d;
                d.extend(self);
                if (count == 1) return d;

                if (d._maxLen < 0 || d._itemCnt * count <= d._maxLen) {
                    var data = ArrayOps.Multiply(d._data, d._itemCnt, count);
                    d._data = data;
                    d._itemCnt = data.Length;
                    Debug.Assert(d._head == 0);
                    d._tail = 0;
                } else {
                    var tempdata = ArrayOps.Multiply(d._data, d._itemCnt, (d._maxLen + (d._itemCnt - 1)) / d._itemCnt);
                    var data = new object[d._maxLen];
                    Array.Copy(tempdata, tempdata.Length - d._maxLen, data, 0, data.Length);
                    d._data = data;
                    d._itemCnt = data.Length;
                    Debug.Assert(d._head == 0);
                    d._tail = 0;
                }
                return d;
            }

            public static deque operator *([NotNull] deque x, int n) {
                return MultiplyWorker(x, n);
            }

            public static deque operator *(int n, [NotNull] deque x) {
                return MultiplyWorker(x, n);
            }

            public static object operator *([NotNull] deque self, [NotNull] Runtime.Index count) {
                return PythonOps.MultiplySequence(MultiplyWorker, self, count, true);
            }

            public static object operator *([NotNull] Runtime.Index count, [NotNull] deque self) {
                return PythonOps.MultiplySequence(MultiplyWorker, self, count, false);
            }

            public static object operator *([NotNull] deque self, object count) {
                if (Converter.TryConvertToIndex(count, out int index)) {
                    return self * index;
                }
                throw PythonOps.TypeErrorForUnIndexableObject(count);
            }

            public static object operator *(object count, [NotNull] deque self) {
                if (Converter.TryConvertToIndex(count, out int index)) {
                    return index * self;
                }

                throw PythonOps.TypeErrorForUnIndexableObject(count);
            }

            #endregion

            #region IEnumerable Members

            IEnumerator IEnumerable.GetEnumerator() {
                return new _deque_iterator(this);
            }

            [PythonType]
            public sealed class _deque_iterator : IEnumerable, IEnumerator {
                private readonly deque _deque;
                private int _curIndex;
                private int _moveCnt;
                private readonly int _version;

                private int IndexToRealIndex(int index) {
                    index += _deque._head;
                    if (index > _deque._data.Length) {
                        index -= _deque._data.Length;
                    }
                    return index;
                }

                public _deque_iterator(deque d, int index = 0) {
                    lock (d._lockObj) {
                        // clamp index to range
                        if (index < 0) index = 0;
                        else if (index > d._itemCnt) index = d._itemCnt;

                        _deque = d;
                        _curIndex = IndexToRealIndex(index) - 1;
                        _moveCnt = index;
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

                public IEnumerator GetEnumerator() => this;

                #endregion

                public int __length_hint__() {
                    lock (_deque._lockObj) {
                        if (_version != _deque._version) {
                            return 0;
                        }
                    }

                    return _deque._itemCnt - _moveCnt;
                }

                public PythonTuple __reduce__(CodeContext context) {
                    return PythonTuple.MakeTuple(
                        DynamicHelpers.GetPythonType(this),
                        PythonTuple.MakeTuple(
                            _deque,
                            _moveCnt
                        )
                    );
                }
            }

            #endregion

            #region __reversed__ implementation

            public virtual IEnumerator __reversed__() {
                return new _deque_reverse_iterator(this);
            }

            [PythonType]
            public class _deque_reverse_iterator : IEnumerable, IEnumerator {
                private readonly deque _deque;
                private int _curIndex;
                private int _moveCnt;
                private readonly int _version;

                public _deque_reverse_iterator(deque d) {
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

                #region IEnumerable Members

                public IEnumerator GetEnumerator() => this;

                #endregion

                public int __length_hint__() {
                    lock (_deque._lockObj) {
                        if (_version != _deque._version) {
                            return 0;
                        }
                    }

                    return _deque._itemCnt - _moveCnt;
                }
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

                int intIndex = context.LanguageContext.ConvertToInt32(index);
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
            private bool WalkDeque(DequeWalker walker) {
                if (_itemCnt != 0) {
                    // capture these at the start so we can mutate
                    int head = _head;
                    int tail = _tail;

                    int end;
                    if (head >= tail) {
                        end = _data.Length;
                    } else {
                        end = tail;
                    }

                    for (int i = head; i < end; i++) {
                        if (!walker(i)) {
                            return false;
                        }
                    }
                    if (head >= tail) {
                        for (int i = 0; i < tail; i++) {
                            if (!walker(i)) {
                                return false;
                            }
                        }
                    }
                }

                return true;
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

            bool IStructuralEquatable.Equals(object other, IEqualityComparer comparer)
                => other is deque d && EqualsWorker(d, comparer);

            private bool EqualsWorker(deque otherDeque, IEqualityComparer comparer = null) {
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
                    int otherIndex = otherDeque._head;
                    return WalkDeque(ourIndex => {
                        bool result;
                        var ourData = _data[ourIndex];
                        var otherData = otherDeque._data[otherIndex];
                        if (comparer == null) {
                            result = PythonOps.IsOrEqualsRetBool(ourData, otherData);
                        } else {
                            result = ReferenceEquals(ourData, otherData) || comparer.Equals(ourData, otherData);
                        }
                        if (!result) {
                            return false;
                        }

                        otherIndex++;
                        if (otherIndex == otherDeque._data.Length) {
                            otherIndex = 0;
                        }
                        return true;
                    });
                } finally {
                    CompareUtil.Pop(this);
                }
            }

            #endregion

            #region Rich Comparison Members

            private object CompareToWorker(CodeContext context, deque other, PythonOperationKind op) {
                if (_itemCnt == 0 || other._itemCnt == 0) {
                    return PythonOps.RichCompare(context, _itemCnt, other._itemCnt, op);
                }

                if (CompareUtil.Check(this)) return 0;

                CompareUtil.Push(this);
                try {
                    int otherIndex = other._head, ourIndex = _head;

                    for (; ; ) {
                        var ourData = _data[ourIndex];
                        var otherData = other._data[otherIndex];
                        if (!PythonOps.IsOrEqualsRetBool(context, ourData, otherData)) {
                            return PythonOps.RichCompare(context, ourData, otherData, op);
                        }

                        // advance both indexes
                        otherIndex++;
                        if (otherIndex == other._data.Length) {
                            otherIndex = 0;
                        }
                        if (otherIndex == other._tail) {
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
                    return PythonOps.RichCompare(context, _itemCnt, other._itemCnt, op);
                } finally {
                    CompareUtil.Pop(this);
                }
            }

            public static object operator >([NotNull] deque self, [NotNull] deque other)
                => self.CompareToWorker(DefaultContext.Default, other, PythonOperationKind.GreaterThan);

            public static object operator <([NotNull] deque self, [NotNull] deque other)
                => self.CompareToWorker(DefaultContext.Default, other, PythonOperationKind.LessThan);

            public static object operator >=([NotNull] deque self, [NotNull] deque other)
                => self.CompareToWorker(DefaultContext.Default, other, PythonOperationKind.GreaterThanOrEqual);

            public static object operator <=([NotNull] deque self, [NotNull] deque other)
                => self.CompareToWorker(DefaultContext.Default, other, PythonOperationKind.LessThanOrEqual);

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

            #region IWeakReferenceable Members

            private WeakRefTracker _tracker;

            WeakRefTracker IWeakReferenceable.GetWeakRef() {
                return _tracker;
            }

            bool IWeakReferenceable.SetWeakRef(WeakRefTracker value) {
                _tracker = value;
                return true;
            }

            void IWeakReferenceable.SetFinalizer(WeakRefTracker value) {
                _tracker = value;
            }

            #endregion
        }

        public static PythonType _deque_iterator {
            get {
                return DynamicHelpers.GetPythonTypeFromType(typeof(deque._deque_iterator));
            }
        }

        public static PythonType _deque_reversed_iterator {
            get {
                return DynamicHelpers.GetPythonTypeFromType(typeof(deque._deque_reverse_iterator));
            }
        }

        [PythonType]
        public class defaultdict : PythonDictionary {
            private readonly CallSite<Func<CallSite, CodeContext, object, object>> _missingSite;

            public defaultdict(CodeContext/*!*/ context) {
                _missingSite = CallSite<Func<CallSite, CodeContext, object, object>>.Create(
                    new PythonInvokeBinder(
                        context.LanguageContext,
                        new CallSignature(0)
                    )
                );
            }

            public new void __init__(CodeContext/*!*/ context, object default_factory) {
                if (default_factory != null && !PythonOps.IsCallable(context, default_factory))
                    throw PythonOps.TypeError("first argument must be callable or None");

                this.default_factory = default_factory;
            }

            public void __init__(CodeContext/*!*/ context, object default_factory, [NotNull]params object[] args) {
                __init__(context, default_factory);

                foreach (object o in args) {
                    update(context, o);
                }
            }

            public void __init__(CodeContext/*!*/ context, object default_factory, [ParamDictionary, NotNull]IDictionary<object, object> dict, [NotNull]params object[] args) {
                __init__(context, default_factory, args);

                foreach (KeyValuePair<object , object> kvp in dict) {
                    this[kvp.Key] = kvp.Value;
                }
            }

            public object default_factory { get; set; }

            public object __missing__(CodeContext context, object key) {
                object factory = default_factory;

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
                return string.Format("defaultdict({0}, {1})", ReprFactory(context, default_factory), base.__repr__(context));

                static string ReprFactory(CodeContext context, object factory) {
                    var infinite = PythonOps.GetAndCheckInfinite(factory);
                    if (infinite == null) {
                        return "...";
                    }

                    int index = infinite.Count;
                    infinite.Add(factory);
                    try {
                        return PythonOps.Repr(context, factory);
                    } finally {
                        Debug.Assert(index == infinite.Count - 1);
                        infinite.RemoveAt(index);
                    }
                }
            }

            public PythonTuple __reduce__(CodeContext context) {
                return PythonTuple.MakeTuple(
                    DynamicHelpers.GetPythonType(this),
                    PythonTuple.MakeTuple(default_factory),
                    null,
                    null,
                    Builtin.iter(context, PythonOps.Invoke(context, this, nameof(PythonDictionary.items)))
                );
            }
        }
    }
}
