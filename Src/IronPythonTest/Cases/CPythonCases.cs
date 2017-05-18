using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IronPython.Hosting;
using IronPythonTest.Util;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using NUnit.Framework;
using NUnit.Framework.Api;

namespace IronPythonTest.Cases {
    [TestFixture(Category = "StandardCPython")]
    class StandardCPythonCases {
        private CaseExecuter executor;

        [OneTimeSetUp]
        public void FixtureSetUp() {
            this.executor = new CaseExecuter();
        }

        [Test, TestCaseSource(typeof(StandardCPythonCaseGenerator))]
        public int StandardCPythonTests(TestInfo testcase) {
            try {
                return this.executor.RunTest(testcase);
            } catch (Exception e) {
                Assert.Fail(this.executor.FormatException(e));
                return -1;
            }
        }
    }

    [TestFixture(Category = "AllCPython")]
    class AllCPythonCases {
        private CaseExecuter executor;

        [OneTimeSetUp]
        public void FixtureSetUp() {
            this.executor = new CaseExecuter();
        }

        [Test, TestCaseSource(typeof(AllCPythonCaseGenerator))]
        public int AllCPythonTests(TestInfo testcase) {
            try {
                Console.Error.WriteLine(testcase.Name); // write to the error stream so it appears before the test is run
                return this.executor.RunTest(testcase);
            } catch (Exception e) {
                Assert.Fail(this.executor.FormatException(e));
                return -1;
            }
        }
    }

    class StandardCPythonCaseGenerator : CommonCaseGenerator<StandardCPythonCases> {
        internal static readonly HashSet<string> STDTESTS = new HashSet<String> {
            "test_grammar",
            "test_opcodes",
            "test_dict",
            "test_builtin",
            "test_exceptions",
            "test_types",
            "test_unittest",
            "test_doctest",
            "test_doctest2",
            "test_support"
        };

        protected override IEnumerable<TestInfo> GetTests() {
            string stdlib = Path.Combine("..", "..", "Src", "StdLib", "Lib", "test");
            return STDTESTS.Select(test => new TestInfo(Path.GetFullPath(Path.Combine(stdlib, test) + ".py"), this.manifest));
        }
    }

    class AllCPythonCaseGenerator : CommonCaseGenerator<AllCPythonCases> {
        protected override IEnumerable<TestInfo> GetTests() {
            string stdlib = Path.Combine("..", "..", "Src", "StdLib", "Lib", "test");
            return Directory.GetFiles(stdlib, "test_*.py", SearchOption.AllDirectories)
                .Where(file => !StandardCPythonCaseGenerator.STDTESTS.Contains(Path.GetFileNameWithoutExtension(file)))
                .Select(file => new TestInfo(Path.GetFullPath(file), this.manifest))
                .OrderBy(testcase => testcase.Name);
        }
    }
}
