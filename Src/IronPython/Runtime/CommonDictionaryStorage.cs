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
using System.Runtime.Serialization;
using System.Threading;

using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

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
    internal class CommonDictionaryStorage : DictionaryStorage, ISerializable, IDeserializationCallback {
        protected Bucket[] _buckets;
        private int _count;
        private int _version;
        private NullValue _nullValue;  // value stored in null bucket

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

        // Marker object used to indicate we have a removed value
        private static readonly object _removed = new object();

 
        /// <summary>
        /// Creates a new dictionary storage with no buckets
        /// </summary>
        public CommonDictionaryStorage() {            
        }

        /// <summary>
        /// Creates a new dictionary storage with no buckets
        /// </summary>
        public CommonDictionaryStorage(int count) {
            _buckets = new Bucket[(int)(count / Load + 2)];
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
                object key = items[i * 2 + 1];
                if (key != null) {
                    AddOne(key, items[i * 2]);
                } else {
                    AddNull(items[i * 2]);
                }
            }
        }

        public int Version {
            get{
                return _version;
            }
        }

        private void AddItems(object[] items) {
            for (int i = 0; i < items.Length / 2; i++) {
                AddNoLock(items[i * 2 + 1], items[i * 2]);
            }
        }

        /// <summary>
        /// Creates a new dictionary storage with the given set of buckets
        /// and size.  Used when cloning the dictionary storage.
        /// </summary>
        private CommonDictionaryStorage(Bucket[] buckets, int count, Type keyType, Func<object, int> hashFunc, Func<object, object, bool> eqFunc, NullValue nullValue) {
            _buckets = buckets;
            _count = count;
            _keyType = keyType;
            _hashFunc = hashFunc;
            _eqFunc = eqFunc;
            _nullValue = nullValue;
        }

#if FEATURE_SERIALIZATION
        private CommonDictionaryStorage(SerializationInfo info, StreamingContext context) {
            // remember the serialization info, we'll deserialize when we get the callback.  This
            // enables special types like DBNull.Value to successfully be deserialized inside of us.  We
            // store the serialization info in a special bucket so we don't have an extra field just for
            // serialization
            _nullValue = new DeserializationNullValue(info);
        }
#endif

        public override void Add(ref DictionaryStorage storage, object key, object value) {
            Add(key, value);
        }

        /// <summary>
        /// Adds a new item to the dictionary, replacing an existing one if it already exists.
        /// </summary>
        public void Add(object key, object value) {
            lock (this) {
                AddNoLock(key, value);
            }
        }

        private void AddNull(object value) {
            if (_nullValue != null) {
                _nullValue.Value = value;
            } else {
                _nullValue = new NullValue(value);
            }
        }

        public override void AddNoLock(ref DictionaryStorage storage, object key, object value) {
            AddNoLock(key, value);
        }

        public void AddNoLock(object key, object value) {
            if (key != null) {
                if (_buckets == null) {
                    Initialize();
                }

                if (key.GetType() != _keyType && _keyType != HeterogeneousType) {
                    UpdateHelperFunctions(key.GetType(), key);
                }

                AddOne(key, value);
            } else {
                AddNull(value);
            }
        }

        private void AddOne(object key, object value) {
            if (Add(_buckets, key, value)) {
                _count++;

                if (_count >= (_buckets.Length * Load)) {
                    // grow the hash table
                    EnsureSize((int)(_buckets.Length / Load) * ResizeMultiplier);
                }
            }
        }

        private void UpdateHelperFunctions(Type t, object key) {
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
                } else if(t == typeof(Type).GetType()) {    // this odd check checks for RuntimeType.
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
                _buckets = (Bucket[])_buckets.Clone();
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

        private void EnsureSize(int newSize) {
            if (_buckets.Length >= newSize) {
                return;
            }

            Bucket[] oldBuckets = _buckets;
            Bucket[] newBuckets = new Bucket[newSize];

            for (int i = 0; i < oldBuckets.Length; i++ ) {
                Bucket curBucket = oldBuckets[i];
                if (curBucket.Key != null && curBucket.Key != _removed) {
                    AddWorker(newBuckets, curBucket.Key, curBucket.Value, curBucket.HashCode);
                }
            }

            _buckets = newBuckets;
        }

        public override void EnsureCapacityNoLock(int size) {
            if (_buckets == null) {
                _buckets = new Bucket[(int)(size / Load) + 1];
            } else {
                EnsureSize((int)(size / Load));
            }
        }

        /// <summary>
        /// Initializes the buckets to their initial capacity, the caller
        /// must check if the buckets are empty first.
        /// </summary>
        private void Initialize() {
            _buckets = new Bucket[InitialBucketSize];
        }

        /// <summary>
        /// Add helper that works over a single set of buckets.  Used for
        /// both the normal add case as well as the resize case.
        /// </summary>
        private bool Add(Bucket[] buckets, object key, object value) {
            int hc = Hash(key);

            return AddWorker(buckets, key, value, hc);
        }

        /// <summary>
        /// Add helper which adds the given key/value (where the key is not null) with
        /// a pre-computed hash code.
        /// </summary>
        protected bool AddWorker(Bucket[] buckets, object/*!*/ key, object value, int hc) {
            Debug.Assert(key != null);

            Debug.Assert(_count < buckets.Length);
            int startIndex = hc % buckets.Length;

            // scan forward for matching key first
            int index = startIndex;
            int firstUsableIndex = -1;
            for (; ; ) {
                Bucket cur = buckets[index];
                if (cur.Key == null) {
                    // no entry was ever here, nothing more to probe
                    if (firstUsableIndex == -1) {
                        firstUsableIndex = index;
                    }
                    break;
                } else if (cur.Key == _removed) {
                    // we recycled this bucket, so need to continue walking to see if a following bucket matches
                    if (firstUsableIndex == -1) {
                        // retain the index of the first recycled bucket, in case we need it later
                        firstUsableIndex = index;
                    }
                } else if (Object.ReferenceEquals(key, cur.Key) || (cur.HashCode == hc && _eqFunc(key, cur.Key))) {
                    // this bucket is a key match
                    _version++;
                    buckets[index].Value = value;
                    return false;
                }

                // keep walking
                index = ProbeNext(buckets, index);

                // if we ended up doing a full scan, then this means the key is not already in use and there are
                // only recycled buckets available -- nothing more to probe
                if (index == startIndex) {
                    break;
                }
            }

            // the key wasn't found, but we did find a fresh or recycled (unused) bucket
            _version++;
            buckets[firstUsableIndex].HashCode = hc;
            buckets[firstUsableIndex].Value = value;
            buckets[firstUsableIndex].Key = key;

            return true;
        }

       private static int ProbeNext(Bucket[] buckets, int index) {
           // probe to next bucket               
           index++;
           if (index == buckets.Length) {
               index = 0;
           }
           return index;
       }

        /// <summary>
        /// Removes an entry from the dictionary and returns true if the
        /// entry was removed or false.
        /// </summary>
       public override bool Remove(ref DictionaryStorage storage, object key) {
           return Remove(key);
        }

       public bool Remove(object key) {
           object dummy;
           return TryRemoveValue(key, out dummy);
       }

        /// <summary>
        /// Removes an entry from the dictionary and returns true if the
        /// entry was removed or false.  The key will always be hashed
        /// so if it is unhashable an exception will be thrown - even
        /// if the dictionary has no buckets.
        /// </summary>
        internal bool RemoveAlwaysHash(object key) {            
            lock (this) {
                object dummy;
                if (key == null) {
                    return TryRemoveNull(out dummy);
                }

                return TryRemoveNoLock(key, out dummy);
            }
        }

        public override bool TryRemoveValue(ref DictionaryStorage storage, object key, out object value) {
            return TryRemoveValue(key, out value);
        }

        public bool TryRemoveValue(object key, out object value) {
            lock (this) {
                if (key == null) {
                    return TryRemoveNull(out value);
                }

                if (_count == 0) {
                    value = null;
                    return false;
                }

                return TryRemoveNoLock(key, out value);
            }
        }

        private bool TryRemoveNull(out object value) {
            if (_nullValue != null) {
                value = _nullValue.Value;
                _nullValue = null;
                return true;
            } else {
                value = null;
                return false;
            }
        }

        private bool TryRemoveNoLock(object/*!*/ key, out object value) {
            Debug.Assert(key != null);

            Func<object, int> hashFunc;
            Func<object, object, bool> eqFunc;
            if (key.GetType() == _keyType || _keyType == HeterogeneousType) {
                hashFunc = _hashFunc;
                eqFunc = _eqFunc;
            } else {
                hashFunc = _genericHash;
                eqFunc = _genericEquals;
            }

            int hc = hashFunc(key) & Int32.MaxValue;

            return TryRemoveNoLock(key, eqFunc, hc, out value);
        }

        protected bool TryRemoveNoLock(object/*!*/ key, Func<object, object, bool> eqFunc, int hc, out object value) {
            Debug.Assert(key != null);

            if (_buckets == null) {
                value = null;
                return false;
            }

            int index = hc % _buckets.Length;
            int startIndex = index;
            do {
                Bucket bucket = _buckets[index];
                if (bucket.Key == null) {
                    break;
                } else if (
                    Object.ReferenceEquals(key, bucket.Key) ||
                    (bucket.Key != _removed &&
                    bucket.HashCode == hc &&
                    eqFunc(key, bucket.Key))) {
                    value = bucket.Value;
                    _version++;
                    _buckets[index].Key = _removed;
#if NETSTANDARD
                    Interlocked.MemoryBarrier();
#else
                    Thread.MemoryBarrier();
#endif
                    _buckets[index].Value = null;
                    _count--;

                    return true;
                }

                index = ProbeNext(_buckets, index);
            } while (index != startIndex);
            value = null;
            return false;
        }

        /// <summary>
        /// Checks to see if the key exists in the dictionary.
        /// </summary>
        public override bool Contains(object key) {
            if (!PythonContext.IsHashable(key))
                throw PythonOps.TypeErrorForUnhashableObject(key);
            object dummy;
            return TryGetValue(key, out dummy);
        }

        /// <summary>
        /// Trys to get the value associated with the given key and returns true
        /// if it's found or false if it's not present.
        /// </summary>
        public override bool TryGetValue(object key, out object value) {
            if (key != null) {
                return TryGetValue(_buckets, key, out value);
            }

            NullValue nv = _nullValue;
            if (nv != null) {
                value = nv.Value;
                return true;
            }

            value = null;
            return false;
        }

        /// <summary>
        /// Static helper to try and get the value from the dictionary.
        /// 
        /// Used so the value lookup can run against a buckets while a writer
        /// replaces the buckets.
        /// </summary>
        private bool TryGetValue(Bucket[] buckets, object/*!*/ key, out object value) {
            Debug.Assert(key != null);

            if (_count > 0 && buckets != null) {
                int hc;
                Func<object, object, bool> eqFunc;
                if (key.GetType() == _keyType || _keyType == HeterogeneousType) {
                    hc = _hashFunc(key) & Int32.MaxValue;
                    eqFunc = _eqFunc;
                } else {
                    hc = _genericHash(key) & Int32.MaxValue;
                    eqFunc = _genericEquals;
                }

                return TryGetValue(buckets, key, hc, eqFunc, out value);
            }

            value = null;
            return false;
        }

        protected static bool TryGetValue(Bucket[] buckets, object key, int hc, Func<object, object, bool> eqFunc, out object value) {
            int index = hc % buckets.Length;
            int startIndex = index;
            do {
                Bucket bucket = buckets[index];
                if (bucket.Key == null) {
                    break;
                } else if (
                    Object.ReferenceEquals(key, bucket.Key) ||
                    (bucket.Key != _removed &&
                    bucket.HashCode == hc &&
                    eqFunc(key, bucket.Key))) {
                    value = bucket.Value;
                    return true;
                }

                index = ProbeNext(buckets, index);
            } while (startIndex != index);

            value = null;
            return false;
        }

        /// <summary>
        /// Returns the number of key/value pairs currently in the dictionary.
        /// </summary>
        public override int Count {
            get {
                int res = _count;
                if (_nullValue != null) {
                    res++;
                }
                return res;
            }
        }

        /// <summary>
        /// Clears the contents of the dictionary.
        /// </summary>
        public override void Clear(ref DictionaryStorage storage) {
            Clear();
        }

        public void Clear() {
            lock (this) {
                if (_buckets != null) {
                    _version++;
                    _buckets = new Bucket[8];
                    _count = 0;
                }
                _nullValue = null;
            }
        }

        public override List<KeyValuePair<object, object>> GetItems() {
            lock (this) {
                List<KeyValuePair<object, object>> res = new List<KeyValuePair<object, object>>(_count + (_nullValue != null ? 1 : 0));
                if (_count > 0) {
                    for (int i = 0; i < _buckets.Length; i++) {
                        Bucket curBucket = _buckets[i];
                        if (curBucket.Key != null && curBucket.Key != _removed) {
                            res.Add(new KeyValuePair<object, object>(curBucket.Key, curBucket.Value));
                        }
                    }
                }

                if (_nullValue != null) {
                    res.Add(new KeyValuePair<object, object>(null, _nullValue.Value));
                }
                return res;
            }
        }

        public override IEnumerator<KeyValuePair<object, object>> GetEnumerator() {
            lock (this) {
                if (_count > 0) {
                    for (int i = 0; i < _buckets.Length; i++) {
                        Bucket curBucket = _buckets[i];
                        if (curBucket.Key != null && curBucket.Key != _removed) {
                            yield return new KeyValuePair<object, object>(curBucket.Key, curBucket.Value);
                        }
                    }
                }

                if (_nullValue != null) {
                    yield return new KeyValuePair<object, object>(null, _nullValue.Value);
                }
            }
        }

        public override IEnumerable<object>/*!*/ GetKeys() {
            Bucket[] buckets = _buckets;
            lock (this) {
                object[] res = new object[Count];
                int index = 0;
                if (buckets != null) {
                    for (int i = 0; i < buckets.Length; i++) {
                        Bucket curBucket = buckets[i];
                        if (curBucket.Key != null && curBucket.Key != _removed) {
                            res[index++] = curBucket.Key;
                        }
                    }
                }

                if (_nullValue != null) {
                    res[index++] = null;
                }

                return res;
            }
        }

        public override bool HasNonStringAttributes() {
            lock (this) {
                var nullValue = _nullValue;
                if (nullValue != null && !(nullValue.Value is string)) {
                    return true;
                }
                if (_keyType != typeof(string) && _keyType != null && _count > 0) {
                    for (int i = 0; i < _buckets.Length; i++) {
                        Bucket curBucket = _buckets[i];
                        
                        if (curBucket.Key != null && curBucket.Key != _removed && !(curBucket.Key is string)) {
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
                if (_buckets == null) {
                    if (_nullValue != null) {
                        return new CommonDictionaryStorage(null, 1, _keyType, _hashFunc, _eqFunc, new NullValue(_nullValue.Value));
                    }

                    return new CommonDictionaryStorage();
                }

                Bucket[] resBuckets = new Bucket[_buckets.Length];
                for (int i = 0; i < _buckets.Length; i++) {
                    if (_buckets[i].Key != null) {
                        resBuckets[i] = _buckets[i];
                    }
                }

                NullValue nv = null;
                if (_nullValue != null) {
                    nv = new NullValue(_nullValue.Value);
                }
                return new CommonDictionaryStorage(resBuckets, _count, _keyType, _hashFunc, _eqFunc, nv);
            }
        }

        public override void CopyTo(ref DictionaryStorage/*!*/ into) {
            into = CopyTo(into);
        }

        public DictionaryStorage CopyTo(DictionaryStorage into) {
            Debug.Assert(into != null);

            if (_buckets != null) {
                using (new OrderedLocker(this, into)) {
                    CommonDictionaryStorage commonInto = into as CommonDictionaryStorage;
                    if (commonInto != null) {
                        CommonCopyTo(commonInto);
                    } else {
                        UncommonCopyTo(ref into);
                    }
                }
            }

            var nullValue = _nullValue;
            if (nullValue != null) {
                into.Add(ref into, null, nullValue.Value);
            }
            return into;
        }

        private void CommonCopyTo(CommonDictionaryStorage into) {
            if (into._buckets == null) {
                into._buckets = new Bucket[_buckets.Length];
            } else {
                int curSize = into._buckets.Length;
                int newSize = (int)((_count + into._count) / Load) + 2;
                while (curSize < newSize) {
                    curSize *= ResizeMultiplier;
                }
                into.EnsureSize(curSize);
            }

            if (into._keyType == null) {
                into._keyType = _keyType;
                into._hashFunc = _hashFunc;
                into._eqFunc = _eqFunc;
            } else if (into._keyType != _keyType) {
                into.SetHeterogeneousSites();
            }

            for (int i = 0; i < _buckets.Length; i++) {
                Bucket curBucket = _buckets[i];

                if (curBucket.Key != null &&
                    curBucket.Key != _removed &&
                    into.AddWorker(into._buckets, curBucket.Key, curBucket.Value, curBucket.HashCode)) {
                    into._count++;
                }
            }
        }

        private void UncommonCopyTo(ref DictionaryStorage into) {
            for (int i = 0; i < _buckets.Length; i++) {
                Bucket curBucket = _buckets[i];
                if (curBucket.Key != null && curBucket.Key != _removed) {
                    into.AddNoLock(ref into, curBucket.Key, curBucket.Value);
                }
            }
        }

        /// <summary>
        /// Helper to hash the given key w/ support for null.
        /// </summary>
        private int Hash(object key) {
            if (key is string) return key.GetHashCode() & Int32.MaxValue;

            return _hashFunc(key) & Int32.MaxValue;
        }

        /// <summary>
        /// Used to store a single hashed key/value.
        /// 
        /// Bucket is not serializable because it stores the computed hash
        /// code which could change between serialization and deserialization.
        /// </summary>
        protected struct Bucket {
            public object Key;          // the key to be hashed
            public object Value;        // the value associated with the key
            public int HashCode;        // the hash code of the contained key.

            public Bucket(int hashCode, object key, object value) {
                HashCode = hashCode;
                Key = key;
                Value = value;
            }
        }

        #region Hash/Equality Delegates

        private static int PrimitiveHash(object o) {
            return o.GetHashCode();
        }

        private static int IntHash(object o) {
            return (int)o;
        }

        private static int DoubleHash(object o) {
            return DoubleOps.__hash__((double)o);
        }

        private static int GenericHash(object o) {
            return PythonOps.Hash(DefaultContext.Default, o);
        }

        private static int TupleHash(object o) {
            return ((IStructuralEquatable)o).GetHashCode(
                DefaultContext.DefaultPythonContext.EqualityComparerNonGeneric
            );
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
            return PythonOps.EqualRetBool(o1, o2);
        }

        #endregion

        [Serializable]
        private class NullValue {
            public object Value;

            public NullValue(object value) {
                Value = value;
            }
        }
#if FEATURE_SERIALIZATION

        /// <summary>
        /// Special marker NullValue used during deserialization to not add
        /// an extra field to the dictionary storage type.
        /// </summary>
        private class DeserializationNullValue : NullValue {
            public SerializationInfo/*!*/ SerializationInfo {
                get {
                    return (SerializationInfo)Value;
                }
            }

            public DeserializationNullValue(SerializationInfo info)
                : base(info) {
            }
        }

        private DeserializationNullValue GetDeserializationBucket() {
            return _nullValue  as DeserializationNullValue;
        }

        #region ISerializable Members

        public void GetObjectData(SerializationInfo info, StreamingContext context) {
            info.AddValue("buckets", GetItems());
            info.AddValue("nullvalue", _nullValue);
        }

        #endregion

        #region IDeserializationCallback Members

        void IDeserializationCallback.OnDeserialization(object sender) {
            DeserializationNullValue bucket = GetDeserializationBucket();
            if (bucket == null) {
                // we've received multiple OnDeserialization callbacks, only 
                // deserialize after the 1st one
                return;
            }

            SerializationInfo info = bucket.SerializationInfo;
            _buckets = null;
            _nullValue = null;

            var buckets = (List<KeyValuePair<object, object>>)info.GetValue("buckets", typeof(List<KeyValuePair<object, object>>));

            foreach (KeyValuePair<object, object> kvp in buckets) {
                Add(kvp.Key, kvp.Value);
            }

            NullValue nullVal = null;
            try {
                nullVal = (NullValue)info.GetValue("nullvalue", typeof(NullValue));
            }
            catch (SerializationException) {
                // for compatibility with dictionary serialized in 2.6.
            }
            if (nullVal != null) {
                _nullValue = new NullValue(nullVal);
            }
        }

        #endregion
#endif
    }

}
