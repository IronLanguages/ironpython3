// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

using IronPython.Hosting;
using IronPython.Modules;

using Microsoft.Scripting.Hosting;

using NUnit.Framework;

namespace IronPythonTest {
    [TestFixture(Category = "IronPython")]
    public class ResourceMetaPathImporterTest {
        private ScriptEngine _engine;
        private ResourceMetaPathImporter _importer;

        [SetUp]
        public void SetUp() {
            _engine = Python.CreateEngine();
            _importer = new ResourceMetaPathImporter(typeof(ResourceMetaPathImporterTest).Assembly, "ResourceMetaPathImporterTest.zip");
            var metaPath = _engine.GetSysModule().GetVariable<IList<object>>("meta_path");
            metaPath.Add(_importer);
        }

        private string ImportValue(string module) {
            var scope = _engine.CreateScope();
            _engine.Execute($"import {module} as m\nvalue = m.value", scope);
            return scope.GetVariable<string>("value");
        }

        [Test]
        public void ImportDeflated() {
            Assert.That(ImportValue("deflated"), Is.EqualTo(string.Concat(Enumerable.Repeat("deflated ", 1000))));
        }

        [Test]
        public void ImportStored() {
            Assert.That(ImportValue("stored"), Is.EqualTo("stored"));
        }

        [Test]
        public void ImportPackage() {
            Assert.That(ImportValue("pkg"), Is.EqualTo("pkg"));
            Assert.That(ImportValue("pkg.sub"), Is.EqualTo("pkg.sub"));
        }

        [Test]
        public void FindMissingModule() {
            var scope = _engine.CreateScope();
            scope.SetVariable("importer", _importer);
            _engine.Execute("value = importer.find_module('missing')", scope);
            Assert.That(scope.GetVariable("value"), Is.Null);
        }
    }
}
