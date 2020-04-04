// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;
using IronPython.Runtime.Exceptions;

namespace IronPython.Runtime {
    [PythonType("slice")]
    public sealed class Slice : ICodeFormattable, IComparable, ISlice {
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

        public void indices(int length, out int ostart, out int ostop, out int ostep) {
            if (length < 0) throw PythonOps.ValueError("length should not be negative");
            PythonOps.FixSlice(length, start, stop, step, out ostart, out ostop, out ostep);
        }

        public void indices(object? length, out int ostart, out int ostop, out int ostep) {
            indices(Converter.ConvertToIndex(length), out ostart, out ostop, out ostep);
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

        #region IComparable Members

        int IComparable.CompareTo(object? obj) {
            if (obj is Slice other) return Compare(other);
            throw new ValueErrorException("expected slice");
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

        #region Internal Implementation details

        internal delegate void SliceAssign(int index, object? value);

        internal void DoSliceAssign(SliceAssign assign, int size, object? value) {
            int ostart, ostop, ostep;
            indices(size, out ostart, out ostop, out ostep);
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
            indices(length, out ostart, out ostop, out ostep);
            count = PythonOps.GetSliceCount(ostart, ostop, ostep);
        }

        private int Compare(Slice obj) {
            return PythonOps.CompareArrays(new object?[] { start, stop, step }, 3,
                new object?[] { obj.start, obj.stop, obj.step }, 3);
        }

        #endregion
    }
}
