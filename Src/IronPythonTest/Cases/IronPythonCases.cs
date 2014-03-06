using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IronPython.Hosting;
using IronPythonTest.Util;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using NUnit.Framework;
using NUnit.Framework.Api;

namespace IronPythonTest.Cases {
    [TestFixture(Category="IronPython")]
    public class IronPythonCases {
        ScriptEngine engine;

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

        [Test, TestCaseSource(typeof(IronPythonCaseGenerator))]
        public int IronPythonTests(TestInfo testcase) {
            var source = this.engine.CreateScriptSourceFromString(
                testcase.Text, testcase.Path, SourceCodeKind.File);
            return source.ExecuteProgram();
        }
    }

    class IronPythonCaseGenerator : CommonCaseGenerator<IronPythonCases> {
        protected override IEnumerable<TestInfo> GetTests() {
            return Directory.GetFiles(@"..\..\Tests", "test_*.py")
                .Select(file => new TestInfo(Path.GetFullPath(file), this.manifest))
                .OrderBy(testcase => testcase.Name)
                .Take(2);
        }
    }
}
