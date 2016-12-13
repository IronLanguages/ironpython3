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

using System.Runtime.CompilerServices;
using Microsoft.Scripting.Runtime;

#if FEATURE_NUMERICS
using System.Numerics;
#else
using Microsoft.Scripting.Math;
#endif

namespace IronPython.Runtime.Operations {
    public static class DecimalOps {

        public static int __cmp__(CodeContext context, decimal x, decimal other) {
            return x.CompareTo(other);
        }

        public static bool __bool__(decimal x) {
            return x != 0;
        }

        [SpecialName]
        public static bool LessThan(decimal x, decimal y) {
            return x < y;
        }
        [SpecialName]
        public static bool LessThanOrEqual(decimal x, decimal y) {
            return x <= y;
        }
        [SpecialName]
        public static bool GreaterThan(decimal x, decimal y) {
            return x > y;
        }
        [SpecialName]
        public static bool GreaterThanOrEqual(decimal x, decimal y) {
            return x >= y;
        }
        [SpecialName]
        public static bool Equals(decimal x, decimal y) {
            return x == y;
        }
        [SpecialName]
        public static bool NotEquals(decimal x, decimal y) {
            return x != y;
        }

        internal static int __cmp__(BigInteger x, decimal y) {
            return -__cmp__(y, x);
        }

        internal static int __cmp__(decimal x, BigInteger y) {
#if CLR2
            if (object.ReferenceEquals(y, null)) return +1;
#endif
            BigInteger bx = (BigInteger)x;
            if (bx == y) {
                decimal mod = x % 1;
                if (mod == 0) return 0;
                if (mod > 0) return +1;
                else return -1;
            }
            return bx > y ? +1 : -1;
        }

#if !CLR2
        [return: MaybeNotImplemented]
        internal static object __cmp__(object x, decimal y) {
            return __cmp__(y, x);
        }

        [return: MaybeNotImplemented]
        internal static object __cmp__(decimal x, object y) {
            if (object.ReferenceEquals(y, null)) {
                return ScriptingRuntimeHelpers.Int32ToObject(+1);
            }
            return PythonOps.NotImplemented;
        }
#endif

        public static int __hash__(decimal x) {
            return ((BigInteger)x).GetHashCode();   
        }
    }
}
