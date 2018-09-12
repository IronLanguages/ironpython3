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
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime {
    /// <summary>
    /// Mutable set class
    /// </summary>
    [PythonType("set"), DebuggerDisplay("set, {Count} items", TargetTypeName = "set"), DebuggerTypeProxy(typeof(CollectionDebugProxy))]
    public class SetCollection : IEnumerable, IEnumerable<object>, ICollection, IStructuralEquatable, ICodeFormattable
    {
        internal SetStorage _items;

        #region Set Construction

        public void __init__() {
            clear();
        }

        public void __init__([NotNull]SetCollection set) {
            _items = set._items.Clone();
        }

        public void __init__([NotNull]FrozenSetCollection set) {
            _items = set._items.Clone();
        }

        public void __init__(object set) {
            SetStorage items;
            if (SetStorage.GetItems(set, out items)) {
                _items = items.Clone();
            } else {
                _items = items;
            }
        }

        public static object __new__(CodeContext/*!*/ context, PythonType cls) {
            if (cls == TypeCache.Set) {
                return new SetCollection();
            }

            return cls.CreateInstance(context);
        }

        public static object __new__(CodeContext/*!*/ context, PythonType cls, object arg) {
            return __new__(context, cls);
        }

        public static object __new__(CodeContext/*!*/ context, PythonType cls, params object[] args\u00F8) {
            return __new__(context, cls);
        }

        public static object __new__(CodeContext/*!*/ context, PythonType cls, [ParamDictionary]IDictionary<object, object> kwArgs, params object[] args\u00F8) {
            return __new__(context, cls);
        }

        public SetCollection() {
            _items = new SetStorage();
        }

        internal SetCollection(SetStorage items) {
            _items = items;
        }

        private SetCollection Empty {
            get {
                if (GetType() == typeof(SetCollection)) {
                    return new SetCollection();
                }

                return Make(DynamicHelpers.GetPythonType(this), new SetStorage());
            }
        }

        internal SetCollection(object[] items) {
            _items = new SetStorage(items.Length);
            foreach (var o in items) {
                _items.AddNoLock(o);
            }
        }

        private SetCollection Make(SetStorage items) {
            if (this.GetType() == typeof(SetCollection)) {
                return new SetCollection(items);
            }

            return Make(DynamicHelpers.GetPythonType(this), items);
        }

        private static SetCollection Make(PythonType/*!*/ cls, SetStorage items) {
            if (cls == TypeCache.Set) {
                return new SetCollection(items);
            }

            SetCollection res = PythonCalls.Call(cls) as SetCollection;
            Debug.Assert(res != null);

            if (items.Count > 0) {
                res._items = items;
            }
            return res;
        }

        internal static SetCollection Make(PythonType/*!*/ cls, object set) {
            SetStorage items;
            if (SetStorage.GetItems(set, out items)) {
                items = items.Clone();
            }

            return Make(cls, items);
        }

        public SetCollection copy() {
            return Make(_items.Clone());
        }

        #endregion

        #region Protocol Methods

        public int __len__() {
            return Count;
        }

        public bool __contains__(object item) {
            if (!SetStorage.GetHashableSetIfSet(ref item)) {
                // make sure we have a hashable item
                return _items.ContainsAlwaysHash(item);
            }
            return _items.Contains(item);
        }

        public PythonTuple __reduce__() {
            var type = GetType() != typeof(SetCollection) ? DynamicHelpers.GetPythonType(this) : TypeCache.Set;
            return SetStorage.Reduce(_items, type);
        }

        #endregion

        #region IStructuralEquatable Members

        public const object __hash__ = null;

        int IStructuralEquatable.GetHashCode(IEqualityComparer/*!*/ comparer) {
            if (CompareUtil.Check(this)) {
                return 0;
            }

            int res;
            CompareUtil.Push(this);
            try {
                res = ((IStructuralEquatable)new FrozenSetCollection(_items)).GetHashCode(comparer);
            } finally {
                CompareUtil.Pop(this);
            }

            return res;
        }

        bool IStructuralEquatable.Equals(object other, IEqualityComparer/*!*/ comparer) {
            SetStorage items;
            return SetStorage.GetItemsIfSet(other, out items) &&
                SetStorage.Equals(_items, items, comparer);
        }

        // default conversion of protocol methods only allows our specific type for equality,
        // but sets can do __eq__ / __ne__ against any type. This is why we define a separate
        // __eq__ / __ne__ here.

        public bool __eq__(object other) {
            SetStorage items;
            return SetStorage.GetItemsIfSet(other, out items) &&
                _items.Count == items.Count &&
                _items.IsSubset(items);
        }

        public bool __ne__(object other) {
            SetStorage items;
            return !SetStorage.GetItemsIfSet(other, out items) ||
                _items.Count != items.Count ||
                !_items.IsSubset(items);
        }

        #endregion

        #region Mutating Members

        public void add(object item) {
            _items.Add(item);
        }

        public void clear() {
            _items.Clear();
        }

        public void discard(object item) {
            SetStorage.GetHashableSetIfSet(ref item);

            _items.Remove(item);
        }

        public object pop() {
            object res;
            if (_items.Pop(out res)) {
                return res;
            }
            throw PythonOps.KeyError("pop from an empty set");
        }

        public void remove(object item) {
            bool res;
            object hashableItem = item;
            if (SetStorage.GetHashableSetIfSet(ref hashableItem)) {
                res = _items.Remove(hashableItem);
            } else {
                res = _items.RemoveAlwaysHash(hashableItem);
            }

            if (!res) {
                throw PythonOps.KeyError(item);
            }
        }

        public void update(SetCollection set) {
            if (ReferenceEquals(set, this)) {
                return;
            }

            lock (_items) {
                _items.UnionUpdate(set._items);
            }
        }

        public void update(FrozenSetCollection set) {
            lock (_items) {
                _items.UnionUpdate(set._items);
            }
        }

        /// <summary>
        /// Appends an IEnumerable to an existing set
        /// </summary>
        public void update(object set) {
            if (object.ReferenceEquals(set, this)) {
                return;
            }

            SetStorage items = SetStorage.GetItems(set);
            lock (_items) {
                _items.UnionUpdate(items);
            }
        }

        public void update([NotNull]params object[]/*!*/ sets) {
            Debug.Assert(sets != null);
            if (sets.Length == 0) {
                return;
            }

            lock (_items) {
                foreach (object set in sets) {
                    if (object.ReferenceEquals(set, this)) {
                        continue;
                    }

                    _items.UnionUpdate(SetStorage.GetItems(set));
                }
            }
        }

        public void intersection_update(SetCollection set) {
            if (object.ReferenceEquals(set, this)) {
                return;
            }

            lock (_items) {
                _items.IntersectionUpdate(set._items);
            }
        }

        public void intersection_update(FrozenSetCollection set) {
            lock (_items) {
                _items.IntersectionUpdate(set._items);
            }
        }

        public void intersection_update(object set) {
            if (object.ReferenceEquals(set, this)) {
                return;
            }

            SetStorage items = SetStorage.GetItems(set);
            lock (_items) {
                _items.IntersectionUpdate(items);
            }
        }

        public void intersection_update([NotNull]params object[]/*!*/ sets) {
            Debug.Assert(sets != null);
            if (sets.Length == 0) {
                return;
            }

            lock (_items) {
                foreach (object set in sets) {
                    if (object.ReferenceEquals(set, this)) {
                        continue;
                    }

                    _items.IntersectionUpdate(SetStorage.GetItems(set));
                }
            }
        }

        public void difference_update(SetCollection set) {
            if (object.ReferenceEquals(set, this)) {
                _items.Clear();
                return;
            }

            lock (_items) {
                _items.DifferenceUpdate(set._items);
            }
        }

        public void difference_update(FrozenSetCollection set) {
            lock (_items) {
                _items.DifferenceUpdate(set._items);
            }
        }

        public void difference_update(object set) {
            if (object.ReferenceEquals(set, this)) {
                _items.Clear();
                return;
            }

            SetStorage items = SetStorage.GetItems(set);
            lock (_items) {
                _items.DifferenceUpdate(items);
            }
        }

        public void difference_update([NotNull]params object[]/*!*/ sets) {
            Debug.Assert(sets != null);
            if (sets.Length == 0) {
                return;
            }

            lock (_items) {
                foreach (object set in sets) {
                    if (object.ReferenceEquals(set, this)) {
                        _items.ClearNoLock();
                        return;
                    }

                    _items.DifferenceUpdate(SetStorage.GetItems(set));
                }
            }
        }

        public void symmetric_difference_update(SetCollection set) {
            if (object.ReferenceEquals(set, this)) {
                _items.Clear();
                return;
            }

            lock (_items) {
                _items.SymmetricDifferenceUpdate(set._items);
            }
        }

        public void symmetric_difference_update(FrozenSetCollection set) {
            lock (_items) {
                _items.SymmetricDifferenceUpdate(set._items);
            }
        }

        public void symmetric_difference_update(object set) {
            if (ReferenceEquals(set, this)) {
                _items.Clear();
                return;
            }

            SetStorage items = SetStorage.GetItems(set);
            lock (_items) {
                _items.SymmetricDifferenceUpdate(items);
            }
        }

        #endregion

        #region Generated NonOperator Operations (SetCollection)

        // *** BEGIN GENERATED CODE ***
        // generated by function: _gen_setops from: generate_set.py

        public bool isdisjoint(SetCollection set) {
            return _items.IsDisjoint(set._items);
        }

        public bool isdisjoint(FrozenSetCollection set) {
            return _items.IsDisjoint(set._items);
        }

        public bool isdisjoint(object set) {
            return _items.IsDisjoint(SetStorage.GetItems(set));
        }

        public bool issubset(SetCollection set) {
            return _items.IsSubset(set._items);
        }

        public bool issubset(FrozenSetCollection set) {
            return _items.IsSubset(set._items);
        }

        public bool issubset(object set) {
            return _items.IsSubset(SetStorage.GetItems(set));
        }

        public bool issuperset(SetCollection set) {
            return set._items.IsSubset(_items);
        }

        public bool issuperset(FrozenSetCollection set) {
            return set._items.IsSubset(_items);
        }

        public bool issuperset(object set) {
            return SetStorage.GetItems(set).IsSubset(_items);
        }

        public SetCollection union() {
            return copy();
        }

        public SetCollection union(SetCollection set) {
            return Make(SetStorage.Union(_items, set._items));
        }

        public SetCollection union(FrozenSetCollection set) {
            return Make(SetStorage.Union(_items, set._items));
        }

        public SetCollection union(object set) {
            SetStorage items;
            if (SetStorage.GetItems(set, out items)) {
                items = SetStorage.Union(_items, items);
            } else {
                items.UnionUpdate(_items);
            }
            return Make(items);
        }

        public SetCollection union([NotNull]params object[]/*!*/ sets) {
            Debug.Assert(sets != null);

            SetStorage res = _items.Clone();
            foreach (object set in sets) {
                res.UnionUpdate(SetStorage.GetItems(set));
            }

            return Make(res);
        }

        public SetCollection intersection() {
            return copy();
        }

        public SetCollection intersection(SetCollection set) {
            return Make(SetStorage.Intersection(_items, set._items));
        }

        public SetCollection intersection(FrozenSetCollection set) {
            return Make(SetStorage.Intersection(_items, set._items));
        }

        public SetCollection intersection(object set) {
            SetStorage items;
            if (SetStorage.GetItems(set, out items)) {
                items = SetStorage.Intersection(_items, items);
            } else {
                items.IntersectionUpdate(_items);
            }
            return Make(items);
        }

        public SetCollection intersection([NotNull]params object[]/*!*/ sets) {
            Debug.Assert(sets != null);

            if (sets.Length == 0) {
                return copy();
            }

            SetStorage res = _items;
            foreach (object set in sets) {
                SetStorage items, x = res, y;
                if (SetStorage.GetItems(set, out items)) {
                    y = items;
                    SetStorage.SortBySize(ref x, ref y);

                    if (object.ReferenceEquals(x, items) || object.ReferenceEquals(x, _items)) {
                        x = x.Clone();
                    }
                } else {
                    y = items;
                    SetStorage.SortBySize(ref x, ref y);

                    if (object.ReferenceEquals(x, _items)) {
                        x = x.Clone();
                    }
                }
                x.IntersectionUpdate(y);
                res = x;
            }

            Debug.Assert(!object.ReferenceEquals(res, _items));
            return Make(res);
        }

        public SetCollection difference() {
            return copy();
        }

        public SetCollection difference(SetCollection set) {
            if (object.ReferenceEquals(set, this)) {
                return Empty;
            }

            return Make(
                SetStorage.Difference(_items, set._items)
            );
        }

        public SetCollection difference(FrozenSetCollection set) {
            return Make(
                SetStorage.Difference(_items, set._items)
            );
        }

        public SetCollection difference(object set) {
            return Make(
                SetStorage.Difference(_items, SetStorage.GetItems(set))
            );
        }

        public SetCollection difference([NotNull]params object[]/*!*/ sets) {
            Debug.Assert(sets != null);

            if (sets.Length == 0) {
                return copy();
            }

            SetStorage res = _items;
            foreach (object set in sets) {
                if (object.ReferenceEquals(set, this)) {
                    return Empty;
                }

                SetStorage items = SetStorage.GetItems(set);
                if (object.ReferenceEquals(res, _items)) {
                    res = SetStorage.Difference(_items, items);
                } else {
                    res.DifferenceUpdate(items);
                }
            }

            Debug.Assert(!object.ReferenceEquals(res, _items));
            return Make(res);
        }

        public SetCollection symmetric_difference(SetCollection set) {
            if (object.ReferenceEquals(set, this)) {
                return Empty;
            }

            return Make(SetStorage.SymmetricDifference(_items, set._items));
        }

        public SetCollection symmetric_difference(FrozenSetCollection set) {
            return Make(SetStorage.SymmetricDifference(_items, set._items));
        }

        public SetCollection symmetric_difference(object set) {
            SetStorage items;
            if (SetStorage.GetItems(set, out items)) {
                items = SetStorage.SymmetricDifference(_items, items);
            } else {
                items.SymmetricDifferenceUpdate(_items);
            }
            return Make(items);
        }


        // *** END GENERATED CODE ***

        #endregion

        #region Generated Mutating Operators

        // *** BEGIN GENERATED CODE ***
        // generated by function: gen_mutating_ops from: generate_set.py

        [SpecialName]
        public SetCollection InPlaceBitwiseOr(SetCollection set) {
            update(set);
            return this;
        }

        [SpecialName]
        public SetCollection InPlaceBitwiseOr(FrozenSetCollection set) {
            update(set);
            return this;
        }

        [SpecialName]
        public SetCollection InPlaceBitwiseOr(object set) {
            if (set is FrozenSetCollection || set is SetCollection) {
                update(set);
                return this;
            }

            throw PythonOps.TypeError(
                "unsupported operand type(s) for |=: '{0}' and '{1}'",
                PythonTypeOps.GetName(this), PythonTypeOps.GetName(set)
            );
        }

        [SpecialName]
        public SetCollection InPlaceBitwiseAnd(SetCollection set) {
            intersection_update(set);
            return this;
        }

        [SpecialName]
        public SetCollection InPlaceBitwiseAnd(FrozenSetCollection set) {
            intersection_update(set);
            return this;
        }

        [SpecialName]
        public SetCollection InPlaceBitwiseAnd(object set) {
            if (set is FrozenSetCollection || set is SetCollection) {
                intersection_update(set);
                return this;
            }

            throw PythonOps.TypeError(
                "unsupported operand type(s) for &=: '{0}' and '{1}'",
                PythonTypeOps.GetName(this), PythonTypeOps.GetName(set)
            );
        }

        [SpecialName]
        public SetCollection InPlaceExclusiveOr(SetCollection set) {
            symmetric_difference_update(set);
            return this;
        }

        [SpecialName]
        public SetCollection InPlaceExclusiveOr(FrozenSetCollection set) {
            symmetric_difference_update(set);
            return this;
        }

        [SpecialName]
        public SetCollection InPlaceExclusiveOr(object set) {
            if (set is FrozenSetCollection || set is SetCollection) {
                symmetric_difference_update(set);
                return this;
            }

            throw PythonOps.TypeError(
                "unsupported operand type(s) for ^=: '{0}' and '{1}'",
                PythonTypeOps.GetName(this), PythonTypeOps.GetName(set)
            );
        }

        [SpecialName]
        public SetCollection InPlaceSubtract(SetCollection set) {
            difference_update(set);
            return this;
        }

        [SpecialName]
        public SetCollection InPlaceSubtract(FrozenSetCollection set) {
            difference_update(set);
            return this;
        }

        [SpecialName]
        public SetCollection InPlaceSubtract(object set) {
            if (set is FrozenSetCollection || set is SetCollection) {
                difference_update(set);
                return this;
            }

            throw PythonOps.TypeError(
                "unsupported operand type(s) for -=: '{0}' and '{1}'",
                PythonTypeOps.GetName(this), PythonTypeOps.GetName(set)
            );
        }


        // *** END GENERATED CODE ***

        #endregion

        #region Generated Operators (SetCollection)

        // *** BEGIN GENERATED CODE ***
        // generated by function: _gen_ops from: generate_set.py

        public static SetCollection operator |(SetCollection x, SetCollection y) {
            return x.union(y);
        }

        public static SetCollection operator &(SetCollection x, SetCollection y) {
            return x.intersection(y);
        }

        public static SetCollection operator ^(SetCollection x, SetCollection y) {
            return x.symmetric_difference(y);
        }

        public static SetCollection operator -(SetCollection x, SetCollection y) {
            return x.difference(y);
        }

        public static SetCollection operator |(SetCollection x, FrozenSetCollection y) {
            return x.union(y);
        }

        public static SetCollection operator &(SetCollection x, FrozenSetCollection y) {
            return x.intersection(y);
        }

        public static SetCollection operator ^(SetCollection x, FrozenSetCollection y) {
            return x.symmetric_difference(y);
        }

        public static SetCollection operator -(SetCollection x, FrozenSetCollection y) {
            return x.difference(y);
        }


        // *** END GENERATED CODE ***

        #endregion

        #region Generated Interface Implementations (SetCollection)

        // *** BEGIN GENERATED CODE ***
        // generated by function: _gen_interfaces from: generate_set.py

        #region IRichComparable

        public static bool operator >(SetCollection self, object other) {
            SetStorage items;
            if (SetStorage.GetItemsIfSet(other, out items)) {
                return items.IsStrictSubset(self._items);
            }

            throw PythonOps.TypeError("can only compare to a set");
        }

        public static bool operator <(SetCollection self, object other) {
            SetStorage items;
            if (SetStorage.GetItemsIfSet(other, out items)) {
                return self._items.IsStrictSubset(items);
            }

            throw PythonOps.TypeError("can only compare to a set");
        }

        public static bool operator >=(SetCollection self, object other) {
            SetStorage items;
            if (SetStorage.GetItemsIfSet(other, out items)) {
                return items.IsSubset(self._items);
            }

            throw PythonOps.TypeError("can only compare to a set");
        }

        public static bool operator <=(SetCollection self, object other) {
            SetStorage items;
            if (SetStorage.GetItemsIfSet(other, out items)) {
                return self._items.IsSubset(items);
            }

            throw PythonOps.TypeError("can only compare to a set");
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic") ,System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "o")]
        [SpecialName]
        public int Compare(object o) {
            throw PythonOps.TypeError("cannot compare sets using cmp()");
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic") ,System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "o")]
        public int __cmp__(object o) {
            throw PythonOps.TypeError("cannot compare sets using cmp()");
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator() {
            return new SetIterator(_items, true);
        }

        #endregion

        #region IEnumerable<object> Members

        IEnumerator<object> IEnumerable<object>.GetEnumerator() {
            return new SetIterator(_items, true);
        }

        #endregion

        #region ICodeFormattable Members

        public virtual string/*!*/ __repr__(CodeContext/*!*/ context) {
            return SetStorage.SetToString(context, this, _items);
        }

        #endregion

        #region ICollection Members

        void ICollection.CopyTo(Array array, int index) {
            int i = 0;
            foreach (object o in this) {
                array.SetValue(o, index + i++);
            }
        }

        public int Count {
            [PythonHidden]
            get { return _items.Count; }
        }

        bool ICollection.IsSynchronized {
            get { return false; }
        }

        object ICollection.SyncRoot {
            get { return this; }
        }

        #endregion


        // *** END GENERATED CODE ***

        #endregion

    }

    /// <summary>
    /// Immutable set class
    /// </summary>
    [PythonType("frozenset"), DebuggerDisplay("frozenset, {Count} items", TargetTypeName = "frozenset"), DebuggerTypeProxy(typeof(CollectionDebugProxy))]
    public class FrozenSetCollection : IEnumerable, IEnumerable<object>, ICollection, IStructuralEquatable, ICodeFormattable
    {
        internal SetStorage _items;
        private HashCache _hashCache;

        private static readonly FrozenSetCollection _empty = new FrozenSetCollection();

        #region Set Construction

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "o")]
        public void __init__(params object[] o) {
            // nop
        }

        public static FrozenSetCollection __new__(CodeContext/*!*/ context, object cls) {
            if (cls == TypeCache.FrozenSet) {
                return _empty;
            } else {
                object res = ((PythonType)cls).CreateInstance(context);
                FrozenSetCollection fs = res as FrozenSetCollection;
                if (fs == null) {
                    throw PythonOps.TypeError("{0} is not a subclass of frozenset", res);
                }

                return fs;
            }
        }

        public static FrozenSetCollection __new__(CodeContext/*!*/ context, object cls, object set) {
            if (cls == TypeCache.FrozenSet) {
                return Make(TypeCache.FrozenSet, set);
            } else {
                object res = ((PythonType)cls).CreateInstance(context, set);
                FrozenSetCollection fs = res as FrozenSetCollection;
                if (fs == null) {
                    throw PythonOps.TypeError("{0} is not a subclass of frozenset", res);
                }

                return fs;
            }
        }

        public FrozenSetCollection() : this(new SetStorage()) { }

        internal FrozenSetCollection(SetStorage set) {
            _items = set;
        }

        protected internal FrozenSetCollection(object set) : this(SetStorage.GetFrozenItems(set)) { }

        private FrozenSetCollection Empty {
            get {
                if (GetType() == typeof(FrozenSetCollection)) {
                    return _empty;
                }

                return Make(DynamicHelpers.GetPythonType(this), new SetStorage());
            }
        }

        private FrozenSetCollection Make(SetStorage items) {
            if (items.Count == 0) {
                return Empty;
            }

            if (this.GetType() == typeof(FrozenSetCollection)) {
                return new FrozenSetCollection(items);
            }

            return Make(DynamicHelpers.GetPythonType(this), items);
        }

        private static FrozenSetCollection Make(PythonType/*!*/ cls, SetStorage items) {
            if (cls == TypeCache.FrozenSet) {
                if (items.Count == 0) {
                    return _empty;
                }
                return new FrozenSetCollection(items);
            }

            FrozenSetCollection res = PythonCalls.Call(cls) as FrozenSetCollection;
            Debug.Assert(res != null);

            if (items.Count > 0) {
                res._items = items;
            }
            return res;
        }

        internal static FrozenSetCollection Make(PythonType/*!*/ cls, object set) {
            FrozenSetCollection fs = set as FrozenSetCollection;
            if (fs != null && cls == TypeCache.FrozenSet) {
                // constructing frozen set from frozen set, we return the original
                return fs;
            }

            return Make(cls, SetStorage.GetFrozenItems(set));
        }

        public FrozenSetCollection copy() {
            // Python behavior: If we're a non-derived frozen set, set return the original
            // frozen set. If we're derived from a frozen set, we make a new set of this type
            // which contains the same elements.
            if (this.GetType() == typeof(FrozenSetCollection)) {
                return this;
            }

            // subclass
            return Make(DynamicHelpers.GetPythonType(this), _items);
        }

        #endregion

        #region Protocol Methods

        public int __len__() {
            return Count;
        }

        public bool __contains__(object item) {
            if (!SetStorage.GetHashableSetIfSet(ref item)) {
                // make sure we have a hashable item
                return _items.ContainsAlwaysHash(item);
            }
            return _items.Contains(item);
        }

        public PythonTuple __reduce__() {
            var type = GetType() != typeof(FrozenSetCollection) ? DynamicHelpers.GetPythonType(this) : TypeCache.FrozenSet;
            return SetStorage.Reduce(_items, type);
        }

        #endregion

        #region IStructuralEquatable Members

        private sealed class HashCache {
            internal readonly int HashCode;
            internal readonly IEqualityComparer Comparer;

            internal HashCache(int hashCode, IEqualityComparer comparer) {
                HashCode = hashCode;
                Comparer = comparer;
            }
        }

        private int CalculateHashCode(IEqualityComparer/*!*/ comparer) {
            Assert.NotNull(comparer);

            HashCache curHashCache = _hashCache;
            if (curHashCache != null && object.ReferenceEquals(comparer, curHashCache.Comparer)) {
                return curHashCache.HashCode;
            }

            int hash = SetStorage.GetHashCode(_items, comparer);

            _hashCache = new HashCache(hash, comparer);
            return hash;
        }

        int IStructuralEquatable.GetHashCode(IEqualityComparer/*!*/ comparer) {
            return CalculateHashCode(comparer);
        }

        bool IStructuralEquatable.Equals(object other, IEqualityComparer comparer) {
            SetStorage items;
            return SetStorage.GetItemsIfSet(other, out items) &&
                SetStorage.Equals(_items, items, comparer);
        }

        // default conversion of protocol methods only allows our specific type for equality,
        // but sets can do __eq__ / __ne__ against any type. This is why we define a separate
        // __eq__ / __ne__ here.

        public bool __eq__(object other) {
            SetStorage items;
            return SetStorage.GetItemsIfSet(other, out items) &&
                _items.Count == items.Count &&
                _items.IsSubset(items);
        }

        public bool __ne__(object other) {
            SetStorage items;
            return !SetStorage.GetItemsIfSet(other, out items) ||
                _items.Count != items.Count ||
                !_items.IsSubset(items);
        }

        #endregion

        #region Generated NonOperator Operations (FrozenSetCollection)

        // *** BEGIN GENERATED CODE ***
        // generated by function: _gen_setops from: generate_set.py

        public bool isdisjoint(FrozenSetCollection set) {
            return _items.IsDisjoint(set._items);
        }

        public bool isdisjoint(SetCollection set) {
            return _items.IsDisjoint(set._items);
        }

        public bool isdisjoint(object set) {
            return _items.IsDisjoint(SetStorage.GetItems(set));
        }

        public bool issubset(FrozenSetCollection set) {
            return _items.IsSubset(set._items);
        }

        public bool issubset(SetCollection set) {
            return _items.IsSubset(set._items);
        }

        public bool issubset(object set) {
            return _items.IsSubset(SetStorage.GetItems(set));
        }

        public bool issuperset(FrozenSetCollection set) {
            return set._items.IsSubset(_items);
        }

        public bool issuperset(SetCollection set) {
            return set._items.IsSubset(_items);
        }

        public bool issuperset(object set) {
            return SetStorage.GetItems(set).IsSubset(_items);
        }

        public FrozenSetCollection union() {
            return Make(_items);
        }

        public FrozenSetCollection union(FrozenSetCollection set) {
            return Make(SetStorage.Union(_items, set._items));
        }

        public FrozenSetCollection union(SetCollection set) {
            return Make(SetStorage.Union(_items, set._items));
        }

        public FrozenSetCollection union(object set) {
            SetStorage items;
            if (SetStorage.GetItems(set, out items)) {
                items = SetStorage.Union(_items, items);
            } else {
                items.UnionUpdate(_items);
            }
            return Make(items);
        }

        public FrozenSetCollection union([NotNull]params object[]/*!*/ sets) {
            Debug.Assert(sets != null);

            SetStorage res = _items.Clone();
            foreach (object set in sets) {
                res.UnionUpdate(SetStorage.GetItems(set));
            }

            return Make(res);
        }

        public FrozenSetCollection intersection() {
            return Make(_items);
        }

        public FrozenSetCollection intersection(FrozenSetCollection set) {
            return Make(SetStorage.Intersection(_items, set._items));
        }

        public FrozenSetCollection intersection(SetCollection set) {
            return Make(SetStorage.Intersection(_items, set._items));
        }

        public FrozenSetCollection intersection(object set) {
            SetStorage items;
            if (SetStorage.GetItems(set, out items)) {
                items = SetStorage.Intersection(_items, items);
            } else {
                items.IntersectionUpdate(_items);
            }
            return Make(items);
        }

        public FrozenSetCollection intersection([NotNull]params object[]/*!*/ sets) {
            Debug.Assert(sets != null);

            if (sets.Length == 0) {
                return Make(_items);
            }

            SetStorage res = _items;
            foreach (object set in sets) {
                SetStorage items, x = res, y;
                if (SetStorage.GetItems(set, out items)) {
                    y = items;
                    SetStorage.SortBySize(ref x, ref y);

                    if (object.ReferenceEquals(x, items) || object.ReferenceEquals(x, _items)) {
                        x = x.Clone();
                    }
                } else {
                    y = items;
                    SetStorage.SortBySize(ref x, ref y);

                    if (object.ReferenceEquals(x, _items)) {
                        x = x.Clone();
                    }
                }
                x.IntersectionUpdate(y);
                res = x;
            }

            Debug.Assert(!object.ReferenceEquals(res, _items));
            return Make(res);
        }

        public FrozenSetCollection difference() {
            return Make(_items);
        }

        public FrozenSetCollection difference(FrozenSetCollection set) {
            if (object.ReferenceEquals(set, this)) {
                return Empty;
            }

            return Make(
                SetStorage.Difference(_items, set._items)
            );
        }

        public FrozenSetCollection difference(SetCollection set) {
            return Make(
                SetStorage.Difference(_items, set._items)
            );
        }

        public FrozenSetCollection difference(object set) {
            return Make(
                SetStorage.Difference(_items, SetStorage.GetItems(set))
            );
        }

        public FrozenSetCollection difference([NotNull]params object[]/*!*/ sets) {
            Debug.Assert(sets != null);

            if (sets.Length == 0) {
                return Make(_items);
            }

            SetStorage res = _items;
            foreach (object set in sets) {
                if (object.ReferenceEquals(set, this)) {
                    return Empty;
                }

                SetStorage items = SetStorage.GetItems(set);
                if (object.ReferenceEquals(res, _items)) {
                    res = SetStorage.Difference(_items, items);
                } else {
                    res.DifferenceUpdate(items);
                }
            }

            Debug.Assert(!object.ReferenceEquals(res, _items));
            return Make(res);
        }

        public FrozenSetCollection symmetric_difference(FrozenSetCollection set) {
            if (object.ReferenceEquals(set, this)) {
                return Empty;
            }

            return Make(SetStorage.SymmetricDifference(_items, set._items));
        }

        public FrozenSetCollection symmetric_difference(SetCollection set) {
            return Make(SetStorage.SymmetricDifference(_items, set._items));
        }

        public FrozenSetCollection symmetric_difference(object set) {
            SetStorage items;
            if (SetStorage.GetItems(set, out items)) {
                items = SetStorage.SymmetricDifference(_items, items);
            } else {
                items.SymmetricDifferenceUpdate(_items);
            }
            return Make(items);
        }


        // *** END GENERATED CODE ***

        #endregion

        #region Generated Operators (FrozenSetCollection)

        // *** BEGIN GENERATED CODE ***
        // generated by function: _gen_ops from: generate_set.py

        public static FrozenSetCollection operator |(FrozenSetCollection x, FrozenSetCollection y) {
            return x.union(y);
        }

        public static FrozenSetCollection operator &(FrozenSetCollection x, FrozenSetCollection y) {
            return x.intersection(y);
        }

        public static FrozenSetCollection operator ^(FrozenSetCollection x, FrozenSetCollection y) {
            return x.symmetric_difference(y);
        }

        public static FrozenSetCollection operator -(FrozenSetCollection x, FrozenSetCollection y) {
            return x.difference(y);
        }

        public static FrozenSetCollection operator |(FrozenSetCollection x, SetCollection y) {
            return x.union(y);
        }

        public static FrozenSetCollection operator &(FrozenSetCollection x, SetCollection y) {
            return x.intersection(y);
        }

        public static FrozenSetCollection operator ^(FrozenSetCollection x, SetCollection y) {
            return x.symmetric_difference(y);
        }

        public static FrozenSetCollection operator -(FrozenSetCollection x, SetCollection y) {
            return x.difference(y);
        }


        // *** END GENERATED CODE ***

        #endregion

        #region Generated Interface Implementations (FrozenSetCollection)

        // *** BEGIN GENERATED CODE ***
        // generated by function: _gen_interfaces from: generate_set.py

        #region IRichComparable

        public static bool operator >(FrozenSetCollection self, object other) {
            SetStorage items;
            if (SetStorage.GetItemsIfSet(other, out items)) {
                return items.IsStrictSubset(self._items);
            }

            throw PythonOps.TypeError("can only compare to a set");
        }

        public static bool operator <(FrozenSetCollection self, object other) {
            SetStorage items;
            if (SetStorage.GetItemsIfSet(other, out items)) {
                return self._items.IsStrictSubset(items);
            }

            throw PythonOps.TypeError("can only compare to a set");
        }

        public static bool operator >=(FrozenSetCollection self, object other) {
            SetStorage items;
            if (SetStorage.GetItemsIfSet(other, out items)) {
                return items.IsSubset(self._items);
            }

            throw PythonOps.TypeError("can only compare to a set");
        }

        public static bool operator <=(FrozenSetCollection self, object other) {
            SetStorage items;
            if (SetStorage.GetItemsIfSet(other, out items)) {
                return self._items.IsSubset(items);
            }

            throw PythonOps.TypeError("can only compare to a set");
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic") ,System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "o")]
        [SpecialName]
        public int Compare(object o) {
            throw PythonOps.TypeError("cannot compare sets using cmp()");
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic") ,System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "o")]
        public int __cmp__(object o) {
            throw PythonOps.TypeError("cannot compare sets using cmp()");
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator() {
            return new SetIterator(_items, false);
        }

        #endregion

        #region IEnumerable<object> Members

        IEnumerator<object> IEnumerable<object>.GetEnumerator() {
            return new SetIterator(_items, false);
        }

        #endregion

        #region ICodeFormattable Members

        public virtual string/*!*/ __repr__(CodeContext/*!*/ context) {
            return SetStorage.SetToString(context, this, _items);
        }

        #endregion

        #region ICollection Members

        void ICollection.CopyTo(Array array, int index) {
            int i = 0;
            foreach (object o in this) {
                array.SetValue(o, index + i++);
            }
        }

        public int Count {
            [PythonHidden]
            get { return _items.Count; }
        }

        bool ICollection.IsSynchronized {
            get { return false; }
        }

        object ICollection.SyncRoot {
            get { return this; }
        }

        #endregion


        // *** END GENERATED CODE ***

        #endregion
    }

    /// <summary>
    /// Iterator over sets
    /// </summary>
    [PythonType("set_iterator")]
    public sealed class SetIterator : IEnumerable, IEnumerable<object>, IEnumerator, IEnumerator<object> {
        private readonly SetStorage _items;
        private readonly int _version;
        private readonly int _maxIndex;
        private int _index = -2;

        internal SetIterator(SetStorage items, bool mutable) {
            _items = items;
            if (mutable) {
                lock (items) {
                    _version = items.Version;
                    _maxIndex = items._count > 0 ? items._buckets.Length : 0;
                }
            } else {
                _version = items.Version;
                _maxIndex = items._count > 0 ? items._buckets.Length : 0;
            }
        }

        #region IDisposable Members

        [PythonHidden]
        public void Dispose() { }

        #endregion

        #region IEnumerator Members

        public object Current {
            [PythonHidden]
            get {
                if (_index < 0) {
                    return null;
                }

                object res = _items._buckets[_index].Item;

                if (_items.Version != _version) {
                    throw PythonOps.RuntimeError("set changed during iteration");
                }

                return res;
            }
        }

        [PythonHidden]
        public bool MoveNext() {
            if (_index == _maxIndex) {
                return false;
            }

            _index++;
            if (_index < 0) {
                if (_items._hasNull) {
                    return true;
                } else {
                    _index++;
                }
            }

            if (_maxIndex > 0) {
                SetStorage.Bucket[] buckets = _items._buckets;
                for (; _index < buckets.Length; _index++) {
                    object item = buckets[_index].Item;
                    if (item != null && item != SetStorage.Removed) {
                        return true;
                    }
                }
            }

            return false;
        }

        [PythonHidden]
        public void Reset() {
            _index = -2;
        }

        #endregion

        #region IEnumerable Members

        [PythonHidden]
        public IEnumerator GetEnumerator() {
            return this;
        }

        #endregion

        #region IEnumerable<object> Members

        IEnumerator<object> IEnumerable<object>.GetEnumerator() {
            return this;
        }

        #endregion

        public int __length_hint__() {
            if (_items.Version != _version || _index == _maxIndex) {
                return 0;
            }

            int index = _index;
            int count = 0;

            index++;
            if (index < 0) {
                if (_items._hasNull) {
                    count++;
                } else {
                    index++;
                }
            }

            if (_maxIndex > 0) {
                SetStorage.Bucket[] buckets = _items._buckets;
                for (; index < buckets.Length; index++) {
                    object item = buckets[index].Item;
                    if (item != null && item != SetStorage.Removed) {
                        count++;
                    }
                }
            }

            return count;
        }
    }
}
