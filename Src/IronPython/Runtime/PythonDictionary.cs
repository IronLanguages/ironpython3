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
using System.Security;
using System.Text;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime {

    [PythonType("dict"), Serializable, DebuggerTypeProxy(typeof(PythonDictionary.DebugProxy)), DebuggerDisplay("dict, {Count} items")]
    public class PythonDictionary : IDictionary<object, object>, IDictionary, 
#if CLR2
        IValueEquality,
#endif
        ICodeFormattable, IStructuralEquatable {
        internal DictionaryStorage _storage;

        internal static object MakeDict(CodeContext/*!*/ context, PythonType cls) {
            if (cls == TypeCache.Dict) {
                return new PythonDictionary();
            }
            return PythonCalls.Call(context, cls);
        }

        #region Constructors

        public PythonDictionary() {
            _storage = EmptyDictionaryStorage.Instance;
        }

        internal PythonDictionary(DictionaryStorage storage) {
            _storage = storage;
        }

        internal PythonDictionary(IDictionary dict) {
            var storage = new CommonDictionaryStorage();
            
            foreach (DictionaryEntry de in dict) {
                storage.AddNoLock(de.Key, de.Value);
            }
            _storage = storage;
        }

        internal PythonDictionary(PythonDictionary dict) {
            _storage = dict._storage.Clone();
        }

        internal PythonDictionary(CodeContext/*!*/ context, object o)
            : this() {
            update(context, o);
        }

        internal PythonDictionary(int size) {
            _storage = new CommonDictionaryStorage(size);
        }

        internal static PythonDictionary FromIAC(CodeContext context, PythonDictionary iac) {
            return iac.GetType() == typeof(PythonDictionary) ? (PythonDictionary)iac : MakeDictFromIAC(context, iac);
        }

        private static PythonDictionary MakeDictFromIAC(CodeContext context, PythonDictionary iac) {
            return new PythonDictionary(new ObjectAttributesAdapter(context, iac));
        }
        
        internal static PythonDictionary MakeSymbolDictionary() {
            return new PythonDictionary(new StringDictionaryStorage());
        }

        internal static PythonDictionary MakeSymbolDictionary(int count) {
            return new PythonDictionary(new StringDictionaryStorage(count));
        }

        public void __init__(CodeContext/*!*/ context, object o\u00F8, [ParamDictionary]IDictionary<object, object> kwArgs) {
            update(context, o\u00F8);
            update(context, kwArgs);
        }

        public void __init__(CodeContext/*!*/ context, [ParamDictionary]IDictionary<object, object> kwArgs) {
            update(context, kwArgs);
        }

        public void __init__(CodeContext/*!*/ context, object o\u00F8) {
            update(context, o\u00F8);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public void __init__() {
        }

        #endregion

        #region IDictionary<object,object> Members

        [PythonHidden]
        public void Add(object key, object value) {
            _storage.Add(ref _storage, key, value);
        }

        [PythonHidden]
        public bool ContainsKey(object key) {
            return _storage.Contains(key);
        }

        public ICollection<object> Keys {
            [PythonHidden]
            get { return keys(); }
        }

        [PythonHidden]
        public bool Remove(object key) {
            try {
                __delitem__(key);
                return true;
            } catch (KeyNotFoundException) {
                return false;
            }
        }

        [PythonHidden]
        public bool RemoveDirect(object key) {
            // Directly remove the value, without calling __delitem__
            // This is used to implement pop() in a manner consistent with CPython, which does
            // not call __delitem__ on pop().
            return _storage.Remove(ref _storage, key);
        }

        [PythonHidden]
        public bool TryGetValue(object key, out object value) {
            if (_storage.TryGetValue(key, out value)) {
                return true;
            }

            // we need to manually look up a slot to get the correct behavior when
            // the __missing__ function is declared on a sub-type which is an old-class
            if (GetType() != typeof(PythonDictionary) &&
                PythonTypeOps.TryInvokeBinaryOperator(DefaultContext.Default,
                this,
                key,
                "__missing__",
                out value)) {
                return true;
            }

            return false;
        }
        
        internal bool TryGetValueNoMissing(object key, out object value) {
            return _storage.TryGetValue(key, out value);
        }

        public ICollection<object> Values {
            [PythonHidden]
            get { return values(); }
        }

        #endregion

        #region ICollection<KeyValuePair<object,object>> Members

        [PythonHidden]
        public void Add(KeyValuePair<object, object> item) {
            _storage.Add(ref _storage, item.Key, item.Value);
        }

        [PythonHidden]
        public void Clear() {
            _storage.Clear(ref _storage);
        }

        [PythonHidden]
        public bool Contains(KeyValuePair<object, object> item) {
            object result;
            return _storage.TryGetValue(item.Key, out result) &&
                PythonOps.EqualRetBool(result, item.Value);
        }

        [PythonHidden]
        public void CopyTo(KeyValuePair<object, object>[] array, int arrayIndex) {
            _storage.GetItems().CopyTo(array, arrayIndex);
        }

        public int Count {
            [PythonHidden]
            get { return _storage.Count; }
        }

        bool ICollection<KeyValuePair<object, object>>.IsReadOnly {
            get { return false; }
        }

        [PythonHidden]
        public bool Remove(KeyValuePair<object, object> item) {
            return _storage.Remove(ref _storage, item.Key);
        }

        #endregion

        #region IEnumerable<KeyValuePair<object,object>> Members

        [PythonHidden]
        public IEnumerator<KeyValuePair<object, object>> GetEnumerator() {
            foreach (KeyValuePair<object, object> kvp in _storage.GetItems()) {
                yield return kvp;
            }
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator() {
            return Converter.ConvertToIEnumerator(__iter__());
        }

        public virtual object __iter__() {
            return new DictionaryKeyEnumerator(_storage);
        }

        #endregion

        #region IMapping Members

        public object get(object key) {
            return DictionaryOps.get(this, key);
        }

        public object get(object key, object defaultValue) {
            return DictionaryOps.get(this, key, defaultValue);
        }

        public virtual object this[params object[] key] {
            get {
                if (key == null) {
                    return GetItem(null);
                }

                if (key.Length == 0) {
                    throw PythonOps.TypeError("__getitem__() takes exactly one argument (0 given)");
                }

                return this[PythonTuple.MakeTuple(key)];
            }
            set {
                if (key == null) {
                    SetItem(null, value);
                    return;
                }

                if (key.Length == 0) {
                    throw PythonOps.TypeError("__setitem__() takes exactly two argument (1 given)");
                }

                this[PythonTuple.MakeTuple(key)] = value;
            }
        }

        public virtual object this[object key] {
            get {
                return GetItem(key);
            }
            set {
                SetItem(key, value);
            }
        }

        internal void SetItem(object key, object value) {
            _storage.Add(ref _storage, key, value);
        }

        private object GetItem(object key) {
            object ret;
            if (TryGetValue(key, out ret)) {
                return ret;
            }

            throw PythonOps.KeyError(key);
        }


        public virtual void __delitem__(object key) {
            if (!this.RemoveDirect(key)) {
                throw PythonOps.KeyError(key);
            }
        }

        public virtual void __delitem__(params object[] key) {
            if (key == null) {
                __delitem__((object)null);
            } else if (key.Length > 0) {
                __delitem__(PythonTuple.MakeTuple(key));
            } else {
                throw PythonOps.TypeError("__delitem__() takes exactly one argument (0 given)");
            }
        }

        #endregion

        #region IPythonContainer Members

        public virtual int __len__() {
            return Count;
        }

        #endregion

        #region Python dict implementation

        public void clear() {
            _storage.Clear(ref _storage);
        }

        public bool has_key(object key) {
            return DictionaryOps.has_key(this, key);
        }

        public object pop(object key) {
            return DictionaryOps.pop(this, key);
        }

        public object pop(object key, object defaultValue) {
            return DictionaryOps.pop(this, key, defaultValue);
        }

        public PythonTuple popitem() {
            return DictionaryOps.popitem(this);
        }

        public object setdefault(object key) {
            return DictionaryOps.setdefault(this, key);
        }

        public object setdefault(object key, object defaultValue) {
            return DictionaryOps.setdefault(this, key, defaultValue);
        }

        public virtual List keys() {
            List res = new List();
            foreach (KeyValuePair<object, object> kvp in _storage.GetItems()) {
                res.append(kvp.Key);
            }
            return res;
        }

        public virtual List values() {
            List res = new List();
            foreach (KeyValuePair<object, object> kvp in _storage.GetItems()) {
                res.append(kvp.Value);
            }
            return res;
        }

        public virtual List items() {
            List res = new List();
            foreach (KeyValuePair<object, object> kvp in _storage.GetItems()) {
                res.append(PythonTuple.MakeTuple(kvp.Key, kvp.Value));
            }
            return res;
        }

        public IEnumerator iteritems() {
            return new DictionaryItemEnumerator(_storage);
        }

        public IEnumerator iterkeys() {
            return new DictionaryKeyEnumerator(_storage);
        }

        public IEnumerator itervalues() {
            return new DictionaryValueEnumerator(_storage);
        }

        public IEnumerable viewitems() {
            return new DictionaryItemView(this);
        }

        public IEnumerable viewkeys() {
            return new DictionaryKeyView(this);
        }

        public IEnumerable viewvalues() {
            return new DictionaryValueView(this);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public void update() {
        }

        public void update(CodeContext/*!*/ context, [ParamDictionary]IDictionary<object, object> other\u00F8) {
            DictionaryOps.update(context, this, other\u00F8);
        }

        public void update(CodeContext/*!*/ context, object other\u00F8) {
            DictionaryOps.update(context, this, other\u00F8);
        }

        public void update(CodeContext/*!*/ context, object other\u00F8, [ParamDictionary]IDictionary<object, object> otherArgs\u00F8) {
            DictionaryOps.update(context, this, other\u00F8);
            DictionaryOps.update(context, this, otherArgs\u00F8);
        }

        private static object fromkeysAny(CodeContext/*!*/ context, PythonType cls, object o, object value) {
            PythonDictionary pyDict;
            object dict;

            if (cls == TypeCache.Dict) {
                string str;
                ICollection ic = o as ICollection;

                // creating our own dict, try and get the ideal size and add w/o locks
                if (ic != null) {
                    pyDict = new PythonDictionary(new CommonDictionaryStorage(ic.Count));
                } else if ((str = o as string) != null) {
                    pyDict = new PythonDictionary(str.Length);
                } else {
                    pyDict = new PythonDictionary();
                }

                IEnumerator i = PythonOps.GetEnumerator(o);
                while (i.MoveNext()) {
                    pyDict._storage.AddNoLock(ref pyDict._storage, i.Current, value);
                }

                return pyDict;
            } else {
                // call the user type constructor
                dict = MakeDict(context, cls);
                pyDict = dict as PythonDictionary;
            }

            if (pyDict != null) {
                // then store all the keys with their associated value
                IEnumerator i = PythonOps.GetEnumerator(o);
                while (i.MoveNext()) {
                    pyDict[i.Current] = value;
                }
            } else {
                // slow path, cls.__new__ returned a user defined dictionary instead of a PythonDictionary.
                PythonContext pc = PythonContext.GetContext(context);
                IEnumerator i = PythonOps.GetEnumerator(o);
                while (i.MoveNext()) {
                    pc.SetIndex(dict, i.Current, value);
                }
            }

            return dict;
        }

        [ClassMethod]
        public static object fromkeys(CodeContext context, PythonType cls, object seq) {
            return fromkeys(context, cls, seq, null);
        }

        [ClassMethod]
        public static object fromkeys(CodeContext context, PythonType cls, object seq, object value) {
            XRange xr = seq as XRange;
            if (xr != null) {
                int n = xr.__len__();
                object ret = PythonContext.GetContext(context).CallSplat(cls);
                if (ret.GetType() == typeof(PythonDictionary)) {
                    PythonDictionary dr = ret as PythonDictionary;
                    for (int i = 0; i < n; i++) {
                        dr[xr[i]] = value;
                    }
                } else {
                    // slow path, user defined dict
                    PythonContext pc = PythonContext.GetContext(context);
                    for (int i = 0; i < n; i++) {
                        pc.SetIndex(ret, xr[i], value);
                    }
                }
                return ret;
            }
            return fromkeysAny(context, cls, seq, value);
        }

        public virtual PythonDictionary copy(CodeContext/*!*/ context) {
            return new PythonDictionary(_storage.Clone());
        }

        public virtual bool __contains__(object key) {
            return _storage.Contains(key);
        }

        // Dictionary has an odd not-implemented check to support custom dictionaries and therefore
        // needs a custom __eq__ / __ne__ implementation.

        [return: MaybeNotImplemented]
        public object __eq__(CodeContext/*!*/ context, object other) {
            if (!(other is PythonDictionary || other is IDictionary<object, object>))
                return NotImplementedType.Value;

            return ScriptingRuntimeHelpers.BooleanToObject(
                ((IStructuralEquatable)this).Equals(other, PythonContext.GetContext(context).EqualityComparerNonGeneric)
            );
        }

        [return: MaybeNotImplemented]
        public object __ne__(CodeContext/*!*/ context, object other) {
            if (!(other is PythonDictionary || other is IDictionary<object, object>))
                return NotImplementedType.Value;

            return ScriptingRuntimeHelpers.BooleanToObject(
                !((IStructuralEquatable)this).Equals(other, PythonContext.GetContext(context).EqualityComparerNonGeneric)
            );
        }

        [return: MaybeNotImplemented]
        public object __cmp__(CodeContext context, object other) {
            IDictionary<object, object> oth = other as IDictionary<object, object>;
            // CompareTo is allowed to throw (string, int, etc... all do it if they don't get a matching type)
            if (oth == null) {
                object len, iteritems;
                if (!PythonOps.TryGetBoundAttr(context, other, "__len__", out len) ||
                    !PythonOps.TryGetBoundAttr(context, other, "iteritems", out iteritems)) {
                    return NotImplementedType.Value;
                }

                // user-defined dictionary...
                int lcnt = Count;
                int rcnt = PythonContext.GetContext(context).ConvertToInt32(PythonOps.CallWithContext(context, len));

                if (lcnt != rcnt) return lcnt > rcnt ? 1 : -1;

                return DictionaryOps.CompareToWorker(context, this, new List(PythonOps.CallWithContext(context, iteritems)));
            }

            CompareUtil.Push(this, oth);
            try {
                return DictionaryOps.CompareTo(context, this, oth);
            } finally {
                CompareUtil.Pop(this, oth);
            }
        }

        public int __cmp__(CodeContext/*!*/ context, [NotNull]PythonDictionary/*!*/ other) {
            CompareUtil.Push(this, other);
            try {
                return DictionaryOps.CompareTo(context, this, other);
            } finally {
                CompareUtil.Pop(this, other);
            }
        }

        // these are present in CPython but always return NotImplemented.
        [return: MaybeNotImplemented]
        [Python3Warning("dict inequality comparisons not supported in 3.x")]
        public static NotImplementedType operator > (PythonDictionary self, PythonDictionary other) {
            return PythonOps.NotImplemented;
        }

        [return: MaybeNotImplemented]
        [Python3Warning("dict inequality comparisons not supported in 3.x")]
        public static NotImplementedType operator <(PythonDictionary self, PythonDictionary other) {
            return PythonOps.NotImplemented;
        }

        [return: MaybeNotImplemented]
        [Python3Warning("dict inequality comparisons not supported in 3.x")]
        public static NotImplementedType operator >=(PythonDictionary self, PythonDictionary other) {
            return PythonOps.NotImplemented;
        }

        [return: MaybeNotImplemented]
        [Python3Warning("dict inequality comparisons not supported in 3.x")]
        public static NotImplementedType operator <=(PythonDictionary self, PythonDictionary other) {
            return PythonOps.NotImplemented;
        }

        #endregion

        #region IValueEquality Members
#if CLR2
        int IValueEquality.GetValueHashCode() {
            throw PythonOps.TypeErrorForUnhashableType("dict");
        }

        bool IValueEquality.ValueEquals(object other) {
            return EqualsWorker(other, null);
        }
#endif
        #endregion

        #region IStructuralEquatable Members

        public const object __hash__ = null;

        int IStructuralEquatable.GetHashCode(IEqualityComparer comparer) {
            if (CompareUtil.Check(this)) {
                return 0;
            }

            int res;
            SetStorage pairs = new SetStorage();
            foreach (KeyValuePair<object, object> kvp in _storage.GetItems()) {
                pairs.AddNoLock(PythonTuple.MakeTuple(kvp.Key, kvp.Value));
            }

            CompareUtil.Push(this);
            try {
                IStructuralEquatable eq = FrozenSetCollection.Make(TypeCache.FrozenSet, pairs);
                res = eq.GetHashCode(comparer);
            } finally {
                CompareUtil.Pop(this);
            }

            return res;
        }

        bool IStructuralEquatable.Equals(object other, IEqualityComparer comparer) {
            return EqualsWorker(other, comparer);
        }

        private bool EqualsWorker(object other, IEqualityComparer comparer) {
            if (Object.ReferenceEquals(this, other)) return true;

            IDictionary<object, object> oth = other as IDictionary<object, object>;
            if (oth == null) return false;
            if (oth.Count != Count) return false;

            PythonDictionary pd = other as PythonDictionary;
            if (pd != null) {
                return ValueEqualsPythonDict(pd, comparer);
            }
            // we cannot call Compare here and compare against zero because Python defines
            // value equality even if the keys/values are unordered.
            List myKeys = keys();

            foreach (object o in myKeys) {
                object res;
                if (!oth.TryGetValue(o, out res)) return false;

                CompareUtil.Push(res);
                try {
                    if (comparer == null) {
                        if (!PythonOps.EqualRetBool(res, this[o])) return false;
                    } else {
                        if (!comparer.Equals(res, this[o])) return false;
                    }
                } finally {
                    CompareUtil.Pop(res);
                }
            }
            return true;
        }

        private bool ValueEqualsPythonDict(PythonDictionary pd, IEqualityComparer comparer) {
            List myKeys = keys();

            foreach (object o in myKeys) {
                object res;
                if (!pd.TryGetValueNoMissing(o, out res)) return false;

                CompareUtil.Push(res);
                try {
                    if (comparer == null) {
                        if (!PythonOps.EqualRetBool(res, this[o])) return false;
                    } else {
                        if (!comparer.Equals(res, this[o])) return false;
                    }
                } finally {
                    CompareUtil.Pop(res);
                }
            }
            return true;
        }

        #endregion

        #region IDictionary Members

        [PythonHidden]
        public bool Contains(object key) {
            return __contains__(key);
        }

        internal class DictEnumerator : IDictionaryEnumerator {
            private IEnumerator<KeyValuePair<object, object>> _enumerator;
            private bool _moved;

            public DictEnumerator(IEnumerator<KeyValuePair<object, object>> enumerator) {
                _enumerator = enumerator;
            }

            #region IDictionaryEnumerator Members

            public DictionaryEntry Entry {
                get {
                    // List<T> enumerator doesn't throw, so we need to.
                    if (!_moved) throw new InvalidOperationException();

                    return new DictionaryEntry(_enumerator.Current.Key, _enumerator.Current.Value);
                }
            }

            public object Key {
                get { return Entry.Key; }
            }

            public object Value {
                get { return Entry.Value; }
            }

            #endregion

            #region IEnumerator Members

            public object Current {
                get { return Entry; }
            }

            public bool MoveNext() {
                if (_enumerator.MoveNext()) {
                    _moved = true;
                    return true;
                }

                _moved = false;
                return false;
            }

            public void Reset() {
                _enumerator.Reset();
                _moved = false;
            }

            #endregion
        }

        IDictionaryEnumerator IDictionary.GetEnumerator() {
            return new DictEnumerator(_storage.GetItems().GetEnumerator());
        }

        bool IDictionary.IsFixedSize {
            get { return false; }
        }

        bool IDictionary.IsReadOnly {
            get { return false; }
        }

        ICollection IDictionary.Keys {
            get { return this.keys(); }
        }

        ICollection IDictionary.Values {
            get { return values(); }
        }

        void IDictionary.Remove(object key) {
            ((IDictionary<object, object>)this).Remove(key);
        }

        #endregion

        #region ICollection Members

        void ICollection.CopyTo(Array array, int index) {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        bool ICollection.IsSynchronized {
            get { return false; }
        }

        object ICollection.SyncRoot {
            get { return null; }
        }

        #endregion

        #region ICodeFormattable Members

        public virtual string/*!*/ __repr__(CodeContext/*!*/ context) {
            return DictionaryOps.__repr__(context, this);
        }

        #endregion

        internal bool TryRemoveValue(object key, out object value) {
            return _storage.TryRemoveValue(ref _storage, key, out value);
        }

        #region Debugger View

        internal class DebugProxy {
            private readonly PythonDictionary _dict;

            public DebugProxy(PythonDictionary dict) {
                _dict = dict;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public List<KeyValueDebugView> Members {
                get {
                    var res = new List<KeyValueDebugView>();
                    foreach (var v in _dict) {
                        res.Add(new KeyValueDebugView(v.Key, v.Value));
                    }
                    return res;
                }
            }
        }

        [DebuggerDisplay("{Value}", Name = "{Key,nq}", Type = "{TypeInfo,nq}")]
        internal class KeyValueDebugView {
            public readonly object Key;
            public readonly object Value;

            public KeyValueDebugView(object key, object value) {
                Key = key;
                Value = value;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            public string TypeInfo {
                get {
                    return "Key: " + PythonTypeOps.GetName(Key) + ", " + "Value: " + PythonTypeOps.GetName(Value);
                }
            }
        }

        #endregion
    }

#if FEATURE_PROCESS
    [Serializable]
    internal sealed class EnvironmentDictionaryStorage : DictionaryStorage {
        private readonly CommonDictionaryStorage/*!*/ _storage = new CommonDictionaryStorage();

        public EnvironmentDictionaryStorage() {
            AddEnvironmentVars();
        }

        private void AddEnvironmentVars() {
            try {
                foreach (DictionaryEntry de in Environment.GetEnvironmentVariables()) {
                    _storage.Add(de.Key, de.Value);
                }
            } catch (SecurityException) {
                // environment isn't available under partial trust
            }
        }

        public override void Add(ref DictionaryStorage storage, object key, object value) {
            _storage.Add(key, value);

            string s1 = key as string;
            string s2 = value as string;
            if (s1 != null && s2 != null) {
                Environment.SetEnvironmentVariable(s1, s2);
            }
        }

        public override bool Remove(ref DictionaryStorage storage, object key) {
            bool res = _storage.Remove(key);

            string s = key as string;
            if (s != null) {
                Environment.SetEnvironmentVariable(s, string.Empty);
            }

            return res;
        }

        public override bool Contains(object key) {
            return _storage.Contains(key);
        }

        public override bool TryGetValue(object key, out object value) {
            return _storage.TryGetValue(key, out value);
        }

        public override int Count {
            get { return _storage.Count; }
        }

        public override void Clear(ref DictionaryStorage storage) {
            foreach (var x in GetItems()) {
                string key = x.Key as string;
                if (key != null) {
                    Environment.SetEnvironmentVariable(key, string.Empty);
                }
            }

            _storage.Clear(ref storage);
        }

        public override List<KeyValuePair<object, object>> GetItems() {
            return _storage.GetItems();
        }
    }
#endif

    /// <summary>
    /// Note: 
    ///   IEnumerator innerEnum = Dictionary&lt;K,V&gt;.KeysCollections.GetEnumerator();
    ///   innerEnum.MoveNext() will throw InvalidOperation even if the values get changed,
    ///   which is supported in python
    /// </summary>
    [PythonType("dictionary-keyiterator")]
    public sealed class DictionaryKeyEnumerator : IEnumerator, IEnumerator<object> {
        private readonly int _size;
        private readonly DictionaryStorage _dict;
        private readonly IEnumerator<object> _keys;
        private int _pos;

        internal DictionaryKeyEnumerator(DictionaryStorage dict) {
            _dict = dict;
            _size = dict.Count;
            _keys = dict.GetKeys().GetEnumerator();
            _pos = -1;
        }

        bool IEnumerator.MoveNext() {
            if (_size != _dict.Count) {
                _pos = _size - 1; // make the length 0
                throw PythonOps.RuntimeError("dictionary changed size during iteration");
            }
            if (_keys.MoveNext()) {
                _pos++;
                return true;
            } else {
                return false;
            }
        }

        void IEnumerator.Reset() {
            _keys.Reset();
            _pos = -1;
        }

        object IEnumerator.Current {
            get {
                return _keys.Current;
            }
        }

        object IEnumerator<object>.Current {
            get {
                return _keys.Current;
            }
        }

        void IDisposable.Dispose() {
        }

        public object __iter__() {
            return this;
        }

        public int __length_hint__() {
            return _size - _pos - 1;
        }
    }

    /// <summary>
    /// Note: 
    ///   IEnumerator innerEnum = Dictionary&lt;K,V&gt;.KeysCollections.GetEnumerator();
    ///   innerEnum.MoveNext() will throw InvalidOperation even if the values get changed,
    ///   which is supported in python
    /// </summary>
    [PythonType("dictionary-valueiterator")]
    public sealed class DictionaryValueEnumerator : IEnumerator, IEnumerator<object> {
        private readonly int _size;
        DictionaryStorage _dict;
        private readonly object[] _values;
        private int _pos;

        internal DictionaryValueEnumerator(DictionaryStorage dict) {
            _dict = dict;
            _size = dict.Count;
            _values = new object[_size];
            int i = 0;
            foreach (KeyValuePair<object, object> kvp in dict.GetItems()) {
                _values[i++] = kvp.Value;
            }
            _pos = -1;
        }

        public bool MoveNext() {
            if (_size != _dict.Count) {
                _pos = _size - 1; // make the length 0
                throw PythonOps.RuntimeError("dictionary changed size during iteration");
            }
            if (_pos + 1 < _size) {
                _pos++;
                return true;
            } else {
                return false;
            }
        }

        public void Reset() {
            _pos = -1;
        }

        public object Current {
            get {
                return _values[_pos];
            }
        }

        public void Dispose() {
        }

        public object __iter__() {
            return this;
        }

        public int __len__() {
            return _size - _pos - 1;
        }
    }

    /// <summary>
    /// Note: 
    ///   IEnumerator innerEnum = Dictionary&lt;K,V&gt;.KeysCollections.GetEnumerator();
    ///   innerEnum.MoveNext() will throw InvalidOperation even if the values get changed,
    ///   which is supported in python
    /// </summary>
    [PythonType("dictionary-itemiterator")]
    public sealed class DictionaryItemEnumerator : IEnumerator, IEnumerator<object> {
        private readonly int _size;
        private readonly DictionaryStorage _dict;
        private readonly List<object> _keys;
        private readonly List<object> _values;
        private int _pos;

        internal DictionaryItemEnumerator(DictionaryStorage dict) {
            _dict = dict;            
            _keys = new List<object>(dict.Count);
            _values = new List<object>(dict.Count);
            foreach (KeyValuePair<object, object> kvp in dict.GetItems()) {
                _keys.Add(kvp.Key);
                _values.Add(kvp.Value);
            }
            _size = _values.Count;
            _pos = -1;
        }

        public bool MoveNext() {
            if (_size != _dict.Count) {
                _pos = _size - 1; // make the length 0
                throw PythonOps.RuntimeError("dictionary changed size during iteration");
            }
            if (_pos + 1 < _size) {
                _pos++;
                return true;
            } else {
                return false;
            }
        }

        public void Reset() {
            _pos = -1;
        }

        public object Current {
            get {
                return PythonOps.MakeTuple(_keys[_pos], _values[_pos]);
            }
        }

        public void Dispose() {
        }

        public object __iter__() {
            return this;
        }

        public int __len__() {
            return _size - _pos - 1;
        }
    }

    [PythonType("dict_values")]
    public sealed class DictionaryValueView : IEnumerable, IEnumerable<object>, ICodeFormattable {
        private readonly PythonDictionary _dict;

        internal DictionaryValueView(PythonDictionary/*!*/ dict) {
            Debug.Assert(dict != null);

            _dict = dict;
        }

        [PythonHidden]
        public IEnumerator GetEnumerator() {
            return _dict.itervalues();
        }

        IEnumerator<object> IEnumerable<object>.GetEnumerator() {
            return new DictionaryValueEnumerator(_dict._storage);
        }

        public int __len__() {
            return _dict.Count;
        }

        #region ICodeFormattable Members

        public string __repr__(CodeContext context) {
            StringBuilder res = new StringBuilder(20);
            res.Append("dict_values([");
            string comma = "";
            foreach (object value in this) {
                res.Append(comma);
                comma = ", ";
                res.Append(PythonOps.Repr(context, value));
            }
            res.Append("])");

            return res.ToString();
        }

        #endregion
    }

    [PythonType("dict_keys")]
    public sealed class DictionaryKeyView : ICollection<object>, ICodeFormattable {
        internal readonly PythonDictionary _dict;

        internal DictionaryKeyView(PythonDictionary/*!*/ dict) {
            Debug.Assert(dict != null);

            _dict = dict;
        }

        [PythonHidden]
        public IEnumerator GetEnumerator() {
            return _dict.iterkeys();
        }

        IEnumerator<object> IEnumerable<object>.GetEnumerator() {
            return new DictionaryKeyEnumerator(_dict._storage);
        }

        #region ICollection<object> Members

        void ICollection<object>.Add(object key) {
            throw new NotSupportedException("Collection is read-only");
        }

        void ICollection<object>.Clear() {
            throw new NotSupportedException("Collection is read-only");
        }

        [PythonHidden]
        public bool Contains(object key) {
            return _dict.__contains__(key);
        }

        [PythonHidden]
        public void CopyTo(object[] array, int arrayIndex) {
            int i = arrayIndex;
            foreach (object item in this) {
                array[i++] = item;
                if (i >= array.Length) {
                    break;
                }
            }
        }

        public int Count {
            [PythonHidden]
            get { return _dict.Count; }
        }

        public bool IsReadOnly {
            [PythonHidden]
            get { return true; }
        }

        bool ICollection<object>.Remove(object item) {
            throw new NotSupportedException("Collection is read-only");
        }

        #endregion

        #region Generated Set Operations (Keys)

        // *** BEGIN GENERATED CODE ***
        // generated by function: _gen_ops from: generate_dict_views.py

        public static SetCollection operator |(DictionaryKeyView x, IEnumerable y) {
            return new SetCollection(SetStorage.Union(
                SetStorage.GetItemsWorker(x.GetEnumerator()),
                SetStorage.GetItems(y)
            ));
        }

        public static SetCollection operator |(IEnumerable y, DictionaryKeyView x) {
            return new SetCollection(SetStorage.Union(
                SetStorage.GetItemsWorker(x.GetEnumerator()),
                SetStorage.GetItems(y)
            ));
        }

        public static SetCollection operator &(DictionaryKeyView x, IEnumerable y) {
            return new SetCollection(SetStorage.Intersection(
                SetStorage.GetItemsWorker(x.GetEnumerator()),
                SetStorage.GetItems(y)
            ));
        }

        public static SetCollection operator &(IEnumerable y, DictionaryKeyView x) {
            return new SetCollection(SetStorage.Intersection(
                SetStorage.GetItemsWorker(x.GetEnumerator()),
                SetStorage.GetItems(y)
            ));
        }

        public static SetCollection operator ^(DictionaryKeyView x, IEnumerable y) {
            return new SetCollection(SetStorage.SymmetricDifference(
                SetStorage.GetItemsWorker(x.GetEnumerator()),
                SetStorage.GetItems(y)
            ));
        }

        public static SetCollection operator ^(IEnumerable y, DictionaryKeyView x) {
            return new SetCollection(SetStorage.SymmetricDifference(
                SetStorage.GetItemsWorker(x.GetEnumerator()),
                SetStorage.GetItems(y)
            ));
        }

        public static SetCollection operator -(DictionaryKeyView x, IEnumerable y) {
            return new SetCollection(SetStorage.Difference(
                SetStorage.GetItemsWorker(x.GetEnumerator()),
                SetStorage.GetItems(y)
            ));
        }

        public static SetCollection operator -(IEnumerable y, DictionaryKeyView x) {
            return new SetCollection(SetStorage.Difference(
                SetStorage.GetItemsWorker(x.GetEnumerator()),
                SetStorage.GetItems(y)
            ));
        }


        // *** END GENERATED CODE ***

        #endregion

        #region Generated Set Comparison Operations (Keys)

        // *** BEGIN GENERATED CODE ***
        // generated by function: _gen_comps from: generate_dict_views.py

        public override bool Equals(object obj) {
            if (obj == null) {
                return false;
            }
            if (obj is DictionaryKeyView) {
                return this == (DictionaryKeyView)obj;
            } else if (obj is DictionaryItemView) {
                return this == (DictionaryItemView)obj;
            } else if (obj is SetCollection) {
                return this == (SetCollection)obj;
            } else if (obj is FrozenSetCollection) {
                return this == (FrozenSetCollection)obj;
            }
            return false;
        }

        public static bool operator ==(DictionaryKeyView x, DictionaryKeyView y) {
            if (object.ReferenceEquals(x._dict, y._dict)) {
                return true;
            }
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = SetStorage.GetItemsWorker(y.GetEnumerator());
            return xs.Count == ys.Count && xs.IsSubset(ys);
        }

        public static bool operator !=(DictionaryKeyView x, DictionaryKeyView y) {
            if (object.ReferenceEquals(x._dict, y._dict)) {
                return false;
            }
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = SetStorage.GetItemsWorker(y.GetEnumerator());
            return xs.Count != ys.Count || !xs.IsSubset(ys);
        }

        public static bool operator >(DictionaryKeyView x, DictionaryKeyView y) {
            if (object.ReferenceEquals(x._dict, y._dict)) {
                return false;
            }
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = SetStorage.GetItemsWorker(y.GetEnumerator());
            return ys.IsStrictSubset(xs);
        }

        public static bool operator <(DictionaryKeyView x, DictionaryKeyView y) {
            if (object.ReferenceEquals(x._dict, y._dict)) {
                return false;
            }
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = SetStorage.GetItemsWorker(y.GetEnumerator());
            return xs.IsStrictSubset(ys);
        }

        public static bool operator >=(DictionaryKeyView x, DictionaryKeyView y) {
            if (object.ReferenceEquals(x._dict, y._dict)) {
                return true;
            }
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = SetStorage.GetItemsWorker(y.GetEnumerator());
            return ys.IsSubset(xs);
        }

        public static bool operator <=(DictionaryKeyView x, DictionaryKeyView y) {
            if (object.ReferenceEquals(x._dict, y._dict)) {
                return true;
            }
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = SetStorage.GetItemsWorker(y.GetEnumerator());
            return xs.IsSubset(ys);
        }

        public static bool operator ==(DictionaryKeyView x, DictionaryItemView y) {
            if (object.ReferenceEquals(x._dict, y._dict)) {
                return false;
            }
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = SetStorage.GetItemsWorker(y.GetEnumerator());
            return xs.Count == ys.Count && xs.IsSubset(ys);
        }

        public static bool operator !=(DictionaryKeyView x, DictionaryItemView y) {
            if (object.ReferenceEquals(x._dict, y._dict)) {
                return true;
            }
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = SetStorage.GetItemsWorker(y.GetEnumerator());
            return xs.Count != ys.Count || !xs.IsSubset(ys);
        }

        public static bool operator >(DictionaryKeyView x, DictionaryItemView y) {
            if (object.ReferenceEquals(x._dict, y._dict)) {
                return true;
            }
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = SetStorage.GetItemsWorker(y.GetEnumerator());
            return ys.IsStrictSubset(xs);
        }

        public static bool operator <(DictionaryKeyView x, DictionaryItemView y) {
            if (object.ReferenceEquals(x._dict, y._dict)) {
                return true;
            }
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = SetStorage.GetItemsWorker(y.GetEnumerator());
            return xs.IsStrictSubset(ys);
        }

        public static bool operator >=(DictionaryKeyView x, DictionaryItemView y) {
            if (object.ReferenceEquals(x._dict, y._dict)) {
                return false;
            }
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = SetStorage.GetItemsWorker(y.GetEnumerator());
            return ys.IsSubset(xs);
        }

        public static bool operator <=(DictionaryKeyView x, DictionaryItemView y) {
            if (object.ReferenceEquals(x._dict, y._dict)) {
                return false;
            }
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = SetStorage.GetItemsWorker(y.GetEnumerator());
            return xs.IsSubset(ys);
        }

        public static bool operator ==(DictionaryKeyView x, SetCollection y) {
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = y._items;
            return xs.Count == ys.Count && xs.IsSubset(ys);
        }

        public static bool operator !=(DictionaryKeyView x, SetCollection y) {
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = y._items;
            return xs.Count != ys.Count || !xs.IsSubset(ys);
        }

        public static bool operator >(DictionaryKeyView x, SetCollection y) {
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = y._items;
            return ys.IsStrictSubset(xs);
        }

        public static bool operator <(DictionaryKeyView x, SetCollection y) {
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = y._items;
            return xs.IsStrictSubset(ys);
        }

        public static bool operator >=(DictionaryKeyView x, SetCollection y) {
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = y._items;
            return ys.IsSubset(xs);
        }

        public static bool operator <=(DictionaryKeyView x, SetCollection y) {
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = y._items;
            return xs.IsSubset(ys);
        }

        public static bool operator ==(DictionaryKeyView x, FrozenSetCollection y) {
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = y._items;
            return xs.Count == ys.Count && xs.IsSubset(ys);
        }

        public static bool operator !=(DictionaryKeyView x, FrozenSetCollection y) {
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = y._items;
            return xs.Count != ys.Count || !xs.IsSubset(ys);
        }

        public static bool operator >(DictionaryKeyView x, FrozenSetCollection y) {
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = y._items;
            return ys.IsStrictSubset(xs);
        }

        public static bool operator <(DictionaryKeyView x, FrozenSetCollection y) {
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = y._items;
            return xs.IsStrictSubset(ys);
        }

        public static bool operator >=(DictionaryKeyView x, FrozenSetCollection y) {
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = y._items;
            return ys.IsSubset(xs);
        }

        public static bool operator <=(DictionaryKeyView x, FrozenSetCollection y) {
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = y._items;
            return xs.IsSubset(ys);
        }


        // *** END GENERATED CODE ***

        #endregion

        #region ICodeFormattable Members

        public string __repr__(CodeContext context) {
            StringBuilder res = new StringBuilder(20);
            res.Append("dict_keys([");
            string comma = "";
            foreach (object key in this) {
                res.Append(comma);
                comma = ", ";
                res.Append(PythonOps.Repr(context, key));
            }
            res.Append("])");

            return res.ToString();
        }

        #endregion

        public override int GetHashCode() {
            return base.GetHashCode();
        }
    }

    [PythonType("dict_items")]
    public sealed class DictionaryItemView : ICollection<object>, ICodeFormattable {
        internal readonly PythonDictionary _dict;

        internal DictionaryItemView(PythonDictionary/*!*/ dict) {
            Debug.Assert(dict != null);

            _dict = dict;
        }

        [PythonHidden]
        public IEnumerator GetEnumerator() {
            return _dict.iteritems();
        }

        IEnumerator<object> IEnumerable<object>.GetEnumerator() {
            return new DictionaryItemEnumerator(_dict._storage);
        }

        #region ICollection<object> Members

        void ICollection<object>.Add(object item) {
            throw new NotSupportedException("Collection is read-only");
        }

        void ICollection<object>.Clear() {
            throw new NotSupportedException("Collection is read-only");
        }

        [PythonHidden]
        public bool Contains(object item) {
            PythonTuple tuple = item as PythonTuple;
            object value;
            if (tuple == null || tuple.Count != 2 || !_dict.TryGetValue(tuple[0], out value)) {
                return false;
            }

            return PythonOps.EqualRetBool(tuple[1], value);
        }

        [PythonHidden]
        public void CopyTo(object[] array, int arrayIndex) {
            int i = arrayIndex;
            foreach (object item in this) {
                array[i++] = item;
                if (i >= array.Length) {
                    break;
                }
            }
        }

        public int Count {
            [PythonHidden]
            get { return _dict.Count; }
        }

        public bool IsReadOnly {
            [PythonHidden]
            get { return true; }
        }

        bool ICollection<object>.Remove(object item) {
            throw new NotSupportedException("Collection is read-only");
        }

        #endregion

        #region Generated Set Operations (Items)

        // *** BEGIN GENERATED CODE ***
        // generated by function: _gen_ops from: generate_dict_views.py

        public static SetCollection operator |(DictionaryItemView x, IEnumerable y) {
            return new SetCollection(SetStorage.Union(
                SetStorage.GetItemsWorker(x.GetEnumerator()),
                SetStorage.GetItems(y)
            ));
        }

        public static SetCollection operator |(IEnumerable y, DictionaryItemView x) {
            return new SetCollection(SetStorage.Union(
                SetStorage.GetItemsWorker(x.GetEnumerator()),
                SetStorage.GetItems(y)
            ));
        }

        public static SetCollection operator &(DictionaryItemView x, IEnumerable y) {
            return new SetCollection(SetStorage.Intersection(
                SetStorage.GetItemsWorker(x.GetEnumerator()),
                SetStorage.GetItems(y)
            ));
        }

        public static SetCollection operator &(IEnumerable y, DictionaryItemView x) {
            return new SetCollection(SetStorage.Intersection(
                SetStorage.GetItemsWorker(x.GetEnumerator()),
                SetStorage.GetItems(y)
            ));
        }

        public static SetCollection operator ^(DictionaryItemView x, IEnumerable y) {
            return new SetCollection(SetStorage.SymmetricDifference(
                SetStorage.GetItemsWorker(x.GetEnumerator()),
                SetStorage.GetItems(y)
            ));
        }

        public static SetCollection operator ^(IEnumerable y, DictionaryItemView x) {
            return new SetCollection(SetStorage.SymmetricDifference(
                SetStorage.GetItemsWorker(x.GetEnumerator()),
                SetStorage.GetItems(y)
            ));
        }

        public static SetCollection operator -(DictionaryItemView x, IEnumerable y) {
            return new SetCollection(SetStorage.Difference(
                SetStorage.GetItemsWorker(x.GetEnumerator()),
                SetStorage.GetItems(y)
            ));
        }

        public static SetCollection operator -(IEnumerable y, DictionaryItemView x) {
            return new SetCollection(SetStorage.Difference(
                SetStorage.GetItemsWorker(x.GetEnumerator()),
                SetStorage.GetItems(y)
            ));
        }


        // *** END GENERATED CODE ***

        #endregion

        #region Generated Set Comparison Operations (Items)

        // *** BEGIN GENERATED CODE ***
        // generated by function: _gen_comps from: generate_dict_views.py

        public override bool Equals(object obj) {
            if (obj == null) {
                return false;
            }
            if (obj is DictionaryItemView) {
                return this == (DictionaryItemView)obj;
            } else if (obj is DictionaryKeyView) {
                return this == (DictionaryKeyView)obj;
            } else if (obj is SetCollection) {
                return this == (SetCollection)obj;
            } else if (obj is FrozenSetCollection) {
                return this == (FrozenSetCollection)obj;
            }
            return false;
        }

        public static bool operator ==(DictionaryItemView x, DictionaryItemView y) {
            if (object.ReferenceEquals(x._dict, y._dict)) {
                return true;
            }
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = SetStorage.GetItemsWorker(y.GetEnumerator());
            return xs.Count == ys.Count && xs.IsSubset(ys);
        }

        public static bool operator !=(DictionaryItemView x, DictionaryItemView y) {
            if (object.ReferenceEquals(x._dict, y._dict)) {
                return false;
            }
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = SetStorage.GetItemsWorker(y.GetEnumerator());
            return xs.Count != ys.Count || !xs.IsSubset(ys);
        }

        public static bool operator >(DictionaryItemView x, DictionaryItemView y) {
            if (object.ReferenceEquals(x._dict, y._dict)) {
                return false;
            }
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = SetStorage.GetItemsWorker(y.GetEnumerator());
            return ys.IsStrictSubset(xs);
        }

        public static bool operator <(DictionaryItemView x, DictionaryItemView y) {
            if (object.ReferenceEquals(x._dict, y._dict)) {
                return false;
            }
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = SetStorage.GetItemsWorker(y.GetEnumerator());
            return xs.IsStrictSubset(ys);
        }

        public static bool operator >=(DictionaryItemView x, DictionaryItemView y) {
            if (object.ReferenceEquals(x._dict, y._dict)) {
                return true;
            }
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = SetStorage.GetItemsWorker(y.GetEnumerator());
            return ys.IsSubset(xs);
        }

        public static bool operator <=(DictionaryItemView x, DictionaryItemView y) {
            if (object.ReferenceEquals(x._dict, y._dict)) {
                return true;
            }
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = SetStorage.GetItemsWorker(y.GetEnumerator());
            return xs.IsSubset(ys);
        }

        public static bool operator ==(DictionaryItemView x, DictionaryKeyView y) {
            if (object.ReferenceEquals(x._dict, y._dict)) {
                return false;
            }
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = SetStorage.GetItemsWorker(y.GetEnumerator());
            return xs.Count == ys.Count && xs.IsSubset(ys);
        }

        public static bool operator !=(DictionaryItemView x, DictionaryKeyView y) {
            if (object.ReferenceEquals(x._dict, y._dict)) {
                return true;
            }
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = SetStorage.GetItemsWorker(y.GetEnumerator());
            return xs.Count != ys.Count || !xs.IsSubset(ys);
        }

        public static bool operator >(DictionaryItemView x, DictionaryKeyView y) {
            if (object.ReferenceEquals(x._dict, y._dict)) {
                return true;
            }
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = SetStorage.GetItemsWorker(y.GetEnumerator());
            return ys.IsStrictSubset(xs);
        }

        public static bool operator <(DictionaryItemView x, DictionaryKeyView y) {
            if (object.ReferenceEquals(x._dict, y._dict)) {
                return true;
            }
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = SetStorage.GetItemsWorker(y.GetEnumerator());
            return xs.IsStrictSubset(ys);
        }

        public static bool operator >=(DictionaryItemView x, DictionaryKeyView y) {
            if (object.ReferenceEquals(x._dict, y._dict)) {
                return false;
            }
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = SetStorage.GetItemsWorker(y.GetEnumerator());
            return ys.IsSubset(xs);
        }

        public static bool operator <=(DictionaryItemView x, DictionaryKeyView y) {
            if (object.ReferenceEquals(x._dict, y._dict)) {
                return false;
            }
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = SetStorage.GetItemsWorker(y.GetEnumerator());
            return xs.IsSubset(ys);
        }

        public static bool operator ==(DictionaryItemView x, SetCollection y) {
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = y._items;
            return xs.Count == ys.Count && xs.IsSubset(ys);
        }

        public static bool operator !=(DictionaryItemView x, SetCollection y) {
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = y._items;
            return xs.Count != ys.Count || !xs.IsSubset(ys);
        }

        public static bool operator >(DictionaryItemView x, SetCollection y) {
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = y._items;
            return ys.IsStrictSubset(xs);
        }

        public static bool operator <(DictionaryItemView x, SetCollection y) {
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = y._items;
            return xs.IsStrictSubset(ys);
        }

        public static bool operator >=(DictionaryItemView x, SetCollection y) {
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = y._items;
            return ys.IsSubset(xs);
        }

        public static bool operator <=(DictionaryItemView x, SetCollection y) {
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = y._items;
            return xs.IsSubset(ys);
        }

        public static bool operator ==(DictionaryItemView x, FrozenSetCollection y) {
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = y._items;
            return xs.Count == ys.Count && xs.IsSubset(ys);
        }

        public static bool operator !=(DictionaryItemView x, FrozenSetCollection y) {
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = y._items;
            return xs.Count != ys.Count || !xs.IsSubset(ys);
        }

        public static bool operator >(DictionaryItemView x, FrozenSetCollection y) {
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = y._items;
            return ys.IsStrictSubset(xs);
        }

        public static bool operator <(DictionaryItemView x, FrozenSetCollection y) {
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = y._items;
            return xs.IsStrictSubset(ys);
        }

        public static bool operator >=(DictionaryItemView x, FrozenSetCollection y) {
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = y._items;
            return ys.IsSubset(xs);
        }

        public static bool operator <=(DictionaryItemView x, FrozenSetCollection y) {
            SetStorage xs = SetStorage.GetItemsWorker(x.GetEnumerator());
            SetStorage ys = y._items;
            return xs.IsSubset(ys);
        }


        // *** END GENERATED CODE ***

        #endregion

        #region ICodeFormattable Members

        public string __repr__(CodeContext context) {
            StringBuilder res = new StringBuilder(20);
            res.Append("dict_items([");
            string comma = "";
            foreach (object item in this) {
                res.Append(comma);
                comma = ", ";
                res.Append(PythonOps.Repr(context, item));
            }
            res.Append("])");

            return res.ToString();
        }

        #endregion
        
        public override int GetHashCode() {
            return base.GetHashCode();
        }
    }
}
