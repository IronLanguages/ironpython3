// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using System.Threading;

using IronPython;
using IronPython.Hosting;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Types;

using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;

using IronPythonTest.Util;

namespace IronPythonTest.Cases {
    internal class CaseExecuter {
        private static string Executable {
            get {
                var folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string runner;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                    runner = Path.Combine(folder, "ipy.exe");
                    if (File.Exists(runner)) return runner;
                } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                    runner = Path.Combine(folder, "ipy");
                    if (File.Exists(runner)) return runner;
                    runner = Path.Combine(folder, "ipy.sh");
                    if (File.Exists(runner)) return runner;
                }
                throw new FileNotFoundException();
            }
        }

        private static readonly string IRONPYTHONPATH = GetIronPythonPath();

        private ScriptEngine defaultEngine;

        public static ScriptEngine CreateEngine(TestOptions options) {
            var engine = Python.CreateEngine(new Dictionary<string, object> {
                {"Debug", options.Debug },
                {"Frames", options.Frames || options.FullFrames },
                {"FullFrames", options.FullFrames },
                {"RecursionLimit", options.MaxRecursion },
                {"Tracing", options.Tracing }
            });

            engine.SetHostVariables(
                Path.GetDirectoryName(Executable),
                Executable,
                "");

            AddSearchPaths(engine);
            return engine;
        }

        internal static string FindRoot() {
            // we start at the current directory and look up until we find the "src" directory
            var current = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var found = false;
            while (!found && !string.IsNullOrEmpty(current)) {
                var test = Path.Combine(current, "src", "core", "IronPython.StdLib", "lib");
                if (Directory.Exists(test)) {
                    return current;
                }

                current = Path.GetDirectoryName(current);
            }
            return string.Empty;
        }

        private static void AddSearchPaths(ScriptEngine engine) {
            var paths = new List<string>(engine.GetSearchPaths());
            if (!paths.Any(x => x.Contains("stdlib", StringComparison.OrdinalIgnoreCase))) {
                var root = FindRoot();
                if (!string.IsNullOrEmpty(root)) {
                    paths.Insert(0, Path.Combine(root, "src", "core", "IronPython.StdLib", "lib"));
                }
            }
            engine.SetSearchPaths(paths);
        }

        public CaseExecuter() {
            this.defaultEngine = Python.CreateEngine();

            this.defaultEngine.SetHostVariables(
                Path.GetDirectoryName(Executable),
                Executable,
                "");
            AddSearchPaths(this.defaultEngine);
        }

        public int RunTest(TestInfo testcase) {
            int retryCount = testcase.Options.RetryCount;
            for (int i = 0; i < retryCount; i++) {
                try {
                    var res = RunTestImpl(testcase);
                    if (res == 0) return res;
                    NUnit.Framework.TestContext.Progress.WriteLine($"Test {testcase.Name} failed, retrying again. Retry #{i + 1}");
                } catch { }
            }

            return RunTestImpl(testcase);
        }

        private int RunTestImpl(TestInfo testcase) {
            var isolationLevel = testcase.Options.IsolationLevel;
            // when RUN_IGNORED is set, always isolate ignored tests
            if (testcase.Options.Ignore) {
                isolationLevel = TestIsolationLevel.PROCESS;
            }

            switch (isolationLevel) {
                case TestIsolationLevel.SCOPE:
                    return GetScopeTest(testcase);

                case TestIsolationLevel.ENGINE:
                    return GetEngineTest(testcase);

                case TestIsolationLevel.PROCESS:
                    return GetProcessTest(testcase);

                default:
                    throw new ArgumentException($"IsolationLevel {testcase.Options.IsolationLevel} is not supported.", "testcase.IsolationLevel");
            }
        }

        public string FormatException(Exception ex) {
            return this.defaultEngine.GetService<ExceptionOperations>().FormatException(ex);
        }

        private static string GetIronPythonPath() {
            var path = Path.Combine(FindRoot(), "src", "core", "IronPython.StdLib", "lib");
            if (Directory.Exists(path)) {
                return path;
            }
            return string.Empty;
        }

        private string ReplaceVariables(string input, Dictionary<string, string> replacements) {
            Regex variableRegex = new Regex(@"\$\(([^}]+)\)", RegexOptions.Compiled);

            var result = input;
            var match = variableRegex.Match(input);
            while (match.Success) {
                var variable = match.Groups[1].Value;
                if (replacements.TryGetValue(variable, out var replacement)) {
                    result = result.Replace(match.Groups[0].Value, replacement);
                }
                match = match.NextMatch();
            }

            return result;
        }

        private int GetEngineTest(TestInfo testcase) {
            if (testcase.Options.Arguments != null) {
                throw new Exception("Arguments have no effect with IsolationLevel=SCOPE or ENGINE, use PROCESS instead.");
            }

            var engine = CreateEngine(testcase.Options);
            var source = engine.CreateScriptSourceFromFile(
               testcase.Path, System.Text.Encoding.UTF8, SourceCodeKind.File);

            return GetResult(testcase, engine, source, testcase.Path, testcase.Options.WorkingDirectory);
        }

        private int GetProcessTest(TestInfo testcase) {
            int exitCode = -1;
            var argReplacements = new Dictionary<string, string>() {
                { "TEST_FILE", testcase.Path },
                { "TEST_FILE_DIR", Path.GetDirectoryName(testcase.Path) }
            };

            var wdReplacements = new Dictionary<string, string>() {
                { "ROOT", FindRoot() },
                { "TEST_FILE_DIR", Path.GetDirectoryName(testcase.Path) }
            };

            // add the arguments - in the normal case no arguments should be added
            var arguments = new List<string>();
            if (testcase.Options.Debug)
                arguments.Add("-X:Debug");
            if (!testcase.Options.Frames)
                arguments.Add("-X:NoFrames");
            if (testcase.Options.FullFrames)
                arguments.Add("-X:FullFrames");
            if (testcase.Options.MaxRecursion != int.MaxValue)
                arguments.Add($"-X:MaxRecursion {testcase.Options.MaxRecursion}");
            if (testcase.Options.Tracing)
                arguments.Add("-X:Tracing");
            arguments.Add(ReplaceVariables(testcase.Options.Arguments ?? "\"$(TEST_FILE)\"", argReplacements));

            using (Process proc = new Process()) {
                proc.StartInfo.FileName = Executable;
                proc.StartInfo.Arguments = string.Join(" ", arguments);

                if (!string.IsNullOrEmpty(IRONPYTHONPATH)) {
                    proc.StartInfo.EnvironmentVariables["IRONPYTHONPATH"] = IRONPYTHONPATH;
                }

                if (!string.IsNullOrEmpty(testcase.Options.WorkingDirectory)) {
                    proc.StartInfo.WorkingDirectory = ReplaceVariables(testcase.Options.WorkingDirectory, wdReplacements);
                }

                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardError = proc.StartInfo.RedirectStandardOutput = testcase.Options.Redirect;
                proc.Start();

                if (testcase.Options.Redirect) {
                    AsyncStreamReader(proc.StandardOutput, data => NUnit.Framework.TestContext.Out.Write(data));
                    AsyncStreamReader(proc.StandardError, data => NUnit.Framework.TestContext.Error.Write(data));
                }

                if (!proc.WaitForExit(testcase.Options.Timeout)) {
                    proc.Kill();
                    NUnit.Framework.TestContext.Error.WriteLine($"{testcase.Name} timed out after {testcase.Options.Timeout / 1000.0} seconds.");
                }
                exitCode = proc.ExitCode;
            }
            return exitCode;

            void AsyncStreamReader(StreamReader reader, Action<string> handler) {
                byte[] buffer = new byte[4096];
                BeginReadAsync();

                void BeginReadAsync() {
                    reader.BaseStream.BeginRead(buffer, 0, buffer.Length, new AsyncCallback(ReadCallback), null);
                }

                void ReadCallback(IAsyncResult asyncResult) {
                    var bytesRead = reader.BaseStream.EndRead(asyncResult);

                    if (bytesRead > 0) {
                        var data = reader.CurrentEncoding.GetString(buffer, 0, bytesRead);
                        handler?.Invoke(data);
                        BeginReadAsync();
                    }
                }
            }
        }

        private int GetScopeTest(TestInfo testcase) {
            if (testcase.Options.Debug
                    || !testcase.Options.Frames
                    || testcase.Options.FullFrames
                    || testcase.Options.MaxRecursion != int.MaxValue
                    || testcase.Options.Tracing) {
                throw new Exception("Options have no effect with IsolationLevel=SCOPE, use ENGINE or PROCESS instead.");
            }

            if (testcase.Options.Arguments != null) {
                throw new Exception("Arguments have no effect with IsolationLevel=SCOPE or ENGINE, use PROCESS instead.");
            }

            var source = this.defaultEngine.CreateScriptSourceFromString(
                testcase.Text, testcase.Path, SourceCodeKind.File);

            return GetResult(testcase, this.defaultEngine, source, testcase.Path, testcase.Options.WorkingDirectory);
        }

        private int GetResult(TestInfo testcase, ScriptEngine engine, ScriptSource source, string testPath, string workingDir) {
            var path = Environment.GetEnvironmentVariable("IRONPYTHONPATH");
            if (string.IsNullOrEmpty(path)) {
                Environment.SetEnvironmentVariable("IRONPYTHONPATH", IRONPYTHONPATH);
            }

            var cwd = Environment.CurrentDirectory;
            if (!string.IsNullOrWhiteSpace(workingDir)) {
                var replacements = new Dictionary<string, string>() {
                    { "ROOT", FindRoot() },
                    { "TEST_FILE_DIR", Path.GetDirectoryName(testPath) }
                };
                Environment.CurrentDirectory = ReplaceVariables(workingDir, replacements);
            }

            try {
                var scope = engine.CreateScope();
                engine.GetSysModule().SetVariable("argv", PythonList.FromArrayNoCopy(new object[] { source.Path }));
                var compiledCode = source.Compile(new IronPython.Compiler.PythonCompilerOptions() { ModuleName = "__main__" });

                ExceptionDispatchInfo exceptionInfo = null;

                int res = -1;
                int maxStackSize = 2 * 1024 * 1024; // 2 MiB
                var thread = new Thread(() => {
                    try {
                        res = engine.Operations.ConvertTo<int>(compiledCode.Execute(scope) ?? 0);
                    } catch (SystemExitException ex) {
                        res = ex.GetExitCode(out object otherCode);
                    } catch (ThreadAbortException) {
                        #pragma warning disable SYSLIB0006 // 'Thread.ResetAbort is not supported and throws PlatformNotSupportedException.' 
                        Thread.ResetAbort();
                        #pragma warning restore SYSLIB0006
                    } catch (Exception ex) {
                        if (ex.GetPythonException() is object pex && DynamicHelpers.GetPythonType(pex).Name == "SkipTest") {
                            NUnit.Framework.TestContext.Progress.WriteLine($"Test {testcase.Name} skipped: {pex}");
                            res = 0;
                        } else {
                            exceptionInfo = ExceptionDispatchInfo.Capture(ex);
                        }
                    }
                }, maxStackSize) {
                    IsBackground = true
                };

                thread.Start();

                if (!thread.Join(testcase.Options.Timeout)) {
                    if(!ClrModule.IsNetCoreApp) {
                        #pragma warning disable SYSLIB0006 // 'Thread.Abort is not supported and throws PlatformNotSupportedException.' 
                        thread.Abort();
                        #pragma warning restore SYSLIB0006
                    }
                    NUnit.Framework.TestContext.Error.WriteLine($"{testcase.Name} timed out after {testcase.Options.Timeout / 1000.0} seconds.");
                    return -1;
                }

                exceptionInfo?.Throw();

                return res;
            } finally {
                Environment.CurrentDirectory = cwd;
            }
        }
    }

#if NETFRAMEWORK
    internal static class StringExtensions {
        public static bool Contains(this string s, string value, StringComparison comparisonType) {
            return s.IndexOf(value, comparisonType) >= 0;
        }
    }
#endif
}
