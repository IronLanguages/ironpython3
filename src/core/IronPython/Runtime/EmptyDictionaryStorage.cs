// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;

using IronPython.Runtime.Operations;

namespace IronPython.Runtime {
    /// <summary>
    /// Singleton used for dictionaries which contain no items.
    /// </summary>
    [Serializable]
    internal class EmptyDictionaryStorage : DictionaryStorage {
        public static EmptyDictionaryStorage Instance = new EmptyDictionaryStorage();

        private EmptyDictionaryStorage() { }

        public override void Add(ref DictionaryStorage storage, object? key, object? value) {
            lock (this) {
                if (storage == this) {
                    CommonDictionaryStorage newStorage = new CommonDictionaryStorage();
                    newStorage.AddNoLock(key, value);
                    storage = newStorage;
                    return;
                }
            }

            // race, try again...
            storage.Add(ref storage, key, value);
        }

        public override bool Remove(ref DictionaryStorage storage, object? key) {
            return false;
        }

        public override DictionaryStorage AsMutable(ref DictionaryStorage storage) {
            lock (this) {
                if (storage == this) {
                    return storage = new CommonDictionaryStorage();
                }
            }

            // race, try again...
            return storage.AsMutable(ref storage);
        }

        public override void Clear(ref DictionaryStorage storage) { }

        public override bool Contains(object? key) {
            // make sure argument is valid, do not calculate hash
            if (PythonContext.IsHashable(key)) {
                return false;
            }
            throw PythonOps.TypeErrorForUnhashableObject(key);
        }

        public override bool TryGetValue(object? key, out object? value) {
            value = null;
            return false;
        }

        public override int Count {
            get { return 0; }
        }

        public override List<KeyValuePair<object?, object?>> GetItems() {
            return new List<KeyValuePair<object?, object?>>();
        }

        public override DictionaryStorage Clone() {
            return this;
        }

        public override bool HasNonStringAttributes() {
            return false;
        }
    }
}
