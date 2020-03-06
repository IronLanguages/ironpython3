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

using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Modules {
    internal interface ArrayData {
        [NotNull]object? this[int index] { get; set; }
        void Add(object? value);
        int CountValues(object value);
        bool CanStore([NotNullWhen(true)]object? value);
        bool Contains(object? value);
        Type StorageType { get; }
        int IndexOf(object? value);
        void Insert(int index, object value);
        bool Remove(object value);
        void RemoveAt(int index);
        int Count { get; }
        void Swap(int x, int y);
        void Clear();
        IntPtr GetAddress();
        ArrayData Multiply(int count);
    }

    internal class ArrayData<T> : ArrayData, IList<T> where T : struct {
        private T[] _data;
        private int _count;
        private GCHandle? _dataHandle;

        public ArrayData() {
            GC.SuppressFinalize(this);
            _data = new T[8];
        }

        private ArrayData(int size) {
            GC.SuppressFinalize(this);
            _data = new T[size];
            _count = size;
        }

        ~ArrayData() {
            Debug.Assert(_dataHandle.HasValue);
            _dataHandle!.Value.Free();
        }

        public T[] Data => _data;

        public T this[int index] {
            get => _data[index];
            set => _data[index] = value;
        }

        [NotNull]
        object? ArrayData.this[int index] {
            get => _data[index];
            set => _data[index] = GetValue(value);
        }

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

        void ArrayData.Add(object? item) => Add(GetValue(item));

        public void Add(T item) {
            EnsureSize(_count + 1);
            _data[_count++] = item;
        }

        public void AddRange(ICollection<T> data) {
            EnsureSize(_count + data.Count);
            data.CopyTo(_data, _count);
            _count += data.Count;
        }

        private void EnsureSize(int size) {
            if (_data.Length < size) {
                var length = _data.Length;
                while (length < size) {
                    length *= 2;
                }
                Array.Resize(ref _data, length);
                if (_dataHandle != null) {
                    _dataHandle.Value.Free();
                    _dataHandle = null;
                    GC.SuppressFinalize(this);
                }
            }
        }

        public int CountValues(object value) {
            T other = GetValue(value);
            return _data.Count(x => x.Equals(other));
        }

        void ArrayData.Insert(int index, object item) => Insert(index, GetValue(item));

        public void Insert(int index, T item) {
            EnsureSize(_count + 1);
            if (index < _count) {
                Array.Copy(_data, index, _data, index + 1, _count - index);
            }
            _data[index] = item;
            _count++;
        }

        int ArrayData.IndexOf(object? item) => IndexOf(GetValue(item));

        public int IndexOf(T item) {
            for (int i = 0; i < _count; i++) {
                if (_data[i].Equals(item)) return i;
            }
            return -1;
        }

        bool ArrayData.Remove(object value) => Remove(GetValue(value));

        public bool Remove(T item) {
            for (int i = 0; i < _count; i++) {
                if (_data[i].Equals(item)) {
                    RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public void RemoveAt(int index) {
            _count--;
            if (index < _count) {
                Array.Copy(_data, index + 1, _data, index, _count - index);
            }
        }

        public void Swap(int x, int y) {
            T temp = _data[x];
            _data[x] = _data[y];
            _data[y] = temp;
        }

        public void Clear() {
            _count = 0;
        }

        public bool CanStore([NotNullWhen(true)]object? value) {
            if (!(value is T) && !Converter.TryConvert(value, typeof(T), out _))
                return false;

            return true;
        }

        public Type StorageType => typeof(T);

        public bool IsReadOnly => false;

        public int Count => _count;

        public IntPtr GetAddress() {
            // slightly evil to pin our data array but it's only used in rare
            // interop cases.  If this becomes a problem we can move the allocation
            // onto the unmanaged heap if we have full trust via a different subclass
            // of ArrayData.
            if (!_dataHandle.HasValue) {
                _dataHandle = GCHandle.Alloc(_data, GCHandleType.Pinned);
                GC.ReRegisterForFinalize(this);
            }
            return _dataHandle.Value.AddrOfPinnedObject();
        }

        public ArrayData Multiply(int count) {
            var res = new ArrayData<T>(count * _count);
            if (count != 0) {
                Array.Copy(_data, res._data, _count);

                int newCount = count * _count;
                int block = _count;
                int pos = _count;
                while (pos < newCount) {
                    Array.Copy(res._data, 0, res._data, pos, Math.Min(block, newCount - pos));
                    pos += block;
                    block *= 2;
                }
            }

            return res;
        }

        bool ArrayData.Contains(object? value) => Contains(GetValue(value));

        public bool Contains(T item) {
            return IndexOf(item) != -1;
        }

        public void CopyTo(T[] array, int arrayIndex) {
            Array.Copy(_data, 0, array, arrayIndex, _data.Length);
        }

        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_data).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
