// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_CTYPES

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting.Utils;

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

                // TODO: is using TryGetBoundAttr the proper way to check the base types? similarly on the _type_ check
                if (!dict.TryGetValue("_length_", out object len) && !TryGetBoundAttr(context, this, "_length_", out len)) {
                    throw PythonOps.AttributeError("arrays must have _length_ attribute and it must be a positive integer");
                }
                int iLen = len switch {
                    BigInteger bi => checked((int)bi),
                    int i => i,
                    _ => throw PythonOps.AttributeError("arrays must have _length_ attribute and it must be a positive integer"),
                };
                if (iLen < 0) throw PythonOps.AttributeError("arrays must have _length_ attribute and it must be a positive integer"); // TODO: ValueError with 3.8

                object type;
                if (!dict.TryGetValue("_type_", out type) && !TryGetBoundAttr(context, this, "_type_", out type)) {
                    throw PythonOps.AttributeError("class must define a '_type_' attribute");
                }

                _length = iLen;
                _type = (INativeType)type;

                if (_type is SimpleType st) {
                    if (st._type == SimpleTypeKind.Char) {
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
                                new ExtensionPropertyInfo(this, typeof(CTypes).GetMethod(nameof(CTypes.GetCharArrayRaw))),
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

            public _Array from_buffer(CodeContext/*!*/ context, ArrayModule.array array, [DefaultParameterValue(0)] int offset) {
                ValidateArraySizes(array, offset, ((INativeType)this).Size);

                _Array res = (_Array)CreateInstance(context);
                IntPtr addr = array.GetArrayAddress();
                res.MemHolder = new MemoryHolder(addr.Add(offset), ((INativeType)this).Size);
                res.MemHolder.AddObject("ffffffff", array);
                return res;
            }

            public _Array from_buffer_copy(CodeContext/*!*/ context, IBufferProtocol data, int offset = 0) {
                using var buffer = data.GetBuffer();
                var span = buffer.AsReadOnlySpan();
                var size = ((INativeType)this).Size;
                ValidateArraySizes(span.Length, offset, size);
                span = span.Slice(offset, size);

                _Array res = (_Array)CreateInstance(context);
                res.MemHolder = new MemoryHolder(size);
                res.MemHolder.WriteSpan(0, span);
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
                if (_type is SimpleType st) {
                    if (st._type == SimpleTypeKind.Char) {
                        var str = owner.ReadBytes(offset, _length);

                        // remove any trailing nulls
                        for (int i = 0; i < str.Count; i++) {
                            if (str[i] == 0) {
                                return new Bytes(str.Substring(0, i));
                            }
                        }

                        return str;

                    }
                    if (st._type == SimpleTypeKind.WChar) {
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
                arr.MemHolder = new MemoryHolder(owner.UnsafeAddress.Add(offset), ((INativeType)this).Size, owner);
                return arr;
            }

            internal object GetRawValue(MemoryHolder owner, int offset) {
                Debug.Assert(_type is SimpleType st && st._type == SimpleTypeKind.Char);
                return owner.ReadBytes(offset, _length);
            }

            internal void SetRawValue(MemoryHolder owner, int offset, object value) {
                Debug.Assert(_type is SimpleType st && st._type == SimpleTypeKind.Char);
                if (value is IBufferProtocol bufferProtocol) {
                    var buffer = bufferProtocol.GetBuffer();
                    var span = buffer.AsReadOnlySpan();
                    if (span.Length > _length) {
                        throw PythonOps.ValueError("byte string too long ({0}, maximum length {1})", span.Length, _length);
                    }
                    owner.WriteSpan(offset, span);
                    return;
                }
                throw PythonOps.TypeErrorForBytesLikeTypeMismatch(value);
            }

            object INativeType.SetValue(MemoryHolder address, int offset, object value) {
                if (_type is SimpleType st) {
                    if (st._type == SimpleTypeKind.Char) {
                        if (value is Bytes bytes) {
                            if (bytes.Count > _length) {
                                throw PythonOps.ValueError("byte string too long ({0}, maximum length {1})", bytes.Count, _length);
                            }

                            WriteBytes(address, offset, bytes);

                            return null;
                        }
                        throw PythonOps.TypeError("expected bytes, {0} found", PythonOps.GetPythonTypeName(value));
                    }
                    if (st._type == SimpleTypeKind.WChar) {
                        if (value is string str) {
                            if (str.Length > _length) {
                                throw PythonOps.ValueError("string too long ({0}, maximum length {1})", str.Length, _length);
                            }

                            WriteString(address, offset, str);

                            return null;
                        }
                        throw PythonOps.TypeError("unicode string expected instead of {0} instance", PythonOps.GetPythonTypeName(value));
                    }
                }

                object[] arrArgs = value as object[];
                if (arrArgs == null) {
                    if (value is PythonTuple pt) {
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
                    if (value is _Array arr && arr.NativeType == this) {
                        arr.MemHolder.CopyTo(address, offset, ((INativeType)this).Size);
                        return arr.MemHolder.EnsureObjects();
                    }

                    throw PythonOps.TypeError("unexpected {0} instance, got {1}", Name, PythonOps.GetPythonTypeName(value));
                }

                return null;
            }

            private void WriteBytes(MemoryHolder address, int offset, Bytes bytes) {
                SimpleType st = (SimpleType)_type;
                Debug.Assert(st._type == SimpleTypeKind.Char && bytes.Count <= _length);
                address.WriteSpan(offset, bytes.AsSpan());
                if (bytes.Count < _length) {
                    address.WriteByte(checked(offset + bytes.Count), 0);
                }
            }

            private void WriteString(MemoryHolder address, int offset, string str) {
                SimpleType st = (SimpleType)_type;
                Debug.Assert(st._type == SimpleTypeKind.WChar && str.Length <= _length);
                if (str.Length < _length) {
                    str = str + '\x00';
                }
                address.WriteUnicodeString(offset, str);
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
                    return _type.TypeFormat;
                }
            }

            internal string ShapeAndFormatRepr() {
                string size = "(" + Length;
                INativeType elemType = _type;
                while (elemType is ArrayType arrayType) {
                    size += "," + arrayType.Length;
                    elemType = arrayType.ElementType;
                }
                size += ")";
                return size + _type.TypeFormat;
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
