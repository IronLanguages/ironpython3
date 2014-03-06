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
        public int StandardCPythonTests(TestInfo testcase) {
            var source = this.engine.CreateScriptSourceFromString(
                testcase.Text, testcase.Path, SourceCodeKind.File);

            try {
                return source.ExecuteProgram();
            } catch (SyntaxErrorException e) {
                Assert.Fail("SyntaxError ({0}, {1}): {2}", e.RawSpan.Start.Line, e.RawSpan.Start.Column, e.Message);
                return -1;
            }
        }
    }

    class StandardCPythonCases : CommonCaseGenerator<CPythonCases> {
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

        protected override IEnumerable<TestInfo> GetTests() {
            var stdlib = @"..\..\Src\StdLib\Lib\test";
            return STDTESTS.Select(test => new TestInfo(Path.GetFullPath(Path.Combine(stdlib, test) + ".py"), this.manifest));
        }
    }
}
