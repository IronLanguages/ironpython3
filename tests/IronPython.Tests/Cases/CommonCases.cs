// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;

using NUnit.Framework;

namespace IronPythonTest.Cases {
    public abstract class CommonCases {
        private CaseExecuter executor;

        [OneTimeSetUp]
        public void FixtureSetUp() {
            this.executor = new CaseExecuter();
        }

        public abstract int Test(TestInfo testcase);

        protected int TestImpl(TestInfo testcase) {
            using var m = testcase.Options.NotParallelSafe ? new Mutex(false, testcase.Name) : null;
            m?.WaitOne();
            try {
                TestContext.Progress.WriteLine(testcase.Name);
                return executor.RunTest(testcase);
            } catch (Exception e) {
                if (e is AggregateException ae) {
                    ae.Handle((x) => {
                        Assert.Fail(executor.FormatException(x));
                        return true;
                    });
                } else {
                    Assert.Fail(this.executor.FormatException(e));
                }
                return -1;
            } finally {
                m?.ReleaseMutex();
                CleanupTempFiles(testcase);
            }
        }

        /// <summary>
        /// Removes @test_*_tmp files/directories left behind by test.support.TESTFN.
        /// </summary>
        private static void CleanupTempFiles(TestInfo testcase) {
            var testDir = Path.GetDirectoryName(testcase.Path);
            if (testDir is null) return;

            // Clean test directory and also the StdLib test directory
            CleanupTempFilesInDir(testDir);
            var stdlibTestDir = Path.Combine(CaseExecuter.FindRoot(), "src", "core", "IronPython.StdLib", "lib", "test");
            if (stdlibTestDir != testDir) {
                CleanupTempFilesInDir(stdlibTestDir);
            }
        }

        private static void CleanupTempFilesInDir(string dir) {
            if (!Directory.Exists(dir)) return;

            try {
                foreach (var entry in Directory.EnumerateFileSystemEntries(dir, "@test_*_tmp*")) {
                    try {
                        if (File.GetAttributes(entry).HasFlag(FileAttributes.Directory)) {
                            Directory.Delete(entry, recursive: true);
                        } else {
                            File.Delete(entry);
                        }
                    } catch {
                        // ignore locked/in-use files
                    }
                }
            } catch {
                // ignore enumeration errors
            }
        }
    }
}
