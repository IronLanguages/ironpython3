using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;


namespace IronPythonTest.Cases {
    [TestFixture(Category = "IronPython")]
    public class IronPythonCases {
        private CaseExecuter executor;

        [OneTimeSetUp]
        public void FixtureSetUp() {
            this.executor = new CaseExecuter();
        }

        [Test, TestCaseSource(typeof(IronPythonCaseGenerator))]
        public int IronPythonTests(TestInfo testcase) {
            try {
                TestContext.Progress.WriteLine(testcase.Name);
                return this.executor.RunTest(testcase);
            } catch (Exception e) {
                Assert.Fail(this.executor.FormatException(e));
                return -1;
            }
        }
    }

    class IronPythonCaseGenerator : CommonCaseGenerator<IronPythonCases> {
        protected override IEnumerable<TestInfo> GetTests() {
            var root = CaseExecuter.FindRoot();

            return GetFilenames()
                .Select(file => new TestInfo(Path.GetFullPath(file), category, "Tests", this.manifest))
                .OrderBy(testcase => testcase.Name);

            IEnumerable<string> GetFilenames() {
                foreach (var filename in Directory.EnumerateFiles(Path.Combine(root, "Tests"), "test_*.py", SearchOption.AllDirectories))
                    yield return filename;

                yield return Path.Combine(root, "Src", "Scripts", "test_cgcheck.py");
            }
        }
    }
}
