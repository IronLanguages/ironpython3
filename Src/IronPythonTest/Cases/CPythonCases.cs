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
            executor = new CaseExecuter();
        }

        [Test, TestCaseSource(typeof(StandardCPythonCaseGenerator))]
        public int StandardCPythonTests(TestInfo testcase) {
            try {
                TestContext.Progress.WriteLine(testcase.Name); // should be printed immediately
                return executor.RunTest(testcase);
            } catch (Exception e) {
                if(e is AggregateException ae) {
                    ae.Handle((x) => {
                       Assert.Fail(executor.FormatException(x));
                       return true; 
                    });
                } else {
                    Assert.Fail(executor.FormatException(e));
                }
                return -1;
            }
        }
    }

    [TestFixture(Category = "AllCPython")]
    class AllCPythonCases {
        private CaseExecuter executor;

        [OneTimeSetUp]
        public void FixtureSetUp() {
            executor = new CaseExecuter();
        }

        [Test, TestCaseSource(typeof(AllCPythonCaseGenerator))]
        public int AllCPythonTests(TestInfo testcase) {
            try {
                TestContext.Progress.WriteLine(testcase.Name); // should be printed immediately
                return executor.RunTest(testcase);
            } catch (Exception e) {
                if(e is AggregateException ae) {
                    ae.Handle((x) => {
                       Assert.Fail(executor.FormatException(x));
                       return true; 
                    });
                } else {
                    Assert.Fail(executor.FormatException(e));
                }
                return -1;
            }
        }
    }

    [TestFixture(Category = "CTypesCPython")]
    class CTypesCPythonCases {
        private CaseExecuter executor;

        [OneTimeSetUp]
        public void FixtureSetUp() {
            executor = new CaseExecuter();
        }

        [Test, TestCaseSource(typeof(CTypesCPythonCaseGenerator))]
        public int CTypesCPythonTests(TestInfo testcase) {
            try {
                TestContext.Progress.WriteLine(testcase.Name); // should be printed immediately
                return executor.RunTest(testcase);
            } catch (Exception e) {
                if(e is AggregateException ae) {
                    ae.Handle((x) => {
                       Assert.Fail(executor.FormatException(x));
                       return true; 
                    });
                } else {
                    Assert.Fail(executor.FormatException(e));
                }
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
            var testDir = Path.Combine("Src", "StdLib", "Lib", "test");
            var fullPath = Path.Combine(CaseExecuter.FindRoot(), testDir);
            return STDTESTS.Select(test => new TestInfo(Path.GetFullPath(Path.Combine(fullPath, test) + ".py"), category, testDir, this.manifest));
        }
    }

    class AllCPythonCaseGenerator : CommonCaseGenerator<AllCPythonCases> {
        protected override IEnumerable<TestInfo> GetTests() {
            var testDir = Path.Combine("Src", "StdLib", "Lib", "test");
            var fullPath = Path.Combine(CaseExecuter.FindRoot(), testDir);
            return Directory.EnumerateFiles(fullPath, "test_*.py", SearchOption.AllDirectories)
                .Where(file => !StandardCPythonCaseGenerator.STDTESTS.Contains(Path.GetFileNameWithoutExtension(file)))
                .Select(file => new TestInfo(Path.GetFullPath(file), category, testDir, this.manifest))
                .OrderBy(testcase => testcase.Name);
        }
    }

    class CTypesCPythonCaseGenerator : CommonCaseGenerator<CTypesCPythonCases> {
        protected override IEnumerable<TestInfo> GetTests() {
            var testDir = Path.Combine("Src", "StdLib", "Lib", "ctypes", "test");
            var fullPath = Path.Combine(CaseExecuter.FindRoot(), testDir);
            return Directory.EnumerateFiles(fullPath, "test_*.py", SearchOption.AllDirectories)
                .Select(file => new TestInfo(Path.GetFullPath(file), category, testDir, this.manifest))
                .OrderBy(testcase => testcase.Name);
        }
    }
}
