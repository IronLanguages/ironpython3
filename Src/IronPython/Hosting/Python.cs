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

using Microsoft.Scripting.Utils;
using Microsoft.Scripting.Hosting;
using Microsoft.Scripting.Hosting.Providers;

using IronPython.Modules;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;

namespace IronPython.Hosting {

    /// <summary>
    /// Provides helpers for interacting with IronPython.
    /// </summary>
    public static class Python {

        #region Public APIs

        /// <summary>
        /// Creates a new ScriptRuntime with the IronPython scipting engine pre-configured.
        /// </summary>
        /// <returns></returns>
        public static ScriptRuntime/*!*/ CreateRuntime() {
            return new ScriptRuntime(CreateRuntimeSetup(null));
        }

        /// <summary>
        /// Creates a new ScriptRuntime with the IronPython scipting engine pre-configured and
        /// additional options.
        /// </summary>
        public static ScriptRuntime/*!*/ CreateRuntime(IDictionary<string, object> options) {
            return new ScriptRuntime(CreateRuntimeSetup(options));
        }

#if FEATURE_REMOTING
        /// <summary>
        /// Creates a new ScriptRuntime with the IronPython scripting engine pre-configured
        /// in the specified AppDomain.  The remote ScriptRuntime may  be manipulated from 
        /// the local domain but all code will run in the remote domain.
        /// </summary>
        public static ScriptRuntime/*!*/ CreateRuntime(AppDomain/*!*/ domain) {
            ContractUtils.RequiresNotNull(domain, nameof(domain));

            return ScriptRuntime.CreateRemote(domain, CreateRuntimeSetup(null));
        }

        /// <summary>
        /// Creates a new ScriptRuntime with the IronPython scripting engine pre-configured
        /// in the specified AppDomain with additional options.  The remote ScriptRuntime may 
        /// be manipulated from the local domain but all code will run in the remote domain.
        /// </summary>
        public static ScriptRuntime/*!*/ CreateRuntime(AppDomain/*!*/ domain, IDictionary<string, object> options) {
            ContractUtils.RequiresNotNull(domain, nameof(domain));

            return ScriptRuntime.CreateRemote(domain, CreateRuntimeSetup(options));
        }

#endif

        /// <summary>
        /// Creates a new ScriptRuntime and returns the ScriptEngine for IronPython. If
        /// the ScriptRuntime is required it can be acquired from the Runtime property
        /// on the engine.
        /// </summary>
        public static ScriptEngine/*!*/ CreateEngine() {
            return GetEngine(CreateRuntime());
        }

        /// <summary>
        /// Creates a new ScriptRuntime with the specified options and returns the 
        /// ScriptEngine for IronPython. If the ScriptRuntime is required it can be 
        /// acquired from the Runtime property on the engine.
        /// </summary>
        public static ScriptEngine/*!*/ CreateEngine(IDictionary<string, object> options) {
            return GetEngine(CreateRuntime(options));
        }

#if FEATURE_REMOTING

        /// <summary>
        /// Creates a new ScriptRuntime and returns the ScriptEngine for IronPython. If
        /// the ScriptRuntime is required it can be acquired from the Runtime property
        /// on the engine.
        /// 
        /// The remote ScriptRuntime may be manipulated from the local domain but 
        /// all code will run in the remote domain.
        /// </summary>
        public static ScriptEngine/*!*/ CreateEngine(AppDomain/*!*/ domain) {
            return GetEngine(CreateRuntime(domain));
        }

        /// <summary>
        /// Creates a new ScriptRuntime with the specified options and returns the 
        /// ScriptEngine for IronPython. If the ScriptRuntime is required it can be 
        /// acquired from the Runtime property on the engine.
        /// 
        /// The remote ScriptRuntime may be manipulated from the local domain but 
        /// all code will run in the remote domain.
        /// </summary>
        public static ScriptEngine/*!*/ CreateEngine(AppDomain/*!*/ domain, IDictionary<string, object> options) {
            return GetEngine(CreateRuntime(domain, options));
        }

#endif

        /// <summary>
        /// Given a ScriptRuntime gets the ScriptEngine for IronPython.
        /// </summary>
        public static ScriptEngine/*!*/ GetEngine(ScriptRuntime/*!*/ runtime) {
            return runtime.GetEngineByTypeName(typeof(PythonContext).AssemblyQualifiedName);
        }

        /// <summary>
        /// Gets a ScriptScope which is the Python sys module for the provided ScriptRuntime.
        /// </summary>
        public static ScriptScope/*!*/ GetSysModule(this ScriptRuntime/*!*/ runtime) {
            ContractUtils.RequiresNotNull(runtime, nameof(runtime));

            return GetSysModule(GetEngine(runtime));
        }

        /// <summary>
        /// Gets a ScriptScope which is the Python sys module for the provided ScriptEngine.
        /// </summary>
        public static ScriptScope/*!*/ GetSysModule(this ScriptEngine/*!*/ engine) {
            ContractUtils.RequiresNotNull(engine, nameof(engine));

            return GetPythonService(engine).GetSystemState();
        }

        /// <summary>
        /// Gets a ScriptScope which is the Python __builtin__ module for the provided ScriptRuntime.
        /// </summary>
        public static ScriptScope/*!*/ GetBuiltinModule(this ScriptRuntime/*!*/ runtime) {
            ContractUtils.RequiresNotNull(runtime, nameof(runtime));

            return GetBuiltinModule(GetEngine(runtime));
        }

        /// <summary>
        /// Gets a ScriptScope which is the Python __builtin__ module for the provided ScriptEngine.
        /// </summary>
        public static ScriptScope/*!*/ GetBuiltinModule(this ScriptEngine/*!*/ engine) {
            ContractUtils.RequiresNotNull(engine, nameof(engine));

            return GetPythonService(engine).GetBuiltins();
        }

        /// <summary>
        /// Gets a ScriptScope which is the Python clr module for the provided ScriptRuntime.
        /// </summary>
        public static ScriptScope/*!*/ GetClrModule(this ScriptRuntime/*!*/ runtime) {
            ContractUtils.RequiresNotNull(runtime, nameof(runtime));

            return GetClrModule(GetEngine(runtime));
        }

        /// <summary>
        /// Gets a ScriptScope which is the Python clr module for the provided ScriptEngine.
        /// </summary>
        public static ScriptScope/*!*/ GetClrModule(this ScriptEngine/*!*/ engine) {
            ContractUtils.RequiresNotNull(engine, nameof(engine));

            return GetPythonService(engine).GetClr();
        }

        /// <summary>
        /// Imports the Python module by the given name and returns its ScriptSCope.  If the 
        /// module does not exist an exception is raised.
        /// </summary>
        public static ScriptScope/*!*/ ImportModule(this ScriptRuntime/*!*/ runtime, string/*!*/ moduleName) {
            ContractUtils.RequiresNotNull(runtime, nameof(runtime));
            ContractUtils.RequiresNotNull(moduleName, nameof(moduleName));

            return ImportModule(GetEngine(runtime), moduleName);
        }

        /// <summary>
        /// Imports the Python module by the given name and returns its ScriptSCope.  If the 
        /// module does not exist an exception is raised.
        /// </summary>
        public static ScriptScope/*!*/ ImportModule(this ScriptEngine/*!*/ engine, string/*!*/ moduleName) {
            ContractUtils.RequiresNotNull(engine, nameof(engine));
            ContractUtils.RequiresNotNull(moduleName, nameof(moduleName));

            return GetPythonService(engine).ImportModule(engine, moduleName);
        }

        /// <summary>
        /// Imports the Python module by the given name and inserts it into the ScriptScope as that name. If the
        /// module does not exist an exception is raised.
        /// </summary>
        /// <param name="scope"></param>
        /// <param name="moduleName"></param>
        public static void ImportModule (this ScriptScope/*!*/ scope, string/*!*/ moduleName) {
            ContractUtils.RequiresNotNull (scope, nameof(scope));
            ContractUtils.RequiresNotNull (moduleName, nameof(moduleName));

            scope.SetVariable (moduleName, scope.Engine.ImportModule (moduleName));
        }

        /// <summary>
        /// Sets sys.exec_prefix, sys.executable and sys.version and adds the prefix to sys.path
        /// </summary>
        public static void SetHostVariables(this ScriptRuntime/*!*/ runtime, string/*!*/ prefix, string/*!*/ executable, string/*!*/ version) {
            ContractUtils.RequiresNotNull(runtime, nameof(runtime));
            ContractUtils.RequiresNotNull(prefix, nameof(prefix));
            ContractUtils.RequiresNotNull(executable, nameof(executable));
            ContractUtils.RequiresNotNull(version, nameof(version));

            GetPythonContext(GetEngine(runtime)).SetHostVariables(prefix, executable, version);
        }

        /// <summary>
        /// Sets sys.exec_prefix, sys.executable and sys.version and adds the prefix to sys.path
        /// </summary>
        public static void SetHostVariables(this ScriptEngine/*!*/ engine, string/*!*/ prefix, string/*!*/ executable, string/*!*/ version) {
            ContractUtils.RequiresNotNull(engine, nameof(engine));
            ContractUtils.RequiresNotNull(prefix, nameof(prefix));
            ContractUtils.RequiresNotNull(executable, nameof(executable));
            ContractUtils.RequiresNotNull(version, nameof(version));

            GetPythonContext(engine).SetHostVariables(prefix, executable, version);
        }

        /// <summary>
        /// Enables call tracing for the current thread in this ScriptEngine.  
        /// 
        /// TracebackDelegate will be called back for each function entry, exit, exception, and line change.
        /// </summary>
        public static void SetTrace(this ScriptEngine/*!*/ engine, TracebackDelegate traceFunc) {
            SysModule.settrace(GetPythonContext(engine).SharedContext, traceFunc);
        }

        /// <summary>
        /// Enables call tracing for the current thread for the Python engine in this ScriptRuntime.  
        /// 
        /// TracebackDelegate will be called back for each function entry, exit, exception, and line change.
        /// </summary>
        public static void SetTrace(this ScriptRuntime/*!*/ runtime, TracebackDelegate traceFunc) {
            SetTrace(GetEngine(runtime), traceFunc);
        }

        /// <summary>
        /// Provides nested level debugging support when SetTrace or SetProfile are used.
        /// 
        /// This saves the current tracing information and then calls the provided object.
        /// </summary>
        public static void CallTracing(this ScriptRuntime/*!*/ runtime, object traceFunc, params object[] args) {
            CallTracing(GetEngine(runtime), traceFunc, args);
        }

        /// <summary>
        /// Provides nested level debugging support when SetTrace or SetProfile are used.
        /// 
        /// This saves the current tracing information and then calls the provided object.
        /// </summary>
        public static void CallTracing(this ScriptEngine/*!*/ engine, object traceFunc, params object[] args) {
            SysModule.call_tracing(GetPythonContext(engine).SharedContext, traceFunc, PythonTuple.MakeTuple(args));
        }

        /// <summary>
        /// Creates a ScriptRuntimeSetup object which includes the Python script engine with the specified options.
        /// 
        /// The ScriptRuntimeSetup object can then be additional configured and used to create a ScriptRuntime.
        /// </summary>
        public static ScriptRuntimeSetup/*!*/ CreateRuntimeSetup(IDictionary<string, object> options) {
            ScriptRuntimeSetup setup = new ScriptRuntimeSetup();
            setup.LanguageSetups.Add(CreateLanguageSetup(options));

            if (options != null) {
                object value;
                if (options.TryGetValue("Debug", out value) &&
                    value is bool &&
                    (bool)value) {
                    setup.DebugMode = true;
                }

                if (options.TryGetValue("PrivateBinding", out value) &&
                    value is bool &&
                    (bool)value) {
                    setup.PrivateBinding = true;
                }
            }

            return setup;
        }

        /// <summary>
        /// Creates a LanguageSetup object which includes the Python script engine with the specified options.
        /// 
        /// The LanguageSetup object can be used with other LanguageSetup objects from other languages to
        /// configure a ScriptRuntimeSetup object.
        /// </summary>
        public static LanguageSetup/*!*/ CreateLanguageSetup(IDictionary<string, object> options) {
            var setup = new LanguageSetup(
                typeof(PythonContext).AssemblyQualifiedName,
                PythonContext.IronPythonDisplayName,
                PythonContext.IronPythonNames.Split(';'),
                PythonContext.IronPythonFileExtensions.Split(';')
            );

            if (options != null) {
                foreach (var entry in options) {
                    setup.Options.Add(entry.Key, entry.Value);
                }
            }

            return setup;
        }

        /// <summary>
        /// Creates a new PythonModule with the specified name and published it in sys.modules.  
        /// 
        /// Returns the ScriptScope associated with the module.
        /// </summary>
        public static ScriptScope CreateModule(this ScriptEngine engine, string name) {
            return GetPythonService(engine).CreateModule(name, String.Empty, String.Empty);
        }

        /// <summary>
        /// Creates a new PythonModule with the specified name and filename published it 
        /// in sys.modules.  
        /// 
        /// Returns the ScriptScope associated with the module.
        /// </summary>
        public static ScriptScope CreateModule(this ScriptEngine engine, string name, string filename) {
            return GetPythonService(engine).CreateModule(name, filename, String.Empty);
        }

        /// <summary>
        /// Creates a new PythonModule with the specified name, filename, and doc string and 
        /// published it in sys.modules.  
        /// 
        /// Returns the ScriptScope associated with the module.
        /// </summary>
        public static ScriptScope CreateModule(this ScriptEngine engine, string name, string filename, string docString) {
            return GetPythonService(engine).CreateModule(name, filename, docString);
        }

        /// <summary>
        /// Gets the list of loaded Python module files names which are available in the provided ScriptEngine.
        /// </summary>
        public static string[] GetModuleFilenames(this ScriptEngine engine) {
            return GetPythonService(engine).GetModuleFilenames();
        }

        #endregion

        #region Private helpers

        private static PythonService/*!*/ GetPythonService(ScriptEngine/*!*/ engine) {
            return engine.GetService<PythonService>(engine);
        }

        private static PythonContext/*!*/ GetPythonContext(ScriptEngine/*!*/ engine) {
            return HostingHelpers.GetLanguageContext(engine) as PythonContext;
        }

        #endregion
    }
}
