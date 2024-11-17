// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting.Utils;

namespace IronPython.Runtime {
    /// <summary>
    /// General-purpose storage used for Python sets and frozensets.
    /// 
    /// The set storage is thread-safe for multiple readers or writers.
    /// 
    /// Mutations to the set involve a simple locking strategy of locking on the SetStorage object
    /// itself to ensure mutual exclusion.
    /// 
    /// Reads against the set happen lock-free. When the set is mutated, it adds or removes buckets
    /// in an atomic manner so that the readers will see a consistent picture as if the read
    /// occurred either before or after the mutation.
    /// </summary>
    [Serializable]
    internal sealed class SetStorage : IEnumerable, IEnumerable<object>, ISerializable, IDeserializationCallback {
        internal Bucket[] _buckets;
        internal int _count;
        private int _version;
        internal bool _hasNull;

        private Func<object, int> _hashFunc;
        private Func<object, object, bool> _eqFunc;
        private Type _itemType;

        // The maximum item count before resizing must occur. This is precomputed upon resizing
        // rather than multiplying by the load factor every time items are added.
        private int _maxCount;

        private const int InitialBuckets = 8;
        private const double Load = 0.7;
        private const double MinLoad = 0.5; // dictates Clone() behavior

        // marker type to indicate we've gone megamorphic (SetStorage happens to be a a type we'll
        // never see as a set element
        private static readonly Type HeterogeneousType = typeof(SetStorage);

        // marker object used to indicate we have a removed value
        internal static readonly object Removed = new object();

        /// <summary>
        /// Creates a new set storage with no buckets
        /// </summary>
        public SetStorage() { }

        /// <summary>
        /// Creates a new set storage with no buckets
        /// </summary>
        public SetStorage(int count) {
            Initialize(count);
        }

#if FEATURE_SERIALIZATION
        private SetStorage(SerializationInfo info, StreamingContext context) {
            // remember the serialization info; we'll deserialize when we get the callback. This
            // enables special types like DBNull.Value to successfully be deserialized inside the
            // set. We store the serialization info in a single-element bucket array so we don't
            // have an extra field just for serialization.
            _buckets = new Bucket[] { new Bucket(0, info) };
        }
#endif

        private void Initialize() {
            _maxCount = (int)(InitialBuckets * Load);
            _buckets = new Bucket[InitialBuckets];
        }

        private void Initialize(int count) {
            int bucketCount = Math.Max((int)(count / Load) + 1, InitialBuckets);

            // convert to a power of 2
            bucketCount = 1 << CeilLog2(bucketCount);

            _maxCount = (int)(bucketCount * Load);
            _buckets = new Bucket[bucketCount];
        }

        /// <summary>
        /// Returns the number of items currently in the set
        /// </summary>
        public int Count {
            get {
                int res = _count;
                if (_hasNull) {
                    res++;
                }
                return res;
            }
        }

        public int Version {
            get {
                return _version;
            }
        }

        /// <summary>
        /// Adds a new item to the set, unless an equivalent item is already present
        /// </summary>
        public void Add(object item) {
            lock (this) {
                AddNoLock(item);
            }
        }

        public void AddNoLock(object item) {
            if (item != null) {
                if (_buckets == null) {
                    Initialize();
                }

                if (item.GetType() != _itemType && _itemType != HeterogeneousType) {
                    UpdateHelperFunctions(item.GetType(), item);
                }

                AddWorker(item, Hash(item));
            } else {
                _hasNull = true;
            }
        }

        private void AddWorker(object/*!*/ item, int hashCode) {
            Debug.Assert(_buckets != null && _count < _buckets.Length);

            if (AddWorker(_buckets, item, hashCode, _eqFunc, ref _version)) {
                _count++;
                if (_count > _maxCount) {
                    Grow();
                }
            }
        }

        /// <summary>
        /// Static helper which adds the given non-null item with a precomputed hash code. Returns
        /// true if the item was added, false if it was already present in the set.
        /// </summary>
        private static bool AddWorker(
            Bucket[]/*!*/ buckets, object/*!*/ item, int hashCode,
            Func<object, object, bool> eqFunc, ref int version
        ) {
            Debug.Assert(buckets != null);
            Debug.Assert(item != null);

            int add_index = -1;
            for (int cnt = 0, index = hashCode & (buckets.Length - 1); cnt < buckets.Length; cnt++, ProbeNext(buckets, ref index)) {
                Bucket bucket = buckets[index];
                if (bucket.Item == null) {
                    version++;
                    if (add_index != -1) index = add_index;
                    buckets[index].HashCode = hashCode;
                    buckets[index].Item = item;
                    return true;
                } else if (bucket.Item == Removed && add_index == -1) {
                    add_index = index;
                } else if (ReferenceEquals(item, bucket.Item) || bucket.Item != Removed && bucket.HashCode == hashCode && eqFunc(item, bucket.Item)) {
                    return false;
                }
            }

            Debug.Assert(add_index != -1);
            version++;
            buckets[add_index].HashCode = hashCode;
            buckets[add_index].Item = item;
            return true;
        }

        /// <summary>
        /// Lock-free helper on a non-null item with a pre-calculated hash code. Removes the item
        /// if it is present in the set, otherwise adds it.
        /// </summary>
        private void AddOrRemoveWorker(object/*!*/ item, int hashCode) {
            Debug.Assert(_buckets != null);
            Debug.Assert(item != null);

            for (int index = hashCode & (_buckets.Length - 1); ; ProbeNext(_buckets, ref index)) {
                Bucket bucket = _buckets[index];
                if (bucket.Item == null) {
                    _version++;
                    _buckets[index].HashCode = hashCode;
                    _buckets[index].Item = item;
                    _count++;
                    if (_count > _maxCount) {
                        Grow();
                    }
                    return;
                } else if (ReferenceEquals(item, bucket.Item) || bucket.Item != Removed && bucket.HashCode == hashCode && _eqFunc(item, bucket.Item)) {
                    _version++;
                    _buckets[index].Item = Removed;
                    _count--;
                    return;
                }
            }
        }

        /// <summary>
        /// Clears the contents of the set
        /// </summary>
        public void Clear() {
            lock (this) {
                ClearNoLock();
            }
        }

        public void ClearNoLock() {
            if (_buckets != null) {
                _version++;
                Initialize();
                _count = 0;
            }
            _hasNull = false;
        }

        /// <summary>
        /// Clones the set, returning a new SetStorage object
        /// </summary>
        public SetStorage Clone() {
            SetStorage res = new SetStorage();
            res._hasNull = _hasNull;

            if (_count == 0) {
                return res;
            }

            Bucket[] buckets = _buckets;
            res._hashFunc = _hashFunc;
            res._eqFunc = _eqFunc;
            res._itemType = _itemType;
            if (_count < _buckets.Length * MinLoad) {
                // If the set is sparsely populated, create a cleaner copy
                res.Initialize(_count);

                for (int i = 0; i < buckets.Length; i++) {
                    Bucket bucket = buckets[i];
                    if (bucket.Item != null && bucket.Item != Removed) {
                        res.AddWorker(bucket.Item, bucket.HashCode);
                    }
                }
            } else {
                // Otherwise, perform a faster copy
                res._maxCount = (int)(buckets.Length * Load);
                res._buckets = new Bucket[buckets.Length];

                for (int i = 0; i < buckets.Length; i++) {
                    Bucket bucket = buckets[i];
                    if (bucket.Item != null) {
                        res._buckets[i].Item = bucket.Item;
                        res._buckets[i].HashCode = bucket.HashCode;
                        if (bucket.Item != Removed) {
                            res._count++;
                        }
                    }
                }
            }

            return res;
        }

        /// <summary>
        /// Checks to see if the given item exists in the set
        /// </summary>
        public bool Contains(object item) {
            if (item == null) {
                return _hasNull;
            }

            if (_count == 0) {
                return false;
            }

            int hashCode;
            Func<object, object, bool> eqFunc;
            if (item.GetType() == _itemType || _itemType == HeterogeneousType) {
                hashCode = _hashFunc(item);
                eqFunc = _eqFunc;
            } else {
                hashCode = _genericHash(item);
                eqFunc = _genericEquals;
            }

            return ContainsWorker(_buckets, item, hashCode, eqFunc);
        }

        /// <summary>
        /// Checks to see if the given item exists in the set, and tries to hash it even
        /// if it is known not to be in the set.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool ContainsAlwaysHash(object item) {
            if (item == null) {
                return _hasNull;
            }

            int hashCode;
            Func<object, object, bool> eqFunc;
            if (item.GetType() == _itemType || _itemType == HeterogeneousType) {
                hashCode = _hashFunc(item);
                eqFunc = _eqFunc;
            } else {
                hashCode = _genericHash(item);
                eqFunc = _genericEquals;
            }

            return _count > 0 && ContainsWorker(_buckets, item, hashCode, eqFunc);
        }

        private static bool ContainsWorker(
            Bucket[]/*!*/ buckets, object/*!*/ item, int hashCode,
            Func<object, object, bool> eqFunc
        ) {
            Debug.Assert(item != null);
            Debug.Assert(buckets != null);
            Debug.Assert(eqFunc != null);

            int index = hashCode & (buckets.Length - 1);
            int startIndex = index;
            do {
                Bucket bucket = buckets[index];
                if (bucket.Item == null) {
                    break;
                } else if (ReferenceEquals(item, bucket.Item) || bucket.Item != Removed && bucket.HashCode == hashCode && eqFunc(item, bucket.Item)) {
                    return true;
                }

                ProbeNext(buckets, ref index);
            } while (startIndex != index);

            return false;
        }

        /// <summary>
        /// Adds items from this set into the other set
        /// </summary>
        public void CopyTo(SetStorage/*!*/ into) {
            Debug.Assert(into != null);

            lock (into) {
                into.UnionUpdate(this);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public IEnumerator<object> GetEnumerator() {
            if (_hasNull) {
                yield return null;
            }

            if (_count == 0) {
                yield break;
            }

            Bucket[] buckets = _buckets;
            for (int i = 0; i < buckets.Length; i++) {
                object item = buckets[i].Item;
                if (item != null && item != Removed) {
                    yield return item;
                }
            }
        }

        public PythonList/*!*/ GetItems() {
            PythonList res = new PythonList(Count);

            if (_hasNull) {
                res.AddNoLock(null);
            }

            if (_count > 0) {
                Bucket[] buckets = _buckets;
                for (int i = 0; i < buckets.Length; i++) {
                    object item = buckets[i].Item;
                    if (item != null && item != Removed) {
                        res.AddNoLock(item);
                    }
                }
            }

            return res;
        }

        /// <summary>
        /// Removes the first set element in the iteration order.
        /// </summary>
        /// <returns>true if an item was removed, false if the set was empty</returns>
        public bool Pop(out object item) {
            item = null;
            if (_hasNull) {
                _hasNull = false;
                return true;
            }

            if (_count == 0) {
                return false;
            }

            lock (this) {
                for (int i = 0; i < _buckets.Length; i++) {
                    if (_buckets[i].Item != null && _buckets[i].Item != Removed) {
                        item = _buckets[i].Item;
                        _version++;
                        _buckets[i].Item = Removed;
                        _count--;
                        return true;
                    }
                }

                item = null;
                return false;
            }
        }

        /// <summary>
        /// Removes an item from the set and returns true if it was present, otherwise returns
        /// false
        /// </summary>
        public bool Remove(object item) {
            lock (this) {
                return RemoveNoLock(item);
            }
        }

        public bool RemoveNoLock(object item) {
            if (item == null) {
                return RemoveNull();
            }

            if (_count == 0) {
                return false;
            }

            return RemoveItem(item);
        }

        /// <summary>
        /// Removes an item from the set and returns true if it was removed. The item will always
        /// be hashed, throwing if it is unhashable - even if the set has no buckets.
        /// </summary>
        internal bool RemoveAlwaysHash(object item) {
            lock (this) {
                if (item == null) {
                    return RemoveNull();
                }
                return RemoveItem(item);
            }
        }

        private bool RemoveNull() {
            if (_hasNull) {
                _hasNull = false;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Lock-free helper to remove a non-null item
        /// </summary>
        private bool RemoveItem(object/*!*/ item) {
            Debug.Assert(item != null);

            int hashCode;
            Func<object, object, bool> eqFunc;
            if (item.GetType() == _itemType || _itemType == HeterogeneousType) {
                hashCode = _hashFunc(item);
                eqFunc = _eqFunc;
            } else {
                hashCode = _genericHash(item);
                eqFunc = _genericEquals;
            }

            return RemoveWorker(item, hashCode, eqFunc);
        }

        private bool RemoveWorker(
            object/*!*/ item, int hashCode, Func<object, object, bool> eqFunc
        ) {
            Debug.Assert(item != null);

            if (_count == 0) {
                return false;
            }

            int index = hashCode & (_buckets.Length - 1);
            int startIndex = index;
            do {
                Bucket bucket = _buckets[index];
                if (bucket.Item == null) {
                    break;
                } else if (ReferenceEquals(item, bucket.Item) || bucket.Item != Removed && bucket.HashCode == hashCode && eqFunc(item, bucket.Item)) {
                    _version++;
                    _buckets[index].Item = Removed;
                    _count--;

                    return true;
                }

                ProbeNext(_buckets, ref index);
            } while (index != startIndex);

            return false;
        }

        #region Set Operations

        // Each of these set operations mutate the current set lock-free. Synchronization must
        // be done by the caller if desired.

        /// <summary>
        /// Determines whether the current set shares no elements with the given set
        /// </summary>
        public bool IsDisjoint(SetStorage other) {
            return IsDisjoint(this, other);
        }

        public static bool IsDisjoint(SetStorage self, SetStorage other) {
            SortBySize(ref self, ref other);

            if (self._hasNull && other._hasNull) {
                return false;
            }

            if (self._count == 0 || other._count == 0) {
                return true;
            }

            Bucket[] buckets = self._buckets;
            Bucket[] otherBuckets = other._buckets;
            var eqFunc = GetEqFunc(self, other);

            for (int i = 0; i < buckets.Length; i++) {
                Bucket bucket = buckets[i];
                if (bucket.Item != null && bucket.Item != Removed &&
                    ContainsWorker(otherBuckets, bucket.Item, bucket.HashCode, eqFunc)) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether the current set is a subset of the given set
        /// </summary>
        public bool IsSubset(SetStorage other) {
            if (_count > other._count ||
                _hasNull && !other._hasNull) {
                return false;
            }

            return IsSubsetWorker(other);
        }

        /// <summary>
        /// Determines whether the current set is a strict subset of the given set
        /// </summary>
        public bool IsStrictSubset(SetStorage other) {
            if (_count > other._count ||
                _hasNull && !other._hasNull ||
                Count == other.Count) {
                return false;
            }

            return IsSubsetWorker(other);
        }

        private bool IsSubsetWorker(SetStorage other) {
            if (_count == 0) {
                return true;
            }

            if (other._count == 0) {
                return false;
            }

            Bucket[] buckets = _buckets;
            Bucket[] otherBuckets = other._buckets;
            var eqFunc = GetEqFunc(this, other);

            for (int i = 0; i < buckets.Length; i++) {
                Bucket bucket = buckets[i];
                if (bucket.Item != null && bucket.Item != Removed &&
                    !ContainsWorker(otherBuckets, bucket.Item, bucket.HashCode, eqFunc)) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Mutates this set to contain its union with 'other'. The caller must lock the current
        /// set if synchronization is desired.
        /// </summary>
        public void UnionUpdate(SetStorage other) {
            _hasNull |= other._hasNull;

            if (other._count == 0) {
                return;
            }

            if (_buckets == null) {
                Initialize(other._count);
            }
            Bucket[] otherBuckets = other._buckets;
            UpdateHelperFunctions(other);


            for (int i = 0; i < otherBuckets.Length; i++) {
                Bucket bucket = otherBuckets[i];
                if (bucket.Item != null && bucket.Item != Removed) {
                    AddWorker(bucket.Item, bucket.HashCode);
                }
            }
        }

        /// <summary>
        /// Mutates this set to contain its intersection with 'other'. The caller must lock the
        /// current set if synchronization is desired.
        /// </summary>
        public void IntersectionUpdate(SetStorage other) {
            if (other._count == 0) {
                ClearNoLock();
                _hasNull &= other._hasNull;
                return;
            }

            _hasNull &= other._hasNull;
            if (_count == 0) {
                return;
            }

            Bucket[] buckets = _buckets;
            Bucket[] otherBuckets = other._buckets;
            var eqFunc = GetEqFunc(this, other);

            for (int i = 0; i < buckets.Length; i++) {
                Bucket bucket = buckets[i];
                if (bucket.Item != null && bucket.Item != Removed &&
                    !ContainsWorker(otherBuckets, bucket.Item, bucket.HashCode, eqFunc)) {
                    _version++;
                    buckets[i].Item = Removed;
                    _count--;
                }
            }
        }

        /// <summary>
        /// Mutates this set to contain its symmetric difference with 'other'. The caller must
        /// lock the current set if synchronization is desired.
        /// </summary>
        public void SymmetricDifferenceUpdate(SetStorage other) {
            _hasNull ^= other._hasNull;

            if (other._count == 0) {
                return;
            }

            if (_buckets == null) {
                Initialize();
            }
            Bucket[] otherBuckets = other._buckets;
            UpdateHelperFunctions(other);

            for (int i = 0; i < otherBuckets.Length; i++) {
                Bucket bucket = otherBuckets[i];
                if (bucket.Item != null && bucket.Item != Removed) {
                    AddOrRemoveWorker(bucket.Item, bucket.HashCode);
                }
            }
        }

        /// <summary>
        /// Mutates this set to contain its difference with 'other'. The caller must lock the
        /// current set if synchronization is desired.
        /// </summary>
        public void DifferenceUpdate(SetStorage other) {
            _hasNull &= !other._hasNull;

            if (_count == 0 || other._count == 0) {
                return;
            }

            Bucket[] buckets = _buckets;
            Bucket[] otherBuckets = other._buckets;
            var eqFunc = GetEqFunc(this, other);

            if (buckets.Length < otherBuckets.Length) {
                // iterate through self, removing anything in other
                for (int i = 0; i < buckets.Length; i++) {
                    Bucket bucket = buckets[i];
                    if (bucket.Item != null && bucket.Item != Removed &&
                        ContainsWorker(otherBuckets, bucket.Item, bucket.HashCode, eqFunc)) {
                        RemoveWorker(bucket.Item, bucket.HashCode, eqFunc);
                    }
                }
            } else {
                // iterate through other, removing anything we find
                for (int i = 0; i < otherBuckets.Length; i++) {
                    Bucket bucket = otherBuckets[i];
                    if (bucket.Item != null && bucket.Item != Removed) {
                        RemoveWorker(bucket.Item, bucket.HashCode, eqFunc);
                    }
                }
            }
        }

        /// <summary>
        /// Computes the union of self and other, returning an entirely new set. This method is
        /// thread-safe and makes no modifications to self or other.
        /// </summary>
        public static SetStorage Union(SetStorage self, SetStorage other) {
            SetStorage res;

            // UnionUpdate iterates through its argument, so clone the larger set
            if (self._count < other._count) {
                res = other.Clone();
                res.UnionUpdate(self);
            } else {
                res = self.Clone();
                res.UnionUpdate(other);
            }

            return res;
        }

        /// <summary>
        /// Computes the intersection of self and other, returning an entirely new set. This
        /// method is thread-safe and makes no modifications to self or other.
        /// </summary>
        public static SetStorage Intersection(SetStorage self, SetStorage other) {
            SetStorage res = new SetStorage(Math.Min(self._count, other._count));

            res._hasNull = self._hasNull && other._hasNull;

            if (self._count == 0 || other._count == 0) {
                return res;
            }

            SortBySize(ref self, ref other);
            Bucket[] buckets = self._buckets;
            Bucket[] otherBuckets = other._buckets;
            var eqFunc = GetEqFunc(self, other);
            // if either set is homogeneous, then the resulting set must be
            if (other._itemType != HeterogeneousType) {
                res.UpdateHelperFunctions(other);
            } else {
                res.UpdateHelperFunctions(self);
            }

            for (int i = 0; i < buckets.Length; i++) {
                Bucket bucket = buckets[i];
                if (bucket.Item != null && bucket.Item != Removed &&
                    ContainsWorker(otherBuckets, bucket.Item, bucket.HashCode, eqFunc)) {
                    res.AddWorker(bucket.Item, bucket.HashCode);
                }
            }

            return res;
        }

        /// <summary>
        /// Computes the symmetric difference of self and other, returning an entirely new set.
        /// This method is thread-safe and makes no modifications to self or other.
        /// </summary>
        public static SetStorage SymmetricDifference(SetStorage self, SetStorage other) {
            // SymmetricDifferenceUpdate iterates through its arg, so clone the larger set
            SortBySize(ref self, ref other);
            SetStorage res = other.Clone();
            res.SymmetricDifferenceUpdate(self);

            return res;
        }

        /// <summary>
        /// Computes the difference of self and other, returning an entirely new set. This
        /// method is thread-safe and makes no modifications to self or other.
        /// </summary>
        public static SetStorage Difference(SetStorage self, SetStorage other) {
            SetStorage res;

            if (self._count == 0 || other._count == 0) {
                res = self.Clone();
                res._hasNull &= !other._hasNull;
                return res;
            }

            if (self._buckets.Length <= other._buckets.Length) {
                res = new SetStorage(self._count);
                res._hasNull &= !other._hasNull;

                Bucket[] buckets = self._buckets;
                Bucket[] otherBuckets = other._buckets;
                var eqFunc = GetEqFunc(self, other);
                res.UpdateHelperFunctions(self);

                for (int i = 0; i < buckets.Length; i++) {
                    Bucket bucket = buckets[i];
                    if (bucket.Item != null && bucket.Item != Removed &&
                        !ContainsWorker(otherBuckets, bucket.Item, bucket.HashCode, eqFunc)) {
                        res.AddWorker(bucket.Item, bucket.HashCode);
                    }
                }
            } else {
                res = self.Clone();
                res.DifferenceUpdate(other);
            }

            return res;
        }

        #endregion

        #region Comparison and Hashing

        public static bool Equals(SetStorage x, SetStorage y, IEqualityComparer comparer) {
            if (object.ReferenceEquals(x, y)) {
                return true;
            }

            if (x._count != y._count || (x._hasNull ^ y._hasNull)) {
                return false;
            }

            if (x._count == 0) {
                // we know y._count == 0
                return true;
            }

            SortBySize(ref x, ref y);

            // optimization when we know the behavior of the comparer
            if (comparer is PythonContext.PythonEqualityComparer) {
                Bucket[] xBuckets = x._buckets;
                Bucket[] yBuckets = y._buckets;
                var eqFunc = GetEqFunc(x, y);

                for (int i = 0; i < xBuckets.Length; i++) {
                    Bucket bucket = xBuckets[i];
                    if (bucket.Item != null && bucket.Item != Removed &&
                        !ContainsWorker(yBuckets, bucket.Item, bucket.HashCode, eqFunc)) {
                        return false;
                    }
                }
                return true;
            }

            // Set comparison using the provided comparer. Create special SetStorage objects
            // which use comparer's hashing and equality functions.
            SetStorage ySet = new SetStorage();
            ySet._itemType = HeterogeneousType;
            ySet._eqFunc = comparer.Equals;
            ySet._hashFunc = comparer.GetHashCode;
            foreach (object item in y) {
                ySet.AddNoLock(item);
            }

            foreach (object item in x) {
                if (!ySet.RemoveNoLock(item)) {
                    return false;
                }
            }

            return ySet._count == 0;
        }

        public static int GetHashCode(SetStorage set, IEqualityComparer/*!*/ comparer) {
            Assert.NotNull(comparer);

            // hash code needs to be stable across collections (even if items are added in
            // different order) and needs to be fairly collision-free.

            int hash1 = 1420601183;
            int hash2 = 674132117;
            int hash3 = 393601577;

            if (set._count > 0) {
                hash1 ^= set._count * 8803;
                hash1 = (hash1 << 10) ^ (hash1 >> 22);
                hash2 += set._count * 5179;
                hash2 = (hash2 << 10) ^ (hash2 >> 22);
                hash3 = hash3 * set._count + 784251623;
                hash3 = (hash3 << 10) ^ (hash3 >> 22);
            }

            if (comparer is PythonContext.PythonEqualityComparer) {
                // Comparer with known hash behavior - use the precomputed hash codes.
                if (set._hasNull) {
                    hash1 = (hash1 << 7) ^ (hash1 >> 25) ^ NoneTypeOps.NoneHashCode;
                    hash2 = ((hash2 << 7) ^ (hash2 >> 25)) + NoneTypeOps.NoneHashCode;
                    hash3 = ((hash3 << 7) ^ (hash3 >> 25)) * NoneTypeOps.NoneHashCode;
                }

                if (set._count > 0) {
                    Bucket[] buckets = set._buckets;
                    for (int i = 0; i < buckets.Length; i++) {
                        object item = buckets[i].Item;
                        if (item != null && item != Removed) {
                            int hashCode = buckets[i].HashCode;
                            hash1 ^= hashCode;
                            hash2 += hashCode;
                            hash3 *= hashCode;
                        }
                    }
                }
            } else {
                // Use the provided comparer for hashing.
                if (set._hasNull) {
                    int hashCode = comparer.GetHashCode(null);
                    hash1 = (hash1 + ((hash1 << 7) ^ (hash1 >> 25))) ^ hashCode;
                    hash2 = ((hash2 << 7) ^ (hash2 >> 25)) + hashCode;
                    hash3 = ((hash3 << 7) ^ (hash3 >> 25)) * hashCode;
                }

                if (set._count > 0) {
                    Bucket[] buckets = set._buckets;
                    for (int i = 0; i < buckets.Length; i++) {
                        object item = buckets[i].Item;
                        if (item != null && item != Removed) {
                            int hashCode = comparer.GetHashCode(item);
                            hash1 ^= hashCode;
                            hash2 += hashCode;
                            hash3 *= hashCode;
                        }
                    }
                }
            }

            hash1 = (hash1 << 11) ^ (hash1 >> 21) ^ hash2;
            hash1 = (hash1 << 27) ^ (hash1 >> 5) ^ hash3;
            return (hash1 << 9) ^ (hash1 >> 23) ^ 2001081521;
        }

        #endregion

        /// <summary>
        /// Used to store a single hashed item.
        /// 
        /// Bucket is not serializable because it stores the computed hash code, which could change
        /// between serialization and deserialization.
        /// </summary>
        internal struct Bucket {
            public object Item;
            public int HashCode;

            public Bucket(int hashCode, object item) {
                HashCode = hashCode;
                Item = item;
            }
        }

        #region Hash/Equality Delegates

        // pre-created delegate instances shared by all homogeneous sets on primitive types
        private static readonly Func<object, int>
            _primitiveHash = PrimitiveHash,
            _intHash = IntHash,
            _doubleHash = DoubleHash,
            _tupleHash = TupleHash,
            _genericHash = GenericHash;
        private static readonly Func<object, object, bool>
            _stringEquals = StringEquals,
            _intEquals = IntEquals,
            _doubleEquals = DoubleEquals,
            _tupleEquals = TupleEquals,
            _genericEquals = GenericEquals,
            _objectEquals = object.ReferenceEquals;

        private static int PrimitiveHash(object o) {
            return o.GetHashCode();
        }

        private static int IntHash(object o) {
            return (int)o;
        }

        private static int DoubleHash(object o) {
            return DoubleOps.__hash__((double)o);
        }

        private static int TupleHash(object o) {
            return ((IStructuralEquatable)o).GetHashCode(
                DefaultContext.DefaultPythonContext.EqualityComparerNonGeneric
            );
        }

        private static int GenericHash(object o) {
            return PythonOps.Hash(DefaultContext.Default, o);
        }

        private static bool StringEquals(object o1, object o2) {
            return (string)o1 == (string)o2;
        }

        private static bool IntEquals(object o1, object o2) {
            Debug.Assert(o1 is int && o2 is int);
            return (int)o1 == (int)o2;
        }

        private static bool DoubleEquals(object o1, object o2) {
            return (double)o1 == (double)o2;
        }

        private static bool TupleEquals(object o1, object o2) {
            return ((IStructuralEquatable)o1).Equals(
                o2, DefaultContext.DefaultPythonContext.EqualityComparerNonGeneric
            );
        }

        private static bool GenericEquals(object o1, object o2) {
            return object.ReferenceEquals(o1, o2) || PythonOps.EqualRetBool(o1, o2);
        }

        private void UpdateHelperFunctions(SetStorage other) {
            if (_itemType == HeterogeneousType || _itemType == other._itemType) {
                return;
            }

            if (other._itemType == HeterogeneousType) {
                SetHeterogeneousSites();
                return;
            }

            if (_itemType == null) {
                _hashFunc = other._hashFunc;
                _eqFunc = other._eqFunc;
                _itemType = other._itemType;
                return;
            }

            SetHeterogeneousSites();
        }

        private void UpdateHelperFunctions(Type t, object item) {
            if (_itemType == null) {
                // first time through; get the sites for this specific type
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
                } else if (t == typeof(Type).GetType()) {
                    // RuntimeType
                    _hashFunc = _primitiveHash;
                    _eqFunc = _objectEquals;
                } else {
                    // random other type, but still homogeneous; get a shared site
                    PythonType pt = DynamicHelpers.GetPythonType(item);
                    AssignSiteDelegates(
                        PythonContext.GetHashSite(pt),
                        DefaultContext.DefaultPythonContext.GetEqualSite(pt)
                    );
                }

                _itemType = t;
            } else if (_itemType != HeterogeneousType) {
                // 2nd time through, we're adding a new type, so the set is heterogeneous
                SetHeterogeneousSites();
            }
            // else this set has already created a new heterogeneous site
        }

        private void SetHeterogeneousSites() {
            // we need to clone the buckets so any lock-free readers will only see the
            // old, homogeneous buckets
            _buckets = (Bucket[])_buckets.Clone();

            AssignSiteDelegates(
                DefaultContext.DefaultPythonContext.MakeHashSite(),
                DefaultContext.DefaultPythonContext.MakeEqualSite()
            );

            _itemType = HeterogeneousType;
        }

        private void AssignSiteDelegates(
            CallSite<Func<CallSite, object, int>> hashSite,
            CallSite<Func<CallSite, object, object, bool>> equalSite
        ) {
            _hashFunc = (o) => hashSite.Target(hashSite, o);
            _eqFunc = (o0, o1) => equalSite.Target(equalSite, o0, o1);
        }

        /// <summary>
        /// Helper to hash the given item w/ support for null
        /// </summary>
        private int Hash(object item) {
            if (item is string) {
                return item.GetHashCode();
            }

            return _hashFunc(item);
        }

        private static Func<object, object, bool> GetEqFunc(SetStorage self, SetStorage other) {
            if (self._itemType == other._itemType || self._itemType == HeterogeneousType) {
                return self._eqFunc;
            } else if (other._itemType == HeterogeneousType) {
                return other._eqFunc;
            }
            return _genericEquals;
        }

        #endregion

        #region Internal Set Helpers

        /// <summary>
        /// Helper which ensures that the first argument x requires the least work to enumerate
        /// </summary>
        internal static void SortBySize(ref SetStorage x, ref SetStorage y) {
            if (x._count > 0 &&
                ((y._count > 0 && x._buckets.Length > y._buckets.Length) || y._count == 0)) {
                SetStorage temp = x;
                x = y;
                y = temp;
            }
        }

        /// <summary>
        /// A factory which creates a SetStorage object from any Python iterable. It extracts
        /// the underlying storage of a set or frozen set without copying, which is left to the
        /// caller if necessary.
        /// </summary>
        internal static SetStorage GetItems(object set) {
            SetStorage items;
            if (GetItemsIfSet(set, out items)) {
                return items;
            }

            return GetItemsWorker(set);
        }

        /// <summary>
        /// A factory which creates a SetStorage object from any Python iterable. It extracts
        /// the underlying storage of a set or frozen set without copying, which is left to the
        /// caller if necessary.
        /// Returns true if the given object was a set or frozen set, false otherwise.
        /// </summary>
        internal static bool GetItems(object set, out SetStorage items) {
            if (GetItemsIfSet(set, out items)) {
                return true;
            }

            items = GetItemsWorker(set);
            return false;
        }

        /// <summary>
        /// A factory which creates a SetStorage object from any Python iterable. It extracts
        /// the underlying storage of a set or frozen set, copying in the former case, to return
        /// a SetStorage object that is guaranteed not to receive any outside mutations.
        /// </summary>
        internal static SetStorage GetFrozenItems(object o) {
            Debug.Assert(!(o is SetStorage));

            if (o is FrozenSetCollection frozenset) {
                return frozenset._items;
            }

            if (o is SetCollection set) {
                return set._items.Clone();
            }

            return GetItemsWorker(o);
        }

        internal static SetStorage GetItemsWorker(object set) {
            Debug.Assert(!(set is SetStorage));
            Debug.Assert(!(set is FrozenSetCollection || set is SetCollection));

            IEnumerator en = PythonOps.GetEnumerator(set);
            return GetItemsWorker(en);
        }

        internal static SetStorage GetItemsWorker(IEnumerator en) {
            SetStorage items = new SetStorage();
            while (en.MoveNext()) {
                items.AddNoLock(en.Current);
            }
            return items;
        }

        /// <summary>
        /// Extracts the SetStorage object from o if it is a set or frozenset and returns true.
        /// Otherwise returns false.
        /// </summary>
        public static bool GetItemsIfSet(object o, out SetStorage items) {
            Debug.Assert(!(o is SetStorage));

            if (o is FrozenSetCollection frozenset) {
                items = frozenset._items;
                return true;
            }

            if (o is SetCollection set) {
                items = set._items;
                return true;
            }

            items = null;
            return false;
        }

        /// <summary>
        /// Creates a hashable set from the given set, or does nothing if the given object 
        /// is not a set.
        /// </summary>
        /// <returns>True if o is a set or frozenset, false otherwise</returns>
        internal static bool GetHashableSetIfSet(ref object o) {
            if (o is SetCollection set) {
                if (IsHashable(set)) {
                    return true;
                }
                o = FrozenSetCollection.Make(set._items.Clone());
                return true;
            }
            return o is FrozenSetCollection;
        }

        private static bool IsHashable(SetCollection set) {
            if (set.GetType() == typeof(SetCollection)) {
                return false;
            }

            // else we have a subclass. Check if it has a hash function
            PythonTypeSlot pts;
            PythonType pt = DynamicHelpers.GetPythonType(set);
            object slotValue;

            return pt.TryResolveSlot(DefaultContext.Default, "__hash__", out pts) &&
                   pts.TryGetValue(DefaultContext.Default, set, pt, out slotValue) &&
                   slotValue != null;
        }

        internal static string SetToString(CodeContext/*!*/ context, object set, SetStorage items) {
            var infinite = PythonOps.GetAndCheckInfinite(set);

            bool notEmpty = items._hasNull || items._count > 0;

            string setTypeStr;
            Type setType = set.GetType();
            if (setType == typeof(SetCollection)) {
                setTypeStr = notEmpty && infinite != null ? null : "set";
            } else if (setType == typeof(FrozenSetCollection)) {
                setTypeStr = "frozenset";
            } else {
                setTypeStr = PythonOps.GetPythonTypeName(set);
            }

            StringBuilder sb = new StringBuilder();
            if (setTypeStr != null) {
                sb.Append(setTypeStr);
                sb.Append("(");
            }

            if (infinite == null) {
                sb.Append("...");
            } else if (notEmpty) {
                sb.Append("{");
                string comma = "";

                if (items._hasNull) {
                    sb.Append(comma);
                    sb.Append(PythonOps.Repr(context, null));
                    comma = ", ";
                }

                if (items._count > 0) {

                    foreach (Bucket bucket in items._buckets) {
                        if (bucket.Item != null && bucket.Item != Removed) {
                            sb.Append(comma);
                            int index = infinite.Count;
                            infinite.Add(set);
                            try {
                                sb.Append(PythonOps.Repr(context, bucket.Item));
                            } finally {
                                Debug.Assert(index == infinite.Count - 1);
                                infinite.RemoveAt(index);
                            }
                            comma = ", ";
                        }
                    }
                }

                sb.Append("}");
            }

            if (setTypeStr != null) {
                sb.Append(")");
            }

            return sb.ToString();
        }

        #endregion

        #region Private Helpers

        private void Grow() {
            Debug.Assert(_buckets != null);

            if (_buckets.Length >= 0x40000000) {
                throw PythonOps.MemoryError("set has reached its maximum size");
            }

            Bucket[] newBuckets = new Bucket[_buckets.Length << 1];
            for (int i = 0; i < _buckets.Length; i++) {
                Bucket bucket = _buckets[i];
                if (bucket.Item != null && bucket.Item != Removed) {
                    AddWorker(newBuckets, bucket.Item, bucket.HashCode, _eqFunc, ref _version);
                }
            }

            _buckets = newBuckets;
            _maxCount = (int)(_buckets.Length * Load);
        }

        private static void ProbeNext(Bucket[]/*!*/ buckets, ref int index) {
            Debug.Assert(buckets != null);

            index++;
            if (index == buckets.Length) {
                index = 0;
            }
        }

        private static int CeilLog2(int x) {
            // Note: x is assumed to be positive
            int xOrig = x;
            int res = 1;
            if (x >= 1 << 16) {
                x >>= 16;
                res += 16;
            }
            if (x >= 1 << 8) {
                x >>= 8;
                res += 8;
            }
            if (x >= 1 << 4) {
                x >>= 4;
                res += 4;
            }
            if (x >= 1 << 2) {
                x >>= 2;
                res += 2;
            }
            if (x >= 1 << 1) {
                res += 1;
            }

            // res is now floor + 1. Convert it to ceiling.
            if (1 << res != xOrig) {
                return res;
            }
            return res + 1;
        }

        #endregion

#if FEATURE_SERIALIZATION
        #region ISerializable Members

        public void GetObjectData(SerializationInfo info, StreamingContext context) {
            info.AddValue("buckets", GetItems());
            info.AddValue("hasnull", _hasNull);
        }

        #endregion

        #region IDeserializationCallback Members

        void IDeserializationCallback.OnDeserialization(object sender) {
            SerializationInfo info;
            if (_buckets == null || (info = _buckets[0].Item as SerializationInfo) == null) {
                // if we've received multiple OnDeserialization callbacks, only 
                // deserialize after the 1st one
                return;
            }

            _buckets = null;

            var items = (PythonList)info.GetValue("buckets", typeof(PythonList));
            foreach (object item in items) {
                AddNoLock(item);
            }

            _hasNull = (bool)info.GetValue("hasnull", typeof(bool));
        }

        #endregion
#endif
    }
}
