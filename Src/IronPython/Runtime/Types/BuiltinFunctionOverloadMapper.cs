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
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Operations;

namespace IronPython.Runtime.Types {

    // Used to map signatures to specific targets on the embedded reflected method.
    public class BuiltinFunctionOverloadMapper : ICodeFormattable {
        private BuiltinFunction _function;
        private object _instance;
        private PythonTuple _allOverloads;  // overloads are stored here and may be bound to an instance

        public BuiltinFunctionOverloadMapper(BuiltinFunction builtinFunction, object instance) {
            this._function = builtinFunction;
            this._instance = instance;
        }

        public object this[params Type[] types] {
            get {
                return GetOverload(types, Targets);
            }
        }

        protected object GetOverload(Type[] sig, IList<MethodBase> targets) {
            return GetOverload(sig, targets, true);
        }

        private object GetOverload(Type[] sig, IList<MethodBase> targets, bool wrapCtors) {
            // We can still end up with more than one target since generic and non-generic
            // methods can share the same name and signature. So we'll build up a new
            // reflected method with all the candidate targets. A caller can then index this
            // reflected method if necessary in order to provide generic type arguments and
            // fully disambiguate the target.

            // Search for targets with the right number of arguments.
            BuiltinFunction bf;
            BuiltinFunction.TypeList tl = new BuiltinFunction.TypeList(sig);
            lock (_function.OverloadDictionary) {
                if (!_function.OverloadDictionary.TryGetValue(tl, out bf)) {
                    MethodBase[] newTargets = FindMatchingTargets(sig, targets);

                    if (targets == null)
                        throw ScriptingRuntimeHelpers.SimpleTypeError(String.Format("No match found for the method signature {0}", sig));    // TODO: Sig to usable display

                    _function.OverloadDictionary[tl] = bf = new BuiltinFunction(_function.Name, newTargets, Function.DeclaringType, _function.FunctionType);
                }
            }

            if (_instance != null) {
                return bf.BindToInstance(_instance);
            } else if (wrapCtors) {
                return GetTargetFunction(bf);
            } else {
                return bf;
            }
        }

        private static MethodBase[] FindMatchingTargets(Type[] sig, IList<MethodBase> targets) {
            int args = sig.Length;

            List<MethodBase> res = new List<MethodBase>();
            foreach (MethodBase mb in targets) {
                ParameterInfo[] pis = mb.GetParameters();
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
