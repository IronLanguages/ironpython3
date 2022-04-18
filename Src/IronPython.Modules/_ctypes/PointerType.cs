// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_CTYPES

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection.Emit;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Modules {
    /// <summary>
    /// Provides support for interop with native code from Python code.
    /// </summary>
    public static partial class CTypes {

        /// <summary>
        /// The meta class for ctypes pointers.
        /// </summary>
        [PythonType, PythonHidden]
        public class PointerType : PythonType, INativeType {
            internal INativeType _type;
            private readonly string _typeFormat;

            public PointerType(CodeContext/*!*/ context, string name, PythonTuple bases, PythonDictionary members)
                : base(context, name, bases, members) {

                object type;
                if (members.TryGetValue("_type_", out type) && !(type is INativeType)) {
                    throw PythonOps.TypeError("_type_ must be a type");
                }
                _type = (INativeType)type;
                if (_type != null) {
                    _typeFormat = _type is ArrayType arrayType ? arrayType.ShapeAndFormatRepr() : _type.TypeFormat;
                }
            }

            private PointerType(Type underlyingSystemType)
                : base(underlyingSystemType) {
            }

            public object from_param([NotNull]CData obj) {
                return new NativeArgument((CData)PythonCalls.Call(this, obj), "P");
            }

            /// <summary>
            /// Converts an object into a function call parameter.
            /// </summary>
            public object from_param(Pointer obj) {
                if (obj == null) {
                    return ScriptingRuntimeHelpers.Int32ToObject(0);
                }

                if (obj.NativeType != this) {
                    throw PythonOps.TypeError("assign to pointer of type {0} from {1} is not valid", Name, ((PythonType)obj.NativeType).Name);
                }

                Pointer res = (Pointer)PythonCalls.Call(this);
                res.MemHolder.WriteIntPtr(0, obj.MemHolder.ReadMemoryHolder(0));
                return res;
            }

            public object from_param([NotNull]NativeArgument obj) {
                return (CData)PythonCalls.Call(this, obj._obj);
            }

            /// <summary>
            /// Access an instance at the specified address
            /// </summary>
            public object from_address(object obj) {
                throw new NotImplementedException("pointer from address");
            }

            public void set_type(PythonType type) {
                _type = (INativeType)type;
            }

            internal static PythonType MakeSystemType(Type underlyingSystemType) {
                return PythonType.SetPythonType(underlyingSystemType, new PointerType(underlyingSystemType));
            }

            public static ArrayType/*!*/ operator *(PointerType type, int count) {
                return MakeArrayType(type, count);
            }

            public static ArrayType/*!*/ operator *(int count, PointerType type) {
                return MakeArrayType(type, count);
            }

            #region INativeType Members

            int INativeType.Size {
                get {
                    return IntPtr.Size;
                }
            }

            int INativeType.Alignment {
                get {
                    return IntPtr.Size;
                }
            }

            object INativeType.GetValue(MemoryHolder owner, object readingFrom, int offset, bool raw) {
                if (!raw) {
                    Pointer res = (Pointer)PythonCalls.Call(Context.SharedContext, this);
                    res.MemHolder.WriteIntPtr(0, owner.ReadIntPtr(offset));
                    res.MemHolder.AddObject(offset, readingFrom);
                    return res;
                }
                return owner.ReadIntPtr(offset).ToPython();
            }

            object INativeType.SetValue(MemoryHolder address, int offset, object value) {
                Pointer ptr;
                _Array array;
                if (value == null) {
                    address.WriteIntPtr(offset, IntPtr.Zero);
                }  else if (value is int) {
                    address.WriteIntPtr(offset, new IntPtr((int)value));
                } else if (value is BigInteger) {
                    address.WriteIntPtr(offset, new IntPtr((long)(BigInteger)value));
                } else if ((ptr = value as Pointer) != null) {
                    address.WriteIntPtr(offset, ptr.MemHolder.ReadMemoryHolder(0));
                    return PythonOps.MakeDictFromItems(ptr, "0", ptr._objects, "1");
                } else if ((array = value as _Array) != null) {
                    address.WriteIntPtr(offset, array.MemHolder);
                    return array;
                } else {
                    throw PythonOps.TypeErrorForTypeMismatch(Name, value);
                }

                return null;
            }

            Type INativeType.GetNativeType() {
                return typeof(IntPtr);
            }

            MarshalCleanup INativeType.EmitMarshalling(ILGenerator/*!*/ method, LocalOrArg argIndex, List<object>/*!*/ constantPool, int constantPoolArgument) {
                Type argumentType = argIndex.Type;
                Label nextTry = method.DefineLabel();
                Label done = method.DefineLabel();

                if (!argumentType.IsValueType) {
                    argIndex.Emit(method);
                    method.Emit(OpCodes.Ldnull);
                    method.Emit(OpCodes.Bne_Un, nextTry);
                    method.Emit(OpCodes.Ldc_I4_0);
                    method.Emit(OpCodes.Conv_I);
                    method.Emit(OpCodes.Br, done);
                }

                method.MarkLabel(nextTry);
                nextTry = method.DefineLabel();
                
                argIndex.Emit(method);
                if (argumentType.IsValueType) {
                    method.Emit(OpCodes.Box, argumentType);
                }
                constantPool.Add(this);

                SimpleType st = _type as SimpleType;
                MarshalCleanup res = null;
                if (st != null && !argIndex.Type.IsValueType) {
                    if (st._type == SimpleTypeKind.Char || st._type == SimpleTypeKind.WChar) {
                            
                        if (st._type == SimpleTypeKind.Char) {
                            SimpleType.TryToCharPtrConversion(method, argIndex, argumentType, done);
                        } else {
                            SimpleType.TryArrayToWCharPtrConversion(method, argIndex, argumentType, done);
                        }

                        Label notStr = method.DefineLabel();
                        LocalOrArg str = argIndex;
                        if (argumentType != typeof(string)) {
                            LocalBuilder lb = method.DeclareLocal(typeof(string));
                            method.Emit(OpCodes.Isinst, typeof(string));
                            method.Emit(OpCodes.Brfalse, notStr);
                            argIndex.Emit(method);
                            method.Emit(OpCodes.Castclass, typeof(string));
                            method.Emit(OpCodes.Stloc, lb);
                            method.Emit(OpCodes.Ldloc, lb);
                            str = new Local(lb);
                        }

                        if (st._type == SimpleTypeKind.Char) {
                            res = SimpleType.MarshalCharPointer(method, str);
                        } else {
                            SimpleType.MarshalWCharPointer(method, str);
                        }
                        method.Emit(OpCodes.Br, done);
                        method.MarkLabel(notStr);
                        argIndex.Emit(method);
                    }
                }

                // native argument being pased (byref)
                method.Emit(OpCodes.Ldarg, constantPoolArgument);
                method.Emit(OpCodes.Ldc_I4, constantPool.Count - 1);
                method.Emit(OpCodes.Ldelem_Ref);
                method.Emit(OpCodes.Call, typeof(ModuleOps).GetMethod(nameof(ModuleOps.CheckNativeArgument)));
                method.Emit(OpCodes.Dup);
                method.Emit(OpCodes.Brfalse, nextTry);
                method.Emit(OpCodes.Call, typeof(CData).GetProperty(nameof(CData.UnsafeAddress)).GetGetMethod());
                method.Emit(OpCodes.Br, done);

                // lone cdata being passed
                method.MarkLabel(nextTry);
                nextTry = method.DefineLabel();
                method.Emit(OpCodes.Pop);   // extra null native arg
                argIndex.Emit(method);
                if (argumentType.IsValueType) {
                    method.Emit(OpCodes.Box, argumentType);
                }
                method.Emit(OpCodes.Ldarg, constantPoolArgument);
                method.Emit(OpCodes.Ldc_I4, constantPool.Count - 1);
                method.Emit(OpCodes.Ldelem_Ref);
                method.Emit(OpCodes.Call, typeof(ModuleOps).GetMethod(nameof(ModuleOps.TryCheckCDataPointerType)));
                method.Emit(OpCodes.Dup);
                method.Emit(OpCodes.Brfalse, nextTry);
                method.Emit(OpCodes.Call, typeof(CData).GetProperty(nameof(CData.UnsafeAddress)).GetGetMethod());
                method.Emit(OpCodes.Br, done);

                // pointer object being passed
                method.MarkLabel(nextTry);
                method.Emit(OpCodes.Pop);   // extra null cdata
                argIndex.Emit(method);
                if (argumentType.IsValueType) {
                    method.Emit(OpCodes.Box, argumentType);
                }
                method.Emit(OpCodes.Ldarg, constantPoolArgument);
                method.Emit(OpCodes.Ldc_I4, constantPool.Count - 1);
                method.Emit(OpCodes.Ldelem_Ref);
                method.Emit(OpCodes.Call, typeof(ModuleOps).GetMethod(nameof(ModuleOps.CheckCDataType)));
                method.Emit(OpCodes.Call, typeof(CData).GetProperty(nameof(CData.UnsafeAddress)).GetGetMethod());
                method.Emit(OpCodes.Ldind_I);

                method.MarkLabel(done);
                return res;
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
                    return "&" + (_typeFormat ?? _type.TypeFormat);
                }
            }

            #endregion
        }
    }
}

#endif
