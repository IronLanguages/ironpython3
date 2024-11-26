// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using IronPython.Runtime.Operations;

namespace IronPython.Runtime {
    [DebuggerDisplay("{Value}", Name = "{GetName(),nq}", Type = "{GetClassName(),nq}")]
    internal class ObjectDebugView {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly string _name;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly object _value;

        public ObjectDebugView(object name, object value) {
            _name = name.ToString();
            _value = value;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public object Value {
            get {
                return _value;
            }
        }

        public string GetClassName() {
#pragma warning disable IPY04 // Direct call to PythonTypeOps.GetName
            return PythonTypeOps.GetName(_value);
#pragma warning restore IPY04
        }

        public string GetName() {
            return _name;
        }
    }
}
