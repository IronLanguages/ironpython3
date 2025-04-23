// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Binding;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime {
    /// <summary>
    /// Provides both helpers for implementing Python dictionaries as well
    /// as providing public methods that should be exposed on all dictionary types.
    /// 
    /// Currently these are published on IDictionary&lt;object, object&gt;
    /// </summary>
    public static class DictionaryOps {
        #region Dictionary Public API Surface        

        // Dictionary has an odd not-implemented check to support custom dictionaries and therefore
        // needs a custom __eq__ / __ne__ implementation.

        public static string/*!*/ __repr__(CodeContext/*!*/ context, IDictionary<object, object> self) {
            List<object> infinite = PythonOps.GetAndCheckInfinite(self);
            if (infinite == null) {
                return "{...}";
            }

            int index = infinite.Count;
            infinite.Add(self);
            try {
                StringBuilder buf = new StringBuilder();
                buf.Append("{");
                bool first = true;
                foreach (KeyValuePair<object, object> kv in self) {
                    if (first) first = false;
                    else buf.Append(", ");

                    if (CustomStringDictionary.IsNullObject(kv.Key))
                        buf.Append("None");
                    else
                        buf.Append(PythonOps.Repr(context, kv.Key));
                    buf.Append(": ");

                    try {
                        PythonOps.FunctionPushFrame(context.LanguageContext);
                        buf.Append(PythonOps.Repr(context, kv.Value));
                    } finally {
                        PythonOps.FunctionPopFrame();
                    }
                }
                buf.Append("}");
                return buf.ToString();
            } finally {
                System.Diagnostics.Debug.Assert(index == infinite.Count - 1);
                infinite.RemoveAt(index);
            }
        }

        public static object get(PythonDictionary self, object key, object defaultValue = null) {
            if (self.TryGetValueNoMissing(key, out object ret)) return ret;
            return defaultValue;
        }

        private static PythonList ToList(IDictionary<object, object> self) {
            PythonList ret = new PythonList(self.Count);
            foreach (KeyValuePair<object, object> kv in self) {
                ret.AddNoLock(PythonTuple.MakeTuple(kv.Key, kv.Value));
            }
            return ret;
        }

        public static object pop(PythonDictionary self, object key) {
            //??? perf won't match expected Python perf
            if (self.TryGetValueNoMissing(key, out object ret)) {
                self.RemoveDirect(key);
                return ret;
            } else {
                throw PythonOps.KeyError(key);
            }
        }

        public static object pop(PythonDictionary self, object key, object defaultValue) {
            //??? perf won't match expected Python perf
            if (self.TryGetValueNoMissing(key, out object ret)) {
                self.RemoveDirect(key);
                return ret;
            } else {
                return defaultValue;
            }
        }

        public static PythonTuple popitem(PythonDictionary self) {
            using IEnumerator<KeyValuePair<object, object>> ie = self.GetEnumerator();
            if (ie.MoveNext()) {
                object key = ie.Current.Key;
                object val = ie.Current.Value;
                self.RemoveDirect(key);
                return PythonTuple.MakeTuple(key, val);
            }
            throw PythonOps.KeyError("dictionary is empty");
        }

        public static object setdefault(PythonDictionary self, object key) {
            return setdefault(self, key, null);
        }

        public static object setdefault(PythonDictionary self, object key, object defaultValue) {
            if (self.TryGetValueNoMissing(key, out object ret)) return ret;
            self.SetItem(key, defaultValue);
            return defaultValue;
        }

        public static void update(CodeContext/*!*/ context, PythonDictionary/*!*/ self, object other) {
            if (other is PythonDictionary pyDict) {
                pyDict._storage.CopyTo(ref self._storage);
            } else {
                SlowUpdate(context, self, other);
            }
        }

        private static void SlowUpdate(CodeContext/*!*/ context, PythonDictionary/*!*/ self, object other) {
            if (other is MappingProxy proxy) {
                update(context, self, proxy.GetDictionary(context));
            } else if (other is IDictionary dict) {
                IDictionaryEnumerator e = dict.GetEnumerator();
                while (e.MoveNext()) {
                    self._storage.Add(ref self._storage, e.Key, e.Value);
                }
            } else if (PythonOps.TryGetBoundAttr(other, "keys", out object keysFunc)) {
                // user defined dictionary
                IEnumerator i = PythonOps.GetEnumerator(context, PythonCalls.Call(context, keysFunc));
                while (i.MoveNext()) {
                    self._storage.Add(ref self._storage, i.Current, PythonOps.GetIndex(context, other, i.Current));
                }
            } else {
                // list of lists (key/value pairs), list of tuples,
                // tuple of tuples, etc...
                IEnumerator i = PythonOps.GetEnumerator(context, other);
                int index = 0;
                while (i.MoveNext()) {
                    if (!AddKeyValue(context, self, i.Current)) {
                        throw PythonOps.ValueError("dictionary update sequence element #{0} has bad length; 2 is required", index);
                    }
                    index++;
                }
            }
        }

        #endregion

        #region Dictionary Helper APIs

        internal static bool TryGetValueVirtual(CodeContext context, PythonDictionary self, object key, ref object DefaultGetItem, out object value) {
            if (self is IPythonObject sdo) {
                Debug.Assert(sdo != null);
                PythonType myType = sdo.PythonType;
                PythonTypeSlot dts;

                if (DefaultGetItem == null) {
                    // lazy init our cached DefaultGetItem
                    TypeCache.Dict.TryLookupSlot(context, "__getitem__", out dts);
                    bool res = dts.TryGetValue(context, self, TypeCache.Dict, out DefaultGetItem);
                    Debug.Assert(res);
                }

                // check and see if it's overridden
                if (myType.TryLookupSlot(context, "__getitem__", out dts)) {
                    dts.TryGetValue(context, self, myType, out object ret);

                    if (ret != DefaultGetItem) {
                        // subtype of dict that has overridden __getitem__
                        // we need to call the user's versions, and handle
                        // any exceptions.
                        try {
                            value = self[key];
                            return true;
                        } catch (KeyNotFoundException) {
                            value = null;
                            return false;
                        }
                    }
                }
            }

            value = null;
            return false;
        }

        internal static bool AddKeyValue(CodeContext context, PythonDictionary self, object o) {
            IEnumerator i = PythonOps.GetEnumerator(context, o); //c.GetEnumerator();
            if (i.MoveNext()) {
                object key = i.Current;
                if (i.MoveNext()) {
                    object value = i.Current;
                    self._storage.Add(ref self._storage, key, value);

                    return !i.MoveNext();
                }
            }
            return false;
        }

        #endregion
    }
}
