using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IronPythonTest.Util;
using NUnit.Framework;
using NUnit.Framework.Api;

namespace IronPythonTest.Cases {
   public class TestInfo {
       public TestInfo(string path, TestManifest testManifest) {
            this.Path = path;
            this.Text = LoadTest(path);
            this.Name = GetName(path);
            this.Options = testManifest[this.Name];
        }

        public string Path { get; private set; }
        public string Text { get; private set; }
        public string Name { get; private set; }
        public TestOptions Options { get; private set; }

        private static string LoadTest(string path) {
            return File.ReadAllText(path);
        }

        private static string GetName(string path) {
            return System.IO.Path.GetFileNameWithoutExtension(path);
        }

        public override string ToString() {
            return this.Name;
        }
   }

    abstract class CommonCaseGenerator<TCases> : IEnumerable {
        protected readonly TestManifest manifest = new TestManifest(typeof(TCases));
        private static readonly string category = typeof(TCases).Name;

        public IEnumerator GetEnumerator() {
            foreach (var testcase in GetTests()) {
                var result = new TestCaseData(testcase)
                    .SetCategory(category)
                    .SetName(testcase.Name)
                    .Returns(0);

                if (testcase.Options.Ignore)
                    result.Ignore();

                yield return result;
            }
        }

        protected abstract IEnumerable<TestInfo> GetTests();
    }
}
