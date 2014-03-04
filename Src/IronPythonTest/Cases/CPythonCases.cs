using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IronPython.Hosting;
using IronPythonTest.Util;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using NUnit.Framework;
using NUnit.Framework.Api;

namespace IronPythonTest.Cases {
    [TestFixture(Category = "CPython")]
    class CPythonCases {
        private ScriptEngine engine;

        [TestFixtureSetUp]
        public void FixtureSetUp() {
            this.engine = Python.CreateEngine(new Dictionary<string, object> {
                {"Debug", true },
                {"Frames", true},
                {"FullFrames", true}
            });

            var executable = System.Reflection.Assembly.GetEntryAssembly().Location;
            this.engine.SetHostVariables(
                Path.GetDirectoryName(executable),
                executable,
                "");
        }

        [Test, TestCaseSource(typeof(StandardCPythonCases))]
        public int StandardCPythonTests(IronPythonCase testcase) {
            var source = this.engine.CreateScriptSourceFromString(
                testcase.Text, testcase.Path, SourceCodeKind.File);
            return source.ExecuteProgram();
        }
    }

    class StandardCPythonCases : IEnumerable {
        private static readonly string[] STDTESTS = {
            "test_grammar",
            "test_opcodes",
            "test_dict",
            "test_builtin",
            "test_exceptions",
            "test_types",
            "test_unittest",
            "test_doctest",
            "test_doctest2",
            "test_support"
        };

        public IEnumerator GetEnumerator() {
            foreach (var testcase in GetTests()) {
                var result = new TestCaseData(testcase)
                    .SetName(testcase.Name)
                    .Returns(0);

                yield return result;
            }
        }

        private IEnumerable<IronPythonCase> GetTests() {
            var stdlib = @"..\..\External.LCA_RESTRICTED\Languages\IronPython\27\Lib\test";
            return STDTESTS.Select(test => new IronPythonCase(Path.GetFullPath(Path.Combine(stdlib, test) + ".py")));
        }
    }
}
