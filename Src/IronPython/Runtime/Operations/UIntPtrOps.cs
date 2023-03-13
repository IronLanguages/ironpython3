// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

using IronPython.Runtime.Types;

using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime.Operations {
    public static class UIntPtrOps {

        #region Constructors

        [StaticExtensionMethod]
        public static object __new__(PythonType cls)
            => __new__(cls, default(UIntPtr));

        [StaticExtensionMethod]
        public static object __new__(PythonType cls, object value) {
            if (cls != DynamicHelpers.GetPythonTypeFromType(typeof(UIntPtr))) {
                throw PythonOps.TypeError("UIntPtr.__new__: first argument must be UIntPtr type.");
            }
            switch (value) {
                case UIntPtr:
                    return value;
                case IConvertible valueConvertible:
                    switch (valueConvertible.GetTypeCode()) {
                        case TypeCode.Byte: return checked((UIntPtr)(byte)value);
                        case TypeCode.SByte: return checked((UIntPtr)(sbyte)value);
                        case TypeCode.Int16: return checked((UIntPtr)(short)value);
                        case TypeCode.UInt16: return checked((UIntPtr)(ushort)value);
                        case TypeCode.Int32: return checked((UIntPtr)(int)value);
                        case TypeCode.UInt32: return checked((UIntPtr)(uint)value);
                        case TypeCode.Int64: return checked((UIntPtr)(long)value);
                        case TypeCode.UInt64: return checked((UIntPtr)(ulong)value);
                    }
                    break;
                case BigInteger bi:
                    if (UIntPtr.Size == 4) {
                        return checked((UIntPtr)(uint)bi);
                    }
                    return checked((UIntPtr)(ulong)bi);
                case Extensible<BigInteger> ebi:
                    if (UIntPtr.Size == 4) {
                        return checked((UIntPtr)(uint)ebi.Value);
                    }
                    return checked((UIntPtr)(ulong)ebi.Value);
            }
            throw PythonOps.TypeError("can't convert {0} to UIntPtr", PythonOps.GetPythonTypeName(value));
        }

        #endregion

        #region Unary Operations

        public static BigInteger __index__(UIntPtr x) => unchecked((BigInteger)(nuint)x);

        #endregion

        #region Binary Operations - Comparisons

        [SpecialName]
        public static bool Equals(UIntPtr x, UIntPtr y) => x == y;
        [SpecialName]
        public static bool NotEquals(UIntPtr x, UIntPtr y) => x != y;

        #endregion

    }
}
