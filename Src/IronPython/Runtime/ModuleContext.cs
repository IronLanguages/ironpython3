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
using System.Diagnostics;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime {
    /// <summary>
    /// Captures the globals and other state of module code.
    /// </summary>
    public sealed class ModuleContext {
        private readonly PythonContext/*!*/ _pyContext;
        private readonly PythonDictionary/*!*/ _globals;
        private readonly CodeContext/*!*/ _globalContext;
        private readonly PythonModule _module;
        private ExtensionMethodSet _extensionMethods = ExtensionMethodSet.Empty;
        private ModuleOptions _features;

        /// <summary>
        /// Creates a new ModuleContext which is backed by the specified dictionary.
        /// </summary>
        public ModuleContext(PythonDictionary/*!*/ globals, PythonContext/*!*/ creatingContext) {
            ContractUtils.RequiresNotNull(globals, "globals");
            ContractUtils.RequiresNotNull(creatingContext, "creatingContext");

            _globals = globals;
            _pyContext = creatingContext;
            _globalContext = new CodeContext(globals, this);
            _module = new PythonModule(globals);
            _module.Scope.SetExtension(_pyContext.ContextId, new PythonScopeExtension(_pyContext, _module, this));
        }

        /// <summary>
        /// Creates a new ModuleContext for the specified module.
        /// </summary>
        public ModuleContext(PythonModule/*!*/ module, PythonContext/*!*/ creatingContext) {
            ContractUtils.RequiresNotNull(module, "module");
            ContractUtils.RequiresNotNull(creatingContext, "creatingContext");

            _globals = module.__dict__;
            _pyContext = creatingContext;
            _globalContext = new CodeContext(_globals, this);
            _module = module;
        }

        /// <summary>
        /// Gets the dictionary used for the global variables in the module
        /// </summary>
        public PythonDictionary/*!*/ Globals {
            get {
                return _globals;
            }
        }

        /// <summary>
        /// Gets the language context which created this module.
        /// </summary>
        public PythonContext/*!*/ Context {
            get {
                return _pyContext;
            }
        }

        /// <summary>
        /// Gets the DLR Scope object which is associated with the modules dictionary.
        /// </summary>
        public Scope/*!*/ GlobalScope {
            get {
                return _module.Scope;
            }
        }

        /// <summary>
        /// Gets the global CodeContext object which is used for execution of top-level code.
        /// </summary>
        public CodeContext/*!*/ GlobalContext {
            get {
                return _globalContext;
            }
        }

        /// <summary>
        /// Gets the module object which this code is executing in.
        /// 
        /// This module may or may not be published in sys.modules.  For user defined
        /// code typically the module gets published at the start of execution.  But if
        /// this ModuleContext is attached to a Scope, or if we've just created a new
        /// module context for executing code it will not be in sys.modules.
        /// </summary>
        public PythonModule Module {
            get {
                return _module;
            }
        }

        /// <summary>
        /// Gets the features that code has been compiled with in the module.
        /// </summary>
        public ModuleOptions Features {
            get {
                return _features;
            }
            set {
                _features = value;
            }
        }

        /// <summary>
        /// Gets or sets whether code running in this context should display
        /// CLR members (for example .ToString on objects).
        /// </summary>
        public bool ShowCls {
            get {
                return (_features & ModuleOptions.ShowClsMethods) != 0;
            }
            set {
                Debug.Assert(this != this._pyContext.SharedContext.ModuleContext || !value);
                if (value) {
                    _features |= ModuleOptions.ShowClsMethods;
                } else {
                    _features &= ~ModuleOptions.ShowClsMethods;
                }
            }
        }

        internal ExtensionMethodSet ExtensionMethods {
            get {
                return _extensionMethods;
            }
            set {
                _extensionMethods = value;
            }
        }

        /// <summary>
        /// Initializes __builtins__ for the module scope.
        /// </summary>
        internal void InitializeBuiltins(bool moduleBuiltins) {
            // adds __builtin__ variable if necessary.  Python adds the module directly to
            // __main__ and __builtin__'s dictionary for all other modules.  Our callers
            // pass the appropriate flags to control this behavior.
            if (!Globals.ContainsKey("__builtins__")) {
                if (moduleBuiltins) {
                    Globals["__builtins__"] = Context.BuiltinModuleInstance;
                } else {
                    Globals["__builtins__"] = Context.BuiltinModuleDict;
                }
            }
        }
    }
}
