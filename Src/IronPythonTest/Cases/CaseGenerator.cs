using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
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
                var name = testcase.Name;
                var framework = TestContext.Parameters["FRAMEWORK"];
                if(!string.IsNullOrWhiteSpace(framework)) {
                    name = $"{framework}.{testcase.Name}";
                }
                
                var result = new TestCaseData(testcase)
                    .SetCategory(category)
                    .SetName(name)
                    .Returns(0);

                if (testcase.Options.Ignore) {
                    if (!string.IsNullOrWhiteSpace(testcase.Options.Reason)) {
                        result.Ignore(string.Format("ignored - {0}", testcase.Options.Reason));
                    } else {
                        result.Ignore("ignored");
                    }
                }

                if(!ConditionMatched(testcase.Options.Condition)) {
                    if (!string.IsNullOrWhiteSpace(testcase.Options.Reason)) {
                        result.Ignore(string.Format("condition ({0}) - {1}", testcase.Options.Condition, testcase.Options.Reason));
                    } else {
                        result.Ignore(string.Format("condition ({0})", testcase.Options.Condition));
                    }
                }

                yield return result;
            }
        }

        protected abstract IEnumerable<TestInfo> GetTests();

        protected bool ConditionMatched(string condition) {
            bool result = true;
            if(!string.IsNullOrEmpty(condition)) {
                try {
                    result = EvaluateExpression(condition);
                } catch(Exception ex) {
                    Console.WriteLine("Error evaluating test condition '{0}', will run the test anyway: {1}", condition, ex.Message);
                    result = true;
                }
            }

            return result;
        }

        private bool EvaluateExpression(string expression) {
            var dummy = new DataTable();
            string filter = expression;
            var replacements = new Dictionary<string, string>() {
                // variables
                { "$(OS)", Environment.OSVersion.Platform.ToString() },
                { "$(IS_NETCOREAPP)", IronPython.Runtime.ClrModule.IsNetCoreApp.ToString() },
                { "$(IS_MONO)", IronPython.Runtime.ClrModule.IsMono.ToString() },
                { "$(IS_DEBUG)", IronPython.Runtime.ClrModule.IsDebug.ToString() },

                // operators
                { "==", "=" },
                { "||", "OR" },
                { "\"\"", "\"" },
                { "\"", "'" },
                { "&&", "AND" },
                { "!=", "<>" }
            };

            foreach (var replacement in replacements) {
                expression = expression.Replace(replacement.Key, replacement.Value);
            }

            try {
                object res = dummy.Compute(expression, null);
                if (res is bool) {
                    return (bool)res;
                }
            } catch (EvaluateException ex) {
                if (ex.Message.StartsWith("The expression contains undefined function call", StringComparison.Ordinal))
                    throw new Exception("A variable used in the filter expression is not defined");
                throw new Exception(string.Format("Invalid filter: {0}", ex.Message));
            } catch(SyntaxErrorException ex) {
                throw new Exception(string.Format("Invalid filter: {0}", ex.Message));
            }

            throw new Exception(string.Format("Invalid filter, does not evaluate to true or false: {0}", filter));
        }
    }
}
