// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

#if FEATURE_CTYPES

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection.Emit;

using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;


namespace IronPython.Modules {
    /// <summary>
    /// Provides support for interop with native code from Python code.
    /// </summary>
    public static partial class CTypes {
        /// <summary>
        /// The meta class for ctypes unions.
        /// </summary>
        [PythonType, PythonHidden]
        public class UnionType : PythonType, INativeType {
            internal Field[]? _fields;
            private int _size, _alignment;

            public UnionType(CodeContext/*!*/ context, [NotNone] string name, [NotNone] PythonTuple bases, [NotNone] PythonDictionary members)
                : base(context, name, bases, members) {

                if (members.TryGetValue("_fields_", out object fields)) {
                    SetFields(fields);
                }
            }

            public new void __setattr__(CodeContext/*!*/ context, [NotNone] string name, object? value) {
                if (name == "_fields_") {
                    lock (this) {
                        if (_fields != null) {
                            throw PythonOps.AttributeError("_fields_ is final");
                        }

                        SetFields(value);
                    }
                }

                base.__setattr__(context, name, value);
            }

            private UnionType(Type underlyingSystemType)
                : base(underlyingSystemType) {
            }

            /// <summary>
            /// Converts an object into a function call parameter.
            /// </summary>
            public object? from_param(object? obj) {
                return null;
            }

            internal static PythonType MakeSystemType(Type underlyingSystemType) {
                return PythonType.SetPythonType(underlyingSystemType, new UnionType(underlyingSystemType));
            }

            public static ArrayType/*!*/ operator *([NotNone] UnionType type, int count) {
                return MakeArrayType(type, count);
            }

            public static ArrayType/*!*/ operator *(int count, [NotNone] UnionType type) {
                return MakeArrayType(type, count);
            }

            public _Union from_buffer(CodeContext/*!*/ context, object? data, int offset = 0) {
                _Union res = (_Union)CreateInstance(context);
                res.InitializeFromBuffer(data, offset, ((INativeType)this).Size);
                return res;
            }

            public _Union from_buffer_copy(CodeContext/*!*/ context, object? data, int offset = 0) {
                _Union res = (_Union)CreateInstance(context);
                res.InitializeFromBufferCopy(data, offset, ((INativeType)this).Size);
                return res;
            }

            #region INativeType Members

            int INativeType.Size {
                get {
                    return _size;
                }
            }

            int INativeType.Alignment {
                get {
                    return _alignment;
                }
            }

            object INativeType.GetValue(MemoryHolder owner, object readingFrom, int offset, bool raw) {
                _Union res = (_Union)CreateInstance(this.Context.SharedContext);
                res.MemHolder = owner.GetSubBlock(offset);
                return res;
            }

            object? INativeType.SetValue(MemoryHolder address, int offset, object value) {
                if (value is IList<object> init) {
                    EnsureFinal();
                    if (init.Count > _fields.Length) {
                        throw PythonOps.TypeError("too many initializers");
                    }

                    for (int i = 0; i < init.Count; i++) {
                        _fields[i].SetValue(address, offset, init[i]);
                    }
                } else if (value is CData data) {
                    data.MemHolder.CopyTo(address, offset, data.Size);
                    return data.MemHolder.EnsureObjects();
                } else {
                    throw new NotImplementedException("Union set value");
                }
                return null;
            }

            Type INativeType.GetNativeType() {
                return GetMarshalTypeFromSize(_size);
            }

            MarshalCleanup? INativeType.EmitMarshalling(ILGenerator/*!*/ method, LocalOrArg argIndex, List<object>/*!*/ constantPool, int constantPoolArgument) {
                Type argumentType = argIndex.Type;
                argIndex.Emit(method);
                if (argumentType.IsValueType) {
                    method.Emit(OpCodes.Box, argumentType);
                }
                constantPool.Add(this);
                method.Emit(OpCodes.Ldarg, constantPoolArgument);
                method.Emit(OpCodes.Ldc_I4, constantPool.Count - 1);
                method.Emit(OpCodes.Ldelem_Ref);
                method.Emit(OpCodes.Call, typeof(ModuleOps).GetMethod(nameof(ModuleOps.CheckCDataType))!);
                method.Emit(OpCodes.Call, typeof(CData).GetProperty(nameof(CData.UnsafeAddress))!.GetGetMethod()!);
                method.Emit(OpCodes.Ldobj, ((INativeType)this).GetNativeType());
                return null;
            }

            Type/*!*/ INativeType.GetPythonType() {
                return typeof(object);
            }

            void INativeType.EmitReverseMarshalling(ILGenerator method, LocalOrArg value, List<object> constantPool, int constantPoolArgument) {
                value.Emit(method);
                EmitCDataCreation(this, method, constantPool, constantPoolArgument);
            }

            string INativeType.TypeFormat {
                get {
                    return "B";
                }
            }

            #endregion

            [MemberNotNull(nameof(_fields))]
            private void SetFields(object? fields) {
                lock (this) {
                    IList<object> fieldDefList = GetFieldsList(fields);
                    IList<string>? anonFields = StructType.GetAnonymousFields(this);

                    int size = 0, alignment = 1;
                    List<Field> allFields = new List<Field>();//GetBaseSizeAlignmentAndFields(out size, out alignment);
                    int? bitCount;
                    foreach (object fieldDef in fieldDefList) {
                        string fieldName;
                        INativeType cdata;
                        GetFieldInfo(this, fieldDef, out fieldName, out cdata, out bitCount);
                        alignment = Math.Max(alignment, cdata.Alignment);
                        size = Math.Max(size, cdata.Size);

                        Field newField = new Field(fieldName, cdata, 0, allFields.Count);
                        allFields.Add(newField);
                        AddSlot(fieldName, newField);

                        if (anonFields != null && anonFields.Contains(fieldName)) {
                            StructType.AddAnonymousFields(this, allFields, cdata, newField);
                        }
                    }

                    StructType.CheckAnonymousFields(allFields, anonFields);

                    _fields = [..allFields];
                    _size = PythonStruct.Align(size, alignment);
                    _alignment = alignment;
                }
            }

            [MemberNotNull(nameof(_fields))]
            internal void EnsureFinal() {
                if (_fields == null) {
                    SetFields(PythonTuple.EMPTY);
                }
            }
        }
    }
}

#endif
