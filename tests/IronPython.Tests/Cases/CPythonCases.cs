// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace IronPythonTest.Cases {
    [TestFixture(Category = "CPython")]
    public class CPythonCases : CommonCases {
        [Test, TestCaseSource(typeof(CPythonCaseGenerator))]
        public override int Test(TestInfo testcase) {
            return TestImpl(testcase);
        }
    }

    internal class CPythonCaseGenerator : CommonCaseGenerator<CPythonCases> {
        protected override IEnumerable<TestInfo> GetTests() {
            var libFolder = Path.Combine("src", "core", "IronPython.StdLib", "lib");
            var suiteFolder = Path.Combine("tests", "suite", "stdlib");

            return GetTestInfo(category, "test")
                .Concat(GetTestInfo($"{category}.ctypes", Path.Combine("ctypes", "test")))
                .Concat(GetTestInfo($"{category}.distutils", Path.Combine("distutils", "tests")))
                .Concat(GetTestInfo($"{category}.unittest", Path.Combine("unittest", "test")))
                .OrderBy(testcase => testcase.Name);

            IEnumerable<TestInfo> GetTestInfo(string category, string folder) {
                var root = CaseExecuter.FindRoot();
                var altFolder = Path.GetDirectoryName(folder); // drop the trailing test (or tests) folder
                var fullPath = Path.GetFullPath(Path.Combine(root, libFolder, folder));
                var altFullPath = Path.GetFullPath(Path.Combine(root, suiteFolder, altFolder));
                foreach (var filename in Directory.EnumerateFiles(fullPath, "test_*.py", SearchOption.AllDirectories)) {
                    var altFilename = Path.GetFullPath(Path.Combine(altFullPath, Path.GetRelativePath(fullPath, filename)));
                    if (File.Exists(altFilename)) {
                        yield return new TestInfo(altFilename, category, Path.Combine(suiteFolder, altFolder), manifest);
                    } else {
                        yield return new TestInfo(filename, category, Path.Combine(libFolder, folder), manifest);
                    }
                }
            }
        }
    }
}
