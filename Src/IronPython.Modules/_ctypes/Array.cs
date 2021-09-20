// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_CTYPES

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Text;

using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting.Runtime;

namespace IronPython.Modules {
    /// <summary>
    /// Provides support for interop with native code from Python code.
    /// </summary>
    public static partial class CTypes {

        [PythonType("Array")]
        public abstract class _Array : CData {

            public void __init__(params object[] args) {
                INativeType nativeType = NativeType;

                _memHolder = new MemoryHolder(nativeType.Size);

                if (args.Length > ((ArrayType)nativeType).Length) {
                    throw PythonOps.IndexError("too many arguments");
                }

                INativeType elementType = ElementType;
                for (var i = 0; i < args.Length; i++) {
                    elementType.SetValue(_memHolder, checked(i * elementType.Size), args[i]);
                }
            }

            public object this[int index] {
                get {
                    index = PythonOps.FixIndex(index, ((ArrayType)NativeType).Length);
                    INativeType elementType = ElementType;
                    return elementType.GetValue(
                        _memHolder,
                        this,
                        checked(index * elementType.Size),
                        false
                    );
                }
                set {
                    index = PythonOps.FixIndex(index, ((ArrayType)NativeType).Length);
                    INativeType elementType = ElementType;

                    object keepAlive = elementType.SetValue(
                        _memHolder,
                        checked(index * elementType.Size),
                        value
                    );

                    if (keepAlive != null) {
                        _memHolder.AddObject(index.ToString(), keepAlive);
                    }
                }
            }

            public object this[[NotNull] Slice slice] {
                get {
                    int start, stop, step, count;
                    int size = ((ArrayType)NativeType).Length;
                    SimpleType elemType = ((ArrayType)NativeType).ElementType as SimpleType;

                    slice.GetIndicesAndCount(size, out start, out stop, out step, out count);
                    if ((step > 0 && start >= stop) || (step < 0 && start <= stop)) {
                        if (elemType != null) {
                            if (elemType._type == SimpleTypeKind.Char) return Bytes.Empty;
                            if (elemType._type == SimpleTypeKind.WChar) return string.Empty;
                        }
                        return new PythonList();
                    }

                    if (elemType != null) {
                        if (elemType._type == SimpleTypeKind.Char) {
                            Debug.Assert(((INativeType)elemType).Size == 1);
                            if (step == 1) {
                                return _memHolder.ReadBytes(start, count);
                            } else {
                                var buffer = new byte[count];
                                for (int i = 0, index = start; i < count; i++, index += step) {
                                    buffer[i] = _memHolder.ReadByte(index);
                                }
                                return Bytes.Make(buffer);
                            }
                        }
                        if (elemType._type == SimpleTypeKind.WChar) {
                            int elmSize = ((INativeType)elemType).Size;
                            if (step == 1) {
                                return _memHolder.ReadUnicodeString(checked(start * elmSize), count);
                            } else {
                                var res = new StringBuilder(count);
                                for (int i = 0, index = start; i < count; i++, index += step) {
                                    res.Append((char)_memHolder.ReadInt16(checked(index * elmSize)));
                                }
                                return res.ToString();
                            }
                        }
                    }

                    object[] ret = new object[count];
                    for (int i = 0, index = start; i < count; i++, index += step) {
                        ret[i] = this[index];
                    }

                    return PythonList.FromArrayNoCopy(ret);
                }
                set {
                    int start, stop, step, count;
                    int size = ((ArrayType)NativeType).Length;

                    slice.GetIndicesAndCount(size, out start, out stop, out step, out count);

                    IEnumerator ie = PythonOps.GetEnumerator(value);
                    for (int i = 0, index = start; i < count; i++, index += step) {
                        if (!ie.MoveNext()) {
                            throw PythonOps.ValueError("sequence not long enough");
                        }
                        this[index] = ie.Current;
                    }

                    if (ie.MoveNext()) {
                        throw PythonOps.ValueError("not all values consumed while slicing");
                    }
                }
            }

            public int __len__() {
                return ((ArrayType)NativeType).Length;
            }

            private INativeType ElementType {
                get {
                    return ((ArrayType)NativeType).ElementType;
                }
            }

            internal override PythonTuple GetBufferInfo() {
                var shape = Shape;
                return PythonTuple.MakeTuple(
                    NativeType.TypeFormat,
                    shape.Count,
                    PythonTuple.Make(shape)
                );
            }

            #region IBufferProtocol

            [PythonHidden]
            public override int ItemSize {
                get {
                    var curType = NativeType;
                    while (curType is ArrayType arrayType) {
                        curType = arrayType.ElementType;
                    }
                    return curType.Size;
                }
            }

            [PythonHidden]
            public override IReadOnlyList<int> Shape {
                get {
                    List<int> shape = new List<int>();
                    var curType = NativeType;
                    while (curType is ArrayType arrayType) {
                        shape.Add(arrayType.Length);
                        curType = arrayType.ElementType;
                    }
                    return shape;
                }
            }

            // TODO: if this.NativeType.ElementType is ArrayType, suboffsets need to be reported

            #endregion
        }
    }
}

#endif
