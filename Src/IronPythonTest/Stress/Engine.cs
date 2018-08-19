// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Hosting;
using IronPython.Hosting;

using NUnit.Framework;

namespace IronPythonTest.Stress {

    [TestFixture(Category="IronPython")]
    public class Engine
#if FEATURE_REMOTING
        : MarshalByRefObject
#endif
    {
        private readonly ScriptEngine _pe;
        private readonly ScriptRuntime _env;

        public Engine() {
            // Load a script with all the utility functions that are required
            // pe.ExecuteFile(InputTestDirectory + "\\EngineTests.py");
            _env = Python.CreateRuntime();
            _pe = _env.GetEngine("py");
        }

        static long GetTotalMemory() {
            // Critical objects can take upto 3 GCs to be collected
            System.Threading.Thread.Sleep(1000);
            for (int i = 0; i < 3; i++) {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            return GC.GetTotalMemory(true);
        }

#if FEATURE_REFEMIT
        [Test]
        public void ScenarioXGC() {
            long initialMemory = GetTotalMemory();

            // Create multiple scopes:
            for (int i = 0; i < 10000; i++) {
                ScriptScope scope = _pe.CreateScope();
                scope.SetVariable("x", "Hello");
                _pe.CreateScriptSourceFromFile(System.IO.Path.Combine(Common.InputTestDirectory, "simpleCommand.py")).Execute(scope);
                Assert.AreEqual(_pe.CreateScriptSourceFromString("x").Execute<int>(scope), 1);
                scope = null;
            }

            long finalMemory = GetTotalMemory();
            long memoryUsed = finalMemory - initialMemory;
            const long memoryThreshold = 100000;

            bool emitsUncollectibleCode = Snippets.Shared.SaveSnippets || _env.Setup.DebugMode;
            if (!emitsUncollectibleCode)
            {
                System.Console.WriteLine("ScenarioGC used {0} bytes of memory.", memoryUsed);
                if (memoryUsed > memoryThreshold)
                    throw new Exception(String.Format("ScenarioGC used {0} bytes of memory. The threshold is {1} bytes", memoryUsed, memoryThreshold));
            }
            else {
                System.Console.WriteLine("Skipping memory usage test under SaveSnippets and/or Debug mode.");
            }
        }
#endif
    }
}
