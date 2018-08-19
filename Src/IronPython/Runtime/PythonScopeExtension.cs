// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;

using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime {    
    /// <summary>
    /// Provides storage of IronPython specific data in the DLR Scope ScopeExtension.
    /// 
    /// This enables IronPython to track code compilation flags such as from __future__
    /// flags and import clr flags across multiple executions of user-provided scopes.
    /// </summary>
    class PythonScopeExtension : ScopeExtension {
        private readonly ModuleContext _modContext;
        private readonly PythonModule _module;
        private Dictionary<object, object> _objectKeys;

        public PythonScopeExtension(PythonContext context, Scope scope) : base(scope) {
            _module = new PythonModule(context, scope);
            _modContext = new ModuleContext(_module, context);
        }

        public PythonScopeExtension(PythonContext context, PythonModule module, ModuleContext modContext)
            : base(module.Scope) {
            _module = module;
            _modContext = modContext;
        }

        public ModuleContext ModuleContext {
            get {
                return _modContext;
            }
        }

        public PythonModule Module {
            get {
                return _module;
            }
        }

        public Dictionary<object, object> EnsureObjectKeys() {
            if (_objectKeys == null) {
                Interlocked.CompareExchange(ref _objectKeys, new Dictionary<object, object>(), null);
            }

            return _objectKeys;
        }

        public Dictionary<object, object> ObjectKeys {
            get {
                return _objectKeys;
            }
        }
    }
}
