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
using System.Diagnostics;
using Microsoft.Scripting;
using System.Threading;

namespace IronPython.Runtime {
    [Serializable]
    internal class StringDictionaryStorage : DictionaryStorage {
        private Dictionary<string, object> _data;

        public StringDictionaryStorage() {
        }

        public StringDictionaryStorage(int count) {
            _data = new Dictionary<string, object>(count, StringComparer.Ordinal);
        }

        public override void Add(ref DictionaryStorage storage, object key, object value) {
            Add(key, value);
        }

        public void Add(object key, object value) {
            lock (this) {
                AddNoLock(key, value);
            }
        }

        public override void AddNoLock(ref DictionaryStorage storage, object key, object value) {
            AddNoLock(key, value);
        }

        public void AddNoLock(object key, object value) {
            EnsureData();

            string strKey = key as string;
            if (strKey != null) {
                _data[strKey] = value;
            } else {
                GetObjectDictionary()[key] = value;
            }
        }

        public override bool Contains(object key) {
            if (_data == null) return false;

            lock (this) {
                string strKey = key as string;
                if (strKey != null) {
                    return _data.ContainsKey(strKey);
                } else {
                    Dictionary<object, object> dict = TryGetObjectDictionary();
                    if (dict != null) {
                        return dict.ContainsKey(key);
                    }

                    return false;
                }
            }
        }

        public override bool Remove(ref DictionaryStorage storage, object key) {
            return Remove(key);
        }

        public bool Remove(object key) {
            if (_data == null) return false;

            lock (this) {
                string strKey = key as string;
                if (strKey != null) {
                    return _data.Remove(strKey);
                } else {
                    Dictionary<object, object> dict = TryGetObjectDictionary();
                    if (dict != null) {
                        return dict.Remove(key);
                    }

                    return false;
                }
            }
        }

        public override bool TryGetValue(object key, out object value) {
            if (_data != null) {
                lock (this) {
                    string strKey = key as string;
                    if (strKey != null) {
                        return _data.TryGetValue(strKey, out value);
                    }

                    Dictionary<object, object> dict = TryGetObjectDictionary();

                    if (dict != null) {
                        return dict.TryGetValue(key, out value);
                    }
                }
            }

            value = null;
            return false;
        }

        public override int Count {
            get {
                if (_data == null) return 0;

                lock (this) {
                    if (_data == null) return 0;

                    int count = _data.Count;
                    Dictionary<object, object> dict = TryGetObjectDictionary();
                    if (dict != null) {
                        // plus the object keys, minus the object dictionary key
                        count += dict.Count - 1;
                    }
                    return count;
                }
            }
        }

        public override void Clear(ref DictionaryStorage storage) {
            _data = null;
        }

        public override List<KeyValuePair<object, object>> GetItems() {
            List<KeyValuePair<object, object>> res = new List<KeyValuePair<object, object>>();

            if (_data != null) {
                lock (this) {
                    foreach (KeyValuePair<string, object> kvp in _data) {
                        if (String.IsNullOrEmpty(kvp.Key)) continue;

                        res.Add(new KeyValuePair<object, object>(kvp.Key, kvp.Value));
                    }

                    Dictionary<object, object> dataDict = TryGetObjectDictionary();
                    if (dataDict != null) {
                        foreach (KeyValuePair<object, object> kvp in GetObjectDictionary()) {
                            res.Add(kvp);
                        }
                    }
                }
            }

            return res;
        }

        public override bool HasNonStringAttributes() {
            if (_data != null) {
                lock (this) {
                    if (TryGetObjectDictionary() != null) {
                        return true;
                    }
                }
            }
            
            return false;
        }

        private Dictionary<object, object> TryGetObjectDictionary() {
            if (_data != null) {
                object dict;
                if (_data.TryGetValue(string.Empty, out dict)) {
                    return (Dictionary<object, object>)dict;
                }
            }

            return null;
        }

        private Dictionary<object, object> GetObjectDictionary() {
            lock (this) {
                EnsureData();

                object dict;
                if (_data.TryGetValue(string.Empty, out dict)) {
                    return (Dictionary<object, object>)dict;
                }

                Dictionary<object, object> res = new Dictionary<object, object>();
                _data[string.Empty] = res;

                return res;
            }
        }

        private void EnsureData() {
            if (_data == null) {
                _data = new Dictionary<string, object>();
            }
        }
    }
}
