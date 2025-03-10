﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.IO;
using System.Runtime.InteropServices;

using IronPython;
using IronPythonTest.Util;

using NUnit.Framework;

namespace IronPythonTest.Cases {
    public class TestInfo {
        public TestInfo(string path, string baseName, string rootDir, TestManifest testManifest) {
            this.Path = path;
            this.Text = LoadTest(path);
            this.Name = GetName(path, baseName, rootDir);
            this.Options = testManifest[this.Name];
        }

        public string Path { get; }
        public string Text { get; }
        public string Name { get; }
        public TestOptions Options { get; }

        private static string LoadTest(string path) {
            return File.ReadAllText(path);
        }

        private static string GetName(string path, string baseName, string rootDir) {
            var root = CaseExecuter.FindRoot();
            var dir = System.IO.Path.GetDirectoryName(path).Replace(root, string.Empty).Replace(rootDir, string.Empty).Replace('\\', '.').Replace('/', '.').TrimStart('.');
            if (string.IsNullOrWhiteSpace(dir)) {
                return $"{baseName}.{System.IO.Path.GetFileNameWithoutExtension(path)}";
            }
            return $"{baseName}.{dir}.{System.IO.Path.GetFileNameWithoutExtension(path)}";
        }

        public override string ToString() {
            return this.Name;
        }
    }

    internal abstract class CommonCaseGenerator<TCases> : IEnumerable {
        protected readonly TestManifest manifest = new TestManifest(typeof(TCases));
        protected static readonly string category = ((TestFixtureAttribute)typeof(TCases).GetCustomAttributes(typeof(TestFixtureAttribute), false)[0]).Category;

        public IEnumerator GetEnumerator() {
            foreach (var testcase in GetTests()) {
                var name = testcase.Name;
                var framework = TestContext.Parameters["FRAMEWORK"];
                if (!string.IsNullOrWhiteSpace(framework)) {
                    name = $"{framework}.{testcase.Name}";
                }

                var result = new TestCaseData(testcase)
                    .SetCategory(category)
                    .SetName(name)
                    .Returns(0);
                result.Properties.Add("_CodeFilePath", testcase.Path);

                var runIgnored = !string.IsNullOrWhiteSpace(TestContext.Parameters["RUN_IGNORED"]);

                if (testcase.Options.Ignore && !runIgnored) {
                    if (!string.IsNullOrWhiteSpace(testcase.Options.Reason)) {
                        result.Ignore($"ignored - {testcase.Options.Reason}");
                    } else {
                        result.Ignore("ignored");
                    }
                }

                if (!ConditionMatched(testcase.Options.RunCondition)) {
                    if (!string.IsNullOrWhiteSpace(testcase.Options.Reason)) {
                        result.Ignore($"condition ({testcase.Options.RunCondition}) - {testcase.Options.Reason}");
                    } else {
                        result.Ignore($"condition ({testcase.Options.RunCondition})");
                    }
                }

                yield return result;
            }
        }

        protected abstract IEnumerable<TestInfo> GetTests();

        protected bool ConditionMatched(string condition) {
            bool result = true;
            if (!string.IsNullOrEmpty(condition)) {
                try {
                    result = EvaluateExpression(condition);
                } catch (Exception ex) {
                    Console.WriteLine($"Error evaluating test condition '{condition}', will run the test anyway: {ex.Message}");
                    result = true;
                }
            }

            return result;
        }

        private bool EvaluateExpression(string expression) {
            var dummy = new DataTable();
            string filter = expression;
            var replacements = new OrderedDictionary() {
                // variables
                { "$(FRAMEWORK)", IronPython.Runtime.ClrModule.TargetFramework },
                { "$(IS_NETCOREAPP)", IronPython.Runtime.ClrModule.IsNetCoreApp.ToString() },
                { "$(IS_NETSTANDARD)", IronPython.Runtime.ClrModule.TargetFramework.StartsWith(".NETStandard", StringComparison.Ordinal).ToString() },
                { "$(IS_MONO)", IronPython.Runtime.ClrModule.IsMono.ToString() },
                { "$(IS_DEBUG)", IronPython.Runtime.ClrModule.IsDebug.ToString() },
                { "$(IS_POSIX)", (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux)).ToString() },
                { "$(IS_OSX)", RuntimeInformation.IsOSPlatform(OSPlatform.OSX).ToString() },
                { "$(IS_LINUX)", RuntimeInformation.IsOSPlatform(OSPlatform.Linux).ToString() },
                { "$(IS_WINDOWS)", RuntimeInformation.IsOSPlatform(OSPlatform.Windows).ToString() },

                // operators
                { "==", "=" },
                { "||", "OR" },
                { "\"", "'" },    // replace double quotes before double-double quotes
                { "\"\"", "\"" },
                { "&&", "AND" },
                { "!=", "<>" },
            };

            foreach (DictionaryEntry replacement in replacements) {
                expression = expression.Replace((string)replacement.Key, replacement.Value?.ToString());
            }

            try {
                object res = dummy.Compute(expression, null);
                if (res is bool result) {
                    return result;
                }
            } catch (EvaluateException ex) {
                if (ex.Message.StartsWith("The expression contains undefined function call", StringComparison.Ordinal))
                    throw new Exception("A variable used in the filter expression is not defined");
                throw new Exception($"Invalid filter: {ex.Message}");
            } catch (SyntaxErrorException ex) {
                throw new Exception($"Invalid filter: {ex.Message}");
            }

            throw new Exception($"Invalid filter, does not evaluate to true or false: {filter}");
        }
    }
}
