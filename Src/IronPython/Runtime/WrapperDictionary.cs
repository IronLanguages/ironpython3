// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.Scripting;
using Microsoft.Scripting.Actions;

namespace IronPython.Runtime {
    [Serializable]
    internal class WrapperDictionaryStorage : DictionaryStorage {
        private readonly TopNamespaceTracker/*!*/ _data;

        public WrapperDictionaryStorage(TopNamespaceTracker/*!*/ data) {
            _data = data;
        }
      
        public override void Add(ref DictionaryStorage storage, object key, object value) {
            throw CannotModifyNamespaceDict();
        }

        private static InvalidOperationException CannotModifyNamespaceDict() {
            return new InvalidOperationException("cannot modify namespace dictionary");
        }

        public override bool Contains(object key) {
            if (key is string strKey) {
                return _data.ContainsKey(strKey);
            }

            return false;
        }

        public override bool Remove(ref DictionaryStorage storage, object key) {
            throw CannotModifyNamespaceDict();
        }

        public override bool TryGetValue(object key, out object value) {
            if (key is string strKey) {
                return _data.TryGetValue(strKey, out value);
            }

            value = null;
            return false;
        }

        public override int Count {
            get {
                return _data.Count;
            }
        }

        public override void Clear(ref DictionaryStorage storage) {
            throw CannotModifyNamespaceDict();
        }

        public override List<KeyValuePair<object, object>> GetItems() {
            var res = new List<KeyValuePair<object, object>>(_data.Count);
            foreach (var item in _data) {
                res.Add(new KeyValuePair<object, object>(item.Key, item.Value));
            }
            return res;
        }
    }
}
