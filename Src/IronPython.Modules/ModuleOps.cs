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
using System.Diagnostics;
using System.Runtime.InteropServices;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using System.Numerics;

namespace IronPython.Modules {
    /// <summary>
    /// Provides helper functions which need to be called from generated code to implement various 
    /// portions of modules.
    /// </summary>
    public static partial class ModuleOps {
        public static IntPtr StringToHGlobalAnsi(string str) {
            if (str == null) {
                return IntPtr.Zero;
            }

            return Marshal.StringToHGlobalAnsi(str);
        }

        public static IntPtr StringToHGlobalUni(string str) {
            if (str == null) {
                return IntPtr.Zero;
            }

            return Marshal.StringToHGlobalUni(str);
        }

        public static object DoErrorCheck(object errCheckFunc, object result, object func, object[] arguments) {
            return PythonCalls.Call(errCheckFunc, result, func, PythonTuple.Make(arguments));
        }

        public static object CreateMemoryHolder(IntPtr data, int size) {
            var res = new MemoryHolder(size);
            res.CopyFrom(data, new IntPtr(size));
            return res;
        }

        public static object CreateNativeWrapper(PythonType type, object holder) {
            Debug.Assert(holder is MemoryHolder);

            CTypes.CData data = (CTypes.CData)type.CreateInstance(type.Context.SharedContext);
            data._memHolder = (MemoryHolder)holder;
            return data;
        }

        public static object CreateCData(IntPtr dataAddress, PythonType type) {
            CTypes.INativeType nativeType = (CTypes.INativeType)type;
            CTypes.CData data = (CTypes.CData)type.CreateInstance(type.Context.SharedContext);
            data._memHolder = new MemoryHolder(nativeType.Size);
            data._memHolder.CopyFrom(dataAddress, new IntPtr(nativeType.Size));
            return data;
        }

        public static object CreateCFunction(IntPtr address, PythonType type) {
            return type.CreateInstance(type.Context.SharedContext, address);
        }

        public static CTypes.CData CheckSimpleCDataType(object o, object type) {
            CTypes.SimpleCData res = o as CTypes.SimpleCData;
            object asParam;
            if (res == null && PythonOps.TryGetBoundAttr(o, "_as_parameter_", out asParam)) {
                res = asParam as CTypes.SimpleCData;
            }

            if (res != null && res.NativeType != type) {
                throw PythonOps.TypeErrorForTypeMismatch(((PythonType)type).Name, o);
            }

            return res;
        }

        public static CTypes.CData/*!*/ CheckCDataType(object o, object type) {
            CTypes.CData res = o as CTypes.CData;
            object asParam;
            if (res == null && PythonOps.TryGetBoundAttr(o, "_as_parameter_", out asParam)) {
                res = asParam as CTypes.CData;
            }

            if (res == null || res.NativeType != type) {
                throw ArgumentError(type, ((PythonType)type).Name, o);
            }

            return res;
        }

        public static IntPtr/*!*/ GetFunctionPointerValue(object o, object type) {
            CTypes._CFuncPtr res = o as CTypes._CFuncPtr;
            object asParam;
            if (res == null && PythonOps.TryGetBoundAttr(o, "_as_parameter_", out asParam)) {
                res = asParam as CTypes._CFuncPtr;
            }
            
            if (res == null || res.NativeType != type) {
                throw ArgumentError(type, ((PythonType)type).Name, o);
            }

            return res.addr;
        }

        public static CTypes.CData TryCheckCDataPointerType(object o, object type) {
            CTypes.CData res = o as CTypes.CData;
            object asParam;
            if (res == null && PythonOps.TryGetBoundAttr(o, "_as_parameter_", out asParam)) {
                res = asParam as CTypes.CData;
            }
            if (res != null && res.NativeType != ((CTypes.PointerType)type)._type) {
                if (!(res is CTypes.Pointer)) {
                    throw ArgumentError(type, ((PythonType)((CTypes.PointerType)type)._type).Name, o);
                }
                return null;
            }

            return res;
        }

        public static CTypes._Array TryCheckCharArray(object o) {
            CTypes._Array array = o as CTypes._Array;
            if (array != null) {
                CTypes.SimpleType elemType = ((CTypes.ArrayType)array.NativeType).ElementType as CTypes.SimpleType;
                if (elemType != null && elemType._type == CTypes.SimpleTypeKind.Char) {
                    return array;
                }
            }

            return null;
        }

        private static readonly byte[] FakeZeroLength = { 42 };

        public static byte[] TryCheckBytes(object o) {
            Bytes bytes = o as Bytes;
            if (bytes != null) {
                if (bytes._bytes.Length == 0) {
                    // OpCodes.Ldelema refuses to get address of empty array
                    // So we feed it with a fake one (cp34892)
                    return FakeZeroLength;
                }
                return bytes._bytes;
            }
            return null;
        }

        public static byte[] GetBytes(Bytes bytes) {
            return bytes._bytes;
        }

        public static CTypes._Array TryCheckWCharArray(object o) {
            CTypes._Array array = o as CTypes._Array;
            if (array != null) {
                CTypes.SimpleType elemType = ((CTypes.ArrayType)array.NativeType).ElementType as CTypes.SimpleType;
                if (elemType != null && elemType._type == CTypes.SimpleTypeKind.WChar) {
                    return array;
                }
            }

            return null;
        }

        public static object CreateSubclassInstance(object type, object instance) {
            return PythonCalls.Call(type, instance);
        }

        public static void CallbackException(Exception e, CodeContext/*!*/ context) {
            PythonContext pc = PythonContext.GetContext(context);
            object stderr = pc.SystemStandardError;
            PythonOps.PrintWithDest(context, stderr, pc.FormatException(e));
        }

        private static Exception ArgumentError(object type, string expected, object got) {
            PythonContext pc = ((PythonType)type).Context;
            return PythonExceptions.CreateThrowable(
                (PythonType)pc.GetModuleState("ArgumentError"),
                string.Format("expected {0}, got {1}", expected, DynamicHelpers.GetPythonType(got).Name)
            );
        }

        public static CTypes.CData CheckNativeArgument(object o, object type) {
            CTypes.NativeArgument arg = o as CTypes.NativeArgument;
            if (arg != null) {
                if (((CTypes.PointerType)type)._type != DynamicHelpers.GetPythonType(arg._obj)) {
                    throw ArgumentError(type, ((PythonType)type).Name, o);
                }
                return arg._obj;
            }

            return null;
        }

        public static string CharToString(byte c) {
            return new string((char)c, 1);
        }

        public static string WCharToString(char c) {
            return new string(c, 1);
        }

        public static char StringToChar(string s) {
            return s[0];
        }

        public static string EnsureString(object o) {
            string res = o as string;
            if (res == null) {
                throw PythonOps.TypeErrorForTypeMismatch("str", o);
            }

            return res;
        }

        public static bool CheckFunctionId(CTypes._CFuncPtr func, int id) {
            return func.Id == id;
        }

        public static IntPtr GetWCharPointer(object value) {
            string strVal = value as string;
            if (strVal != null) {
                return Marshal.StringToCoTaskMemUni(strVal);
            }


            if (value == null) {
                return IntPtr.Zero;
            }

            object asParam;
            if (PythonOps.TryGetBoundAttr(value, "_as_parameter_", out asParam)) {
                return GetWCharPointer(asParam);
            }

            throw PythonOps.TypeErrorForTypeMismatch("wchar pointer", value);
        }

        public static IntPtr GetBSTR(object value) {
            string strVal = value as string;
            if (strVal != null) {
                return Marshal.StringToBSTR(strVal);
            }


            if (value == null) {
                return IntPtr.Zero;
            }

            object asParam;
            if (PythonOps.TryGetBoundAttr(value, "_as_parameter_", out asParam)) {
                return GetBSTR(asParam);
            }

            throw PythonOps.TypeErrorForTypeMismatch("BSTR", value);
        }

        public static IntPtr GetCharPointer(object value) {
            string strVal = value as string;
            if (strVal != null) {
                return Marshal.StringToCoTaskMemAnsi(strVal);
            }

            if (value == null) {
                return IntPtr.Zero;
            }

            object asParam;
            if (PythonOps.TryGetBoundAttr(value, "_as_parameter_", out asParam)) {
                return GetCharPointer(asParam);
            }

            throw PythonOps.TypeErrorForTypeMismatch("char pointer", value);
        }

        public static IntPtr GetPointer(object value) {
            if (value is int) {
                int iVal = (int)value;
                if (iVal >= 0) {
                    return new IntPtr(iVal);
                }
            }

            if (value is BigInteger) {
                return new IntPtr((long)(BigInteger)value);
            }

            if (value is Int64) {
                return new IntPtr((Int64) value);
            }

            if (value == null) {
                return IntPtr.Zero;
            }

            object asParam;
            if (PythonOps.TryGetBoundAttr(value, "_as_parameter_", out asParam)) {
                return GetPointer(asParam);
            }

            CTypes.SimpleCData sd = value as CTypes.SimpleCData;
            if (sd != null) {
                CTypes.SimpleType simpType = (CTypes.SimpleType)sd.NativeType;
                if (simpType._type == CTypes.SimpleTypeKind.WCharPointer ||
                    simpType._type == CTypes.SimpleTypeKind.CharPointer) {
                    return sd.UnsafeAddress;
                } else if (simpType._type == CTypes.SimpleTypeKind.Pointer) {
                    return sd._memHolder.ReadIntPtr(0);
                }
            }

            CTypes._Array arr = value as CTypes._Array;
            if (arr != null) {
                return arr.UnsafeAddress;
            }

            CTypes._CFuncPtr func = value as CTypes._CFuncPtr;
            if (func != null) {
                return func.UnsafeAddress;
            }

            CTypes.Pointer pointer = value as CTypes.Pointer;
            if (pointer != null) {
                return pointer.UnsafeAddress;
            }

            throw PythonOps.TypeErrorForTypeMismatch("pointer", value);
        }

        public static IntPtr GetInterfacePointer(IntPtr self, int offset) {
            var vtable = Marshal.ReadIntPtr(self);
            return Marshal.ReadIntPtr(vtable, offset * IntPtr.Size);
        }

        public static IntPtr GetObject(object value) {
            GCHandle handle = GCHandle.Alloc(value);

            // TODO: Need to free the handle at some point
            return GCHandle.ToIntPtr(handle);
        }

        public static long GetSignedLongLong(object value, object type) {
            int? res = Converter.ImplicitConvertToInt32(value);
            if (res != null) {
                return res.Value;
            }

            if (value is BigInteger) {
                return (long)(BigInteger)value;
            }

            object asParam;
            if (PythonOps.TryGetBoundAttr(value, "_as_parameter_", out asParam)) {
                return GetSignedLongLong(asParam, type);
            }

            throw PythonOps.TypeErrorForTypeMismatch("signed long long ", value);
        }

        public static long GetUnsignedLongLong(object value, object type) {
            int? res = Converter.ImplicitConvertToInt32(value);
            if (res != null && res.Value >= 0) {
                return res.Value;
            }

            if (value is BigInteger) {
                return (long)(ulong)(BigInteger)value;
            }

            object asParam;
            if (PythonOps.TryGetBoundAttr(value, "_as_parameter_", out asParam)) {
                return GetUnsignedLongLong(asParam, type);
            }

            throw PythonOps.TypeErrorForTypeMismatch("unsigned long long", value);
        }

        public static double GetDouble(object value, object type) {
            if (value is double) {
                return (double)value;
            } else if (value is float) {
                return (float)value;
            } else if (value is int) {
                return (double)(int)value;
            } else if (value is BigInteger) {
                return (double)((BigInteger)value).ToFloat64();
            }

            object asParam;
            if (PythonOps.TryGetBoundAttr(value, "_as_parameter_", out asParam)) {
                return GetDouble(asParam, type);
            }

            return Converter.ConvertToDouble(value);
        }

        public static float GetSingle(object value, object type) {
            if (value is double) {
                return (float)(double)value;
            } else if (value is float) {
                return (float)value;
            } else if (value is int) {
                return (float)(int)value;
            } else if (value is BigInteger) {
                return (float)((BigInteger)value).ToFloat64();
            }

            object asParam;
            if (PythonOps.TryGetBoundAttr(value, "_as_parameter_", out asParam)) {
                return GetSingle(asParam, type);
            }

            return (float)Converter.ConvertToDouble(value);
        }

        public static long GetDoubleBits(object value) {
            if (value is double) {
                return BitConverter.ToInt64(BitConverter.GetBytes((double)value), 0);
            } else if (value is float) {
                return BitConverter.ToInt64(BitConverter.GetBytes((float)value), 0);
            } else if (value is int) {
                return BitConverter.ToInt64(BitConverter.GetBytes((double)(int)value), 0);
            } else if (value is BigInteger) {
                return BitConverter.ToInt64(BitConverter.GetBytes((double)((BigInteger)value).ToFloat64()), 0);
            }

            object asParam;
            if (PythonOps.TryGetBoundAttr(value, "_as_parameter_", out asParam)) {
                return GetDoubleBits(asParam);
            }

            return BitConverter.ToInt64(BitConverter.GetBytes(Converter.ConvertToDouble(value)), 0);
        }

        public static int GetSingleBits(object value) {
            if (value is double) {
                return BitConverter.ToInt32(BitConverter.GetBytes((float)(double)value), 0);
            } else if (value is float) {
                return BitConverter.ToInt32(BitConverter.GetBytes((float)value), 0);
            } else if (value is int) {
                return BitConverter.ToInt32(BitConverter.GetBytes((float)(int)value), 0);
            } else if (value is BigInteger) {
                return BitConverter.ToInt32(BitConverter.GetBytes((float)((BigInteger)value).ToFloat64()), 0);
            }

            object asParam;
            if (PythonOps.TryGetBoundAttr(value, "_as_parameter_", out asParam)) {
                return GetSingleBits(asParam);
            }

            return BitConverter.ToInt32(BitConverter.GetBytes((float)Converter.ConvertToDouble(value)), 0);
        }

        public static int GetSignedLong(object value, object type) {
            if (value is int) {
                return (int)value;
            }

            int? res = Converter.ImplicitConvertToInt32(value);
            if (res != null) {
                return res.Value;
            }

            uint unsigned;
            if (value is BigInteger && ((BigInteger)value).AsUInt32(out unsigned)) {
                return (int)unsigned;
            }

            object asParam;
            if (PythonOps.TryGetBoundAttr(value, "_as_parameter_", out asParam)) {
                return GetSignedLong(asParam, type);
            }

            throw PythonOps.TypeErrorForTypeMismatch("signed long", value);
        }

        public static int GetUnsignedLong(object value, object type) {
            int? res = Converter.ImplicitConvertToInt32(value);
            if (res != null) {
                return res.Value;
            }

            if (value is BigInteger) {
                uint ures;
                if (((BigInteger)value).AsUInt32(out ures)) {
                    return (int)ures;
                }
            }

            object asParam;
            if (PythonOps.TryGetBoundAttr(value, "_as_parameter_", out asParam)) {
                return GetUnsignedLong(asParam, type);
            }

            throw PythonOps.TypeErrorForTypeMismatch("unsigned long", value);
        }

        public static int GetUnsignedInt(object value, object type) {
            int? res = Converter.ImplicitConvertToInt32(value);
            if (res != null && res.Value >= 0) {
                return res.Value;
            }

            object asParam;
            if (PythonOps.TryGetBoundAttr(value, "_as_parameter_", out asParam)) {
                return GetUnsignedInt(type, asParam);
            }

            throw PythonOps.TypeErrorForTypeMismatch("unsigned int", value);
        }

        public static int GetSignedInt(object value, object type) {
            int? res = Converter.ImplicitConvertToInt32(value);
            if (res != null) {
                return res.Value;
            }
            object asParam;
            if (PythonOps.TryGetBoundAttr(value, "_as_parameter_", out asParam)) {
                return GetSignedInt(asParam, type);
            }

            throw PythonOps.TypeErrorForTypeMismatch("signed int", value);
        }

        public static short GetUnsignedShort(object value, object type) {
            int? res = Converter.ImplicitConvertToInt32(value);
            if (res != null) {
                int iVal = res.Value;
                if (iVal >= ushort.MinValue && iVal <= ushort.MaxValue) {
                    return (short)(ushort)iVal;
                }
            }
            object asParam;
            if (PythonOps.TryGetBoundAttr(value, "_as_parameter_", out asParam)) {
                return GetUnsignedShort(asParam, type);
            }

            throw PythonOps.TypeErrorForTypeMismatch("unsigned short", value);
        }

        public static short GetSignedShort(object value, object type) {
            int? res = Converter.ImplicitConvertToInt32(value);
            if (res != null) {
                int iVal = res.Value;
                return (short)iVal;
            } else if (value is BigInteger) {
                BigInteger bigInt = (BigInteger)value;
                return (short)(int)(bigInt & 0xffff);
            }

            object asParam;
            if (PythonOps.TryGetBoundAttr(value, "_as_parameter_", out asParam)) {
                return GetSignedShort(asParam, type);
            }

            throw PythonOps.TypeErrorForTypeMismatch("signed short", value);
        }

        public static int GetVariantBool(object value, object type) {
            return Converter.ConvertToBoolean(value) ? 1 : 0;
        }

        public static byte GetUnsignedByte(object value, object type) {
            int? res = Converter.ImplicitConvertToInt32(value);
            if (res != null) {
                return (byte)res.Value;
            }

            object asParam;
            if (PythonOps.TryGetBoundAttr(value, "_as_parameter_", out asParam)) {
                return GetUnsignedByte(asParam, type);
            }

            throw PythonOps.TypeErrorForTypeMismatch("unsigned byte", value);
        }

        public static byte GetSignedByte(object value, object type) {
            int? res = Converter.ImplicitConvertToInt32(value);
            if (res != null) {
                int iVal = res.Value;
                if (iVal >= sbyte.MinValue && iVal <= sbyte.MaxValue) {
                    return (byte)(sbyte)iVal;
                }
            }
            object asParam;
            if (PythonOps.TryGetBoundAttr(value, "_as_parameter_", out asParam)) {
                return GetSignedByte(asParam, type);
            }

            throw PythonOps.TypeErrorForTypeMismatch("signed byte", value);
        }


        public static byte GetBoolean(object value, object type) {
            if (value is bool) {
                return ((bool)value) ? (byte)1 : (byte)0;
            } else if (value is int) {
                return ((int)value) != 0 ? (byte)1 : (byte)0;
            }

            object asParam;
            if (PythonOps.TryGetBoundAttr(value, "_as_parameter_", out asParam)) {
                return GetBoolean(asParam, type);
            }

            throw PythonOps.TypeErrorForTypeMismatch("bool", value);
        }

        public static byte GetChar(object value, object type) {
            string strVal = value as string;
            if (strVal != null && strVal.Length == 1) {
                return (byte)strVal[0];
            }

            object asParam;
            if (PythonOps.TryGetBoundAttr(value, "_as_parameter_", out asParam)) {
                return GetChar(asParam, type);
            }

            throw ArgumentError(type, "char", value);
        }

        public static char GetWChar(object value, object type) {
            string strVal = value as string;
            if (strVal != null && strVal.Length == 1) {
                return strVal[0];
            }

            object asParam;
            if (PythonOps.TryGetBoundAttr(value, "_as_parameter_", out asParam)) {
                return GetWChar(asParam, type);
            }

            throw ArgumentError(type, "wchar", value);
        }

        public static object IntPtrToObject(IntPtr address) {
            GCHandle handle = GCHandle.FromIntPtr(address);

            object res = handle.Target;
            handle.Free();
            return res;
        }

    }
}
#endif