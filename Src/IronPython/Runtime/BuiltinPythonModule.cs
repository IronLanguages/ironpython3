// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using IronPython.Compiler;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime {
    /// <summary>
    /// Implements a built-in module which is instanced per PythonContext.
    /// 
    /// Implementers can subclass this type and then have a module which has efficient access
    /// to internal state (this doesn't need to go through PythonContext.GetModuleState).  These
    /// modules can also declare module level globals which they'd like to provide efficient
    /// access to by overloading GetGlobalVariableNames.  When Initialize is called these
    /// globals are provided and can be cached in the instance for fast global access.
    /// 
    /// Just like normal static modules these modules are registered with the PythonModuleAttribute.
    /// </summary>
    public class BuiltinPythonModule {
        private readonly PythonContext/*!*/ _context;
        private CodeContext/*!*/ _codeContext;

        protected BuiltinPythonModule(PythonContext/*!*/ context) {
            ContractUtils.RequiresNotNull(context, nameof(context));

            _context = context;
        }

        /// <summary>
        /// Initializes the module for it's first usage.  By default this calls PerformModuleReload with the
        /// the dictionary.
        /// </summary>
        /// <param name="codeContext">The CodeContext for the module.</param>
        /// <param name="optimizedGlobals">A list of globals which have optimize access.  Contains at least all of the global variables reutrned by GetGlobalVariableNames.</param>
        protected internal virtual void Initialize(CodeContext/*!*/ codeContext, Dictionary<string/*!*/, PythonGlobal/*!*/>/*!*/ optimizedGlobals) {
            ContractUtils.RequiresNotNull(codeContext, nameof(codeContext));
            ContractUtils.RequiresNotNull(optimizedGlobals, nameof(optimizedGlobals));

            _codeContext = codeContext;

            PerformModuleReload();
        }

        /// <summary>
        /// Gets a list of variable names which should have optimized storage (instances of PythonGlobal objects).
        /// The module receives the global objects during the Initialize call and can hold onto them for
        /// direct access to global members.
        /// </summary>
        protected internal virtual IEnumerable<string/*!*/>/*!*/ GetGlobalVariableNames() {
            return ArrayUtils.EmptyStrings;
        }

        /// <summary>
        /// Called when the user attempts to reload() on your module and by the base class Initialize method.
        /// 
        /// This provides an opportunity to allocate any per-module data which is not simply function definitions.
        /// 
        /// A common usage here is to create exception objects which are allocated by the module using PythonExceptions.CreateSubType.
        /// </summary>
        protected internal virtual void PerformModuleReload() {
        }

        /// <summary>
        /// Provides access to the PythonContext which this module was created for.
        /// </summary>
        protected PythonContext/*!*/ Context {
            get {
                return _context;
            }
        }

        /// <summary>
        /// Provides access to the CodeContext for the module.  Returns null before Initialize() is called.
        /// </summary>
        protected CodeContext Globals {
            get {
                return _codeContext;
            }
        }
    }
}
