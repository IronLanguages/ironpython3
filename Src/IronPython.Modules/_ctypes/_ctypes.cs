// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_CTYPES

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
using System.Threading;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

[assembly: PythonModule("_ctypes", typeof(IronPython.Modules.CTypes))]
namespace IronPython.Modules {
    /// <summary>
    /// Provides support for interop with native code from Python code.
    /// </summary>
    public static partial class CTypes {
        private static readonly object _lock = new object();                              // lock for creating dynamic module for unsafe code
        private static readonly object _pointerTypeCacheKey = new object();               // key for system state for the pointer type cache
        private static readonly object _conversion_mode = new object();                   // key for system state current conversion mode
        private static Dictionary<object, RefCountInfo> _refCountTable;                   // dictionary used to maintain a ref count on objects
        private static ModuleBuilder _dynamicModule;                                      // the dynamic module we generate unsafe code into
        private static Dictionary<int, Type> _nativeTypes = new Dictionary<int, Type>();  // native types of the specified size for marshalling
        private static StringAtDelegate _stringAt = StringAt, _wstringAt = WStringAt;     // delegates for wchar/char functions we hand addresses out to (just keeping it alive)
        private static CastDelegate _cast = Cast;                                         // delegate for cast function whose address we hand out (just keeping it alive)

        public const string __version__ = "1.1.0";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr CastDelegate(IntPtr data, IntPtr obj, IntPtr type);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr StringAtDelegate(IntPtr addr, int length);

        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, PythonDictionary/*!*/ dict) {

            context.EnsureModuleException("ArgumentError", dict, "ArgumentError", "_ctypes");

            // TODO: Provide an implementation which is coordinated with our _refCountTable
            // context.SystemState.__dict__["getrefcount"] = null;
            PythonDictionary pointerTypeCache = new PythonDictionary();
            dict["_pointer_type_cache"] = pointerTypeCache;
            context.SetModuleState(_pointerTypeCacheKey, pointerTypeCache);

            if (Environment.OSVersion.Platform == PlatformID.Win32NT ||
                Environment.OSVersion.Platform == PlatformID.Win32S ||
                Environment.OSVersion.Platform == PlatformID.Win32Windows ||
                Environment.OSVersion.Platform == PlatformID.WinCE) {
                context.EnsureModuleException(
                    "COMError",
                    PythonExceptions.Exception,
                    typeof(_COMError),
                    dict,
                    "COMError",
                    "_ctypes",
                    "Raised when a COM method call failed.",
                    (msg, _) => new COMException(msg)
                );

                context.SetModuleState(_conversion_mode, PythonTuple.MakeTuple("mbcs", "ignore"));
            } else {
                context.SetModuleState(_conversion_mode, PythonTuple.MakeTuple("ascii", "strict"));
            }
        }

        #region Public Functions

        /// <summary>
        /// Gets a function which casts the specified memory.  Because this is used only
        /// w/ Python API we use a delegate as the return type instead of an actual address.
        /// </summary>
        public static object _cast_addr {
            get {
                return Marshal.GetFunctionPointerForDelegate(_cast).ToPython();
            }
        }

        /// <summary>
        /// Implementation of our cast function.  data is marshalled as a void*
        /// so it ends up as an address.  obj and type are marshalled as an object
        /// so we need to unmarshal them.
        /// </summary>
        private static IntPtr Cast(IntPtr data, IntPtr obj, IntPtr type) {
            GCHandle objHandle = GCHandle.FromIntPtr(obj);
            GCHandle typeHandle = GCHandle.FromIntPtr(type);
            try {
                CData cdata = objHandle.Target as CData;
                PythonType pt = (PythonType)typeHandle.Target;
                
                CData res = (CData)pt.CreateInstance(pt.Context.SharedContext);
                if (IsPointer(pt)) {
                    res._memHolder = new MemoryHolder(IntPtr.Size);
                    if (IsPointer(DynamicHelpers.GetPythonType(cdata))) {
                        res._memHolder.WriteIntPtr(0, cdata._memHolder.ReadIntPtr(0));
                    } else {
                        res._memHolder.WriteIntPtr(0, data);
                    }

                    if (cdata != null) {
                        res._memHolder.Objects = cdata._memHolder.Objects;
                        res._memHolder.AddObject(IdDispenser.GetId(cdata), cdata);
                    }
                } else {
                    if (cdata != null) {
                        res._memHolder = new MemoryHolder(data, ((INativeType)pt).Size, cdata._memHolder);
                    } else {
                        res._memHolder = new MemoryHolder(data, ((INativeType)pt).Size);
                    }
                }

                return GCHandle.ToIntPtr(GCHandle.Alloc(res));
            } finally {
                typeHandle.Free();
                objHandle.Free();
            }
        }

        private static bool IsPointer(PythonType pt) {
            SimpleType simpleType;
            return pt is PointerType || ((simpleType = pt as SimpleType) != null && (simpleType._type == SimpleTypeKind.Pointer || simpleType._type == SimpleTypeKind.CharPointer || simpleType._type == SimpleTypeKind.WCharPointer));
        }

        public static object _memmove_addr {
            get {
                return NativeFunctions.GetMemMoveAddress().ToPython();
            }
        }

        public static object _memset_addr {
            get {
                return NativeFunctions.GetMemSetAddress().ToPython();
            }
        }

        public static object _string_at_addr {
            get {
                return Marshal.GetFunctionPointerForDelegate(_stringAt).ToPython();
            }
        }

        public static object _wstring_at_addr {
            get {
                return Marshal.GetFunctionPointerForDelegate(_wstringAt).ToPython();
            }
        }

        public static int CopyComPointer(object src, object dest) {
            throw new NotImplementedException("CopyComPointer");
        }

        public static string FormatError() {
            return FormatError(get_last_error());
        }

        public static string FormatError(int errorCode) {
            return new Win32Exception(errorCode).Message;
        }

        [PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        public static void FreeLibrary(int handle) {
            FreeLibrary(new IntPtr(handle));
        }

        [PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        public static void FreeLibrary(BigInteger handle) {
            FreeLibrary(new IntPtr((long)handle));
        }

        [PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        public static void FreeLibrary(IntPtr handle) {
            NativeFunctions.FreeLibrary(handle);
        }

        [PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        public static object LoadLibrary(string library, int mode = 0) {
            IntPtr res = NativeFunctions.LoadDLL(library, mode);
            if (res == IntPtr.Zero) {
                throw PythonOps.OSError($"cannot load library {library}");
            }

            return res.ToPython();
        }

        // Provided for Posix compat.
        public static object dlopen(string library, int mode = 0) {
            return LoadLibrary(library, mode);
        }

        /// <summary>
        /// Returns a new type which represents a pointer given the existing type.
        /// </summary>
        public static PythonType POINTER(CodeContext/*!*/ context, PythonType type) {
            PythonContext pc = context.LanguageContext;
            PythonDictionary dict = (PythonDictionary)pc.GetModuleState(_pointerTypeCacheKey);

            lock (dict) {
                if (!dict.TryGetValue(type, out object res)) {
                    string name;
                    if (type == null) {
                        name = "c_void_p";
                    } else {
                        name = "LP_" + type.Name;
                    }

                    dict[type] = res = MakePointer(context, name, PythonOps.MakeDictFromItems(new object[] { type, "_type_" }));
                }

                return res as PythonType;
            }
        }

        private static PointerType MakePointer(CodeContext context, string name, PythonDictionary dict) {
            return new PointerType(context,
               name,
               PythonTuple.MakeTuple(_Pointer),
               dict
           );
        }

        public static PythonType POINTER(CodeContext/*!*/ context, [NotNull]string name) {
            PythonType res = MakePointer(context, name, new PythonDictionary());
            PythonContext pc = context.LanguageContext;
            PythonDictionary dict = (PythonDictionary)pc.GetModuleState(_pointerTypeCacheKey);

            lock (dict) {
                dict[Builtin.id(res)] = res;
            }

            return res;
        }

        /// <summary>
        /// Converts an address acquired from PyObj_FromPtr or that has been
        /// marshaled as type 'O' back into an object.
        /// </summary>
        public static object PyObj_FromPtr(IntPtr address) {
            GCHandle handle = GCHandle.FromIntPtr(address);
            object res = handle.Target;

            handle.Free();
            return res;
        }

        /// <summary>
        /// Converts an object into an opaque address which can be handed out to
        /// managed code.
        /// </summary>
        public static IntPtr PyObj_ToPtr(object obj) {
            return GCHandle.ToIntPtr(GCHandle.Alloc(obj));
        }

        /// <summary>
        /// Decreases the ref count on an object which has been increased with
        /// Py_INCREF.
        /// </summary>
        public static void Py_DECREF(object key) {
            EnsureRefCountTable();

            lock (_refCountTable) {
                if (!_refCountTable.TryGetValue(key, out RefCountInfo info)) {
                    // dec without an inc
                    throw new InvalidOperationException();
                }

                info.RefCount--;
                if (info.RefCount == 0) {
                    info.Handle.Free();
                    _refCountTable.Remove(key);
                }
            }
        }

        /// <summary>
        /// Increases the ref count on an object ensuring that it will not be collected.
        /// </summary>
        public static void Py_INCREF(object key) {
            EnsureRefCountTable();

            lock (_refCountTable) {
                if (!_refCountTable.TryGetValue(key, out RefCountInfo info)) {
                    _refCountTable[key] = info = new RefCountInfo();
                    // TODO: this only works w/ blittable types, what to do for others?
                    info.Handle = GCHandle.Alloc(key, GCHandleType.Pinned);
                }

                info.RefCount++;
            }
        }

        // for testing purposes only
        public static PythonTuple _buffer_info(CData data) {
            return data.GetBufferInfo();
        }

        public static void _check_HRESULT(int hresult) {
            if (hresult < 0) {
                throw PythonOps.WindowsError("ctypes function returned failed HRESULT: {0:x}", (uint)hresult);
            }
        }

        public static void _unpickle() {
        }

        /// <summary>
        /// returns address of C instance internal buffer.
        /// 
        /// It is the callers responsibility to ensure that the provided instance will 
        /// stay alive if memory in the resulting address is to be used later.
        /// </summary>
        public static object addressof(CData data) {
            return data._memHolder.UnsafeAddress.ToPython();
        }

        /// <summary>
        /// Gets the required alignment of the given type.
        /// </summary>
        public static int alignment(PythonType type) {
            if (!(type is INativeType nativeType)) {
                throw PythonOps.TypeError("this type has no size");
            }

            return nativeType.Alignment;
        }

        /// <summary>
        /// Gets the required alignment of an object.
        /// </summary>
        public static int alignment(object o) {
            return alignment(DynamicHelpers.GetPythonType(o));
        }

        public static object byref(CData instance, int offset=0) {
            if (offset != 0) {
                // new in 2.6
                throw new NotImplementedException("byref w/ arg");
            }

            return new NativeArgument(instance, "P");
        }

        public static object call_cdeclfunction(CodeContext context, int address, PythonTuple args) {
            return call_cdeclfunction(context, new IntPtr(address), args);
        }

        public static object call_cdeclfunction(CodeContext context, BigInteger address, PythonTuple args) {
            return call_cdeclfunction(context, new IntPtr((long)address), args);
        }

        public static object call_cdeclfunction(CodeContext context, IntPtr address, PythonTuple args) {
            CFuncPtrType funcType = GetFunctionType(context, FUNCFLAG_CDECL);

            _CFuncPtr func = (_CFuncPtr)funcType.CreateInstance(context, address);

            return PythonOps.CallWithArgsTuple(func, new object[0], args);
        }

        public static void call_commethod() {
        }

        public static object call_function(CodeContext context, int address, PythonTuple args) {
            return call_function(context, new IntPtr(address), args);
        }

        public static object call_function(CodeContext context, BigInteger address, PythonTuple args) {
            return call_function(context, new IntPtr((long)address), args);
        }

        public static object call_function(CodeContext context, IntPtr address, PythonTuple args) {
            CFuncPtrType funcType = GetFunctionType(context, FUNCFLAG_STDCALL);
            
            _CFuncPtr func = (_CFuncPtr)funcType.CreateInstance(context, address);

            return PythonOps.CallWithArgsTuple(func, new object[0], args);
        }

        private static CFuncPtrType GetFunctionType(CodeContext context, int flags) {
            // Ideally we should cache these...
            SimpleType resType = new SimpleType(
                context,
                "int",
                PythonTuple.MakeTuple(DynamicHelpers.GetPythonTypeFromType(typeof(SimpleCData))), PythonOps.MakeHomogeneousDictFromItems(new object[] { "i", "_type_" }));

            CFuncPtrType funcType = new CFuncPtrType(
                context,
                "func",
                PythonTuple.MakeTuple(DynamicHelpers.GetPythonTypeFromType(typeof(_CFuncPtr))),
                PythonOps.MakeHomogeneousDictFromItems(new object[] { FUNCFLAG_STDCALL, "_flags_", resType, "_restype_" }));
            return funcType;
        }

        public static int get_errno() {
            return 0;
        }

        public static int get_last_error() {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
                return NativeFunctions.GetLastError();
            }

            throw PythonOps.NameError("get_last_error");
        }

        /// <summary>
        /// Returns a pointer instance for the given CData
        /// </summary>
        public static Pointer pointer(CodeContext/*!*/ context, CData data) {
            PythonType ptrType = POINTER(context, DynamicHelpers.GetPythonType(data));

            return (Pointer)ptrType.CreateInstance(context, data);
        }

        public static void resize(CData obj, int newSize) {
            if (newSize < obj.NativeType.Size) {
                throw PythonOps.ValueError("minimum size is {0}", newSize);
            }

            MemoryHolder newMem = new MemoryHolder(newSize);
            obj._memHolder.CopyTo(newMem, 0, Math.Min(obj._memHolder.Size, newSize));
            obj._memHolder = newMem;
        }

        public static PythonTuple/*!*/ set_conversion_mode(CodeContext/*!*/ context, string encoding, string errors) {
            // TODO: Need an atomic update for module state
            PythonContext pc = context.LanguageContext;
            PythonTuple prev = (PythonTuple)pc.GetModuleState(_conversion_mode);

            pc.SetModuleState(_conversion_mode, PythonTuple.MakeTuple(encoding, errors));

            return prev;
        }

        public static void set_errno() {
            // we can't support this without a native library
        }

        [PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        public static int set_last_error(int errorCode) {
            int old_errno = NativeFunctions.GetLastError();
            NativeFunctions.SetLastError(errorCode);
            return old_errno;
        }

        public static int @sizeof(PythonType/*!*/ type) {
            if (!(type is INativeType simpleType)) {
                throw PythonOps.TypeError("this type has no size");
            }

            return simpleType.Size;
        }

        public static int @sizeof(object/*!*/ instance) {
            if (instance is CData cdata && cdata._memHolder != null) {
                return cdata._memHolder.Size;
            }
            return @sizeof(DynamicHelpers.GetPythonType(instance));
        }

        #endregion

        #region Public Constants

        public const int FUNCFLAG_STDCALL = 0;
        public const int FUNCFLAG_CDECL = 1;
        public const int FUNCFLAG_HRESULT = 2;
        public const int FUNCFLAG_PYTHONAPI = 4;

        public const int FUNCFLAG_USE_ERRNO = 8;
        public const int FUNCFLAG_USE_LASTERROR = 16;

        public const int RTLD_GLOBAL = 0;
        public const int RTLD_LOCAL = 0;

        #endregion

        #region Implementation Details

        /// <summary>
        /// Gets the ModuleBuilder used to generate our unsafe call stubs into.
        /// </summary>
        private static ModuleBuilder DynamicModule {
            get {
                if (_dynamicModule == null) {
                    lock (_lock) {
                        if (_dynamicModule == null) {
                            var attributes = new[] { 
                                new CustomAttributeBuilder(typeof(UnverifiableCodeAttribute).GetConstructor(ReflectionUtils.EmptyTypes), new object[0]),
#if !NETCOREAPP2_1 && !NETSTANDARD2_0
                                //PermissionSet(SecurityAction.Demand, Unrestricted = true)
                                new CustomAttributeBuilder(typeof(PermissionSetAttribute).GetConstructor(new Type[] { typeof(SecurityAction) }), 
                                    new object[]{ SecurityAction.Demand },
                                    new PropertyInfo[] { typeof(PermissionSetAttribute).GetProperty(nameof(PermissionSetAttribute.Unrestricted)) },
                                    new object[] { true }
                                )
#endif
                            };

                            string name = typeof(CTypes).Namespace + ".DynamicAssembly";
#if NETCOREAPP2_1 || NETSTANDARD2_0
                            var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run, attributes);
#else
                            var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run, attributes);
                            assembly.DefineVersionInfoResource();
#endif
                            _dynamicModule = assembly.DefineDynamicModule(name);
                        }
                    }
                }

                return _dynamicModule;
            }
        }

        /// <summary>
        /// Given a specific size returns a .NET type of the equivalent size that
        /// we can use when marshalling these values across calls.
        /// </summary>
        private static Type/*!*/ GetMarshalTypeFromSize(int size) {
            lock (_nativeTypes) {
                if (!_nativeTypes.TryGetValue(size, out Type res)) {
                    int sizeRemaining = size;
                    TypeBuilder tb = DynamicModule.DefineType("interop_type_size_" + size,
                        TypeAttributes.Public | TypeAttributes.SequentialLayout | TypeAttributes.Sealed | TypeAttributes.Serializable,
                        typeof(ValueType),
                        size);

                    while (sizeRemaining > 8) {
                        tb.DefineField("field" + sizeRemaining, typeof(long), FieldAttributes.Private);
                        sizeRemaining -= 8;
                    }

                    while (sizeRemaining > 4) {
                        tb.DefineField("field" + sizeRemaining, typeof(int), FieldAttributes.Private);
                        sizeRemaining -= 4;
                    }

                    while (sizeRemaining > 0) {
                        tb.DefineField("field" + sizeRemaining, typeof(byte), FieldAttributes.Private);
                        sizeRemaining--;
                    }

                    _nativeTypes[size] = res = tb.CreateTypeInfo();
                }

                return res;
            }
        }

        /// <summary>
        /// Shared helper between struct and union for getting field info and validating it.
        /// </summary>
        private static void GetFieldInfo(INativeType type, object o, out string fieldName, out INativeType cdata, out int? bitCount) {
            PythonTuple pt = o as PythonTuple;
            if (pt.Count != 2 && pt.Count != 3) {
                throw PythonOps.AttributeError("'_fields_' must be a sequence of pairs");
            }

            fieldName = pt[0] as string;
            if (fieldName == null) {
                throw PythonOps.TypeError("first item in _fields_ tuple must be a string, got", PythonTypeOps.GetName(pt[0]));
            }

            cdata = pt[1] as INativeType;
            if (cdata == null) {
                throw PythonOps.TypeError("second item in _fields_ tuple must be a C type, got {0}", PythonTypeOps.GetName(pt[0]));
            } else if (cdata == type) {
                throw StructureCannotContainSelf();
            }

            if (cdata is StructType st) {
                st.EnsureFinal();
            }

            if (pt.Count != 3) {
                bitCount = null;
            } else {
                bitCount = CheckBits(cdata, pt);
            }
        }

        /// <summary>
        /// Verifies that the provided bit field settings are valid for this type.
        /// </summary>
        private static int CheckBits(INativeType cdata, PythonTuple pt) {
            int bitCount = Converter.ConvertToInt32(pt[2]);

            if (!(cdata is SimpleType simpType)) {
                throw PythonOps.TypeError("bit fields not allowed for type {0}", ((PythonType)cdata).Name);
            }

            switch (simpType._type) {
                case SimpleTypeKind.Object:
                case SimpleTypeKind.Pointer:
                case SimpleTypeKind.Single:
                case SimpleTypeKind.Double:
                case SimpleTypeKind.Char:
                case SimpleTypeKind.CharPointer:
                case SimpleTypeKind.WChar:
                case SimpleTypeKind.WCharPointer:
                    throw PythonOps.TypeError("bit fields not allowed for type {0}", ((PythonType)cdata).Name);
            }

            if (bitCount <= 0 || bitCount > cdata.Size * 8) {
                throw PythonOps.ValueError("number of bits invalid for bit field");
            }
            return bitCount;
        }

        /// <summary>
        /// Shared helper to get the _fields_ list for struct/union and validate it.
        /// </summary>
        private static IList<object>/*!*/ GetFieldsList(object fields) {
            if (!(fields is IList<object> list)) {
                throw PythonOps.TypeError("class must be a sequence of pairs");
            }
            return list;
        }

        private static Exception StructureCannotContainSelf() {
            return PythonOps.AttributeError("Structure or union cannot contain itself");
        }

        /// <summary>
        /// Helper function for translating from memset to NT's FillMemory API.
        /// </summary>
        private static IntPtr StringAt(IntPtr src, int len) {
            Bytes res;
            if (len == -1) {
                res = MemoryHolder.ReadBytes(src, 0);
            } else {
                res = MemoryHolder.ReadBytes(src, 0, len);
            }

            return GCHandle.ToIntPtr(GCHandle.Alloc(res));
        }

        /// <summary>
        /// Helper function for translating from memset to NT's FillMemory API.
        /// </summary>
        private static IntPtr WStringAt(IntPtr src, int len) {
            string res;
            if (len == -1) {
                res = Marshal.PtrToStringUni(src);
            } else {
                res = Marshal.PtrToStringUni(src, len);
            }

            return GCHandle.ToIntPtr(GCHandle.Alloc(res));
        }

        private static IntPtr GetHandleFromObject(object dll, string errorMsg) {
            IntPtr intPtrHandle;
            object dllHandle = PythonOps.GetBoundAttr(DefaultContext.Default, dll, "_handle");

            if (!Converter.TryConvertToBigInteger(dllHandle, out BigInteger intHandle)) {
                throw PythonOps.TypeError(errorMsg);
            }
            intPtrHandle = new IntPtr((long)intHandle);
            return intPtrHandle;
        }

        private static void ValidateArraySizes(ArrayModule.array array, int offset, int size) {
            ValidateArraySizes(array.__len__() * array.itemsize, offset, size);
        }

        private static void ValidateArraySizes(Bytes bytes, int offset, int size) {
            ValidateArraySizes(bytes.Count, offset, size);
        }

        private static void ValidateArraySizes(string data, int offset, int size) {
            ValidateArraySizes(data.Length, offset, size);
        }

        private static void ValidateArraySizes(int arraySize, int offset, int size) {
            if (offset < 0) {
                throw PythonOps.ValueError("offset cannot be negative");
            } else if (arraySize < size + offset) {
                throw PythonOps.ValueError($"Buffer size too small ({arraySize} instead of at least {size} bytes)");
            }
        }

        private static void ValidateArraySizes(BigInteger arraySize, int offset, int size) {
            if (offset < 0) {
                throw PythonOps.ValueError("offset cannot be negative");
            } else if (arraySize < size + offset) {
                throw PythonOps.ValueError($"Buffer size too small ({arraySize} instead of at least {size} bytes)");
            }
        }

        // TODO: Move these to an Ops class
        public static object GetCharArrayValue(_Array arr) {
            return arr.NativeType.GetValue(arr._memHolder, arr, 0, false);
        }

        public static void SetCharArrayValue(_Array arr, object value) {
            arr.NativeType.SetValue(arr._memHolder, 0, value);
        }

        public static void DeleteCharArrayValue(_Array arr, object value) {
            throw PythonOps.TypeError("cannot delete char array value");
        }

        public static object GetWCharArrayValue(_Array arr) {
            return arr.NativeType.GetValue(arr._memHolder, arr, 0, false);
        }

        public static void SetWCharArrayValue(_Array arr, object value) {
            arr.NativeType.SetValue(arr._memHolder, 0, value);
        }

        public static object DeleteWCharArrayValue(_Array arr) {
            throw PythonOps.TypeError("cannot delete wchar array value");
        }

        public static object GetWCharArrayRaw(_Array arr) {
            return ((ArrayType)arr.NativeType).GetRawValue(arr._memHolder, 0);
        }

        public static void SetWCharArrayRaw(_Array arr, object value) {
            MemoryView view = value as MemoryView;
            if ((object)view != null) {
                string strVal = view.tobytes().MakeString();
                if (strVal.Length > arr.__len__()) {
                    throw PythonOps.ValueError("string too long");
                }
                value = strVal;
            }

            arr.NativeType.SetValue(arr._memHolder, 0, value);
        }

        public static object DeleteWCharArrayRaw(_Array arr) {
            throw PythonOps.TypeError("cannot delete wchar array raw");
        }
        
        class RefCountInfo {
            public int RefCount;
            public GCHandle Handle;
        }        

        /// <summary>
        /// Emits the marshalling code to create a CData object for reverse marshalling.
        /// </summary>
        private static void EmitCDataCreation(INativeType type, ILGenerator method, List<object> constantPool, int constantPoolArgument) {
            LocalBuilder locVal = method.DeclareLocal(type.GetNativeType());
            method.Emit(OpCodes.Stloc, locVal);
            method.Emit(OpCodes.Ldloca, locVal);

            constantPool.Add(type);
            method.Emit(OpCodes.Ldarg, constantPoolArgument);
            method.Emit(OpCodes.Ldc_I4, constantPool.Count - 1);
            method.Emit(OpCodes.Ldelem_Ref);

            method.Emit(OpCodes.Call, typeof(ModuleOps).GetMethod("CreateCData"));
        }
      
        private static void EnsureRefCountTable() {
            if (_refCountTable == null) {
                Interlocked.CompareExchange(ref _refCountTable, new Dictionary<object, RefCountInfo>(), null);
            }
        }

        #endregion

        [PythonHidden, PythonType("COMError"), DynamicBaseType]
        public class _COMError : PythonExceptions.BaseException {

            public _COMError(PythonType cls) : base(cls) { }

            public override void __init__(params object[] args) {
                base.__init__(args);
                if(args.Length < 3) {
                    throw PythonOps.TypeError($"COMError() takes exactly 4 arguments({args.Length} given)");
                }

                hresult = args[0];
                text = args[1];
                details = args[2];
            }

            public object hresult { get; set; }

            public object text { get; set; }

            public object details { get; set; }
        }

    }
}

#endif
