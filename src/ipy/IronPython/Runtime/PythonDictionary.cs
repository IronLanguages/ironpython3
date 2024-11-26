// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Text;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime {

    [PythonType("dict"), Serializable, DebuggerTypeProxy(typeof(DebugProxy)), DebuggerDisplay("Count = {Count}")]
    public class PythonDictionary : IDictionary<object, object>, IDictionary,
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

        internal PythonDictionary(IDictionary<object, object> dict) {
            var storage = new CommonDictionaryStorage();

            foreach (var pair in dict) {
                storage.AddNoLock(pair.Key, pair.Value);
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
            _storage = size == 0 ? (DictionaryStorage)EmptyDictionaryStorage.Instance : new CommonDictionaryStorage(size);
        }

        internal static PythonDictionary FromIAC(CodeContext context, PythonDictionary iac) {
            return iac.GetType() == typeof(PythonDictionary) ? iac : MakeDictFromIAC(context, iac);
        }

        internal static PythonDictionary MakeDictFromIAC(CodeContext context, object iac) {
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

        [PythonHidden]
        public ICollection<object> Keys {
            // Convert to an array since keys() is slow to iterate over in most of the cases where we use this
            get { return _storage.GetKeys().ToArray(); }
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
            return _storage.TryGetValue(item.Key, out result) && PythonOps.IsOrEqualsRetBool(result, item.Value);
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
            if (!RemoveDirect(key)) {
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

        public DictionaryItemView items() => new DictionaryItemView(this);

        public DictionaryKeyView keys() => new DictionaryKeyView(this);

        public DictionaryValueView values() => new DictionaryValueView(this);

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

                // creating our own dict, try and get the ideal size and add w/o locks
                if (o is ICollection ic) {
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
                PythonContext pc = context.LanguageContext;
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
            if (seq is PythonRange xr) {
                int n = xr.__len__();
                object ret = context.LanguageContext.CallSplat(cls);
                if (ret.GetType() == typeof(PythonDictionary)) {
                    PythonDictionary dr = ret as PythonDictionary;
                    for (int i = 0; i < n; i++) {
                        dr[xr[i]] = value;
                    }
                } else {
                    // slow path, user defined dict
                    PythonContext pc = context.LanguageContext;
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
                ((IStructuralEquatable)this).Equals(other, context.LanguageContext.EqualityComparerNonGeneric)
            );
        }

        [return: MaybeNotImplemented]
        public object __ne__(CodeContext/*!*/ context, object other) {
            if (!(other is PythonDictionary || other is IDictionary<object, object>))
                return NotImplementedType.Value;

            return ScriptingRuntimeHelpers.BooleanToObject(
                !((IStructuralEquatable)this).Equals(other, context.LanguageContext.EqualityComparerNonGeneric)
            );
        }

        [return: MaybeNotImplemented]
        public NotImplementedType __gt__(CodeContext context, object other) => NotImplementedType.Value;

        [return: MaybeNotImplemented]
        public NotImplementedType __lt__(CodeContext context, object other) => NotImplementedType.Value;

        [return: MaybeNotImplemented]
        public NotImplementedType __ge__(CodeContext context, object other) => NotImplementedType.Value;

        [return: MaybeNotImplemented]
        public NotImplementedType __le__(CodeContext context, object other) => NotImplementedType.Value;

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
                IStructuralEquatable eq = FrozenSetCollection.Make(pairs);
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

            if (!(other is IDictionary<object, object> oth)) return false;
            if (oth.Count != Count) return false;

            if (other is PythonDictionary pd) {
                return ValueEqualsPythonDict(pd, comparer);
            }
            // we cannot call Compare here and compare against zero because Python defines
            // value equality even if the keys/values are unordered.
            foreach (object o in keys()) {
                object res;
                if (!oth.TryGetValue(o, out res)) return false;

                CompareUtil.Push(res);
                try {
                    var val = this[o];
                    if (comparer == null) {
                        if (!PythonOps.IsOrEqualsRetBool(res, val)) return false;
                    } else {
                        if (!ReferenceEquals(res, val) && !comparer.Equals(res, val)) return false;
                    }
                } finally {
                    CompareUtil.Pop(res);
                }
            }
            return true;
        }

        private bool ValueEqualsPythonDict(PythonDictionary pd, IEqualityComparer comparer) {
            foreach (object o in keys()) {
                object res;
                if (!pd.TryGetValueNoMissing(o, out res)) return false;

                CompareUtil.Push(res);
                try {
                    var val = this[o];
                    if (comparer == null) {
                        if (!PythonOps.IsOrEqualsRetBool(res, val)) return false;
                    } else {
                        if (!ReferenceEquals(res, val) && !comparer.Equals(res, val)) return false;
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
            private readonly IEnumerator<KeyValuePair<object, object>> _enumerator;
            private bool _moved;

            public DictEnumerator(IEnumerator<KeyValuePair<object, object>> enumerator) {
                _enumerator = enumerator;
            }

            #region IDictionaryEnumerator Members

            public DictionaryEntry Entry {
                get {
                    // PythonList<T> enumerator doesn't throw, so we need to.
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
            get { return keys(); }
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
#pragma warning disable IPY04 // Direct call to PythonTypeOps.GetName
                    return "Key: " + PythonTypeOps.GetName(Key) + ", " + "Value: " + PythonTypeOps.GetName(Value);
#pragma warning restore IPY04
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

            if (key is string s1 && value is string s2) {
                Environment.SetEnvironmentVariable(s1, s2);
            }
        }

        public override bool Remove(ref DictionaryStorage storage, object key) {
            bool res = _storage.Remove(key);

            if (key is string s) {
                Environment.SetEnvironmentVariable(s, string.Empty);
            }

            return res;
        }

        /// <summary>
        /// Since <see cref="EnvironmentDictionaryStorage"/> is always mutable, this is a no-op.
        /// </summary>
        /// <param name="storage">Ignored.</param>
        /// <returns><c>this</c></returns>
        public override DictionaryStorage AsMutable(ref DictionaryStorage storage) => this;

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
                if (x.Key is string key) {
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
    [PythonType("dict_keyiterator")]
    public sealed class DictionaryKeyEnumerator : IEnumerator<object> {
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

        object IEnumerator<object>.Current => _keys.Current;

        object IEnumerator.Current => _keys.Current;

        void IDisposable.Dispose() { }

        public object __iter__() => this;

        public int __length_hint__() => _size - _pos - 1;

        #region Pickling

        public object __reduce__(CodeContext context) {
            object iter;
            context.TryLookupBuiltin("iter", out iter);
            return PythonTuple.MakeTuple(iter, PythonTuple.MakeTuple(PythonList.FromArrayNoCopy(_dict.GetKeys().Skip(_pos + 1).ToArray())));
        }

        #endregion
    }

    /// <summary>
    /// Note: 
    ///   IEnumerator innerEnum = Dictionary&lt;K,V&gt;.KeysCollections.GetEnumerator();
    ///   innerEnum.MoveNext() will throw InvalidOperation even if the values get changed,
    ///   which is supported in python
    /// </summary>
    [PythonType("dict_valueiterator")]
    public sealed class DictionaryValueEnumerator : IEnumerator<object> {
        private readonly int _size;
        private readonly DictionaryStorage _dict;
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

        bool IEnumerator.MoveNext() {
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

        void IEnumerator.Reset() {
            _pos = -1;
        }

        object IEnumerator<object>.Current => _values[_pos];

        object IEnumerator.Current => _values[_pos];

        void IDisposable.Dispose() { }

        public object __iter__() => this;

        public int __len__() => _size - _pos - 1;

        #region Pickling

        public object __reduce__(CodeContext context) {
            object iter;
            context.TryLookupBuiltin("iter", out iter);
            return PythonTuple.MakeTuple(iter, PythonTuple.MakeTuple(PythonList.FromArrayNoCopy(_dict.GetItems().Skip(_pos + 1).Select(x => x.Value).ToArray())));
        }

        #endregion
    }

    /// <summary>
    /// Note: 
    ///   IEnumerator innerEnum = Dictionary&lt;K,V&gt;.KeysCollections.GetEnumerator();
    ///   innerEnum.MoveNext() will throw InvalidOperation even if the values get changed,
    ///   which is supported in python
    /// </summary>
    [PythonType("dict_itemiterator")]
    public sealed class DictionaryItemEnumerator : IEnumerator<object> {
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

        bool IEnumerator.MoveNext() {
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

        void IEnumerator.Reset() {
            _pos = -1;
        }

        object IEnumerator<object>.Current => PythonTuple.MakeTuple(_keys[_pos], _values[_pos]);

        object IEnumerator.Current => PythonTuple.MakeTuple(_keys[_pos], _values[_pos]);

        void IDisposable.Dispose() { }

        public object __iter__() => this;

        public int __len__() => _size - _pos - 1;

        #region Pickling

        public object __reduce__(CodeContext context) {
            object iter;
            context.TryLookupBuiltin("iter", out iter);
            return PythonTuple.MakeTuple(iter, PythonTuple.MakeTuple(PythonList.FromArrayNoCopy(_dict.GetItems().Skip(_pos + 1).Select(x => x.Value).ToArray())));
        }

        #endregion
    }

    [PythonType("dict_values")]
    public sealed class DictionaryValueView : ICollection<object>, ICollection, ICodeFormattable {
        private readonly PythonDictionary _dict;

        internal DictionaryValueView(PythonDictionary/*!*/ dict) {
            Debug.Assert(dict != null);

            _dict = dict;
        }

        IEnumerator<object> IEnumerable<object>.GetEnumerator() => new DictionaryValueEnumerator(_dict._storage);

        IEnumerator IEnumerable.GetEnumerator() => new DictionaryValueEnumerator(_dict._storage);

        #region ICollection<object> Members

        void ICollection<object>.Add(object item) => throw new NotSupportedException("Collection is read-only");

        void ICollection<object>.Clear() => throw new NotSupportedException("Collection is read-only");

        bool ICollection<object>.Contains(object item) {
            foreach (var val in this) {
                if (PythonOps.IsOrEqualsRetBool(val, item))
                    return true;
            }
            return false;
        }

        void ICollection<object>.CopyTo(object[] array, int arrayIndex) {
            int i = arrayIndex;
            foreach (object item in this) {
                array[i++] = item;
                if (i >= array.Length) {
                    break;
                }
            }
        }

        int ICollection<object>.Count => _dict.Count;

        bool ICollection<object>.IsReadOnly => true;

        bool ICollection<object>.Remove(object item) => throw new NotSupportedException("Collection is read-only");

        #endregion

        #region ICollection Members

        int ICollection.Count => _dict.Count;

        object ICollection.SyncRoot => this;

        bool ICollection.IsSynchronized => false;

        void ICollection.CopyTo(Array array, int index) {
            int i = index;
            foreach (object item in this) {
                array.SetValue(item, i++);
                if (i >= array.Length) {
                    break;
                }
            }
        }

        #endregion

        #region ICodeFormattable Members

        public string __repr__(CodeContext context) {
            StringBuilder res = new StringBuilder(20);
            res.Append("dict_values([");
            string comma = "";
            foreach (object value in this) {
                res.Append(comma);
                comma = ", ";
                try {
                    PythonOps.FunctionPushFrame(context.LanguageContext);
                    res.Append(PythonOps.Repr(context, value));
                } finally {
                    PythonOps.FunctionPopFrame();
                }
            }
            res.Append("])");

            return res.ToString();
        }

        #endregion

        #region Pickling

        // TODO: this is not technically correct, it should be throwing in ObjectOps.ReduceProtocol2
        public object __reduce__(CodeContext context) => throw PythonOps.TypeError($"can't pickle dict_values objects");

        #endregion
    }

    [PythonType("dict_keys")]
    public sealed class DictionaryKeyView : ICollection<object>, ICollection, ICodeFormattable {
        internal readonly PythonDictionary _dict;

        internal DictionaryKeyView(PythonDictionary/*!*/ dict) {
            Debug.Assert(dict != null);

            _dict = dict;
        }

        [PythonHidden]
        public IEnumerator<object> GetEnumerator() => new DictionaryKeyEnumerator(_dict._storage);

        IEnumerator IEnumerable.GetEnumerator() => new DictionaryKeyEnumerator(_dict._storage);

        #region ICollection<object> Members

        void ICollection<object>.Add(object key) => throw new NotSupportedException("Collection is read-only");

        void ICollection<object>.Clear() => throw new NotSupportedException("Collection is read-only");

        bool ICollection<object>.Contains(object key) => _dict.__contains__(key);

        void ICollection<object>.CopyTo(object[] array, int arrayIndex) {
            int i = arrayIndex;
            foreach (object item in this) {
                array[i++] = item;
                if (i >= array.Length) {
                    break;
                }
            }
        }

        int ICollection<object>.Count => _dict.Count;

        bool ICollection<object>.IsReadOnly => true;

        bool ICollection<object>.Remove(object item) => throw new NotSupportedException("Collection is read-only");

        #endregion

        #region ICollection Members

        int ICollection.Count => _dict.Count;

        object ICollection.SyncRoot => this;

        bool ICollection.IsSynchronized => false;

        void ICollection.CopyTo(Array array, int index) {
            int i = index;
            foreach (object item in this) {
                array.SetValue(item, i++);
                if (i >= array.Length) {
                    break;
                }
            }
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

        #region Pickling

        // TODO: this is not technically correct, it should be throwing in ObjectOps.ReduceProtocol2
        public object __reduce__(CodeContext context) => throw PythonOps.TypeError($"can't pickle dict_keys objects");

        #endregion

        public bool isdisjoint(IEnumerable other) {
            return SetStorage.Intersection(
                SetStorage.GetItemsWorker(GetEnumerator()),
                SetStorage.GetItems(other)
            ).Count == 0;
        }

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
        public IEnumerator<object> GetEnumerator() => new DictionaryItemEnumerator(_dict._storage);

        IEnumerator IEnumerable.GetEnumerator() => new DictionaryItemEnumerator(_dict._storage);

        #region ICollection<object> Members

        void ICollection<object>.Add(object item) => throw new NotSupportedException("Collection is read-only");

        void ICollection<object>.Clear() => throw new NotSupportedException("Collection is read-only");

        bool ICollection<object>.Contains(object item) {
            if (item is PythonTuple tuple && tuple.Count == 2 && _dict.TryGetValue(tuple[0], out object value)) {
                return PythonOps.IsOrEqualsRetBool(tuple[1], value);
            }
            return false;
        }

        void ICollection<object>.CopyTo(object[] array, int arrayIndex) {
            int i = arrayIndex;
            foreach (object item in this) {
                array[i++] = item;
                if (i >= array.Length) {
                    break;
                }
            }
        }

        int ICollection<object>.Count => _dict.Count;

        bool ICollection<object>.IsReadOnly =>  true;

        bool ICollection<object>.Remove(object item) => throw new NotSupportedException("Collection is read-only");

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
                try {
                    PythonOps.FunctionPushFrame(context.LanguageContext);
                    res.Append(PythonOps.Repr(context, item));
                } finally {
                    PythonOps.FunctionPopFrame();
                }
            }
            res.Append("])");

            return res.ToString();
        }

        #endregion

        #region Pickling

        // TODO: this is not technically correct, it should be throwing in ObjectOps.ReduceProtocol2
        public object __reduce__(CodeContext context) => throw PythonOps.TypeError($"can't pickle dict_items objects");

        #endregion

        public bool isdisjoint(IEnumerable other) {
            return SetStorage.Intersection(
                SetStorage.GetItemsWorker(GetEnumerator()),
                SetStorage.GetItems(other)
            ).Count == 0;
        }

        public override int GetHashCode() {
            return base.GetHashCode();
        }
    }
}
