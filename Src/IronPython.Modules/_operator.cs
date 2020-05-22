// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

using Microsoft.Scripting;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Runtime;

using IronPython.Runtime;
using IronPython.Runtime.Binding;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

[assembly: PythonModule("_operator", typeof(IronPython.Modules.PythonOperator))]
namespace IronPython.Modules {
    public static class PythonOperator {
        public const string __doc__ = "Provides programmatic access to various operators (addition, accessing members, etc...)";

        public class attrgetter {
            private readonly object[] _names;
            public attrgetter(params object[] attrs) {
                if (attrs == null || !attrs.All(x => x is string)) throw PythonOps.TypeError("attribute name must be a string");
                if (attrs.Length == 0) throw PythonOps.TypeError("attrgetter expected 1 arguments, got 0");

                this._names = attrs;
            }

            [SpecialName]
            public object Call(CodeContext context, object param) {
                if (_names.Length == 1) {
                    return GetOneAttr(context, param, _names[0]);
                }

                object[] res = new object[_names.Length];
                for (int i = 0; i < _names.Length; i++) {
                    res[i] = GetOneAttr(context, param, _names[i]);
                }
                return PythonTuple.MakeTuple(res);
            }

            private static object GetOneAttr(CodeContext context, object param, object val) {
                string s = val as string;
                if (s == null) {
                    throw PythonOps.TypeError("attribute name must be string");
                }
                int dotPos = s.IndexOf('.');
                if (dotPos >= 0) {
                    object nextParam = GetOneAttr(context, param, s.Substring(0, dotPos));
                    return GetOneAttr(context, nextParam, s.Substring(dotPos + 1, s.Length - dotPos - 1));
                }
                return PythonOps.GetBoundAttr(context, param, s);
            }
        }

        public class itemgetter {
            private readonly object[] _items;

            public itemgetter([NotNull]params object[] items) {
                if (items.Length == 0) {
                    throw PythonOps.TypeError("itemgetter needs at least one argument");
                }
                _items = items;
            }

            [SpecialName]
            public object Call(CodeContext/*!*/ context, object param) {
                if (_items.Length == 1) {
                    return PythonOps.GetIndex(context, param, _items[0]);
                }

                object[] res = new object[_items.Length];
                for (int i = 0; i < _items.Length; i++) {
                    res[i] = PythonOps.GetIndex(context, param, _items[i]);
                }

                return PythonTuple.MakeTuple(res);
            }
        }

        [PythonType]
        public class methodcaller {
            private readonly string _name;
            private readonly object[] _args;
            private readonly IDictionary<object, object> _dict;

            public methodcaller(params object[] args) {
                if (args == null) throw PythonOps.TypeError("TypeError: method name must be a string");
                if (args.Length == 0) throw PythonOps.TypeError("methodcaller needs at least one argument, the method name");
                _name = args[0] as string;
                if (_name == null) throw PythonOps.TypeError("TypeError: method name must be a string");
                _args = args.Skip(1).ToArray();
            }

            public methodcaller([ParamDictionary]IDictionary<object, object> kwargs, params object[] args) : this(args) {
                _dict = kwargs;
            }

            public override string ToString() {
                return String.Format("<operator.methodcaller: {0}>", _name);
            }

            [SpecialName]
            public object Call(CodeContext/*!*/ context, object param) {
                object method = PythonOps.GetBoundAttr(context, param, _name);
                if (_dict == null) {
                    return PythonOps.CallWithContext(context, method, _args);
                } else {
                    return PythonCalls.CallWithKeywordArgs(context, method, _args, _dict);
                }
            }
        }
        
        public static object lt(CodeContext/*!*/ context, object a, object b) {
            return context.LanguageContext.Operation(PythonOperationKind.LessThan, a, b);
        }

        public static object le(CodeContext/*!*/ context, object a, object b) {
            return context.LanguageContext.Operation(PythonOperationKind.LessThanOrEqual, a, b);
        }

        public static object eq(CodeContext/*!*/ context, object a, object b) {
            return context.LanguageContext.Operation(PythonOperationKind.Equal, a, b);
        }

        public static object ne(CodeContext/*!*/ context, object a, object b) {
            return context.LanguageContext.Operation(PythonOperationKind.NotEqual, a, b);
        }

        public static object ge(CodeContext/*!*/ context, object a, object b) {
            return context.LanguageContext.Operation(PythonOperationKind.GreaterThanOrEqual, a, b);
        }

        public static object gt(CodeContext/*!*/ context, object a, object b) {
            return context.LanguageContext.Operation(PythonOperationKind.GreaterThan, a, b);
        }

        public static bool not_(object o) {
            return PythonOps.Not(o);
        }

        public static bool truth(object o) {
            return PythonOps.IsTrue(o);
        }

        public static object is_(object a, object b) {
            return PythonOps.Is(a, b);
        }

        public static object is_not(object a, object b) {
            return PythonOps.IsNot(a, b);
        }

        public static object abs(CodeContext context, object o) {
            return Builtin.abs(context, o);
        }

        public static object add(CodeContext/*!*/ context, object a, object b) {
            return context.LanguageContext.Operation(PythonOperationKind.Add, a, b);
        }

        public static object and_(CodeContext/*!*/ context, object a, object b) {
            return context.LanguageContext.Operation(PythonOperationKind.BitwiseAnd, a, b);
        }

        public static object floordiv(CodeContext/*!*/ context, object a, object b) {
            return context.LanguageContext.Operation(PythonOperationKind.FloorDivide, a, b);
        }

        public static object inv(CodeContext/*!*/ context, object o) {
            return PythonOps.OnesComplement(o);
        }

        public static object invert(CodeContext/*!*/ context, object o) {
            return PythonOps.OnesComplement(o);
        }

        public static object lshift(CodeContext/*!*/ context, object a, object b) {
            return context.LanguageContext.Operation(PythonOperationKind.LeftShift, a, b);
        }

        public static object mod(CodeContext/*!*/ context, object a, object b) {
            return context.LanguageContext.Operation(PythonOperationKind.Mod, a, b);
        }

        public static object mul(CodeContext/*!*/ context, object a, object b) {
            return context.LanguageContext.Operation(PythonOperationKind.Multiply, a, b);
        }

        public static object neg(object o) {
            return PythonOps.Negate(o);
        }

        public static object or_(CodeContext/*!*/ context, object a, object b) {
            return context.LanguageContext.Operation(PythonOperationKind.BitwiseOr, a, b);
        }

        public static object pos(object o) {
            return PythonOps.Plus(o);
        }

        public static object pow(CodeContext/*!*/ context, object a, object b) {
            return context.LanguageContext.Operation(PythonOperationKind.Power, a, b);
        }

        public static object rshift(CodeContext/*!*/ context, object a, object b) {
            return context.LanguageContext.Operation(PythonOperationKind.RightShift, a, b);
        }

        public static object sub(CodeContext/*!*/ context, object a, object b) {
            return context.LanguageContext.Operation(PythonOperationKind.Subtract, a, b);
        }

        public static object truediv(CodeContext/*!*/ context, object a, object b) {
            return context.LanguageContext.Operation(PythonOperationKind.TrueDivide, a, b);
        }

        public static object xor(CodeContext/*!*/ context, object a, object b) {
            return context.LanguageContext.Operation(PythonOperationKind.ExclusiveOr, a, b);
        }

        public static object concat(CodeContext/*!*/ context, object a, object b) {
            TestBothSequence(a, b);

            return context.LanguageContext.Operation(PythonOperationKind.Add, a, b);
        }

        public static bool contains(CodeContext/*!*/ context, object a, object b) {
            return context.LanguageContext.Contains(b, a);
        }

        public static int countOf(CodeContext/*!*/ context, object a, object b) {
            System.Collections.IEnumerator e = PythonOps.GetEnumerator(a);
            int count = 0;
            while (e.MoveNext()) {
                if (PythonOps.IsOrEqualsRetBool(context, e.Current, b)) {
                    count++;
                }
            }
            return count;
        }

        public static void delitem(CodeContext/*!*/ context, object a, object b) {
            context.LanguageContext.DelIndex(a, b);
        }

        public static object getitem(CodeContext/*!*/ context, object a, object b) {
            return PythonOps.GetIndex(context, a, b);
        }

        public static int indexOf(CodeContext/*!*/ context, object a, object b) {
            System.Collections.IEnumerator e = PythonOps.GetEnumerator(a);
            int index = 0;
            while (e.MoveNext()) {
                if (PythonOps.IsOrEqualsRetBool(context, e.Current, b)) {
                    return index;
                }
                index++;
            }
            throw PythonOps.ValueError("object not in sequence");
        }

        public static void setitem(CodeContext/*!*/ context, object a, object b, object c) {
            context.LanguageContext.SetIndex(a, b, c);
        }

        private static bool isSequenceType(object o) {
            return
                   CompilerHelpers.GetType(o) != typeof(PythonType) &&
                   !(o is PythonDictionary) && (
                   o is System.Collections.ICollection ||
                   o is System.Collections.IEnumerable ||
                   o is System.Collections.IEnumerator ||
                   o is System.Collections.IList ||
                   PythonOps.HasAttr(DefaultContext.Default, o, "__getitem__"));
        }

        public static object iadd(CodeContext/*!*/ context, object a, object b) {
            return context.LanguageContext.Operation(PythonOperationKind.InPlaceAdd, a, b);
        }

        public static object iand(CodeContext/*!*/ context, object a, object b) {
            return context.LanguageContext.Operation(PythonOperationKind.InPlaceBitwiseAnd, a, b);
        }

        public static object ifloordiv(CodeContext/*!*/ context, object a, object b) {
            return context.LanguageContext.Operation(PythonOperationKind.InPlaceFloorDivide, a, b);
        }

        public static object ilshift(CodeContext/*!*/ context, object a, object b) {
            return context.LanguageContext.Operation(PythonOperationKind.InPlaceLeftShift, a, b);
        }

        public static object imod(CodeContext/*!*/ context, object a, object b) {
            return context.LanguageContext.Operation(PythonOperationKind.InPlaceMod, a, b);
        }

        public static object imul(CodeContext/*!*/ context, object a, object b) {
            return context.LanguageContext.Operation(PythonOperationKind.InPlaceMultiply, a, b);
        }

        public static object ior(CodeContext/*!*/ context, object a, object b) {
            return context.LanguageContext.Operation(PythonOperationKind.InPlaceBitwiseOr, a, b);
        }

        public static object ipow(CodeContext/*!*/ context, object a, object b) {
            return context.LanguageContext.Operation(PythonOperationKind.InPlacePower, a, b);
        }

        public static object irshift(CodeContext/*!*/ context, object a, object b) {
            return context.LanguageContext.Operation(PythonOperationKind.InPlaceRightShift, a, b);
        }

        public static object isub(CodeContext/*!*/ context, object a, object b) {
            return context.LanguageContext.Operation(PythonOperationKind.InPlaceSubtract, a, b);
        }

        public static object itruediv(CodeContext/*!*/ context, object a, object b) {
            return context.LanguageContext.Operation(PythonOperationKind.InPlaceTrueDivide, a, b);
        }

        public static object ixor(CodeContext/*!*/ context, object a, object b) {
            return context.LanguageContext.Operation(PythonOperationKind.InPlaceExclusiveOr, a, b);
        }

        public static object iconcat(CodeContext/*!*/ context, object a, object b) {
            TestBothSequence(a, b);

            return context.LanguageContext.Operation(PythonOperationKind.InPlaceAdd, a, b);
        }

        public static object index(object a) {
            return Converter.ConvertToIndex(a);
        }

        [Documentation(@"compare_digest(a, b)-> bool

Return 'a == b'.  This function uses an approach designed to prevent
timing analysis, making it appropriate for cryptography.
a and b must both be of the same type: either str (ASCII only),
or any type that supports the buffer protocol (e.g. bytes).

Note: If a and b are of different lengths, or if an error occurs,
a timing attack could theoretically reveal information about the
types and lengths of a and b--but not their values.")]
        public static bool _compare_digest(object a, object b) {
            if(a is string && b is string) {
                string aStr = a as string;
                string bStr = b as string;
                return CompareBytes(aStr.MakeByteArray(), bStr.MakeByteArray());
            } else if(a is IBufferProtocol abp && b is IBufferProtocol bbp) {
                using IPythonBuffer aBuf = abp.GetBuffer();
                using IPythonBuffer bBuf = bbp.GetBuffer();
                if(aBuf.NumOfDims > 1 || bBuf.NumOfDims > 1) {
                    throw PythonOps.BufferError("Buffer must be single dimension");
                }

                return CompareBytes(aBuf.AsReadOnlySpan().ToArray(), bBuf.AsReadOnlySpan().ToArray());
            }
            throw PythonOps.TypeError("unsupported operand types(s) or combination of types: '{0}' and '{1}", PythonOps.GetPythonTypeName(a), PythonOps.GetPythonTypeName(b));
        }

        private static bool CompareBytes(IEnumerable<byte> a, IEnumerable<byte> b) {
            var aList = a.ToList();
            var bList = b.ToList();
            int len_b = bList.Count, len_a = aList.Count, length = len_b, result = 0;
            List<byte> left = null, right = bList;
            if(len_a == length) {
                left = aList;
                result = 0;
            }

            if(len_a != length) {
                left = bList;
                result = 1;
            }

            for(int i = 0; i < length; i++) {
                result |= (left[i] ^ right[i]);
            }
            return result == 0;
        }

        private static void TestBothSequence(object a, object b) {
            if (!isSequenceType(a)) {
                throw PythonOps.TypeError("'{0}' object cannot be concatenated", PythonTypeOps.GetName(a));
            } else if (!isSequenceType(b)) {
                throw PythonOps.TypeError("cannot concatenate '{0}' and '{1} objects", PythonTypeOps.GetName(a), PythonTypeOps.GetName(b));
            }
        }
    }
}
