using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace IronPythonTest.Util {
    public class TestManifest {
        IniParser manifest;

        public TestManifest(Type parent) {
            var file = parent.Assembly.GetManifestResourceStream(
                string.Format("IronPythonTest.Cases.{0}Manifest.ini", parent.Name));

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
        string testName;
        IniParser manifest;

        public TestOptions(IniParser manifest, string testName) {
            this.manifest = manifest;
            this.testName = testName;
        }

        public bool Ignore {
            get {
                return this.manifest.GetBool(this.testName, "Ignore", false);
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
                return this.manifest.GetBool(this.testName, "Frames", false);
            }
        }

        public bool FullFrames {
            get {
                return this.manifest.GetBool(this.testName, "FullFrames", false);
            }
        }
    }

}
