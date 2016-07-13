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
#if FEATURE_FULL_CONSOLE

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using IronPython.Compiler;
using IronPython.Modules;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting.Shell;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace IronPython.Hosting {
    /// <summary>
    /// A simple Python command-line should mimic the standard python.exe
    /// </summary>
    public sealed class PythonCommandLine : CommandLine {
        private PythonContext PythonContext {
            get { return (PythonContext)Language; }
        }
        
        private new PythonConsoleOptions Options { get { return (PythonConsoleOptions)base.Options; } }
        
        public PythonCommandLine() {
        }

        protected override string/*!*/ Logo {
            get {
                return GetLogoDisplay();
            }
        }

        /// <summary>
        /// Returns the display look for IronPython.  
        /// 
        /// The returned string uses This \n instead of Environment.NewLine for it's line seperator 
        /// because it is intended to be outputted through the Python I/O system.
        /// </summary>
        public static string GetLogoDisplay() {
            return PythonContext.GetVersionString() +
                   "\nType \"help\", \"copyright\", \"credits\" or \"license\" for more information.\n";
        }

        private int GetEffectiveExitCode(SystemExitException/*!*/ e) {
            object nonIntegerCode;
            int exitCode = e.GetExitCode(out nonIntegerCode);
            if (nonIntegerCode != null) {
                Console.WriteLine(nonIntegerCode.ToString(), Style.Error);
            }
            return exitCode;
        }

        protected override void Shutdown() {
            try {
                Language.Shutdown();
            } catch (Exception e) {
                Console.WriteLine("", Style.Error);
                Console.WriteLine("Error in sys.exitfunc:", Style.Error);
                Console.Write(Language.FormatException(e), Style.Error);
            }
        }

        protected override int Run() {
            if (Options.ModuleToRun != null) {
                // PEP 338 support - http://www.python.org/dev/peps/pep-0338
                // This requires the presence of the Python standard library or
                // an equivalent runpy.py which defines a run_module method.

                // import the runpy module
                object runpy, runMod;
                try {
                    runpy = Importer.Import(
                        PythonContext.SharedContext,
                        "runpy",
                        PythonTuple.EMPTY,
                        0
                    );
                } catch (Exception) {
                    Console.WriteLine("Could not import runpy module", Style.Error);
                    return -1;
                }

                // get the run_module method
                try {
                    runMod = PythonOps.GetBoundAttr(PythonContext.SharedContext, runpy, "run_module");
                } catch (Exception) {
                    Console.WriteLine("Could not access runpy.run_module", Style.Error);
                    return -1;
                }

                // call it with the name of the module to run
                try {
                    PythonOps.CallWithKeywordArgs(
                        PythonContext.SharedContext,
                        runMod,
                        new object[] { Options.ModuleToRun, "__main__", ScriptingRuntimeHelpers.True },
                        new string[] { "run_name", "alter_sys" }
                    );
                } catch (SystemExitException e) {
                    object dummy;
                    return e.GetExitCode(out dummy);
                }

                return 0;
            }

            int result = base.Run();

            // Check if IRONPYTHONINSPECT was set during execution
            string inspectLine = Environment.GetEnvironmentVariable("IRONPYTHONINSPECT");
            if (inspectLine != null && !Options.Introspection)
                result = RunInteractiveLoop();

            return result;


        }

        #region Initialization

        protected override void Initialize() {
            Debug.Assert(Language != null);

            base.Initialize();

            Console.Output = new OutputWriter(PythonContext, false);
            Console.ErrorOutput = new OutputWriter(PythonContext, true);
            
            // TODO: must precede path initialization! (??? - test test_importpkg.py)
            int pathIndex = PythonContext.PythonOptions.SearchPaths.Count;
                        
            Language.DomainManager.LoadAssembly(typeof(string).Assembly);
            Language.DomainManager.LoadAssembly(typeof(System.Diagnostics.Debug).Assembly);

            InitializePath(ref pathIndex);
            InitializeModules();
            InitializeExtensionDLLs();

            ImportSite();

            // Equivalent to -i command line option
            // Check if IRONPYTHONINSPECT was set before execution
            string inspectLine = Environment.GetEnvironmentVariable("IRONPYTHONINSPECT");
            if (inspectLine != null)
                Options.Introspection = true;

            // If running in console mode (including with -c), the current working directory should be
            // the first entry in sys.path. If running a script file, however, the CWD should not be added;
            // instead, the script's containg folder should be added.

            string fullPath = "."; // this is a valid path resolving to current working dir. Pinky-swear.

            if (Options.Command == null && Options.FileName != null) {
                if (Options.FileName == "-") {
                    Options.FileName = "<stdin>";
                } else {
#if !SILVERLIGHT
                    if (Directory.Exists(Options.FileName)) {
                        Options.FileName = Path.Combine(Options.FileName, "__main__.py");
                    }

                    if (!File.Exists(Options.FileName)) {
                        Console.WriteLine(
                            String.Format(
                                System.Globalization.CultureInfo.InvariantCulture,
                                "File {0} does not exist.",
                                Options.FileName),
                            Style.Error);
                        Environment.Exit(1);
                    }
#endif
                    fullPath = Path.GetDirectoryName(
                        Language.DomainManager.Platform.GetFullPath(Options.FileName)
                    );
                }
            }

            PythonContext.InsertIntoPath(0, fullPath);
            PythonContext.MainThread = Thread.CurrentThread;
        }

        protected override Scope/*!*/ CreateScope() {
            ModuleOptions trueDiv = (PythonContext.PythonOptions.DivisionOptions == PythonDivisionOptions.New) ? ModuleOptions.TrueDivision : ModuleOptions.None;
            var modCtx = new ModuleContext(new PythonDictionary(), PythonContext);
            modCtx.Features = trueDiv;
            modCtx.InitializeBuiltins(true);

            PythonContext.PublishModule("__main__", modCtx.Module);
            modCtx.Globals["__doc__"] = null;
            modCtx.Globals["__name__"] = "__main__";

            return modCtx.GlobalScope;
        }
        
        private void InitializePath(ref int pathIndex) {
#if !SILVERLIGHT // paths, environment vars
            if (!Options.IgnoreEnvironmentVariables) {
                string path = Environment.GetEnvironmentVariable("IRONPYTHONPATH");
                if (path != null && path.Length > 0) {
                    string[] paths = path.Split(Path.PathSeparator);
                    foreach (string p in paths) {
                        PythonContext.InsertIntoPath(pathIndex++, p);
                    }
                }
            }
#endif
        }

        private void InitializeModules() {
            string executable = "";
            string prefix = null;
#if !SILVERLIGHT // paths     
            Assembly entryAssembly = Assembly.GetEntryAssembly();
            //Can be null if called from unmanaged code (VS integration scenario)
            if (entryAssembly != null) {
                executable = entryAssembly.Location;
                prefix = Path.GetDirectoryName(executable);
            }

            // Make sure there an IronPython Lib directory, and if not keep looking up
            while (prefix != null && !File.Exists(Path.Combine(prefix, "Lib/os.py"))) {
                prefix = Path.GetDirectoryName(prefix);
            }
#endif

            PythonContext.SetHostVariables(prefix ?? "", executable, null);
        }

        /// <summary>
        /// Loads any extension DLLs present in sys.prefix\DLLs directory and adds references to them.
        /// 
        /// This provides an easy drop-in location for .NET assemblies which should be automatically referenced
        /// (exposed via import), COM libraries, and pre-compiled Python code.
        /// </summary>
        private void InitializeExtensionDLLs() {
            string dir = Path.Combine(PythonContext.InitialPrefix, "DLLs");
            if (Directory.Exists(dir)) {
                foreach (string file in Directory.GetFiles(dir)) {
                    if (file.ToLower().EndsWith(".dll")) {
                        try {
                            ClrModule.AddReference(PythonContext.SharedContext, new FileInfo(file).Name);
                        } catch {
                        }
                    }
                }
            }
        }

        private void ImportSite() {
            if (Options.SkipImportSite)
                return;

            try {
                Importer.ImportModule(PythonContext.SharedContext, null, "site", false, -1);
            } catch (Exception e) {
                Console.Write(Language.FormatException(e), Style.Error);
            }
        }

        #endregion

        #region Interactive

        protected override int RunInteractive() {
            PrintLogo();
            if (Scope == null) {
                Scope = CreateScope();
            }

            int result = 1;
            try {
                RunStartup();
                result = 0;
            } catch (SystemExitException pythonSystemExit) {
                return GetEffectiveExitCode(pythonSystemExit);
            } catch (Exception) {
            }

            var sys = Engine.GetSysModule();
            
            sys.SetVariable("ps1", ">>> ");
            sys.SetVariable("ps2", "... ");

            result = RunInteractiveLoop();

            return (int)result;
        }

        protected override string Prompt {
            get {
                object value;
                if (Engine.GetSysModule().TryGetVariable("ps1", out value)) {
                    var context = ((PythonScopeExtension)Scope.GetExtension(Language.ContextId)).ModuleContext.GlobalContext;

                    return PythonOps.ToString(context, value);
                }

                return ">>> ";
            }
        }

        public override string PromptContinuation {
            get {
                object value;
                if (Engine.GetSysModule().TryGetVariable("ps2", out value)) {
                    var context = ((PythonScopeExtension)Scope.GetExtension(Language.ContextId)).ModuleContext.GlobalContext;

                    return PythonOps.ToString(context, value);
                }

                return "... ";
            }
        }
        
        private void RunStartup() {
            if (Options.IgnoreEnvironmentVariables)
                return;

#if !SILVERLIGHT // Environment.GetEnvironmentVariable
            string startup = Environment.GetEnvironmentVariable("IRONPYTHONSTARTUP");
            if (startup != null && startup.Length > 0) {
                if (Options.HandleExceptions) {
                    try {
                        ExecuteCommand(Engine.CreateScriptSourceFromFile(startup));
                    } catch (Exception e) {
                        if (e is SystemExitException) throw;
                        Console.Write(Language.FormatException(e), Style.Error);
                    }
                } else {
                    ExecuteCommand(Engine.CreateScriptSourceFromFile(startup));
                }
            }
#endif
        }



        protected override int? TryInteractiveAction() {
            try {
                try {
                    return TryInteractiveActionWorker();
                } finally {
                    // sys.exc_info() is normally cleared after functions exit. But interactive console enters statements
                    // directly instead of using functions. So clear explicitly.
                    PythonOps.ClearCurrentException();
                }
            } catch (SystemExitException se) {
                return GetEffectiveExitCode(se);
            }
        }

        /// <summary>
        /// Attempts to run a single interaction and handle any language-specific
        /// exceptions.  Base classes can override this and call the base implementation
        /// surrounded with their own exception handling.
        /// 
        /// Returns null if successful and execution should continue, or an exit code.
        /// </summary>
        private int? TryInteractiveActionWorker() {
            int? result = null;

            try {
                result = RunOneInteraction();
#if !FEATURE_EXCEPTION_STATE
            } catch (ThreadAbortException) {
#else
            } catch (ThreadAbortException tae) {
                KeyboardInterruptException pki = tae.ExceptionState as KeyboardInterruptException;
                if (pki != null) {
                    Console.WriteLine(Language.FormatException(tae), Style.Error);
                    Thread.ResetAbort();
                }
#endif
            }

            return result;
        }

        /// <summary>
        /// Parses a single interactive command and executes it.  
        /// 
        /// Returns null if successful and execution should continue, or the appropiate exit code.
        /// </summary>
        private int? RunOneInteraction() {
            bool continueInteraction;
            string s = ReadStatement(out continueInteraction);

            if (continueInteraction == false) {
                PythonContext.DispatchCommand(null); // Notify dispatcher that we're done
                return 0;
            }

            if (String.IsNullOrEmpty(s)) {
                // Is it an empty line?
                Console.Write(String.Empty, Style.Out);
                return null;
            }


            SourceUnit su = Language.CreateSnippet(s, "<stdin>", SourceCodeKind.InteractiveCode);
            PythonCompilerOptions pco = (PythonCompilerOptions)Language.GetCompilerOptions(Scope);
            pco.Module |= ModuleOptions.ExecOrEvalCode;

            Action action = delegate() {
                try {
                    su.Compile(pco, ErrorSink).Run(Scope);
                } catch (Exception e) {
                    if (e is SystemExitException) {
                        throw;
                    }
                    // Need to handle exceptions in the delegate so that they're not wrapped
                    // in a TargetInvocationException
                    UnhandledException(e);
                }
            };

            try {
                PythonContext.DispatchCommand(action);
            } catch (SystemExitException sx) {
                object dummy;
                return sx.GetExitCode(out dummy);
            }

            return null;
        }

        protected override ErrorSink/*!*/ ErrorSink {
            get { return ThrowingErrorSink.Default; }
        }

        protected override int GetNextAutoIndentSize(string text) {
            return Parser.GetNextAutoIndentSize(text, Options.AutoIndentSize);
        }

        #endregion

        #region Command

        protected override int RunCommand(string command) {
            if (Options.HandleExceptions) {
                try {
                    return RunCommandWorker(command);
                } catch (Exception e) {
                    Console.Write(Language.FormatException(e), Style.Error);
                    return 1;
                }
            } 

            return RunCommandWorker(command);            
        }

        private int RunCommandWorker(string command) {
            ScriptCode compiledCode;
            ModuleOptions trueDiv = (PythonContext.PythonOptions.DivisionOptions == PythonDivisionOptions.New) ?
                ModuleOptions.TrueDivision : ModuleOptions.None;
            ModuleOptions modOpt = ModuleOptions.Optimized | ModuleOptions.ModuleBuiltins | trueDiv;
                ;
            if (Options.SkipFirstSourceLine) {
                modOpt |= ModuleOptions.SkipFirstLine;
            }
            PythonModule module = PythonContext.CompileModule(
                "", // there is no file, it will be set to <module>
                "__main__",
                PythonContext.CreateSnippet(command, SourceCodeKind.File),
                modOpt,
                out compiledCode);
            PythonContext.PublishModule("__main__", module);
            Scope = module.Scope;
            try {
                compiledCode.Run(Scope);
            } catch (SystemExitException pythonSystemExit) {
                // disable introspection when exited:
                Options.Introspection = false;
                return GetEffectiveExitCode(pythonSystemExit);
            }
            return 0;
        }

        #endregion

        #region File

        protected override int RunFile(string/*!*/ fileName) {
            int result = 1;
            if (Options.HandleExceptions) {
                try {
                    result = RunFileWorker(fileName);
                } catch (Exception e) {
                    Console.Write(Language.FormatException(e), Style.Error);
                }
            } else {
                result = RunFileWorker(fileName);
            }

            return result;
        }        
        
        private int RunFileWorker(string/*!*/ fileName) {
            try {
                // There is no PEP for this case, only http://bugs.python.org/issue1739468
                object importer;
                if (Importer.TryImportMainFromZip(DefaultContext.Default, fileName, out importer)) {
                    return 0;
                }
                if (importer != null && importer.GetType() != typeof(PythonImport.NullImporter)) {
                    Console.WriteLine(String.Format("can't find '__main__' module in '{0}'", fileName), Style.Error);
                    return 0;
                }
            } catch (SystemExitException pythonSystemExit) {
                // disable introspection when exited:
                Options.Introspection = false;
                return GetEffectiveExitCode(pythonSystemExit);
            }

            // classic file
            ScriptCode compiledCode;
            ModuleOptions modOpt = ModuleOptions.Optimized | ModuleOptions.ModuleBuiltins;
            if(Options.SkipFirstSourceLine) {
                modOpt |= ModuleOptions.SkipFirstLine;

            }
            PythonModule module = PythonContext.CompileModule(
                fileName, 
                "__main__",
                PythonContext.CreateFileUnit(String.IsNullOrEmpty(fileName) ? null : fileName, PythonContext.DefaultEncoding),
                modOpt, 
                out compiledCode);
            PythonContext.PublishModule("__main__", module);
            Scope = module.Scope;

            try {
                compiledCode.Run(Scope);
            } catch (SystemExitException pythonSystemExit) {
                
                // disable introspection when exited:
                Options.Introspection = false;

                return GetEffectiveExitCode(pythonSystemExit);
            }

            return 0;
        }

        #endregion

        public override IList<string> GetGlobals(string name) {
            IList<string> res = base.GetGlobals(name);
            foreach (object builtinName in PythonContext.BuiltinModuleInstance.__dict__.Keys) {
                string strName = builtinName as string;
                if (strName != null && strName.StartsWith(name)) {
                    res.Add(strName);
                }
            }

            return res;
        }

        protected override void UnhandledException(Exception e) {
            PythonOps.PrintException(PythonContext.SharedContext, e, Console);
        }

        private new PythonContext Language {
            get {
                return (PythonContext)base.Language;
            }
        }

    }
}
#endif
