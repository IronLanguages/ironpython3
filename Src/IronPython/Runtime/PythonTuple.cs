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

using MSAst = System.Linq.Expressions;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Text;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Compiler.Ast;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Utils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Runtime {
    using Ast = MSAst.Expression;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix")]
    [PythonType("tuple"), Serializable, DebuggerTypeProxy(typeof(CollectionDebugProxy)), DebuggerDisplay("tuple, {Count} items")]
    public class PythonTuple : ICollection, IEnumerable, IEnumerable<object>, IList, IList<object>, ICodeFormattable, IExpressionSerializable,
        IStructuralEquatable, IStructuralComparable
#if FEATURE_READONLY_COLLECTION_INTERFACE
        , IReadOnlyList<object>
#endif
    {
        internal readonly object[] _data;
        
        internal static readonly PythonTuple EMPTY = new PythonTuple();

        public PythonTuple(object o) {
            this._data = MakeItems(o);
        }

        protected PythonTuple(object[] items) {
            this._data = items;
        }

        public PythonTuple() {
            this._data = ArrayUtils.EmptyObjects;
        }

        internal PythonTuple(PythonTuple other, object o) {
            this._data = other.Expand(o);
        }

        #region Python Constructors

        // Tuples are immutable so their initialization happens in __new__
        // They also explicitly implement __new__ so they can perform the
        // appropriate caching.  

        public static PythonTuple __new__(CodeContext context, PythonType cls) {
            if (cls == TypeCache.PythonTuple) {
                return EMPTY;
            } else {
                PythonTuple tupObj = cls.CreateInstance(context) as PythonTuple;
                if (tupObj == null) throw PythonOps.TypeError("{0} is not a subclass of tuple", cls);
                return tupObj;
            }
        }

        public static PythonTuple __new__(CodeContext context, PythonType cls, object sequence) {
            if (sequence == null) throw PythonOps.TypeError("iteration over a non-sequence");

            if (cls == TypeCache.PythonTuple) {
                if (sequence.GetType() == typeof(PythonTuple)) return (PythonTuple)sequence;
                return new PythonTuple(MakeItems(sequence));
            } else {
                PythonTuple tupObj = cls.CreateInstance(context, sequence) as PythonTuple;
                if (tupObj == null) throw PythonOps.TypeError("{0} is not a subclass of tuple", cls);
                return tupObj;
            }
        }
        #endregion

        #region Python 2.6 Methods

        public int index(object obj, object start) {
            return index(obj, Converter.ConvertToIndex(start), _data.Length);
        }

        public int index(object obj, int start = 0) {
            return index(obj, start, _data.Length);
        }

        public int index(object obj, object start, object end) {
            return index(obj, Converter.ConvertToIndex(start), Converter.ConvertToIndex(end));
        }

        public int index(object obj, int start, int end) {
            start = PythonOps.FixSliceIndex(start, _data.Length);
            end = PythonOps.FixSliceIndex(end, _data.Length);

            for (int i = start; i < end; i++) {
                if (PythonOps.IsOrEqualsRetBool(obj, _data[i])) {
                    return i;
                }
            }

            throw PythonOps.ValueError("tuple.index(x): x not in list");
        }

        public int count(object obj) {
            int cnt = 0;
            foreach (object elem in _data) {
                if (PythonOps.IsOrEqualsRetBool(obj, elem)) {
                    cnt++;
                }
            }
            return cnt;
        }

        #endregion

        internal static PythonTuple Make(object o) {
            if (o is PythonTuple) return (PythonTuple)o;
            return new PythonTuple(MakeItems(o));
        }

        internal static PythonTuple MakeTuple(params object[] items) {
            if (items.Length == 0) return EMPTY;
            return new PythonTuple(items);
        }

        private static object[] MakeItems(object o) {
            object[] arr;
            if (o is PythonTuple) {
                return ((PythonTuple)o)._data;
            } else if (o is string) {
                string s = (string)o;
                object[] res = new object[s.Length];
                for (int i = 0; i < res.Length; i++) {
                    res[i] = ScriptingRuntimeHelpers.CharToString(s[i]);
                }
                return res;
            } else if (o is List) {
                return ((List)o).GetObjectArray();
            } else if ((arr = o as object[])!=null) {
                return ArrayOps.CopyArray(arr, arr.Length);
            } else {
                PerfTrack.NoteEvent(PerfTrack.Categories.OverAllocate, "TupleOA: " + PythonTypeOps.GetName(o));

                List<object> l = new List<object>();
                IEnumerator i = PythonOps.GetEnumerator(o);
                while (i.MoveNext()) {
                    l.Add(i.Current);
                }

                return l.ToArray();
            }
        }
        
        /// <summary>
        /// Return a copy of this tuple's data array.
        /// </summary>
        internal object[] ToArray() {
            return ArrayOps.CopyArray(_data, _data.Length);
        }

        #region ISequence Members

        public virtual int __len__() {
            return _data.Length;
        }

        public virtual object this[int index] {
            get {
                return _data[PythonOps.FixIndex(index, _data.Length)];
            }
        }
        
        public virtual object this[object index] {
            get {
                return this[Converter.ConvertToIndex(index)];
            }
        }

        public virtual object this[BigInteger index] {
            get {
                return this[(int)index];
            }
        }

        public virtual object this[Slice slice] {
            get {
                int start, stop, step;
                slice.indices(_data.Length, out start, out stop, out step);

                if (start == 0 && stop == _data.Length && step == 1 &&
                    this.GetType() == typeof(PythonTuple)) {
                    return this;
                }
                return MakeTuple(ArrayOps.GetSlice(_data, start, stop, step));
            }
        }

        #endregion

        #region binary operators
        public static PythonTuple operator +([NotNull]PythonTuple x, [NotNull]PythonTuple y) {
            return MakeTuple(ArrayOps.Add(x._data, x._data.Length, y._data, y._data.Length));
        }

        private static PythonTuple MultiplyWorker(PythonTuple self, int count) {
            if (count <= 0) {
                return EMPTY;
            } else if (count == 1 && self.GetType() == typeof(PythonTuple)) {
                return self;
            }

            return MakeTuple(ArrayOps.Multiply(self._data, self._data.Length, count));
        }

        public static PythonTuple operator *(PythonTuple x, int n) {
            return MultiplyWorker(x, n);
        }

        public static PythonTuple operator *(int n, PythonTuple x) {
            return MultiplyWorker(x, n);
        }

        public static object operator *([NotNull]PythonTuple self, [NotNull]Index count) {
            return PythonOps.MultiplySequence<PythonTuple>(MultiplyWorker, self, count, true);
        }

        public static object operator *([NotNull]Index count, [NotNull]PythonTuple self) {
            return PythonOps.MultiplySequence<PythonTuple>(MultiplyWorker, self, count, false);
        }

        public static object operator *([NotNull]PythonTuple self, object count) {
            int index;
            if (Converter.TryConvertToIndex(count, out index)) {
                return self * index;
            }
            throw PythonOps.TypeErrorForUnIndexableObject(count);
        }

        public static object operator *(object count, [NotNull]PythonTuple self) {
            int index;
            if (Converter.TryConvertToIndex(count, out index)) {
                return index * self;
            }

            throw PythonOps.TypeErrorForUnIndexableObject(count);
        }

        #endregion

        #region ICollection Members

        bool ICollection.IsSynchronized {
            get { return false; }
        }

        public int Count {
            [PythonHidden]
            get { return _data.Length; }
        }

        [PythonHidden]
        public void CopyTo(Array array, int index) {
            Array.Copy(_data, 0, array, index, _data.Length);
        }

        object ICollection.SyncRoot {
            get {
                return this;
            }
        }

        #endregion

        public virtual IEnumerator __iter__() {
            return new TupleEnumerator(this);
        }

        #region IEnumerable Members

        [PythonHidden]
        public IEnumerator GetEnumerator() {
            return __iter__();
        }

        #endregion

        private object[] Expand(object value) {
            object[] args;
            int length = _data.Length;
            if (value == null)
                args = new object[length];
            else
                args = new object[length + 1];

            for (int i = 0; i < length; i++) {
                args[i] = _data[i];
            }

            if (value != null) {
                args[length] = value;
            }

            return args;
        }

        public object __getnewargs__() {
            // Call "new Tuple()" to force result to be a Tuple (otherwise, it could possibly be a Tuple subclass)
            return PythonTuple.MakeTuple(new PythonTuple(this));
        }

        #region IEnumerable<object> Members

        IEnumerator<object> IEnumerable<object>.GetEnumerator() {
            return new TupleEnumerator(this);
        }

        #endregion

        #region IList<object> Members

        [PythonHidden]
        public int IndexOf(object item) {
            for (int i = 0; i < Count; i++) {
                if (PythonOps.IsOrEqualsRetBool(this[i], item)) return i;
            }
            return -1;
        }

        void IList<object>.Insert(int index, object item) {
            throw new InvalidOperationException("Tuple is readonly");
        }

        void IList<object>.RemoveAt(int index) {
            throw new InvalidOperationException("Tuple is readonly");
        }

        object IList<object>.this[int index] {
            get {
                return this[index];
            }
            set {
                throw new InvalidOperationException("Tuple is readonly");
            }
        }

        #endregion

        #region ICollection<object> Members

        void ICollection<object>.Add(object item) {
            throw new InvalidOperationException("Tuple is readonly");
        }

        void ICollection<object>.Clear() {
            throw new InvalidOperationException("Tuple is readonly");
        }

        [PythonHidden]
        public bool Contains(object item) {
            for (int i = 0; i < _data.Length; i++) {
                if (PythonOps.IsOrEqualsRetBool(_data[i], item)) {
                    return true;
                }
            }

            return false;
        }

        [PythonHidden]
        public void CopyTo(object[] array, int arrayIndex) {
            for (int i = 0; i < Count; i++) {
                array[arrayIndex + i] = this[i];
            }
        }

        bool ICollection<object>.IsReadOnly {
            get { return true; }
        }

        bool ICollection<object>.Remove(object item) {
            throw new InvalidOperationException("Tuple is readonly");
        }

        #endregion

        #region Rich Comparison Members

        internal int CompareTo(PythonTuple other) {
            return PythonOps.CompareArrays(_data, _data.Length, other._data, other._data.Length);
        }

        public static bool operator >([NotNull]PythonTuple self, [NotNull]PythonTuple other) {
            return self.CompareTo(other) > 0;
        }

        public static bool operator <([NotNull]PythonTuple self, [NotNull]PythonTuple other) {
            return self.CompareTo(other) < 0;
        }

        public static bool operator >=([NotNull]PythonTuple self, [NotNull]PythonTuple other) {
            return self.CompareTo(other) >= 0;
        }

        public static bool operator <=([NotNull]PythonTuple self, [NotNull]PythonTuple other) {
            return self.CompareTo(other) <= 0;
        }

        #endregion

        #region IStructuralComparable Members

        int IStructuralComparable.CompareTo(object obj, IComparer comparer) {
            PythonTuple other = obj as PythonTuple;
            if (other == null) {
                throw new ValueErrorException("expected tuple");
            }

            return PythonOps.CompareArrays(_data, _data.Length, other._data, other._data.Length, comparer);
        }

        #endregion

        public override bool Equals(object obj) {
            if (!Object.ReferenceEquals(this, obj)) {
                PythonTuple other = obj as PythonTuple;
                if (other == null || _data.Length != other._data.Length) {
                    return false;
                }

                for (int i = 0; i < _data.Length; i++) {
                    object obj1 = this[i], obj2 = other[i];

                    if (Object.ReferenceEquals(obj1, obj2)) {
                        continue;
                    } else if (obj1 != null) {
                        if (!obj1.Equals(obj2)) {
                            return false;
                        }
                    } else {
                        return false;
                    }
                }
            }
            return true;
        }

        public override int GetHashCode() {
            int hash1 = 6551;
            int hash2 = hash1;

            for (int i = 0; i < _data.Length; i += 2) {
                hash1 = ((hash1 << 27) + ((hash2 + 1) << 1) + (hash1 >> 5)) ^ (_data[i] == null ? NoneTypeOps.NoneHashCode : _data[i].GetHashCode());

                if (i == _data.Length - 1) {
                    break;
                }
                hash2 = ((hash2 << 5) + ((hash1 - 1) >> 1) + (hash2 >> 27)) ^ (_data[i + 1] == null ? NoneTypeOps.NoneHashCode : _data[i + 1].GetHashCode());
            }
            return hash1 + (hash2 * 1566083941);
        }

        private int GetHashCode(HashDelegate dlg) {
            int hash1 = 6551;
            int hash2 = hash1;

            for (int i = 0; i < _data.Length; i += 2) {
                hash1 = ((hash1 << 27) + ((hash2 + 1) << 1) + (hash1 >> 5)) ^ dlg(_data[i], ref dlg);

                if (i == _data.Length - 1) {
                    break;
                }
                hash2 = ((hash2 << 5) + ((hash1 - 1) >> 1) + (hash2 >> 27)) ^ dlg(_data[i + 1], ref dlg);
            }
            return hash1 + (hash2 * 1566083941);
        }

        private int GetHashCode(IEqualityComparer comparer) {
            int hash1 = 6551;
            int hash2 = hash1;

            for (int i = 0; i < _data.Length; i += 2) {
                hash1 = ((hash1 << 27) + ((hash2 + 1) << 1) + (hash1 >> 5)) ^ comparer.GetHashCode(_data[i]);

                if (i == _data.Length - 1) {
                    break;
                }
                hash2 = ((hash2 << 5) + ((hash1 - 1) >> 1) + (hash2 >> 27)) ^ comparer.GetHashCode(_data[i + 1]);
            }
            return hash1 + (hash2 * 1566083941);
        }

        public override string ToString() {
            return __repr__(DefaultContext.Default);
        }

        #region IStructuralEquatable Members

        int IStructuralEquatable.GetHashCode(IEqualityComparer comparer) {
            // Optimization for when comparer is IronPython's default IEqualityComparer
            PythonContext.PythonEqualityComparer pythonComparer = comparer as PythonContext.PythonEqualityComparer;
            if (pythonComparer != null) {
                return GetHashCode(pythonComparer.Context.InitialHasher);
            }

            return GetHashCode(comparer);
        }

        bool IStructuralEquatable.Equals(object other, IEqualityComparer comparer) {
            if (!Object.ReferenceEquals(other, this)) {
                PythonTuple l = other as PythonTuple;
                if (l == null || _data.Length != l._data.Length) {
                    return false;
                }

                for (int i = 0; i < _data.Length; i++) {
                    object obj1 = _data[i], obj2 = l._data[i];

                    if (Object.ReferenceEquals(obj1, obj2)) {
                        continue;
                    } else if (!comparer.Equals(obj1, obj2)) {
                        return false;
                    }
                }
            }
            return true;
        }

        #endregion

        #region ICodeFormattable Members

        public virtual string/*!*/ __repr__(CodeContext/*!*/ context) {
            StringBuilder buf = new StringBuilder();
            buf.Append("(");
            for (int i = 0; i < _data.Length; i++) {
                if (i > 0) buf.Append(", ");
                buf.Append(PythonOps.Repr(context, _data[i]));
            }
            if (_data.Length == 1) buf.Append(",");
            buf.Append(")");
            return buf.ToString();
        }

        #endregion

        #region IList Members

        int IList.Add(object value) {
            throw new InvalidOperationException("Tuple is readonly");
        }

        void IList.Clear() {
            throw new InvalidOperationException("Tuple is readonly");
        }

        void IList.Insert(int index, object value) {
            throw new InvalidOperationException("Tuple is readonly");
        }

        bool IList.IsFixedSize {
            get { return true; }
        }

        bool IList.IsReadOnly {
            get { return true; }
        }

        void IList.Remove(object value) {
            throw new InvalidOperationException("Tuple is readonly");
        }

        void IList.RemoveAt(int index) {
            throw new InvalidOperationException("Tuple is readonly");
        }

        object IList.this[int index] {
            get {
                return this[index];
            }
            set {
                throw new InvalidOperationException("Tuple is readonly");
            }
        }

        #endregion

        #region IExpressionSerializable Members

        public MSAst.Expression CreateExpression() {
            Ast[] items = new Ast[Count];
            for (int i = 0; i < items.Length; i++) {
                items[i] = Expression.Convert(Utils.Constant(this[i]), typeof(object));
            }

            return Ast.Call(
                AstMethods.MakeTuple,
                Ast.NewArrayInit(
                    typeof(object),
                    items
                )
            );

        }

        #endregion
    }

    /// <summary>
    /// public class to get optimized
    /// </summary>
    [PythonType("tupleiterator")]
    public sealed class TupleEnumerator : IEnumerable, IEnumerator, IEnumerator<object> {
        private int _curIndex;
        private PythonTuple _tuple;

        public TupleEnumerator(PythonTuple t) {
            _tuple = t;
            _curIndex = -1;
        }

        #region IEnumerator Members

        public object Current {
            get {
                // access _data directly because this is what CPython does:
                // class T(tuple):
                //     def __getitem__(self): return None
                // 
                // for x in T((1,2)): print x
                // prints 1 and 2
                return _tuple._data[_curIndex];
            }
        }

        public bool MoveNext() {
            if ((_curIndex + 1) >= _tuple.Count) {
                return false;
            }
            _curIndex++;
            return true;
        }

        public void Reset() {
            _curIndex = -1;
        }

        #endregion

        #region IDisposable Members

        public void Dispose() {
            GC.SuppressFinalize(this);
        }

        #endregion

        #region IEnumerable Members

        public IEnumerator GetEnumerator() {
            return this;
        }

        #endregion
    }
}
