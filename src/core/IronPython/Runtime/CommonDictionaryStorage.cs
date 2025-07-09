// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime {
    /// <summary>
    /// General purpose storage used for most PythonDictionarys.
    /// 
    /// This dictionary storage is thread safe for multiple readers or writers.
    /// 
    /// Mutations to the dictionary involves a simple locking strategy of
    /// locking on the DictionaryStorage object to ensure that only one
    /// mutation happens at a time.
    ///
    /// Reads against the dictionary happen lock free.  When the dictionary is mutated
    /// it is either adding or removing buckets in a thread-safe manner so that the readers
    /// will either see a consistent picture as if the read occured before or after the mutation.
    /// 
    /// When resizing the dictionary the buckets are replaced atomically so that the reader
    /// sees the new buckets or the old buckets.  When reading the reader first reads
    /// the buckets and then calls a static helper function to do the read from the bucket
    /// array to ensure that readers are not seeing multiple bucket arrays.
    /// </summary>
    [Serializable]
    internal sealed class CommonDictionaryStorage : DictionaryStorage, ISerializable, IDeserializationCallback {
        private int[] _indices;
        private List<Bucket> _buckets;
        private int _count;
        private int _version;

        private const int FREE = -1;
        private const int DUMMY = -2;

        private Func<object, int> _hashFunc;
        private Func<object, object, bool> _eqFunc;
        private Type _keyType;

        private const int InitialBucketSize = 7;
        private const int ResizeMultiplier = 3;
        private const double Load = .7;

        // pre-created delegate instances shared by all homogeneous dictionaries for primitive types.
        private static readonly Func<object, int> _primitiveHash = PrimitiveHash, _doubleHash = DoubleHash, _intHash = IntHash, _tupleHash = TupleHash, _genericHash = GenericHash;
        private static readonly Func<object, object, bool> _intEquals = IntEquals, _doubleEquals = DoubleEquals, _stringEquals = StringEquals, _tupleEquals = TupleEquals, _genericEquals = GenericEquals, _objectEq = System.Object.ReferenceEquals;

        // marker type used to indicate we've gone megamorphic
        private static readonly Type HeterogeneousType = typeof(CommonDictionaryStorage);   // a type we can never see here.

        /// <summary>
        /// Creates a new dictionary storage with no buckets
        /// </summary>
        public CommonDictionaryStorage() { }

        /// <summary>
        /// Creates a new dictionary storage with buckets
        /// </summary>
        public CommonDictionaryStorage(int count) {
            _indices = new int[(int)(count / Load + 2)];
            _indices.AsSpan().Fill(FREE);
            _buckets = new List<Bucket>();
        }

        /// <summary>
        /// Creates a new dictionary geting values/keys from the
        /// items arary
        /// </summary>
        public CommonDictionaryStorage(object[] items, bool isHomogeneous)
            : this(Math.Max(items.Length / 2, InitialBucketSize)) {
            // always called w/ items, and items should be even (key/value pairs)
            Debug.Assert(items.Length > 0 && (items.Length & 0x01) == 0);

            PythonType t = DynamicHelpers.GetPythonType(items[1]);

            if (!isHomogeneous) {
                for (int i = 1; i < items.Length / 2; i++) {
                    if (DynamicHelpers.GetPythonType(items[i * 2 + 1]) != t) {
                        SetHeterogeneousSites();
                        t = null;
                        break;
                    }
                }
            }

            if (t != null) {
                // homogeneous collection
                UpdateHelperFunctions(t, items[1]);
            }

            for (int i = 0; i < items.Length / 2; i++) {
                AddOne(items[i * 2 + 1], items[i * 2]);
            }
        }

        public int Version => _version;

        /// <summary>
        /// Creates a new dictionary storage with the given set of buckets
        /// and size.  Used when cloning the dictionary storage.
        /// </summary>
        private CommonDictionaryStorage(int[] indices, List<Bucket> buckets, int count, Type keyType, Func<object, int> hashFunc, Func<object, object, bool> eqFunc) {
            _indices = indices;
            _buckets = buckets;
            _count = count;
            _keyType = keyType;
            _hashFunc = hashFunc;
            _eqFunc = eqFunc;
        }

#if FEATURE_SERIALIZATION
        private CommonDictionaryStorage(SerializationInfo info, StreamingContext context) {
            // Remember the serialization info, we'll deserialize when we get the callback.  This
            // enables special types like DBNull.Value to successfully be deserialized inside of us.  We
            // store the serialization info in a special bucket so we don't have an extra field just for
            // serialization.
            _buckets = new List<Bucket> { new Bucket(null, info, 0) };
        }
#endif

        public override void Add(ref DictionaryStorage storage, object key, object value)
            => Add(key, value);

        /// <summary>
        /// Adds a new item to the dictionary, replacing an existing one if it already exists.
        /// </summary>
        public void Add(object key, object value) {
            lock (this) {
                AddNoLock(key, value);
            }
        }

        public override void AddNoLock(ref DictionaryStorage storage, object key, object value)
            => AddNoLock(key, value);

        public void AddNoLock(object key, object value) {
            if (_indices == null) {
                Initialize();
            }

            if (_keyType != HeterogeneousType) {
                if (key is null) {
                    SetHeterogeneousSites();
                } else if (key.GetType() != _keyType) {
                    UpdateHelperFunctions(key.GetType(), key);
                }
            }

            AddOne(key, value);
        }

        private void AddOne(object key, object value) {
            Debug.Assert(_keyType == HeterogeneousType || key?.GetType() == _keyType);
            var hc = _hashFunc(key) & int.MaxValue;
            if (AddWorker(_indices, _buckets, new Bucket(key, value, hc), _eqFunc)) {
                if (_count >= (_indices.Length * Load)) {
                    // grow the hash table
                    EnsureSize((int)(_indices.Length / Load) * ResizeMultiplier, _eqFunc);
                }
            }
        }

        private void UpdateHelperFunctions(Type t, object key) {
            Debug.Assert(_indices != null);

            if (_keyType == null) {
                // first time through, get the sites for this specific type...
                if (t == typeof(int)) {
                    _hashFunc = _intHash;
                    _eqFunc = _intEquals;
                } else if (t == typeof(string)) {
                    _hashFunc = _primitiveHash;
                    _eqFunc = _stringEquals;
                } else if (t == typeof(double)) {
                    _hashFunc = _doubleHash;
                    _eqFunc = _doubleEquals;
                } else if (t == typeof(PythonTuple)) {
                    _hashFunc = _tupleHash;
                    _eqFunc = _tupleEquals;
                } else if (t == typeof(Type).GetType()) {    // this odd check checks for RuntimeType.
                    _hashFunc = _primitiveHash;
                    _eqFunc = _objectEq;
                } else {
                    // random type, but still homogeneous... get a shared site for this type.
                    PythonType pt = DynamicHelpers.GetPythonType(key);
                    var hashSite = PythonContext.GetHashSite(pt);
                    var equalSite = DefaultContext.DefaultPythonContext.GetEqualSite(pt);

                    AssignSiteDelegates(hashSite, equalSite);
                }

                _keyType = t;
            } else if (_keyType != HeterogeneousType) {
                // 2nd time through, we're adding a new type so we have mutliple types now, 
                // make a new site for this storage

                SetHeterogeneousSites();

                // we need to clone the buckets so any lock-free readers will only see
                // the old buckets which are homogeneous
                _indices = (int[])_indices.Clone();
            }
            // else we have already created a new site this dictionary
        }

        private void SetHeterogeneousSites() {
            var hashSite = DefaultContext.DefaultPythonContext.MakeHashSite();
            var equalSite = DefaultContext.DefaultPythonContext.MakeEqualSite();

            AssignSiteDelegates(hashSite, equalSite);

            _keyType = HeterogeneousType;
        }

        private void AssignSiteDelegates(CallSite<Func<CallSite, object, int>> hashSite, CallSite<Func<CallSite, object, object, bool>> equalSite) {
            _hashFunc = (o) => hashSite.Target(hashSite, o);
            _eqFunc = (o1, o2) => equalSite.Target(equalSite, o1, o2);
        }

        private void EnsureSize(int newSize, Func<object, object, bool> eqFunc) {
            if (_indices.Length >= newSize) {
                return;
            }

            int[] newIndices = new int[newSize];
            newIndices.AsSpan().Fill(FREE);

            // redo the indexing
            for (var i = 0; i < _buckets.Count; i++) {
                var bucket = _buckets[i];
                if (!bucket.IsRemoved) {
                    var pair = LookupIndex(newIndices, _buckets, bucket.Key, bucket.HashCode, eqFunc);
                    Debug.Assert(pair.Value < 0);
                    newIndices[pair.Key] = i;
                }
            }

            _indices = newIndices;
        }

        public override void EnsureCapacityNoLock(int size) {
            if (_indices == null) {
                _indices = new int[(int)(size / Load) + 1];
                _indices.AsSpan().Fill(FREE);
            } else {
                EnsureSize((int)(size / Load), _eqFunc);
            }
        }

        /// <summary>
        /// Initializes the buckets to their initial capacity, the caller
        /// must check if the buckets are empty first.
        /// </summary>
        private void Initialize() {
            _indices = new int[InitialBucketSize];
            _indices.AsSpan().Fill(FREE);
            _buckets = new List<Bucket>();
        }

        /// <summary>
        /// Add helper which adds the given key/value (where the key is not null) with
        /// a pre-computed hash code.
        /// </summary>
        private static KeyValuePair<int, int> LookupIndex(int[] indices, List<Bucket> buckets, object key, int hashCode, Func<object, object, bool> eqFunc) {
            int startIndex = hashCode % indices.Length;

            // scan forward for matching key first
            int index = startIndex;
            int firstUsableIndex = -1;
            for (; ; ) {
                var idx = indices[index];
                if (idx == FREE) {
                    // no entry was ever here, nothing more to probe
                    return firstUsableIndex == -1 ? new KeyValuePair<int, int>(index, FREE) : new KeyValuePair<int, int>(firstUsableIndex, DUMMY);
                }
                if (idx == DUMMY) {
                    // we recycled this bucket, so need to continue walking to see if a following bucket matches
                    if (firstUsableIndex == -1) {
                        // retain the index of the first recycled bucket, in case we need it later
                        firstUsableIndex = index;
                    }
                } else {
                    Bucket cur = buckets[idx];
                    if (object.ReferenceEquals(key, cur.Key) || (hashCode == cur.HashCode && eqFunc(key, cur.Key))) {
                        // this bucket is a key match
                        return new KeyValuePair<int, int>(index, idx);
                    }
                }

                // keep walking
                if (++index == indices.Length) {
                    index = 0;
                }

                // if we ended up doing a full scan, then this means the key is not already in use and there are
                // only recycled buckets available -- nothing more to probe
                if (index == startIndex) {
                    break;
                }
            }

            // the key wasn't found, but we did find a fresh or recycled (unused) bucket
            return new KeyValuePair<int, int>(firstUsableIndex, DUMMY);
        }

        /// <summary>
        /// Add helper which adds the given key/value with a pre-computed hash code.
        /// </summary>
        private bool AddWorker(int[] indices, List<Bucket> buckets, Bucket bucket, Func<object, object, bool> eqFunc) {
            var pair = LookupIndex(indices, buckets, bucket.Key, bucket.HashCode, eqFunc);
            if (pair.Value < 0) {
                // the key wasn't found, but we did find a fresh or recycled (unused) bucket
                indices[pair.Key] = buckets.Count;
                buckets.Add(bucket);
                _count++;
            } else {
                buckets[pair.Value] = bucket;
            }
            _version++;

            return true;
        }

        /// <summary>
        /// Removes an entry from the dictionary and returns true if the
        /// entry was removed or false.
        /// </summary>
        public override bool Remove(ref DictionaryStorage storage, object key)
            => Remove(key);

        public bool Remove(object key)
            => TryRemoveValue(key, out _);

        /// <summary>
        /// Removes an entry from the dictionary and returns true if the
        /// entry was removed or false.  The key will always be hashed
        /// so if it is unhashable an exception will be thrown - even
        /// if the dictionary has no buckets.
        /// </summary>
        internal bool RemoveAlwaysHash(object key) {
            lock (this) {
                return TryRemoveNoLock(key, out _);
            }
        }

        public override bool TryRemoveValue(ref DictionaryStorage storage, object key, out object value)
            => TryRemoveValue(key, out value);

        public bool TryRemoveValue(object key, out object value) {
            lock (this) {
                if (_count == 0) {
                    value = null;
                    return false;
                }

                return TryRemoveNoLock(key, out value);
            }
        }

        private void GetHash(object key, out int hc, out Func<object, object, bool> eqFunc) {
            Func<object, int> hashFunc;
            if (_keyType == HeterogeneousType || key?.GetType() == _keyType) {
                hashFunc = _hashFunc;
                eqFunc = _eqFunc;
            } else {
                hashFunc = _genericHash;
                eqFunc = _genericEquals;
            }

            hc = hashFunc(key) & int.MaxValue;
        }

        private bool TryRemoveNoLock(object key, out object value) {
            GetHash(key, out var hc, out var eqFunc);

            if (_indices == null) {
                value = null;
                return false;
            }

            var pair = LookupIndex(_indices, _buckets, key, hc, eqFunc);
            if (pair.Value < 0) {
                value = null;
                return false;
            }

            value = _buckets[pair.Value].Value;
            _version++;
            _indices[pair.Key] = DUMMY;
            _buckets[pair.Value] = Bucket.Removed;
            Thread.MemoryBarrier();
            _count--;
            return true;
        }

        /// <summary>
        /// Since <see cref="CommonDictionaryStorage"/> is always mutable, this is a no-op.
        /// </summary>
        /// <param name="storage">Ignored.</param>
        /// <returns><c>this</c></returns>
        public override DictionaryStorage AsMutable(ref DictionaryStorage storage) => this;

        /// <summary>
        /// Checks to see if the key exists in the dictionary.
        /// </summary>
        public override bool Contains(object key) {
            if (!PythonContext.IsHashable(key))
                throw PythonOps.TypeErrorForUnhashableObject(key);
            return TryGetValue(key, out _);
        }

        /// <summary>
        /// Trys to get the value associated with the given key and returns true
        /// if it's found or false if it's not present.
        /// </summary>
        public override bool TryGetValue(object key, out object value)
            => TryGetValue(_indices, _buckets, key, out value);

        /// <summary>
        /// Static helper to try and get the value from the dictionary.
        /// 
        /// Used so the value lookup can run against a buckets while a writer
        /// replaces the buckets.
        /// </summary>
        private bool TryGetValue(int[] indices, List<Bucket> buckets, object key, out object value) {
            if (_count > 0 && indices != null) {
                GetHash(key, out var hc, out var eqFunc);

                var pair = LookupIndex(indices, buckets, key, hc, eqFunc);
                if (pair.Value < 0) {
                    value = null;
                    return false;
                }

                value = buckets[pair.Value].Value;
                return true;
            }

            value = null;
            return false;
        }

        /// <summary>
        /// Returns the number of key/value pairs currently in the dictionary.
        /// </summary>
        public override int Count => _count;

        /// <summary>
        /// Clears the contents of the dictionary.
        /// </summary>
        public override void Clear(ref DictionaryStorage storage) => Clear();

        public void Clear() {
            lock (this) {
                if (_indices != null) {
                    _version++;
                    _indices = new int[8];
                    _indices.AsSpan().Fill(FREE);
                    _buckets.Clear();
                    _count = 0;
                }
            }
        }

        public override List<KeyValuePair<object, object>> GetItems() {
            lock (this) {
                List<KeyValuePair<object, object>> res = new List<KeyValuePair<object, object>>(_count);
                if (_count > 0) {
                    foreach (var bucket in _buckets) {
                        if (!bucket.IsRemoved) {
                            res.Add(new KeyValuePair<object, object>(bucket.Key, bucket.Value));
                        }
                    }
                }
                return res;
            }
        }

        public override IEnumerator<KeyValuePair<object, object>> GetEnumerator() {
            lock (this) {
                if (_count > 0) {
                    foreach (var bucket in _buckets) {
                        if (!bucket.IsRemoved) {
                            yield return new KeyValuePair<object, object>(bucket.Key, bucket.Value);
                        }
                    }
                }
            }
        }

        public override IEnumerable<object>/*!*/ GetKeys() {
            lock (this) {
                if (_count == 0) return Array.Empty<object>();

                object[] res = new object[_count];
                int index = 0;
                foreach (var bucket in _buckets) {
                    if (!bucket.IsRemoved) {
                        res[index++] = bucket.Key;
                    }
                }
                return res;
            }
        }

        public override bool HasNonStringAttributes() {
            lock (this) {
                if (_keyType != typeof(string) && _keyType != null && _count > 0 && _keyType != typeof(Extensible<string>) && !_keyType.IsSubclassOf(typeof(Extensible<string>))) {
                    foreach (var bucket in _buckets) {
                        if (!bucket.IsRemoved && bucket.Key is not string && bucket.Key is not Extensible<string>) {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Clones the storage returning a new DictionaryStorage object.
        /// </summary>
        public override DictionaryStorage Clone() {
            lock (this) {
                if (_indices == null) {
                    return new CommonDictionaryStorage();
                }

                int[] resIndices = (int[])_indices.Clone();
                var resBuckets = new List<Bucket>(_buckets);

                return new CommonDictionaryStorage(resIndices, resBuckets, _count, _keyType, _hashFunc, _eqFunc);
            }
        }

        public override void CopyTo(ref DictionaryStorage/*!*/ into) {
            into = CopyTo(into);
        }

        public DictionaryStorage CopyTo(DictionaryStorage into) {
            Debug.Assert(into != null);

            if (_indices != null) {
                using (new OrderedLocker(this, into)) {
                    if (@into is CommonDictionaryStorage commonInto) {
                        CommonCopyTo(commonInto);
                    } else {
                        UncommonCopyTo(ref into);
                    }
                }
            }

            return into;

            void CommonCopyTo(CommonDictionaryStorage into) {
                if (into._indices == null) {
                    into._indices = new int[_indices.Length];
                    into._indices.AsSpan().Fill(FREE);
                    into._buckets = new List<Bucket>();
                } else {
                    int curSize = into._indices.Length;
                    int newSize = (int)((_count + into._count) / Load) + 2;
                    while (curSize < newSize) {
                        curSize *= ResizeMultiplier;
                    }
                    into.EnsureSize(curSize, into._eqFunc);
                }

                if (into._keyType == null) {
                    into._keyType = _keyType;
                    into._hashFunc = _hashFunc;
                    into._eqFunc = _eqFunc;
                } else if (into._keyType != _keyType) {
                    into.SetHeterogeneousSites();
                }

                foreach (var bucket in _buckets) {
                    if (!bucket.IsRemoved) {
                        into.AddWorker(into._indices, into._buckets, bucket, into._eqFunc);
                    }
                }
            }

            void UncommonCopyTo(ref DictionaryStorage into) {
                foreach (var bucket in _buckets) {
                    if (!bucket.IsRemoved) {
                        into.AddNoLock(ref into, bucket.Key, bucket.Value);
                    }
                }
            }
        }

        /// <summary>
        /// Used to store a single hashed key/value.
        /// 
        /// Bucket is not serializable because it stores the computed hash
        /// code which could change between serialization and deserialization.
        /// </summary>
        private readonly struct Bucket {
            public readonly object Key;          // the key to be hashed
            public readonly object Value;        // the value associated with the key
            public readonly int HashCode;        // the hash code of the contained key.

            public static readonly Bucket Removed = new Bucket(new object(), null, 0);

            public bool IsRemoved => object.ReferenceEquals(Key, Removed.Key);

            public Bucket(object key, object value, int hashCode) {
                HashCode = hashCode;
                Key = key;
                Value = value;
            }
        }

        #region Hash/Equality Delegates

        private static int PrimitiveHash(object o) => o.GetHashCode();

        private static int IntHash(object o) => (int)o;

        private static int DoubleHash(object o) => DoubleOps.__hash__((double)o);

        private static int GenericHash(object o) => PythonOps.Hash(DefaultContext.Default, o);

        private static int TupleHash(object o)
            => ((IStructuralEquatable)o).GetHashCode(DefaultContext.DefaultPythonContext.EqualityComparerNonGeneric);

        private static bool StringEquals(object o1, object o2) => (string)o1 == (string)o2;

        private static bool IntEquals(object o1, object o2) {
            Debug.Assert(o1 is int && o2 is int);
            return (int)o1 == (int)o2;
        }

        private static bool DoubleEquals(object o1, object o2) => (double)o1 == (double)o2;

        private static bool TupleEquals(object o1, object o2)
            => ((IStructuralEquatable)o1).Equals(o2, DefaultContext.DefaultPythonContext.EqualityComparerNonGeneric);

        private static bool GenericEquals(object o1, object o2) => PythonOps.EqualRetBool(o1, o2);

        #endregion

#if FEATURE_SERIALIZATION
        #region ISerializable Members

        public void GetObjectData(SerializationInfo info, StreamingContext context) {
            info.AddValue("buckets", GetItems());
        }

        void IDeserializationCallback.OnDeserialization(object sender) {
            if (_indices is not null) {
                // we've received multiple OnDeserialization callbacks, only
                // deserialize after the 1st one
                return;
            }

            var info = (SerializationInfo)_buckets[0].Value;
            _buckets.Clear();

            var buckets = (List<KeyValuePair<object, object>>)info.GetValue("buckets", typeof(List<KeyValuePair<object, object>>));
            foreach (KeyValuePair<object, object> kvp in buckets) {
                AddNoLock(kvp.Key, kvp.Value);
            }
        }

        #endregion
#endif
    }
}
