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
using System.IO;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

using IronPython.Compiler;
using IronPython.Runtime.Operations;

namespace IronPython.Runtime {
    public class CompiledLoader {
        private Dictionary<string, OnDiskScriptCode> _codes = new Dictionary<string, OnDiskScriptCode>();

        internal void AddScriptCode(ScriptCode code) {
            OnDiskScriptCode onDiskCode = code as OnDiskScriptCode;
            if (onDiskCode != null) {
                if (onDiskCode.ModuleName == "__main__") {
                    _codes["__main__"] = onDiskCode;
                } else {
                    string name = code.SourceUnit.Path;
                    name = name.Replace(Path.DirectorySeparatorChar, '.');
                    if (name.EndsWith("__init__.py")) {
                        name = name.Substring(0, name.Length - ".__init__.py".Length);
                    }
                    _codes[name] = onDiskCode;
                }
            }
        }

        public ModuleLoader find_module(CodeContext/*!*/ context, string fullname, List path) {
            OnDiskScriptCode sc;
            if (_codes.TryGetValue(fullname, out sc)) {
                int sep = fullname.LastIndexOf('.');
                string name = fullname;
                string parentName = null;
                if (sep != -1) {
                    parentName = fullname.Substring(0, sep);
                    name = fullname.Substring(sep + 1);
                }
                return new ModuleLoader(sc, parentName, name);
            }

            return null;
        }
    }
}
