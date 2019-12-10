// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Dynamic;
using System.Linq;
using IronPython.Runtime.Binding;
using IronPython.Runtime.Types;
using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime.Operations {
    internal static class PythonTypeOps {
        private static readonly Dictionary<FieldInfo, PythonTypeSlot> _fieldCache = new Dictionary<FieldInfo, PythonTypeSlot>();
        private static readonly Dictionary<BuiltinFunction, BuiltinMethodDescriptor> _methodCache = new Dictionary<BuiltinFunction, BuiltinMethodDescriptor>();
        private static readonly Dictionary<BuiltinFunction, ClassMethodDescriptor> _classMethodCache = new Dictionary<BuiltinFunction, ClassMethodDescriptor>();
        internal static readonly Dictionary<BuiltinFunctionKey, BuiltinFunction> _functions = new Dictionary<BuiltinFunctionKey, BuiltinFunction>();
        private static readonly Dictionary<ReflectionCache.MethodBaseCache, ConstructorFunction> _ctors = new Dictionary<ReflectionCache.MethodBaseCache, ConstructorFunction>();
        private static readonly Dictionary<EventTracker, ReflectedEvent> _eventCache = new Dictionary<EventTracker, ReflectedEvent>();
        internal static readonly Dictionary<PropertyTracker, ReflectedGetterSetter> _propertyCache = new Dictionary<PropertyTracker, ReflectedGetterSetter>();

        internal static PythonTuple MroToPython(IList<PythonType> types) {
            List<object> res = new List<object>(types.Count);
            foreach (PythonType dt in types) {
                if (dt.UnderlyingSystemType == typeof(ValueType)) continue; // hide value type
                res.Add(dt);
            }

            return PythonTuple.Make(res);
        }

        internal static string GetModuleName(CodeContext/*!*/ context, Type type) {
            Type curType = type;
            while (curType != null) {
                string moduleName;
                if (context.LanguageContext.BuiltinModuleNames.TryGetValue(curType, out moduleName)) {
                    return moduleName;
                }

                curType = curType.DeclaringType;
            }

            FieldInfo modField = type.GetField("__module__");
            if (modField != null && modField.IsLiteral && modField.FieldType == typeof(string)) {
                return (string)modField.GetRawConstantValue();
            }

            return "builtins";
        }

        internal static object CallParams(CodeContext/*!*/ context, PythonType cls, params object[] args\u03c4) {
            if (args\u03c4 == null) args\u03c4 = ArrayUtils.EmptyObjects;

            return CallWorker(context, cls, args\u03c4);
        }

        internal static object CallWorker(CodeContext/*!*/ context, PythonType dt, object[] args) {
            object newObject = PythonOps.CallWithContext(context, GetTypeNew(context, dt), ArrayUtils.Insert<object>(dt, args));

            if (ShouldInvokeInit(dt, DynamicHelpers.GetPythonType(newObject), args.Length)) {
                PythonOps.CallWithContext(context, GetInitMethod(context, dt, newObject), args);

                AddFinalizer(context, dt, newObject);
            }

            return newObject;
        }

        internal static object CallWorker(CodeContext/*!*/ context, PythonType dt, IDictionary<string, object> kwArgs, object[] args) {
            object[] allArgs = ArrayOps.CopyArray(args, kwArgs.Count + args.Length);
            string[] argNames = new string[kwArgs.Count];

            int i = args.Length;
            foreach (KeyValuePair<string, object> kvp in kwArgs) {
                allArgs[i] = kvp.Value;
                argNames[i++ - args.Length] = kvp.Key;
            }

            return CallWorker(context, dt, new KwCallInfo(allArgs, argNames));        
        }

        internal static object CallWorker(CodeContext/*!*/ context, PythonType dt, KwCallInfo args) {
            object[] clsArgs = ArrayUtils.Insert<object>(dt, args.Arguments);
            object newObject = PythonOps.CallWithKeywordArgs(context,
                GetTypeNew(context, dt),
                clsArgs,
                args.Names);

            if (newObject == null) return null;

            if (ShouldInvokeInit(dt, DynamicHelpers.GetPythonType(newObject), args.Arguments.Length)) {
                PythonOps.CallWithKeywordArgs(context, GetInitMethod(context, dt, newObject), args.Arguments, args.Names);

                AddFinalizer(context, dt, newObject);
            }

            return newObject;
        }

        /// <summary>
        /// Looks up __init__ avoiding calls to __getattribute__ and handling both
        /// new-style and old-style classes in the MRO.
        /// </summary>
        private static object GetInitMethod(CodeContext/*!*/ context, PythonType dt, object newObject) {
            // __init__ is never searched for w/ __getattribute__
            for (int i = 0; i < dt.ResolutionOrder.Count; i++) {
                PythonType cdt = dt.ResolutionOrder[i];
                PythonTypeSlot dts;
                object value;
               
                if (cdt.TryLookupSlot(context, "__init__", out dts) &&
                    dts.TryGetValue(context, newObject, dt, out value)) {
                    return value;
                }

            }
            return null;
        }


        private static void AddFinalizer(CodeContext/*!*/ context, PythonType dt, object newObject) {
            // check if object has finalizer...
            PythonTypeSlot dummy;
            if (dt.TryResolveSlot(context, "__del__", out dummy)) {
                IWeakReferenceable iwr = context.LanguageContext.ConvertToWeakReferenceable(newObject);
                Debug.Assert(iwr != null);

                InstanceFinalizer nif = new InstanceFinalizer(context, newObject);
                iwr.SetFinalizer(new WeakRefTracker(iwr, nif, nif));
            }
        }

        private static object GetTypeNew(CodeContext/*!*/ context, PythonType dt) {
            PythonTypeSlot dts;

            if (!dt.TryResolveSlot(context, "__new__", out dts)) {
                throw PythonOps.TypeError("cannot create instances of {0}", dt.Name);
            }

            object newInst;
            bool res = dts.TryGetValue(context, dt, dt, out newInst);
            Debug.Assert(res);

            return newInst;
        }

        internal static bool IsRuntimeAssembly(Assembly assembly) {
            if (assembly == typeof(PythonOps).Assembly || // IronPython.dll
                assembly == typeof(Microsoft.Scripting.Interpreter.LightCompiler).Assembly || // Microsoft.Scripting.dll
                assembly == typeof(DynamicMetaObject).Assembly) {  // Microsoft.Scripting.Core.dll
                return true;
            }

            AssemblyName assemblyName = new AssemblyName(assembly.FullName);
            if (assemblyName.Name.Equals("IronPython.Modules")) { // IronPython.Modules.dll
                return true;
            }

            return false;
        }

        private static bool ShouldInvokeInit(PythonType cls, PythonType newObjectType, int argCnt) {
            // don't run __init__ if it's not a subclass of ourselves,
            // or if this is the user doing type(x), or if it's a standard
            // .NET type which doesn't have an __init__ method (this is a perf optimization)
            return (!cls.IsSystemType || cls.IsPythonType) &&
                newObjectType.IsSubclassOf(cls) &&                
                (cls != TypeCache.PythonType || argCnt > 1);
        }

        // note: returns "instance" rather than type name if o is an OldInstance
        internal static string GetName(object o) {
            // Resolve Namespace-Tracker name, which would end in `namespace#` if 
            // it is not handled indivdualy
            if (o is NamespaceTracker)
            {
                return ((NamespaceTracker)o).Name;
            }

            return DynamicHelpers.GetPythonType(o).Name;
        }

        internal static PythonType[] ObjectTypes(object[] args) {
            PythonType[] types = new PythonType[args.Length];
            for (int i = 0; i < args.Length; i++) {
                types[i] = DynamicHelpers.GetPythonType(args[i]);
            }
            return types;
        }

        internal static Type[] ConvertToTypes(PythonType[] pythonTypes) {
            Type[] types = new Type[pythonTypes.Length];
            for (int i = 0; i < pythonTypes.Length; i++) {
                types[i] = ConvertToType(pythonTypes[i]);
            }
            return types;
        }

        private static Type ConvertToType(PythonType pythonType) {
            if (pythonType.IsNull) {
                return typeof(DynamicNull);
            } else {
                return pythonType.UnderlyingSystemType;
            }
        }

        internal static TrackerTypes GetMemberType(MemberGroup members) {
            TrackerTypes memberType = TrackerTypes.All;
            for (int i = 0; i < members.Count; i++) {
                MemberTracker mi = members[i];
                if (mi.MemberType != memberType) {
                    if (memberType != TrackerTypes.All) {
                        return TrackerTypes.All;
                    }
                    memberType = mi.MemberType;
                }
            }

            return memberType;
        }


        internal static PythonTypeSlot/*!*/ GetSlot(MemberGroup group, string name, bool privateBinding) {
            if (group.Count == 0) {
                return null;
            }

            group = FilterNewSlots(group);
            TrackerTypes tt = GetMemberType(group);
            switch(tt) {
                case TrackerTypes.Method:
                    bool checkStatic = false;
                    List<MemberInfo> mems = new List<MemberInfo>();
                    foreach (MemberTracker mt in group) {
                        MethodTracker metht = (MethodTracker)mt;
                        mems.Add(metht.Method);
                        checkStatic |= metht.IsStatic;
                    }

                    Type declType = group[0].DeclaringType;
                    MemberInfo[] memArray = mems.ToArray(); 
                    FunctionType ft = GetMethodFunctionType(declType, memArray, checkStatic);
                    return GetFinalSlotForFunction(GetBuiltinFunction(declType, group[0].Name, name, ft, memArray));
                
                case TrackerTypes.Field:
                    return GetReflectedField(((FieldTracker)group[0]).Field);
                
                case TrackerTypes.Property:
                    return GetReflectedProperty((PropertyTracker)group[0], group, privateBinding);       
                
                case TrackerTypes.Event:
                    return GetReflectedEvent(((EventTracker)group[0]));
                
                case TrackerTypes.Type:
                    TypeTracker type = (TypeTracker)group[0];
                    for (int i = 1; i < group.Count; i++) {
                        type = TypeGroup.UpdateTypeEntity(type, (TypeTracker)group[i]);
                    }
                    
                    if (type is TypeGroup) {
                        return new PythonTypeUserDescriptorSlot(type, true);
                    }

                    return new PythonTypeUserDescriptorSlot(DynamicHelpers.GetPythonTypeFromType(type.Type), true);
                
                case TrackerTypes.Constructor:
                    return GetConstructorFunction(group[0].DeclaringType, privateBinding);
                
                case TrackerTypes.Custom:
                    return ((PythonCustomTracker)group[0]).GetSlot();
                
                default:
                    // if we have a new slot in the derived class filter out the 
                    // members from the base class.
                    throw new InvalidOperationException(String.Format("Bad member type {0} on {1}.{2}", tt.ToString(), group[0].DeclaringType, name));
            }
        }

        internal static MemberGroup FilterNewSlots(MemberGroup group) {
            if (GetMemberType(group) == TrackerTypes.All) {
                Type declType = group[0].DeclaringType;
                for (int i = 1; i < group.Count; i++) {
                    if (group[i].DeclaringType != declType) {
                        if (group[i].DeclaringType.IsSubclassOf(declType)) {
                            declType = group[i].DeclaringType;
                        }
                    }
                }
                List<MemberTracker> trackers = new List<MemberTracker>();

                for (int i = 0; i < group.Count; i++) {
                    if (group[i].DeclaringType == declType) {
                        trackers.Add(group[i]);
                    }
                }

                if (trackers.Count != group.Count) {
                    return new MemberGroup(trackers.ToArray());
                }
            }

            return group;
        }

        private static BuiltinFunction GetConstructorFunction(Type t, bool privateBinding) {
            BuiltinFunction ctorFunc = InstanceOps.NonDefaultNewInst;
            MethodBase[] ctors = GetConstructors(t, privateBinding, true);

            return GetConstructor(t, ctorFunc, ctors);
        }
        
        internal static MethodBase[] GetConstructors(Type t, bool privateBinding, bool includeProtected = false) {
            MethodBase[] ctors = CompilerHelpers.GetConstructors(t, privateBinding, includeProtected);
            if (t.IsEnum) {
                var enumCtor = typeof(PythonTypeOps).GetDeclaredMethods(nameof(CreateEnum)).Single().MakeGenericMethod(t);
                ctors = ctors.Concat(new[] { enumCtor }).ToArray();
            }
            return ctors;
        }
         // support for EnumType(number)
        private static T CreateEnum<T>(object value) {
            if (value == null) {
                throw PythonOps.ValueError(
                    $"None is not a valid {PythonOps.ToString(typeof(T))}"
                );
            }
            try {
                return (T)Enum.ToObject(typeof(T), value);
            } catch (ArgumentException) {
                throw PythonOps.ValueError(
                    $"{PythonOps.ToString(value)} is not a valid {PythonOps.ToString(typeof(T))}"
                );
            }
        }

        internal static bool IsDefaultNew(MethodBase[] targets) {
            if (targets.Length == 1) {
                ParameterInfo[] pis = targets[0].GetParameters();
                if (pis.Length == 0) {
                    return true;
                }

                if (pis.Length == 1 && pis[0].ParameterType == typeof(CodeContext)) {
                    return true;
                }
            }

            return false;
        }

        internal static BuiltinFunction GetConstructorFunction(Type type, string name) {
            List<MethodBase> methods = new List<MethodBase>();
            bool hasDefaultConstructor = false;

            foreach (ConstructorInfo ci in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)) {
                if (ci.IsPublic) {
                    if (ci.GetParameters().Length == 0) {
                        hasDefaultConstructor = true;
                    }

                    methods.Add(ci);
                }
            }
            
            if (type.IsValueType && !hasDefaultConstructor && type != typeof(void)) {
                try {
                    methods.Add(typeof(ScriptingRuntimeHelpers).GetMethod(nameof(ScriptingRuntimeHelpers.CreateInstance), ReflectionUtils.EmptyTypes).MakeGenericMethod(type));
                } catch (BadImageFormatException) {
                    // certain types (e.g. ArgIterator) won't survive the above call.
                    // we won't let you create instances of these types.
                }
            }

            if (methods.Count > 0) {
                return BuiltinFunction.MakeFunction(name, methods.ToArray(), type);
            }

            return null;
        }

        internal static ReflectedEvent GetReflectedEvent(EventTracker tracker) {
            ReflectedEvent res;
            lock (_eventCache) {
                if (!_eventCache.TryGetValue(tracker, out res)) {
                    if (PythonBinder.IsExtendedType(tracker.DeclaringType)) {
                        _eventCache[tracker] = res = new ReflectedEvent(tracker, true);
                    } else {
                        _eventCache[tracker] = res = new ReflectedEvent(tracker, false);
                    }
                }
            }
            return res;
        }

        internal static PythonTypeSlot/*!*/ GetFinalSlotForFunction(BuiltinFunction/*!*/ func) {
            if ((func.FunctionType & FunctionType.Method) != 0) {
                BuiltinMethodDescriptor desc;
                lock (_methodCache) {
                    if (!_methodCache.TryGetValue(func, out desc)) {
                        _methodCache[func] = desc = new BuiltinMethodDescriptor(func);
                    }

                    return desc;
                }
            }

            if (func.Targets[0].IsDefined(typeof(ClassMethodAttribute), true)) {
                lock (_classMethodCache) {
                    ClassMethodDescriptor desc;
                    if (!_classMethodCache.TryGetValue(func, out desc)) {
                        _classMethodCache[func] = desc = new ClassMethodDescriptor(func);
                    }

                    return desc;
                }
            }

            return func;
        }

        internal static BuiltinFunction/*!*/ GetBuiltinFunction(Type/*!*/ type, string/*!*/ name, MemberInfo/*!*/[]/*!*/ mems) {
            return GetBuiltinFunction(type, name, null, mems);
        }

#pragma warning disable 414  // unused fields - they're used by GetHashCode()
        internal readonly struct BuiltinFunctionKey {
            private readonly Type DeclaringType;
            private readonly ReflectionCache.MethodBaseCache Cache;
            private readonly FunctionType FunctionType;

            public BuiltinFunctionKey(Type declaringType, ReflectionCache.MethodBaseCache cache, FunctionType funcType) {
                Cache = cache;
                FunctionType = funcType;
                DeclaringType = declaringType;
            }
        }
#pragma warning restore 169

        public static MethodBase[] GetNonBaseHelperMethodInfos(MemberInfo[] members) {
            List<MethodBase> res = new List<MethodBase>();
            foreach (MemberInfo mi in members) {
                MethodBase mb = mi as MethodBase;
                if (mb != null && !mb.Name.StartsWith(NewTypeMaker.BaseMethodPrefix)) {
                    res.Add(mb);
                }
            }

            return res.ToArray();
        }

        public static MemberInfo[] GetNonBaseHelperMemberInfos(MemberInfo[] members) {
            List<MemberInfo> res = new List<MemberInfo>(members.Length);
            foreach (MemberInfo mi in members) {
                MethodBase mb = mi as MethodBase;
                if (mb == null || !mb.Name.StartsWith(NewTypeMaker.BaseMethodPrefix)) {
                    res.Add(mi);
                }
            }

            return res.ToArray();
        }

        internal static BuiltinFunction/*!*/ GetBuiltinFunction(Type/*!*/ type, string/*!*/ name, FunctionType? funcType, params MemberInfo/*!*/[]/*!*/ mems) {
            return GetBuiltinFunction(type, name, name, funcType, mems);
        }

        /// <summary>
        /// Gets a builtin function for the given declaring type and member infos.
        /// 
        /// Given the same inputs this always returns the same object ensuring there's only 1 builtinfunction
        /// for each .NET method.
        /// 
        /// This method takes both a cacheName and a pythonName.  The cache name is the real method name.  The pythonName
        /// is the name of the method as exposed to Python.
        /// </summary>
        internal static BuiltinFunction/*!*/ GetBuiltinFunction(Type/*!*/ type, string/*!*/ cacheName, string/*!*/ pythonName, FunctionType? funcType, params MemberInfo/*!*/[]/*!*/ mems) {
            BuiltinFunction res = null;

            if (mems.Length != 0) {
                FunctionType ft = funcType ?? GetMethodFunctionType(type, mems); 
                type = GetBaseDeclaringType(type, mems);

                BuiltinFunctionKey cache = new BuiltinFunctionKey(type, new ReflectionCache.MethodBaseCache(cacheName, GetNonBaseHelperMethodInfos(mems)), ft);

                lock (_functions) {
                    if (!_functions.TryGetValue(cache, out res)) {
                        if (PythonTypeOps.GetFinalSystemType(type) == type) {
                            IList<MethodInfo> overriddenMethods = NewTypeMaker.GetOverriddenMethods(type, cacheName);

                            if (overriddenMethods.Count > 0) {
                                List<MemberInfo> newMems = new List<MemberInfo>(mems);

                                foreach (MethodInfo mi in overriddenMethods) {
                                    newMems.Add(mi);
                                }

                                mems = newMems.ToArray();
                            }
                        }

                        _functions[cache] = res = BuiltinFunction.MakeMethod(pythonName, ReflectionUtils.GetMethodInfos(mems), type, ft);
                    }
                }
            }

            return res;
        }

        private static Type GetCommonBaseType(Type xType, Type yType) {
            if (xType.IsSubclassOf(yType)) {
                return yType;
            } else if (yType.IsSubclassOf(xType)) {
                return xType;
            } else if (xType == yType) {
                return xType;
            }

            Type xBase = xType.BaseType;
            Type yBase = yType.BaseType;
            if (xBase != null) {
                Type res = GetCommonBaseType(xBase, yType);
                if (res != null) {
                    return res;
                }
            }

            if (yBase != null) {
                Type res = GetCommonBaseType(xType, yBase);
                if (res != null) {
                    return res;
                }
            }

            return null;
        }

        private static Type GetBaseDeclaringType(Type type, MemberInfo/*!*/[] mems) {
            // get the base most declaring type, first sort the list so that
            // the most derived class is at the beginning.
            Array.Sort<MemberInfo>(mems, delegate(MemberInfo x, MemberInfo y) {
                if (x.DeclaringType.IsSubclassOf(y.DeclaringType)) {
                    return -1;
                } else if (y.DeclaringType.IsSubclassOf(x.DeclaringType)) {
                    return 1;
                } else if (x.DeclaringType == y.DeclaringType) {
                    return 0;
                }

                // no relationship between these types, they should be base helper
                // methods for two different types - for example object.MemberwiseClone for
                // ExtensibleInt & object.  We need to reset our type to the common base type.
                type = GetCommonBaseType(x.DeclaringType, y.DeclaringType) ?? typeof(object);

                // generic type definitions will have a null name.
                if (x.DeclaringType.FullName == null) {
                    return -1;
                } else if (y.DeclaringType.FullName == null) {
                    return 1;
                }
                return x.DeclaringType.FullName.CompareTo(y.DeclaringType.FullName);
            });

            // then if the provided type is a subclass of the most derived type
            // then our declaring type is the methods declaring type.
            foreach (MemberInfo mb in mems) {
                // skip extension methods
                if (mb.DeclaringType.IsAssignableFrom(type)) {
                    if (type == mb.DeclaringType || type.IsSubclassOf(mb.DeclaringType)) {
                        type = mb.DeclaringType;
                        break;
                    }
                }
            }
            return type;
        }

        internal static ConstructorFunction GetConstructor(Type type, BuiltinFunction realTarget, params MethodBase[] mems) {
            ConstructorFunction res = null;

            if (mems.Length != 0) {
                ReflectionCache.MethodBaseCache cache = new ReflectionCache.MethodBaseCache("__new__", mems);
                lock (_ctors) {
                    if (!_ctors.TryGetValue(cache, out res)) {
                        _ctors[cache] = res = new ConstructorFunction(realTarget, mems);
                    }
                }
            }

            return res;
        }

        internal static FunctionType GetMethodFunctionType(Type/*!*/ type, MemberInfo/*!*/[]/*!*/ methods) {
            return GetMethodFunctionType(type, methods, true);
        }

        internal static FunctionType GetMethodFunctionType(Type/*!*/ type, MemberInfo/*!*/[]/*!*/ methods, bool checkStatic) {
            FunctionType ft = FunctionType.None;
            foreach (MethodInfo mi in methods) {
                if (mi.IsStatic && mi.IsSpecialName) {
                    ParameterInfo[] pis = mi.GetParameters();

                    if ((pis.Length == 2 && pis[0].ParameterType != typeof(CodeContext)) || 
                        (pis.Length == 3 && pis[0].ParameterType == typeof(CodeContext))) {
                        ft |= FunctionType.BinaryOperator;

                        if (pis[pis.Length - 2].ParameterType != type && pis[pis.Length - 1].ParameterType == type) {
                            ft |= FunctionType.ReversedOperator;
                        }
                    }
                }

                if (checkStatic && IsStaticFunction(type, mi)) {
                    ft |= FunctionType.Function;
                } else {
                    ft |= FunctionType.Method;
                }
            }

            if (IsMethodAlwaysVisible(type, methods)) {
                ft |= FunctionType.AlwaysVisible;
            }

            return ft;
        }

        /// <summary>
        /// Checks to see if the provided members are always visible for the given type.
        /// 
        /// This filters out methods such as GetHashCode and Equals on standard .NET 
        /// types that we expose directly as Python types (e.g. object, string, etc...).
        /// 
        /// It also filters out the base helper overrides that are added for supporting
        /// super calls on user defined types.
        /// </summary>
        private static bool IsMethodAlwaysVisible(Type/*!*/ type, MemberInfo/*!*/[]/*!*/ methods) {
            bool alwaysVisible = true;
            if (PythonBinder.IsPythonType(type)) {
                // only show methods defined outside of the system types (object, string)
                foreach (MethodInfo mi in methods) {
                    if (PythonBinder.IsExtendedType(mi.DeclaringType) ||
                        PythonBinder.IsExtendedType(mi.GetBaseDefinition().DeclaringType) ||
                        PythonHiddenAttribute.IsHidden(mi)) {
                        alwaysVisible = false;
                        break;
                    }
                }
            } else if (typeof(IPythonObject).IsAssignableFrom(type)) {
                // check if this is a virtual override helper, if so we
                // may need to filter it out.
                foreach (MethodInfo mi in methods) {
                    if (PythonBinder.IsExtendedType(mi.DeclaringType)) {
                        alwaysVisible = false;
                        break;
                    }
                }

            }
            return alwaysVisible;
        }

        /// <summary>
        /// a function is static if it's a static .NET method and it's defined on the type or is an extension method 
        /// with StaticExtensionMethod decoration.
        /// </summary>
        private static bool IsStaticFunction(Type type, MethodInfo mi) {            
            return mi.IsStatic &&       // method must be truly static
                !mi.IsDefined(typeof(WrapperDescriptorAttribute), false) &&     // wrapper descriptors are instance methods
                (mi.DeclaringType.IsAssignableFrom(type) || mi.IsDefined(typeof(StaticExtensionMethodAttribute), false)); // or it's not an extension method or it's a static extension method
        }

        internal static PythonTypeSlot GetReflectedField(FieldInfo info) {
            PythonTypeSlot res;

            NameType nt = NameType.Field;
            if (!PythonBinder.IsExtendedType(info.DeclaringType) && 
                !PythonHiddenAttribute.IsHidden(info)) {
                nt |= NameType.PythonField;
            }

            lock (_fieldCache) {
                if (!_fieldCache.TryGetValue(info, out res)) {
                    if (nt == NameType.PythonField && info.IsLiteral) {
                        if (info.FieldType == typeof(int)) {
                            res = new PythonTypeUserDescriptorSlot(
                                ScriptingRuntimeHelpers.Int32ToObject((int)info.GetRawConstantValue()),
                                true
                            );
                        } else if (info.FieldType == typeof(bool)) {
                            res = new PythonTypeUserDescriptorSlot(
                                ScriptingRuntimeHelpers.BooleanToObject((bool)info.GetRawConstantValue()),
                                true
                            );
                        } else {
                            res = new PythonTypeUserDescriptorSlot(
                                info.GetValue(null),
                                true
                            );
                        }
                    } else {
                        res = new ReflectedField(info, nt);
                    }

                    _fieldCache[info] = res;
                }
            }

            return res;
        }

        internal static string GetDocumentation(Type type) {
            // Python documentation
            object[] docAttr = type.GetCustomAttributes(typeof(DocumentationAttribute), false);
            if (docAttr != null && docAttr.Length > 0) {
                return ((DocumentationAttribute)docAttr[0]).Documentation;
            }

            if (type == typeof(DynamicNull)) return null;

            // Auto Doc (XML or otherwise)
            string autoDoc = DocBuilder.CreateAutoDoc(type);
            if (autoDoc == null) {
                autoDoc = String.Empty;
            } else {
                autoDoc += Environment.NewLine + Environment.NewLine;
            }

            // Simple generated helpbased on ctor, if available.
            ConstructorInfo[] cis = type.GetConstructors();
            foreach (ConstructorInfo ci in cis) {
                autoDoc += FixCtorDoc(type, DocBuilder.CreateAutoDoc(ci, DynamicHelpers.GetPythonTypeFromType(type).Name, 0)) + Environment.NewLine;
            }

            return autoDoc;
        }

        private static string FixCtorDoc(Type type, string autoDoc) {
            return autoDoc.Replace("__new__(cls)", DynamicHelpers.GetPythonTypeFromType(type).Name + "()").
                            Replace("__new__(cls, ", DynamicHelpers.GetPythonTypeFromType(type).Name + "(");
        }

        internal static ReflectedGetterSetter GetReflectedProperty(PropertyTracker pt, MemberGroup allProperties, bool privateBinding) {
            ReflectedGetterSetter rp;
            lock (_propertyCache) {
                if (_propertyCache.TryGetValue(pt, out rp)) {
                    return rp;
                }

                NameType nt = NameType.PythonProperty;
                MethodInfo getter = FilterProtectedGetterOrSetter(pt.GetGetMethod(true), privateBinding);
                MethodInfo setter = FilterProtectedGetterOrSetter(pt.GetSetMethod(true), privateBinding);

                if ((getter != null && PythonHiddenAttribute.IsHidden(getter, true)) ||
                    (setter != null && PythonHiddenAttribute.IsHidden(setter, true))) {
                    nt = NameType.Property;
                }

                if (pt is ReflectedPropertyTracker rpt) {
                    if (PythonBinder.IsExtendedType(pt.DeclaringType) ||
                        PythonHiddenAttribute.IsHidden(rpt.Property, true)) {
                        nt = NameType.Property;
                    }

                    if (pt.GetIndexParameters().Length == 0) {
                        List<MethodInfo> getters = new List<MethodInfo>();
                        List<MethodInfo> setters = new List<MethodInfo>();

                        IList<ExtensionPropertyTracker> overriddenProperties = NewTypeMaker.GetOverriddenProperties((getter ?? setter).DeclaringType, pt.Name);
                        foreach (ExtensionPropertyTracker tracker in overriddenProperties) {
                            MethodInfo method = tracker.GetGetMethod(privateBinding);
                            if (method != null) {
                                getters.Add(method);
                            }

                            method = tracker.GetSetMethod(privateBinding);
                            if (method != null) {
                                setters.Add(method);
                            }
                        }

                        foreach (PropertyTracker propTracker in allProperties) {
                            MethodInfo method = propTracker.GetGetMethod(privateBinding);
                            if (method != null) {
                                getters.Add(method);
                            }

                            method = propTracker.GetSetMethod(privateBinding);
                            if (method != null) {
                                setters.Add(method);
                            }
                        }
                        rp = new ReflectedProperty(rpt.Property, getters.ToArray(), setters.ToArray(), nt);
                    } else {
                        rp = new ReflectedIndexer(rpt.Property, NameType.Property, privateBinding);
                    }
                } else {
                    Debug.Assert(pt is ExtensionPropertyTracker);
                    rp = new ReflectedExtensionProperty(new ExtensionPropertyInfo(pt.DeclaringType, getter ?? setter), nt);
                }

                _propertyCache[pt] = rp;

                return rp;
            }            
        }

        private static MethodInfo FilterProtectedGetterOrSetter(MethodInfo info, bool privateBinding) {
            if (info != null) {
                if (privateBinding || info.IsPublic) {
                    return info;
                }

                if (info.IsProtected()) {
                    return info;
                }
            }

            return null;
        }

        internal static bool TryInvokeUnaryOperator(CodeContext context, object o, string name, out object value) {
            PerfTrack.NoteEvent(PerfTrack.Categories.Temporary, "UnaryOp " + CompilerHelpers.GetType(o).Name + " " + name);

            PythonTypeSlot pts;
            PythonType pt = DynamicHelpers.GetPythonType(o);
            object callable;

            if (pt.TryResolveSlot(context, name, out pts) &&
                pts.TryGetValue(context, o, pt, out callable)) {
                value = PythonCalls.Call(context, callable);
                return true;
            }

            value = null;
            return false;
        }

        internal static bool TryInvokeBinaryOperator(CodeContext context, object o, object arg1, string name, out object value) {
            PerfTrack.NoteEvent(PerfTrack.Categories.Temporary, "BinaryOp " + CompilerHelpers.GetType(o).Name + " " + name);

            PythonTypeSlot pts;
            PythonType pt = DynamicHelpers.GetPythonType(o);
            object callable;
            if (pt.TryResolveSlot(context, name, out pts) &&
                pts.TryGetValue(context, o, pt, out callable)) {
                value = PythonCalls.Call(context, callable, arg1);
                return true;
            }

            value = null;
            return false;
        }

        internal static bool TryInvokeTernaryOperator(CodeContext context, object o, object arg1, object arg2, string name, out object value) {
            PerfTrack.NoteEvent(PerfTrack.Categories.Temporary, "TernaryOp " + CompilerHelpers.GetType(o).Name + " " + name);

            PythonTypeSlot pts;
            PythonType pt = DynamicHelpers.GetPythonType(o);
            object callable;
            if (pt.TryResolveSlot(context, name, out pts) &&
                pts.TryGetValue(context, o, pt, out callable)) {
                value = PythonCalls.Call(context, callable, arg1, arg2);
                return true;
            }

            value = null;
            return false;
        }

        /// <summary>
        /// If we have only interfaces, we'll need to insert object's base
        /// </summary>
        internal static PythonTuple EnsureBaseType(PythonTuple bases) {
            bool hasInterface = false;
            foreach (object baseClass in bases) {               
                PythonType dt = baseClass as PythonType;

                if (!dt.UnderlyingSystemType.IsInterface) {
                    return bases;
                } else {
                    hasInterface = true;
                }
            }

            if (hasInterface || bases.Count == 0) {
                // We found only interfaces. We need do add System.Object to the bases
                return new PythonTuple(bases, TypeCache.Object);
            }

            throw PythonOps.TypeError("a new-style class can't have only classic bases");
        }

        internal static Type GetFinalSystemType(Type type) {
            while (typeof(IPythonObject).IsAssignableFrom(type) && !type.IsDefined(typeof(DynamicBaseTypeAttribute), false)) {
                type = type.BaseType;
            }
            return type;
        }
    }
}
