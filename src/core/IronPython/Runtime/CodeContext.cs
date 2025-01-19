// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Diagnostics;

using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Compiler;

namespace IronPython.Runtime {

    /// <summary>
    /// Captures and flows the state of executing code from the generated 
    /// Python code into the IronPython runtime.
    /// </summary>    
    [DebuggerTypeProxy(typeof(DebugProxy)), DebuggerDisplay("ModuleName = {ModuleName}")]
    public sealed class CodeContext {
        /// <summary>
        /// Creates a new CodeContext which is backed by the specified Python dictionary.
        /// </summary>
        public CodeContext(PythonDictionary/*!*/ dict, ModuleContext/*!*/ moduleContext) {
            ContractUtils.RequiresNotNull(dict, nameof(dict));
            ContractUtils.RequiresNotNull(moduleContext, nameof(moduleContext));
            Dict = dict;
            ModuleContext = moduleContext;
        }

        #region Public APIs

        /// <summary>
        /// Gets the module state for top-level code.
        /// </summary>   
        public ModuleContext ModuleContext { get; }

        /// <summary>
        /// Gets the DLR scope object that corresponds to the global variables of this context.
        /// </summary>
        public Scope GlobalScope => ModuleContext.GlobalScope;

        /// <summary>
        /// Gets the PythonContext which created the CodeContext.
        /// </summary>
        public PythonContext LanguageContext => ModuleContext.Context;

        #endregion

        #region Internal Helpers

        /// <summary>
        /// Gets the dictionary for the global variables from the ModuleContext.
        /// </summary>
        internal PythonDictionary GlobalDict => ModuleContext.Globals;

        /// <summary>
        /// True if this global context should display CLR members on shared types (for example .ToString on int/bool/etc...)
        /// 
        /// False if these attributes should be hidden.
        /// </summary>
        internal bool ShowCls {
            get {
                return ModuleContext.ShowCls;
            }
            set {
                ModuleContext.ShowCls = value;
            }
        }

        /// <summary>
        /// Attempts to lookup the provided name in this scope or any outer scope.
        /// </summary>
        internal bool TryLookupName(string name, out object? value)
            => Dict.TryGetValue(name, out value) || ModuleContext.Globals.TryGetValue(name, out value);

        /// <summary>
        /// Looks up a global variable.  If the variable is not defined in the
        /// global scope then built-ins is consulted.
        /// </summary>
        internal bool TryLookupBuiltin(string name, out object? value) {
            object builtins;
            if (!GlobalDict.TryGetValue("__builtins__", out builtins)) {
                value = null;
                return false;
            }

            if (builtins is PythonModule builtinsScope && builtinsScope.__dict__.TryGetValue(name, out value)) {
                return true;
            }

            if (builtins is PythonDictionary dict && dict.TryGetValue(name, out value)) {
                return true;
            }

            value = null;
            return false;
        }

        /// <summary>
        /// Gets the dictionary used for storage of local variables.
        /// </summary>
        internal PythonDictionary Dict { get; }

        /// <summary>
        /// Attempts to lookup the variable in the local scope.
        /// </summary>
        internal bool TryGetVariable(string name, out object? value)
            => Dict.TryGetValue(name, out value);

        /// <summary>
        /// Removes a variable from the local scope.
        /// </summary>
        internal bool TryRemoveVariable(string name)
            => Dict.Remove(name);

        /// <summary>
        /// Sets a variable in the local scope.
        /// </summary>
        internal void SetVariable(string name, object? value)
            => Dict.Add(name, value);

        /// <summary>
        /// Gets a variable from the global scope.
        /// </summary>
        internal bool TryGetGlobalVariable(string name, out object? res)
            => GlobalDict.TryGetValue(name, out res);


        /// <summary>
        /// Sets a variable in the global scope.
        /// </summary>
        internal void SetGlobalVariable(string name, object? value)
            => GlobalDict.Add(name, value);

        /// <summary>
        /// Removes a variable from the global scope.
        /// </summary>
        internal bool TryRemoveGlobalVariable(string name)
            => GlobalDict.Remove(name);

        internal PythonGlobal/*!*/[]? GetGlobalArray()
            => ((GlobalDictionaryStorage)Dict._storage).Data;

        internal bool IsTopLevel
            => Dict != ModuleContext.Globals;

        /// <summary>
        /// Returns the dictionary associated with __builtins__ if one is
        /// set or null if it's not available.  If __builtins__ is a module
        /// the module's dictionary is returned.
        /// </summary>
        internal PythonDictionary? GetBuiltinsDict() {
            if (GlobalDict._storage.TryGetBuiltins(out object? builtins)) {
                if (builtins is PythonModule builtinsScope) {
                    return builtinsScope.__dict__;
                }

                return builtins as PythonDictionary;
            }

            return null;
        }

        internal PythonModule Module => ModuleContext.Module;

        internal string ModuleName => Module.GetName();

        internal class DebugProxy {
            private readonly CodeContext _context;

            public DebugProxy(CodeContext context) {
                _context = context;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public PythonModule Members => _context.Module;
        }

        #endregion
    }
}
