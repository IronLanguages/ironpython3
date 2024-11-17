// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

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
            PythonContext pc = context.LanguageContext;

            CodeContext newContext = _sc.CreateContext();
            newContext.ModuleContext.InitializeBuiltins(false);
            pc.InitializeModule(_sc.SourceUnit.Path, newContext.ModuleContext, _sc, ModuleOptions.Initialize);

            if (_parentName != null) {
                // if we are a module in a package update the parent package w/ our scope.
                object parent;
                if (pc.SystemStateModules.TryGetValue(_parentName, out parent)) {
                    if (parent is PythonModule s) {
                        s.__dict__[_name] = newContext.ModuleContext.Module;
                    }
                }
            }

            return newContext.ModuleContext.Module;
        }
    }

}
