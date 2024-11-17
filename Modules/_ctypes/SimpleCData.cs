// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_CTYPES

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting.Runtime;

namespace IronPython.Modules {
    /// <summary>
    /// Provides support for interop with native code from Python code.
    /// </summary>
    public static partial class CTypes {

        [PythonType("_SimpleCData")]
        public abstract class SimpleCData : CData, ICodeFormattable {
            // members: __bool__ __new__ __repr__ __ctypes_from_outparam__ __doc__ __init__

            protected SimpleCData() {
            }

            protected SimpleCData(params object[] args) {
            }

            public void __init__() {
                MemHolder = new MemoryHolder(Size);
            }

            public void __init__(CodeContext/*!*/ context, object value) {
                switch (((SimpleType)NativeType)._type) {
                    case SimpleTypeKind.Char: {
                            if (value is IList<byte> t && t.Count == 1) {
                                value = Bytes.FromByte(t[0]);
                            } else if (value is int i) {
                                try {
                                    value = Bytes.FromByte(checked((byte)i));
                                } catch (OverflowException) {
                                    throw PythonOps.TypeError("one character bytes, bytearray or integer expected");
                                }
                            } else {
                                throw PythonOps.TypeError("one character bytes, bytearray or integer expected");
                            }
                        }
                        break;
                    case SimpleTypeKind.WChar: {
                            if (value is string t) {
                                if (t.Length != 1) throw PythonOps.TypeError("one character unicode string expected");
                            } else {
                                throw PythonOps.TypeError("unicode string expected instead of {0} instance", DynamicHelpers.GetPythonType(value).Name);
                            }
                        }
                        break;
                    case SimpleTypeKind.SignedInt:
                    case SimpleTypeKind.SignedLong:
                    case SimpleTypeKind.SignedLongLong:
                    case SimpleTypeKind.SignedShort:
                    case SimpleTypeKind.UnsignedInt:
                    case SimpleTypeKind.UnsignedLong:
                    case SimpleTypeKind.UnsignedLongLong:
                    case SimpleTypeKind.UnsignedShort: {
                            object __int__ = null;
                            if (value is float || value is double) {
                                throw PythonOps.TypeError("int expected instead of float");
                            }

                            if (!(value is int || value is BigInteger || PythonOps.TryGetBoundAttr(value, "__int__", out __int__))) {
                                throw PythonOps.TypeError("an integer is required");
                            }

                            if (__int__ != null) {
                                value = PythonOps.CallWithContext(context, __int__);
                            }
                        }
                        break;
                    case SimpleTypeKind.Double:
                    case SimpleTypeKind.Single: {
                            object __float__ = null;

                            if (!(value is double || value is float || value is int || value is BigInteger || PythonOps.TryGetBoundAttr(value, "__float__", out __float__))) {
                                throw PythonOps.TypeError("a float is required");
                            }

                            if (value is BigInteger x) {
                                if (x > (BigInteger)double.MaxValue) {
                                    throw PythonOps.OverflowError("long int too large to convert to float");
                                }
                            }

                            if (__float__ != null) {
                                value = PythonOps.CallWithContext(context, __float__);
                            }
                        }
                        break;
                    default:
                        break;
                }

                MemHolder = new MemoryHolder(Size);
                NativeType.SetValue(MemHolder, 0, value);
                if (IsString) {
                    MemHolder.AddObject("str", value);
                }
            }

            // implemented as PropertyMethod's so that we can have delete
            [PropertyMethod, SpecialName]
            public void Setvalue(object value) {
                NativeType.SetValue(MemHolder, 0, value);
                if (IsString) {
                    MemHolder.AddObject("str", value);
                }
            }

            [PropertyMethod, SpecialName]
            public object Getvalue() {
                return NativeType.GetValue(MemHolder, this, 0, true);
            }

            [PropertyMethod, SpecialName]
            public void Deletevalue() {
                throw PythonOps.TypeError("cannot delete value property from simple cdata");
            }

            public override object _objects {
                get {
                    if (IsString) {
                        PythonDictionary objs = MemHolder.Objects;
                        if (objs != null) {
                            return objs["str"];
                        }
                    }

                    return MemHolder.Objects;
                }
            }

            private bool IsString {
                get {
                    SimpleTypeKind t = ((SimpleType)NativeType)._type;
                    return t == SimpleTypeKind.CharPointer || t == SimpleTypeKind.WCharPointer;
                }
            }

            #region ICodeFormattable Members

            public string __repr__(CodeContext context) {
                if (DynamicHelpers.GetPythonType(this).BaseTypes[0] == _SimpleCData) {
                    // direct subtypes have a custom repr
                    return String.Format("{0}({1})", DynamicHelpers.GetPythonType(this).Name, GetDataRepr(context));
                }

                return ObjectOps.__repr__(this);
            }

            private string GetDataRepr(CodeContext/*!*/ context) {
                return PythonOps.Repr(context, NativeType.GetValue(MemHolder, this, 0, false));
            }

            #endregion

        }
    }

}
#endif
