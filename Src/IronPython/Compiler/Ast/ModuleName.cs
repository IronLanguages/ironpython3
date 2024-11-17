// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

namespace IronPython.Compiler.Ast {
    public class ModuleName : DottedName {
        public ModuleName(string[] names)
            : base(names) {
        }
    }
}
