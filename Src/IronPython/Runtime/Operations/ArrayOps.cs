// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using IronPython.Runtime.Types;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using SpecialNameAttribute = System.Runtime.CompilerServices.SpecialNameAttribute;

namespace IronPython.Runtime.Operations {
    public static class ArrayOps {
        #region Python APIs

        [SpecialName]
        public static Array Add(Array data1, Array data2) {
            if (data1 == null) throw PythonOps.TypeError("expected array for 1st argument, got None");
            if (data2 == null) throw PythonOps.TypeError("expected array for 2nd argument, got None");

            if (data1.Rank > 1 || data2.Rank > 1) throw new NotImplementedException("can't add multidimensional arrays");

            Type type1 = data1.GetType();
            Type type2 = data2.GetType();
            Type type = (type1 == type2) ? type1.GetElementType()! : typeof(object);

            Array ret = Array.CreateInstance(type, data1.Length + data2.Length);
            Array.Copy(data1, 0, ret, 0, data1.Length);
            Array.Copy(data2, 0, ret, data1.Length, data2.Length);
            return ret;
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext context, PythonType pythonType, int length) {
            Type type = pythonType.UnderlyingSystemType.GetElementType()!;

            return Array.CreateInstance(type, length);
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext context, PythonType pythonType, params int[] lengths) {
            Type type = pythonType.UnderlyingSystemType.GetElementType()!;

            return Array.CreateInstance(type, lengths);
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext context, PythonType pythonType, ICollection items) {
            Type type = pythonType.UnderlyingSystemType.GetElementType()!;

            Array res = Array.CreateInstance(type, items.Count);

            int i = 0;
            foreach (var item in items) {
                res.SetValue(Converter.Convert(item, type), i++);
            }

            return res;
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext context, PythonType pythonType, object items) {
            Type type = pythonType.UnderlyingSystemType.GetElementType()!;

            object? lenFunc;
            if (!PythonOps.TryGetBoundAttr(items, "__len__", out lenFunc))
                throw PythonOps.TypeErrorForBadInstance("expected object with __len__ function, got {0}", items);

            int len = context.LanguageContext.ConvertToInt32(PythonOps.CallWithContext(context, lenFunc));

            Array res = Array.CreateInstance(type, len);

            IEnumerator ie = PythonOps.GetEnumerator(items);
            int i = 0;
            while (ie.MoveNext()) {
                res.SetValue(Converter.Convert(ie.Current, type), i++);
            }

            return res;
        }

        [StaticExtensionMethod]
        public static object __eq__(CodeContext context, Array self, [NotNone] Array other) {
            if (self is null) throw PythonOps.TypeError("expected Array, got None");
            if (other is null) throw PythonOps.TypeError("expected Array, got None");

            if (self.GetType() != other.GetType()) return ScriptingRuntimeHelpers.False;
            // same type implies: same rank, same element type
            for (int d = 0; d < self.Rank; d++) {
                if (self.GetLowerBound(d) != other.GetLowerBound(d)) return ScriptingRuntimeHelpers.False;
                if (self.GetUpperBound(d) != other.GetUpperBound(d)) return ScriptingRuntimeHelpers.False;
            }
            if (self.Length == 0) return ScriptingRuntimeHelpers.True; // fast track

            if (self.Rank == 1 && self.GetLowerBound(0) == 0 ) {
                // IStructuralEquatable.Equals only works for 1-dim, 0-based arrays
                return ScriptingRuntimeHelpers.BooleanToObject(
                    ((IStructuralEquatable)self).Equals(other, context.LanguageContext.EqualityComparerNonGeneric)
                );
            } else {
                int[] ix = new int[self.Rank];
                for (int d = 0; d < self.Rank; d++) {
                    ix[d] = self.GetLowerBound(d);
                }
                for (int i = 0; i < self.Length; i++) {
                    if (!PythonOps.EqualRetBool(self.GetValue(ix), other.GetValue(ix))) {
                        return ScriptingRuntimeHelpers.False;
                    }
                    for (int d = self.Rank - 1; d >= 0; d--) {
                        if (ix[d] < self.GetUpperBound(d)) {
                            ix[d]++;
                            break;
                        } else {
                            ix[d] = self.GetLowerBound(d);
                        }
                    }
                }
                return ScriptingRuntimeHelpers.True;
            }
        }

        [StaticExtensionMethod]
        [return: MaybeNotImplemented]
        public static object __eq__(CodeContext context, object self, object? other) => NotImplementedType.Value;

        [StaticExtensionMethod]
        public static object __ne__(CodeContext context, Array self, [NotNone] Array other)
            => ScriptingRuntimeHelpers.BooleanToObject(ReferenceEquals(__eq__(context, self, other), ScriptingRuntimeHelpers.False));

        [StaticExtensionMethod]
        [return: MaybeNotImplemented]
        public static object __ne__(CodeContext context, object self, object? other) => NotImplementedType.Value;

        /// <summary>
        /// Multiply two object[] arrays - slow version, we need to get the type, etc...
        /// </summary>
        [SpecialName]
        public static Array Multiply(Array data, int count) {
            if (data.Rank > 1) throw new NotImplementedException("can't multiply multidimensional arrays");

            Type elemType = data.GetType().GetElementType()!;
            if (count <= 0) return Array.CreateInstance(elemType, 0);

            int newCount = data.Length * count;

            Array ret = Array.CreateInstance(elemType, newCount);
            Array.Copy(data, 0, ret, 0, data.Length);

            // this should be extremely fast for large count as it uses the same algoithim as efficient integer powers
            // ??? need to test to see how large count and n need to be for this to be fastest approach
            int block = data.Length;
            int pos = data.Length;
            while (pos < newCount) {
                Array.Copy(ret, 0, ret, pos, Math.Min(block, newCount - pos));
                pos += block;
                block *= 2;
            }
            return ret;
        }

        [SpecialName]
        public static object? GetItem(Array data, int index) {
            if (data == null) throw PythonOps.TypeError("expected Array, got None");
            if (data.Rank != 1) throw PythonOps.TypeError("bad dimensions for array, got {0} expected {1}", 1, data.Rank);

            return data.GetValue(FixIndex(data, index, 0));
        }

        [SpecialName]
        public static object GetItem(Array data, Slice slice) {
            if (data == null) throw PythonOps.TypeError("expected Array, got None");

            return GetSlice(data, slice);
        }

        [SpecialName]
        public static object? GetItem(Array data, PythonTuple tuple) {
            if (data == null) throw PythonOps.TypeError("expected Array, got None");
            return GetItem(data, tuple._data);
        }

        [SpecialName]
        public static object? GetItem(Array data, params object?[] indices) {
            if (indices == null || indices.Length < 1) throw PythonOps.TypeError("__getitem__ requires at least 1 parameter");

            if (indices.Length == 1 && Converter.TryConvertToInt32(indices[0], out int iindex)) {
                return GetItem(data, iindex);
            }

            Type t = data.GetType();
            Debug.Assert(t.HasElementType);

            int[] iindices = TupleToIndices(data, indices);
            if (data.Rank != indices.Length) throw PythonOps.TypeError("bad dimensions for array, got {0} expected {1}", indices.Length, data.Rank);

            return data.GetValue(iindices);
        }

        [SpecialName]
        public static void SetItem(Array data, int index, object value) {
            if (data == null) throw PythonOps.TypeError("expected Array, got None");
            if (data.Rank != 1) throw PythonOps.TypeError("bad dimensions for array, got {0} expected {1}", 1, data.Rank);

            data.SetValue(Converter.Convert(value, data.GetType().GetElementType()), FixIndex(data, index, 0));
        }

        [SpecialName]
        public static void SetItem(Array a, params object[] indexAndValue) {
            if (indexAndValue == null || indexAndValue.Length < 2) throw PythonOps.TypeError("__setitem__ requires at least 2 parameters");

            if (indexAndValue.Length == 2 && Converter.TryConvertToInt32(indexAndValue[0], out int iindex)) {
                SetItem(a, iindex, indexAndValue[1]);
                return;
            }

            Type t = a.GetType();
            Debug.Assert(t.HasElementType);

            object[] args = ArrayUtils.RemoveLast(indexAndValue);

            int[] indices = TupleToIndices(a, args);

            if (a.Rank != args.Length) throw PythonOps.TypeError("bad dimensions for array, got {0} expected {1}", args.Length, a.Rank);

            Type elm = t.GetElementType()!;
            a.SetValue(Converter.Convert(indexAndValue[indexAndValue.Length - 1], elm), indices);
        }

        [SpecialName]
        public static void SetItem(Array a, Slice index, object? value) {
            if (a.Rank != 1) throw PythonOps.NotImplementedError("slice on multi-dimensional array");

            Type elm = a.GetType().GetElementType()!;

            int lb = a.GetLowerBound(0);
            if (lb != 0) {
                FixSlice(index, a, out int start, out int stop, out int step);
                index = new Slice(start - lb, stop - lb, step);
            }

            index.DoSliceAssign(
                delegate (int idx, object? val) {
                    a.SetValue(Converter.Convert(val, elm), idx + lb);
                },
                a.Length,
                value);
        }

        public static string __repr__(CodeContext/*!*/ context, [NotNone] Array/*!*/ self) {
            var infinite = PythonOps.GetAndCheckInfinite(self);
            if (infinite == null) {
                return "...";
            }

            int index = infinite.Count;
            infinite.Add(self);
            try {
                StringBuilder ret = new StringBuilder();
                if (self.Rank == 1) {
                    // single dimensional Array's have a valid display
                    ret.Append("Array[");
                    Type elemType = self.GetType().GetElementType()!;
                    ret.Append(DynamicHelpers.GetPythonTypeFromType(elemType).Name);
                    ret.Append(']');
                    ret.Append("((");
                    for (int i = 0; i < self.Length; i++) {
                        if (i > 0) ret.Append(", ");
                        ret.Append(PythonOps.Repr(context, self.GetValue(i + self.GetLowerBound(0))));
                    }
                    ret.Append(')');
                    if (self.GetLowerBound(0) != 0) {
                        ret.Append(", base: ");
                        ret.Append(self.GetLowerBound(0));
                    }
                    ret.Append(')');
                } else {
                    // multi dimensional arrays require multiple statements to construct so we just
                    // give enough info to identify the object and its type.
                    ret.Append('<');
                    ret.Append(self.Rank);
                    ret.Append(" dimensional Array[");
                    Type elemType = self.GetType().GetElementType()!;
                    ret.Append(DynamicHelpers.GetPythonTypeFromType(elemType).Name);
                    ret.Append("] at ");
                    ret.Append(PythonOps.HexId(self));
                    ret.Append('>');
                }
                return ret.ToString();
            } finally {
                System.Diagnostics.Debug.Assert(index == infinite.Count - 1);
                infinite.RemoveAt(index);
            }
        }

        #endregion

        #region Internal APIs


        /// <summary>
        /// Multiply two object[] arrays - internal version used for objects backed by arrays
        /// </summary>
        internal static object?[] Multiply(object?[] data, int size, int count) {
            int newCount;
            try {
                newCount = checked(size * count);
            } catch (OverflowException) {
                throw PythonOps.MemoryError();
            }

            var ret = ArrayOps.CopyArray(data, newCount);
            if (count > 0) {
                // this should be extremely fast for large count as it uses the same algoithim as efficient integer powers
                // ??? need to test to see how large count and n need to be for this to be fastest approach
                int block = size;
                int pos = size;
                while (pos < newCount) {
                    Array.Copy(ret, 0, ret, pos, Math.Min(block, newCount - pos));
                    pos += block;
                    block *= 2;
                }
            }
            return ret;
        }

        /// <summary>
        /// Add two arrays - internal versions for objects backed by arrays
        /// </summary>
        /// <param name="data1"></param>
        /// <param name="size1"></param>
        /// <param name="data2"></param>
        /// <param name="size2"></param>
        /// <returns></returns>
        internal static object?[] Add(object?[] data1, int size1, object?[] data2, int size2) {
            var ret = ArrayOps.CopyArray(data1, size1 + size2);
            Array.Copy(data2, 0, ret, size1, size2);
            return ret;
        }

        internal static object?[] GetSlice(object?[] data, int start, int stop) {
            if (stop <= start) return [];

            var ret = new object?[stop - start];
            int index = 0;
            for (int i = start; i < stop; i++) {
                ret[index++] = data[i];
            }
            return ret;
        }

        internal static object?[] GetSlice(object?[] data, int start, int stop, int step) {
            Debug.Assert(step != 0);

            if (step == 1) {
                return GetSlice(data, start, stop);
            }

            int size = PythonOps.GetSliceCount(start, stop, step);
            if (size <= 0) return [];

            var res = new object?[size];
            for (int i = 0, index = start; i < res.Length; i++, index += step) {
                res[i] = data[index];
            }
            return res;
        }

        internal static object?[] GetSlice(object?[] data, Slice slice) {
            slice.Indices(data.Length, out int start, out int stop, out int step);
            return GetSlice(data, start, stop, step);
        }

        private static Array GetSlice(Array data, Slice slice) {
            if (data.Rank != 1) throw PythonOps.NotImplementedError("slice on multi-dimensional array");

            FixSlice(slice, data, out int start, out int stop, out int step);

            if ((step > 0 && start >= stop) || (step < 0 && start <= stop)) {
                if (data.GetType().GetElementType() == typeof(object))
                    return Array.Empty<object>();

                return Array.CreateInstance(data.GetType().GetElementType()!, 0);
            }

            if (step == 1) {
                int n = stop - start;
                Array ret = Array.CreateInstance(data.GetType().GetElementType()!, n);
                Array.Copy(data, start, ret, 0, n);
                return ret;
            } else {
                int n = PythonOps.GetSliceCount(start, stop, step);
                Array ret = Array.CreateInstance(data.GetType().GetElementType()!, n);
                int ri = 0;
                for (int i = 0, index = start; i < n; i++, index += step) {
                    ret.SetValue(data.GetValue(index), ri++);
                }
                return ret;
            }
        }

        internal static object?[] CopyArray(object?[] data, int newSize) {
            if (newSize == 0) return [];

            var newData = new object?[newSize];
            if (data.Length < 20) {
                for (int i = 0; i < data.Length && i < newSize; i++) {
                    newData[i] = data[i];
                }
            } else {
                Array.Copy(data, newData, Math.Min(newSize, data.Length));
            }

            return newData;
        }

        #endregion

        #region Private helpers


        private static int[] TupleToIndices(Array a, IList<object?> tuple) {
            int[] indices = new int[tuple.Count];
            for (int i = 0; i < indices.Length; i++) {
                int iindex = Converter.ConvertToInt32(tuple[i]);
                indices[i] = i < a.Rank ? FixIndex(a, iindex, i) : int.MinValue;
            }
            return indices;
        }

        private static int FixIndex(Array a, int v, int axis) {
            int idx = v;
            if (idx < 0) idx += a.GetUpperBound(axis) + 1;
            if (idx < a.GetLowerBound(axis) || idx > a.GetUpperBound(axis)) {
                throw PythonOps.IndexError("index out of range: {0}", v);
            }
            return idx;
        }

        private static void FixSlice(Slice slice, Array a, out int ostart, out int ostop, out int ostep) {
            Debug.Assert(a.Rank == 1);

            if (slice.step == null) {
                ostep = 1;
            } else {
                ostep = Converter.ConvertToIndex(slice.step);
                if (ostep == 0) {
                    throw PythonOps.ValueError("step cannot be zero");
                }
            }

            int lb = a.GetLowerBound(0);
            int ub = a.GetUpperBound(0);

            if (slice.start == null) {
                ostart = ostep > 0 ? lb : ub;
            } else {
                ostart = Converter.ConvertToIndex(slice.start);
                if (ostart < lb) {
                    if (ostart < 0) {
                        ostart += ub + 1;
                    }
                    if (ostart < lb) {
                        ostart = ostep > 0 ? lb : lb - 1;
                    }
                } else if (ostart > ub) {
                    ostart = ostep > 0 ? ub + 1 : ub;
                }
            }

            if (slice.stop == null) {
                ostop = ostep > 0 ? ub + 1 : lb - 1;
            } else {
                ostop = Converter.ConvertToIndex(slice.stop);
                if (ostop < lb) {
                    if (ostop < 0) {
                        ostop += ub + 1;
                    }
                    if (ostop < 0) {
                        ostop = ostep > 0 ? lb : lb - 1;
                    }
                } else if (ostop > ub) {
                    ostop = ostep > 0 ? ub + 1 : ub;
                }
            }
        }

        #endregion
    }
}
