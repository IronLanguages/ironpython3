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

namespace IronPython.Runtime {
    [PythonHidden, PythonType("types.SimpleNamespace")]
    public class SimpleNamespace {
        public SimpleNamespace([ParamDictionary, NotNone] Dictionary<string, object?> kwargs\u00F8) {
            __dict__ = new PythonDictionary(kwargsø);
        }

        public PythonDictionary __dict__ { get; }

        [SpecialName]
        public object GetCustomMember([NotNone] string name) {
            return __dict__.get(name, OperationFailed.Value);
        }

        [SpecialName]
        public void SetMember([NotNone] string name, object? value) {
            __dict__[name] = value;
        }

        [SpecialName]
        public void DeleteMember([NotNone] string name) {
            __dict__.__delitem__(name);
        }

        public string __repr__(CodeContext context) {
            var infinite = PythonOps.GetAndCheckInfinite(this);
            if (infinite == null) {
                return "namespace(...)";
            }

            int index = infinite.Count;
            infinite.Add(this);
            try {
                var attrs = Modules.Builtin.sorted(context, __dict__, new Dictionary<string, object>()).Select(key => $"{PythonOps.ToString(context, key)}={PythonOps.Repr(context, __dict__[key])}");
                return $"namespace({string.Join(", ", attrs)})";
            } finally {
                System.Diagnostics.Debug.Assert(index == infinite.Count - 1);
                infinite.RemoveAt(index);
            }
        }

        public bool __eq__(CodeContext context, [NotNone] SimpleNamespace other)
            => PythonOps.IsOrEqualsRetBool(__dict__, other.__dict__);

        [return: MaybeNotImplemented]
        public object __eq__(CodeContext context, object? other) {
            if (other is SimpleNamespace simpleNamespace)
                return __eq__(context, simpleNamespace);
            return NotImplementedType.Value;
        }
    }
}
