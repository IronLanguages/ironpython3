// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Operations;

namespace IronPython.Runtime.Types {

    // Used to map signatures to specific targets on the embedded reflected method.
    public class BuiltinFunctionOverloadMapper : ICodeFormattable {
        private readonly BuiltinFunction _function;
        private readonly object _instance;
        private PythonTuple _allOverloads;  // overloads are stored here and may be bound to an instance

        public BuiltinFunctionOverloadMapper(BuiltinFunction builtinFunction, object instance) {
            _function = builtinFunction;
            _instance = instance;
        }

        public object this[params Type[] types] => GetOverload(types, Targets);

        protected object GetOverload(Type[] sig, IList<MethodBase> targets) {
            return GetOverload(sig, targets, true);
        }

        private object GetOverload(Type[] sig, IList<MethodBase> targets, bool wrapCtors) {
            // We can still end up with more than one target since generic and non-generic
            // methods can share the same name and signature. So we'll build up a new
            // reflected method with all the candidate targets. A caller can then index this
            // reflected method if necessary in order to provide generic type arguments and
            // fully disambiguate the target.

            BuiltinFunction bf;
            BuiltinFunction.TypeList tl = new BuiltinFunction.TypeList(sig);
            lock (_function.OverloadDictionary) {
                if (!_function.OverloadDictionary.TryGetValue(tl, out bf)) {
                    MethodBase[] newTargets = FindMatchingTargets(sig, targets, true);

                    if (newTargets.Length == 0) {
                        // If no overload was found, check also using CodeContext for backward compatibility
                        newTargets = FindMatchingTargets(sig, targets, false);
                    }

                    if (newTargets.Length == 0) {
                        ThrowOverloadException(sig, targets);
                    } else {
                        _function.OverloadDictionary[tl] = bf = new BuiltinFunction(_function.Name, newTargets, Function.DeclaringType, _function.FunctionType);
                    }
                }
            }

            if (_instance != null) {
                return bf.BindToInstance(_instance);
            }

            if (wrapCtors) {
                return GetTargetFunction(bf);
            } 

            return bf;
        }

        /// <summary>
        /// Find matching overloads by checking signature against available targets
        /// </summary>
        /// <param name="sig">Given signature</param>
        /// <param name="targets">List of possible targets</param>
        /// <param name="removeCodeContext">If set to true, the method will check whether the first paramter of the
        /// target is of the type CodeContext and removes it</param>
        /// <returns>Possible overloads</returns>
        private static MethodBase[] FindMatchingTargets(Type[] sig, IList<MethodBase> targets, bool removeCodeContext) {
            int args = sig.Length;

            List<MethodBase> res = new List<MethodBase>();

            foreach (MethodBase mb in targets) {
                ParameterInfo[] pis = mb.GetParameters();

                if (removeCodeContext) {
                    if (pis.Length > 0 && pis[0].ParameterType == typeof(CodeContext)) {
                        // Copy array and skip CodeContext
                        var newPis = new ParameterInfo[pis.Length - 1];
                        for (int i = 1; i < pis.Length; i++) {
                            newPis[i - 1] = pis[i];
                        }
                        pis = newPis;
                    }
                }

                if (pis.Length != args)
                    continue;

                // Check each parameter type for an exact match.
                bool match = true;
                for (int i = 0; i < args; i++)
                    if (pis[i].ParameterType != sig[i]) {
                        match = false;
                        break;
                    }
                if (!match)
                    continue;

                // Okay, we have a match, add it to the list.
                res.Add(mb);
            }
            return res.ToArray();
        }

        /// <summary>
        /// Throws a formatted exception if no overload matchs.
        /// </summary>
        /// <param name="sig">Passed signature which should be used</param>
        /// <param name="targets">Given targets, which does not fit to the signature</param>
        /// <example>
        /// <code language="cs" title="Cause overload exception"><![CDATA[
        /// # Will cause an exception:
        /// from System import Convert, Double
        /// Convert.ToInt32.Overloads[Double, Double](24)
        /// ]]></code>
        /// </example>
        public void ThrowOverloadException(Type[] sig, IList<MethodBase> targets) {
            // Create info for given signature
            System.Text.StringBuilder sigInfo = new System.Text.StringBuilder();
            sigInfo.Append((targets.Count > 0 ? targets[0].Name : "") + "[");
            foreach (var type in sig) {
                if (!sigInfo.ToString().EndsWith("[")) {
                    sigInfo.Append(", ");
                }

                sigInfo.Append(type.Name);
            }
            sigInfo.Append("]");

            // Get possible overloads.
            System.Text.StringBuilder possibleOverloads = new System.Text.StringBuilder();

            foreach (var overload in targets) {
                if (possibleOverloads.Length > 0) {
                    possibleOverloads.Append(", ");
                }

                possibleOverloads.Append("[");
                foreach (var param in overload.GetParameters()) {
                    if (!possibleOverloads.ToString().EndsWith("[")) {
                        possibleOverloads.Append(", ");
                    }
                    possibleOverloads.Append(param.ParameterType.Name);
                }
                possibleOverloads.Append("]");
            }

            throw ScriptingRuntimeHelpers.SimpleTypeError(String.Format("No match found for the method signature {0}. Expected {1}", sigInfo.ToString(),
                possibleOverloads.ToString()));
        }

        public BuiltinFunction Function {
            get {
                return _function;
            }
        }

        public virtual IList<MethodBase> Targets {
            get {
                return _function.Targets;
            }
        }

        public PythonTuple Functions {
            get {
                if (_allOverloads == null) {
                    object[] res = new object[Targets.Count];
                    int writeIndex = 0;
                    foreach (MethodBase mb in Targets) {
                        ParameterInfo[] pis = mb.GetParameters();
                        Type[] types = new Type[pis.Length];

                        for (int i = 0; i < pis.Length; i++) {
                            types[i] = pis[i].ParameterType;
                        }

                        res[writeIndex++] = GetOverload(types, Targets, false);
                    }

                    Interlocked.CompareExchange(ref _allOverloads, PythonTuple.MakeTuple(res), null);
                }

                return _allOverloads;
            }
        }

        protected virtual object GetTargetFunction(BuiltinFunction bf) {
            return bf;
        }

        public virtual string/*!*/ __repr__(CodeContext/*!*/ context) {
            PythonDictionary overloadList = new PythonDictionary();

            foreach (MethodBase mb in Targets) {
                string key = DocBuilder.CreateAutoDoc(mb);
                overloadList[key] = Function;
            }
            return overloadList.__repr__(context);
        }
    }

    public class ConstructorOverloadMapper : BuiltinFunctionOverloadMapper {
        public ConstructorOverloadMapper(ConstructorFunction builtinFunction, object instance)
            : base(builtinFunction, instance) {
        }

        public override IList<MethodBase> Targets {
            get {
                return ((ConstructorFunction)Function).ConstructorTargets;
            }
        }

        protected override object GetTargetFunction(BuiltinFunction bf) {
            // return a function that's bound to the overloads, we'll
            // the user then calls this w/ the dynamic type, and the bound
            // function drops the class & calls the overload.
            if (bf.Targets[0].DeclaringType != typeof(InstanceOps)) {
                return new ConstructorFunction(InstanceOps.OverloadedNew, bf.Targets).BindToInstance(bf);
            }

            return base.GetTargetFunction(bf);
        }
    }
}
