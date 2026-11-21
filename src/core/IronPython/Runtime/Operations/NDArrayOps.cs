// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime.Operations {
    /// <summary>
    ///   Provides the NumPy-like interface of CLR arrays.
    /// </summary>
    /// <remarks>
    ///   The functions are static methods of <c>System.Array</c> and follow the interface of the NumPy
    ///   functions of the same name (a subset of it). The operators of CLR arrays are in <see cref="ArrayOps"/>.
    ///   <para>
    ///     The result element type follows NumPy's type promotion rules applied to the CLR primitive
    ///     numeric types (<c>bool</c>, signed and unsigned integers, <c>Half</c> on .NET 7+, <c>Single</c>,
    ///     <c>Double</c>), <c>BigInteger</c> (an integer wider than any other) and <c>Complex</c>.
    ///     Like in NumPy, operands are cast to a numeric result type before the operation is applied. Python scalars
    ///     (<c>bool</c>, <c>int</c>, <c>float</c>, <c>complex</c>) are "weak", as in NumPy 2:
    ///     they don't change the element type of the other operand unless their kind is higher.
    ///     Operations of other types produce <c>object</c> elements, unless both operands have the same
    ///     element type, which is <c>str</c> or <c>Decimal</c>.
    ///   </para>
    /// </remarks>
    public static class NDArrayOps {
        #region Python APIs

        [StaticExtensionMethod]
        public static object? add(CodeContext context, object? x1, object? x2, [Optional] object? @out, [ParamDictionary] IDictionary<string, object?> kwargs)
            => BinaryUFunc(context, nameof(add), ExpressionType.Add, x1, x2, @out, kwargs);

        [StaticExtensionMethod]
        public static object? multiply(CodeContext context, object? x1, object? x2, [Optional] object? @out, [ParamDictionary] IDictionary<string, object?> kwargs)
            => BinaryUFunc(context, nameof(multiply), ExpressionType.Multiply, x1, x2, @out, kwargs);

        #endregion

        #region Internal APIs

        /// <summary>
        ///   Applies a binary Python operator element-wise, broadcasting the operands.
        /// </summary>
        /// <remarks>
        ///   When neither the operands nor <paramref name="out"/> are arrays, the result is a scalar.
        /// </remarks>
        internal static object? BinaryUFunc(CodeContext context, string name, ExpressionType operation, object? x1, object? x2, object? @out, IDictionary<string, object?> kwargs) {
            Type? dtype = null;
            foreach (var kwarg in kwargs) {
                switch (kwarg.Key) {
                    case "dtype":
                        if (kwarg.Value is not null) dtype = Converter.ConvertToType(kwarg.Value);
                        break;
                    default:
                        throw PythonOps.TypeError("{0}() got an unexpected keyword argument '{1}'", name, kwarg.Key);
                }
            }

            Array? outArray = GetOutArray(@out);
            CheckOperand(name, x1);
            CheckOperand(name, x2);

            Type resultType = dtype ?? ResultType(x1, x2);
            if (dtype is not null) {
                CheckCast(name, "input 0", GetElementType(x1), dtype);
                CheckCast(name, "input 1", GetElementType(x2), dtype);
            }
            if (outArray is not null) {
                CheckCast(name, "output", resultType, outArray.GetType().GetElementType()!);
            }

            var site = CallSite<Func<CallSite, object?, object?, object?>>.Create(context.LanguageContext.BinaryOperation(operation));

            Array? a1 = x1 as Array;
            Array? a2 = x2 as Array;
            if (a1 is null && a2 is null && outArray is null && dtype is null && IsWeakScalar(x1) && IsWeakScalar(x2)) {
                return site.Target(site, x1, x2);
            }

            // Like NumPy, operands are cast to a common numeric type before the operation is applied
            bool cast1 = MustCastInput(x1, resultType);
            bool cast2 = MustCastInput(x2, resultType);
            if (a1 is null && cast1) x1 = ConvertElement(x1, resultType);
            if (a2 is null && cast2) x2 = ConvertElement(x2, resultType);

            if (a1 is null && a2 is null && outArray is null) {
                return ConvertElement(site.Target(site, x1, x2), resultType);
            }

            int[] shape = BroadcastShapes(GetShape(x1), GetShape(x2))
                ?? throw PythonOps.ValueError("operands could not be broadcast together with shapes {0} {1} ", FormatShape(GetShape(x1)), FormatShape(GetShape(x2)));

            if (outArray is not null) {
                int[] outShape = GetShape(outArray);
                int[]? fullShape = BroadcastShapes(shape, outShape);
                if (fullShape is null || !ShapeEquals(fullShape, outShape)) {
                    throw PythonOps.ValueError("non-broadcastable output operand with shape {0} doesn't match the broadcast shape {1}", FormatShape(outShape), FormatShape(shape));
                }
                shape = outShape;
            }

            int[] lowerBounds = GetLowerBounds(shape.Length, a1, a2, outArray);
            Array result = outArray ?? CreateArray(resultType, shape, lowerBounds);
            Type elementType = result.GetType().GetElementType()!;

            int count = 1;
            foreach (int len in shape) count *= len;
            if (count == 0) return result;

            int[] index = (int[])lowerBounds.Clone();
            int[]? index1 = a1 is null ? null : new int[a1.Rank];
            int[]? index2 = a2 is null ? null : new int[a2.Rank];
            for (int i = 0; i < count; i++) {
                object? item1 = a1 is null ? x1 : GetBroadcastValue(a1, index, index1!);
                object? item2 = a2 is null ? x2 : GetBroadcastValue(a2, index, index2!);
                if (a1 is not null && cast1) item1 = ConvertElement(item1, resultType);
                if (a2 is not null && cast2) item2 = ConvertElement(item2, resultType);
                object? res = site.Target(site, item1, item2);
                if (dtype is not null && outArray is not null) res = ConvertElement(res, dtype);
                result.SetValue(ConvertElement(res, elementType), index);

                NextIndex(index, shape, lowerBounds);
            }
            return result;
        }

        /// <summary>
        ///   Returns the shape of an array, or an empty shape for a scalar.
        /// </summary>
        internal static int[] GetShape(object? x) {
            if (x is not Array a) return [];

            int[] shape = new int[a.Rank];
            for (int d = 0; d < shape.Length; d++) {
                shape[d] = a.GetLength(d);
            }
            return shape;
        }

        /// <summary>
        ///   Computes the shape that the given shapes broadcast to, or <c>null</c> if they are not broadcastable.
        /// </summary>
        /// <remarks>
        ///   Shapes are aligned at the trailing dimension; a dimension of length 1, or a missing one,
        ///   stretches to the length of the corresponding dimension of the other shape.
        /// </remarks>
        internal static int[]? BroadcastShapes(int[] shape1, int[] shape2) {
            int rank = Math.Max(shape1.Length, shape2.Length);
            int[] res = new int[rank];
            for (int d = 0; d < rank; d++) {
                int len1 = d < rank - shape1.Length ? 1 : shape1[d - rank + shape1.Length];
                int len2 = d < rank - shape2.Length ? 1 : shape2[d - rank + shape2.Length];
                if (len1 == len2 || len2 == 1) {
                    res[d] = len1;
                } else if (len1 == 1) {
                    res[d] = len2;
                } else {
                    return null;
                }
            }
            return res;
        }

        /// <summary>
        ///   Creates an array of the given element type, shape and lower bounds.
        /// </summary>
        internal static Array CreateArray(Type elementType, int[] shape, int[] lowerBounds) {
            foreach (int lb in lowerBounds) {
                if (lb != 0) return Array.CreateInstance(elementType, shape, lowerBounds);
            }
            return Array.CreateInstance(elementType, shape);
        }

        /// <summary>
        ///   Determines the element type of the result of an operation on the given operands.
        /// </summary>
        /// <remarks>
        ///   If all operands are weak scalars, the result is the default type of the highest kind among them.
        /// </remarks>
        internal static Type ResultType(params object?[] operands) {
            Type? res = null;
            int weakCategory = 0;
            foreach (object? x in operands) {
                if (IsWeakScalar(x)) {
                    weakCategory = Math.Max(weakCategory, KindCategory(x!.GetType()));
                } else {
                    Type type = GetElementType(x);
                    res = res is null ? type : PromoteTypes(res, type);
                }
            }
            if (res is null) return DefaultType(weakCategory);

            foreach (object? x in operands) {
                if (IsWeakScalar(x)) res = PromoteWeakScalar(res, x!);
            }
            return res;
        }

        /// <summary>
        ///   Returns the element type of an array, or the type of a scalar.
        /// </summary>
        internal static Type GetElementType(object? x)
            => x is Array a ? a.GetType().GetElementType()! : x?.GetType() ?? typeof(object);

        /// <summary>
        ///   Determines the common element type of two element types.
        /// </summary>
        internal static Type PromoteTypes(Type type1, Type type2) {
            if (type1 == type2) {
                return TryGetNumericInfo(type1, out _, out _) || type1 == typeof(string) || type1 == typeof(decimal) ? type1 : typeof(object);
            }
            if (!TryGetNumericInfo(type1, out NumericKind kind1, out int size1) || !TryGetNumericInfo(type2, out NumericKind kind2, out int size2)) {
                return typeof(object);
            }

            if (kind1 == NumericKind.Bool) return type2;
            if (kind2 == NumericKind.Bool) return type1;
            if (kind1 == NumericKind.Complex || kind2 == NumericKind.Complex) return typeof(Complex);

            if (kind1 == NumericKind.Float || kind2 == NumericKind.Float) {
                if (kind1 == kind2) return size1 >= size2 ? type1 : type2;

                // a float combined with an integer, which needs a float twice its size to hold all its values
                int floatSize = kind1 == NumericKind.Float ? size1 : size2;
                int intSize = kind1 == NumericKind.Float ? size2 : size1;
                return FloatType(Math.Max(floatSize, Math.Min(2 * intSize, 8)));
            }

            // both are integers
            if (kind1 == kind2) return size1 >= size2 ? type1 : type2;

            // a signed integer combined with an unsigned one
            int signedSize = kind1 == NumericKind.Int ? size1 : size2;
            int unsignedSize = kind1 == NumericKind.Int ? size2 : size1;
            if (signedSize > unsignedSize) return kind1 == NumericKind.Int ? type1 : type2;
            return unsignedSize switch {
                1 => typeof(short),
                2 => typeof(int),
                4 => typeof(long),
                _ => typeof(double), // no signed integer can hold all values of UInt64
            };
        }

        #endregion

        #region Private helpers

        private enum NumericKind {
            Bool,
            UInt,
            Int,
            Float,
            Complex,
        }

        /// <summary>
        ///   Classifies a numeric element type by kind and size in bytes.
        /// </summary>
        /// <remarks>
        ///   <c>BigInteger</c> is treated as a signed integer wider than any other.
        ///   <c>Half</c> (NumPy's <c>float16</c>) is supported on .NET 7 and later, which provide its arithmetic operators.
        /// </remarks>
        private static bool TryGetNumericInfo(Type type, out NumericKind kind, out int size) {
            switch (Type.GetTypeCode(type)) {
                case TypeCode.Boolean: kind = NumericKind.Bool; size = 1; return true;
                case TypeCode.SByte: kind = NumericKind.Int; size = 1; return true;
                case TypeCode.Byte: kind = NumericKind.UInt; size = 1; return true;
                case TypeCode.Int16: kind = NumericKind.Int; size = 2; return true;
                case TypeCode.UInt16: kind = NumericKind.UInt; size = 2; return true;
                case TypeCode.Int32: kind = NumericKind.Int; size = 4; return true;
                case TypeCode.UInt32: kind = NumericKind.UInt; size = 4; return true;
                case TypeCode.Int64: kind = NumericKind.Int; size = 8; return true;
                case TypeCode.UInt64: kind = NumericKind.UInt; size = 8; return true;
                case TypeCode.Single: kind = NumericKind.Float; size = 4; return true;
                case TypeCode.Double: kind = NumericKind.Float; size = 8; return true;
            }
            if (type == typeof(BigInteger)) {
                kind = NumericKind.Int; size = 16; return true;
            }
            if (type == typeof(Complex)) {
                kind = NumericKind.Complex; size = 16; return true;
            }
#if NET7_0_OR_GREATER
            if (type == typeof(Half)) {
                kind = NumericKind.Float; size = 2; return true;
            }
#endif
            kind = default; size = 0;
            return false;
        }

        /// <summary>
        ///   Returns the floating-point type of the given size in bytes.
        /// </summary>
        private static Type FloatType(int size) => size switch {
#if NET7_0_OR_GREATER
            2 => typeof(Half),
#endif
            4 => typeof(float),
            _ => typeof(double),
        };

        /// <summary>
        ///   Checks whether an operand has to be cast to the numeric type in which the operation is computed.
        /// </summary>
        private static bool MustCastInput(object? x, Type type)
            => GetElementType(x) != type && TryGetNumericInfo(type, out _, out _);

        /// <summary>
        ///   Checks whether the operand is a Python <c>bool</c>, <c>int</c>, <c>float</c> or <c>complex</c>.
        /// </summary>
        private static bool IsWeakScalar(object? x)
            => x is bool || x is int || x is BigInteger || x is double || x is Complex;

        /// <summary>
        ///   Determines the element type of an operation of a weak scalar with an operand of the given element type.
        /// </summary>
        private static Type PromoteWeakScalar(Type type, object scalar) {
            if (!TryGetNumericInfo(type, out _, out _)) return typeof(object);

            int scalarCategory = KindCategory(scalar.GetType());
            return scalarCategory <= KindCategory(type) ? type : DefaultType(scalarCategory);
        }

        /// <summary>
        ///   Orders numeric types by kind: <c>bool</c> &lt; integers &lt; floats &lt; complex.
        /// </summary>
        /// <returns>The category of the kind, or -1 if the type is not numeric.</returns>
        private static int KindCategory(Type type) {
            if (!TryGetNumericInfo(type, out NumericKind kind, out _)) return -1;

            return kind switch {
                NumericKind.Bool => 0,
                NumericKind.UInt => 1,
                NumericKind.Int => 1,
                NumericKind.Float => 2,
                _ => 3,
            };
        }

        /// <summary>
        ///   Returns the element type for a Python scalar of the given kind category.
        /// </summary>
        /// <remarks>
        ///   For integers, it is <c>BigInteger</c>, the CLR type of Python <c>int</c> (as in <c>Array[int]</c>).
        /// </remarks>
        private static Type DefaultType(int category) => category switch {
            0 => typeof(bool),
            1 => typeof(BigInteger),
            2 => typeof(double),
            _ => typeof(Complex),
        };

        /// <summary>
        ///   Checks that a value of one type can be cast to another with NumPy's <c>same_kind</c> casting rule.
        /// </summary>
        /// <remarks>
        ///   Numeric types can be cast to any numeric type of the same or a higher kind, and any type
        ///   can be cast to itself or to <c>object</c>.
        /// </remarks>
        private static void CheckCast(string name, string operand, Type from, Type to) {
            if (from == to || to == typeof(object)) return;

            int fromCategory = KindCategory(from);
            int toCategory = KindCategory(to);
            if (fromCategory < 0 || toCategory < fromCategory) {
                throw PythonOps.TypeError("Cannot cast ufunc '{0}' {1} from {2} to {3} with casting rule 'same_kind'",
                    name, operand, PythonOps.GetPythonTypeNameFromType(from), PythonOps.GetPythonTypeNameFromType(to));
            }
        }

        private static void CheckOperand(string name, object? x) {
            if (x is PythonList || x is PythonTuple) {
                throw PythonOps.TypeError("{0}() does not support operands of type '{1}'; convert them to System.Array", name, PythonOps.GetPythonTypeName(x));
            }
        }

        private static Array? GetOutArray(object? @out) {
            if (@out is PythonTuple tuple) {
                if (tuple.Count != 1) throw PythonOps.ValueError("The 'out' tuple must have exactly one entry per ufunc output");
                @out = tuple[0];
            }
            return @out switch {
                null or Missing => null,
                Array a => a,
                _ => throw PythonOps.TypeError("return arrays must be of ArrayType"),
            };
        }

        /// <summary>
        ///   Determines the lower bounds of the result of an operation on the given arrays.
        /// </summary>
        /// <remarks>
        ///   Dimensions of the arrays are aligned at the trailing dimension, and aligned dimensions must
        ///   have the same lower bound.
        /// </remarks>
        private static int[] GetLowerBounds(int rank, params Array?[] arrays) {
            int[] lowerBounds = new int[rank];
            bool[] isSet = new bool[rank];
            foreach (Array? a in arrays) {
                if (a is null) continue;
                int offset = rank - a.Rank;
                for (int d = 0; d < a.Rank; d++) {
                    int lb = a.GetLowerBound(d);
                    if (!isSet[d + offset]) {
                        lowerBounds[d + offset] = lb;
                        isSet[d + offset] = true;
                    } else if (lowerBounds[d + offset] != lb) {
                        throw PythonOps.ValueError("operands have different lower bounds in aligned dimensions");
                    }
                }
            }
            return lowerBounds;
        }

        /// <summary>
        ///   Gets the element of <paramref name="a"/> that broadcasts to the given index of the result.
        /// </summary>
        /// <param name="a">The array, of a rank not higher than the length of <paramref name="index"/>.</param>
        /// <param name="index">The index into the result.</param>
        /// <param name="buffer">An index buffer of the rank of <paramref name="a"/>.</param>
        private static object? GetBroadcastValue(Array a, int[] index, int[] buffer) {
            int offset = index.Length - buffer.Length;
            for (int d = 0; d < buffer.Length; d++) {
                buffer[d] = a.GetLength(d) == 1 ? a.GetLowerBound(d) : index[d + offset];
            }
            return a.GetValue(buffer);
        }

        /// <summary>
        ///   Advances the index to the next element in row-major order.
        /// </summary>
        private static void NextIndex(int[] index, int[] shape, int[] lowerBounds) {
            for (int d = index.Length - 1; d >= 0; d--) {
                if (++index[d] < lowerBounds[d] + shape[d]) return;
                index[d] = lowerBounds[d];
            }
        }

        private static object? ConvertElement(object? value, Type type) {
            if (type == typeof(object)) return value;
            if (type == typeof(bool) && value is int i) return ScriptingRuntimeHelpers.BooleanToObject(i != 0); // NumPy: add is or, multiply is and
            if (value is bool b && type != typeof(bool)) value = b ? 1 : 0; // not every numeric type converts from bool, e.g. Half
            return Converter.Convert(value!, type);
        }

        private static bool ShapeEquals(int[] shape1, int[] shape2) {
            if (shape1.Length != shape2.Length) return false;
            for (int d = 0; d < shape1.Length; d++) {
                if (shape1[d] != shape2[d]) return false;
            }
            return true;
        }

        /// <summary>
        ///   Formats a shape like NumPy does in error messages, e.g. <c>(2,3)</c> or <c>(2,)</c>.
        /// </summary>
        private static string FormatShape(int[] shape) {
            var sb = new StringBuilder("(");
            for (int d = 0; d < shape.Length; d++) {
                if (d > 0) sb.Append(',');
                sb.Append(shape[d]);
            }
            if (shape.Length == 1) sb.Append(',');
            return sb.Append(')').ToString();
        }

        #endregion
    }
}
