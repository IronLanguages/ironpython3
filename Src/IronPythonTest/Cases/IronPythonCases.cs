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
        public int IronPythonTests(IronPythonCase testcase) {
            var source = this.engine.CreateScriptSourceFromString(
                testcase.Text, testcase.Path, SourceCodeKind.File);
            return source.ExecuteProgram();
        }

    }

    public class IronPythonCase {
        public IronPythonCase(string path) {
            this.Path = path;
            this.Text = LoadTest(path);
            this.Name = GetName(path);
        }

        public string Path { get; private set; }
        public string Text { get; private set; }
        public string Name { get; private set; }

        private static string LoadTest(string path) {
            return File.ReadAllText(path);
        }

        private static string GetName(string path) {
            return System.IO.Path.GetFileNameWithoutExtension(path);
        }

        public override string ToString() {
            return this.Name;
        }
    }

    class IronPythonCaseGenerator : IEnumerable {
        TestManifest manifest = new TestManifest(typeof(IronPythonCases));

        public IEnumerator GetEnumerator() {
            foreach (var testcase in GetTests()) {
                var result = new TestCaseData(testcase)
                    .SetName(testcase.Name)
                    .Returns(0);

                if (this.manifest[testcase.Name].Skip)
                    result.RunState = RunState.Skipped;

                yield return result;
            }
        }

        private IEnumerable<IronPythonCase> GetTests() {
            return Directory.GetFiles(@"..\..\Languages\IronPython\Tests", "test_*.py")
                .Select(file => new IronPythonCase(Path.GetFullPath(file)))
                .OrderBy(testcase => testcase.Name);
        }

    }

}
