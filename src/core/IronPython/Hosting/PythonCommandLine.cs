// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;

using IronPython.Compiler;
using IronPython.Modules;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;

using Microsoft.Scripting;
using Microsoft.Scripting.Hosting.Shell;
using Microsoft.Scripting.Runtime;

namespace IronPython.Hosting {
    /// <summary>
    /// A simple Python command-line should mimic the standard python.exe
    /// </summary>
    public class PythonCommandLine : CommandLine {
        private PythonContext PythonContext => Language;

        private new PythonConsoleOptions Options => (PythonConsoleOptions)base.Options;

        public PythonCommandLine() {
        }

        protected override string? Logo => PythonContext.PythonOptions.Quiet ? null : GetLogoDisplay();

        /// <summary>
        /// Returns the display look for IronPython.
        ///
        /// The returned string uses This \n instead of Environment.NewLine for it's line seperator
        /// because it is intended to be outputted through the Python I/O system.
        /// </summary>
        public static string GetLogoDisplay() {
            return "IronPython " + PythonContext.GetVersionString() + " on " + PythonContext.GetPlatform() +
                   "\nType \"help\", \"copyright\", \"credits\" or \"license\" for more information.\n";
        }

        private int GetEffectiveExitCode(SystemExitException/*!*/ e) {
            object? nonIntegerCode;
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
                Console.WriteLine("Error in sys._exitfunc:", Style.Error);
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

                // get the _run_module_as_main method
                try {
                    runMod = PythonOps.GetBoundAttr(PythonContext.SharedContext, runpy, "_run_module_as_main");
                } catch (Exception) {
                    Console.WriteLine("Could not access runpy._run_module_as_main", Style.Error);
                    return -1;
                }

                if (Scope == null) {
                    Scope = CreateScope();
                }

                var argv = PythonContext.GetSystemStateValue("argv") as PythonList;
                if (argv is not null) {
                    argv[0] = "-m";
                }

                // call it with the name of the module to run
                try {
                    PythonCalls.Call(
                        PythonContext.SharedContext,
                        runMod,
                        Options.ModuleToRun
                    );
                } catch (SystemExitException e) {
                    return GetEffectiveExitCode(e);
                }

                return 0;
            }

            int result = base.Run();

            // Check if IRONPYTHONINSPECT was set during execution
            string? inspectLine = Environment.GetEnvironmentVariable("IRONPYTHONINSPECT");
            if (!string.IsNullOrEmpty(inspectLine) && !Options.Introspection)
                result = RunInteractiveLoop();

            return result;
        }

        protected override int RunInteractiveLoop() {
            var sys = Engine.GetSysModule();

            sys.SetVariable("ps1", ">>> ");
            sys.SetVariable("ps2", "... ");
            return base.RunInteractiveLoop();
        }

        #region Initialization

        protected override void Initialize() {
            base.Initialize();

            Console.Output = new OutputWriter(PythonContext, false);
            Console.ErrorOutput = new OutputWriter(PythonContext, true);
            Language.Console = Console;

            // TODO: must precede path initialization! (??? - test test_importpkg.py)
            int pathIndex = PythonContext.PythonOptions.SearchPaths.Count;

            Language.DomainManager.LoadAssembly(typeof(string).Assembly);
            Language.DomainManager.LoadAssembly(typeof(System.Diagnostics.Debug).Assembly);

            InitializePath(ref pathIndex);
            InitializeEnvironmentVariables();
            InitializeModules();
            InitializeExtensionDLLs();

            // ensure the warnings module loads
            var warnOptions = PythonContext.GetSystemStateValue("warnoptions") as PythonList;
            if (warnOptions?.Count > 0) {
                PythonContext.GetWarningsModule();
            }

            ImportSite();

            // Equivalent to -i command line option
            // Check if IRONPYTHONINSPECT was set before execution
            string? inspectLine = Environment.GetEnvironmentVariable("IRONPYTHONINSPECT");
            if (!string.IsNullOrEmpty(inspectLine))
                Options.Introspection = true;

            // If running in console mode (including with -c), the current working directory should be
            // the first entry in sys.path. If running a script file, however, the CWD should not be added;
            // instead, the script's containg folder should be added.

            string fullPath = "."; // this is a valid path resolving to current working dir. Pinky-swear.

            if (Options.Command == null && Options.FileName != null) {
                if (Options.FileName == "-") {
                    Options.FileName = "<stdin>";
                } else {
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

                    fullPath = Path.GetDirectoryName(
                        Language.DomainManager.Platform.GetFullPath(Options.FileName)
                    )!;
                }
            }

            if (!PythonContext.PythonOptions.Isolated) {
                PythonContext.InsertIntoPath(0, fullPath);
            }
            PythonContext.MainThread = Thread.CurrentThread;
        }

        protected override Scope/*!*/ CreateScope() {
            var modCtx = new ModuleContext(new PythonDictionary(), PythonContext);
            modCtx.Features = ModuleOptions.None;
            modCtx.InitializeBuiltins(true);

            PythonContext.PublishModule("__main__", modCtx.Module);
            modCtx.Globals["__doc__"] = null;
            modCtx.Globals["__name__"] = "__main__";
            modCtx.Globals["__package__"] = null;

            return modCtx.GlobalScope;
        }

        private void InitializePath(ref int pathIndex) {
            // paths, environment vars
            if (!Options.IgnoreEnvironmentVariables) {
                string? path = Environment.GetEnvironmentVariable("IRONPYTHONPATH");
                if (!string.IsNullOrEmpty(path)) {
                    string[] paths = path.Split(Path.PathSeparator);
                    foreach (string p in paths) {
                        PythonContext.InsertIntoPath(pathIndex++, p);
                    }
                }
            }
        }

        private void InitializeEnvironmentVariables() {
            if (!Options.IgnoreEnvironmentVariables) {
                string? warnings = Environment.GetEnvironmentVariable("IRONPYTHONWARNINGS");
                object o = PythonContext.GetSystemStateValue("warnoptions");
                if (o == null) {
                    o = new PythonList();
                    PythonContext.SetSystemStateValue("warnoptions", o);
                }

                if (o is PythonList warnoptions && !string.IsNullOrEmpty(warnings)) {
                    string[] warns = warnings.Split(',');
                    foreach (string warn in warns) {
                        warnoptions.Add(warn);
                    }
                }
            }
        }

        private void InitializeModules() {
            string executable = "";
            string? prefix = null;

            Assembly? entryAssembly = Assembly.GetEntryAssembly();
            // Can be null if called from unmanaged code (VS integration scenario)
            if (entryAssembly != null) {
                executable = entryAssembly.Location;
                prefix = Path.GetDirectoryName(executable)!;

                var name = Path.GetFileNameWithoutExtension(executable);
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                    var exename = name + ".exe";
                    var runner = Path.Combine(prefix, exename);
                    if (File.Exists(runner) || FindRunner(prefix, exename, executable, out runner)) {
                        executable = runner;
                    } else {
                        // ipy.bat is created Install-IronPython.ps1, which installs from a zip file
                        runner = Path.Combine(prefix, name + ".bat");
                        if (File.Exists(runner)) executable = runner;
                    }
                } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                    var runner = Path.Combine(prefix, name);
                    if (File.Exists(runner) || FindRunner(prefix, name, executable, out runner)) {
                        executable = runner;
                    } else {
                        runner = Path.Combine(prefix, name + ".sh");
                        if (File.Exists(runner)) executable = runner;
                    }
                }
            }

            if (prefix is not null) {
                string? pyvenv_prefix = null;

                // look for pyvenv.cfg in the current folder and then the parent folder
                var path = Path.Combine(prefix, "pyvenv.cfg");
                for (var i = 0; i < 2; i++) {
                    if (File.Exists(path)) {
                        foreach (var line in File.ReadAllLines(path, Encoding.UTF8)) { // TODO: this actually needs to be decoded with surrogateescape
                            if (line.StartsWith('#')) continue;
                            var split = line.Split(['='], 2);
                            if (split.Length != 2) continue;
                            if (split[0].Trim() == "home") {
                                pyvenv_prefix = split[1].Trim();
                                break;
                            }
                        }
                        break;
                    }
                    var parent = Path.GetDirectoryName(prefix);
                    if (parent is null) break;
                    path = Path.Combine(parent, "pyvenv.cfg");
                }

                prefix = pyvenv_prefix ?? prefix;
                // Make sure there an IronPython lib directory, and if not keep looking up
                while (prefix != null && !File.Exists(Path.Combine(prefix, "lib", "os.py"))) {
                    prefix = Path.GetDirectoryName(prefix);
                }
            }

            PythonContext.SetHostVariables(prefix ?? "", executable, null);


            // --- Local functions -------

            static bool FindRunner([DisallowNull] string? prefix, string name, string assembly, [NotNullWhen(true)] out string? runner) {
                runner = string.Empty;
#if NET
                while (prefix != null) {
                    runner = Path.Combine(prefix, name);
                    if (File.Exists(runner)) {
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || IsExecutable(runner)) {
                            break;
                        }
                    }
                    prefix = Path.GetDirectoryName(prefix);
                }
                if (prefix != null && Path.GetExtension(assembly).Equals(".dll", StringComparison.OrdinalIgnoreCase)) {
                    // make sure that the runner refers to this DLL
                    var relativeAssemblyPath = assembly.Substring(prefix.Length + 1); // skip over the path separator
                    byte[] fsAssemblyPath = Encoding.UTF8.GetBytes(relativeAssemblyPath);
                    byte fsap0 = fsAssemblyPath[0];

                    try {
                        using var mmf = MemoryMappedFile.CreateFromFile(runner, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
                        using var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

                        for (long i = accessor.Capacity - fsAssemblyPath.Length; i >= 0; i--) { // the path should be close to the end of the file
                            if (accessor.ReadByte(i) != fsap0) continue;

                            bool found = true;
                            for (int j = 1; j < fsAssemblyPath.Length; j++) {
                                if (accessor.ReadByte(i + j) != fsAssemblyPath[j]) {
                                    found = false;
                                    break;
                                }
                            }
                            if (found) return true;
                        }
                    } catch { }  // if reading the file fails, it is not our runner
                }
#endif
                runner = null;
                return false;
            }

#if NET
            [UnsupportedOSPlatform("windows")]
            static bool IsExecutable(string filePath) {
                var fileInfo = new Mono.Unix.UnixFileInfo(filePath);
                var fileMode = fileInfo.FileAccessPermissions;

                return (fileMode & (Mono.Unix.FileAccessPermissions.UserExecute | Mono.Unix.FileAccessPermissions.GroupExecute | Mono.Unix.FileAccessPermissions.OtherExecute)) != 0;
            }
#endif
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
                foreach (string file in Directory.EnumerateFiles(dir, "*.dll")) {
                    if (file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
                        try {
                            ClrModule.AddReferenceToFile(PythonContext.SharedContext, new FileInfo(file).Name);
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
                Importer.ImportModule(PythonContext.SharedContext, null, "site", false, 0);
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

            string? startup = Environment.GetEnvironmentVariable("IRONPYTHONSTARTUP");
            if (!string.IsNullOrEmpty(startup)) {
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
                if (tae.ExceptionState is KeyboardInterruptException pki) {
#pragma warning disable SYSLIB0006 // Thread.Abort is not supported and throws PlatformNotSupportedException on .NET Core.
                    Thread.ResetAbort();
#pragma warning restore SYSLIB0006
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

            Action action = delegate () {
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
                return GetEffectiveExitCode(sx);
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
                    Console.WriteLine(Language.FormatException(e), Style.Error);
                    return 1;
                }
            }

            return RunCommandWorker(command);
        }

        private int RunCommandWorker(string command) {
            ScriptCode compiledCode;
            ModuleOptions modOpt = ModuleOptions.Optimized | ModuleOptions.ModuleBuiltins;
            if (Options.SkipFirstSourceLine) {
                modOpt |= ModuleOptions.SkipFirstLine;
            }
            PythonModule module = PythonContext.CompileModule(
                "", // there is no file, it will be set to <module>
                "__main__",
                PythonContext.CreateSnippet(command, "<string>", SourceCodeKind.File),
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
                    UnhandledException(e);
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
            if (Options.SkipFirstSourceLine) {
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
                if (builtinName is string strName && strName.StartsWith(name, StringComparison.Ordinal)) {
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
