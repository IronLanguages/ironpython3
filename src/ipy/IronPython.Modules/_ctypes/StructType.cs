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

using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Modules {
    /// <summary>
    /// Provides support for interop with native code from Python code.
    /// </summary>
    public static partial class CTypes {
        /// <summary>
        /// Meta class for structures.  Validates _fields_ on creation, provides factory
        /// methods for creating instances from addresses and translating to parameters.
        /// </summary>
        [PythonType, PythonHidden]
        public class StructType : PythonType, INativeType {
            internal Field[] _fields;
            private int? _size, _alignment, _pack;
            private static readonly Field[] _emptyFields = System.Array.Empty<Field>(); // fields were never initialized before a type was created

            public StructType(CodeContext/*!*/ context, string name, PythonTuple bases, PythonDictionary members)
                : base(context, name, bases, members) {

                foreach (PythonType pt in ResolutionOrder) {
                    StructType st = pt as StructType;
                    if (st != this) {
                        st?.EnsureFinal();
                    }

                    if (pt is UnionType ut) {
                        ut.EnsureFinal();
                    }
                }

                if (members.TryGetValue("_pack_", out object pack)) {
                    object index = PythonOps.Index(pack); // since 3.8
                    _pack = index switch {
                        int i => i,
                        BigInteger bi => (int)bi, // CPython throws the ValueError below on overflow
                        _ => throw new InvalidOperationException(),
                    };
                    if (_pack < 0) {
                        throw PythonOps.ValueError("pack must be a non-negative integer");
                    }
                }

                if (members.TryGetValue("_fields_", out object fields)) {
                    __setattr__(context, "_fields_", fields);
                }

                // TODO: _anonymous_
            }

            private StructType(Type underlyingSystemType)
                : base(underlyingSystemType) {
            }

            public static ArrayType/*!*/ operator *(StructType type, int count) {
                return MakeArrayType(type, count);
            }

            public static ArrayType/*!*/ operator *(int count, StructType type) {
                return MakeArrayType(type, count);
            }

            public _Structure from_address(CodeContext/*!*/ context, int address) {
                return from_address(context, new IntPtr(address));
            }

            public _Structure from_address(CodeContext/*!*/ context, BigInteger address) {
                return from_address(context, new IntPtr((long)address));
            }

            public _Structure from_address(CodeContext/*!*/ context, IntPtr ptr) {
                _Structure res = (_Structure)CreateInstance(context);
                res.SetAddress(ptr);
                return res;
            }

            public _Structure from_buffer(CodeContext/*!*/ context, object/*?*/ data, int offset = 0) {
                _Structure res = (_Structure)CreateInstance(context);
                res.InitializeFromBuffer(data, offset, ((INativeType)this).Size);
                return res;
            }

            public _Structure from_buffer_copy(CodeContext/*!*/ context, object/*?*/ data, int offset = 0) {
                _Structure res = (_Structure)CreateInstance(context);
                res.InitializeFromBufferCopy(data, offset, ((INativeType)this).Size);
                return res;
            }

            /// <summary>
            /// Converts an object into a function call parameter.
            /// 
            /// Structures just return themselves.
            /// </summary>
            public object from_param(object obj) {
                if (!Builtin.isinstance(obj, this)) {
                    throw PythonOps.TypeError("expected {0} instance got {1}", Name, PythonOps.GetPythonTypeName(obj));
                }

                return obj;
            }

            public object in_dll(object library, string name) {
                throw new NotImplementedException("in dll");
            }

            public new virtual void __setattr__(CodeContext/*!*/ context, string name, object value) {
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

            #region INativeType Members

            int INativeType.Size {
                get {
                    EnsureSizeAndAlignment();

                    return _size.Value;
                }
            }

            int INativeType.Alignment {
                get {
                    EnsureSizeAndAlignment();

                    return _alignment.Value;
                }
            }

            object INativeType.GetValue(MemoryHolder/*!*/ owner, object readingFrom, int offset, bool raw) {
                _Structure res = (_Structure)CreateInstance(this.Context.SharedContext);
                res.MemHolder = owner.GetSubBlock(offset);
                return res;
            }

            object INativeType.SetValue(MemoryHolder/*!*/ address, int offset, object value) {
                try {
                    return SetValueInternal(address, offset, value);
                } catch (ArgumentTypeException e) {
                    throw PythonOps.RuntimeError("({0}) <class 'TypeError'>: {1}",
                        Name,
                        e.Message);
                } catch (ArgumentException e) {
                    throw PythonOps.RuntimeError("({0}) <class 'ValueError'>: {1}",
                        Name,
                        e.Message);
                }
            }

            internal object SetValueInternal(MemoryHolder address, int offset, object value) {
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
                        throw new NotImplementedException("set value");
                    }
                }
                return null;
            }

            Type/*!*/ INativeType.GetNativeType() {
                EnsureFinal();

                return GetMarshalTypeFromSize(_size.Value);
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
                    if (_pack != null || _fields == _emptyFields || _fields == null) {
                        return "B";
                    }
                    StringBuilder res = new StringBuilder();
                    res.Append("T{");
                    foreach (Field f in _fields) {
                        res.Append(f.NativeType is ArrayType arrayType ? arrayType.ShapeAndFormatRepr() : f.NativeType.TypeFormat);
                        res.Append(':');
                        res.Append(f.FieldName);
                        res.Append(':');
                    }
                    res.Append('}');
                    return res.ToString();
                }
            }

            #endregion

            internal static PythonType MakeSystemType(Type underlyingSystemType) {
                return PythonType.SetPythonType(underlyingSystemType, new StructType(underlyingSystemType));
            }

            private void SetFields(object fields) {
                lock (this) {
                    IList<object> list = GetFieldsList(fields);

                    int? bitCount = null;
                    int? curBitCount = null;
                    INativeType lastType = null;
                    List<Field> allFields = GetBaseSizeAlignmentAndFields(out int size, out int alignment);

                    IList<object> anonFields = GetAnonymousFields(this);

                    for (int fieldIndex = 0; fieldIndex < list.Count; fieldIndex++) {
                        object o = list[fieldIndex];
                        GetFieldInfo(this, o, out string fieldName, out INativeType cdata, out bitCount);

                        int prevSize = UpdateSizeAndAlignment(cdata, bitCount, lastType, ref size, ref alignment, ref curBitCount);

                        Field newField = new Field(fieldName, cdata, prevSize, allFields.Count, bitCount, curBitCount - bitCount);
                        allFields.Add(newField);
                        AddSlot(fieldName, newField);

                        if (anonFields != null && anonFields.Contains(fieldName)) {
                            AddAnonymousFields(this, allFields, cdata, newField);
                        }

                        lastType = cdata;
                    }

                    CheckAnonymousFields(allFields, anonFields);

                    if (bitCount != null) {
                        size += lastType.Size;
                    }

                    _fields = allFields.ToArray();
                    _size = PythonStruct.Align(size, alignment);
                    _alignment = alignment;
                }
            }

            internal static void CheckAnonymousFields(List<Field> allFields, IList<object> anonFields) {
                if (anonFields != null) {
                    foreach (string s in anonFields) {
                        bool found = false;
                        foreach (Field f in allFields) {
                            if (f.FieldName == s) {
                                found = true;
                                break;
                            }
                        }

                        if (!found) {
                            throw PythonOps.AttributeError("anonymous field {0} is not defined in this structure", s);
                        }
                    }
                }
            }

            internal static IList<object> GetAnonymousFields(PythonType type) {
                object anonymous;
                IList<object> anonFields = null;
                if (type.TryGetBoundAttr(type.Context.SharedContext, type, "_anonymous_", out anonymous)) {
                    anonFields = anonymous as IList<object>;
                    if (anonFields == null) {
                        throw PythonOps.TypeError("_anonymous_ must be a sequence");
                    }
                }
                return anonFields;
            }

            internal static void AddAnonymousFields(PythonType type, List<Field> allFields, INativeType cdata, Field newField) {
                Field[] childFields;
                if (cdata is StructType) {
                    childFields = ((StructType)cdata)._fields;
                } else if (cdata is UnionType) {
                    childFields = ((UnionType)cdata)._fields;
                } else {
                    throw PythonOps.TypeError("anonymous field must be struct or union");
                }

                foreach (Field existingField in childFields) {
                    Field anonField = new Field(
                        existingField.FieldName,
                        existingField.NativeType,
                        checked(existingField.offset + newField.offset),
                        allFields.Count
                    );

                    type.AddSlot(existingField.FieldName, anonField);
                    allFields.Add(anonField);
                }
            }

            private List<Field> GetBaseSizeAlignmentAndFields(out int size, out int alignment) {
                size = 0;
                alignment = 1;
                List<Field> allFields = new List<Field>();
                INativeType lastType = null;
                int? totalBitCount = null;
                foreach (PythonType pt in BaseTypes) {
                    StructType st = pt as StructType;
                    if (st != null) {
                        foreach (Field f in st._fields) {
                            allFields.Add(f);
                            UpdateSizeAndAlignment(f.NativeType, f.BitCount, lastType, ref size, ref alignment, ref totalBitCount);

                            if (f.NativeType == this) {
                                throw StructureCannotContainSelf();
                            }

                            lastType = f.NativeType;
                        }
                    }
                }
                return allFields;
            }

            private int UpdateSizeAndAlignment(INativeType cdata, int? bitCount, INativeType lastType, ref int size, ref int alignment, ref int? totalBitCount) {
                int prevSize = size;
                if (bitCount != null) {
                    if (lastType != null && lastType.Size != cdata.Size) {
                        totalBitCount = null;
                        prevSize = size += lastType.Size;
                    }

                    size = PythonStruct.Align(size, cdata.Alignment);

                    if (totalBitCount != null) {
                        if ((bitCount + totalBitCount + 7) / 8 <= cdata.Size) {
                            totalBitCount = bitCount + totalBitCount;
                        } else {
                            size += lastType.Size;
                            prevSize = size;
                            totalBitCount = bitCount;
                        }
                    } else {
                        totalBitCount = bitCount;
                    }
                } else {
                    if (totalBitCount != null) {
                        size += lastType.Size;
                        prevSize = size;
                        totalBitCount = null;
                    }

                    if (_pack != null) {
                        alignment = _pack.Value;
                        prevSize = size = PythonStruct.Align(size, _pack.Value);

                        size += cdata.Size;
                    } else {
                        alignment = Math.Max(alignment, cdata.Alignment);
                        prevSize = size = PythonStruct.Align(size, cdata.Alignment);
                        size += cdata.Size;
                    }
                }

                return prevSize;
            }

            internal void EnsureFinal() {
                if (_fields == null) {
                    SetFields(PythonTuple.EMPTY);

                    if (_fields.Length == 0) {
                        // track that we were initialized w/o fields.
                        _fields = _emptyFields;
                    }
                }
            }

            /// <summary>
            /// If our size/alignment hasn't been initialized then grabs the size/alignment
            /// from all of our base classes.  If later new _fields_ are added we'll be
            /// initialized and these values will be replaced.
            /// </summary>
            private void EnsureSizeAndAlignment() {
                Debug.Assert(_size.HasValue == _alignment.HasValue);
                // these are always iniitalized together
                if (_size == null) {
                    lock (this) {
                        if (_size == null) {
                            int size, alignment;
                            GetBaseSizeAlignmentAndFields(out size, out alignment);
                            _size = size;
                            _alignment = alignment;
                        }
                    }
                }
            }
        }
    }
}

#endif
