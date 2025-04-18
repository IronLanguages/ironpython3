﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace IronPythonTest.Util {
    public class TestManifest {
        private IniParser manifest;

        public TestManifest(Type parent) {
            var file = parent.Assembly.GetManifestResourceStream($"IronPython.Tests.Cases.{parent.Name}Manifest.ini");
            this.manifest = new IniParser(file);
        }

        public TestOptions this[string testName] {
            get {
                return new TestOptions(this.manifest, testName);
            }
        }
    }

    public enum TestIsolationLevel {
        DEFAULT = 1,
        SCOPE = 1,
        ENGINE,
        RUNTIME,
        APPDOMAIN,
        PROCESS
    }

    public class TestOptions {
        private string testName;
        private IniParser manifest;

        public TestOptions(IniParser manifest, string testName) {
            this.manifest = manifest;
            this.testName = testName;
        }

        public bool Ignore {
            get {
                return this.manifest.GetBool(this.testName, "Ignore", false);
            }
        }

        public string Reason {
            get {
                return this.manifest.GetValue(this.testName, "Reason", string.Empty);
            }
        }

        public TestIsolationLevel IsolationLevel {
            get {
                return this.manifest.GetEnum<TestIsolationLevel>(this.testName, "IsolationLevel", TestIsolationLevel.DEFAULT);
            }
        }

        public bool Debug {
            get {
                return this.manifest.GetBool(this.testName, "Debug", false);
            }
        }

        public bool Frames {
            get {
                return this.manifest.GetBool(this.testName, "Frames", true);
            }
        }

        public bool FullFrames {
            get {
                return this.manifest.GetBool(this.testName, "FullFrames", false);
            }
        }

        public string RunCondition {
            get {
                return this.manifest.GetValue(this.testName, "RunCondition", string.Empty);
            }
        }

        public int MaxRecursion {
            get {
                return this.manifest.GetInt(this.testName, "MaxRecursion", Int32.MaxValue);
            }
        }

        public bool NotParallelSafe {
            get {
                return this.manifest.GetBool(this.testName, "NotParallelSafe", false);
            }
        }

        public bool Tracing {
            get {
                return this.manifest.GetBool(this.testName, "Tracing", false);
            }
        }

        public string Arguments {
            get {
                return this.manifest.GetValue(this.testName, "Arguments", null);
            }
        }

        public string WorkingDirectory {
            get {
                return this.manifest.GetValue(this.testName, "WorkingDirectory", string.Empty);
            }
        }

        public bool Redirect {
            get {
                return this.manifest.GetBool(this.testName, "Redirect", true);
            }
        }

        public int Timeout {
            get {
                return this.manifest.GetInt(this.testName, "Timeout", System.Threading.Timeout.Infinite);
            }
        }

        public int RetryCount {
            get {
                return this.manifest.GetInt(this.testName, "RetryCount", 0);
            }
        }
    }
}
