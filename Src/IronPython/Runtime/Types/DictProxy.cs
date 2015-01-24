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
using System.Linq;
using System.Runtime.InteropServices;
using IronPython.Runtime.Operations;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime.Types {
    [PythonType("dictproxy")]
    public class DictProxy : IDictionary, IEnumerable, IDictionary<object, object> {
        private readonly PythonType/*!*/ _dt;
        
        public DictProxy(PythonType/*!*/ dt) {
            Debug.Assert(dt != null);
            _dt = dt;
        }

        #region Python Public API Surface

        public int __len__(CodeContext context) {
            return _dt.GetMemberDictionary(context, false).Count;
        }

        public bool __contains__(CodeContext/*!*/ context, object value) {
            return has_key(context, value);
        }

        public string/*!*/ __str__(CodeContext/*!*/ context) {
            return DictionaryOps.__repr__(context, this);
        }

        public bool has_key(CodeContext/*!*/ context, object key) {
            object dummy;
            return TryGetValue(context, key, out dummy);
        }

        public object get(CodeContext/*!*/ context, [NotNull]object k, [DefaultParameterValue(null)]object d) {
            object res;
            if (!TryGetValue(context, k, out res)) {
                res = d;
            }

            return res;
        }

        public object keys(CodeContext context) {
            return _dt.GetMemberDictionary(context, false).keys();
        }

        public object values(CodeContext context) {
            return _dt.GetMemberDictionary(context, false).values();
        }

        public List items(CodeContext context) {
            return _dt.GetMemberDictionary(context, false).items();
        }

        public PythonDictionary copy(CodeContext/*!*/ context) {
            return new PythonDictionary(context, this);
        }

        public IEnumerator iteritems(CodeContext/*!*/ context) {
            return new DictionaryItemEnumerator(_dt.GetMemberDictionary(context, false)._storage);
        }

        public IEnumerator iterkeys(CodeContext/*!*/ context) {
            return new DictionaryKeyEnumerator(_dt.GetMemberDictionary(context, false)._storage);
        }

        public IEnumerator itervalues(CodeContext/*!*/ context) {
            return new DictionaryValueEnumerator(_dt.GetMemberDictionary(context, false)._storage);
        }

        #endregion

        #region Object overrides

        public override bool Equals(object obj) {
            DictProxy proxy = obj as DictProxy;
            if (proxy == null) return false;

            return proxy._dt == _dt;
        }

        public override int GetHashCode() {
            return ~_dt.GetHashCode();
        }

        #endregion

        #region IDictionary Members
      
        public object this[object key] {
            get {
                return GetIndex(DefaultContext.Default, key);
            }
            [PythonHidden]
            set {
                throw PythonOps.TypeError("cannot assign to dictproxy");
            }
        }

        bool IDictionary.Contains(object key) {
            return has_key(DefaultContext.Default, key);
        }

        #endregion              

        #region IEnumerable Members

        System.Collections.IEnumerator IEnumerable.GetEnumerator() {
            return DictionaryOps.iterkeys(_dt.GetMemberDictionary(DefaultContext.Default, false));
        }

        #endregion

        #region IDictionary Members

        [PythonHidden]
        public void Add(object key, object value) {
            this[key] = value;
        }

        [PythonHidden]
        public void Clear() {
            throw new InvalidOperationException("dictproxy is read-only");
        }

        IDictionaryEnumerator IDictionary.GetEnumerator() {
            return new PythonDictionary.DictEnumerator(_dt.GetMemberDictionary(DefaultContext.Default, false).GetEnumerator());
        }

        bool IDictionary.IsFixedSize {
            get { return true; }
        }

        bool IDictionary.IsReadOnly {
            get { return true; }
        }

        ICollection IDictionary.Keys {
            get {
                ICollection<object> res = _dt.GetMemberDictionary(DefaultContext.Default, false).Keys;
                ICollection coll = res as ICollection;
                if (coll != null) {
                    return coll;
                }

                return new List<object>(res);
            }
        }

        void IDictionary.Remove(object key) {
            throw new InvalidOperationException("dictproxy is read-only");
        }

        ICollection IDictionary.Values {
            get {
                List<object> res = new List<object>();
                foreach (KeyValuePair<object, object> kvp in _dt.GetMemberDictionary(DefaultContext.Default, false)) {
                    res.Add(kvp.Value);
                }
                return res;
            }
        }

        #endregion

        #region ICollection Members

        void ICollection.CopyTo(Array array, int index) {
            foreach (DictionaryEntry de in (IDictionary)this) {
                array.SetValue(de, index++);
            }
        }

        int ICollection.Count {
            get { return __len__(DefaultContext.Default); }
        }

        bool ICollection.IsSynchronized {
            get { return false; }
        }

        object ICollection.SyncRoot {
            get { return this; }
        }

        #endregion

        #region IDictionary<object,object> Members

        bool IDictionary<object, object>.ContainsKey(object key) {
            return has_key(DefaultContext.Default, key);
        }

        ICollection<object> IDictionary<object, object>.Keys {
            get {
                return _dt.GetMemberDictionary(DefaultContext.Default, false).Keys;
            }
        }

        bool IDictionary<object, object>.Remove(object key) {
            throw new InvalidOperationException("dictproxy is read-only");
        }

        bool IDictionary<object, object>.TryGetValue(object key, out object value) {
            return TryGetValue(DefaultContext.Default, key, out value);
        }

        ICollection<object> IDictionary<object, object>.Values {
            get {
                return _dt.GetMemberDictionary(DefaultContext.Default, false).Values;
            }
        }

        #endregion

        #region ICollection<KeyValuePair<object,object>> Members

        void ICollection<KeyValuePair<object, object>>.Add(KeyValuePair<object, object> item) {
            this[item.Key] = item.Value;
        }

        bool ICollection<KeyValuePair<object, object>>.Contains(KeyValuePair<object, object> item) {
            return has_key(DefaultContext.Default, item.Key);
        }

        void ICollection<KeyValuePair<object, object>>.CopyTo(KeyValuePair<object, object>[] array, int arrayIndex) {
            foreach (KeyValuePair<object, object> de in (IEnumerable<KeyValuePair<object, object>>)this) {
                array.SetValue(de, arrayIndex++);
            }
        }

        int ICollection<KeyValuePair<object, object>>.Count {
            get { return __len__(DefaultContext.Default); }
        }

        bool ICollection<KeyValuePair<object, object>>.IsReadOnly {
            get { return true; }
        }

        bool ICollection<KeyValuePair<object, object>>.Remove(KeyValuePair<object, object> item) {
            return ((IDictionary<object, object>)this).Remove(item.Key);
        }

        #endregion

        #region IEnumerable<KeyValuePair<object,object>> Members

        IEnumerator<KeyValuePair<object, object>> IEnumerable<KeyValuePair<object, object>>.GetEnumerator() {
            return _dt.GetMemberDictionary(DefaultContext.Default, false).GetEnumerator();
        }

        #endregion

        #region Internal implementation details

        private object GetIndex(CodeContext context, object index) {
            string strIndex = index as string;
            if (strIndex != null) {
                PythonTypeSlot dts;
                if (_dt.TryLookupSlot(context, strIndex, out dts)) {
                    PythonTypeUserDescriptorSlot uds = dts as PythonTypeUserDescriptorSlot;
                    if (uds != null) {
                        return uds.Value;
                    }

                    return dts;
                }
            }

            throw PythonOps.KeyError(index.ToString());
        }

        private bool TryGetValue(CodeContext/*!*/ context, object key, out object value) {
            string strIndex = key as string;
            if (strIndex != null) {
                PythonTypeSlot dts;
                if (_dt.TryLookupSlot(context, strIndex, out dts)) {
                    PythonTypeUserDescriptorSlot uds = dts as PythonTypeUserDescriptorSlot;
                    if (uds != null) {
                        value = uds.Value;
                        return true;
                    }

                    value = dts;
                    return true;
                }
            }

            value = null;
            return false;
        }
        
        internal PythonType Type {
            get {
                return _dt;
            }
        }
        
        #endregion
    }
}
