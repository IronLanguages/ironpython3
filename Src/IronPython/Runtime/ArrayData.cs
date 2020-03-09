// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime {
    internal interface ArrayData : IList {
        Type StorageType { get; }
        bool CanStore([NotNullWhen(true)]object? item);
        int CountItems(object item);
        IntPtr GetAddress();
        ArrayData Multiply(int count);
        new bool Remove(object item);
        void Reverse();
    }

    internal class ArrayData<T> : ArrayData, IList<T>, IReadOnlyList<T> where T : struct {
        private T[] _items;
        private int _size;
        private GCHandle? _dataHandle;

        private static readonly T[] empty = new T[0];

        public ArrayData() : this(0) { }

        public ArrayData(int capacity) {
            GC.SuppressFinalize(this);
            _items = capacity == 0 ? empty : new T[capacity];
        }

        public ArrayData(IEnumerable<T> collection) : this(collection is ICollection<T> c ? c.Count : 0) {
            AddRange(collection);
        }

        ~ArrayData() {
            Debug.Assert(_dataHandle.HasValue);
            _dataHandle!.Value.Free();
        }

        public int Count => _size;

        public T[] Data => _items;

        bool IList.IsFixedSize => false;

        bool ICollection<T>.IsReadOnly => false;

        bool IList.IsReadOnly => false;

        bool ICollection.IsSynchronized => false;

        Type ArrayData.StorageType { get; } = typeof(T);

        object ICollection.SyncRoot => this;

        public T this[int index] {
            get => _items[index];
            set => _items[index] = value;
        }

        [NotNull]
        object? IList.this[int index] {
            get => _items[index];
            set => _items[index] = GetValue(value);
        }

        public void Add(T item) {
            EnsureSize(_size + 1);
            _items[_size++] = item;
        }

        int IList.Add(object? item) {
            Add(GetValue(item));
            return _size - 1;
        }

        public void AddRange(IEnumerable<T> collection) {
            if (collection is ICollection<T> c) {
                EnsureSize(_size + c.Count);
                c.CopyTo(_items, _size);
                _size += c.Count;
            } else {
                foreach (var x in collection) {
                    Add(x);
                }
            }
        }

        bool ArrayData.CanStore([NotNullWhen(true)]object? item) {
            if (!(item is T) && !Converter.TryConvert(item, typeof(T), out _))
                return false;

            return true;
        }

        public void Clear() {
            _size = 0;
        }

        public bool Contains(T item)
            => _size != 0 && IndexOf(item) != -1;

        bool IList.Contains(object? item)
            => Contains(GetValue(item));

        public void CopyTo(T[] array, int arrayIndex)
            => Array.Copy(_items, 0, array, arrayIndex, _size);

        void ICollection.CopyTo(Array array, int index)
            => Array.Copy(_items, 0, array, index, _size);

        int ArrayData.CountItems(object item) {
            T other = GetValue(item);
            return _items.Take(_size).Count(x => x.Equals(other));
        }

        private void EnsureSize(int size) {
            if (_items.Length < size) {
                var length = _items.Length;
                if (length == 0) length = 8;
                while (length < size) {
                    length *= 2;
                }
                Array.Resize(ref _items, length);
                if (_dataHandle != null) {
                    _dataHandle.Value.Free();
                    _dataHandle = null;
                    GC.SuppressFinalize(this);
                }
            }
        }

        IntPtr ArrayData.GetAddress() {
            // slightly evil to pin our data array but it's only used in rare
            // interop cases.  If this becomes a problem we can move the allocation
            // onto the unmanaged heap if we have full trust via a different subclass
            // of ArrayData.
            if (!_dataHandle.HasValue) {
                _dataHandle = GCHandle.Alloc(_items, GCHandleType.Pinned);
                GC.ReRegisterForFinalize(this);
            }
            return _dataHandle.Value.AddrOfPinnedObject();
        }

        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private static T GetValue(object? value) {
            if (value is T v) {
                return v;
            }

            object? newVal;
            if (!Converter.TryConvert(value, typeof(T), out newVal)) {
                if (value != null && typeof(T).IsPrimitive && typeof(T) != typeof(char))
                    throw PythonOps.OverflowError("couldn't convert {1} to {0}",
                        DynamicHelpers.GetPythonTypeFromType(typeof(T)).Name,
                        DynamicHelpers.GetPythonType(value).Name);
                throw PythonOps.TypeError("expected {0}, got {1}",
                    DynamicHelpers.GetPythonTypeFromType(typeof(T)).Name,
                    DynamicHelpers.GetPythonType(value).Name);
            }
            value = newVal;
            return (T)value;
        }

        public int IndexOf(T item)
            => Array.IndexOf(_items, item, 0, _size);

        int IList.IndexOf(object? item) => IndexOf(GetValue(item));

        public void Insert(int index, T item) {
            EnsureSize(_size + 1);
            if (index < _size) {
                Array.Copy(_items, index, _items, index + 1, _size - index);
            }
            _items[index] = item;
            _size++;
        }

        void IList.Insert(int index, object? item) => Insert(index, GetValue(item));

        ArrayData ArrayData.Multiply(int count) {
            count *= _size;
            var res = new ArrayData<T>(count * _size);
            if (count != 0) {
                Array.Copy(_items, res._items, _size);

                int block = _size;
                int pos = _size;
                while (pos < count) {
                    Array.Copy(res._items, 0, res._items, pos, Math.Min(block, count - pos));
                    pos += block;
                    block *= 2;
                }
                res._size = count;
            }

            return res;
        }

        public bool Remove(T item) {
            int index = IndexOf(item);
            if (index >= 0) {
                RemoveAt(index);
                return true;
            }

            return false;
        }

        bool ArrayData.Remove(object? item) => Remove(GetValue(item));

        void IList.Remove(object? item) => Remove(GetValue(item));

        public void RemoveAt(int index) {
            _size--;
            if (index < _size) {
                Array.Copy(_items, index + 1, _items, index, _size - index);
            }
        }

        public void RemoveRange(int index, int count) {
            if (count > 0) {
                _size -= count;
                if (index < _size) {
                    Array.Copy(_items, index + count, _items, index, _size - index);
                }
            }
        }

        public void Reverse() {
            Array.Reverse(_items, 0, _size);
        }
    }
}
