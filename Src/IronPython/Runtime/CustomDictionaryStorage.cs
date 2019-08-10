// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime {
    internal abstract class CustomDictionaryStorage : DictionaryStorage {
        private readonly CommonDictionaryStorage/*!*/ _storage = new CommonDictionaryStorage();

        public override void Add(ref DictionaryStorage storage, object key, object value) {
            Add(key, value);
        }

        public override void AddNoLock(ref DictionaryStorage storage, object key, object value) {
            if (key is string && TrySetExtraValue((string)key, value)) {
                return;
            }
            _storage.AddNoLock(ref storage, key, value);
        }

        public void Add(object key, object value) {
            if (key is string && TrySetExtraValue((string)key, value)) {
                return;
            }
            _storage.Add(key, value);
        }

        public override bool Contains(object key) {
            object dummy;
            if (key is string && TryGetExtraValue((string)key, out dummy)) {
                return dummy != Uninitialized.Instance;
            }

            return _storage.Contains(key);
        }

        public override bool Remove(ref DictionaryStorage storage, object key) {
            return Remove(key);
        }

        public bool Remove(object key) {
            if (key is string) {
                return TryRemoveExtraValue((string)key) ?? _storage.Remove(key);

            }
            return _storage.Remove(key);
        }

        public override bool TryGetValue(object key, out object value) {
            if (key is string && TryGetExtraValue((string)key, out value)) {
                return value != Uninitialized.Instance;
            }

            return _storage.TryGetValue(key, out value);
        }

        public override int Count {
            get { return GetItems().Count; }
        }

        public override void Clear(ref DictionaryStorage storage) {
            _storage.Clear(ref storage);
            foreach (var item in GetExtraItems()) {
                TryRemoveExtraValue(item.Key);
            }
        }

        public override List<KeyValuePair<object, object>> GetItems() {
            List<KeyValuePair<object, object>> res = _storage.GetItems();

            foreach (var item in GetExtraItems()) {
                res.Add(new KeyValuePair<object, object>(item.Key, item.Value));
            }

            return res;
        }

        /// <summary>
        /// Gets all of the extra names and values stored in the dictionary.
        /// </summary>
        protected abstract IEnumerable<KeyValuePair<string, object>> GetExtraItems();

        /// <summary>
        /// Attemps to sets a value in the extra keys.  Returns true if the value is set, false if 
        /// the value is not an extra key.
        /// </summary>
        protected abstract bool TrySetExtraValue(string key, object value);

        /// <summary>
        /// Attempts to get a value from the extra keys.  Returns true if the value is an extra
        /// key and has a value.  False if it is not an extra key or doesn't have a value.
        /// </summary>
        protected abstract bool TryGetExtraValue(string key, out object value);

        /// <summary>
        /// Attempts to remove the key.  Returns true if the key is removed, false
        /// if the key was not removed, or null if the key is not an extra key.
        /// </summary>
        protected abstract bool? TryRemoveExtraValue(string key);
    }
}
