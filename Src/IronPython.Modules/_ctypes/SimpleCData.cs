// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_CTYPES

using System;
using System.Runtime.CompilerServices;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;
using System.Numerics;

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
                _memHolder = new MemoryHolder(Size);
            }

            public void __init__(CodeContext/*!*/ context, object value) {
                if(IsChar && !(value is string t && t.Length == 1)) {
                    throw PythonOps.TypeError("one character string expected");
                }

                if(IsIntegerType) {
                    object __int__ = null;
                    if(value is float || value is double) {
                        throw PythonOps.TypeError("int expected instead of float");
                    }

                    if (!(value is int || value is BigInteger || PythonOps.TryGetBoundAttr(value, "__int__", out __int__))) {
                        throw PythonOps.TypeError("an integer is required");
                    }

                    if(__int__ != null) {
                        value = PythonOps.CallWithContext(context, __int__);
                    }
                }

                if(IsFloatType) {
                    object __float__ = null;

                    if (!(value is double || value is float || value is int || value is BigInteger || PythonOps.TryGetBoundAttr(value, "__float__", out __float__))) {
                        throw PythonOps.TypeError("a float is required");
                    }

                    if(value is BigInteger x) {
                        if(x > (BigInteger)double.MaxValue) {
                            throw PythonOps.OverflowError("long int too large to convert to float");
                        }
                    }

                    if(__float__ != null) {
                        value = PythonOps.CallWithContext(context, __float__);
                    }
                }

                _memHolder = new MemoryHolder(Size);
                NativeType.SetValue(_memHolder, 0, value);
                if (IsString) {
                    _memHolder.AddObject("str", value);
                }
            }

            // implemented as PropertyMethod's so taht we can have delete
            [PropertyMethod, SpecialName]
            public void Setvalue(object value) {
                NativeType.SetValue(_memHolder, 0, value);
                if (IsString) {
                    _memHolder.AddObject("str", value);
                }
            }

            [PropertyMethod, SpecialName]
            public object Getvalue() {
                return NativeType.GetValue(_memHolder, this, 0, true);
            }

            [PropertyMethod, SpecialName]
            public void Deletevalue() {
                throw PythonOps.TypeError("cannot delete value property from simple cdata");
            }

            public override object _objects {
                get {
                    if (IsString) {
                        PythonDictionary objs = _memHolder.Objects;
                        if (objs != null) {
                            return objs["str"];
                        }
                    }

                    return _memHolder.Objects;
                }
            }

            private bool IsString {
                get {
                    SimpleTypeKind t = ((SimpleType)NativeType)._type;
                    return t == SimpleTypeKind.CharPointer || t == SimpleTypeKind.WCharPointer;
                }
            }

            private bool IsChar {
                get {
                    SimpleTypeKind t = ((SimpleType)NativeType)._type;
                    return t == SimpleTypeKind.Char || t == SimpleTypeKind.WChar;
                }
            }

            private bool IsIntegerType {
                get {
                    SimpleTypeKind t = ((SimpleType)NativeType)._type;
                    return t == SimpleTypeKind.SignedInt || t == SimpleTypeKind.SignedLong || t == SimpleTypeKind.SignedLongLong ||
                        t == SimpleTypeKind.SignedShort || t == SimpleTypeKind.UnsignedInt || t == SimpleTypeKind.UnsignedLong ||
                        t == SimpleTypeKind.UnsignedLongLong || t == SimpleTypeKind.UnsignedShort;
                }
            }

            private bool IsFloatType {
                get {
                    SimpleTypeKind t = ((SimpleType)NativeType)._type;
                    return t == SimpleTypeKind.Double || t == SimpleTypeKind.Single;
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
                return PythonOps.Repr(context, NativeType.GetValue(_memHolder, this, 0, false));
            }

            #endregion

        }
    }

}
#endif
