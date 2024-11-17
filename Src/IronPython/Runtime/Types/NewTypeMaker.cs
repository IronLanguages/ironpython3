// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Dynamic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;

using IronPython.Runtime.Binding;
using IronPython.Runtime.Operations;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime.Types {
    /// <summary>
    /// Python class hierarchy is represented using the __class__ field in the object. It does not 
    /// use the CLI type system for pure Python types. However, Python types which inherit from a 
    /// CLI type, or from a builtin Python type which is implemented in the engine by a CLI type,
    /// do have to use the CLI type system to interoperate with the CLI world. This means that 
    /// objects of different Python types, but with the same CLI base type, can use the same CLI type - 
    /// they will just have different values for the __class__ field.
    /// 
    /// The easiest way to inspect the functionality implemented by NewTypeMaker is to persist the
    /// generated IL using "ipy.exe -X:SaveAssemblies", and then inspect the
    /// persisted IL using ildasm.
    /// </summary>
    internal sealed class NewTypeMaker {
        private Type _baseType;
        private IList<Type> _interfaceTypes;

#if FEATURE_REFEMIT
        private TypeBuilder _tg;

        private FieldInfo _typeField;
        private FieldInfo _dictField;
        private FieldInfo _slotsField;
        private FieldInfo _explicitMO;

        private ILGen _cctor;
        private int _site;

        [MultiRuntimeAware]
        private static int _typeCount;
#endif

        public const string VtableNamesField = "#VTableNames#";
        public const string TypePrefix = "IronPython.NewTypes.";
        public const string BaseMethodPrefix = "#base#";
        public const string FieldGetterPrefix = "#field_get#", FieldSetterPrefix = "#field_set#";
#if FEATURE_REFEMIT
        public const string ClassFieldName = ".class", DictFieldName = ".dict", SlotsAndWeakRefFieldName = ".slots_and_weakref";
#else
        public const string ClassFieldName = "_class", DictFieldName = "_dict", SlotsAndWeakRefFieldName = "_slots_and_weakref";
#endif
        private const string _constructorTypeName = "PythonCachedTypeConstructor";
        private const string _constructorMethodName = "GetTypeInfo";

        [MultiRuntimeAware]
        internal static readonly Publisher<NewTypeInfo, Type> _newTypes = new Publisher<NewTypeInfo, Type>();
        [MultiRuntimeAware]
        private static readonly Dictionary<Type, Dictionary<string, List<MethodInfo>>> _overriddenMethods = new Dictionary<Type, Dictionary<string, List<MethodInfo>>>();
        [MultiRuntimeAware]
        private static readonly Dictionary<Type, Dictionary<string, List<ExtensionPropertyTracker>>> _overriddenProperties = new Dictionary<Type, Dictionary<string, List<ExtensionPropertyTracker>>>();


        private NewTypeMaker(NewTypeInfo typeInfo) {
            _baseType = typeInfo.BaseType;
            _interfaceTypes = typeInfo.InterfaceTypes;
        }

        #region Public API

#if FEATURE_REFEMIT
        public static Type/*!*/ GetNewType(string/*!*/ typeName, PythonTuple/*!*/ bases) {
            Assert.NotNull(typeName, bases);

            NewTypeInfo typeInfo = NewTypeInfo.GetTypeInfo(typeName, bases);

            if (typeInfo.BaseType.IsValueType) {
                throw PythonOps.TypeError("cannot derive from {0} because it is a value type", typeInfo.BaseType.FullName);
            } else if (typeInfo.BaseType.IsSealed) {
                throw PythonOps.TypeError("cannot derive from {0} because it is sealed", typeInfo.BaseType.FullName);
            }

            Type ret = _newTypes.GetOrCreateValue(typeInfo,
                () => {
                    if (typeInfo.InterfaceTypes.Count == 0) {
                        // types that the have DynamicBaseType attribute can be used as NewType's directly, no 
                        // need to create a new type unless we're adding interfaces
                        object[] attrs = typeInfo.BaseType.GetCustomAttributes(typeof(DynamicBaseTypeAttribute), false);
                        if (attrs.Length > 0) {
                            return typeInfo.BaseType;
                        }
                    }

                    // creation code                    
                    return new NewTypeMaker(typeInfo).CreateNewType();
                });

            return ret;
        }

        public static void SaveNewTypes(string assemblyName, IList<PythonTuple> types) {
            Assert.NotNull(assemblyName, types);

            AssemblyGen ag = new AssemblyGen(new AssemblyName(assemblyName), ".", ".dll", false);
            TypeBuilder tb = ag.DefinePublicType(_constructorTypeName, typeof(object), true);
            tb.SetCustomAttribute(new CustomAttributeBuilder(typeof(PythonCachedTypeInfoAttribute).GetConstructor(ReflectionUtils.EmptyTypes), Array.Empty<object>()));

            MethodBuilder mb = tb.DefineMethod(_constructorMethodName, MethodAttributes.Public | MethodAttributes.Static, typeof(CachedNewTypeInfo[]), ReflectionUtils.EmptyTypes);
            ILGenerator ilg = mb.GetILGenerator();

            // new CachedTypeInfo[types.Count]
            // we leave this on the stack (duping it) and storing into it.
            EmitInt(ilg, types.Count);
            ilg.Emit(OpCodes.Newarr, typeof(CachedNewTypeInfo));
            int curType = 0;

            foreach (var v in types) {
                NewTypeInfo nti = NewTypeInfo.GetTypeInfo(String.Empty, v);

                var typeInfos = new NewTypeMaker(nti).SaveType(ag, "Python" + _typeCount++ + "$" + nti.BaseType.Name);

                // prepare for storing the element into our final array
                ilg.Emit(OpCodes.Dup);
                EmitInt(ilg, curType++);

                // new CachedNewTypeInfo(type, specialNames, interfaceTypes):

                // load the type
                ilg.Emit(OpCodes.Ldtoken, typeInfos.Key);
                ilg.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle"));

                // create the dictionary<str, str[]> of special names
                ilg.Emit(OpCodes.Newobj, typeof(Dictionary<string, string[]>).GetConstructor(Array.Empty<Type>()));
                foreach (var specialName in typeInfos.Value) {
                    // dup dict
                    ilg.Emit(OpCodes.Dup);

                    // emit key
                    ilg.Emit(OpCodes.Ldstr, specialName.Key);

                    // emit value
                    int iVal = specialName.Value.Length;
                    EmitInt(ilg, iVal);
                    ilg.Emit(OpCodes.Newarr, typeof(string));
                    for (int i = 0; i < specialName.Value.Length; i++) {
                        ilg.Emit(OpCodes.Dup);
                        EmitInt(ilg, i);
                        ilg.Emit(OpCodes.Ldstr, specialName.Value[0]);
                        ilg.Emit(OpCodes.Stelem_Ref);
                    }

                    // assign to dict
                    ilg.Emit(OpCodes.Call, typeof(Dictionary<string, string[]>).GetMethod("set_Item"));
                }

                // emit the interface types (if any)
                if (nti.InterfaceTypes.Count != 0) {
                    EmitInt(ilg, nti.InterfaceTypes.Count);
                    ilg.Emit(OpCodes.Newarr, typeof(Type));

                    for (int i = 0; i < nti.InterfaceTypes.Count; i++) {
                        ilg.Emit(OpCodes.Dup);
                        EmitInt(ilg, i);
                        ilg.Emit(OpCodes.Ldtoken, nti.InterfaceTypes[i]);
                        ilg.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle"));
                        ilg.Emit(OpCodes.Stelem_Ref);
                    }
                } else {
                    ilg.Emit(OpCodes.Ldnull);
                }

                // crated the CachedNewTypeInfo and store it in the array
                ilg.Emit(OpCodes.Newobj, typeof(CachedNewTypeInfo).GetConstructors()[0]);
                ilg.Emit(OpCodes.Stelem_Ref);
            }
            ilg.Emit(OpCodes.Ret);
            tb.CreateTypeInfo();
            ag.SaveAssembly();
        }
#endif

        /// <summary>
        /// Loads any available new types from the provided assembly and makes them
        /// available via the GetNewType API.
        /// </summary>
        public static void LoadNewTypes(Assembly/*!*/ asm) {
            Assert.NotNull(asm);

            Type t = asm.GetType(_constructorTypeName);
            if (t == null || !t.IsDefined(typeof(PythonCachedTypeInfoAttribute), false)) {
                return;
            }

            MethodInfo mi = t.GetMethod(_constructorMethodName);
            var typeInfo = (CachedNewTypeInfo[])mi.Invoke(null, Array.Empty<object>());
            foreach (var v in typeInfo) {
                _newTypes.GetOrCreateValue(
                    new NewTypeInfo(v.Type.BaseType, v.InterfaceTypes),
                    () => {
                        // type wasn't already created, go ahead and publish
                        // the info and return the type.
                        AddBaseMethods(v.Type, v.SpecialNames);

                        return v.Type;
                    }
                );
            }
        }

        /// <summary>
        /// Is this a type used for instances Python types (and not for the types themselves)?
        /// </summary>
        public static bool IsInstanceType(Type type) {
            return type.FullName.StartsWith(NewTypeMaker.TypePrefix, StringComparison.Ordinal) ||
                // Users can create sub-types of instance-types using __clrtype__ without using 
                // NewTypeMaker.TypePrefix
                ((type.BaseType != null) && IsInstanceType(type.BaseType));
        }

        #endregion

        #region Type Generation

#if FEATURE_REFEMIT
        private Type CreateNewType() {
            string name = GetName();
            _tg = Snippets.Shared.DefinePublicType(TypePrefix + name, _baseType);

            Dictionary<string, string[]> specialNames = ImplementType();

            Type ret = FinishType();

            AddBaseMethods(ret, specialNames);

            return ret;
        }

        // Build a name which is unique to this TypeInfo.
        private string GetName() {
            StringBuilder name = new StringBuilder(_baseType.Namespace);
            name.Append('.');
            name.Append(_baseType.Name);
            foreach (Type interfaceType in _interfaceTypes) {
                name.Append("#");
                name.Append(interfaceType.Name);
            }

            name.Append("_");
            name.Append(System.Threading.Interlocked.Increment(ref _typeCount));
            return name.ToString();
        }

        private Dictionary<string, string[]> ImplementType() {
            DefineInterfaces();

            ImplementPythonObject();

            ImplementConstructors();

            Dictionary<string, string[]> specialNames = new Dictionary<string, string[]>();

            OverrideMethods(_baseType, specialNames);

            ImplementProtectedFieldAccessors(specialNames);

            Dictionary<Type, bool> doneTypes = new Dictionary<Type, bool>();
            foreach (Type interfaceType in _interfaceTypes) {
                DoInterfaceType(interfaceType, doneTypes, specialNames);
            }

            return specialNames;
        }

        private void DefineInterfaces() {
            foreach (Type interfaceType in _interfaceTypes) {
                ImplementInterface(interfaceType);
            }
        }

        private void ImplementInterface(Type interfaceType) {
            _tg.AddInterfaceImplementation(interfaceType);
        }

        private void ImplementPythonObject() {
            ImplementIPythonObject();

            ImplementDynamicObject();

#if FEATURE_CUSTOM_TYPE_DESCRIPTOR
            ImplementCustomTypeDescriptor();
#endif
            ImplementWeakReference();

            AddDebugView();
        }

        private void AddDebugView() {
            _tg.SetCustomAttribute(
                new CustomAttributeBuilder(
                    typeof(DebuggerTypeProxyAttribute).GetConstructor(new[] { typeof(Type) }),
                    new object[] { typeof(UserTypeDebugView) }
                )
            );

            _tg.SetCustomAttribute(
                new CustomAttributeBuilder(
                    typeof(DebuggerDisplayAttribute).GetConstructor(new[] { typeof(string) }),
                    new object[] { "{get_PythonType().GetTypeDebuggerDisplay()}" }
                )
            );
        }

        private void EmitGetDict(ILGen gen) {
            gen.EmitFieldGet(_dictField);
        }

        private void EmitSetDict(ILGen gen) {
            gen.EmitFieldSet(_dictField);
        }

        private ParameterInfo[] GetOverrideCtorSignature(ParameterInfo[] original) {
            if (typeof(IPythonObject).IsAssignableFrom(_baseType)) {
                return original;
            }

            ParameterInfo[] argTypes = new ParameterInfo[original.Length + 1];
            if (original.Length == 0 || original[0].ParameterType != typeof(CodeContext)) {
                argTypes[0] = new ParameterInfoWrapper(typeof(PythonType), "cls");
                Array.Copy(original, 0, argTypes, 1, argTypes.Length - 1);
            } else {
                argTypes[0] = original[0];
                argTypes[1] = new ParameterInfoWrapper(typeof(PythonType), "cls");
                Array.Copy(original, 1, argTypes, 2, argTypes.Length - 2);
            }

            return argTypes;
        }

        private void ImplementConstructors() {
            ConstructorInfo[] constructors = _baseType.GetConstructors(BindingFlags.Public |
                                                    BindingFlags.NonPublic |
                                                    BindingFlags.Instance
                                                    );

            foreach (ConstructorInfo ci in constructors) {
                if (ci.IsPublic || ci.IsProtected()) {
                    OverrideConstructor(ci);
                }
            }
        }

        private static bool CanOverrideMethod(MethodInfo mi) {
            return true;
        }

        private void DoInterfaceType(Type interfaceType, Dictionary<Type, bool> doneTypes, Dictionary<string, string[]> specialNames) {
            if (interfaceType == typeof(IDynamicMetaObjectProvider)) {
                // very tricky, we'll handle it when we're creating
                // our own IDynamicMetaObjectProvider interface
                return;
            }

            if (doneTypes.ContainsKey(interfaceType)) return;
            doneTypes.Add(interfaceType, true);
            OverrideMethods(interfaceType, specialNames);

            foreach (Type t in interfaceType.GetInterfaces()) {
                DoInterfaceType(t, doneTypes, specialNames);
            }
        }

        private void OverrideConstructor(ConstructorInfo parentConstructor) {
            ParameterInfo[] pis = parentConstructor.GetParameters();
            if (pis.Length == 0 && typeof(IPythonObject).IsAssignableFrom(_baseType)) {
                // default ctor on a base type, don't override this one, it assumes
                // the PythonType is some default value and we'll always be unique.
                return;
            }

            ParameterInfo[] overrideParams = GetOverrideCtorSignature(pis);

            Type[] argTypes = new Type[overrideParams.Length];
            string[] paramNames = new string[overrideParams.Length];
            for (int i = 0; i < overrideParams.Length; i++) {
                argTypes[i] = overrideParams[i].ParameterType;
                paramNames[i] = overrideParams[i].Name;
            }

            ConstructorBuilder cb = _tg.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, argTypes);

            for (int i = 0; i < overrideParams.Length; i++) {
                ParameterBuilder pb = cb.DefineParameter(i + 1,
                    overrideParams[i].Attributes,
                    overrideParams[i].Name);

                int origIndex = GetOriginalIndex(pis, overrideParams, i);
                if (origIndex >= 0) {
                    ParameterInfo pi = pis[origIndex];

                    // Defines attributes that might be used in .net methods for overload detection and other
                    // parameter based attribute logic
                    if (pi.IsDefined(typeof(ParamArrayAttribute), false)) {
                        pb.SetCustomAttribute(new CustomAttributeBuilder(
                            typeof(ParamArrayAttribute).GetConstructor(ReflectionUtils.EmptyTypes), ArrayUtils.EmptyObjects));
                    } else if (pi.IsDefined(typeof(ParamDictionaryAttribute), false)) {
                        pb.SetCustomAttribute(new CustomAttributeBuilder(
                            typeof(ParamDictionaryAttribute).GetConstructor(ReflectionUtils.EmptyTypes), ArrayUtils.EmptyObjects));
                    } else if (pi.IsDefined(typeof(BytesLikeAttribute), false)) {
                        pb.SetCustomAttribute(new CustomAttributeBuilder(
                            typeof(BytesLikeAttribute).GetConstructor(ReflectionUtils.EmptyTypes), ArrayUtils.EmptyObjects));
                    }

                    if ((pi.Attributes & ParameterAttributes.HasDefault) != 0) {
                        if (pi.DefaultValue == null || pi.ParameterType.IsAssignableFrom(pi.DefaultValue.GetType())) {
                            pb.SetConstant(pi.DefaultValue);
                        } else {
                            pb.SetConstant(Convert.ChangeType(
                                pi.DefaultValue, pi.ParameterType,
                                System.Globalization.CultureInfo.CurrentCulture
                            ));
                        }
                    }
                }
            }

            ILGen il = new ILGen(cb.GetILGenerator());

            int typeArg;
            if (pis.Length == 0 || pis[0].ParameterType != typeof(CodeContext)) {
                typeArg = 1;
            } else {
                typeArg = 2;
            }

            // this.__class__ = <arg?>
            //  can occur 2 ways:
            //      1. If we have our own _typeField then we set it
            //      2. If we're a subclass of IPythonObject (e.g. one of our exception classes) then we'll flow it to the
            //             base type constructor which will set it.
            if (!typeof(IPythonObject).IsAssignableFrom(_baseType)) {
                il.EmitLoadArg(0);
                // base class could have CodeContext parameter in which case our type is the 2nd parameter.
                il.EmitLoadArg(typeArg);
                il.EmitFieldSet(_typeField);
            }

            if (_explicitMO != null) {
                il.Emit(OpCodes.Ldarg_0);
                il.EmitNew(_explicitMO.FieldType.GetConstructor(ReflectionUtils.EmptyTypes));
                il.Emit(OpCodes.Stfld, _explicitMO);
            }

            // initialize all slots to Uninitialized.instance
            MethodInfo init = typeof(PythonOps).GetMethod(nameof(PythonOps.InitializeUserTypeSlots));

            il.EmitLoadArg(0);

            il.EmitLoadArg(typeArg);
            il.EmitCall(init);

            Debug.Assert(_slotsField != null);
            il.EmitFieldSet(_slotsField);

            CallBaseConstructor(parentConstructor, pis, overrideParams, il);
        }

        /// <summary>
        /// Gets the position for the parameter which we are overriding.
        /// </summary>
        /// <param name="pis"></param>
        /// <param name="overrideParams"></param>
        /// <param name="i"></param>
        /// <returns></returns>
        private static int GetOriginalIndex(ParameterInfo[] pis, ParameterInfo[] overrideParams, int i) {
            if (pis.Length == 0 || pis[0].ParameterType != typeof(CodeContext)) {
                return i - (overrideParams.Length - pis.Length);
            }

            // context & cls are swapped, context comes first.
            if (i == 1) return -1;
            if (i == 0) return 0;

            return i - (overrideParams.Length - pis.Length);
        }

        private static void CallBaseConstructor(ConstructorInfo parentConstructor, ParameterInfo[] pis, ParameterInfo[] overrideParams, ILGen il) {
            il.EmitLoadArg(0);
#if DEBUG
            int lastIndex = -1;
#endif
            for (int i = 0; i < overrideParams.Length; i++) {
                int index = GetOriginalIndex(pis, overrideParams, i);

#if DEBUG
                // we insert a new parameter (the class) but the parametrers should
                // still remain in the same order after the extra parameter is removed.
                if (index >= 0) {
                    Debug.Assert(index > lastIndex);
                    lastIndex = index;
                }
#endif
                if (index >= 0) {
                    il.EmitLoadArg(i + 1);
                }
            }
            il.Emit(OpCodes.Call, parentConstructor);
            il.Emit(OpCodes.Ret);
        }

        private ILGen GetCCtor() {
            if (_cctor == null) {
                ConstructorBuilder cctor = _tg.DefineTypeInitializer();
                _cctor = new ILGen(cctor.GetILGenerator());
            }
            return _cctor;
        }

#if FEATURE_CUSTOM_TYPE_DESCRIPTOR
        private void ImplementCustomTypeDescriptor() {
            ImplementInterface(typeof(ICustomTypeDescriptor));

            foreach (MethodInfo m in typeof(ICustomTypeDescriptor).GetMethods()) {
                ImplementCTDOverride(m);
            }
        }

        private void ImplementCTDOverride(MethodInfo m) {
            MethodBuilder builder;
            ILGen il = DefineExplicitInterfaceImplementation(m, out builder);
            il.EmitLoadArg(0);

            ParameterInfo[] pis = m.GetParameters();
            Type[] paramTypes = new Type[pis.Length + 1];
            paramTypes[0] = typeof(object);
            for (int i = 0; i < pis.Length; i++) {
                il.EmitLoadArg(i + 1);
                paramTypes[i + 1] = pis[i].ParameterType;
            }

            il.EmitCall(typeof(CustomTypeDescHelpers), m.Name, paramTypes);
            il.EmitBoxing(m.ReturnType);
            il.Emit(OpCodes.Ret);
            _tg.DefineMethodOverride(builder, m);
        }
#endif

        private bool NeedsPythonObject {
            get {
                return !typeof(IPythonObject).IsAssignableFrom(_baseType);
            }
        }

        private void ImplementDynamicObject() {
            // true if the user has explicitly included IDynamicMetaObjectProvider in the list of interfaces
            bool explicitDynamicObject = false;
            foreach (Type t in _interfaceTypes) {
                if (t == typeof(IDynamicMetaObjectProvider)) {
                    explicitDynamicObject = true;
                    break;
                }
            }

            // true if our base type implements IDMOP already
            bool baseIdo = typeof(IDynamicMetaObjectProvider).IsAssignableFrom(_baseType);
            if (baseIdo) {
                InterfaceMapping mapping = _baseType.GetInterfaceMap(typeof(IDynamicMetaObjectProvider));
                if (mapping.TargetMethods[0].IsPrivate) {
                    // explicitly implemented IDynamicMetaObjectProvider, we cannot override it.

                    if (_baseType.IsDefined(typeof(DynamicBaseTypeAttribute), true)) {
                        // but it's been implemented by IronPython so it's going to return a MetaUserObject
                        return;
                    }

                    // we can't dispatch to the subclasses IDMOP implementation, completely
                    // replace it with our own.
                    baseIdo = false;
                }
            }

            ImplementInterface(typeof(IDynamicMetaObjectProvider));

            MethodInfo decl;
            MethodBuilder impl;
            ILGen il = DefineMethodOverride(MethodAttributes.Private, typeof(IDynamicMetaObjectProvider), "GetMetaObject", out decl, out impl);
            MethodInfo mi = typeof(UserTypeOps).GetMethod(nameof(UserTypeOps.GetMetaObjectHelper));

            LocalBuilder retVal = il.DeclareLocal(typeof(DynamicMetaObject));
            Label retLabel = il.DefineLabel();
            if (explicitDynamicObject) {
                _explicitMO = _tg.DefineField("__gettingMO", typeof(ThreadLocal<bool>), FieldAttributes.InitOnly | FieldAttributes.Private);

                Label ipyImpl = il.DefineLabel();
                Label noOverride = il.DefineLabel();
                Label retNull = il.DefineLabel();

                var valueProperty = typeof(ThreadLocal<bool>).GetDeclaredProperty("Value");

                // check if the we're recursing (this enables the user to refer to self
                // during GetMetaObject calls)
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, _explicitMO);
                il.EmitPropertyGet(valueProperty);
                il.Emit(OpCodes.Brtrue, ipyImpl);

                // we're not recursing, set the flag...
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, _explicitMO);
                il.Emit(OpCodes.Ldc_I4_1);
                il.EmitPropertySet(valueProperty);

                il.BeginExceptionBlock();

                LocalBuilder callTarget = EmitNonInheritedMethodLookup("GetMetaObject", il);

                il.Emit(OpCodes.Brfalse, noOverride);

                // call the user GetMetaObject function
                EmitClrCallStub(il, typeof(IDynamicMetaObjectProvider).GetMethod("GetMetaObject"), callTarget);

                // check for null return
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Ldnull);
                il.Emit(OpCodes.Beq, retNull);

                // store the local value
                il.Emit(OpCodes.Stloc_S, retVal.LocalIndex);

                // returned a value, that's our result
                il.Emit(OpCodes.Leave, retLabel);

                // user returned null, fallback to base impl
                il.MarkLabel(retNull);
                il.Emit(OpCodes.Pop);

                // no override exists
                il.MarkLabel(noOverride);

                // will emit leave to end of exception block
                il.BeginFinallyBlock();

                // restore the flag now that we're done
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, _explicitMO);
                il.Emit(OpCodes.Ldc_I4_0);
                il.EmitPropertySet(typeof(ThreadLocal<bool>).GetDeclaredProperty("Value"));

                il.EndExceptionBlock();

                // no user defined function or no result
                il.MarkLabel(ipyImpl);
            }

            il.EmitLoadArg(0);  // this
            il.EmitLoadArg(1);  // parameter

            // baseMetaObject
            if (baseIdo) {
                InterfaceMapping imap = _baseType.GetInterfaceMap(typeof(IDynamicMetaObjectProvider));

                il.EmitLoadArg(0);  // this
                il.EmitLoadArg(1);  // parameter
                il.EmitCall(imap.TargetMethods[0]);
            } else {
                il.EmitNull();
            }

            il.EmitCall(mi);
            il.Emit(OpCodes.Stloc, retVal.LocalIndex);

            il.MarkLabel(retLabel);

            il.Emit(OpCodes.Ldloc, retVal.LocalIndex);
            il.Emit(OpCodes.Ret);

            _tg.DefineMethodOverride(impl, decl);
        }

        private void ImplementIPythonObject() {
            ILGen il;
            MethodInfo decl;
            MethodBuilder impl;

            if (NeedsPythonObject) {
                _typeField = _tg.DefineField(ClassFieldName, typeof(PythonType), FieldAttributes.Public);
                _dictField = _tg.DefineField(DictFieldName, typeof(PythonDictionary), FieldAttributes.Public);

                ImplementInterface(typeof(IPythonObject));

                MethodAttributes attrs = MethodAttributes.Private;

                il = DefineMethodOverride(attrs, typeof(IPythonObject), "get_Dict", out decl, out impl);
                il.EmitLoadArg(0);
                EmitGetDict(il);
                il.Emit(OpCodes.Ret);
                _tg.DefineMethodOverride(impl, decl);

                il = DefineMethodOverride(attrs, typeof(IPythonObject), nameof(IPythonObject.ReplaceDict), out decl, out impl);
                il.EmitLoadArg(0);
                il.EmitLoadArg(1);
                EmitSetDict(il);
                il.EmitBoolean(true);
                il.Emit(OpCodes.Ret);
                _tg.DefineMethodOverride(impl, decl);

                il = DefineMethodOverride(attrs, typeof(IPythonObject), nameof(IPythonObject.SetDict), out decl, out impl);
                il.EmitLoadArg(0);
                il.EmitFieldAddress(_dictField);
                il.EmitLoadArg(1);
                il.EmitCall(typeof(UserTypeOps), nameof(UserTypeOps.SetDictHelper));
                il.Emit(OpCodes.Ret);
                _tg.DefineMethodOverride(impl, decl);

                il = DefineMethodOverride(attrs, typeof(IPythonObject), "get_PythonType", out decl, out impl);
                il.EmitLoadArg(0);
                il.EmitFieldGet(_typeField);
                il.Emit(OpCodes.Ret);
                _tg.DefineMethodOverride(impl, decl);

                il = DefineMethodOverride(attrs, typeof(IPythonObject), nameof(IPythonObject.SetPythonType), out decl, out impl);
                il.EmitLoadArg(0);
                il.EmitLoadArg(1);
                il.EmitFieldSet(_typeField);
                il.Emit(OpCodes.Ret);
                _tg.DefineMethodOverride(impl, decl);
            }

            // Types w/ DynamicBaseType attribute still need new slots implementation

            _slotsField = _tg.DefineField(SlotsAndWeakRefFieldName, typeof(object[]), FieldAttributes.Public);
            il = DefineMethodOverride(MethodAttributes.Private, typeof(IPythonObject), nameof(IPythonObject.GetSlots), out decl, out impl);
            il.EmitLoadArg(0);
            il.EmitFieldGet(_slotsField);
            il.Emit(OpCodes.Ret);
            _tg.DefineMethodOverride(impl, decl);

            il = DefineMethodOverride(MethodAttributes.Private, typeof(IPythonObject), nameof(IPythonObject.GetSlotsCreate), out decl, out impl);
            il.EmitLoadArg(0);
            il.EmitLoadArg(0);
            il.EmitFieldAddress(_slotsField);
            il.EmitCall(typeof(UserTypeOps).GetMethod(nameof(UserTypeOps.GetSlotsCreate)));
            il.Emit(OpCodes.Ret);
            _tg.DefineMethodOverride(impl, decl);

        }

        /// <summary>
        /// Defines an interface on the type that forwards all calls
        /// to a helper method in UserType.  The method names all will
        /// have Helper appended to them to get the name for UserType.  The 
        /// UserType version should take 1 extra parameter (self).
        /// </summary>
        private void DefineHelperInterface(Type intf) {
            ImplementInterface(intf);
            MethodInfo[] mis = intf.GetMethods();

            foreach (MethodInfo mi in mis) {
                MethodBuilder impl;
                ILGen il = DefineExplicitInterfaceImplementation(mi, out impl);
                ParameterInfo[] pis = mi.GetParameters();

                MethodInfo helperMethod = typeof(UserTypeOps).GetMethod(mi.Name + "Helper");
                int offset = 0;
                if (pis.Length > 0 && pis[0].ParameterType == typeof(CodeContext)) {
                    // if the interface takes CodeContext then the helper method better take
                    // it as well.
                    Debug.Assert(helperMethod.GetParameters()[0].ParameterType == typeof(CodeContext));
                    offset = 1;
                    il.EmitLoadArg(1);
                }

                il.EmitLoadArg(0);
                for (int i = offset; i < pis.Length; i++) {
                    il.EmitLoadArg(i + 1);
                }

                il.EmitCall(helperMethod);
                il.Emit(OpCodes.Ret);
                _tg.DefineMethodOverride(impl, mi);
            }
        }

        private void ImplementWeakReference() {
            if (typeof(IWeakReferenceable).IsAssignableFrom(_baseType)) {
                return;
            }

            DefineHelperInterface(typeof(IWeakReferenceable));
        }

        private void ImplementProtectedFieldAccessors(Dictionary<string, string[]> specialNames) {
            // For protected fields to be accessible from the derived type in Silverlight,
            // we need to create public helper methods that expose them. These methods are
            // used by the IDynamicMetaObjectProvider implementation (in MetaUserObject)

            foreach (FieldInfo fi in _baseType.GetInheritedFields(flattenHierarchy: true)) {
                if (!fi.IsProtected()) {
                    continue;
                }

                List<string> fieldAccessorNames = new List<string>();

                PropertyBuilder pb = _tg.DefineProperty(fi.Name, PropertyAttributes.None, fi.FieldType, ReflectionUtils.EmptyTypes);
                MethodAttributes methodAttrs = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName;
                if (fi.IsStatic) {
                    methodAttrs |= MethodAttributes.Static;
                }

                MethodBuilder method = _tg.DefineMethod(FieldGetterPrefix + fi.Name, methodAttrs, fi.FieldType, ReflectionUtils.EmptyTypes);
                ILGen il = new ILGen(method.GetILGenerator());
                if (!fi.IsStatic) {
                    il.EmitLoadArg(0);
                }

                if (fi.IsLiteral) {
                    // literal fields need to be inlined directly in here... We use GetRawConstant
                    // which will work even in partial trust if the constant is protected.
                    object value = fi.GetRawConstantValue();
                    switch (fi.FieldType.GetTypeCode()) {
                        case TypeCode.Boolean:
                            if ((bool)value) {
                                il.Emit(OpCodes.Ldc_I4_1);
                            } else {
                                il.Emit(OpCodes.Ldc_I4_0);
                            }
                            break;
                        case TypeCode.Byte: il.Emit(OpCodes.Ldc_I4, (byte)value); break;
                        case TypeCode.Char: il.Emit(OpCodes.Ldc_I4, (char)value); break;
                        case TypeCode.Double: il.Emit(OpCodes.Ldc_R8, (double)value); break;
                        case TypeCode.Int16: il.Emit(OpCodes.Ldc_I4, (short)value); break;
                        case TypeCode.Int32: il.Emit(OpCodes.Ldc_I4, (int)value); break;
                        case TypeCode.Int64: il.Emit(OpCodes.Ldc_I8, (long)value); break;
                        case TypeCode.SByte: il.Emit(OpCodes.Ldc_I4, (sbyte)value); break;
                        case TypeCode.Single: il.Emit(OpCodes.Ldc_R4, (float)value); break;
                        case TypeCode.String: il.Emit(OpCodes.Ldstr, (string)value); break;
                        case TypeCode.UInt16: il.Emit(OpCodes.Ldc_I4, (ushort)value); break;
                        case TypeCode.UInt32: il.Emit(OpCodes.Ldc_I4, (uint)value); break;
                        case TypeCode.UInt64: il.Emit(OpCodes.Ldc_I8, (ulong)value); break;
                    }
                } else {
                    il.EmitFieldGet(fi);
                }
                il.Emit(OpCodes.Ret);

                pb.SetGetMethod(method);
                fieldAccessorNames.Add(method.Name);

                if (!fi.IsLiteral && !fi.IsInitOnly) {
                    method = _tg.DefineMethod(FieldSetterPrefix + fi.Name, methodAttrs,
                                              null, new Type[] { fi.FieldType });
                    method.DefineParameter(1, ParameterAttributes.None, "value");
                    il = new ILGen(method.GetILGenerator());
                    il.EmitLoadArg(0);
                    if (!fi.IsStatic) {
                        il.EmitLoadArg(1);
                    }
                    il.EmitFieldSet(fi);
                    il.Emit(OpCodes.Ret);
                    pb.SetSetMethod(method);

                    fieldAccessorNames.Add(method.Name);
                }

                specialNames[fi.Name] = fieldAccessorNames.ToArray();
            }
        }

        /// <summary>
        /// Overrides methods - this includes all accessible virtual methods as well as protected non-virtual members
        /// including statics and non-statics.
        /// </summary>
        private void OverrideMethods(Type type, Dictionary<string, string[]> specialNames) {
            // if we have conflicting virtual's do to new slots only override the methods on the
            // most derived class.
            Dictionary<KeyValuePair<string, MethodSignatureInfo>, MethodInfo> added = new Dictionary<KeyValuePair<string, MethodSignatureInfo>, MethodInfo>();

            MethodInfo overridden;
            MethodInfo[] methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy);

            foreach (MethodInfo mi in methods) {
                KeyValuePair<string, MethodSignatureInfo> key = new KeyValuePair<string, MethodSignatureInfo>(mi.Name, new MethodSignatureInfo(mi));

                if (!added.TryGetValue(key, out overridden)) {
                    added[key] = mi;
                    continue;
                }

                if (overridden.DeclaringType.IsAssignableFrom(mi.DeclaringType)) {
                    added[key] = mi;
                }
            }

            if (type.IsAbstract && !type.IsInterface) {
                // abstract types can define interfaces w/o implementations
                foreach (Type iface in type.GetInterfaces()) {
                    InterfaceMapping mapping = type.GetInterfaceMap(iface);
                    for (int i = 0; i < mapping.TargetMethods.Length; i++) {

                        if (mapping.TargetMethods[i] == null) {
                            MethodInfo mi = mapping.InterfaceMethods[i];

                            KeyValuePair<string, MethodSignatureInfo> key = new KeyValuePair<string, MethodSignatureInfo>(mi.Name, new MethodSignatureInfo(mi));

                            added[key] = mi;
                        }
                    }
                }
            }

            Dictionary<PropertyInfo, PropertyBuilder> overriddenProperties = new Dictionary<PropertyInfo, PropertyBuilder>();
            foreach (MethodInfo mi in added.Values) {
                if (!CanOverrideMethod(mi)) continue;

                if (mi.IsPublic || mi.IsProtected()) {
                    if (mi.IsSpecialName) {
                        OverrideSpecialName(mi, specialNames, overriddenProperties);
                    } else {
                        OverrideBaseMethod(mi, specialNames);
                    }
                }
            }
        }

        private void OverrideSpecialName(MethodInfo mi, Dictionary<string, string[]> specialNames, Dictionary<PropertyInfo, PropertyBuilder> overridden) {
            if (!mi.IsVirtual || mi.IsFinal) {
                // TODO: A better check here would be mi.IsFamily && mi.IsSpecialName.  But we need to also check
                // the other property method (getter if we're a setter, setter if we're a getter) because if one is protected
                // and the other isn't we need to still override both (so our newslot property is also both a getter and a setter).
                if ((mi.IsProtected() || mi.IsSpecialName) && (mi.Name.StartsWith("get_", StringComparison.Ordinal) || mi.Name.StartsWith("set_", StringComparison.Ordinal))) {
                    // need to be able to call into protected getter/setter methods from derived types,
                    // even if these methods aren't virtual and we are in partial trust.
                    specialNames[mi.Name] = new[] { mi.Name };
                    MethodBuilder mb = CreateSuperCallHelper(mi);

                    foreach (PropertyInfo pi in mi.DeclaringType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {

                        if (pi.GetGetMethod(true).MemberEquals(mi) || pi.GetSetMethod(true).MemberEquals(mi)) {
                            AddPublicProperty(mi, overridden, mb, pi);
                            break;
                        }
                    }
                }
            } else if (!TryOverrideProperty(mi, specialNames, overridden)) {
                string name;
                EventInfo[] eis = mi.DeclaringType.GetEvents(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (EventInfo ei in eis) {
                    if (ei.GetAddMethod().MemberEquals(mi)) {
                        if (NameConverter.TryGetName(DynamicHelpers.GetPythonTypeFromType(mi.DeclaringType), ei, mi, out name) == NameType.None) return;
                        CreateVTableEventOverride(mi, mi.Name);
                        return;
                    } else if (ei.GetRemoveMethod().MemberEquals(mi)) {
                        if (NameConverter.TryGetName(DynamicHelpers.GetPythonTypeFromType(mi.DeclaringType), ei, mi, out name) == NameType.None) return;
                        CreateVTableEventOverride(mi, mi.Name);
                        return;
                    }
                }

                OverrideBaseMethod(mi, specialNames);
            }
        }

        private bool TryOverrideProperty(MethodInfo mi, Dictionary<string, string[]> specialNames, Dictionary<PropertyInfo, PropertyBuilder> overridden) {
            string name;
            PropertyInfo[] pis = mi.DeclaringType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            specialNames[mi.Name] = new[] { mi.Name };
            MethodBuilder mb = null;
            PropertyInfo foundProperty = null;
            foreach (PropertyInfo pi in pis) {
                if (pi.GetIndexParameters().Length > 0) {
                    if (mi.MemberEquals(pi.GetGetMethod(true))) {
                        mb = CreateVTableMethodOverride(mi, "__getitem__");
                        if (!mi.IsAbstract) {
                            CreateSuperCallHelper(mi);
                        }
                        foundProperty = pi;
                        break;
                    } else if (mi.MemberEquals(pi.GetSetMethod(true))) {
                        mb = CreateVTableMethodOverride(mi, "__setitem__");
                        if (!mi.IsAbstract) {
                            CreateSuperCallHelper(mi);
                        }
                        foundProperty = pi;
                        break;
                    }
                } else if (mi.MemberEquals(pi.GetGetMethod(true))) {
                    if (mi.Name != "get_PythonType") {
                        if (NameConverter.TryGetName(GetBaseTypeForMethod(mi), pi, mi, out name) == NameType.None) {
                            return true;
                        }
                        mb = CreateVTableGetterOverride(mi, name);
                        if (!mi.IsAbstract) {
                            CreateSuperCallHelper(mi);
                        }
                    }
                    foundProperty = pi;
                    break;
                } else if (mi.MemberEquals(pi.GetSetMethod(true))) {
                    if (NameConverter.TryGetName(GetBaseTypeForMethod(mi), pi, mi, out name) == NameType.None) {
                        return true;
                    }
                    mb = CreateVTableSetterOverride(mi, name);
                    if (!mi.IsAbstract) {
                        CreateSuperCallHelper(mi);
                    }
                    foundProperty = pi;
                    break;
                }
            }

            if (foundProperty != null) {
                AddPublicProperty(mi, overridden, mb, foundProperty);
                return true;
            }
            return false;
        }

        private void AddPublicProperty(MethodInfo mi, Dictionary<PropertyInfo, PropertyBuilder> overridden, MethodBuilder mb, PropertyInfo foundProperty) {
            MethodInfo getter = foundProperty.GetGetMethod(true);
            MethodInfo setter = foundProperty.GetSetMethod(true);
            if (getter != null && getter.IsProtected() || setter != null && setter.IsProtected()) {
                PropertyBuilder builder;
                if (!overridden.TryGetValue(foundProperty, out builder)) {
                    ParameterInfo[] indexArgs = foundProperty.GetIndexParameters();
                    Type[] paramTypes = new Type[indexArgs.Length];
                    for (int i = 0; i < paramTypes.Length; i++) {
                        paramTypes[i] = indexArgs[i].ParameterType;
                    }

                    overridden[foundProperty] = builder = _tg.DefineProperty(foundProperty.Name, foundProperty.Attributes, foundProperty.PropertyType, paramTypes);
                }

                if (foundProperty.GetGetMethod(true).MemberEquals(mi)) {
                    builder.SetGetMethod(mb);
                } else if (foundProperty.GetSetMethod(true).MemberEquals(mi)) {
                    builder.SetSetMethod(mb);
                }
            }
        }

        /// <summary>
        /// Loads all the incoming arguments and forwards them to mi which
        /// has the same signature and then returns the result
        /// </summary>
        private static void EmitBaseMethodDispatch(MethodInfo mi, ILGen il) {
            if (!mi.IsAbstract) {
                int offset = 0;
                if (!mi.IsStatic) {
                    il.EmitLoadArg(0);
                    offset = 1;
                }
                ParameterInfo[] parameters = mi.GetParameters();
                for (int i = 0; i < parameters.Length; i++) {
                    il.EmitLoadArg(i + offset);
                }
                il.EmitCall(OpCodes.Call, mi, null); // base call must be non-virtual
                il.Emit(OpCodes.Ret);
            } else {
                il.EmitLoadArg(0);
                il.EmitString(mi.Name);
                il.EmitCall(typeof(PythonOps), "MissingInvokeMethodException");
                il.Emit(OpCodes.Throw);
            }
        }

        private void OverrideBaseMethod(MethodInfo mi, Dictionary<string, string[]> specialNames) {
            if ((!mi.IsVirtual || mi.IsFinal) && !mi.IsProtected()) {
                return;
            }

            PythonType basePythonType = GetBaseTypeForMethod(mi);

            string name = null;
            if (NameConverter.TryGetName(basePythonType, mi, out name) == NameType.None)
                return;

            if (mi.DeclaringType == typeof(object) && mi.Name == "Finalize") return;

            specialNames[mi.Name] = new[] { mi.Name };

            if (!mi.IsStatic) {
                CreateVTableMethodOverride(mi, name);
            }
            if (!mi.IsAbstract) {
                CreateSuperCallHelper(mi);
            }
        }

        private PythonType GetBaseTypeForMethod(MethodInfo mi) {
            PythonType basePythonType;
            if (_baseType == mi.DeclaringType || _baseType.IsSubclassOf(mi.DeclaringType)) {
                basePythonType = DynamicHelpers.GetPythonTypeFromType(_baseType);
            } else {
                // We must be inherting from an interface
                Debug.Assert(mi.DeclaringType.IsInterface);
                basePythonType = DynamicHelpers.GetPythonTypeFromType(mi.DeclaringType);
            }
            return basePythonType;
        }

        /// <summary>
        /// Emits code to check if the class has overridden this specific
        /// function.  For example:
        /// 
        /// MyDerivedType.SomeVirtualFunction = ...
        ///     or
        /// 
        /// class MyDerivedType(MyBaseType):
        ///     def SomeVirtualFunction(self, ...):
        /// 
        /// </summary>
        private LocalBuilder EmitBaseClassCallCheckForProperties(ILGen il, MethodInfo baseMethod, string name) {
            Label instanceCall = il.DefineLabel();
            LocalBuilder callTarget = il.DeclareLocal(typeof(object));

            // first lookup under the property name
            il.EmitLoadArg(0);
            il.EmitString(name);
            il.Emit(OpCodes.Ldloca, callTarget);
            il.EmitCall(typeof(UserTypeOps), nameof(UserTypeOps.TryGetNonInheritedValueHelper));
            il.Emit(OpCodes.Brtrue, instanceCall);

            // then look up under the method name (get_Foo/set_Foo)
            LocalBuilder methodTarget = EmitNonInheritedMethodLookup(baseMethod.Name, il);
            Label instanceCallMethod = il.DefineLabel();
            il.Emit(OpCodes.Brtrue, instanceCallMethod);

            // method isn't overridden using either form
            EmitBaseMethodDispatch(baseMethod, il);

            // we're calling the get_/set_ method
            il.MarkLabel(instanceCallMethod);
            EmitClrCallStub(il, baseMethod, methodTarget);
            il.Emit(OpCodes.Ret);

            il.MarkLabel(instanceCall);

            // we're accessing a property
            return callTarget;
        }

        private MethodBuilder CreateVTableGetterOverride(MethodInfo mi, string name) {
            MethodBuilder impl;
            ILGen il;
            DefineVTableMethodOverride(mi, out impl, out il);
            LocalBuilder callTarget = EmitBaseClassCallCheckForProperties(il, mi, name);

            il.Emit(OpCodes.Ldloc, callTarget);
            il.EmitLoadArg(0);
            il.EmitString(name);
            il.EmitCall(typeof(UserTypeOps), nameof(UserTypeOps.GetPropertyHelper));

            if (!il.TryEmitImplicitCast(typeof(object), mi.ReturnType)) {
                EmitConvertFromObject(il, mi.ReturnType);
            }
            il.Emit(OpCodes.Ret);
            _tg.DefineMethodOverride(impl, mi);
            return impl;
        }

        /// <summary>
        /// Emit code to convert object to a given type. This code is semantically equivalent
        /// to PythonBinder.EmitConvertFromObject, except this version accepts ILGen whereas
        /// PythonBinder accepts Compiler. The Binder will chagne soon and the two will merge.
        /// </summary>
        private static void EmitConvertFromObject(ILGen il, Type toType) {
            if (toType == typeof(object)) return;
            if (toType.IsGenericParameter) {
                il.EmitCall(typeof(PythonOps).GetMethod(nameof(PythonOps.ConvertFromObject)).MakeGenericMethod(toType));
                return;
            }

            MethodInfo fastConvertMethod = PythonBinder.GetFastConvertMethod(toType);
            if (fastConvertMethod != null) {
                il.EmitCall(fastConvertMethod);
            } else if (toType == typeof(void)) {
                il.Emit(OpCodes.Pop);
            } else if (typeof(Delegate).IsAssignableFrom(toType)) {
                il.EmitType(toType);
                il.EmitCall(typeof(Converter), "ConvertToDelegate");
                il.Emit(OpCodes.Castclass, toType);
            } else {
                Label end = il.DefineLabel();
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Isinst, toType);

                il.Emit(OpCodes.Brtrue_S, end);
                il.Emit(OpCodes.Ldtoken, toType);
                il.EmitCall(PythonBinder.GetGenericConvertMethod(toType));
                il.MarkLabel(end);

                il.Emit(OpCodes.Unbox_Any, toType); //??? this check may be redundant
            }
        }

        private MethodBuilder CreateVTableSetterOverride(MethodInfo mi, string name) {
            MethodBuilder impl;
            ILGen il;
            DefineVTableMethodOverride(mi, out impl, out il);
            LocalBuilder callTarget = EmitBaseClassCallCheckForProperties(il, mi, name);

            il.Emit(OpCodes.Ldloc, callTarget);     // property
            il.EmitLoadArg(0);                      // instance
            il.EmitLoadArg(1);
            il.EmitBoxing(mi.GetParameters()[0].ParameterType);    // newValue
            il.EmitString(name);    // name
            il.EmitCall(typeof(UserTypeOps), nameof(UserTypeOps.SetPropertyHelper));
            il.Emit(OpCodes.Ret);
            _tg.DefineMethodOverride(impl, mi);
            return impl;
        }

        private void CreateVTableEventOverride(MethodInfo mi, string name) {
            // override the add/remove method  
            MethodBuilder impl;
            ILGen il = DefineMethodOverride(mi, out impl);

            LocalBuilder callTarget = EmitBaseClassCallCheckForEvents(il, mi, name);

            il.Emit(OpCodes.Ldloc, callTarget);
            il.EmitLoadArg(0);
            il.EmitLoadArg(1);
            il.EmitBoxing(mi.GetParameters()[0].ParameterType);
            il.EmitString(name);
            il.EmitCall(typeof(UserTypeOps), nameof(UserTypeOps.AddRemoveEventHelper));
            il.Emit(OpCodes.Ret);
            _tg.DefineMethodOverride(impl, mi);
        }

        /// <summary>
        /// Emits code to check if the class has overridden this specific
        /// function.  For example:
        /// 
        /// MyDerivedType.SomeVirtualFunction = ...
        ///     or
        /// 
        /// class MyDerivedType(MyBaseType):
        ///     def SomeVirtualFunction(self, ...):
        /// 
        /// </summary>
        private static LocalBuilder EmitBaseClassCallCheckForEvents(ILGen il, MethodInfo baseMethod, string name) {
            Label instanceCall = il.DefineLabel();
            LocalBuilder callTarget = il.DeclareLocal(typeof(object));

            il.EmitLoadArg(0);
            il.EmitString(name);
            il.Emit(OpCodes.Ldloca, callTarget);
            il.EmitCall(typeof(UserTypeOps), nameof(UserTypeOps.TryGetNonInheritedValueHelper));

            il.Emit(OpCodes.Brtrue, instanceCall);

            EmitBaseMethodDispatch(baseMethod, il);

            il.MarkLabel(instanceCall);

            return callTarget;
        }

        private MethodBuilder CreateVTableMethodOverride(MethodInfo mi, string name) {
            MethodBuilder impl;
            ILGen il;
            DefineVTableMethodOverride(mi, out impl, out il);
            //CompilerHelpers.GetArgumentNames(parameters));  TODO: Set names

            LocalBuilder callTarget = EmitNonInheritedMethodLookup(name, il);
            Label instanceCall = il.DefineLabel();
            il.Emit(OpCodes.Brtrue, instanceCall);

            // lookup failed, call the base class method (this returns or throws)
            EmitBaseMethodDispatch(mi, il);

            // lookup succeeded, call the user defined method & return
            il.MarkLabel(instanceCall);
            EmitClrCallStub(il, mi, callTarget);
            il.Emit(OpCodes.Ret);

            if (mi.IsVirtual && !mi.IsFinal) {
                _tg.DefineMethodOverride(impl, mi);
            }
            return impl;
        }

        private void DefineVTableMethodOverride(MethodInfo mi, out MethodBuilder impl, out ILGen il) {
            if (mi.IsVirtual && !mi.IsFinal) {
                il = DefineMethodOverride(MethodAttributes.Public, mi, out impl);
            } else {
                impl = _tg.DefineMethod(
                    mi.Name,
                    mi.IsVirtual ?
                        (mi.Attributes | MethodAttributes.NewSlot) :
                        ((mi.Attributes & ~MethodAttributes.MemberAccessMask) | MethodAttributes.Public),
                    mi.CallingConvention
                );
                ReflectionUtils.CopyMethodSignature(mi, impl, false);
                il = new ILGen(impl.GetILGenerator());
            }
        }

        /// <summary>
        /// Emits the call to lookup a member defined in the user's type.  Returns
        /// the local which stores the resulting value and leaves a value on the
        /// stack indicating the success of the lookup.
        /// </summary>
        private LocalBuilder EmitNonInheritedMethodLookup(string name, ILGen il) {
            LocalBuilder callTarget = il.DeclareLocal(typeof(object));

            // emit call to helper to do lookup
            il.EmitLoadArg(0);

            if (typeof(IPythonObject).IsAssignableFrom(_baseType)) {
                Debug.Assert(_typeField == null);
                il.EmitPropertyGet(PythonTypeInfo._IPythonObject.PythonType);
            } else {
                il.EmitFieldGet(_typeField);
            }

            il.EmitLoadArg(0);
            il.EmitString(name);
            il.Emit(OpCodes.Ldloca, callTarget);
            il.EmitCall(typeof(UserTypeOps), nameof(UserTypeOps.TryGetNonInheritedMethodHelper));
            return callTarget;
        }

        /// <summary>
        /// Creates a method for doing a base method dispatch.  This is used to support
        /// super(type, obj) calls.
        /// </summary>
        private MethodBuilder CreateSuperCallHelper(MethodInfo mi) {
            MethodAttributes attrs = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName;
            if (mi.IsStatic) {
                attrs |= MethodAttributes.Static;
            }

            MethodBuilder method = _tg.DefineMethod(BaseMethodPrefix + mi.Name, attrs, mi.CallingConvention);
            ReflectionUtils.CopyMethodSignature(mi, method, true);
            EmitBaseMethodDispatch(mi, new ILGen(method.GetILGenerator()));
            return method;
        }

        private KeyValuePair<Type, Dictionary<string, string[]>> SaveType(AssemblyGen ag, string name) {
            _tg = ag.DefinePublicType(TypePrefix + name, _baseType, true);

            Dictionary<string, string[]> specialNames = ImplementType();

            Type ret = FinishType();

            return new KeyValuePair<Type, Dictionary<string, string[]>>(ret, specialNames);
        }

        private Type FinishType() {
            _cctor?.Emit(OpCodes.Ret);

            return _tg.CreateTypeInfo();
        }

        private ILGen DefineExplicitInterfaceImplementation(MethodInfo baseMethod, out MethodBuilder builder) {
            MethodAttributes attrs = baseMethod.Attributes & ~(MethodAttributes.Abstract | MethodAttributes.Public);
            attrs |= MethodAttributes.NewSlot | MethodAttributes.Final;

            Type[] baseSignature = ReflectionUtils.GetParameterTypes(baseMethod.GetParameters());
            builder = _tg.DefineMethod(
                baseMethod.DeclaringType.Name + "." + baseMethod.Name,
                attrs,
                baseMethod.ReturnType,
                baseSignature
            );
            return new ILGen(builder.GetILGenerator());
        }

        private const MethodAttributes MethodAttributesToEraseInOveride = MethodAttributes.Abstract | MethodAttributes.ReservedMask;

        private ILGen DefineMethodOverride(MethodAttributes extra, Type type, string name, out MethodInfo decl, out MethodBuilder impl) {
            decl = type.GetMethod(name);
            return DefineMethodOverride(extra, decl, out impl);
        }

        private ILGen DefineMethodOverride(MethodInfo decl, out MethodBuilder impl) {
            return DefineMethodOverride(MethodAttributes.PrivateScope, decl, out impl);
        }

        private ILGen DefineMethodOverride(MethodAttributes extra, MethodInfo decl, out MethodBuilder impl) {
            impl = ReflectionUtils.DefineMethodOverride(_tg, extra, decl);
            return new ILGen(impl.GetILGenerator());
        }

        /// <summary>
        /// Generates stub to receive the CLR call and then call the dynamic language code.
        /// This code is same as StubGenerator.cs in the Microsoft.Scripting, except it
        /// accepts ILGen instead of Compiler.
        /// </summary>
        private void EmitClrCallStub(ILGen il, MethodInfo mi, LocalBuilder callTarget) {
            int firstArg = 0;
            bool list = false;              // The list calling convention
            bool context = false;           // Context is an argument

            ParameterInfo[] pis = mi.GetParameters();
            if (pis.Length > 0) {
                if (pis[0].ParameterType == typeof(CodeContext)) {
                    firstArg = 1;
                    context = true;
                }
                if (pis[pis.Length - 1].IsDefined(typeof(ParamArrayAttribute), false)) {
                    list = true;
                }
            }
            ParameterInfo[] args = pis;
            int nargs = args.Length - firstArg;
            Type[] genericArgs = mi.GetGenericArguments();

            // Create the action
            ILGen cctor = GetCCtor();
            if (list || genericArgs.Length > 0) {
                // Use a complex call signature that includes param array and keywords
                cctor.EmitInt(nargs);
                cctor.EmitBoolean(list);

                // Emit an array of keyword names:
                cctor.EmitInt(genericArgs.Length);
                cctor.Emit(OpCodes.Newarr, typeof(string));
                for (int i = 0; i < genericArgs.Length; i++) {
                    cctor.Emit(OpCodes.Dup);
                    cctor.EmitInt(i);
                    cctor.Emit(OpCodes.Ldelema, typeof(string));
                    cctor.Emit(OpCodes.Ldstr, genericArgs[i].Name);
                    cctor.Emit(OpCodes.Stobj, typeof(string));
                }
                cctor.EmitCall(typeof(PythonOps).GetMethod(nameof(PythonOps.MakeComplexCallAction)));
            } else {
                cctor.EmitInt(nargs);
                cctor.EmitCall(typeof(PythonOps).GetMethod(nameof(PythonOps.MakeSimpleCallAction)));
            }

            // Create the dynamic site
            Type siteType = CompilerHelpers.MakeCallSiteType(MakeSiteSignature(nargs + genericArgs.Length));
            FieldBuilder site = _tg.DefineField("site$" + _site++, siteType, FieldAttributes.Private | FieldAttributes.Static);
            cctor.EmitCall(siteType.GetMethod("Create"));
            cctor.EmitFieldSet(site);

            List<ReturnFixer> fixers = new List<ReturnFixer>(0);

            //
            // Emit the site invoke
            //
            il.EmitFieldGet(site);
            FieldInfo target = siteType.GetDeclaredField("Target");
            il.EmitFieldGet(target);
            il.EmitFieldGet(site);

            // Emit the code context
            EmitCodeContext(il, context);

            il.Emit(OpCodes.Ldloc, callTarget);

            for (int i = firstArg; i < args.Length; i++) {
                ReturnFixer rf = ReturnFixer.EmitArgument(il, args[i], i + 1);
                if (rf != null) {
                    fixers.Add(rf);
                }
            }

            for (int i = 0; i < genericArgs.Length; i++) {
                il.EmitType(genericArgs[i]);
                il.EmitCall(typeof(DynamicHelpers).GetMethod(nameof(DynamicHelpers.GetPythonTypeFromType)));
            }

            il.EmitCall(target.FieldType, "Invoke");

            foreach (ReturnFixer rf in fixers) {
                rf.FixReturn(il);
            }

            EmitConvertFromObject(il, mi.ReturnType);
        }

        private static void EmitCodeContext(ILGen il, bool context) {
            if (context) {
                il.EmitLoadArg(1);
            } else {
                il.EmitPropertyGet(typeof(DefaultContext).GetProperty(nameof(DefaultContext.Default)));
            }
        }

        private static void EmitInt(ILGenerator ilg, int iVal) {
            switch (iVal) {
                case 0: ilg.Emit(OpCodes.Ldc_I4_0); break;
                case 1: ilg.Emit(OpCodes.Ldc_I4_1); break;
                case 2: ilg.Emit(OpCodes.Ldc_I4_2); break;
                case 3: ilg.Emit(OpCodes.Ldc_I4_3); break;
                case 4: ilg.Emit(OpCodes.Ldc_I4_4); break;
                case 5: ilg.Emit(OpCodes.Ldc_I4_5); break;
                case 6: ilg.Emit(OpCodes.Ldc_I4_6); break;
                case 7: ilg.Emit(OpCodes.Ldc_I4_7); break;
                case 8: ilg.Emit(OpCodes.Ldc_I4_8); break;
                default: ilg.Emit(OpCodes.Ldc_I4, iVal); break;
            }
        }

        private static Type[] MakeSiteSignature(int nargs) {
            Type[] sig = new Type[nargs + 4];
            sig[0] = typeof(CallSite);
            sig[1] = typeof(CodeContext);
            for (int i = 2; i < sig.Length; i++) {
                sig[i] = typeof(object);
            }
            return sig;
        }

        /// <summary>
        /// Same as the DLR ReturnFixer, but accepts lower level constructs,
        /// such as LocalBuilder, ParameterInfos and ILGen.
        /// </summary>
        private sealed class ReturnFixer {
            private readonly ParameterInfo _parameter;
            private readonly LocalBuilder _reference;
            private readonly int _index;

            private ReturnFixer(LocalBuilder reference, ParameterInfo parameter, int index) {
                Debug.Assert(reference.LocalType.IsGenericType && reference.LocalType.GetGenericTypeDefinition() == typeof(StrongBox<>));
                Debug.Assert(parameter.ParameterType.IsByRef);

                _parameter = parameter;
                _reference = reference;
                _index = index;
            }

            public void FixReturn(ILGen il) {
                il.EmitLoadArg(_index);
                il.Emit(OpCodes.Ldloc, _reference);
                il.EmitFieldGet(_reference.LocalType.GetDeclaredField("Value"));
                il.EmitStoreValueIndirect(_parameter.ParameterType.GetElementType());
            }

            public static ReturnFixer EmitArgument(ILGen il, ParameterInfo parameter, int index) {
                il.EmitLoadArg(index);
                if (parameter.ParameterType.IsByRef) {
                    Type elementType = parameter.ParameterType.GetElementType();
                    Type concreteType = typeof(StrongBox<>).MakeGenericType(elementType);
                    LocalBuilder refSlot = il.DeclareLocal(concreteType);
                    il.EmitLoadValueIndirect(elementType);
                    ConstructorInfo ci = concreteType.GetConstructor(new Type[] { elementType });
                    il.Emit(OpCodes.Newobj, ci);
                    il.Emit(OpCodes.Stloc, refSlot);
                    il.Emit(OpCodes.Ldloc, refSlot);
                    return new ReturnFixer(refSlot, parameter, index);
                } else {
                    il.EmitBoxing(parameter.ParameterType);
                    return null;
                }
            }
        }
#endif
        #endregion

        #region Base type helper data updates

        private static void AddBaseMethods(Type finishedType, Dictionary<string, string[]> specialNames) {
            // "Adds" base methods to super type - this makes super(...).xyz to work - otherwise 
            // we'd return a function that did a virtual call resulting in a stack overflow.
            // The addition is to a seperate cache that NewTypeMaker maintains.  TypeInfo consults this
            // cache when doing member lookup and includes these members in the returned members.
            foreach (MethodInfo mi in finishedType.GetMethods()) {
                if (IsInstanceType(finishedType.BaseType) && IsInstanceType(mi.DeclaringType)) continue;

                string methodName = mi.Name;
                if (methodName.StartsWith(BaseMethodPrefix, StringComparison.Ordinal) || methodName.StartsWith(FieldGetterPrefix, StringComparison.Ordinal) || methodName.StartsWith(FieldSetterPrefix, StringComparison.Ordinal)) {
                    foreach (string newName in GetBaseName(mi, specialNames)) {
                        if (mi.IsSpecialName && (newName.StartsWith("get_", StringComparison.Ordinal) || newName.StartsWith("set_", StringComparison.Ordinal))) {
                            // if it's a property we want to override it
                            string propName = newName.Substring(4);

                            MemberInfo[] defaults = finishedType.BaseType.GetDefaultMembers();
                            if (defaults.Length > 0) {
                                // if it's an indexer then we want to override get_Item/set_Item methods
                                // which map to __getitem__ and __setitem__ as normal Python methods.
                                foreach (MemberInfo method in defaults) {
                                    if (method.Name == propName) {
                                        StoreOverriddenMethod(mi, newName);
                                        break;
                                    }
                                }
                            }

                            StoreOverriddenProperty(mi, newName);
                        } else if (mi.IsSpecialName && (newName.StartsWith(FieldGetterPrefix, StringComparison.Ordinal) || newName.StartsWith(FieldSetterPrefix, StringComparison.Ordinal))) {
                            StoreOverriddenField(mi, newName);
                        } else {
                            // not a property, just store the overridden method.
                            StoreOverriddenMethod(mi, newName);
                        }
                    }
                }
            }
        }

        private static void StoreOverriddenField(MethodInfo mi, string newName) {
            Type baseType = mi.DeclaringType.BaseType;
            string fieldName = newName.Substring(FieldGetterPrefix.Length); // get_ or set_
            lock (PythonTypeOps._propertyCache) {
                foreach (FieldInfo pi in baseType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy)) {
                    if (pi.Name == fieldName) {
                        if (newName.StartsWith(FieldGetterPrefix, StringComparison.Ordinal)) {
                            AddPropertyInfo(pi.DeclaringType, fieldName, mi, null);
                        } else if (newName.StartsWith(FieldSetterPrefix, StringComparison.Ordinal)) {
                            AddPropertyInfo(pi.DeclaringType, fieldName, null, mi);
                        }
                    }
                }
            }
        }

        private static ExtensionPropertyTracker AddPropertyInfo(Type baseType, string/*!*/ propName, MethodInfo get, MethodInfo set) {
            MethodInfo mi = get ?? set;

            Dictionary<string, List<ExtensionPropertyTracker>> propInfoList;

            if (!_overriddenProperties.TryGetValue(baseType, out propInfoList)) {
                _overriddenProperties[baseType] = propInfoList = new Dictionary<string, List<ExtensionPropertyTracker>>();
            }

            List<ExtensionPropertyTracker> trackers;

            if (!propInfoList.TryGetValue(propName, out trackers)) {
                propInfoList[propName] = trackers = new List<ExtensionPropertyTracker>();
            }

            ExtensionPropertyTracker res;
            for (int i = 0; i < trackers.Count; i++) {
                if (trackers[i].DeclaringType == mi.DeclaringType) {
                    trackers[i] = res = new ExtensionPropertyTracker(
                        propName,
                        get ?? trackers[i].GetGetMethod(),
                        set ?? trackers[i].GetSetMethod(),
                        null,
                        mi.DeclaringType
                    );
                    return res;
                }
            }

            trackers.Add(
                res = new ExtensionPropertyTracker(
                    propName,
                    mi,
                    null,
                    null,
                    mi.DeclaringType
                )
            );

            return res;
        }

        private static void StoreOverriddenMethod(MethodInfo mi, string newName) {
            Type baseType = mi.DeclaringType.BaseType;

            MemberInfo[] members = baseType.GetMember(newName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            Debug.Assert(members.Length > 0, String.Format("{0} from {1}", newName, baseType.Name));
            Type declType = ((MethodInfo)members[0]).GetBaseDefinition().DeclaringType;

            string pythonName = newName;
            switch (newName) {
                case "get_Item": pythonName = "__getitem__"; break;
                case "set_Item": pythonName = "__setitem__"; break;
            }

            // back-patch any existing functions so that cached rules will work
            // when called again...
            lock (PythonTypeOps._functions) {
                foreach (BuiltinFunction bf in PythonTypeOps._functions.Values) {
                    if (bf.Name == pythonName && bf.DeclaringType == declType) {
                        bf.AddMethod(mi);
                        break;
                    }
                }

                Dictionary<string, List<MethodInfo>> overrideInfo;
                if (!_overriddenMethods.TryGetValue(declType, out overrideInfo)) {
                    _overriddenMethods[declType] = overrideInfo = new Dictionary<string, List<MethodInfo>>();
                }

                List<MethodInfo> methods;
                if (!overrideInfo.TryGetValue(pythonName, out methods)) {
                    overrideInfo[pythonName] = methods = new List<MethodInfo>();
                }

                methods.Add(mi);
            }
        }

        private static void StoreOverriddenProperty(MethodInfo mi, string newName) {
            Type baseType = mi.DeclaringType.BaseType;

            lock (PythonTypeOps._propertyCache) {
                string propName = newName.Substring(4); // get_ or set_
                ExtensionPropertyTracker newProp = null;
                foreach (PropertyInfo pi in baseType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy)) {
                    if (pi.Name == propName) {
                        Type declType = pi.DeclaringType;
                        if (newName.StartsWith("get_", StringComparison.Ordinal)) {
                            newProp = AddPropertyInfo(declType, propName, mi, null);
                        } else if (newName.StartsWith("set_", StringComparison.Ordinal)) {
                            newProp = AddPropertyInfo(declType, propName, null, mi);
                        }
                    }
                }

                if (newProp != null) {
                    // back-patch any existing properties so that cached rules will work
                    // when called again...

                    foreach (ReflectedGetterSetter rg in PythonTypeOps._propertyCache.Values) {
                        if (rg.DeclaringType != baseType ||
                            rg.__name__ != newProp.Name) {
                            continue;
                        }

                        if (newProp.GetGetMethod(true) != null) {
                            rg.AddGetter(newProp.GetGetMethod(true));
                        }

                        if (newProp.GetSetMethod(true) != null) {
                            rg.AddSetter(newProp.GetSetMethod(true));
                        }
                    }

                }
            }
        }

        private static IEnumerable<string> GetBaseName(MethodInfo mi, Dictionary<string, string[]> specialNames) {
            string newName;
            if (mi.Name.StartsWith(BaseMethodPrefix, StringComparison.Ordinal)) {
                newName = mi.Name.Substring(BaseMethodPrefix.Length);
            } else if (mi.Name.StartsWith(FieldGetterPrefix, StringComparison.Ordinal)) {
                newName = mi.Name.Substring(FieldGetterPrefix.Length);
            } else if (mi.Name.StartsWith(FieldSetterPrefix, StringComparison.Ordinal)) {
                newName = mi.Name.Substring(FieldSetterPrefix.Length);
            } else {
                throw new InvalidOperationException();
            }

            Debug.Assert(specialNames.ContainsKey(newName));

            return specialNames[newName];
        }

        /// <summary>
        /// Called from PythonTypeOps - the BuiltinFunction._function lock must be held.
        /// </summary>
        internal static IList<MethodInfo> GetOverriddenMethods(Type type, string name) {
            Dictionary<string, List<MethodInfo>> methods;
            List<MethodInfo> res = null;
            Type curType = type;
            while (curType != null) {
                if (_overriddenMethods.TryGetValue(curType, out methods)) {
                    List<MethodInfo> methodList;
                    if (methods.TryGetValue(name, out methodList)) {
                        if (res == null) {
                            res = new List<MethodInfo>(methodList.Count);
                        }
                        foreach (MethodInfo method in methodList) {
                            if (type.IsAssignableFrom(method.DeclaringType)) {
                                res.Add(method);
                            }
                        }
                    }
                }
                curType = curType.BaseType;
            }
            if (res != null) {
                return res;
            }

            return Array.Empty<MethodInfo>();
        }

        internal static IList<ExtensionPropertyTracker> GetOverriddenProperties(Type type, string name) {
            lock (_overriddenProperties) {
                Dictionary<string, List<ExtensionPropertyTracker>> props;
                if (_overriddenProperties.TryGetValue(type, out props)) {
                    List<ExtensionPropertyTracker> propList;
                    if (props.TryGetValue(name, out propList)) {
                        return propList;
                    }
                }
            }

            return Array.Empty<ExtensionPropertyTracker>();
        }

        #endregion

    }
}
