// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Modules {
    internal abstract class ArrayData {
        public abstract void SetData(int index, object value);
        public abstract object GetData(int index);
        public abstract void Append(object value);
        public abstract int Count(object value);
        public abstract bool CanStore(object value);
        public abstract Type StorageType { get; }
        public abstract int Index(object value);
        public abstract void Insert(int index, object value);
        public abstract void Remove(object value);
        public abstract void RemoveAt(int index);
        public abstract int Length { get; set; }
        public abstract void Swap(int x, int y);
        public abstract void Clear();
        public abstract IntPtr GetAddress();
        public abstract ArrayData Multiply(int count);
    }

    internal class ArrayData<T> : ArrayData where T : struct {
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

        public T[] Data {
            get {
                return _data;
            }
            set {
                _data = value;
            }
        }

        public override object GetData(int index) {
            return _data[index];
        }

        public override void SetData(int index, object value) {
            _data[index] = GetValue(value);
        }

        private static T GetValue(object value) {
            if (!(value is T)) {
                object newVal;
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
            }
            return (T)value;
        }

        public override void Append(object value) {
            EnsureSize(_count + 1);
            _data[_count++] = GetValue(value);
        }

        public void EnsureSize(int size) {
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

        public override int Count(object value) {
            T other = GetValue(value);

            int count = 0;
            for (int i = 0; i < _count; i++) {
                if (_data[i].Equals(other)) {
                    count++;
                }
            }
            return count;
        }

        public override void Insert(int index, object value) {
            EnsureSize(_count + 1);
            if (index < _count) {
                Array.Copy(_data, index, _data, index + 1, _count - index);
            }
            _data[index] = GetValue(value);
            _count++;
        }

        public override int Index(object value) {
            T other = GetValue(value);

            for (int i = 0; i < _count; i++) {
                if (_data[i].Equals(other)) return i;
            }
            return -1;
        }

        public override void Remove(object value) {
            T other = GetValue(value);

            for (int i = 0; i < _count; i++) {
                if (_data[i].Equals(other)) {
                    RemoveAt(i);
                    return;
                }
            }
            throw PythonOps.ValueError("couldn't find value to remove");
        }

        public override void RemoveAt(int index) {
            _count--;
            if (index < _count) {
                Array.Copy(_data, index + 1, _data, index, _count - index);
            }
        }

        public override void Swap(int x, int y) {
            T temp = _data[x];
            _data[x] = _data[y];
            _data[y] = temp;
        }

        public override int Length {
            get {
                return _count;
            }
            set {
                _count = value;
            }
        }

        public override void Clear() {
            _count = 0;
        }

        public override bool CanStore(object value) {
            if (!(value is T) && !Converter.TryConvert(value, typeof(T), out _))
                return false;

            return true;
        }

        public override Type StorageType {
            get { return typeof(T); }
        }

        public override IntPtr GetAddress() {
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

        public override ArrayData Multiply(int count) {
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
    }
}
