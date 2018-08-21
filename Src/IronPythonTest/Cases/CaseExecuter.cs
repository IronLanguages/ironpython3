using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using IronPython.Hosting;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPythonTest.Util;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using IronPython.Runtime.Operations;

namespace IronPythonTest.Cases {
    class CaseExecuter {
        private static string Executable {
            get {
#if NETCOREAPP2_0 || NETCOREAPP2_1
                if (Environment.OSVersion.Platform == PlatformID.Unix) {
                    return Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "ipy.sh");
                }
                return Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "ipy.bat");
#else
                return Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "ipy.exe");
#endif
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
            // we start at the current directory and look up until we find the "Src" directory
            var current = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var found = false;
            while (!found && !string.IsNullOrEmpty(current)) {
                var test = Path.Combine(current, "Src", "StdLib", "Lib");
                if (Directory.Exists(test)) {
                    return current;
                }

                current = Path.GetDirectoryName(current);
            }
            return string.Empty;
        }

        private static void AddSearchPaths(ScriptEngine engine) {
            var paths = new List<string>(engine.GetSearchPaths());
            if (!paths.Any(x => x.ToLower().Contains("stdlib"))) {
                var root = FindRoot();
                if (!string.IsNullOrEmpty(root)) {
                    paths.Insert(0, Path.Combine(root, "Src", "StdLib", "Lib"));
                }
            }
            engine.SetSearchPaths(paths);
        }

        public CaseExecuter() {
            this.defaultEngine = Python.CreateEngine(new Dictionary<string, object> {
                {"Debug", false},
                {"Frames", true},
                {"FullFrames", false},
                {"RecursionLimit", 100}
            });

            this.defaultEngine.SetHostVariables(
                Path.GetDirectoryName(Executable),
                Executable,
                "");
            AddSearchPaths(this.defaultEngine);
        }

        public int RunTest(TestInfo testcase) {
            int retryCount = testcase.Options.RetryCount;
            if (retryCount > 0) {
                int res = -1;
                for (int i = 0; i < retryCount; i++) {
                    try {
                        res = RunTestImpl(testcase);
                    } catch (Exception ex) {
                        res = -1;
                        if (i == (retryCount - 1)) {
                            throw ex;
                        }
                    }

                    if (res != 0) {
                        NUnit.Framework.TestContext.Progress.WriteLine($"Test {testcase.Name} failed, retrying again. Retry #{i + 1}");
                    } else {
                        break;
                    }
                }
                return res;
            }

            return RunTestImpl(testcase);
        }

        private int RunTestImpl(TestInfo testcase) {
            switch (testcase.Options.IsolationLevel) {
                case TestIsolationLevel.DEFAULT:
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
            var path = Path.Combine(FindRoot(), "Src", "StdLib", "Lib");
            if (Directory.Exists(path)) {
                return path;
            }
            return string.Empty;
        }

        private string ReplaceVariables(string input, IDictionary<string, string> replacements) {
            Regex variableRegex = new Regex(@"\$\(([^}]+)\)", RegexOptions.Compiled);

            var result = input;
            var match = variableRegex.Match(input);
            while (match.Success) {
                var variable = match.Groups[1].Value;
                if (replacements.ContainsKey(variable)) {
                    result = result.Replace(match.Groups[0].Value, replacements[variable]);
                }
                match = match.NextMatch();
            }

            return result;
        }

        private int GetEngineTest(TestInfo testcase) {
            var engine = CreateEngine(testcase.Options);
            var source = engine.CreateScriptSourceFromString(
                testcase.Text, testcase.Path, SourceCodeKind.File);

            return GetResult(engine, source, testcase.Path, testcase.Options.WorkingDirectory);
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

            using (Process proc = new Process()) {
                proc.StartInfo.FileName = Executable;
                proc.StartInfo.Arguments = ReplaceVariables(testcase.Options.Arguments, argReplacements);

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
                    AsyncStreamReader(proc.StandardOutput, data => Console.Write(data));
                    AsyncStreamReader(proc.StandardError, data => Console.Error.Write(data));
                }

                if (!proc.WaitForExit(testcase.Options.Timeout)) {
                    proc.Kill();
                    Console.Error.Write($"Timed out after {testcase.Options.Timeout / 1000.0} seconds.");
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
            var source = this.defaultEngine.CreateScriptSourceFromString(
                testcase.Text, testcase.Path, SourceCodeKind.File);

            return GetResult(this.defaultEngine, source, testcase.Path, testcase.Options.WorkingDirectory);
        }

        private int GetResult(ScriptEngine engine, ScriptSource source, string testPath, string workingDir) {
            int res = 0;
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
                engine.GetSysModule().SetVariable("argv", List.FromArrayNoCopy(new object[] { source.Path }));
                var compiledCode = source.Compile(new IronPython.Compiler.PythonCompilerOptions() { ModuleName = "__main__" });

                try {
                    res = engine.Operations.ConvertTo<int>(compiledCode.Execute(scope) ?? 0);
                } catch (SystemExitException ex) {
                    object otherCode;
                    res = ex.GetExitCode(out otherCode);
                }
            } finally {
                Environment.CurrentDirectory = cwd;
            }
            return res;
        }
    }
}
