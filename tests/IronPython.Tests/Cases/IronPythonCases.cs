// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

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

    internal class IronPythonCaseGenerator : CommonCaseGenerator<IronPythonCases> {
        protected override IEnumerable<TestInfo> GetTests() {
            return GetTestSuite().Concat(GetEngScripts()).OrderBy(testcase => testcase.Name);

            IEnumerable<TestInfo> GetTestSuite() {
                var root = CaseExecuter.FindRoot();
                var folder = Path.Combine("tests", "suite");
                var fullPath = Path.GetFullPath(Path.Combine(root, folder));
                var stdlibFullPath = Path.Combine(fullPath, "stdlib");
                foreach (var filename in Directory.EnumerateFiles(fullPath, "test_*.py", SearchOption.AllDirectories)) {
                    if (filename.StartsWith(stdlibFullPath, System.StringComparison.OrdinalIgnoreCase)) continue;
                    yield return new TestInfo(filename, category, folder, manifest);
                }
            }

            IEnumerable<TestInfo> GetEngScripts() {
                var root = CaseExecuter.FindRoot();
                var folder = Path.Combine("eng", "scripts");
                var filename = Path.GetFullPath(Path.Combine(root, folder, "test_cgcheck.py"));
                if (File.Exists(filename)) {
                    yield return new TestInfo(filename, $"{category}.scripts", folder, manifest);
                }
            }
        }
    }
}
