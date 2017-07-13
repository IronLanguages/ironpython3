using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;


namespace IronPythonTest.Cases {
    [TestFixture(Category="IronPython")]
    public class IronPythonCases {
        private CaseExecuter executor;

        [OneTimeSetUp]
        public void FixtureSetUp() {
            this.executor = new CaseExecuter();
        }

        [Test, TestCaseSource(typeof(IronPythonCaseGenerator))]
        public int IronPythonTests(TestInfo testcase) {
            try {
                Console.Error.WriteLine(testcase.Name); // write to the error stream so it appears before the test is run
                return this.executor.RunTest(testcase);
            } catch (Exception e) {
                Assert.Fail(this.executor.FormatException(e));
                return -1;
            }
        }
    }

    class IronPythonCaseGenerator : CommonCaseGenerator<IronPythonCases> {
        protected override IEnumerable<TestInfo> GetTests() {
            return Directory.EnumerateFiles(Path.Combine("..", "..", "Tests"), "test_*.py", SearchOption.AllDirectories)
                .Select(file => new TestInfo(Path.GetFullPath(file), this.manifest))
                .OrderBy(testcase => testcase.Name);
        }
    }
}
