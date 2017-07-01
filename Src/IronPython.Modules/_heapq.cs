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
using System.Text;
using Microsoft.Scripting.Runtime;
using IronPython.Runtime;
using IronPython.Runtime.Binding;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

[assembly: PythonModule("_heapq", typeof(IronPython.Modules.PythonHeapq))]
namespace IronPython.Modules {
    public static class PythonHeapq {
        public const string __doc__ = "implements a heapq or priority queue.";
        public const string __about__ = "Heaps are arrays for which a[k] <= a[2*k+1] and a[k] <= a[2*k+2] for all k.";

        #region public API

        [Documentation("Transform list into a heap, in-place, in O(len(heap)) time.")]
        public static void heapify(CodeContext/*!*/ context, List list) {
            lock (list) {
                DoHeapify(context, list);
            }
        }

        [Documentation("Pop the smallest item off the heap, maintaining the heap invariant.")]
        public static object heappop(CodeContext/*!*/ context, List list) {
            lock (list) {
                int last = list._size - 1;
                if (last < 0) {
                    throw PythonOps.IndexError("index out of range");
                }
                list.FastSwap(0, last);
                list._size--;
                SiftDown(context, list, 0, last - 1);
                return list._data[list._size];
            }
        }

        [Documentation("Push item onto heap, maintaining the heap invariant.")]
        public static void heappush(CodeContext/*!*/ context, List list, object item) {
            lock (list) {
                list.AddNoLock(item);
                SiftUp(context, list, list._size - 1);
            }
        }

        [Documentation("Push item on the heap, then pop and return the smallest item\n"
            + "from the heap. The combined action runs more efficiently than\n"
            + "heappush() followed by a separate call to heappop()."
            )]
        public static object heappushpop(CodeContext/*!*/ context, List list, object item) {
            lock (list) {
                return DoPushPop(context, list, item);
            }
        }

        [Documentation("Pop and return the current smallest value, and add the new item.\n\n"
            + "This is more efficient than heappop() followed by heappush(), and can be\n"
            + "more appropriate when using a fixed-size heap. Note that the value\n"
            + "returned may be larger than item!  That constrains reasonable uses of\n"
            + "this routine unless written as part of a conditional replacement:\n\n"
            + "        if item > heap[0]:\n"
            + "            item = heapreplace(heap, item)\n"
            )]
        public static object heapreplace(CodeContext/*!*/ context, List list, object item) {
            lock (list) {
                object ret = list._data[0];
                list._data[0] = item;
                SiftDown(context, list, 0, list._size - 1);
                return ret;
            }
        }

        [Documentation("Find the n largest elements in a dataset.\n\n"
            + "Equivalent to:  sorted(iterable, reverse=True)[:n]\n"
            )]
        public static List nlargest(CodeContext/*!*/ context, int n, object iterable) {
            if (n <= 0) {
                return new List();
            }

            List ret = new List(Math.Min(n, 4000)); // don't allocate anything too huge
            IEnumerator en = PythonOps.GetEnumerator(iterable);

            // populate list with first n items
            for (int i = 0; i < n; i++) {
                if (!en.MoveNext()) {
                    // fewer than n items; finish up here
                    HeapSort(context, ret, true);
                    return ret;
                }
                ret.append(en.Current);
            }

            // go through the remainder of the iterator, maintaining a min-heap of the n largest values
            DoHeapify(context, ret);
            while (en.MoveNext()) {
                DoPushPop(context, ret, en.Current);
            }

            // return the largest items, in descending order
            HeapSort(context, ret, true);
            return ret;
        }

        [Documentation("Find the n smallest elements in a dataset.\n\n"
            + "Equivalent to:  sorted(iterable)[:n]\n"
            )]
        public static List nsmallest(CodeContext/*!*/ context, int n, object iterable) {
            if (n <= 0) {
                return new List();
            }

            List ret = new List(Math.Min(n, 4000)); // don't allocate anything too huge
            IEnumerator en = PythonOps.GetEnumerator(iterable);

            // populate list with first n items
            for (int i = 0; i < n; i++) {
                if (!en.MoveNext()) {
                    // fewer than n items; finish up here
                    HeapSort(context, ret);
                    return ret;
                }
                ret.append(en.Current);
            }

            // go through the remainder of the iterator, maintaining a max-heap of the n smallest values
            DoHeapifyMax(context, ret);
            while (en.MoveNext()) {
                DoPushPopMax(context, ret, en.Current);
            }

            // return the smallest items, in ascending order
            HeapSort(context, ret);
            return ret;
        }

        #endregion

        #region private implementation details (NOTE: thread-unsafe)

        private static bool IsLessThan(CodeContext/*!*/ context, object x, object y) {
            object ret;
            if (PythonTypeOps.TryInvokeBinaryOperator(context, x, y, "__lt__", out ret) &&
                !Object.ReferenceEquals(ret, NotImplementedType.Value)) {
                return Converter.ConvertToBoolean(ret);
            } else if (PythonTypeOps.TryInvokeBinaryOperator(context, y, x, "__le__", out ret) &&
                !Object.ReferenceEquals(ret, NotImplementedType.Value)) {
                return !Converter.ConvertToBoolean(ret);
            } else {
                return context.LanguageContext.LessThan(x, y);
            }
        }

        private static void HeapSort(CodeContext/*!*/ context, List list) {
            HeapSort(context, list, false);
        }

        private static void HeapSort(CodeContext/*!*/ context, List list, bool reverse) {
            // for an ascending sort (reverse = false), use a max-heap, and vice-versa
            if (reverse) {
                DoHeapify(context, list);
            } else {
                DoHeapifyMax(context, list);
            }

            int last = list._size - 1;
            while (last > 0) {
                // put the root node (max if ascending, min if descending) at the end
                list.FastSwap(0, last);
                // shrink heap by 1
                last--;
                // maintain heap invariant
                if (reverse) {
                    SiftDown(context, list, 0, last);
                } else {
                    SiftDownMax(context, list, 0, last);
                }
            }
        }

        private static void DoHeapify(CodeContext/*!*/ context, List list) {
            int last = list._size - 1;
            int start = (last - 1) / 2; // index of last parent node
            // Sift down each parent node from right to left.
            while (start >= 0) {
                SiftDown(context, list, start, last);
                start--;
            }
        }

        private static void DoHeapifyMax(CodeContext/*!*/ context, List list) {
            int last = list._size - 1;
            int start = (last - 1) / 2; // index of last parent node
            // Sift down each parent node from right to left.
            while (start >= 0) {
                SiftDownMax(context, list, start, last);
                start--;
            }
        }

        private static object DoPushPop(CodeContext/*!*/ context, List heap, object item) {
            object first;
            if (heap._size == 0 || !IsLessThan(context, first = heap._data[0], item)) {
                return item;
            }
            heap._data[0] = item;
            SiftDown(context, heap, 0, heap._size - 1);
            return first;
        }

        private static object DoPushPopMax(CodeContext/*!*/ context, List heap, object item) {
            object first;
            if (heap._size == 0 || !IsLessThan(context, item, first = heap._data[0])) {
                return item;
            }
            heap._data[0] = item;
            SiftDownMax(context, heap, 0, heap._size - 1);
            return first;
        }

        private static void SiftDown(CodeContext/*!*/ context, List heap, int start, int stop) {
            int parent = start;
            int child;
            while ((child = parent * 2 + 1) <= stop) {
                // find the smaller sibling
                if (child + 1 <= stop && IsLessThan(context, heap._data[child + 1], heap._data[child])) {
                    child++;
                }
                // check if min-heap property is violated
                if (IsLessThan(context, heap._data[child], heap._data[parent])) {
                    heap.FastSwap(parent, child);
                    parent = child;
                } else {
                    return;
                }
            }
        }

        private static void SiftDownMax(CodeContext/*!*/ context, List heap, int start, int stop) {
            int parent = start;
            int child;
            while ((child = parent * 2 + 1) <= stop) {
                // find the larger sibling
                if (child + 1 <= stop && IsLessThan(context, heap._data[child], heap._data[child + 1])) {
                    child++;
                }
                // check if max-heap property is violated
                if (IsLessThan(context, heap._data[parent], heap._data[child])) {
                    heap.FastSwap(parent, child);
                    parent = child;
                } else {
                    return;
                }
            }
        }

        private static void SiftUp(CodeContext/*!*/ context, List heap, int index) {
            while (index > 0) {
                int parent = (index - 1) / 2;
                if (IsLessThan(context, heap._data[index], heap._data[parent])) {
                    heap.FastSwap(parent, index);
                    index = parent;
                } else {
                    return;
                }
            }
        }

        private static void SiftUpMax(CodeContext/*!*/ context, List heap, int index) {
            while (index > 0) {
                int parent = (index - 1) / 2;
                if (IsLessThan(context, heap._data[parent], heap._data[index])) {
                    heap.FastSwap(parent, index);
                    index = parent;
                } else {
                    return;
                }
            }
        }

        #endregion
    }
}
