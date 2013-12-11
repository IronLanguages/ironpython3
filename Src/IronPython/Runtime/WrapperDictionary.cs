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
using System.Collections.Generic;
using Microsoft.Scripting;
using Microsoft.Scripting.Actions;

namespace IronPython.Runtime {
    [Serializable]
    internal class WrapperDictionaryStorage : DictionaryStorage {
        private TopNamespaceTracker/*!*/ _data;

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
            string strKey = key as string;
            if (strKey != null) {
                return _data.ContainsKey(strKey);
            }

            return false;
        }

        public override bool Remove(ref DictionaryStorage storage, object key) {
            throw CannotModifyNamespaceDict();
        }

        public override bool TryGetValue(object key, out object value) {
            string strKey = key as string;
            if (strKey != null) {
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
