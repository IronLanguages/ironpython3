// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using NotNullAttribute = Microsoft.Scripting.Runtime.NotNullAttribute;

namespace IronPython.Runtime {
    [PythonHidden, PythonType("types.SimpleNamespace")]
    public class SimpleNamespace {
        public SimpleNamespace([ParamDictionary, NotNull]Dictionary<string, object?> kwargs\u00F8) {
            __dict__ = new PythonDictionary(kwargsø);
        }

        public PythonDictionary __dict__ { get; }

        [SpecialName]
        public object GetCustomMember([NotNull]string name) {
            return __dict__.get(name, OperationFailed.Value);
        }

        [SpecialName]
        public void SetMember([NotNull]string name, object? value) {
            __dict__[name] = value;
        }

        [SpecialName]
        public void DeleteMember([NotNull]string name) {
            __dict__.__delitem__(name);
        }

        public string __repr__(CodeContext context) {
            var attrs = Modules.Builtin.sorted(context, __dict__).Select(key => $"{PythonOps.ToString(context, key)}={PythonOps.Repr(context, __dict__[key])}");
            return $"namespace({string.Join(", ", attrs)})";
        }
    }
}
