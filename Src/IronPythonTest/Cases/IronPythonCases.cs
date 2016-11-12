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
        private CaseExecuter executor;

        [OneTimeSetUp]
        public void FixtureSetUp() {
            this.executor = new CaseExecuter();
        }

        [Test, TestCaseSource(typeof(IronPythonCaseGenerator))]
        public int IronPythonTests(TestInfo testcase) {
            return this.executor.RunTest(testcase);
        }
    }

    class IronPythonCaseGenerator : CommonCaseGenerator<IronPythonCases> {
        protected override IEnumerable<TestInfo> GetTests() {
            return Directory.GetFiles(@"..\..\Tests", "test_*.py")
                .Select(file => new TestInfo(Path.GetFullPath(file), this.manifest))
                .OrderBy(testcase => testcase.Name);
        }
    }
}
