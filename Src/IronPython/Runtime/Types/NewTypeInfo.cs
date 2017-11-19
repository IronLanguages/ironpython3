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

using System;
using System.Reflection;
using System.Collections.Generic;

using IronPython.Runtime.Operations;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime.Types {
    /// <summary>
    /// TypeInfo captures the minimal CLI information required by NewTypeMaker for a Python object
    /// that inherits from a CLI type.
    /// </summary>
    internal class NewTypeInfo {
        // The CLI base-type.
        private readonly Type _baseType;

        private readonly IList<Type> _interfaceTypes;
        private Nullable<int> _hash;

        public NewTypeInfo(Type baseType, IList<Type> interfaceTypes) {
            _baseType = baseType;
            _interfaceTypes = interfaceTypes;
        }

        /// <summary>
        /// "bases" contains a set of PythonTypes. These can include types defined in Python (say cpy1, cpy2),
        /// CLI types (say cCLI1, cCLI2), and CLI interfaces (say iCLI1, iCLI2). Here are some
        /// examples of how this works:
        /// 
        /// (bases)                      => baseType,        {interfaceTypes}
        /// 
        /// (cpy1)                       => System.Object,   {}
        /// (cpy1, cpy2)                 => System.Object,   {}
        /// (cpy1, cCLI1, iCLI1, iCLI2)  => cCLI1,           {iCLI1, iCLI2}
        /// [some type that satisfies the line above] => 
        ///                                 cCLI1,           {iCLI1, iCLI2}
        /// (cCLI1, cCLI2)               => error
        /// </summary>
        public static NewTypeInfo GetTypeInfo(string typeName, PythonTuple bases) {
            List<Type> interfaceTypes = new List<Type>();
            Type baseCLIType = typeof(object); // Pure Python object instances inherit from System.Object
            PythonType basePythonType = null;

            foreach (PythonType curBasePythonType in GetPythonTypes(typeName, bases)) {
                // discover the initial base/interfaces
                IList<Type> baseInterfaces = ReflectionUtils.EmptyTypes;
                Type curTypeToExtend = curBasePythonType.ExtensionType;

                if (curBasePythonType.ExtensionType.IsInterface) {
                    baseInterfaces = new Type[] { curTypeToExtend };
                    curTypeToExtend = typeof(object);
                } else if (NewTypeMaker.IsInstanceType(curTypeToExtend)) {
                    baseInterfaces = new List<Type>();
                    curTypeToExtend = GetBaseTypeFromUserType(curBasePythonType, baseInterfaces, curTypeToExtend.BaseType);
                }

                if (curTypeToExtend == null || typeof(BuiltinFunction).IsAssignableFrom(curTypeToExtend) || typeof(PythonFunction).IsAssignableFrom(curTypeToExtend))
                    throw PythonOps.TypeError(typeName + ": {0} is not an acceptable base type", curBasePythonType.Name);
                if (curTypeToExtend.ContainsGenericParameters())
                    throw PythonOps.TypeError(typeName + ": cannot inhert from open generic instantiation {0}. Only closed instantiations are supported.", curBasePythonType);

                foreach (Type interfaceType in baseInterfaces) {
                    if (interfaceType.ContainsGenericParameters())
                        throw PythonOps.TypeError(typeName + ": cannot inhert from open generic instantiation {0}. Only closed instantiations are supported.", interfaceType);

                    // collecting all the interfaces because we override them all.
                    interfaceTypes.Add(interfaceType);
                }

                // if we're not extending something already in our existing base classes type hierarchy
                // then we better be in some esoteric __slots__ situation
                if (!baseCLIType.IsSubclassOf(curTypeToExtend)) {
                    if (baseCLIType != typeof(object) && baseCLIType != curTypeToExtend &&
                        (!baseCLIType.IsDefined(typeof(DynamicBaseTypeAttribute), false) && !curTypeToExtend.IsSubclassOf(baseCLIType))) {
                        throw PythonOps.TypeError(
                            typeName + ": can only extend one CLI or builtin type, not both {0} (for {1}) and {2} (for {3})",
                            baseCLIType.FullName,
                            basePythonType,
                            curTypeToExtend.FullName,
                            curBasePythonType);
                    }

                    // we have a new base type
                    baseCLIType = curTypeToExtend;
                    basePythonType = curBasePythonType;
                }

            }
            return new NewTypeInfo(baseCLIType, interfaceTypes.Count == 0 ? ReflectionUtils.EmptyTypes : interfaceTypes.ToArray());
        }

        /// <summary>
        /// Filters out old-classes and throws if any non-types are included, returning a
        /// yielding the remaining PythonType objects.
        /// </summary>
        private static IEnumerable<PythonType> GetPythonTypes(string typeName, ICollection<object> bases) {
            foreach (object curBaseType in bases) {
                PythonType curBasePythonType = curBaseType as PythonType;
                if (curBasePythonType == null) {
                    throw PythonOps.TypeError(typeName + ": unsupported base type for new-style class " + curBaseType);
                }

                yield return curBasePythonType;
            }
        }

        private static Type GetBaseTypeFromUserType(PythonType curBasePythonType, IList<Type> baseInterfaces, Type curTypeToExtend) {
            Queue<PythonType> processing = new Queue<PythonType>();
            processing.Enqueue(curBasePythonType);

            do {
                PythonType walking = processing.Dequeue();
                foreach (PythonType dt in walking.BaseTypes) {
                    if (dt.ExtensionType == curTypeToExtend || curTypeToExtend.IsSubclassOf(dt.ExtensionType)) continue;

                    if (dt.ExtensionType.IsInterface) {
                        baseInterfaces.Add(dt.ExtensionType);
                    } else if (NewTypeMaker.IsInstanceType(dt.ExtensionType)) {
                        processing.Enqueue(dt);
                    }
                }
            } while (processing.Count > 0);
            return curTypeToExtend;
        }

        public Type BaseType {
            get { return _baseType; }
        }

        public IList<Type> InterfaceTypes {
            get { return _interfaceTypes; }
        }

        public override int GetHashCode() {
            if (_hash == null) {
                int hashCode = _baseType.GetHashCode();
                for (int i = 0; i < _interfaceTypes.Count; i++) {
                    hashCode ^= _interfaceTypes[i].GetHashCode();
                }

                _hash = hashCode;
            }

            return _hash.Value;
        }

        public override bool Equals(object obj) {
            NewTypeInfo other = obj as NewTypeInfo;
            if (other == null) return false;


            if (_baseType.Equals(other._baseType) &&
                _interfaceTypes.Count == other._interfaceTypes.Count) {

                for (int i = 0; i < _interfaceTypes.Count; i++) {
                    if (!_interfaceTypes[i].Equals(other._interfaceTypes[i])) return false;
                }
                return true;
            }
            return false;
        }        
    }
}
