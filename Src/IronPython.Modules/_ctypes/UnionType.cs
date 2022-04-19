// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_CTYPES

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using NotDynamicNullAttribute = Microsoft.Scripting.Runtime.NotNullAttribute;

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
            internal Field[] _fields;
            private int _size, _alignment;

            public UnionType(CodeContext/*!*/ context, string name, PythonTuple bases, PythonDictionary members)
                : base(context, name, bases, members) {

                object fields;
                if (members.TryGetValue("_fields_", out fields)) {
                    SetFields(fields);
                }
            }

            public new void __setattr__(CodeContext/*!*/ context, string name, object value) {
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
            public object from_param(object obj) {
                return null;
            }

            internal static PythonType MakeSystemType(Type underlyingSystemType) {
                return PythonType.SetPythonType(underlyingSystemType, new UnionType(underlyingSystemType));
            }

            public static ArrayType/*!*/ operator *(UnionType type, int count) {
                return MakeArrayType(type, count);
            }

            public static ArrayType/*!*/ operator *(int count, UnionType type) {
                return MakeArrayType(type, count);
            }

            public _Union from_buffer(CodeContext/*!*/ context, [NotDynamicNull] IBufferProtocol data, int offset = 0) {
                _Union res = (_Union)CreateInstance(context);
                res.InitializeFromBuffer(data, offset, ((INativeType)this).Size);
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

            object INativeType.SetValue(MemoryHolder address, int offset, object value) {
                IList<object> init = value as IList<object>;
                if (init != null) {
                    if (init.Count > _fields.Length) {
                        throw PythonOps.TypeError("too many initializers");
                    }

                    for (int i = 0; i < init.Count; i++) {
                        _fields[i].SetValue(address, offset, init[i]);
                    }
                } else {
                    CData data = value as CData;
                    if (data != null) {
                        data.MemHolder.CopyTo(address, offset, data.Size);
                        return data.MemHolder.EnsureObjects();
                    } else {
                        throw new NotImplementedException("Union set value");
                    }
                }
                return null;
            }

            Type INativeType.GetNativeType() {
                return GetMarshalTypeFromSize(_size);
            }

            MarshalCleanup INativeType.EmitMarshalling(ILGenerator/*!*/ method, LocalOrArg argIndex, List<object>/*!*/ constantPool, int constantPoolArgument) {
                Type argumentType = argIndex.Type;
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

            private void SetFields(object fields) {
                lock (this) {
                    IList<object> list = GetFieldsList(fields);
                    IList<object> anonFields = StructType.GetAnonymousFields(this);

                    int size = 0, alignment = 1;
                    List<Field> allFields = new List<Field>();//GetBaseSizeAlignmentAndFields(out size, out alignment);
                    int? bitCount;
                    for (int fieldIndex = 0; fieldIndex < list.Count; fieldIndex++) {
                        object o = list[fieldIndex];
                        string fieldName;
                        INativeType cdata;
                        GetFieldInfo(this, o, out fieldName, out cdata, out bitCount);
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

                    _fields = allFields.ToArray();
                    _size = PythonStruct.Align(size, alignment);
                    _alignment = alignment;
                }
            }

            internal void EnsureFinal() {
                if (_fields == null) {
                    SetFields(PythonTuple.EMPTY);
                }
            }
        }
    }
}

#endif
