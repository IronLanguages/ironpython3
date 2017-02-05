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
                return this.executor.RunTest(testcase);
            } catch (Exception e) {
                Assert.Fail(this.executor.FormatException(e));
                return -1;
            }
        }
    }

    class IronPythonCaseGenerator : CommonCaseGenerator<IronPythonCases> {
        protected override IEnumerable<TestInfo> GetTests() {
            return Directory.GetFiles(Path.Combine("..", "..", "Tests"), "test_*.py")
                .Select(file => new TestInfo(Path.GetFullPath(file), this.manifest))
                .OrderBy(testcase => testcase.Name);
        }
    }
}
