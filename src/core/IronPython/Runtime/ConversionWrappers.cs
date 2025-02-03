// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace IronPython.Runtime {

    public class ListGenericWrapper<T> : IList<T> {
        private readonly IList<object?> _value;
        // PEP 237: int/long unification (GH #52)
        private static readonly bool IsBigIntWrapper = typeof(T) == typeof(BigInteger) || typeof(T) == typeof(BigInteger?);

        public ListGenericWrapper(IList<object?> value) {
            _value = value ?? throw new ArgumentNullException(nameof(value));
        }

        #region IList<T> Members

        public int IndexOf(T item) {
            int pos = _value.IndexOf(item);
            if (IsBigIntWrapper && item is BigInteger bi && bi >= int.MinValue && bi <= int.MaxValue) {
                int pos32 = _value.IndexOf((int)bi);
                if (pos32 >= 0 && (pos32 < pos || pos < 0)) {
                    pos = pos32;
                }
            }
            return pos;
        }

        public void Insert(int index, T item) {
            _value.Insert(index, item);
        }

        public void RemoveAt(int index) {
            _value.RemoveAt(index);
        }

        public T this[int index] {
            get {
                object? item = _value[index];
                if (IsBigIntWrapper && item is int i32) {
                    item = new BigInteger(i32);
                }
                try {
                    return (T)item!;
                } catch (NullReferenceException nex) {
                    throw new InvalidCastException(string.Format("Error in ListGenericWrapper.this[]. Could not cast: from null to {0}", typeof(T).ToString()), nex);
                } catch (InvalidCastException iex) {
                    throw new InvalidCastException(string.Format("Error in ListGenericWrapper.this[]. Could not cast: from {1} to {0}", typeof(T).ToString(), item!.GetType().ToString()), iex);
                }
            }

            set => _value[index] = value;
        }

        #endregion

        #region ICollection<T> Members

        public void Add(T item) {
            _value.Add(item);
        }

        public void Clear() {
            _value.Clear();
        }

        public bool Contains(T item) {
            bool found = _value.Contains(item);
            if (!found && IsBigIntWrapper && item is BigInteger bi && bi >= int.MinValue && bi <= int.MaxValue) {
                found = _value.Contains((int)bi);
            }
            return found;
        }

        public void CopyTo(T[] array, int arrayIndex) {
            for (int i = 0; i < _value.Count; i++) {
                array[arrayIndex + i] = this[i];
            }
        }

        public int Count {
            get { return _value.Count; }
        }

        public bool IsReadOnly {
            get { return _value.IsReadOnly; }
        }

        public bool Remove(T item) {
            int pos = IndexOf(item);
            if (pos >= 0) {
                _value.RemoveAt(pos);
                return true;
            }
            return false;
        }

        #endregion

        #region IEnumerable<T> Members

        public IEnumerator<T> GetEnumerator() {
            return new IEnumeratorOfTWrapper<T>(_value.GetEnumerator());
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator() {
            return _value.GetEnumerator();
        }

        #endregion
    }

    public class DictionaryGenericWrapper<K, V> : IDictionary<K, V> {
        private readonly IDictionary<object?, object?> self;
        // PEP 237: int/long unification (GH #52)
        private static readonly bool IsBigIntWrapperK = typeof(K) == typeof(BigInteger) || typeof(K) == typeof(BigInteger?);
        private static readonly bool IsBigIntWrapperV = typeof(V) == typeof(BigInteger) || typeof(V) == typeof(BigInteger?);

        public DictionaryGenericWrapper(IDictionary<object?, object?> self) {
            this.self = self ?? throw new ArgumentNullException(nameof(self));
        }

        #region IDictionary<K,V> Members

        public void Add(K key, V value) {
            object? okey = key;
            if (IsValidKey32(key, out object? i32) && self.ContainsKey(i32)) {
                okey = i32;
            }
            self.Add(okey, value);
        }

        public bool ContainsKey(K key) {
            return self.ContainsKey(key) || (IsValidKey32(key, out object? i32) && self.ContainsKey(i32));
        }

        public ICollection<K> Keys {
            get {
                List<K> res = new List<K>(Count);
                foreach (object? o in self.Keys) {
                    res.Add(CastKey(o));
                }
                return res;
            }
        }

        public bool Remove(K key) {
            return self.Remove(key) || (IsValidKey32(key, out object? i32) && self.Remove(i32));
        }

        public bool TryGetValue(K key, out V value) {
            if (self.TryGetValue(key, out object? outValue)) {
                value = CastValue(outValue);
                return true;
            }
            if (IsValidKey32(key, out object? i32) && self.TryGetValue(i32, out object? outValue2)) {
                value = CastValue(outValue2);
                return true;
            }
            value = default(V)!;
            return false;
        }

        public ICollection<V> Values {
            get {
                List<V> res = new List<V>();
                foreach (object? o in self.Values) {
                    res.Add(CastValue(o));
                }
                return res;
            }
        }

        public V this[K key] {
            get {
                if (TryGetValue(key, out V value)) {
                    return value;
                }
                throw new KeyNotFoundException($"The given key '{key}' was not present in the dictionary.");
            }
            set {
                Remove(key);
                self[key] = value;
            }
        }

        #endregion

        #region ICollection<KeyValuePair<K,V>> Members

        public void Add(KeyValuePair<K, V> item) {
            object? key = item.Key;
            if (IsValidKey32(item.Key, out object? i32) && self.ContainsKey(i32)) {
                key = i32;
            }
            self.Add(new KeyValuePair<object?, object?>(key, item.Value));
        }

        public void Clear() {
            self.Clear();
        }

        public bool Contains(KeyValuePair<K, V> item) {
            object? key = item.Key;
            if (IsValidKey32(item.Key, out object? i32) && self.ContainsKey(i32)) {
                key = i32;
            }
            return self.Contains(new KeyValuePair<object?, object?>(key, item.Value));
        }

        public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex) {
            foreach (KeyValuePair<K, V> kvp in this) {
                array[arrayIndex++] = kvp;
            }
        }

        public int Count {
            get { return self.Count; }
        }

        public bool IsReadOnly {
            get { return self.IsReadOnly; }
        }

        public bool Remove(KeyValuePair<K, V> item) {
            object? key = item.Key;
            if (IsValidKey32(item.Key, out object? i32) && self.ContainsKey(i32)) {
                key = i32;
            }
            return self.Remove(new KeyValuePair<object?, object?>(key, item.Value));
        }

        #endregion

        #region IEnumerable<KeyValuePair<K,V>> Members

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator() {
            foreach (KeyValuePair<object?, object?> kv in self) {
                yield return new KeyValuePair<K, V>(CastKey(kv.Key), CastValue(kv.Value));
            }
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator() {
            return self.GetEnumerator();
        }

        #endregion

        private static K CastKey(object? key) {
            if (IsBigIntWrapperK && key is int i32) {
                key = new BigInteger(i32);
            }
            try {
                return (K)key!;
            } catch (NullReferenceException nex) {
                throw new InvalidCastException(string.Format("Error in DictionaryGenericWrapper.CastKey. Could not cast: from null to {0}", typeof(K).ToString()), nex);
            } catch (InvalidCastException iex) {
                throw new InvalidCastException(string.Format("Error in DictionaryGenericWrapper.CastKey. Could not cast: from {1} to {0}", typeof(K).ToString(), key!.GetType().ToString()), iex);
            }
        }

        private static V CastValue(object? val) {
            if (IsBigIntWrapperV && val is int i32) {
                val = new BigInteger(i32);
            }
            try {
                return (V)val!;
            } catch (NullReferenceException nex) {
                throw new InvalidCastException(string.Format("Error in DictionaryGenericWrapper.CastValue. Could not cast: from null to {0}", typeof(V).ToString()), nex);
            } catch (InvalidCastException iex) {
                throw new InvalidCastException(string.Format("Error in DictionaryGenericWrapper.CastValue. Could not cast: from {1} to {0}", typeof(V).ToString(), val!.GetType().ToString()), iex);
            }
        }

        private static bool IsValidKey32(K key, [NotNullWhen(true)] out object? key32) {
            if (IsBigIntWrapperK && key is BigInteger bi && bi >= int.MinValue && bi <= int.MaxValue) {
                key32 = (int)bi;
                return true;
            }
            key32 = null;
            return false;
        }
    }

    public class IEnumeratorOfTWrapper<T> : IEnumerator<T> {
        private readonly IEnumerator enumerable;
        // PEP 237: int/long unification (GH #52)
        private static readonly bool IsBigIntWrapper = typeof(T) == typeof(BigInteger) || typeof(T) == typeof(BigInteger?);

        public IEnumeratorOfTWrapper(IEnumerator enumerable) {
            this.enumerable = enumerable ?? throw new ArgumentNullException(nameof(enumerable));
        }

        #region IEnumerator<T> Members

        public T Current {
            get {
                object? current = enumerable.Current;
                if (IsBigIntWrapper && current is int i32) {
                    current = new BigInteger(i32);
                }
                try {
                    return (T)current!;
                } catch (NullReferenceException nex) {
                    throw new InvalidCastException(string.Format("Error in IEnumeratorOfTWrapper.Current. Could not cast: from null to {0}", typeof(T).ToString()), nex);
                } catch (InvalidCastException iex) {
                    throw new InvalidCastException(string.Format("Error in IEnumeratorOfTWrapper.Current. Could not cast: from {1} to {0}", typeof(T).ToString(), current!.GetType().ToString()), iex);
                }
            }
        }

        #endregion

        #region IDisposable Members

        public void Dispose() {
        }

        #endregion

        #region IEnumerator Members

        object? IEnumerator.Current {
            get { return enumerable.Current; }
        }

        public bool MoveNext() {
            return enumerable.MoveNext();
        }

        public void Reset() {
            enumerable.Reset();
        }

        #endregion
    }

    public class IEnumerableOfTWrapper<T> : IEnumerable<T>, IEnumerable {
        private readonly IEnumerable enumerable;

        public IEnumerableOfTWrapper(IEnumerable enumerable) {
            this.enumerable = enumerable ?? throw new ArgumentNullException(nameof(enumerable));
        }

        #region IEnumerable<T> Members

        public IEnumerator<T> GetEnumerator() {
            return new IEnumeratorOfTWrapper<T>(enumerable.GetEnumerator());
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        #endregion
    }

    public sealed class MemoryBufferWrapper : IPythonBuffer {
        private readonly ReadOnlyMemory<byte> _rom;
        private readonly Memory<byte>? _memory;
        private readonly BufferFlags _flags;

        public MemoryBufferWrapper(ReadOnlyMemory<byte> memory, BufferFlags flags) {
            _rom = memory;
            _memory = null;
            _flags = flags;
        }

        public MemoryBufferWrapper(Memory<byte> memory, BufferFlags flags) {
            _rom = memory;
            _memory = memory;
            _flags = flags;
        }

        public void Dispose() { }

        public object Object => _memory ?? _rom;

        public bool IsReadOnly => !_memory.HasValue;

        public ReadOnlySpan<byte> AsReadOnlySpan() => _rom.Span;

        public Span<byte> AsSpan() => _memory.HasValue ? _memory.Value.Span : throw new InvalidOperationException("ReadOnlyMemory is not writable");

        public MemoryHandle Pin() => _rom.Pin();

        public int Offset => 0;

        public string? Format => _flags.HasFlag(BufferFlags.Format) ? "B" : null;

        public int ItemCount => _rom.Length;

        public int ItemSize => 1;

        public int NumOfDims => 1;

        public IReadOnlyList<int>? Shape => null;

        public IReadOnlyList<int>? Strides => null;

        public IReadOnlyList<int>? SubOffsets => null;
    }

    public class MemoryBufferProtocolWrapper : IBufferProtocol {
        private readonly ReadOnlyMemory<byte> _rom;
        private readonly Memory<byte>? _memory;

        public MemoryBufferProtocolWrapper(ReadOnlyMemory<byte> memory) {
            _rom = memory;
            _memory = null;
        }

        public MemoryBufferProtocolWrapper(Memory<byte> memory) {
            _rom = memory;
            _memory = memory;
        }

        public IPythonBuffer? GetBuffer(BufferFlags flags, bool throwOnError) {
            if (_memory.HasValue) {
                return new MemoryBufferWrapper(_memory.Value, flags);
            }

            if (flags.HasFlag(BufferFlags.Writable)) {
                if (throwOnError) {
                    throw Operations.PythonOps.BufferError("ReadOnlyMemory is not writable.");
                }
                return null;
            }

            return new MemoryBufferWrapper(_rom, flags);
        }
    }
}
