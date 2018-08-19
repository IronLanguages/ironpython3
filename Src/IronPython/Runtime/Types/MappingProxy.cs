// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

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
    [PythonType("mappingproxy")]
    public class MappingProxy : IDictionary<object, object>, IDictionary {
        internal PythonDictionary Dictionary { get; }

        internal MappingProxy(CodeContext context, PythonType/*!*/ dt) {
            Debug.Assert(dt != null);
            Dictionary = dt.GetMemberDictionary(context, false);
        }

        public MappingProxy([NotNull]PythonDictionary dict) {
            Dictionary = dict;
        }

        #region Python Public API Surface

        public int __len__(CodeContext context) {
            return Dictionary.Count;
        }

        public bool __contains__(CodeContext/*!*/ context, object value) {
            object dummy;
            return Dictionary.TryGetValue(value, out dummy);
        }

        public string/*!*/ __str__(CodeContext/*!*/ context) {
            return DictionaryOps.__repr__(context, this);
        }

        public object get(CodeContext/*!*/ context, [NotNull]object k, object d=null) {
            object res;
            if (!Dictionary.TryGetValue(k, out res)) {
                res = d;
            }

            return res;
        }

        public object keys(CodeContext context) {
            return Dictionary.keys();
        }

        public object values(CodeContext context) {
            return Dictionary.values();
        }

        public object items(CodeContext context) {
            return Dictionary.items();
        }

        public PythonDictionary copy(CodeContext/*!*/ context) {
            return new PythonDictionary(context, this);
        }

        #endregion

        #region Object overrides

        public override bool Equals(object obj) {
            if (obj is MappingProxy)
                return ((IStructuralEquatable)Dictionary).Equals(((MappingProxy)obj).Dictionary, DefaultContext.DefaultPythonContext.EqualityComparerNonGeneric);

            return ((IStructuralEquatable)Dictionary).Equals(obj as PythonDictionary, DefaultContext.DefaultPythonContext.EqualityComparerNonGeneric);
        }

        public override int GetHashCode() {
            return ~Dictionary.GetHashCode();
        }

        #endregion

        #region IDictionary Members
      
        public object this[object key] {
            get {
                return Dictionary[key];
            }
            [PythonHidden]
            set {
                throw PythonOps.TypeError("'mappingproxy' object does not support item assignment");
            }
        }

        bool IDictionary.Contains(object key) {
            return __contains__(DefaultContext.Default, key);
        }

        #endregion              

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator() {
            return Dictionary.keys().GetEnumerator();
        }

        #endregion

        #region IDictionary Members

        [PythonHidden]
        public void Add(object key, object value) {
            this[key] = value;
        }

        [PythonHidden]
        public void Clear() {
            throw new InvalidOperationException("mappingproxy is read-only");
        }

        IDictionaryEnumerator IDictionary.GetEnumerator() {
            return ((IDictionary)Dictionary).GetEnumerator();
        }

        bool IDictionary.IsFixedSize {
            get { return true; }
        }

        bool IDictionary.IsReadOnly {
            get { return true; }
        }

        ICollection IDictionary.Keys {
            get {
                ICollection<object> res = Dictionary.Keys;
                ICollection coll = res as ICollection;
                if (coll != null) {
                    return coll;
                }

                return new List<object>(res);
            }
        }

        void IDictionary.Remove(object key) {
            throw new InvalidOperationException("mappingproxy is read-only");
        }

        ICollection IDictionary.Values {
            get {
                List<object> res = new List<object>();
                foreach (KeyValuePair<object, object> kvp in Dictionary) {
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
            return __contains__(DefaultContext.Default, key);
        }

        ICollection<object> IDictionary<object, object>.Keys {
            get {
                return Dictionary.Keys;
            }
        }

        bool IDictionary<object, object>.Remove(object key) {
            throw new InvalidOperationException("mappingproxy is read-only");
        }

        bool IDictionary<object, object>.TryGetValue(object key, out object value) {
            return Dictionary.TryGetValue(key, out value);
        }

        ICollection<object> IDictionary<object, object>.Values {
            get {
                return Dictionary.Values;
            }
        }

        #endregion

        #region ICollection<KeyValuePair<object,object>> Members

        void ICollection<KeyValuePair<object, object>>.Add(KeyValuePair<object, object> item) {
            this[item.Key] = item.Value;
        }

        bool ICollection<KeyValuePair<object, object>>.Contains(KeyValuePair<object, object> item) {
            return __contains__(DefaultContext.Default, item.Key);
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
            return Dictionary.GetEnumerator();
        }

        #endregion
    }
}
