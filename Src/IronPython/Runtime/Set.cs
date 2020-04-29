// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime {
    /// <summary>
    /// Mutable set class
    /// </summary>
    [PythonType("set"), DebuggerDisplay("set, {Count} items", TargetTypeName = "set"), DebuggerTypeProxy(typeof(CollectionDebugProxy))]
    public class SetCollection : IEnumerable, IEnumerable<object?>, ICollection, IStructuralEquatable, ICodeFormattable, IWeakReferenceable {
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

        public void __init__(object? set) {
            SetStorage items;
            if (SetStorage.GetItems(set, out items)) {
                _items = items.Clone();
            } else {
                _items = items;
            }
        }

        public static object __new__(CodeContext/*!*/ context, [NotNull] PythonType cls) {
            if (cls == TypeCache.Set) {
                return new SetCollection();
            }

            return cls.CreateInstance(context);
        }

        public static object __new__(CodeContext/*!*/ context, [NotNull] PythonType cls, object? arg) {
            return __new__(context, cls);
        }

        public static object __new__(CodeContext/*!*/ context, [NotNull] PythonType cls, [NotNull] params object?[] args\u00F8) {
            return __new__(context, cls);
        }

        public static object __new__(CodeContext/*!*/ context, [NotNull] PythonType cls, [ParamDictionary, NotNull] IDictionary<object, object> kwArgs, [NotNull] params object?[] args\u00F8) {
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
                return new SetCollection();
            }
        }

        internal SetCollection(object[] items) {
            _items = new SetStorage(items.Length);
            foreach (var o in items) {
                _items.AddNoLock(o);
            }
        }

        internal static SetCollection Make(SetStorage items) {
            return new SetCollection(items);
        }

        internal static SetCollection Make(object set) {
            SetStorage items;
            if (SetStorage.GetItems(set, out items)) {
                items = items.Clone();
            }

            return Make(items);
        }

        public SetCollection copy() {
            return Make(_items.Clone());
        }

        #endregion

        #region Protocol Methods

        public int __len__() {
            return Count;
        }

        public bool __contains__(object? item) {
            if (!SetStorage.GetHashableSetIfSet(ref item)) {
                // make sure we have a hashable item
                return _items.ContainsAlwaysHash(item);
            }
            return _items.Contains(item);
        }

        public PythonTuple __reduce__(CodeContext context) {
            return PythonTuple.MakeTuple(
                DynamicHelpers.GetPythonType(this),
                PythonTuple.MakeTuple(_items.GetItems()),
                GetType() == typeof(SetCollection) ? null : (ObjectOps.ReduceProtocol0(context, this) is PythonTuple tuple ? tuple[2] : null)
            ); ;
        }

        #endregion

        #region IStructuralEquatable Members

        public const object? __hash__ = null;

        int IStructuralEquatable.GetHashCode(IEqualityComparer/*!*/ comparer) {
            if (CompareUtil.Check(this)) {
                return 0;
            }

            int res;
            CompareUtil.Push(this);
            try {
                res = ((IStructuralEquatable)FrozenSetCollection.Make(_items)).GetHashCode(comparer);
            } finally {
                CompareUtil.Pop(this);
            }

            return res;
        }

        bool IStructuralEquatable.Equals(object? other, IEqualityComparer/*!*/ comparer) {
            SetStorage items;
            return SetStorage.GetItemsIfSet(other, out items) &&
                SetStorage.Equals(_items, items, comparer);
        }

        // default conversion of protocol methods only allows our specific type for equality,
        // but sets can do __eq__ / __ne__ against any type. This is why we define a separate
        // __eq__ / __ne__ here.

        [return: MaybeNotImplemented]
        public object __eq__(object? other) {
            if (SetStorage.GetItemsIfSet(other, out SetStorage items)) {
                return _items.Count == items.Count && _items.IsSubset(items);
            }
            return NotImplementedType.Value;
        }

        [return: MaybeNotImplemented]
        public object __ne__(object? other) {
            if (SetStorage.GetItemsIfSet(other, out SetStorage items)) {
                return _items.Count != items.Count || !_items.IsSubset(items);
            }
            return NotImplementedType.Value;
        }

        #endregion

        #region Mutating Members

        public void add(object? item) {
            _items.Add(item);
        }

        public void clear() {
            _items.Clear();
        }

        public void discard(object? item) {
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

        public void remove(object? item) {
            bool res;
            object? hashableItem = item;
            if (SetStorage.GetHashableSetIfSet(ref hashableItem)) {
                res = _items.Remove(hashableItem);
            } else {
                res = _items.RemoveAlwaysHash(hashableItem);
            }

            if (!res) {
                throw PythonOps.KeyError(item);
            }
        }

        public void update([NotNull] SetCollection set) {
            if (ReferenceEquals(set, this)) {
                return;
            }

            lock (_items) {
                _items.UnionUpdate(set._items);
            }
        }

        public void update([NotNull] FrozenSetCollection set) {
            lock (_items) {
                _items.UnionUpdate(set._items);
            }
        }

        /// <summary>
        /// Appends an IEnumerable to an existing set
        /// </summary>
        public void update(object? set) {
            if (object.ReferenceEquals(set, this)) {
                return;
            }

            SetStorage items = SetStorage.GetItems(set);
            lock (_items) {
                _items.UnionUpdate(items);
            }
        }

        public void update([NotNull]params object[]/*!*/ sets) {
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

        public void intersection_update([NotNull] SetCollection set) {
            if (object.ReferenceEquals(set, this)) {
                return;
            }

            lock (_items) {
                _items.IntersectionUpdate(set._items);
            }
        }

        public void intersection_update([NotNull] FrozenSetCollection set) {
            lock (_items) {
                _items.IntersectionUpdate(set._items);
            }
        }

        public void intersection_update(object? set) {
            if (object.ReferenceEquals(set, this)) {
                return;
            }

            SetStorage items = SetStorage.GetItems(set);
            lock (_items) {
                _items.IntersectionUpdate(items);
            }
        }

        public void intersection_update([NotNull]params object[]/*!*/ sets) {
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

        public void difference_update([NotNull] SetCollection set) {
            if (object.ReferenceEquals(set, this)) {
                _items.Clear();
                return;
            }

            lock (_items) {
                _items.DifferenceUpdate(set._items);
            }
        }

        public void difference_update([NotNull] FrozenSetCollection set) {
            lock (_items) {
                _items.DifferenceUpdate(set._items);
            }
        }

        public void difference_update(object? set) {
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

        public void symmetric_difference_update([NotNull] SetCollection set) {
            if (object.ReferenceEquals(set, this)) {
                _items.Clear();
                return;
            }

            lock (_items) {
                _items.SymmetricDifferenceUpdate(set._items);
            }
        }

        public void symmetric_difference_update([NotNull] FrozenSetCollection set) {
            lock (_items) {
                _items.SymmetricDifferenceUpdate(set._items);
            }
        }

        public void symmetric_difference_update(object? set) {
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

        public bool isdisjoint([NotNull] SetCollection set) {
            return _items.IsDisjoint(set._items);
        }

        public bool isdisjoint([NotNull] FrozenSetCollection set) {
            return _items.IsDisjoint(set._items);
        }

        public bool isdisjoint(object? set) {
            return _items.IsDisjoint(SetStorage.GetItems(set));
        }

        public bool issubset([NotNull] SetCollection set) {
            return _items.IsSubset(set._items);
        }

        public bool issubset([NotNull] FrozenSetCollection set) {
            return _items.IsSubset(set._items);
        }

        public bool issubset(object? set) {
            return _items.IsSubset(SetStorage.GetItems(set));
        }

        public bool issuperset([NotNull] SetCollection set) {
            return set._items.IsSubset(_items);
        }

        public bool issuperset([NotNull] FrozenSetCollection set) {
            return set._items.IsSubset(_items);
        }

        public bool issuperset(object? set) {
            return SetStorage.GetItems(set).IsSubset(_items);
        }

        public SetCollection union() {
            return copy();
        }

        public SetCollection union([NotNull] SetCollection set) {
            return Make(SetStorage.Union(_items, set._items));
        }

        public SetCollection union([NotNull] FrozenSetCollection set) {
            return Make(SetStorage.Union(_items, set._items));
        }

        public SetCollection union(object? set) {
            SetStorage items;
            if (SetStorage.GetItems(set, out items)) {
                items = SetStorage.Union(_items, items);
            } else {
                items.UnionUpdate(_items);
            }
            return Make(items);
        }

        public SetCollection union([NotNull] params object[]/*!*/ sets) {
            SetStorage res = _items.Clone();
            foreach (object set in sets) {
                res.UnionUpdate(SetStorage.GetItems(set));
            }

            return Make(res);
        }

        public SetCollection intersection() {
            return copy();
        }

        public SetCollection intersection([NotNull] SetCollection set) {
            return Make(SetStorage.Intersection(_items, set._items));
        }

        public SetCollection intersection([NotNull] FrozenSetCollection set) {
            return Make(SetStorage.Intersection(_items, set._items));
        }

        public SetCollection intersection(object? set) {
            SetStorage items;
            if (SetStorage.GetItems(set, out items)) {
                items = SetStorage.Intersection(_items, items);
            } else {
                items.IntersectionUpdate(_items);
            }
            return Make(items);
        }

        public SetCollection intersection([NotNull] params object[]/*!*/ sets) {
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

        public SetCollection difference([NotNull] SetCollection set) {
            if (object.ReferenceEquals(set, this)) {
                return Empty;
            }

            return Make(
                SetStorage.Difference(_items, set._items)
            );
        }

        public SetCollection difference([NotNull] FrozenSetCollection set) {
            return Make(
                SetStorage.Difference(_items, set._items)
            );
        }

        public SetCollection difference(object? set) {
            return Make(
                SetStorage.Difference(_items, SetStorage.GetItems(set))
            );
        }

        public SetCollection difference([NotNull] params object[]/*!*/ sets) {
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

        public SetCollection symmetric_difference([NotNull] SetCollection set) {
            if (object.ReferenceEquals(set, this)) {
                return Empty;
            }

            return Make(SetStorage.SymmetricDifference(_items, set._items));
        }

        public SetCollection symmetric_difference([NotNull] FrozenSetCollection set) {
            return Make(SetStorage.SymmetricDifference(_items, set._items));
        }

        public SetCollection symmetric_difference(object? set) {
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
        public SetCollection InPlaceBitwiseOr([NotNull] SetCollection set) {
            update(set);
            return this;
        }

        [SpecialName]
        public SetCollection InPlaceBitwiseOr([NotNull] FrozenSetCollection set) {
            update(set);
            return this;
        }

        [SpecialName]
        public SetCollection InPlaceBitwiseOr(object? set) {
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
        public SetCollection InPlaceBitwiseAnd([NotNull] SetCollection set) {
            intersection_update(set);
            return this;
        }

        [SpecialName]
        public SetCollection InPlaceBitwiseAnd([NotNull] FrozenSetCollection set) {
            intersection_update(set);
            return this;
        }

        [SpecialName]
        public SetCollection InPlaceBitwiseAnd(object? set) {
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
        public SetCollection InPlaceExclusiveOr([NotNull] SetCollection set) {
            symmetric_difference_update(set);
            return this;
        }

        [SpecialName]
        public SetCollection InPlaceExclusiveOr([NotNull] FrozenSetCollection set) {
            symmetric_difference_update(set);
            return this;
        }

        [SpecialName]
        public SetCollection InPlaceExclusiveOr(object? set) {
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
        public SetCollection InPlaceSubtract([NotNull] SetCollection set) {
            difference_update(set);
            return this;
        }

        [SpecialName]
        public SetCollection InPlaceSubtract([NotNull] FrozenSetCollection set) {
            difference_update(set);
            return this;
        }

        [SpecialName]
        public SetCollection InPlaceSubtract(object? set) {
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

        public static SetCollection operator |([NotNull] SetCollection x, [NotNull] SetCollection y) {
            return x.union(y);
        }

        public static SetCollection operator &([NotNull] SetCollection x, [NotNull] SetCollection y) {
            return x.intersection(y);
        }

        public static SetCollection operator ^([NotNull] SetCollection x, [NotNull] SetCollection y) {
            return x.symmetric_difference(y);
        }

        public static SetCollection operator -([NotNull] SetCollection x, [NotNull] SetCollection y) {
            return x.difference(y);
        }

        public static SetCollection operator |([NotNull] SetCollection x, [NotNull] FrozenSetCollection y) {
            return x.union(y);
        }

        public static SetCollection operator &([NotNull] SetCollection x, [NotNull] FrozenSetCollection y) {
            return x.intersection(y);
        }

        public static SetCollection operator ^([NotNull] SetCollection x, [NotNull] FrozenSetCollection y) {
            return x.symmetric_difference(y);
        }

        public static SetCollection operator -([NotNull] SetCollection x, [NotNull] FrozenSetCollection y) {
            return x.difference(y);
        }


        // *** END GENERATED CODE ***

        #endregion

        #region Generated Interface Implementations (SetCollection)

        // *** BEGIN GENERATED CODE ***
        // generated by function: _gen_interfaces from: generate_set.py

        #region IRichComparable

        [return: MaybeNotImplemented]
        public static object operator >([NotNull] SetCollection self, object? other) {
            if (SetStorage.GetItemsIfSet(other, out SetStorage items)) {
                return items.IsStrictSubset(self._items);
            }

            return NotImplementedType.Value;
        }

        [return: MaybeNotImplemented]
        public static object operator <([NotNull] SetCollection self, object? other) {
            if (SetStorage.GetItemsIfSet(other, out SetStorage items)) {
                return self._items.IsStrictSubset(items);
            }

            return NotImplementedType.Value;
        }

        [return: MaybeNotImplemented]
        public static object operator >=([NotNull] SetCollection self, object? other) {
            if (SetStorage.GetItemsIfSet(other, out SetStorage items)) {
                return items.IsSubset(self._items);
            }

            return NotImplementedType.Value;
        }

        [return: MaybeNotImplemented]
        public static object operator <=([NotNull] SetCollection self, object? other) {
            if (SetStorage.GetItemsIfSet(other, out SetStorage items)) {
                return self._items.IsSubset(items);
            }

            return NotImplementedType.Value;
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator() {
            return new SetIterator(_items, true);
        }

        #endregion

        #region IEnumerable<object?> Members

        IEnumerator<object?> IEnumerable<object?>.GetEnumerator() {
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
            foreach (var o in this) {
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

        #region IWeakReferenceable Members

        private WeakRefTracker? _tracker;

        WeakRefTracker? IWeakReferenceable.GetWeakRef() {
            return _tracker;
        }

        bool IWeakReferenceable.SetWeakRef(WeakRefTracker value) {
            return Interlocked.CompareExchange(ref _tracker, value, null) == null;
        }

        void IWeakReferenceable.SetFinalizer(WeakRefTracker value) {
            _tracker = value;
        }

        #endregion


        // *** END GENERATED CODE ***

        #endregion
    }

    /// <summary>
    /// Immutable set class
    /// </summary>
    [PythonType("frozenset"), DebuggerDisplay("frozenset, {Count} items", TargetTypeName = "frozenset"), DebuggerTypeProxy(typeof(CollectionDebugProxy))]
    public class FrozenSetCollection : IEnumerable, IEnumerable<object?>, ICollection, IStructuralEquatable, ICodeFormattable, IWeakReferenceable {
        internal readonly SetStorage _items;
        private HashCache? _hashCache;

        #region Set Construction

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "o")]
        public void __init__([NotNull] params object?[] o) {
            // nop
        }

        public static FrozenSetCollection __new__(CodeContext/*!*/ context, [NotNull] PythonType cls) {
            if (cls == TypeCache.FrozenSet) {
                return Empty;
            } else {
                object res = cls.CreateInstance(context);
                if (res is FrozenSetCollection fs) return fs;
                throw PythonOps.TypeError("{0} is not a subclass of frozenset", res);
            }
        }

        public static FrozenSetCollection __new__(CodeContext/*!*/ context, [NotNull] PythonType cls, object? set) {
            if (cls == TypeCache.FrozenSet) {
                return Make(set);
            } else {
                object res = cls.CreateInstance(context, set);
                if (res is FrozenSetCollection fs) return fs;
                throw PythonOps.TypeError("{0} is not a subclass of frozenset", res);
            }
        }

        protected internal FrozenSetCollection() : this(new SetStorage()) { }

        private FrozenSetCollection(SetStorage set) {
            _items = set;
        }

        protected internal FrozenSetCollection(object set) : this(SetStorage.GetFrozenItems(set)) { }

        private static FrozenSetCollection Empty { get; } = new FrozenSetCollection(new SetStorage());

        internal static FrozenSetCollection Make(SetStorage items) {
            if (items.Count == 0) {
                return Empty;
            }

            return new FrozenSetCollection(items);
        }

        internal static FrozenSetCollection Make(object? set) {
            if (set?.GetType() == typeof(FrozenSetCollection)) {
                return (FrozenSetCollection)set;
            }

            return Make(SetStorage.GetFrozenItems(set));
        }

        public FrozenSetCollection copy() {
            if (this.GetType() == typeof(FrozenSetCollection)) {
                return this;
            }
            return Make(_items);
        }

        #endregion

        #region Protocol Methods

        public int __len__() {
            return Count;
        }

        public bool __contains__(object? item) {
            if (!SetStorage.GetHashableSetIfSet(ref item)) {
                // make sure we have a hashable item
                return _items.ContainsAlwaysHash(item);
            }
            return _items.Contains(item);
        }

        public PythonTuple __reduce__(CodeContext context) {
            return PythonTuple.MakeTuple(
                DynamicHelpers.GetPythonType(this),
                PythonTuple.MakeTuple(_items.GetItems()),
                GetType() == typeof(FrozenSetCollection) ? null : (ObjectOps.ReduceProtocol0(context, this) is PythonTuple tuple ? tuple[2] : null)
            );
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

            HashCache? curHashCache = _hashCache;
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

        bool IStructuralEquatable.Equals(object? other, IEqualityComparer comparer) {
            SetStorage items;
            return SetStorage.GetItemsIfSet(other, out items) &&
                SetStorage.Equals(_items, items, comparer);
        }

        // default conversion of protocol methods only allows our specific type for equality,
        // but sets can do __eq__ / __ne__ against any type. This is why we define a separate
        // __eq__ / __ne__ here.

        [return: MaybeNotImplemented]
        public object __eq__(object? other) {
            if (SetStorage.GetItemsIfSet(other, out SetStorage items)) {
                return _items.Count == items.Count && _items.IsSubset(items);
            }
            return NotImplementedType.Value;
        }

        [return: MaybeNotImplemented]
        public object __ne__(object? other) {
            if (SetStorage.GetItemsIfSet(other, out SetStorage items)) {
                return _items.Count != items.Count || !_items.IsSubset(items);
            }
            return NotImplementedType.Value;
        }

        #endregion

        #region Generated NonOperator Operations (FrozenSetCollection)

        // *** BEGIN GENERATED CODE ***
        // generated by function: _gen_setops from: generate_set.py

        public bool isdisjoint([NotNull] FrozenSetCollection set) {
            return _items.IsDisjoint(set._items);
        }

        public bool isdisjoint([NotNull] SetCollection set) {
            return _items.IsDisjoint(set._items);
        }

        public bool isdisjoint(object? set) {
            return _items.IsDisjoint(SetStorage.GetItems(set));
        }

        public bool issubset([NotNull] FrozenSetCollection set) {
            return _items.IsSubset(set._items);
        }

        public bool issubset([NotNull] SetCollection set) {
            return _items.IsSubset(set._items);
        }

        public bool issubset(object? set) {
            return _items.IsSubset(SetStorage.GetItems(set));
        }

        public bool issuperset([NotNull] FrozenSetCollection set) {
            return set._items.IsSubset(_items);
        }

        public bool issuperset([NotNull] SetCollection set) {
            return set._items.IsSubset(_items);
        }

        public bool issuperset(object? set) {
            return SetStorage.GetItems(set).IsSubset(_items);
        }

        public FrozenSetCollection union() {
            return Make(_items);
        }

        public FrozenSetCollection union([NotNull] FrozenSetCollection set) {
            return Make(SetStorage.Union(_items, set._items));
        }

        public FrozenSetCollection union([NotNull] SetCollection set) {
            return Make(SetStorage.Union(_items, set._items));
        }

        public FrozenSetCollection union(object? set) {
            SetStorage items;
            if (SetStorage.GetItems(set, out items)) {
                items = SetStorage.Union(_items, items);
            } else {
                items.UnionUpdate(_items);
            }
            return Make(items);
        }

        public FrozenSetCollection union([NotNull] params object[]/*!*/ sets) {
            SetStorage res = _items.Clone();
            foreach (object set in sets) {
                res.UnionUpdate(SetStorage.GetItems(set));
            }

            return Make(res);
        }

        public FrozenSetCollection intersection() {
            return Make(_items);
        }

        public FrozenSetCollection intersection([NotNull] FrozenSetCollection set) {
            return Make(SetStorage.Intersection(_items, set._items));
        }

        public FrozenSetCollection intersection([NotNull] SetCollection set) {
            return Make(SetStorage.Intersection(_items, set._items));
        }

        public FrozenSetCollection intersection(object? set) {
            SetStorage items;
            if (SetStorage.GetItems(set, out items)) {
                items = SetStorage.Intersection(_items, items);
            } else {
                items.IntersectionUpdate(_items);
            }
            return Make(items);
        }

        public FrozenSetCollection intersection([NotNull] params object[]/*!*/ sets) {
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

        public FrozenSetCollection difference([NotNull] FrozenSetCollection set) {
            if (object.ReferenceEquals(set, this)) {
                return Empty;
            }

            return Make(
                SetStorage.Difference(_items, set._items)
            );
        }

        public FrozenSetCollection difference([NotNull] SetCollection set) {
            return Make(
                SetStorage.Difference(_items, set._items)
            );
        }

        public FrozenSetCollection difference(object? set) {
            return Make(
                SetStorage.Difference(_items, SetStorage.GetItems(set))
            );
        }

        public FrozenSetCollection difference([NotNull] params object[]/*!*/ sets) {
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

        public FrozenSetCollection symmetric_difference([NotNull] FrozenSetCollection set) {
            if (object.ReferenceEquals(set, this)) {
                return Empty;
            }

            return Make(SetStorage.SymmetricDifference(_items, set._items));
        }

        public FrozenSetCollection symmetric_difference([NotNull] SetCollection set) {
            return Make(SetStorage.SymmetricDifference(_items, set._items));
        }

        public FrozenSetCollection symmetric_difference(object? set) {
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

        public static FrozenSetCollection operator |([NotNull] FrozenSetCollection x, [NotNull] FrozenSetCollection y) {
            return x.union(y);
        }

        public static FrozenSetCollection operator &([NotNull] FrozenSetCollection x, [NotNull] FrozenSetCollection y) {
            return x.intersection(y);
        }

        public static FrozenSetCollection operator ^([NotNull] FrozenSetCollection x, [NotNull] FrozenSetCollection y) {
            return x.symmetric_difference(y);
        }

        public static FrozenSetCollection operator -([NotNull] FrozenSetCollection x, [NotNull] FrozenSetCollection y) {
            return x.difference(y);
        }

        public static FrozenSetCollection operator |([NotNull] FrozenSetCollection x, [NotNull] SetCollection y) {
            return x.union(y);
        }

        public static FrozenSetCollection operator &([NotNull] FrozenSetCollection x, [NotNull] SetCollection y) {
            return x.intersection(y);
        }

        public static FrozenSetCollection operator ^([NotNull] FrozenSetCollection x, [NotNull] SetCollection y) {
            return x.symmetric_difference(y);
        }

        public static FrozenSetCollection operator -([NotNull] FrozenSetCollection x, [NotNull] SetCollection y) {
            return x.difference(y);
        }


        // *** END GENERATED CODE ***

        #endregion

        #region Generated Interface Implementations (FrozenSetCollection)

        // *** BEGIN GENERATED CODE ***
        // generated by function: _gen_interfaces from: generate_set.py

        #region IRichComparable

        [return: MaybeNotImplemented]
        public static object operator >([NotNull] FrozenSetCollection self, object? other) {
            if (SetStorage.GetItemsIfSet(other, out SetStorage items)) {
                return items.IsStrictSubset(self._items);
            }

            return NotImplementedType.Value;
        }

        [return: MaybeNotImplemented]
        public static object operator <([NotNull] FrozenSetCollection self, object? other) {
            if (SetStorage.GetItemsIfSet(other, out SetStorage items)) {
                return self._items.IsStrictSubset(items);
            }

            return NotImplementedType.Value;
        }

        [return: MaybeNotImplemented]
        public static object operator >=([NotNull] FrozenSetCollection self, object? other) {
            if (SetStorage.GetItemsIfSet(other, out SetStorage items)) {
                return items.IsSubset(self._items);
            }

            return NotImplementedType.Value;
        }

        [return: MaybeNotImplemented]
        public static object operator <=([NotNull] FrozenSetCollection self, object? other) {
            if (SetStorage.GetItemsIfSet(other, out SetStorage items)) {
                return self._items.IsSubset(items);
            }

            return NotImplementedType.Value;
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator() {
            return new SetIterator(_items, false);
        }

        #endregion

        #region IEnumerable<object?> Members

        IEnumerator<object?> IEnumerable<object?>.GetEnumerator() {
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
            foreach (var o in this) {
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

        #region IWeakReferenceable Members

        private WeakRefTracker? _tracker;

        WeakRefTracker? IWeakReferenceable.GetWeakRef() {
            return _tracker;
        }

        bool IWeakReferenceable.SetWeakRef(WeakRefTracker value) {
            return Interlocked.CompareExchange(ref _tracker, value, null) == null;
        }

        void IWeakReferenceable.SetFinalizer(WeakRefTracker value) {
            _tracker = value;
        }

        #endregion


        // *** END GENERATED CODE ***

        #endregion
    }

    /// <summary>
    /// Iterator over sets
    /// </summary>
    [PythonType("set_iterator")]
    public sealed class SetIterator : IEnumerable, IEnumerable<object?>, IEnumerator, IEnumerator<object?> {
        private readonly SetStorage _items;
        private readonly int _version;
        private readonly int _maxIndex;
        private int _index = -2;
        private int _cnt = 0;

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

        [PythonHidden]
        public object? Current {
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

            _cnt++;

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

            _cnt = -1;
            return false;
        }

        [PythonHidden]
        public void Reset() {
            _index = -2;
            _cnt = 0;
        }

        #endregion

        #region IEnumerable Members

        [PythonHidden]
        public IEnumerator GetEnumerator() {
            return this;
        }

        #endregion

        #region IEnumerable<object> Members

        IEnumerator<object?> IEnumerable<object?>.GetEnumerator() {
            return this;
        }

        #endregion

        public PythonTuple __reduce__(CodeContext/*!*/ context) {
            object? iter;
            context.TryLookupBuiltin("iter", out iter);
            if (_cnt < 0)
                return PythonTuple.MakeTuple(iter, PythonTuple.MakeTuple(new PythonList()));
            return PythonTuple.MakeTuple(iter, PythonTuple.MakeTuple(new PythonList(_items)), _cnt);
        }

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
