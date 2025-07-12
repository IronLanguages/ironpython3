// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

#if FEATURE_REMOTING
using System.Runtime.Remoting;
#else
using MarshalByRefObject = System.Object;
#endif

using System;
using System.Collections.Generic;
using System.Threading;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

using Microsoft.Scripting.Hosting;
using Microsoft.Scripting.Hosting.Providers;
using Microsoft.Scripting.Utils;

namespace IronPython.Hosting {
    /// <summary>
    /// Helper class for implementing the Python class.
    /// 
    /// This is exposed as a service through PythonEngine and the helper class
    /// uses this service to get the correct remoting semantics.
    /// </summary>
    public sealed class PythonService : MarshalByRefObject {
        private readonly ScriptEngine/*!*/ _engine;
        private readonly PythonContext/*!*/ _context;
        private ScriptScope? _sys, _builtins, _clr;

        public PythonService(PythonContext/*!*/ context, ScriptEngine/*!*/ engine) {
            Assert.NotNull(context, engine);
            _context = context;
            _engine = engine;
        }

        public ScriptScope/*!*/ GetSystemState() {
            if (_sys == null) {
                Interlocked.CompareExchange(
                    ref _sys,
                    HostingHelpers.CreateScriptScope(_engine, _context.SystemState.Scope),
                    null
                );
            }

            return _sys;
        }

        public ScriptScope/*!*/ GetBuiltins() {
            if (_builtins == null) {
                Interlocked.CompareExchange(
                    ref _builtins,
                    HostingHelpers.CreateScriptScope(_engine, _context.BuiltinModuleInstance.Scope),
                    null
                );
            }

            return _builtins;
        }

        public ScriptScope/*!*/ GetClr() {
            if (_clr == null) {
                Interlocked.CompareExchange(
                    ref _clr,
                    HostingHelpers.CreateScriptScope(_engine, _context.ClrModule.Scope),
                    null
                );
            }

            return _clr;
        }

        public ScriptScope/*!*/ CreateModule(string name, string filename, string docString) {
            var module = new PythonModule();
            _context.PublishModule(name, module);
            module.__init__(name, docString);
            module.__dict__["__file__"] = filename;

            return HostingHelpers.CreateScriptScope(_engine, module.Scope);
        }

        public ScriptScope/*!*/ ImportModule(ScriptEngine/*!*/ engine, string/*!*/ name) {
            if (Importer.ImportModule(_context.SharedClsContext, _context.SharedClsContext.GlobalDict, name, false, 0) is PythonModule module) {
                return HostingHelpers.CreateScriptScope(engine, module.Scope);
            }

            throw PythonOps.ImportError("no module named {0}", name);
        }

        public string[] GetModuleFilenames() {
            List<string> res = new List<string>();
            if ((object)_engine.GetSysModule().GetVariable("modules") is PythonDictionary dict) {
                foreach (var kvp in dict) {
                    if (kvp.Key is string key && kvp.Value is PythonModule module) {
                        var modDict = module.Get__dict__();
                        object file;
                        if (modDict.TryGetValue("__file__", out file) && file != null) {
                            res.Add(key);
                        }
                    }
                }
            }
            return res.ToArray();
        }

        public void DispatchCommand(Action command) {
            _context.DispatchCommand(command);
        }

#if FEATURE_REMOTING
        public ObjectHandle? GetSetCommandDispatcher(ObjectHandle dispatcher) {
            var res = _context.GetSetCommandDispatcher((Action<Action>)dispatcher.Unwrap());
            if (res != null) {
                return new ObjectHandle(res);
            }
                        
            return null;
        }

        /// <summary>
        /// Returns an ObjectHandle to a delegate of type Action[Action] which calls the current
        /// command dispatcher.
        /// </summary>
        public ObjectHandle GetLocalCommandDispatcher() {
            return new ObjectHandle((Action<Action>)(action => _context.DispatchCommand(action)));
        }

        public override object InitializeLifetimeService() {
            return _engine.InitializeLifetimeService();
        }
#endif
    }
}
