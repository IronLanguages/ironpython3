// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

namespace IronPython.Compiler.Ast {
    /// <summary>
    /// Represents a reference to a name.  A PythonReference is created for each name
    /// referred to in a scope (global, class, or function).  
    /// </summary>
    internal class PythonReference {
        public PythonReference(string name) {
            Name = name;
        }

        public string Name { get; }

        internal PythonVariable PythonVariable { get; set; }
    }
}
