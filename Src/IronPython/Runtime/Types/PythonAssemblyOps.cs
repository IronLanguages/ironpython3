// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using IronPython.Runtime.Types;
using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime.Operations {
    public static class PythonAssemblyOps {
        [MultiRuntimeAware]
        private static readonly object _key = new object();

        private static Dictionary<Assembly, TopNamespaceTracker> GetAssemblyMap(PythonContext/*!*/ context) {
            return context.GetOrCreateModuleState(_key, () => new Dictionary<Assembly, TopNamespaceTracker>());
        }

        [SpecialName]
        public static object GetBoundMember(CodeContext/*!*/ context, Assembly self, string name) {
            TopNamespaceTracker reflectedAssembly = GetReflectedAssembly(context, self);

            if (name == "__dict__") {
                return new PythonDictionary(new WrapperDictionaryStorage(reflectedAssembly));
            }
            MemberTracker mem = reflectedAssembly.TryGetPackageAny(name);
            if (mem != null) {
                if (mem.MemberType == TrackerTypes.Type) {
                    return DynamicHelpers.GetPythonTypeFromType(((TypeTracker)mem).Type);
                }
                // namespace or type collision
                return mem;
            }
            return OperationFailed.Value;
        }

        [SpecialName]
        public static List GetMemberNames(CodeContext/*!*/ context, Assembly self) {
            Debug.Assert(self != null);
            List ret = DynamicHelpers.GetPythonTypeFromType(self.GetType()).GetMemberNames(context);

            foreach (object o in GetReflectedAssembly(context, self).Keys) {
                if (o is string) {
                    ret.AddNoLock((string)o);
                }
            }

            return ret;
        }

        public static object __repr__(Assembly self) {
            Assembly asmSelf = self as Assembly;

            return "<Assembly " + asmSelf.FullName + ">";
        }

        private static TopNamespaceTracker GetReflectedAssembly(CodeContext/*!*/ context, Assembly assem) {
            Debug.Assert(assem != null);
            var assemblyMap = GetAssemblyMap(context.LanguageContext as PythonContext);
            lock (assemblyMap) {
                TopNamespaceTracker reflectedAssembly;
                if (assemblyMap.TryGetValue(assem, out reflectedAssembly))
                    return reflectedAssembly;

                reflectedAssembly = new TopNamespaceTracker(context.LanguageContext.DomainManager);
                reflectedAssembly.LoadAssembly(assem);
                assemblyMap[assem] = reflectedAssembly;

                return reflectedAssembly;
            }
        }
    }   
}
