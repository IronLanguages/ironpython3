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
            return PythonTypeOps.GetName(_value);
        }

        public string GetName() {
            return _name;
        }
    }
}
