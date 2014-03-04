using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace IronPythonTest.Util {
    class TestManifest {
        IniParser manifest;

        public TestManifest(Type parent) {
            var file = parent.Assembly.GetManifestResourceStream(
                string.Format("IronPythonTest.{0}Manifest.ini", parent.Name));

            this.manifest = new IniParser(file);
        }

        public TestOptions this[string testName] {
            get {
                return new TestOptions(this.manifest, testName);
            }
        }
    }

    class TestOptions {
        string testName;
        IniParser manifest;

        public TestOptions(IniParser manifest, string testName) {
            this.manifest = manifest;
            this.testName = testName;
        }

        public bool Skip {
            get {
                return this.manifest.GetBool(this.testName, "Skip");
            }
        }
    }
}
