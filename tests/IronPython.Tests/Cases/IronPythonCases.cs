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
            return GetFilenames(new[] {
                System.Tuple.Create(category, "Tests"),
                System.Tuple.Create($"{category}.scripts", Path.Combine("eng", "scripts")),
            })
            .OrderBy(testcase => testcase.Name);

            IEnumerable<TestInfo> GetFilenames(IEnumerable<System.Tuple<string, string>> folders) {
                foreach (var tuple in folders) {
                    var fullPath = Path.Combine(CaseExecuter.FindRoot(), tuple.Item2);
                    foreach (var filename in Directory.EnumerateFiles(fullPath, "test_*.py", SearchOption.AllDirectories))
                        yield return new TestInfo(Path.GetFullPath(filename), tuple.Item1, tuple.Item2, manifest);
                }
            }
        }
    }
}
