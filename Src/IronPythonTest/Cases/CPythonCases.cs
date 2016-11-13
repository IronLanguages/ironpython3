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
            } catch (SyntaxErrorException e) {
                Assert.Fail("SyntaxError: {3}({0}, {1}): {2}", e.Line, e.Column, e.Message, e.SourcePath);
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
                return this.executor.RunTest(testcase);
            } catch (SyntaxErrorException e) {
                Assert.Fail("SyntaxError: {3}({0}, {1}): {2}", e.Line, e.Column, e.Message, e.SourcePath);
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
            string[] path = {"..", "..", "Src", "StdLib", "Lib", "test"};
            string stdlib = Path.Combine(path);
            return STDTESTS.Select(test => new TestInfo(Path.GetFullPath(Path.Combine(stdlib, test) + ".py"), this.manifest));
        }
    }

    class AllCPythonCaseGenerator : CommonCaseGenerator<AllCPythonCases> {
        protected override IEnumerable<TestInfo> GetTests() {
            string[] path = {"..", "..", "Src", "StdLib", "Lib", "test"};
            string stdlib = Path.Combine(path);
            return Directory.GetFiles(stdlib, "test_*.py", SearchOption.AllDirectories)
                .Where(file => !StandardCPythonCaseGenerator.STDTESTS.Contains(Path.GetFileNameWithoutExtension(file)))
                .Select(file => new TestInfo(Path.GetFullPath(file), this.manifest))
                .OrderBy(testcase => testcase.Name);
        }
    }
}
