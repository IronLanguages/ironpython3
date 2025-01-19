// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections;
using System.Numerics;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime {
    [PythonType("slice")]
    public sealed class Slice : ICodeFormattable, ISlice {
        public Slice(object? stop) : this(null, stop, null) { }

        public Slice(object? start, object? stop) : this(start, stop, null) { }

        public Slice(object? start, object? stop, object? step) {
            this.start = start;
            this.stop = stop;
            this.step = step;
        }

        #region Python Public API Surface

        public object? start { get; }

        public object? stop { get; }

        public object? step { get; }

        public void indices(int length, out BigInteger ostart, out BigInteger ostop, out BigInteger ostep) {
            if (length < 0) throw PythonOps.ValueError("length should not be negative");
            PythonOps.FixSlice(this, length, out ostart, out ostop, out ostep);
        }

        public void indices(object? length, out BigInteger ostart, out BigInteger ostop, out BigInteger ostep) {
            var index = PythonOps.Index(length);
            BigInteger len = index is int i ? i : (BigInteger)index;
            if (len < 0) throw PythonOps.ValueError("length should not be negative");
            PythonOps.FixSlice(this, len, out ostart, out ostop, out ostep);
        }

        public PythonTuple __reduce__() {
            return PythonTuple.MakeTuple(
                DynamicHelpers.GetPythonTypeFromType(typeof(Slice)),
                PythonTuple.MakeTuple(
                    start,
                    stop,
                    step
                )
            );
        }

        #endregion

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public int __hash__() {
            throw PythonOps.TypeErrorForUnhashableType("slice");
        }

        #region ISlice Members

        object? ISlice.Start => start;

        object? ISlice.Stop => stop;

        object? ISlice.Step => step;

        #endregion

        #region ICodeFormattable Members

        public string/*!*/ __repr__(CodeContext/*!*/ context) {
            return string.Format("slice({0}, {1}, {2})", PythonOps.Repr(context, start), PythonOps.Repr(context, stop), PythonOps.Repr(context, step));
        }

        #endregion

        #region Rich Comparison Members

        private PythonTuple ToTuple() => PythonTuple.MakeTuple(start, stop, step);

        private bool Equals(Slice other) => ToTuple().Equals(other.ToTuple());

        public bool __eq__([NotNone] Slice other) => Equals(other);

        public bool __ne__([NotNone] Slice other) => !Equals(other);

        public static object? operator >([NotNone] Slice self, [NotNone] Slice other)=> self.ToTuple() < other.ToTuple();

        public static object? operator <([NotNone] Slice self, [NotNone] Slice other)=> self.ToTuple() < other.ToTuple();

        public static object? operator >=([NotNone] Slice self, [NotNone] Slice other) => self.ToTuple() >= other.ToTuple();

        public static object? operator <=([NotNone] Slice self, [NotNone] Slice other) => self.ToTuple() <= other.ToTuple();

        #endregion

        #region Internal Implementation details

        /// <summary>
        /// Like indices but only works for values in the Int32 domain.
        /// </summary>
        internal void Indices(int length, out int ostart, out int ostop, out int ostep)
            => PythonOps.FixSlice(this, length, out ostart, out ostop, out ostep);

        internal delegate void SliceAssign(int index, object? value);

        internal void DoSliceAssign(SliceAssign assign, int size, object? value) {
            Indices(size, out int ostart, out int ostop, out int ostep);
            DoSliceAssign(assign, ostart, ostop, ostep, value);
        }

        private static void DoSliceAssign(SliceAssign assign, int start, int stop, int step, object? value) {
            // fast paths, if we know the size then we can
            // do this quickly.
            if (value is IList list) {
                int count = PythonOps.GetSliceCount(start, stop, step);
                ListSliceAssign(assign, start, count, step, list);
            } else {
                OtherSliceAssign(assign, start, stop, step, value);
            }
        }

        private static void ListSliceAssign(SliceAssign assign, int start, int n, int step, IList lst) {
            if (lst.Count < n) throw PythonOps.ValueError("too few items in the enumerator. need {0} have {1}", n, lst.Count);
            else if (lst.Count != n) throw PythonOps.ValueError("too many items in the enumerator need {0} have {1}", n, lst.Count);

            for (int i = 0, index = start; i < n; i++, index += step) {
                assign(index, lst[i]);
            }
        }

        private static void OtherSliceAssign(SliceAssign assign, int start, int stop, int step, object? value) {
            // get enumerable data into a list, and then
            // do the slice.
            IEnumerator enumerator = PythonOps.GetEnumerator(value);
            PythonList sliceData = new PythonList();
            while (enumerator.MoveNext()) sliceData.AddNoLock(enumerator.Current);

            DoSliceAssign(assign, start, stop, step, sliceData);
        }

        internal void GetIndicesAndCount(int length, out int ostart, out int ostop, out int ostep, out int count) {
            Indices(length, out ostart, out ostop, out ostep);
            count = PythonOps.GetSliceCount(ostart, ostop, ostep);
        }

        #endregion
    }
}
