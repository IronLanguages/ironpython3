// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Actions.Calls;
using Microsoft.Scripting.Utils;
using System.Diagnostics;

namespace IronPython.Runtime.Binding {
    class PythonExtensionBinder : PythonBinder {
        private readonly ExtensionMethodSet _extMethodSet;

        public PythonExtensionBinder(PythonBinder binder, ExtensionMethodSet extensionMethods)
            : base(binder) {
            _extMethodSet = extensionMethods;
        }

        public override MemberGroup GetMember(MemberRequestKind actionKind, Type type, string name) {
            var res = base.GetMember(actionKind, type, name);
            if (res.Count == 0) {
                List<MemberTracker> trackers = new List<MemberTracker>();

                foreach (var method in _extMethodSet.GetExtensionMethods(name)) {
                    var parameters = method.GetParameters();
                    if (parameters.Length == 0) {
                        continue;
                    }

                    var paramType = parameters[0].ParameterType;

                    if (IsApplicableExtensionMethod(type, paramType)) {
                        trackers.Add(MemberTracker.FromMemberInfo(method, paramType));
                    }
                }

                if (trackers.Count > 0) {
                    return new MemberGroup(trackers.ToArray());
                }
            }
            return res;
        }

        internal static bool IsApplicableExtensionMethod(Type instanceType, Type extensionMethodThisType) {
            
            if (extensionMethodThisType.ContainsGenericParameters()) {
                Dictionary<Type, Type> binding = new Dictionary<Type, Type>();
                
                if (extensionMethodThisType.IsArray) {
                    if (instanceType.IsArray) {
                        extensionMethodThisType = extensionMethodThisType.GetElementType();
                        instanceType = instanceType.GetElementType();
                        var inferredType = TypeInferer.GetInferedType(extensionMethodThisType, extensionMethodThisType, instanceType, instanceType, binding);
                        return inferredType != null;
                    }
                    return false;
                }

                Type[] genArgs = extensionMethodThisType.IsGenericParameter ? new[] { extensionMethodThisType } : extensionMethodThisType.GetGenericArguments();
                Type[] inferredTypes = new Type[genArgs.Length];

                for (int i = 0; i < genArgs.Length; i++) {
                    if ((inferredTypes[i] = TypeInferer.GetInferedType(genArgs[i], extensionMethodThisType, instanceType, instanceType, binding)) == null) {
                        inferredTypes = null;
                        break;
                    }
                }

                if (inferredTypes != null) {
                    if (extensionMethodThisType.IsGenericParameter) {
                        extensionMethodThisType = inferredTypes[0];
                    } else if (inferredTypes.Length > 0) {
                        extensionMethodThisType = extensionMethodThisType.GetGenericTypeDefinition().MakeGenericType(inferredTypes);
                    }
                }
            }

            return extensionMethodThisType.IsAssignableFrom(instanceType);
        }
    }
}
