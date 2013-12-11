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
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

#if CLR2
using Microsoft.Scripting.Math;
#else
using System.Numerics;
#endif

namespace IronPython.Modules {
    /// <summary>
    /// Provides support for interop with native code from Python code.
    /// </summary>
    public static partial class CTypes {
        /// <summary>
        /// The meta class for ctypes function pointer instances.
        /// </summary>
        [PythonType, PythonHidden]
        public class CFuncPtrType : PythonType, INativeType {
            internal readonly int _flags;
            internal readonly PythonType _restype;
            internal readonly INativeType[] _argtypes;
            private DynamicMethod _reverseDelegate;         // reverse delegates are lazily computed the 1st time a callable is turned into a func ptr
            private List<object> _reverseDelegateConstants;
            private Type _reverseDelegateType;
            private static Dictionary<DelegateCacheKey, Type> _reverseDelegates = new Dictionary<DelegateCacheKey, Type>();

            //from_buffer_copy,  from_param, from_address, from_buffer, __doc__ __mul__ __rmul__ in_dll __new__ 
            public CFuncPtrType(CodeContext/*!*/ context, string name, PythonTuple bases, PythonDictionary members)
                : base(context, name, bases, members) {

                object flags;
                if (!members.TryGetValue("_flags_", out flags) || !(flags is int)) {
                    throw PythonOps.TypeError("class must define _flags_ which must be an integer");
                }
                _flags = (int)flags;

                object restype;
                if (members.TryGetValue("_restype_", out restype) && (restype is PythonType)) {
                    _restype = (PythonType)restype;
                }

                object argtypes;
                if (members.TryGetValue("_argtypes_", out argtypes) && (argtypes is PythonTuple)) {
                    PythonTuple pt = argtypes as PythonTuple;
                    _argtypes = new INativeType[pt.Count];
                    for (int i = 0; i < pt.Count; i++) {
                        _argtypes[i] = (INativeType)pt[i];
                    }
                }
            }

            private CFuncPtrType(Type underlyingSystemType)
                : base(underlyingSystemType) {
            }

            internal static PythonType MakeSystemType(Type underlyingSystemType) {
                return PythonType.SetPythonType(underlyingSystemType, new CFuncPtrType(underlyingSystemType));
            }

            /// <summary>
            /// Converts an object into a function call parameter.
            /// </summary>
            public object from_param(object obj) {
                return null;
            }

            // TODO: Move to Ops class
            public object internal_restype {
                get {
                    return _restype;
                }
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
                IntPtr funcAddr = owner.ReadIntPtr(offset);
                if (raw) {
                    return funcAddr.ToPython();
                }

                return CreateInstance(Context.SharedContext, funcAddr);
            }

            object INativeType.SetValue(MemoryHolder address, int offset, object value) {
                if (value is int) {
                    address.WriteIntPtr(offset, new IntPtr((int)value));
                } else if (value is BigInteger) {
                    address.WriteIntPtr(offset, new IntPtr((long)(BigInteger)value));
                } else if (value is _CFuncPtr) {
                    address.WriteIntPtr(offset, ((_CFuncPtr)value).addr);
                    return value;
                } else {
                    throw PythonOps.TypeErrorForTypeMismatch("func pointer", value);
                }
                return null;
            }

            Type INativeType.GetNativeType() {
                return typeof(IntPtr);
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
                method.Emit(OpCodes.Call, typeof(ModuleOps).GetMethod("GetFunctionPointerValue"));
                return null;
            }

            Type/*!*/ INativeType.GetPythonType() {
                return typeof(_CFuncPtr);
            }

            void INativeType.EmitReverseMarshalling(ILGenerator method, LocalOrArg value, List<object> constantPool, int constantPoolArgument) {
                value.Emit(method);
                constantPool.Add(this);
                method.Emit(OpCodes.Ldarg, constantPoolArgument);
                method.Emit(OpCodes.Ldc_I4, constantPool.Count - 1);
                method.Emit(OpCodes.Ldelem_Ref);

                method.Emit(OpCodes.Call, typeof(ModuleOps).GetMethod("CreateCFunction"));
            }

            string INativeType.TypeFormat {
                get {
                    return "X{}";
                }
            }

            #endregion

            internal CallingConvention CallingConvention {
                get {
                    switch (_flags & 0x07) {
                        case FUNCFLAG_STDCALL: return CallingConvention.StdCall;
                        case FUNCFLAG_CDECL: return CallingConvention.Cdecl;
                        case FUNCFLAG_HRESULT:
                        case FUNCFLAG_PYTHONAPI:
                            break;
                    }
                    return CallingConvention.Cdecl;
                }
            }

            internal Delegate MakeReverseDelegate(CodeContext/*!*/ context, object target) {
                if (_reverseDelegate == null) {
                    lock (this) {
                        if (_reverseDelegate == null) {
                            MakeReverseDelegateWorker(context);
                        }
                    }
                }

                object[] constantPool = _reverseDelegateConstants.ToArray();
                constantPool[0] = target;
                return _reverseDelegate.CreateDelegate(_reverseDelegateType, constantPool);
            }

           
            private void MakeReverseDelegateWorker(CodeContext context) {
                Type[] sigTypes;
                Type[] callSiteType;
                Type retType;
                GetSignatureInfo(out sigTypes, out callSiteType, out retType);

                DynamicMethod dm = new DynamicMethod("ReverseInteropInvoker", retType, ArrayUtils.RemoveLast(sigTypes), DynamicModule);
                ILGenerator ilGen = dm.GetILGenerator();
                PythonContext pc = PythonContext.GetContext(context);

                Type callDelegateSiteType = CompilerHelpers.MakeCallSiteDelegateType(callSiteType);
                CallSite site = CallSite.Create(callDelegateSiteType, pc.Invoke(new CallSignature(_argtypes.Length)));

                List<object> constantPool = new List<object>();
                constantPool.Add(null); // 1st item is the target object, will be put in later.
                constantPool.Add(site);

                ilGen.BeginExceptionBlock();

                //CallSite<Func<CallSite, object, object>> mySite;
                //mySite.Target(mySite, target, ...);

                LocalBuilder siteLocal = ilGen.DeclareLocal(site.GetType());
                ilGen.Emit(OpCodes.Ldarg_0);
                ilGen.Emit(OpCodes.Ldc_I4, constantPool.Count - 1);
                ilGen.Emit(OpCodes.Ldelem_Ref);
                ilGen.Emit(OpCodes.Castclass, site.GetType());
                ilGen.Emit(OpCodes.Stloc, siteLocal);
                ilGen.Emit(OpCodes.Ldloc, siteLocal);
                ilGen.Emit(OpCodes.Ldfld, site.GetType().GetField("Target"));
                ilGen.Emit(OpCodes.Ldloc, siteLocal);

                // load code context
                int contextIndex = constantPool.Count;
                Debug.Assert(pc.SharedContext != null);
                constantPool.Add(pc.SharedContext);                
                ilGen.Emit(OpCodes.Ldarg_0);
                ilGen.Emit(OpCodes.Ldc_I4, contextIndex);
                ilGen.Emit(OpCodes.Ldelem_Ref);

                // load function target, in constant pool slot 0
                ilGen.Emit(OpCodes.Ldarg_0);
                ilGen.Emit(OpCodes.Ldc_I4_0);
                ilGen.Emit(OpCodes.Ldelem_Ref);

                // load arguments
                for (int i = 0; i < _argtypes.Length; i++) {
                    INativeType nativeType = _argtypes[i];
                    nativeType.EmitReverseMarshalling(ilGen, new Arg(i + 1, sigTypes[i + 1]), constantPool, 0);
                }

                ilGen.Emit(OpCodes.Call, callDelegateSiteType.GetMethod("Invoke"));

                LocalBuilder finalRes = null;
                // emit forward marshaling for return value
                if (_restype != null) {
                    LocalBuilder tmpRes = ilGen.DeclareLocal(typeof(object));
                    ilGen.Emit(OpCodes.Stloc, tmpRes);
                    finalRes = ilGen.DeclareLocal(retType);

                    ((INativeType)_restype).EmitMarshalling(ilGen, new Local(tmpRes), constantPool, 0);
                    ilGen.Emit(OpCodes.Stloc, finalRes);
                } else {
                    ilGen.Emit(OpCodes.Pop);
                }

                // } catch(Exception e) { 
                // emit the cleanup code

                ilGen.BeginCatchBlock(typeof(Exception));

                ilGen.Emit(OpCodes.Ldarg_0);
                ilGen.Emit(OpCodes.Ldc_I4, contextIndex);
                ilGen.Emit(OpCodes.Ldelem_Ref);
                ilGen.Emit(OpCodes.Call, typeof(ModuleOps).GetMethod("CallbackException"));

                ilGen.EndExceptionBlock();

                if (_restype != null) {
                    ilGen.Emit(OpCodes.Ldloc, finalRes);
                }
                ilGen.Emit(OpCodes.Ret);

                _reverseDelegateConstants = constantPool;
                _reverseDelegateType = GetReverseDelegateType(ArrayUtils.RemoveFirst(sigTypes), CallingConvention);
                _reverseDelegate = dm;
            }

            private void GetSignatureInfo(out Type[] sigTypes, out Type[] callSiteType, out Type retType) {
                sigTypes = new Type[_argtypes.Length + 2];     // constant pool, args ..., ret type
                callSiteType = new Type[_argtypes.Length + 4]; // CallSite, context, target, args ..., ret type

                sigTypes[0] = typeof(object[]);
                callSiteType[0] = typeof(CallSite);
                callSiteType[1] = typeof(CodeContext);
                callSiteType[2] = typeof(object);
                callSiteType[callSiteType.Length - 1] = typeof(object);

                for (int i = 0; i < _argtypes.Length; i++) {
                    sigTypes[i + 1] = _argtypes[i].GetNativeType();
                    Debug.Assert(sigTypes[i + 1] != typeof(object));
                    callSiteType[i + 3] = _argtypes[i].GetPythonType();
                }

                if (_restype != null) {
                    sigTypes[sigTypes.Length - 1] = retType = ((INativeType)_restype).GetNativeType();
                } else {
                    sigTypes[sigTypes.Length - 1] = retType = typeof(void);
                }
            }

            private static Type GetReverseDelegateType(Type[] nativeSig, CallingConvention callingConvention) {
                Type res;
                lock (_reverseDelegates) {
                    DelegateCacheKey key = new DelegateCacheKey(nativeSig, callingConvention);
                    if (!_reverseDelegates.TryGetValue(key, out res)) {
                        res = _reverseDelegates[key] = PythonOps.MakeNewCustomDelegate(nativeSig, callingConvention);
                    }
                }

                return res;
            }

            struct DelegateCacheKey : IEquatable<DelegateCacheKey> {
                private readonly Type[] _types;
                private readonly CallingConvention _callConv;

                public DelegateCacheKey(Type[] sig, CallingConvention callingConvention) {
                    Assert.NotNullItems(sig);
                    _types = sig;
                    _callConv = callingConvention;
                }

                public override int GetHashCode() {
                    int res = _callConv.GetHashCode();
                    for (int i = 0; i < _types.Length; i++) {
                        res ^= _types[i].GetHashCode();
                    }
                    return res;
                }

                public override bool Equals(object obj) {
                    if (obj is DelegateCacheKey) {
                        return Equals((DelegateCacheKey)obj);
                    }

                    return false;
                }

                #region IEquatable<DelegateCacheKey> Members

                public bool Equals(DelegateCacheKey other) {
                    if (other._types.Length != _types.Length ||
                        other._callConv != _callConv) {
                        return false;
                    }

                    for (int i = 0; i < _types.Length; i++) {
                        if (_types[i] != other._types[i]) {
                            return false;
                        }
                    }
                    return true;
                }

                #endregion
            }
        }
    }
}

#endif
