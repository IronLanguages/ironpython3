// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime.Types {
    /// <summary>
    /// Provides custom, versioned, dictionary access for instances.
    /// </summary>
    /// <remarks>
    /// Each class can allocate a version for instance storage using the 
    /// CustomInstanceDictionaryStorage.AllocateInstance method.  The version allocated
    /// is dependent upon the names which are likely to appear in the instance 
    /// dictionary.  Currently these names are calculated by collecting the names
    /// that are assigned to during the __init__ method and combining these with
    /// all such names in the types MRO.
    /// 
    /// When creating the dictionary for storing instance values the class can then create
    /// a PythonDictionary backed by a CustomInstanceDictionaryStorage with it's
    /// version.  When doing a get/set optimized code can then be produced that
    /// verifies we have CustomInstanceDictionaryStorage and it has the 
    /// correct version.  If we have a matching dictionary then gets/sets can turn
    /// into simple array accesses rather than dictionary gets/sets.  For programs
    /// which access a large number of instance variables this can dramatically
    /// speed up the program.
    /// 
    /// TODO: Should we attempt to unify all versions which share the same keys?
    /// </remarks>
    [Serializable]
    internal sealed class CustomInstanceDictionaryStorage : StringDictionaryStorage {
        private readonly int _keyVersion;
        private readonly string[] _extraKeys;
        private readonly object?[] _values;
        [MultiRuntimeAware]
        private static int _namesVersion;

        internal static int AllocateVersion() {
            return Interlocked.Increment(ref _namesVersion);
        }

        public CustomInstanceDictionaryStorage(string[] extraKeys, int keyVersion) {
            _extraKeys = extraKeys;
            _keyVersion = keyVersion;
            _values = new object[extraKeys.Length];
            for (int i = 0; i < _values.Length; i++) {
                _values[i] = Uninitialized.Instance;
            }
        }

        public override void Add(ref DictionaryStorage storage, object? key, object? value) {
            int ikey = FindKey(key);
            if (ikey != -1) {
                _values[ikey] = value;
                return;
            }

            base.Add(ref storage, key, value);
        }

        public override void AddNoLock(ref DictionaryStorage storage, object? key, object? value) {
            int ikey = FindKey(key);
            if (ikey != -1) {
                _values[ikey] = value;
                return;
            }

            base.AddNoLock(ref storage, key, value);
        }

        public override bool Contains(object? key) {
            int ikey = FindKey(key);
            if (ikey != -1) {
                return _values[ikey] != Uninitialized.Instance;
            }

            return base.Contains(key);
        }

        public override bool Remove(ref DictionaryStorage storage, object? key) {
            int ikey = FindKey(key);
            if (ikey != -1) {
                if (Interlocked.Exchange<object?>(ref _values[ikey], Uninitialized.Instance) != Uninitialized.Instance) {
                    return true;
                }

                return false;
            }

            return base.Remove(ref storage, key);
        }

        public override bool TryGetValue(object? key, out object? value) {
            int ikey = FindKey(key);
            if (ikey != -1) {
                value = _values[ikey];
                if (value != Uninitialized.Instance) {
                    return true;
                }

                value = null;
                return false;
            }

            return base.TryGetValue(key, out value);
        }

        public override int Count {
            get { 
                int count = base.Count;
                foreach (object? o in _values) {
                    if (o != Uninitialized.Instance) {
                        count++;
                    }
                }

                return count;
            }
        }

        public override void Clear(ref DictionaryStorage storage) {
            for (int i = 0; i < _values.Length; i++) {
                _values[i] = Uninitialized.Instance;
            }

            base.Clear(ref storage);
        }

        public override List<KeyValuePair<object?, object?>> GetItems() {
            List<KeyValuePair<object?, object?>> res = base.GetItems();

            for (int i = 0; i < _extraKeys.Length; i++) {
                if (!String.IsNullOrEmpty(_extraKeys[i]) && _values[i] != Uninitialized.Instance) {
                    res.Add(new KeyValuePair<object?, object?>(_extraKeys[i], _values[i]));
                }
            }

            return res;
        }

        public int KeyVersion {
            get {
                return _keyVersion;
            }
        }

        public int FindKey(object? key) {
            if (key is string strKey) {
                return FindKey(strKey);
            }

            return -1;
        }

        public int FindKey(string key) {
            for (int i = 0; i < _extraKeys.Length; i++) {
                if (_extraKeys[i] == key) {
                    return i;
                }
            }
            return -1;
        }

        public bool TryGetValue(int index, out object? value) {
            value = _values[index];
            return value != Uninitialized.Instance;
        }        

        public void SetExtraValue(int index, object? value) {
            _values[index] = value;
        }
    }
}
