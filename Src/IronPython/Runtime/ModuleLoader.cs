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
using Microsoft.Scripting;
using IronPython.Compiler;

namespace IronPython.Runtime {
    public sealed class ModuleLoader {
        private readonly OnDiskScriptCode _sc;
        private readonly string _parentName, _name;


        internal ModuleLoader(OnDiskScriptCode sc, string parentName, string name) {
            _sc = sc;
            _parentName = parentName;
            _name = name;
        }

        public PythonModule load_module(CodeContext/*!*/ context, string fullName) {
            PythonContext pc = PythonContext.GetContext(context);

            CodeContext newContext = _sc.CreateContext();
            newContext.ModuleContext.InitializeBuiltins(false);
            pc.InitializeModule(_sc.SourceUnit.Path, newContext.ModuleContext, _sc, ModuleOptions.Initialize);

            if (_parentName != null) {
                // if we are a module in a package update the parent package w/ our scope.
                object parent;
                if (pc.SystemStateModules.TryGetValue(_parentName, out parent)) {
                    PythonModule s = parent as PythonModule;
                    if (s != null) {
                        s.__dict__[_name] = newContext.ModuleContext.Module;
                    }
                }
            }

            return newContext.ModuleContext.Module;
        }
    }

}
