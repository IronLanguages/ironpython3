// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

#if NETCOREAPP3_1 // IDictionary<object?, object?> is incorrectly annotated with TKey : notnull
#pragma warning disable CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Operations;

namespace IronPython.Runtime.Types {
    [PythonType("mappingproxy")]
    public sealed class MappingProxy : IDictionary<object?, object?>, IDictionary {
        internal PythonDictionary GetDictionary(CodeContext context) => dictionary ?? type!.GetMemberDictionary(context, false);

        private readonly PythonDictionary? dictionary;
        private readonly PythonType? type;

        internal MappingProxy(CodeContext context, PythonType/*!*/ dt) {
            Debug.Assert(dt != null);
            type = dt;
        }

        public MappingProxy([NotNone] PythonDictionary dict) {
            dictionary = dict;
        }

        #region Python Public API Surface

        public int __len__(CodeContext context) => GetDictionary(context).Count;

        public bool __contains__(CodeContext/*!*/ context, object? value) => GetDictionary(context).TryGetValue(value, out _);

        public string/*!*/ __str__(CodeContext/*!*/ context) => DictionaryOps.__repr__(context, this);

        public string __repr__(CodeContext/*!*/ context) {
            var dict = GetDictionary(context);
            List<object>? infinite = PythonOps.GetAndCheckInfinite(this);
            if (infinite == null) {
                return "mappingproxy({...})";
            }

            int infiniteIndex = infinite.Count;
            infinite.Add(this);
            try {
                return $"mappingproxy({PythonOps.Repr(context, dict)})";
            } finally {
                System.Diagnostics.Debug.Assert(infiniteIndex == infinite.Count - 1);
                infinite.RemoveAt(infiniteIndex);
            }
        }

        public object? get(CodeContext/*!*/ context, object? k, object? d = null) {
            if (GetDictionary(context).TryGetValue(k, out object? res)) {
                return res;
            }

            return d;
        }

        public object keys(CodeContext context) {
            var dict = GetDictionary(context);
            if (dict.GetType() == typeof(PythonDictionary)) return dict.keys();
            PythonTypeOps.TryInvokeUnaryOperator(context, dict, nameof(dict.keys), out object keys);
            return keys;
        }

        public object values(CodeContext context) {
            var dict = GetDictionary(context);
            if (dict.GetType() == typeof(PythonDictionary)) return dict.values();
            PythonTypeOps.TryInvokeUnaryOperator(context, dict, nameof(dict.values), out object values);
            return values;
        }

        public object items(CodeContext context) {
            var dict = GetDictionary(context);
            if (dict.GetType() == typeof(PythonDictionary)) return dict.items();
            PythonTypeOps.TryInvokeUnaryOperator(context, dict, nameof(dict.items), out object items);
            return items;
        }

        public PythonDictionary copy(CodeContext/*!*/ context) => GetDictionary(context).copy(context);

        public const object __hash__ = null;

        public object __eq__(CodeContext/*!*/ context, object? other) {
            if (other is MappingProxy proxy) {
                if (type == null) {
                    return __eq__(context, proxy.GetDictionary(context));
                }

                return type == proxy.type;
            }

            if (other is PythonDictionary) {
                return ((IStructuralEquatable)GetDictionary(context)).Equals(other, DefaultContext.DefaultPythonContext.EqualityComparerNonGeneric);
            }

            return false;
        }

        #endregion

        #region IDictionary Members

        public object? this[object? key] {
            get => GetDictionary(DefaultContext.Default)[key];
            [PythonHidden]
            set => throw PythonOps.TypeError("'mappingproxy' object does not support item assignment");
        }

        bool IDictionary.Contains(object key) => __contains__(DefaultContext.Default, key);

        #endregion              

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator() => GetDictionary(DefaultContext.Default).keys().GetEnumerator();

        #endregion

        #region IDictionary Members

        [PythonHidden]
        public void Add(object? key, object? value) {
            this[key] = value;
        }

        [PythonHidden]
        public void Clear() => throw new InvalidOperationException("mappingproxy is read-only");

        IDictionaryEnumerator IDictionary.GetEnumerator() => ((IDictionary)GetDictionary(DefaultContext.Default)).GetEnumerator();

        bool IDictionary.IsFixedSize => true;

        bool IDictionary.IsReadOnly => true;

        ICollection IDictionary.Keys {
            get {
                ICollection<object> res = GetDictionary(DefaultContext.Default).Keys;
                if (res is ICollection coll) {
                    return coll;
                }

                return new List<object>(res);
            }
        }

        void IDictionary.Remove(object key) => throw new InvalidOperationException("mappingproxy is read-only");

        ICollection IDictionary.Values {
            get {
                var res = new List<object>();
                foreach (KeyValuePair<object, object> kvp in GetDictionary(DefaultContext.Default)) {
                    res.Add(kvp.Value);
                }
                return res;
            }
        }

        #endregion

        #region ICollection Members

        void ICollection.CopyTo(Array array, int index) => throw new NotImplementedException("The method or operation is not implemented.");

        int ICollection.Count => __len__(DefaultContext.Default);

        bool ICollection.IsSynchronized => false;

        object ICollection.SyncRoot => this;

        #endregion

        #region IDictionary<object,object> Members

        bool IDictionary<object?, object?>.ContainsKey(object? key) => __contains__(DefaultContext.Default, key);

        ICollection<object?> IDictionary<object?, object?>.Keys => GetDictionary(DefaultContext.Default).Keys;

        bool IDictionary<object?, object?>.Remove(object? key) => throw new InvalidOperationException("mappingproxy is read-only");

        bool IDictionary<object?, object?>.TryGetValue(object? key, out object? value) => GetDictionary(DefaultContext.Default).TryGetValue(key, out value);

        ICollection<object?> IDictionary<object?, object?>.Values => GetDictionary(DefaultContext.Default).Values;

        #endregion

        #region ICollection<KeyValuePair<object,object>> Members

        void ICollection<KeyValuePair<object?, object?>>.Add(KeyValuePair<object?, object?> item) {
            this[item.Key] = item.Value;
        }

        bool ICollection<KeyValuePair<object?, object?>>.Contains(KeyValuePair<object?, object?> item) => __contains__(DefaultContext.Default, item.Key);

        void ICollection<KeyValuePair<object?, object?>>.CopyTo(KeyValuePair<object?, object?>[] array, int arrayIndex) {
            foreach (KeyValuePair<object?, object?> de in (IEnumerable<KeyValuePair<object?, object?>>)this) {
                array.SetValue(de, arrayIndex++);
            }
        }

        int ICollection<KeyValuePair<object?, object?>>.Count => __len__(DefaultContext.Default);

        bool ICollection<KeyValuePair<object?, object?>>.IsReadOnly => true;

        bool ICollection<KeyValuePair<object?, object?>>.Remove(KeyValuePair<object?, object?> item) => ((IDictionary<object?, object?>)this).Remove(item.Key);

        #endregion

        #region IEnumerable<KeyValuePair<object,object>> Members

        IEnumerator<KeyValuePair<object?, object?>> IEnumerable<KeyValuePair<object?, object?>>.GetEnumerator() => GetDictionary(DefaultContext.Default).GetEnumerator();

        #endregion
    }
}
