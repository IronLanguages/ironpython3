// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Types;

namespace IronPython.Runtime.Operations {
    public static class ListOfTOps<T> {
        public static string __repr__(CodeContext/*!*/ context, List<T> self) {
            List<object> infinite = PythonOps.GetAndCheckInfinite(self);
            if (infinite == null) {
                return "[...]";
            }

            int index = infinite.Count;
            infinite.Add(self);
            try {
                StringBuilder res = new StringBuilder();
                res.Append("List[");
                res.Append(DynamicHelpers.GetPythonTypeFromType(typeof(T)).Name);
                res.Append("](");
                if (self.Count > 0) {
                    res.Append("[");
                    string comma = "";
                    foreach (T obj in self) {
                        res.Append(comma);
                        res.Append(PythonOps.Repr(context, obj));
                        comma = ", ";
                    }
                    res.Append("]");
                }

                res.Append(")");
                return res.ToString();
            } finally {
                System.Diagnostics.Debug.Assert(index == infinite.Count - 1);
                infinite.RemoveAt(index);
            }
        }

        #region Python __ methods

        [SpecialName]
        public static void DeleteItem(List<T> l, int index) {
            l.RemoveAt(PythonOps.FixIndex(index, l.Count));
        }

        [SpecialName]
        public static void DeleteItem(List<T> l, object index) {
            DeleteItem(l, Converter.ConvertToIndex(index));
        }

        [SpecialName]
        public static void DeleteItem(List<T> l, Slice slice) {
            if (slice == null) {
                throw PythonOps.TypeError("List<T> indices must be slices or integers");
            }

            int start, stop, step;
            // slice is sealed, indices can't be user code...
            slice.indices(l.Count, out start, out stop, out step);

            if (step > 0 && (start >= stop)) return;
            if (step < 0 && (start <= stop)) return;

            if (step == 1) {
                int i = start;
                for (int j = stop; j < l.Count; j++, i++) {
                    l[i] = l[j];
                }
                l.RemoveRange(i, stop - start);
                return;
            } else if (step == -1) {
                int i = stop + 1;
                for (int j = start + 1; j < l.Count; j++, i++) {
                    l[i] = l[j];
                }
                l.RemoveRange(i, start - stop);
                return;
            } else if (step < 0) {
                // find "start" we will skip in the 1,2,3,... order
                int i = start;
                while (i > stop) {
                    i += step;
                }
                i -= step;

                // swap start/stop, make step positive
                stop = start + 1;
                start = i;
                step = -step;
            }

            int curr, skip, move;
            // skip: the next position we should skip
            // curr: the next position we should fill in data
            // move: the next position we will check
            curr = skip = move = start;

            while (curr < stop && move < stop) {
                if (move != skip) {
                    l[curr++] = l[move];
                } else
                    skip += step;
                move++;
            }
            while (stop < l.Count) {
                l[curr++] = l[stop++];
            }
            l.RemoveRange(curr, l.Count - curr);
        }

        public static T __getitem__(List<T> l, int index) {
            return l[index];
        }

        public static List<T> __getitem__(List<T> l, Slice slice) {
            if (slice == null) throw PythonOps.TypeError("List<T> indices must be slices or integers");
            int start, stop, step;
            slice.indices(l.Count, out start, out stop, out step);
            if (step == 1) {
                return stop > start ? l.Skip(start).Take(stop - start).ToList() : new List<T>();
            } else {
                List<T> newData;
                if (step > 0) {
                    if (start > stop) return new List<T>();

                    int icnt = (stop - start + step - 1) / step;
                    newData = new List<T>(icnt);
                    for (int i = start; i < stop; i += step) {
                        newData.Add(l[i]);
                    }
                } else {
                    if (start < stop) return new List<T>();

                    int icnt = (stop - start + step + 1) / step;
                    newData = new List<T>(icnt);
                    for (int i = start; i > stop; i += step) {
                        newData.Add(l[i]);
                    }
                }
                return newData;
            }
        }

        #endregion
    }
}
