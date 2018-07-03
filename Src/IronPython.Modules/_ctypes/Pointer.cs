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

#if FEATURE_CTYPES

using System;
using System.Text;

using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Modules {
    /// <summary>
    /// Provides support for interop with native code from Python code.
    /// </summary>
    public static partial class CTypes {
        [PythonType("_Pointer")]
        public abstract class Pointer : CData {
#pragma warning disable 414 // unused field - we need to keep the object alive
            private readonly CData _object;
#pragma warning restore 414

            public Pointer() {
                _memHolder = new MemoryHolder(IntPtr.Size);
            }

            public Pointer(CData value) {
                _object = value; // Keep alive the object, more to do here.
                _memHolder = new MemoryHolder(IntPtr.Size);
                _memHolder.WriteIntPtr(0, value._memHolder);
                _memHolder.AddObject("1", value);
                if (value._objects != null) {
                    _memHolder.AddObject("0", value._objects);
                }
            }

            public object contents {
                get {
                    PythonType elementType = (PythonType)((PointerType)NativeType)._type;

                    CData res = (CData)elementType.CreateInstance(elementType.Context.SharedContext);
                    res._memHolder = _memHolder.ReadMemoryHolder(0);
                    if(res._memHolder.UnsafeAddress == IntPtr.Zero) {
                        throw PythonOps.ValueError("NULL value access");
                    }
                    return res;
                }
                set {
                }
            }

            public object this[int index] {
                get {
                    INativeType type = ((PointerType)NativeType)._type;
                    MemoryHolder address = _memHolder.ReadMemoryHolder(0);

                    return type.GetValue(address, this, checked(type.Size * index), false);
                }
                set {
                    MemoryHolder address = _memHolder.ReadMemoryHolder(0);

                    INativeType type = ((PointerType)NativeType)._type;
                    object keepAlive = type.SetValue(address, checked(type.Size * index), value);
                    if (keepAlive != null) {
                        _memHolder.AddObject(index.ToString(), keepAlive);
                    }
                }
            }

            public bool __bool__() {
                return _memHolder.ReadIntPtr(0) != IntPtr.Zero;
            }

            public object this[Slice index] {
                get {
                    if (index.stop == null) {
                        throw PythonOps.ValueError("slice stop is required");
                    }

                    int start = index.start != null ? (int)index.start : 0;
                    int stop = index.stop != null ? (int)index.stop : 0;
                    int step = index.step != null ? (int)index.step : 1;

                    if (step < 0 && index.start == null) {
                        throw PythonOps.ValueError("slice start is required for step < 0");
                    }

                    if (start < 0) {
                        start = 0;
                    }
                    INativeType type = ((PointerType)NativeType)._type;
                    SimpleType elemType = type as SimpleType;

                    if ((stop < start && step > 0) || (start < stop && step < 0)) {
                        if (elemType != null && (elemType._type == SimpleTypeKind.WChar || elemType._type == SimpleTypeKind.Char)) {
                            return String.Empty;
                        }
                        return new List();
                    }

                    MemoryHolder address = _memHolder.ReadMemoryHolder(0);
                    if (elemType != null && (elemType._type == SimpleTypeKind.WChar || elemType._type == SimpleTypeKind.Char)) {
                        int elmSize = ((INativeType)elemType).Size;
                        StringBuilder res = new StringBuilder();

                        for (int i = start; stop > start ? i < stop : i > stop; i += step) {
                            res.Append(
                                elemType.ReadChar(address, checked(i * elmSize))
                            );
                        }

                        return res.ToString();
                    } else {
                        List res = new List((stop - start) / step);
                        for (int i = start; stop > start ? i < stop : i > stop; i += step) {
                            res.AddNoLock(
                                type.GetValue(address, this, checked(type.Size * i), false)
                            );
                        }
                        return res;
                    }
                }
            }
        }
    }
}
#endif
