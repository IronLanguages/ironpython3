using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;


namespace IronPythonTest.Cases {
    [TestFixture(Category = "IronPython")]
    public class IronPythonCases : CommonCases {        
        [Test, TestCaseSource(typeof(IronPythonCaseGenerator))]
        public override int Test(TestInfo testcase) {
            return TestImpl(testcase);
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
