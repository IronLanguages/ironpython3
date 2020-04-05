// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_CTYPES

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Modules {
    /// <summary>
    /// Provides support for interop with native code from Python code.
    /// </summary>
    public static partial class CTypes {
        private static WeakDictionary<PythonType, Dictionary<int, ArrayType>> _arrayTypes = new WeakDictionary<PythonType, Dictionary<int, ArrayType>>();

        /// <summary>
        /// The meta class for ctypes array instances.
        /// </summary>
        [PythonType, PythonHidden]
        public class ArrayType : PythonType, INativeType {
            private int _length;
            private INativeType _type;

            public ArrayType(CodeContext/*!*/ context, string name, PythonTuple bases, PythonDictionary dict)
                : base(context, name, bases, dict) {
                int iLen;
                if (!dict.TryGetValue("_length_", out object len) || !(len is int) || (iLen = (int)len) < 0) {
                    throw PythonOps.AttributeError("arrays must have _length_ attribute and it must be a positive integer");
                }

                object type;
                if (!dict.TryGetValue("_type_", out type)) {
                    throw PythonOps.AttributeError("class must define a '_type_' attribute");
                }

                _length = iLen;
                _type = (INativeType)type;

                if (_type is SimpleType st) {
                    if (st._type == SimpleTypeKind.Char) {
                        // TODO: (c_int * 2).value isn't working
                        SetCustomMember(context,
                            "value",
                            new ReflectedExtensionProperty(
                                new ExtensionPropertyInfo(this, typeof(CTypes).GetMethod(nameof(CTypes.GetCharArrayValue))),
                                NameType.Property | NameType.Python
                            )
                        );

                        SetCustomMember(context,
                            "raw",
                            new ReflectedExtensionProperty(
                                new ExtensionPropertyInfo(this, typeof(CTypes).GetMethod(nameof(CTypes.GetWCharArrayRaw))),
                                NameType.Property | NameType.Python
                            )
                        );
                    } else if (st._type == SimpleTypeKind.WChar) {
                        SetCustomMember(context,
                            "value",
                            new ReflectedExtensionProperty(
                                new ExtensionPropertyInfo(this, typeof(CTypes).GetMethod(nameof(CTypes.GetWCharArrayValue))),
                                NameType.Property | NameType.Python
                            )
                        );

                        SetCustomMember(context,
                            "raw",
                            new ReflectedExtensionProperty(
                                new ExtensionPropertyInfo(this, typeof(CTypes).GetMethod(nameof(CTypes.GetWCharArrayRaw))),
                                NameType.Property | NameType.Python
                            )
                        );
                    }
                }
            }

            private ArrayType(Type underlyingSystemType)
                : base(underlyingSystemType) {
            }

            public _Array from_address(CodeContext/*!*/ context, int ptr) {
                _Array res = (_Array)CreateInstance(context);
                res.SetAddress(new IntPtr(ptr));
                return res;
            }

            public _Array from_address(CodeContext/*!*/ context, BigInteger ptr) {
                _Array res = (_Array)CreateInstance(context);
                res.SetAddress(new IntPtr((long)ptr));
                return res;
            }

            public _Array from_buffer(CodeContext/*!*/ context, ArrayModule.array array, [DefaultParameterValue(0)]int offset) {
                ValidateArraySizes(array, offset, ((INativeType)this).Size);

                _Array res = (_Array)CreateInstance(context);
                IntPtr addr = array.GetArrayAddress();
                res._memHolder = new MemoryHolder(addr.Add(offset), ((INativeType)this).Size);
                res._memHolder.AddObject("ffffffff", array);
                return res;
            }

            public _Array from_buffer_copy(CodeContext/*!*/ context, ArrayModule.array array, [DefaultParameterValue(0)]int offset) {
                ValidateArraySizes(array, offset, ((INativeType)this).Size);

                _Array res = (_Array)CreateInstance(context);
                res._memHolder = new MemoryHolder(((INativeType)this).Size);
                res._memHolder.CopyFrom(array.GetArrayAddress().Add(offset), new IntPtr(((INativeType)this).Size));
                GC.KeepAlive(array);
                return res;
            }

            public _Array from_buffer_copy(CodeContext/*!*/ context, Bytes array, [DefaultParameterValue(0)]int offset) {
                ValidateArraySizes(array, offset, ((INativeType)this).Size);

                _Array res = (_Array)CreateInstance(context);
                res._memHolder = new MemoryHolder(((INativeType)this).Size);
                for (int i = 0; i < ((INativeType)this).Size; i++) {
                    res._memHolder.WriteByte(i, ((IList<byte>)array)[i]);
                }
                return res;
            }

            public _Array from_buffer_copy(CodeContext/*!*/ context, string data, int offset=0) {
                ValidateArraySizes(data, offset, ((INativeType)this).Size);

                _Array res = (_Array)CreateInstance(context);
                res._memHolder = new MemoryHolder(((INativeType)this).Size);
                for (int i = 0; i < ((INativeType)this).Size; i++) {
                    res._memHolder.WriteByte(i, (byte)data[i]);
                }
                return res;
            }

            /// <summary>
            /// Converts an object into a function call parameter.
            /// </summary>
            public object from_param(object obj) {
                return null;
            }

            internal static PythonType MakeSystemType(Type underlyingSystemType) {
                return PythonType.SetPythonType(underlyingSystemType, new ArrayType(underlyingSystemType));
            }

            public static ArrayType/*!*/ operator *(ArrayType type, int count) {
                return MakeArrayType(type, count);
            }

            public static ArrayType/*!*/ operator *(int count, ArrayType type) {
                return MakeArrayType(type, count);
            }

#region INativeType Members

            int INativeType.Size {
                get {
                    return GetSize();
                }
            }

            private int GetSize() {
                return _length * _type.Size;
            }

            int INativeType.Alignment {
                get {
                    return _type.Alignment;
                }
            }

            object INativeType.GetValue(MemoryHolder owner, object readingFrom, int offset, bool raw) {
                if (IsStringType) {
                    SimpleType st = (SimpleType)_type;
                    if (st._type == SimpleTypeKind.Char) {
                        IList<byte> str = owner.ReadBytes(offset, _length);

                        // remove any trailing nulls
                        for (int i = 0; i < str.Count; i++) {
                            if (str[i] == 0) {
                                return new Bytes(str.Substring(0, i));
                            }
                        }

                        return str;

                    } else {
                        string str = owner.ReadUnicodeString(offset, _length);

                        // remove any trailing nulls
                        for (int i = 0; i < str.Length; i++) {
                            if (str[i] == '\x00') {
                                return str.Substring(0, i);
                            }
                        }

                        return str;
                    }
                }

                _Array arr = (_Array)CreateInstance(Context.SharedContext);
                arr._memHolder = new MemoryHolder(owner.UnsafeAddress.Add(offset), ((INativeType)this).Size, owner);
                return arr;
            }

            internal object GetRawValue(MemoryHolder owner, int offset) {
                Debug.Assert(IsStringType);
                SimpleType st = (SimpleType)_type;
                if (st._type == SimpleTypeKind.Char) {
                    return owner.ReadBytes(offset, _length);
                } else {
                    return owner.ReadUnicodeString(offset, _length);
                }
            }

            private bool IsStringType {
                get {
                    SimpleType st = _type as SimpleType;
                    if (st != null) {
                        return st._type == SimpleTypeKind.WChar || st._type == SimpleTypeKind.Char;
                    }

                    return false;
                }
            }

            object INativeType.SetValue(MemoryHolder address, int offset, object value) {
                string str = value as string;
                if (str != null) {
                    if (!IsStringType) {
                        throw PythonOps.TypeError("expected {0} instance, got str", Name);
                    } else if (str.Length > _length) {
                        throw PythonOps.ValueError("string too long ({0}, maximum length {1})", str.Length, _length);
                    }

                    WriteString(address, offset, str);

                    return null;
                } else if (IsStringType) {
                    IList<object> objList = value as IList<object>;
                    if (objList != null) {
                        StringBuilder res = new StringBuilder(objList.Count);
                        foreach (object o in objList) {
                            res.Append(Converter.ConvertToChar(o));
                        }

                        WriteString(address, offset, res.ToString());
                        return null;
                    }

                    throw PythonOps.TypeError("expected string or Unicode object, {0} found", DynamicHelpers.GetPythonType(value).Name);
                }

                object[] arrArgs = value as object[];
                if (arrArgs == null) {
                    PythonTuple pt = value as PythonTuple;
                    if (pt != null) {
                        arrArgs = pt._data;
                    }

                }

                if (arrArgs != null) {
                    if (arrArgs.Length > _length) {
                        throw PythonOps.RuntimeError("invalid index");
                    }

                    for (int i = 0; i < arrArgs.Length; i++) {
                        _type.SetValue(address, checked(offset + i * _type.Size), arrArgs[i]);
                    }
                } else {
                    _Array arr = value as _Array;
                    if (arr != null && arr.NativeType == this) {
                        arr._memHolder.CopyTo(address, offset, ((INativeType)this).Size);
                        return arr._memHolder.EnsureObjects();
                    }

                    throw PythonOps.TypeError("unexpected {0} instance, got {1}", Name, DynamicHelpers.GetPythonType(value).Name);
                }

                return null;
            }

            private void WriteString(MemoryHolder address, int offset, string str) {
                SimpleType st = (SimpleType)_type;
                if (str.Length < _length) {
                    str = str + '\x00';
                }
                if (st._type == SimpleTypeKind.Char) {
                    address.WriteAnsiString(offset, str);
                } else {
                    address.WriteUnicodeString(offset, str);
                }

            }

            Type/*!*/ INativeType.GetNativeType() {
                return typeof(IntPtr);
            }

            MarshalCleanup INativeType.EmitMarshalling(ILGenerator/*!*/ method, LocalOrArg argIndex, List<object>/*!*/ constantPool, int constantPoolArgument) {
                Type argumentType = argIndex.Type;
                Label done = method.DefineLabel();
                if (!argumentType.IsValueType) {
                    Label next = method.DefineLabel();
                    argIndex.Emit(method);
                    method.Emit(OpCodes.Ldnull);
                    method.Emit(OpCodes.Bne_Un, next);
                    method.Emit(OpCodes.Ldc_I4_0);
                    method.Emit(OpCodes.Conv_I);
                    method.Emit(OpCodes.Br, done);
                    method.MarkLabel(next);                    
                }

                argIndex.Emit(method);
                if (argumentType.IsValueType) {
                    method.Emit(OpCodes.Box, argumentType);
                }
                constantPool.Add(this);
                method.Emit(OpCodes.Ldarg, constantPoolArgument);
                method.Emit(OpCodes.Ldc_I4, constantPool.Count - 1);
                method.Emit(OpCodes.Ldelem_Ref);
                method.Emit(OpCodes.Call, typeof(ModuleOps).GetMethod(nameof(ModuleOps.CheckCDataType)));
                method.Emit(OpCodes.Call, typeof(CData).GetProperty(nameof(CData.UnsafeAddress)).GetGetMethod());

                method.MarkLabel(done);
                return null;
            }

            Type/*!*/ INativeType.GetPythonType() {
                return ((INativeType)this).GetNativeType();
            }

            void INativeType.EmitReverseMarshalling(ILGenerator method, LocalOrArg value, List<object> constantPool, int constantPoolArgument) {
                // TODO: Implement me
                value.Emit(method);
            }

            #endregion

            internal int Length {
                get {
                    return _length;
                }
            }

            internal INativeType ElementType {
                get {
                    return _type;
                }
            }

            string INativeType.TypeFormat {
                get {
                    string size = "(" + Length;
                    INativeType elemType = ElementType;
                    while (elemType is ArrayType) {
                        size += "," + ((ArrayType)elemType).Length;
                        elemType = ((ArrayType)elemType).ElementType;
                    }
                    size += ")";
                    return size + elemType.TypeFormat;
                }
            }
        }

        private static ArrayType/*!*/ MakeArrayType(PythonType type, int count) {
            if (count < 0) {
                throw PythonOps.ValueError("cannot multiply ctype by negative number");
            }

            lock (_arrayTypes) {
                if (!_arrayTypes.TryGetValue(type, out Dictionary<int, ArrayType> countDict)) {
                    _arrayTypes[type] = countDict = new Dictionary<int, ArrayType>();
                }

                if (!countDict.TryGetValue(count, out ArrayType res)) {
                    res = countDict[count] = new ArrayType(type.Context.SharedContext,
                        type.Name + "_Array_" + count,
                        PythonTuple.MakeTuple(Array),
                        PythonOps.MakeDictFromItems(new object[] { type, "_type_", count, "_length_" })
                    );
                }

                return res;
            }
        }        
    }
}

#endif
