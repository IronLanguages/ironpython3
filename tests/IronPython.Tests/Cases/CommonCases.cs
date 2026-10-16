// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;
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

        /// <summary>
        /// Acquires a cross-process lock using a lock file. Named mutexes are avoided as they can fail
        /// on Unix since .NET 8.0.32 moved their storage (dotnet/docs#56396), and are not cross-process
        /// on Mono (IronLanguages/ironpython3#946). Lock is needed on Mono, where FileShare.None is only
        /// enforced in-process. The file is never deleted, to avoid racing with waiters.
        /// </summary>
        private static FileStream AcquireLock(string name, TimeSpan timeout) {
            var dir = Path.Combine(Path.GetTempPath(), "IronPython.Tests.locks");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, name + ".lock");
            var deadline = DateTime.UtcNow + timeout;
            var waiting = false;
            while (true) {
                FileStream fs = null;
                try {
                    fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, bufferSize: 1);
#if NETFRAMEWORK
                    fs.Lock(0, 1);
#else
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) { // not supported on macOS
                        fs.Lock(0, 1);
                    }
#endif
                    return fs;
                } catch (IOException e) {
                    fs?.Dispose();
                    if (DateTime.UtcNow >= deadline) {
                        throw new TimeoutException($"Could not acquire lock file '{path}' within {timeout}.", e);
                    }
                    if (!waiting) {
                        waiting = true;
                        TestContext.Progress.WriteLine($"{name} waiting for lock");
                    }
                    Thread.Sleep(100);
                }
            }
        }

        protected int TestImpl(TestInfo testcase) {
            var lockTimeout = TimeSpan.FromMilliseconds(testcase.Options.Timeout);
            using var lockFile = testcase.Options.NotParallelSafe ? AcquireLock(testcase.Name, lockTimeout) : null;
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
            }
        }
    }
}
