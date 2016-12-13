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

#if FEATURE_NATIVE

using System;
using System.Runtime.CompilerServices;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

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

            public void __init__(object value) {
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
                    return ((SimpleType)NativeType)._type == SimpleTypeKind.CharPointer ||
                                            ((SimpleType)NativeType)._type == SimpleTypeKind.WCharPointer;
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
