using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IronPython.Hosting;
using IronPythonTest.Util;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;

namespace IronPythonTest.Cases {
    class CaseExecuter {
        private static readonly String Executable = System.Reflection.Assembly.GetEntryAssembly().Location;

        private ScriptEngine defaultEngine;

        public static ScriptEngine CreateEngine(TestOptions options) {
            var engine = Python.CreateEngine(new Dictionary<string, object> {
                {"Debug", options.Debug },
                {"Frames", options.Frames || options.FullFrames },
                {"FullFrames", options.FullFrames }
            });

            engine.SetHostVariables(
                Path.GetDirectoryName(Executable),
                Executable,
                "");

            return engine;
        }

        public CaseExecuter() {
            this.defaultEngine = Python.CreateEngine(new Dictionary<string, object> {
                {"Debug", true },
                {"Frames", true},
                {"FullFrames", true}
            });

            this.defaultEngine.SetHostVariables(
                Path.GetDirectoryName(Executable),
                Executable,
                "");
        }

        public int RunTest(TestInfo testcase) {
            switch(testcase.Options.IsolationLevel) {
                case TestIsolationLevel.DEFAULT:
                    return GetScopeTest(testcase);

                case TestIsolationLevel.ENGINE:
                    return GetEngineTest(testcase);

                default:
                    throw new ArgumentException(String.Format("IsolationLevel {0} is not supported.", testcase.Options.IsolationLevel.ToString()), "testcase.IsolationLevel");
            }
        }

        private int GetEngineTest(TestInfo testcase) {
            var engine = CreateEngine(testcase.Options);
            var source = engine.CreateScriptSourceFromString(
                testcase.Text, testcase.Path, SourceCodeKind.File);

            return GetResult(engine, source);
        }

        private int GetScopeTest(TestInfo testcase) {
            var source = this.defaultEngine.CreateScriptSourceFromString(
                testcase.Text, testcase.Path, SourceCodeKind.File);

            return GetResult(this.defaultEngine, source);
        }

        private int GetResult(ScriptEngine engine, ScriptSource source) {
            var scope = engine.CreateScope();
            return engine.Operations.ConvertTo<int>(source.Execute(scope) ?? 0);
        }
    }
}
