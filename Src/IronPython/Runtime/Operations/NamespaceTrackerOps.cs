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
using System.Runtime.CompilerServices;
using System.Text;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Types;

namespace IronPython.Runtime.Operations {
    public static class NamespaceTrackerOps {
        [SpecialName, PropertyMethod]
        public static object Get__file__(NamespaceTracker self) {
            if (self.PackageAssemblies.Count == 1) {
                return self.PackageAssemblies[0].FullName;
            }

            StringBuilder res = new StringBuilder();
            for (int i = 0; i < self.PackageAssemblies.Count; i++) {
                if (i != 0) {
                    res.Append(", ");
                }
                res.Append(self.PackageAssemblies[i].FullName);
            }
            return res.ToString();
        }

        public static string __repr__(NamespaceTracker self) {
            return __str__(self);
        }

        public static string __str__(NamespaceTracker self) {
            if (self.PackageAssemblies.Count != 1) {
                return String.Format("<module '{0}' (CLS module, {1} assemblies loaded)>", Get__name__(self.Name), self.PackageAssemblies.Count);
            }
            return String.Format("<module '{0}' (CLS module from {1})>", Get__name__(self.Name), self.PackageAssemblies[0].FullName);
        }

        [SpecialName, PropertyMethod]
        public static PythonDictionary Get__dict__(CodeContext context, NamespaceTracker self) {
            PythonDictionary res = new PythonDictionary();
            foreach (var kvp in self) {
                if (kvp.Value is TypeGroup || kvp.Value is NamespaceTracker) {
                    res[kvp.Key] = kvp.Value;
                } else {
                    res[kvp.Key] = DynamicHelpers.GetPythonTypeFromType(((TypeTracker)kvp.Value).Type);
                }
            }
            return res;
        }

        [SpecialName, PropertyMethod]
        public static string Get__name__(CodeContext context, NamespaceTracker self) {
            return Get__name__(self.Name);
        }

        private static string Get__name__(string name) {
            int lastDot = name.LastIndexOf('.');
            if (lastDot == -1) return name;

            return name.Substring(lastDot + 1);
        }

        [SpecialName]
        public static object GetCustomMember(CodeContext/*!*/ context, NamespaceTracker/*!*/ self, string name) {
            MemberTracker mt;
            if (self.TryGetValue(name, out mt)) {
                if (mt.MemberType == TrackerTypes.Namespace || mt.MemberType == TrackerTypes.TypeGroup) {
                    return mt;
                }

                PythonTypeSlot pts = PythonTypeOps.GetSlot(new MemberGroup(mt), name, PythonContext.GetContext(context).Binder.PrivateBinding);
                object value;
                if (pts != null && pts.TryGetValue(context, null, TypeCache.PythonType, out value)) {
                    return value;
                }
            }

            return OperationFailed.Value;
        }
    }
}
