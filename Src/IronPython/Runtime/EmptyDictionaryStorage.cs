using System;
using System.Collections.Generic;
using System.Text;

namespace IronPython.Runtime {
    /// <summary>
    /// Singleton used for dictionaries which contain no items.
    /// </summary>
    [Serializable]
    class EmptyDictionaryStorage : DictionaryStorage {
        public static EmptyDictionaryStorage Instance = new EmptyDictionaryStorage();

        private EmptyDictionaryStorage() {
        }

        public override void Add(ref DictionaryStorage storage, object key, object value) {
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

        public override bool Remove(ref DictionaryStorage storage, object key) {
            return false;
        }

        public override void Clear(ref DictionaryStorage storage) {
        }

        public override bool Contains(object key) {
            return false;
        }

        public override bool TryGetValue(object key, out object value) {
            value = null;
            return false;
        }

        public override int Count {
            get { return 0; }
        }

        public override List<KeyValuePair<object, object>> GetItems() {
            return new List<KeyValuePair<object, object>>();
        }

        public override DictionaryStorage Clone() {
            return this;
        }

        public override bool HasNonStringAttributes() {
            return false;
        }
    }
}
