// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

using Microsoft.Scripting;

namespace IronPython.Compiler.Ast {
    /// <summary>
    /// Represents a reference to a name.  A PythonReference is created for each name
    /// referred to in a scope (global, class, or function).  
    /// </summary>
    class PythonReference {
        private readonly string _name;
        private PythonVariable _variable;

        public PythonReference(string name) {
            _name = name;
        }

        public string Name {
            get { return _name; }
        }

        internal PythonVariable PythonVariable {
            get { return _variable; }
            set { _variable = value; }
        }
    }
}
