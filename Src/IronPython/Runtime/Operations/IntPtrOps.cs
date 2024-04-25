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
    public static class IntPtrOps {

        #region Constructors

        [StaticExtensionMethod]
        public static object __new__(PythonType cls)
            => __new__(cls, default(IntPtr));

        [StaticExtensionMethod]
        public static object __new__(PythonType cls, object value) {
            if (cls != DynamicHelpers.GetPythonTypeFromType(typeof(IntPtr))) {
                throw PythonOps.TypeError("IntPtr.__new__: first argument must be IntPtr type.");
            }
            switch (value) {
                case IntPtr:
                    return value;
                case IConvertible valueConvertible:
                    switch (valueConvertible.GetTypeCode()) {
                        case TypeCode.Byte: return checked((IntPtr)(byte)value);
                        case TypeCode.SByte: return checked((IntPtr)(sbyte)value);
                        case TypeCode.Int16: return checked((IntPtr)(short)value);
                        case TypeCode.UInt16: return checked((IntPtr)(ushort)value);
                        case TypeCode.Int32: return checked((IntPtr)(int)value);
                        case TypeCode.UInt32: return checked((IntPtr)(uint)value);
                        case TypeCode.Int64: return checked((IntPtr)(long)value);
                        case TypeCode.UInt64: return checked((IntPtr)(ulong)value);
                    }
                    break;
                case BigInteger bi:
                    if (IntPtr.Size == 4) {
                        return checked((IntPtr)(int)bi);
                    }
                    return checked((IntPtr)(long)bi);
                case Extensible<BigInteger> ebi:
                    if (IntPtr.Size == 4) {
                        return checked((IntPtr)(int)ebi.Value);
                    }
                    return checked((IntPtr)(long)ebi.Value);
            }
            throw PythonOps.TypeError("can't convert {0} to IntPtr", PythonOps.GetPythonTypeName(value));
        }

        #endregion

        #region Unary Operations

        public static PythonTuple __getnewargs__(IntPtr self) => PythonTuple.MakeTuple(unchecked((BigInteger)(nint)self));

        public static BigInteger __index__(IntPtr x) => unchecked((BigInteger)(nint)x);

        #endregion

        #region Binary Operations - Comparisons

        [SpecialName]
        public static bool Equals(IntPtr x, IntPtr y) => x == y;
        [SpecialName]
        public static bool NotEquals(IntPtr x, IntPtr y) => x != y;

        #endregion

    }
}
