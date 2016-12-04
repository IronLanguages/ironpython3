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

#if FEATURE_CORE_DLR
using System.Linq.Expressions;
#else
using Microsoft.Scripting.Ast;
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Reflection;
using System.Threading;
using IronPython.Runtime.Binding;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime {
    /// <summary>
    /// Represents the set of extension methods which are loaded into a module.
    /// 
    /// This set is immutable (as far the external viewer is considered).  When a 
    /// new extension method set is loaded into a module we create a new ExtensionMethodsSet object.  
    /// 
    /// Multiple modules which have the same set of extension methods use the same set.
    /// </summary>
    sealed class ExtensionMethodSet : IEquatable<ExtensionMethodSet> {
        private PythonExtensionBinder _extBinder;
        private Dictionary<Assembly, AssemblyLoadInfo>/*!*/ _loadedAssemblies;
        private int _id;
        private static int _curId;

        public static readonly ExtensionMethodSet Empty = new ExtensionMethodSet();
        public const int OutOfIds = Int32.MinValue;

        private ExtensionMethodSet(Dictionary<Assembly, AssemblyLoadInfo> dict) {
            _loadedAssemblies = dict;
            if (_curId < 0 || (_id = Interlocked.Increment(ref _curId)) < 0) {
                // overflow, we ran out of ids..
                _id = OutOfIds;
            }
        }

        public BindingRestrictions GetRestriction(Expression codeContext) {
            BindingRestrictions extCheck;
            if (_id == ExtensionMethodSet.OutOfIds) {
                extCheck = BindingRestrictions.GetInstanceRestriction(
                    Expression.Call(
                        typeof(PythonOps).GetMethod("GetExtensionMethodSet"),
                        codeContext
                    ),
                    this
                );
            } else {
                extCheck = BindingRestrictions.GetExpressionRestriction(
                    Expression.Call(
                        typeof(PythonOps).GetMethod("IsExtensionSet"),
                        codeContext,
                        Expression.Constant(_id)
                    )
                );
            }
            return extCheck;
        }

        private ExtensionMethodSet() {
            _loadedAssemblies = new Dictionary<Assembly, AssemblyLoadInfo>();
        }

        /// <summary>
        /// Tracks the extension types that are loaded for a given assembly.
        /// 
        /// We can have either types, namespaces, or a full assembly added as a reference.
        /// 
        /// When the user just adds types we just add them to the type hash set.
        /// 
        /// When the user adds namespaces we add them to the namespaces hashset.  On the
        /// next lookup we'll lazily load the types from that namespace and put them in Types.
        /// 
        /// When the user adds assemblies we set the value to the NotYetLoadedButFullAssembly
        /// value.  The next load request will load the types from that namespace and put them
        /// in Types.  When we do that we'll mark the assembly as FullyLoaded so we don't 
        /// have to go through that again if the user adds a namespace.
        /// </summary>
        sealed class AssemblyLoadInfo : IEquatable<AssemblyLoadInfo> {
            private static IEqualityComparer<HashSet<PythonType>> TypeComparer = CollectionUtils.CreateSetComparer<PythonType>();
            private static IEqualityComparer<HashSet<string>> StringComparer = CollectionUtils.CreateSetComparer<string>();

            public HashSet<PythonType> Types;   // set of types loaded from the assembly
            public HashSet<string> Namespaces;  // list of namespaces which should be loaded from the assembly.
            public bool IsFullAssemblyLoaded;   // all types in the assembly are loaded and stored in the Types set.
            private readonly Assembly _asm;

            public AssemblyLoadInfo(Assembly asm) {
                _asm = asm;
            }

            public override int GetHashCode() {
                if (_asm != null) {
                    return _asm.GetHashCode();
                }
                return 0;
            }
            public override bool Equals(object obj) {
                AssemblyLoadInfo asmLoadInfo = obj as AssemblyLoadInfo;
                if (asmLoadInfo != null) {
                    return Equals(asmLoadInfo);
                }
                return false;
            }

            public AssemblyLoadInfo EnsureTypesLoaded() {
                if (Namespaces != null || Types == null) {
                    HashSet<PythonType> loadedTypes = new HashSet<PythonType>();
                    var ns = Namespaces;
                    
                    foreach (var type in _asm.GetExportedTypes()) {
                        if (type.GetTypeInfo().IsExtension()) {
                            if (ns != null && ns.Contains(type.Namespace)) {
                                loadedTypes.Add(DynamicHelpers.GetPythonTypeFromType(type));
                            }
                        }
                    }

                    var info = new AssemblyLoadInfo(_asm);
                    info.Types = loadedTypes;
                    if (ns == null) {
                        info.IsFullAssemblyLoaded = true;
                    }

                    return info;
                }

                return this;
            }

            #region IEquatable<AssemblyLoadInfo> Members

            public bool Equals(AssemblyLoadInfo other) {
                if ((object)this == (object)other) {
                    return true;
                } else if (other == null || _asm != other._asm) {
                    return false;
                }

                if (IsFullAssemblyLoaded && other.IsFullAssemblyLoaded) {
                    // full assembly is loaded for both
                    return true;
                }

                return TypeComparer.Equals(Types, other.Types) && StringComparer.Equals(Namespaces, other.Namespaces);
            }

            #endregion
        }

        /// <summary>
        /// Returns all of the extension methods with the given name.
        /// </summary>
        public IEnumerable<MethodInfo> GetExtensionMethods(string/*!*/ name) {
            Assert.NotNull(name);

            lock (this) {
                EnsureLoaded();

                foreach (var keyValue in _loadedAssemblies) {
                    AssemblyLoadInfo info = keyValue.Value;

                    Debug.Assert(info.Types != null);
                    foreach (var type in info.Types) {
                        List<MethodInfo> methods;
                        if (type.ExtensionMethods.TryGetValue(name, out methods)) {
                            foreach (var method in methods) {
                                yield return method;
                            }
                        }
                    }
                }
            }
        }

        private void EnsureLoaded() {
            bool hasUnloaded = false;
            foreach (AssemblyLoadInfo info in _loadedAssemblies.Values) {
                if (info.Namespaces != null ||
                    info.Types == null) {
                    hasUnloaded = true;
                }
            }

            if (hasUnloaded) {
                LoadAllTypes();
            }
        }

        /// <summary>
        /// Returns all of the extension methods which are applicable for the given type.
        /// </summary>
        public IEnumerable<MethodInfo> GetExtensionMethods(PythonType type) {
            lock (this) {
                EnsureLoaded();

                foreach (var keyValue in _loadedAssemblies) {
                    AssemblyLoadInfo info = keyValue.Value;

                    Debug.Assert(info.Types != null);
                    foreach (var containingType in info.Types) {
                        foreach(var methodList in containingType.ExtensionMethods.Values) {                            
                            foreach (var method in methodList) {
                                var methodParams = method.GetParameters();
                                if (methodParams.Length == 0) {
                                    continue;
                                }

                                if (PythonExtensionBinder.IsApplicableExtensionMethod(type.UnderlyingSystemType, methodParams[0].ParameterType)) {
                                    yield return method;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void LoadAllTypes() {
            var newAssemblies = new Dictionary<Assembly, AssemblyLoadInfo>(_loadedAssemblies.Count);

            foreach (var keyValue in _loadedAssemblies) {
                AssemblyLoadInfo info = keyValue.Value;
                var asm = keyValue.Key;

                newAssemblies[asm] = info.EnsureTypesLoaded();
            }
            _loadedAssemblies = newAssemblies;
        }

        public static ExtensionMethodSet AddType(PythonContext context, ExtensionMethodSet/*!*/ existingSet, PythonType/*!*/ type) {
            Assert.NotNull(existingSet, type);

            lock (existingSet) {
                AssemblyLoadInfo assemblyLoadInfo;
                Assembly assembly = type.UnderlyingSystemType.GetTypeInfo().Assembly;
                if (existingSet._loadedAssemblies.TryGetValue(assembly, out assemblyLoadInfo)) {
                    if (assemblyLoadInfo.IsFullAssemblyLoaded ||
                        (assemblyLoadInfo.Types != null && assemblyLoadInfo.Types.Contains(type)) ||
                        (assemblyLoadInfo.Namespaces != null && assemblyLoadInfo.Namespaces.Contains(type.UnderlyingSystemType.Namespace))) {

                        // type is already in this set.
                        return existingSet;
                    }
                }

                var dict = NewInfoOrCopy(existingSet);
                if (!dict.TryGetValue(assembly, out assemblyLoadInfo)) {
                    dict[assembly] = assemblyLoadInfo = new AssemblyLoadInfo(assembly);
                }

                if (assemblyLoadInfo.Types == null) {
                    assemblyLoadInfo.Types = new HashSet<PythonType>();
                }

                assemblyLoadInfo.Types.Add(type);
                return context.UniqifyExtensions(new ExtensionMethodSet(dict));
            }
        }

        public static ExtensionMethodSet AddNamespace(PythonContext context, ExtensionMethodSet/*!*/ existingSet, NamespaceTracker/*!*/ ns) {
            Assert.NotNull(existingSet, ns);

            lock (existingSet) {
                AssemblyLoadInfo asmInfo;

                Dictionary<Assembly, AssemblyLoadInfo> newDict = null;
                foreach (var assembly in ns.PackageAssemblies) {
                    if (existingSet != null && existingSet._loadedAssemblies.TryGetValue(assembly, out asmInfo)) {
                        if (asmInfo.IsFullAssemblyLoaded) {
                            // full assembly is already in this set.
                            continue;
                        }

                        if (asmInfo.Namespaces == null || !asmInfo.Namespaces.Contains(ns.Name)) {
                            if (newDict == null) {
                                newDict = NewInfoOrCopy(existingSet);
                            }
                            if (newDict[assembly].Namespaces == null) {
                                newDict[assembly].Namespaces = new HashSet<string>();
                            }
                            newDict[assembly].Namespaces.Add(ns.Name);
                        }
                    } else {
                        if (newDict == null) {
                            newDict = NewInfoOrCopy(existingSet);
                        }
                        var newAsmInfo = newDict[assembly] = new AssemblyLoadInfo(assembly);
                        newAsmInfo.Namespaces = new HashSet<string>();
                        newAsmInfo.Namespaces.Add(ns.Name);
                    }
                }
                if (newDict != null) {
                    return context.UniqifyExtensions(new ExtensionMethodSet(newDict));
                }
                return existingSet;
            }
        }

        public static bool operator  == (ExtensionMethodSet set1, ExtensionMethodSet set2) {
            if ((object)set1 != (object)null) {
                return set1.Equals(set2);
            }

            return (object)set2 == (object)null;
        }
        
        public static bool operator  != (ExtensionMethodSet set1, ExtensionMethodSet set2) {
            return !(set1 == set2);
        }

        public PythonExtensionBinder GetBinder(PythonContext/*!*/ context) {
            Debug.Assert(context != null);

            if (_extBinder == null) {
                _extBinder = new PythonExtensionBinder(context.Binder, this);
            }

            return _extBinder;
        }

        private static Dictionary<Assembly, AssemblyLoadInfo> NewInfoOrCopy(ExtensionMethodSet/*!*/ existingSet) {
            var dict = new Dictionary<Assembly, AssemblyLoadInfo>();
            if (existingSet != null) {
                foreach (var keyValue in existingSet._loadedAssemblies) {
                    var assemblyLoadInfo = new AssemblyLoadInfo(keyValue.Key);
                    if (keyValue.Value.Namespaces != null) {
                        assemblyLoadInfo.Namespaces = new HashSet<string>(keyValue.Value.Namespaces);
                    }
                    if (keyValue.Value.Types != null) {
                        assemblyLoadInfo.Types = new HashSet<PythonType>(keyValue.Value.Types);
                    }
                    dict[keyValue.Key] = assemblyLoadInfo;
                }
            }
            return dict;
        }

        public int Id { get { return _id; } }

        public override bool Equals(object obj) {
            ExtensionMethodSet other = obj as ExtensionMethodSet;
            if (other != null) {
                return this.Equals(other);
            }
            return false;
        }

        #region IEquatable<ExtensionMethodSet> Members

        public bool Equals(ExtensionMethodSet other) {
            if (other == null) {
                return false;
            } else if ((object)this == (object)other) {
                // object identity
                return true;
            } else if (_loadedAssemblies.Count != other._loadedAssemblies.Count) {
                // different assembly set
                return false;
            }

            foreach (var asmAndInfo in _loadedAssemblies) {
                var asm = asmAndInfo.Key;
                var info = asmAndInfo.Value;

                AssemblyLoadInfo otherAsmInfo;
                if (!other._loadedAssemblies.TryGetValue(asm, out otherAsmInfo)) {
                    // different assembly set.
                    return false;
                }

                if (otherAsmInfo != info) {
                    return false;
                }
            }
            return true;
        }

        public override int GetHashCode() {
            int res = 6551;
            foreach (var asm in _loadedAssemblies.Keys) {
                res ^= asm.GetHashCode();
            }
            return res;
        }

        #endregion

#if FALSE
        // simpler but less lazy...
        public ExtensionMethodSet(HashSet<Type> newTypes, HashSet<NamespaceTracker> newNspaces, HashSet<Assembly> newAssms) {
            _types = newTypes;
            _namespaces = newNspaces;
            _assemblies = newAssms;
        }

        private HashSet<Type> _types;
        private HashSet<Assembly> _assemblies;
        private HashSet<NamespaceTracker> _namespaces;        

        internal static ExtensionMethodSet AddExtensions(ExtensionMethodSet extensionMethodSet, object[] extensions) {
            var res = extensionMethodSet;
            var newTypes = new HashSet<Type>();
            var newAssms = new HashSet<Assembly>();
            var newNspaces = new HashSet<NamespaceTracker>();

            foreach (object o in extensions) {
                PythonType type = o as PythonType;
                if (type != null) {
                    if (res._types != null && res._types.Contains(type)) {
                        continue;
                    }

                    newTypes.Add(type.UnderlyingSystemType);
                }

                Assembly asm = o as Assembly;
                if (asm != null) {
                    if (res._assemblies != null && res._assemblies.Contains(asm)) {
                        continue;
                    }

                    foreach (var method in ReflectionUtils.GetVisibleExtensionMethods(asm)) {
                        if (newTypes.Contains(method.DeclaringType) ||
                            (res._types != null && res._types.Contains(method.DeclaringType)) ) {
                            continue;
                        }

                        newTypes.Add(method.DeclaringType);
                    }
                }

                NamespaceTracker ns = o as NamespaceTracker;
                if (ns != null) {
                    if (res._namespaces != null && res._namespaces.Contains(ns)) {
                        continue;
                    }

                    foreach (var packageAsm in ns.PackageAssemblies) {
                        foreach (var method in ReflectionUtils.GetVisibleExtensionMethods(packageAsm)) {
                            if (newTypes.Contains(method.DeclaringType) ||
                                (res._types != null && res._types.Contains(method.DeclaringType))) {
                                continue;
                            }

                            newTypes.Add(method.DeclaringType);
                        }
                    }
                }
            }

            if (newTypes.Count > 0) {
                if (res._types != null) {
                    newTypes.UnionWith(res._types);
                }
                if (res._namespaces != null) {
                    newNspaces.UnionWith(res._namespaces);
                }
                if (res._assemblies != null) {
                    newAssms.UnionWith(res._assemblies);
                }

                return new ExtensionMethodSet(newTypes, newNspaces, newAssms);
            }

            return res;
        }
#endif
    }
}
