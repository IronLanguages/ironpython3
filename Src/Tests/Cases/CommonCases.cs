// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
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
            }
        }
    }
}
