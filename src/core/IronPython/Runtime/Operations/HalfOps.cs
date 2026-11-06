// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

#if NET6_0_OR_GREATER

using System;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Types;

namespace IronPython.Runtime.Operations {
    public static class HalfOps {

        [StaticExtensionMethod]
        public static object __new__(PythonType cls) {
            CheckHalfType(cls);
            return default(Half);
        }

        [StaticExtensionMethod]
        public static object __new__(CodeContext/*!*/ context, PythonType cls, object x) {
            CheckHalfType(cls);
            if (x is Half h) return h;
            if (Converter.TryConvertToDouble(x, out double doubleVal)) return (Half)doubleVal;

            return (Half)(double)DoubleOps.NewFloat(context, TypeCache.Double, x);
        }

        private static void CheckHalfType(PythonType cls) {
            if (cls != DynamicHelpers.GetPythonTypeFromType(typeof(Half))) {
                throw PythonOps.TypeError("Half.__new__: first argument must be Half type.");
            }
        }
    }
}

#endif
