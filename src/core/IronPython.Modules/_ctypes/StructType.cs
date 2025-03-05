// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

#if FEATURE_CTYPES

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
            [DisallowNull]
            internal Field[]? _fields;              // not null after type construction completes
            private int? _size, _alignment, _pack;
            private static readonly Field[] _emptyFields = []; // fields were never initialized before a type was created

            public StructType(CodeContext/*!*/ context, [NotNone] string name, [NotNone] PythonTuple bases, [NotNone] PythonDictionary members)
                : base(context, name, bases, members) {

                foreach (PythonType pt in ResolutionOrder) {
                    StructType? st = pt as StructType;
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

            public static ArrayType/*!*/ operator *([NotNone] StructType type, int count) {
                return MakeArrayType(type, count);
            }

            public static ArrayType/*!*/ operator *(int count, [NotNone] StructType type) {
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

            public _Structure from_buffer(CodeContext/*!*/ context, object? data, int offset = 0) {
                _Structure res = (_Structure)CreateInstance(context);
                res.InitializeFromBuffer(data, offset, ((INativeType)this).Size);
                return res;
            }

            public _Structure from_buffer_copy(CodeContext/*!*/ context, object? data, int offset = 0) {
                _Structure res = (_Structure)CreateInstance(context);
                res.InitializeFromBufferCopy(data, offset, ((INativeType)this).Size);
                return res;
            }

            /// <summary>
            /// Converts an object into a function call parameter.
            /// 
            /// Structures just return themselves.
            /// </summary>
            public object from_param(object? obj) {
                if (!Builtin.isinstance(obj, this)) {
                    throw PythonOps.TypeError("expected {0} instance, got {1}", Name, PythonOps.GetPythonTypeName(obj));
                }

                return obj!;
            }

            public object in_dll(object? library, [NotNone] string name) {
                throw new NotImplementedException("in dll");
            }

            public new virtual void __setattr__(CodeContext/*!*/ context, [NotNone] string name, object? value) {
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

            object? INativeType.SetValue(MemoryHolder/*!*/ address, int offset, object value) {
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

            internal object? SetValueInternal(MemoryHolder address, int offset, object value) {
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
                    throw new NotImplementedException("set value");
                }
                return null;
            }

            Type/*!*/ INativeType.GetNativeType() {
                EnsureFinal();

                return GetMarshalTypeFromSize(_size.Value);
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

            [MemberNotNull(nameof(_fields), nameof(_size), nameof(_alignment))]
            private void SetFields(object? fields) {
                lock (this) {
                    IList<object> fieldDefList = GetFieldsList(fields);

                    int? bitCount = null;
                    int? curBitCount = null;
                    INativeType? lastType = null;
                    List<Field> allFields = GetBaseSizeAlignmentAndFields(out int size, out int alignment);

                    IList<string>? anonFields = GetAnonymousFields(this);

                    foreach (object fieldDef in fieldDefList) {
                        GetFieldInfo(this, fieldDef, out string fieldName, out INativeType cdata, out bitCount);

                        int fieldOffset = UpdateSizeAndAlignment(cdata, bitCount, ref lastType, ref size, ref alignment, ref curBitCount);

                        var newField = new Field(fieldName, cdata, fieldOffset, allFields.Count, bitCount, curBitCount - bitCount);
                        allFields.Add(newField);
                        AddSlot(fieldName, newField);

                        if (anonFields != null && anonFields.Contains(fieldName)) {
                            AddAnonymousFields(this, allFields, cdata, newField);
                        }
                    }

                    CheckAnonymousFields(allFields, anonFields);

                    if (bitCount != null) {
                        // incomplete last bitfield
                        // bitCount not null implies at least one bitfield, so at least one iteration of the loop above
                        size += lastType!.Size;
                    }

                    _fields = [..allFields];
                    _size = PythonStruct.Align(size, alignment);
                    _alignment = alignment;
                }
            }

            internal static void CheckAnonymousFields(List<Field> allFields, IList<string>? anonFields) {
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


            internal static IList<string>? GetAnonymousFields(PythonType type) {
                IList<string>? anonFieldNames = null;
                if (type.TryGetBoundAttr(type.Context.SharedContext, type, "_anonymous_", out object anonymous)) {
                    if (anonymous is not IList<object> anonFields) {
                        throw PythonOps.TypeError("_anonymous_ must be a sequence");
                    }
                    anonFieldNames = [];
                    foreach (object anonField in anonFields) {
                        if (Converter.TryConvertToString(anonField, out string? anonFieldStr)) {
                            anonFieldNames.Add(anonFieldStr);
                        } else {
                            throw PythonOps.TypeErrorForBadInstance("anonymous field must be a string, not '{0}'", anonField);
                        }
                    }
                }
                return anonFieldNames;
            }


            internal static void AddAnonymousFields(PythonType type, List<Field> allFields, INativeType cdata, Field newField) {
                Field[] childFields;
                if (cdata is StructType st) {
                    st.EnsureFinal();
                    childFields = st._fields;
                } else if (cdata is UnionType un) {
                    un.EnsureFinal();
                    childFields = un._fields;
                } else {
                    throw PythonOps.TypeError("anonymous field must be struct or union");
                }

                foreach (Field existingField in childFields) {
                    var anonField = new Field(
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
                List<Field> allFields = [];
                INativeType? lastType = null;
                int? totalBitCount = null;
                foreach (PythonType pt in BaseTypes) {
                    if (pt is StructType st) {
                        st.EnsureFinal();
                        foreach (Field f in st._fields) {
                            allFields.Add(f);
                            UpdateSizeAndAlignment(f.NativeType, f.BitCount, ref lastType, ref size, ref alignment, ref totalBitCount);

                            if (f.NativeType == this) {
                                throw StructureCannotContainSelf();
                            }
                        }
                    }
                }
                return allFields;
            }


            /// <summary>
            /// Processes one field definition and allocates its data payload within the struct.
            /// </summary>
            /// <param name="cdata">
            /// The type of the field to process.</param>
            /// <param name="bitCount">
            /// Width of the bitfield in bits. If the fields to process is not a bitfield, this value is null</param>
            /// <param name="lastType">
            /// The type of the last field (or container unit) processed in the struct. If processing the first field in the struct, this value is null.
            /// On return, this value is updated with the processed field's type, or, if the processed field was a bitfield, with its container unit type.</param>
            /// <param name="size">
            /// The total size of the struct in bytes excluding an open bitfield container, if any.
            /// On input, the size of the struct before the field was allocated.
            /// On return, the size of the struct after the field has been processed.
            /// If the processed field was a bitfield, the size may not have been increased yet, depending whether the bitfield fit in the current container unit.
            /// So the full (current) struct size in bits is size * 8 + totalBitCount. </param>
            /// <param name="alignment">
            /// The total alignment of the struct (the common denominator of all fields).
            /// This value is being updated as necessary with the alignment of the processed field.</param>
            /// <param name="totalBitCount">
            /// The number of already occupied bits in the currently open containment unit for bitfields.
            /// If the previous field is not a bitfield, this value is null.
            /// On return, the count is updated with the number of occupied bits.</param>
            /// <returns>
            /// The offset of the processed field within the struct. If the processed field was a bitfield, this is the offset of its container unit.</returns>
            private int UpdateSizeAndAlignment(INativeType cdata, int? bitCount, ref INativeType? lastType, ref int size, ref int alignment, ref int? totalBitCount) {
                int fieldOffset;
                if (bitCount != null) {
                    // process a bitfield
                    Debug.Assert(bitCount <= cdata.Size * 8);
                    Debug.Assert(totalBitCount == null || lastType != null);

                    if (_pack != null) throw new NotImplementedException("pack with bitfields");  // TODO: implement

                    if (UseMsvcBitfieldAlignmentRules) {
                        if (totalBitCount != null) {
                            // this bitfield is not the first field in the struct
                            if (totalBitCount != null) { // there is already a bitfield container open
                                // under the MSVC rules, only bitfields of type that has the same size/alignment, are packed into the same container unit
                                if (lastType!.Size != cdata.Size || lastType.Alignment != cdata.Alignment) {
                                    // if the bitfield type is not compatible with the type of the previous container unit, close the previous container unit
                                    size += lastType.Size;
                                    fieldOffset = size = PythonStruct.Align(size, cdata.Alignment);  // TODO: _pack
                                    totalBitCount = null;
                                }
                            }
                        }
                        if (totalBitCount != null) {
                            // container unit open
                            if ((bitCount + totalBitCount + 7) / 8 <= cdata.Size) {
                                // new bitfield fits into the container unit
                                fieldOffset = size;
                                totalBitCount = bitCount + totalBitCount;
                            } else {
                                // new bitfield does not fit into the container unit, close it
                                size += lastType!.Size;
                                // and open a new container unit for the bitfield
                                fieldOffset = size = PythonStruct.Align(size, cdata.Alignment);  // TODO: _pack
                                totalBitCount = bitCount;
                                lastType = cdata;
                            }
                        } else {
                            // open a new container unit for the bitfield
                            fieldOffset = size = PythonStruct.Align(size, cdata.Alignment);  // TODO: _pack
                            totalBitCount = bitCount;
                            lastType = cdata;
                        }
                    } else {  // GCC bitfield alignment rules
                        // under the GCC rules, all bitfields are packed into the same container unit or an overlapping container unit of a different type,
                        // as long as they fit and match the alignment
                        int containerOffset = AlignBack(size, cdata.Alignment);  // TODO: _pack
                        int containerBitCount = (totalBitCount ?? 0) + (size - containerOffset) * 8;
                        while (containerBitCount + bitCount > cdata.Size * 8) {
                            // the bitfield does not fit into the container unit at this offset, try the next allowed offset
                            int deltaOffset = cdata.Alignment; // TODO: _pack 
                            containerOffset += deltaOffset;
                            containerBitCount = Math.Max(0, containerBitCount - deltaOffset * 8);
                        }
                        // the bitfield now fits into the container unit at this offset
                        fieldOffset = size = containerOffset;
                        totalBitCount = containerBitCount + bitCount;
                        lastType = cdata;
                    }
                    alignment = Math.Max(alignment, lastType!.Alignment);  // TODO: _pack
                } else {
                    // process a regular field
                    if (totalBitCount != null) {
                        // last field was a bitfield; close its container unit to prepare for the next regular field
                        size += lastType!.Size;
                        totalBitCount = null;
                    }

                    if (_pack != null) {
                        alignment = _pack.Value;
                        fieldOffset = size = PythonStruct.Align(size, _pack.Value);
                        size += cdata.Size;
                    } else {
                        alignment = Math.Max(alignment, cdata.Alignment);
                        fieldOffset = size = PythonStruct.Align(size, cdata.Alignment);
                        size += cdata.Size;
                    }
                    lastType = cdata;
                }

                return fieldOffset;  // this is the offset of the field or the container unit for the bitfield
            }


            [MemberNotNull(nameof(_fields), nameof(_size), nameof(_alignment))]
            internal void EnsureFinal() {
                if (_fields == null) {
                    SetFields(PythonTuple.EMPTY);

                    if (_fields.Length == 0) {
                        // track that we were initialized w/o fields.
                        _fields = _emptyFields;
                    }
                } else if (_size == null || _alignment == null) {
                    throw new InvalidOperationException("fields initialized w/o size or alignment");
                }
            }

            /// <summary>
            /// If our size/alignment hasn't been initialized then grabs the size/alignment
            /// from all of our base classes.  If later new _fields_ are added we'll be
            /// initialized and these values will be replaced.
            /// </summary>
            [MemberNotNull(nameof(_size), nameof(_alignment))]
            private void EnsureSizeAndAlignment() {
                Debug.Assert(_size.HasValue == _alignment.HasValue);
                // these are always initialized together
                if (_size == null) {
                    lock (this) {
                        if (_size == null) {
                            GetBaseSizeAlignmentAndFields(out int size, out int alignment);
                            _size = size;
                            _alignment = alignment;
                        }
                    }
                }
                if (_alignment == null) {
                    throw new InvalidOperationException("size and alignment should always be initialized together");
                }
            }

            private static int AlignBack(int length, int size)
                => length & ~(size - 1);

            private static bool UseMsvcBitfieldAlignmentRules
                => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }
    }
}

#endif
