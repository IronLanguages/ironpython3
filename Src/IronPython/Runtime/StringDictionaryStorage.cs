// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime {
    [Serializable]
    internal class StringDictionaryStorage : DictionaryStorage {
        private Dictionary<string, object?>? _data;
        private static readonly object _nullkey = new();

        public StringDictionaryStorage() {
        }

        public StringDictionaryStorage(int count) {
            _data = new Dictionary<string, object?>(count, StringComparer.Ordinal);
        }

        public override void Add(ref DictionaryStorage storage, object? key, object? value) {
            Add(key, value);
        }

        public void Add(object? key, object? value) {
            lock (this) {
                AddNoLock(key, value);
            }
        }

        public override void AddNoLock(ref DictionaryStorage storage, object? key, object? value) {
            AddNoLock(key, value);
        }

        public void AddNoLock(object? key, object? value) {
            EnsureData();

            if (key is string strKey && strKey.Length != 0) {
                _data[strKey] = value;
            } else {
                GetObjectDictionary()[ToNonNullKey(key)] = value;
            }
        }

        public override bool Contains(object? key) {
            if (_data is null) return false;

            lock (this) {
                if (key is string strKey && strKey.Length != 0) {
                    return _data.ContainsKey(strKey);
                } else {
                    Dictionary<object, object?>? dict = TryGetObjectDictionary();
                    if (dict is not null) {
                        return dict.ContainsKey(ToNonNullKey(key));
                    }

                    return false;
                }
            }
        }

        public override bool Remove(ref DictionaryStorage storage, object? key) {
            return Remove(key);
        }

        public bool Remove(object? key) {
            if (_data is null) return false;

            lock (this) {
                if (key is string strKey && strKey.Length != 0) {
                    return _data.Remove(strKey);
                } else {
                    Dictionary<object, object?>? dict = TryGetObjectDictionary();
                    if (dict is not null) {
                        return dict.Remove(ToNonNullKey(key));
                    }

                    return false;
                }
            }
        }

        public override bool TryGetValue(object? key, out object? value) {
            if (_data is not null) {
                lock (this) {
                    if (key is string strKey && strKey.Length != 0) {
                        return _data.TryGetValue(strKey, out value);
                    }

                    Dictionary<object, object?>? dict = TryGetObjectDictionary();
                    if (dict is not null) {
                        return dict.TryGetValue(ToNonNullKey(key), out value);
                    }
                }
            }

            value = null;
            return false;
        }

        public override int Count {
            get {
                if (_data is null) return 0;

                lock (this) {
                    if (_data is null) return 0;

                    int count = _data.Count;
                    Dictionary<object, object?>? dict = TryGetObjectDictionary();
                    if (dict is not null) {
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

        public override List<KeyValuePair<object?, object?>> GetItems() {
            var res = new List<KeyValuePair<object?, object?>>();

            if (_data is not null) {
                lock (this) {
                    foreach (KeyValuePair<string, object?> kvp in _data) {
                        if (kvp.Key.Length == 0) continue;

                        res.Add(new KeyValuePair<object?, object?>(kvp.Key, kvp.Value));
                    }

                    Dictionary<object, object?>? dataDict = TryGetObjectDictionary();
                    if (dataDict is not null) {
                        foreach (KeyValuePair<object, object?> kvp in GetObjectDictionary()) {
                            res.Add(new KeyValuePair<object?, object?>(FromNonNullKey(kvp.Key), kvp.Value));
                        }
                    }
                }
            }

            return res;
        }

        public override bool HasNonStringAttributes() {
            if (_data is not null) {
                lock (this) {
                    Dictionary<object, object?>? dataDict = TryGetObjectDictionary();
                    if (dataDict is not null) {
                        foreach (object o in dataDict.Keys) {
                            if (o is not Extensible<string> and not string) {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private Dictionary<object, object?>? TryGetObjectDictionary() {
            if (_data is not null) {
                if (_data.TryGetValue(string.Empty, out object? dict)) {
                    Debug.Assert(dict is not null);
                    return (Dictionary<object, object?>?)dict;
                }
            }

            return null;
        }

        private Dictionary<object, object?> GetObjectDictionary() {
            lock (this) {
                EnsureData();

                if (_data.TryGetValue(string.Empty, out object? dict) && dict is not null) {
                    return (Dictionary<object, object?>)dict;
                }

                var res = new Dictionary<object, object?>();
                _data[string.Empty] = res;

                return res;
            }
        }

        [MemberNotNull(nameof(_data))]
        private void EnsureData() {
            _data ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        private object ToNonNullKey(object? key) => key is null ? _nullkey : key;

        private object? FromNonNullKey(object key) => key == _nullkey? null : key;
    }
}
