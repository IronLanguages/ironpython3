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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime {
    internal interface ArrayData : IList {
        Type StorageType { get; }
        bool CanStore([NotNullWhen(true)]object? item);
        int CountItems(object? item);
        IntPtr GetAddress();
        void AddRange(ArrayData value);
        void InsertRange(int index, int count, ArrayData value);
        void RemoveSlice(Slice slice);
        ArrayData Multiply(int count);
        new bool Remove(object? item);
        void Reverse();
        Span<byte> AsByteSpan();
        IPythonBuffer GetBuffer(object owner, string format, BufferFlags flags);
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

        public ArrayData(IEnumerable<T> collection) : this(collection is ICollection<T> c ? c.Count : collection is IReadOnlyCollection<T> rc ? rc.Count : 0) {
            AddRange(collection);
        }

        internal ArrayData(ReadOnlySpan<T> data) {
            GC.SuppressFinalize(this);
            _items = data.ToArray();
            _size = _items.Length;
        }

        ~ArrayData() {
            Debug.Assert(_dataHandle.HasValue);
            _dataHandle?.Free();
        }

        public int Count => _size;

        public T[] Data => _items;

        bool IList.IsFixedSize => false;

        bool ICollection<T>.IsReadOnly => false;

        bool IList.IsReadOnly => false;

        bool ICollection.IsSynchronized => false;

        Type ArrayData.StorageType => typeof(T);

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
            lock (this) {
                CheckBuffer();
                EnsureSize(_size + 1L);
                _items[_size++] = item;
            }
        }

        int IList.Add(object? item) {
            Add(GetValue(item));
            return _size - 1;
        }

        public void AddRange(IPythonBuffer data) {
            ReadOnlySpan<byte> dataSpan = data.AsReadOnlySpan();

            Debug.Assert(data.Offset == 0);
            Debug.Assert(data.Strides == null); // C-contiguous
            Debug.Assert(dataSpan.Length % Unsafe.SizeOf<T>() == 0);

            int delta = dataSpan.Length / Unsafe.SizeOf<T>();
            lock (this) {
                CheckBuffer();
                EnsureSize((long)_size + delta);
                dataSpan.CopyTo(MemoryMarshal.AsBytes(_items.AsSpan(_size)));
                _size += delta;
            }
        }

        public void AddRange(IEnumerable<T> collection) {
            if (collection is ICollection<T> c) {
                lock (this) {
                    CheckBuffer();
                    EnsureSize((long)_size + c.Count);
                    c.CopyTo(_items, _size);
                    _size += c.Count;
                }
            } else {
                foreach (var x in collection) {
                    Add(x);
                }
            }
        }

        void ArrayData.AddRange(ArrayData value)
            => AddRange((ArrayData<T>)value);

        bool ArrayData.CanStore([NotNullWhen(true)]object? item)
            => TryConvert(item, out _);

        public void Clear() {
            lock (this) {
                CheckBuffer();
                _size = 0;
            }
        }

        public bool Contains(T item)
            => _size != 0 && IndexOf(item) != -1;

        bool IList.Contains([NotNullWhen(true)]object? item)
            => TryConvert(item, out T value) && Contains(value);

        public void CopyTo(T[] array, int arrayIndex)
            => Array.Copy(_items, 0, array, arrayIndex, _size);

        void ICollection.CopyTo(Array array, int index)
            => Array.Copy(_items, 0, array, index, _size);

        int ArrayData.CountItems(object? item)
            => TryConvert(item, out T value) ? this.Count(x => x.Equals(value)) : 0;

        private void EnsureSize(long size) {
            if (size > int.MaxValue) throw PythonOps.MemoryError();

            const int IndexOverflow = 0x7FF00000; // https://docs.microsoft.com/en-us/dotnet/api/system.array?view=netcore-3.1#remarks

            if (_items.Length < size) {
                var length = _items.Length;
                if (length == 0) length = 8;
                while (length < size && length <= IndexOverflow / 2) {
                    length *= 2;
                }
                if (length < size) length = (int)size;
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

        public IEnumerator<T> GetEnumerator()
            => _items.Take(_size).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        private static T GetValue(object? value) {
            if (TryConvert(value, out T v)) {
                return v;
            }

            if (value != null && typeof(T).IsPrimitive && typeof(T) != typeof(char))
                throw PythonOps.OverflowError("couldn't convert {1} to {0}",
                    DynamicHelpers.GetPythonTypeFromType(typeof(T)).Name,
                    DynamicHelpers.GetPythonType(value).Name);
            throw PythonOps.TypeError("expected {0}, got {1}",
                DynamicHelpers.GetPythonTypeFromType(typeof(T)).Name,
                DynamicHelpers.GetPythonType(value).Name);
        }

        public int IndexOf(T item)
            => Array.IndexOf(_items, item, 0, _size);

        int IList.IndexOf(object? item)
            => TryConvert(item, out T value) ? IndexOf(value) : -1;

        public void Insert(int index, T item) {
            lock (this) {
                CheckBuffer();
                EnsureSize(_size + 1L);
                if (index < _size) {
                    Array.Copy(_items, index, _items, index + 1, _size - index);
                }
                _items[index] = item;
                _size++;
            }
        }

        void IList.Insert(int index, object? item)
            => Insert(index, GetValue(item));

        public void InsertRange(int index, int count, IList<T> value) {
            // The caller has to ensure that value does not use this ArrayData as its data backing
            Debug.Assert(index >= 0);
            Debug.Assert(count >= 0);
            Debug.Assert(index + count <= _size);

            int delta = value.Count - count;
            if (delta != 0) {
                lock (this) {
                    CheckBuffer();
                    EnsureSize((long)_size + delta);
                    if (index + count < _size) {
                        Array.Copy(_items, index + count, _items, index + value.Count, _size - index - count);
                    }
                    _size += delta;
                }
            }
            value.CopyTo(_items, index);
        }

        void ArrayData.InsertRange(int index, int count, ArrayData value)
            => InsertRange(index, count, (ArrayData<T>)value);

        public void InPlaceMultiply(int count) {
            long newSize = (long)_size * count;
            if (newSize > int.MaxValue) throw PythonOps.MemoryError();
            if (newSize < 0) newSize = 0;
            if (newSize == _size) return;

            long block = _size;
            long pos = _size;
            lock (this) {
                CheckBuffer();
                EnsureSize(newSize);
                _size = (int)newSize;
            }
            while (pos < _size) {
                Array.Copy(_items, 0, _items, pos, Math.Min(block, _size - pos));
                pos += block;
                block *= 2;
            }
        }

        ArrayData ArrayData.Multiply(int count) {
            var res = new ArrayData<T>(count * _size);
            CopyTo(res._items, 0);
            res._size = _size;
            res.InPlaceMultiply(count);
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

        bool ArrayData.Remove(object? item)
            => TryConvert(item, out T value) && Remove(value);

        void IList.Remove(object? item) {
            if (TryConvert(item, out T value))
                Remove(value);
        }

        public void RemoveAt(int index) {
            lock (this) {
                CheckBuffer();
                _size--;
            }
            if (index < _size) {
                Array.Copy(_items, index + 1, _items, index, _size - index);
            }
        }

        public void RemoveRange(int index, int count) {
            if (count > 0) {
                lock (this) {
                    CheckBuffer();
                    _size -= count;
                }
                if (index < _size) {
                    Array.Copy(_items, index + count, _items, index, _size - index);
                }
            }
        }

        public void RemoveSlice(Slice slice) {
            int start, stop, step;
            // slice is sealed, indices can't be user code...
            slice.indices(_size, out start, out stop, out step);

            if (step > 0 && (start >= stop)) return;
            if (step < 0 && (start <= stop)) return;

            lock (this) {
                CheckBuffer();

                if (step == 1) {
                    RemoveRange(start, stop - start);
                    return;
                } else if (step == -1) {
                    RemoveRange(stop + 1, start - stop);
                    return;
                } else if (step < 0) {
                    // normalize start/stop for positive step case
                    int count = PythonOps.GetSliceCount(start, stop, step);
                    stop = start + 1;
                    start += (count - 1) * step;
                    step = -step; // can overflow, OK
                }

                int curr, skip, move;
                // skip: the next position we should skip
                // curr: the next position we should fill in data
                // move: the next position we will check
                curr = skip = move = start;

                while (move < stop) {
                    if (move != skip) {
                        _items[curr++] = _items[move];
                    } else {
                        skip += step; // can overflow, OK
                    }
                    move++;
                }
                RemoveRange(curr, stop - curr);
            }
        }

        public void Reverse()
            => Array.Reverse(_items, 0, _size);

        public Span<byte> AsByteSpan()
            => MemoryMarshal.AsBytes(_items.AsSpan(0, _size));

        private static bool TryConvert([NotNullWhen(true)]object? value, out T result) {
            if (value is null) {
                result = default;
                return false;
            }
            if (value is T res) {
                result = res;
                return true;
            }
            try {
                result = Converter.Convert<T>(value);
                return true;
            } catch {
                result = default;
                return false;
            }
        }

        private int _bufferCount = 0;

        public IPythonBuffer GetBuffer(object owner, string format, BufferFlags flags) {
            return new ArrayDataView(owner, format, this, flags);
        }

        private void CheckBuffer() {
            if (_bufferCount > 0) throw PythonOps.BufferError("Existing exports of data: object cannot be re-sized");
        }

        private sealed class ArrayDataView : IPythonBuffer {
            private readonly BufferFlags _flags;
            private readonly ArrayData<T> _arrayData;
            private readonly string _format;

            public ArrayDataView(object owner, string format, ArrayData<T> arrayData, BufferFlags flags) {
                Object = owner;
                _format = format;
                _arrayData = arrayData;
                _flags = flags;
                lock (_arrayData) {
                    _arrayData._bufferCount++;
                }
            }

            private bool _disposed = false;

            public void Dispose() {
                lock (_arrayData) {
                    if (_disposed) return;
                    _arrayData._bufferCount--;
                    _disposed = true;
                }
            }

            public object Object { get; }

            public bool IsReadOnly => false;

            public ReadOnlySpan<byte> AsReadOnlySpan() => _arrayData.AsByteSpan();

            public Span<byte> AsSpan() => _arrayData.AsByteSpan();

            public int Offset => 0;

            public string? Format => _flags.HasFlag(BufferFlags.Format)? _format : null;

            public int ItemCount => _arrayData.Count;

            public int ItemSize => Unsafe.SizeOf<T>();

            public int NumOfDims => 1;

            public IReadOnlyList<int>? Shape => null;

            public IReadOnlyList<int>? Strides => null;

            public IReadOnlyList<int>? SubOffsets => null;
        }
    }

}
