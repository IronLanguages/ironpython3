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
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using System.Text;

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

                    buf.Append(PythonOps.Repr(context, kv.Value));
                }
                buf.Append("}");
                return buf.ToString();
            } finally {
                System.Diagnostics.Debug.Assert(index == infinite.Count - 1);
                infinite.RemoveAt(index);
            }
        }

        public static object get(PythonDictionary self, object key) {
            return get(self, key, null);
        }

        public static object get(PythonDictionary self, object key, object defaultValue) {
            object ret;
            if (self.TryGetValueNoMissing(key, out ret)) return ret;
            return defaultValue;
        }

        public static bool has_key(IDictionary<object, object> self, object key) {
            return self.ContainsKey(key);
        }

        public static List items(IDictionary<object, object> self) {
            List ret = PythonOps.MakeEmptyList(self.Count);
            foreach (KeyValuePair<object, object> kv in self) {
                ret.AddNoLock(PythonTuple.MakeTuple(kv.Key, kv.Value));
            }
            return ret;
        }

        public static IEnumerator iteritems(IDictionary<object, object> self) {
            return ((IEnumerable)items(self)).GetEnumerator();
        }

        public static IEnumerator iterkeys(IDictionary<object, object> self) {
            return ((IEnumerable)keys(self)).GetEnumerator();
        }

        public static List keys(IDictionary<object, object> self) {
            return PythonOps.MakeListFromSequence(self.Keys);
        }

        public static object pop(PythonDictionary self, object key) {
            //??? perf won't match expected Python perf
            object ret;
            if (self.TryGetValueNoMissing(key, out ret)) {
                self.RemoveDirect(key);
                return ret;
            } else {
                throw PythonOps.KeyError(key);
            }
        }

        public static object pop(PythonDictionary self, object key, object defaultValue) {
            //??? perf won't match expected Python perf
            object ret;
            if (self.TryGetValueNoMissing(key, out ret)) {
                self.RemoveDirect(key);
                return ret;
            } else {
                return defaultValue;
            }
        }

        public static PythonTuple popitem(IDictionary<object, object> self) {
            IEnumerator<KeyValuePair<object, object>> ie = self.GetEnumerator();
            if (ie.MoveNext()) {
                object key = ie.Current.Key;
                object val = ie.Current.Value;
                self.Remove(key);
                return PythonTuple.MakeTuple(key, val);
            }
            throw PythonOps.KeyError("dictionary is empty");
        }

        public static object setdefault(PythonDictionary self, object key) {
            return setdefault(self, key, null);
        }

        public static object setdefault(PythonDictionary self, object key, object defaultValue) {
            object ret;
            if (self.TryGetValueNoMissing(key, out ret)) return ret;
            self.SetItem(key, defaultValue);
            return defaultValue;
        }

        public static void update(CodeContext/*!*/ context, PythonDictionary/*!*/ self, object other) {
            PythonDictionary pyDict;

            if ((pyDict = other as PythonDictionary) != null) {
                pyDict._storage.CopyTo(ref self._storage);
            } else {
                SlowUpdate(context, self, other);
            }
        }

        private static void SlowUpdate(CodeContext/*!*/ context, PythonDictionary/*!*/ self, object other) {
            object keysFunc;
            DictProxy dictProxy;
            IDictionary dict;
            if ((dictProxy = other as DictProxy) != null) {
                update(context, self, dictProxy.Type.GetMemberDictionary(context, false));
            } else if ((dict = other as IDictionary) != null) {
                IDictionaryEnumerator e = dict.GetEnumerator();
                while (e.MoveNext()) {
                    self._storage.Add(ref self._storage, e.Key, e.Value);
                }
            } else if (PythonOps.TryGetBoundAttr(other, "keys", out keysFunc)) {
                // user defined dictionary
                IEnumerator i = PythonOps.GetEnumerator(PythonCalls.Call(context, keysFunc));
                while (i.MoveNext()) {
                    self._storage.Add(ref self._storage, i.Current, PythonOps.GetIndex(context, other, i.Current));
                }
            } else {
                // list of lists (key/value pairs), list of tuples,
                // tuple of tuples, etc...
                IEnumerator i = PythonOps.GetEnumerator(other);
                int index = 0;
                while (i.MoveNext()) {
                    if (!AddKeyValue(self, i.Current)) {
                        throw PythonOps.ValueError("dictionary update sequence element #{0} has bad length; 2 is required", index);
                    }
                    index++;
                }
            }
        }

        #endregion

        #region Dictionary Helper APIs

        internal static bool TryGetValueVirtual(CodeContext context, PythonDictionary self, object key, ref object DefaultGetItem, out object value) {
            IPythonObject sdo = self as IPythonObject;
            if (sdo != null) {
                Debug.Assert(sdo != null);
                PythonType myType = sdo.PythonType;
                object ret;
                PythonTypeSlot dts;

                if (DefaultGetItem == null) {
                    // lazy init our cached DefaultGetItem
                    TypeCache.Dict.TryLookupSlot(context, "__getitem__", out dts);
                    bool res = dts.TryGetValue(context, self, TypeCache.Dict, out DefaultGetItem);
                    Debug.Assert(res);
                }

                // check and see if it's overridden
                if (myType.TryLookupSlot(context, "__getitem__", out dts)) {
                    dts.TryGetValue(context, self, myType, out ret);

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

        internal static bool AddKeyValue(PythonDictionary self, object o) {
            IEnumerator i = PythonOps.GetEnumerator(o); //c.GetEnumerator();
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
       
        internal static int CompareTo(CodeContext/*!*/ context, IDictionary<object, object> left, IDictionary<object, object> right) {
            int lcnt = left.Count;
            int rcnt = right.Count;

            if (lcnt != rcnt) return lcnt > rcnt ? 1 : -1;

            List ritems = DictionaryOps.items(right);
            return CompareToWorker(context, left, ritems);
        }

        internal static int CompareToWorker(CodeContext/*!*/ context, IDictionary<object, object> left, List ritems) {
            List litems = DictionaryOps.items(left);

            litems.sort(context);
            ritems.sort(context);

            return litems.CompareToWorker(ritems);
        }

        #endregion
    }
}
