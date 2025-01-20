// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using IronPython.Compiler;

namespace IronPython.Runtime {
    /// <summary>
    /// ModuleDictionaryStorage for a built-in module which is bound to a specific instance.
    /// 
    /// These modules don't need to use PythonContext.GetModuleState() for storage and therefore
    /// can provide efficient access to internal variables.  They can also cache PythonGlobal
    /// objects and provide efficient access to module globals.  
    /// 
    /// To the end user these modules appear just like any other module.  These modules are
    /// implemented by subclassing the BuiltinPythonModule class.
    /// </summary>
    internal class InstancedModuleDictionaryStorage : ModuleDictionaryStorage {
        private BuiltinPythonModule _module;

        public InstancedModuleDictionaryStorage(BuiltinPythonModule/*!*/ moduleInstance, Dictionary<string, PythonGlobal> globalsDict)
            : base(moduleInstance.GetType(), globalsDict) {
            _module = moduleInstance;
        }

        public override BuiltinPythonModule Instance {
            get {
                return _module;
            }
        }

    }
}
