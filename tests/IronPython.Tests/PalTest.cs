// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using IronPython.Hosting;

using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;

using NUnit.Framework;

using IronPythonTest.Cases;

namespace IronPythonTest {
    [TestFixture(Category = "IronPython")]
    public class PalTest {
        /// <summary>
        /// Serves the files under Root from memory, everything else from the file system.
        /// </summary>
        public sealed class MemoryPal : PlatformAdaptationLayer {
            // path which doesn't exist on disk so importlib's PathFinder can't load it
            public readonly string Root = Path.Combine(Path.GetTempPath(), "PalTest-" + Guid.NewGuid().ToString("N"));
            public readonly Dictionary<string, string> Files = new();

            private bool IsInRoot(string path) => path.StartsWith(Root, StringComparison.Ordinal);

            public override bool FileExists(string path)
                => IsInRoot(path) ? Files.ContainsKey(path) : base.FileExists(path);

            public override bool DirectoryExists(string path)
                => IsInRoot(path) ? Files.Keys.Any(f => f.StartsWith(path + Path.DirectorySeparatorChar, StringComparison.Ordinal)) : base.DirectoryExists(path);

            public override Stream OpenInputFileStream(string path, FileMode mode = FileMode.Open, FileAccess access = FileAccess.Read, FileShare share = FileShare.Read, int bufferSize = 8192)
                => IsInRoot(path) ? new MemoryStream(Encoding.UTF8.GetBytes(Files[path])) : base.OpenInputFileStream(path, mode, access, share, bufferSize);

            public override string[] GetFileSystemEntries(string path, string searchPattern, bool includeFiles, bool includeDirectories) {
                if (!IsInRoot(path)) return base.GetFileSystemEntries(path, searchPattern, includeFiles, includeDirectories);

                // the importer only looks up exact names
                string candidate = Path.Combine(path, searchPattern);
                if (includeFiles && FileExists(candidate) || includeDirectories && DirectoryExists(candidate)) {
                    return new[] { candidate };
                }
                return Array.Empty<string>();
            }

            public void AddFile(string relativePath, string contents) => Files[Path.Combine(Root, relativePath)] = contents;
        }

        public sealed class MemoryPalHost : ScriptHost {
            private readonly PlatformAdaptationLayer _pal;

            public MemoryPalHost(PlatformAdaptationLayer pal) {
                _pal = pal;
            }

            public override PlatformAdaptationLayer PlatformAdaptationLayer => _pal;
        }

        private static ScriptEngine CreateEngine(MemoryPal pal) {
            var setup = Python.CreateRuntimeSetup(null);
            setup.HostType = typeof(MemoryPalHost);
            setup.HostArguments = [pal];
            var engine = Python.GetEngine(new ScriptRuntime(setup));

            // for importlib.reload
            var paths = new List<string>(engine.GetSearchPaths()) {
                Path.Combine(CaseExecuter.FindRoot(), "src", "core", "IronPython.StdLib", "lib"),
                pal.Root
            };
            engine.SetSearchPaths(paths);
            return engine;
        }

        [Test]
        public void TestImport() {
            var pal = new MemoryPal();
            pal.AddFile("palmod.py", "value = 1");
            pal.AddFile(Path.Combine("palpkg", "__init__.py"), "value = 2");
            pal.AddFile(Path.Combine("palpkg", "sub.py"), "from . import value as parent_value\nvalue = 3");
            // packages take precedence over modules, and modules over directories without __init__.py
            pal.AddFile("palpkg.py", "value = 0");
            pal.AddFile(Path.Combine("palmod", "data.txt"), "");
            var engine = CreateEngine(pal);
            var scope = engine.CreateScope();
            scope.SetVariable("root", pal.Root);

            engine.Execute(@"
import os
import palmod
import palpkg.sub

def check(actual, expected):
    assert actual == expected, (actual, expected)

check(palmod.value, 1)
check(palmod.__file__, os.path.join(root, 'palmod.py'))
check(palpkg.value, 2)
check(palpkg.__file__, os.path.join(root, 'palpkg', '__init__.py'))
check(palpkg.__path__, [os.path.join(root, 'palpkg')])
check(palpkg.sub.value, 3)
check(palpkg.sub.parent_value, 2)

for name in ('PALMOD', 'PalPkg', 'palpkg.SUB'):
    try:
        __import__(name)
    except ImportError:
        pass
    else:
        raise AssertionError(name)
", scope);
        }

        [Test]
        public void TestReload() {
            var pal = new MemoryPal();
            pal.AddFile("palreload.py", "value = 1");
            var engine = CreateEngine(pal);
            var scope = engine.CreateScope();

            engine.Execute("import importlib, palreload; before = palreload", scope);
            pal.AddFile("palreload.py", "value = 2");
            engine.Execute("after = importlib.reload(palreload)", scope);

            // the new source runs in the same module
            Assert.That(engine.Execute<bool>("after is before and after.value == 2", scope), Is.True);
        }

        [Test]
        public void TestGetSource() {
            var pal = new MemoryPal();
            pal.AddFile("palsource.py", "def f():\n    raise ValueError('palsource')\n");
            pal.AddFile(Path.Combine("palsourcepkg", "__init__.py"), "value = 1\n");
            var engine = CreateEngine(pal);

            engine.Execute(@"
import traceback
import palsource, palsourcepkg

def check(actual, expected):
    assert actual == expected, (actual, expected)

check(palsource.__loader__.get_source('palsource'), ""def f():\n    raise ValueError('palsource')\n"")
check(palsourcepkg.__loader__.get_source('palsourcepkg'), 'value = 1\n')

try:
    palsource.__loader__.get_source('palsourcemissing')
except ImportError:
    pass
else:
    raise AssertionError('palsourcemissing')

# linecache reads the source through the loader
try:
    palsource.f()
except ValueError:
    tb = traceback.format_exc()
assert ""raise ValueError('palsource')"" in tb, tb
", engine.CreateScope());
        }

        [Test]
        public void TestPathHook() {
            var pal = new MemoryPal();
            pal.AddFile(Path.Combine("other", "palother.py"), "value = 1");
            var engine = CreateEngine(pal);
            var scope = engine.CreateScope();
            scope.SetVariable("root", pal.Root);
            scope.SetVariable("other", Path.Combine(pal.Root, "other"));

            // the module is loaded from a directory which isn't on sys.path
            engine.Execute(@"
import os, sys, types
PalImporter = next(hook for hook in sys.path_hooks if getattr(hook, '__name__', None) == 'PalImporter')

# FileFinder's hook doesn't claim directories which only exist in the PAL
try:
    import palmissing
except ImportError:
    pass
assert isinstance(sys.path_importer_cache[root], PalImporter)

spec = PalImporter(other).find_spec('palother')
module = types.ModuleType('palother')
module.__spec__ = spec
spec.loader.exec_module(module)
result = module.value

# like FileFinder, None and '' are the current directory
for path in (None, ''):
    assert repr(PalImporter(path)) == 'PalImporter(%r)' % os.getcwd(), path

for path in (other + 'missing', other.encode()):
    try:
        PalImporter(path)
    except ImportError:
        pass
    else:
        raise AssertionError(path)
", scope);

            Assert.That(scope.GetVariable<int>("result"), Is.EqualTo(1));
        }

        [Test]
        public void TestPathOrder() {
            var pal = new MemoryPal();
            pal.AddFile("palfirst.py", "value = 'pal'");
            pal.AddFile("diskfirst.py", "value = 'pal'");
            pal.AddFile(Path.Combine("palns", "a.py"), "value = 'pal'");
            pal.AddFile(Path.Combine("palonlyns", "a.py"), "value = 'pal'");

            string disk = Path.Combine(Path.GetTempPath(), "PalTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(disk, "palns"));
            try {
                File.WriteAllText(Path.Combine(disk, "palfirst.py"), "value = 'disk'");
                File.WriteAllText(Path.Combine(disk, "diskfirst.py"), "value = 'disk'");
                File.WriteAllText(Path.Combine(disk, "palns", "b.py"), "value = 'disk'");

                var engine = CreateEngine(pal);
                var scope = engine.CreateScope();
                scope.SetVariable("disk", disk);

                engine.Execute(@"
import sys

def check(actual, expected):
    assert actual == expected, (actual, expected)

# sys.path order decides between the PAL and the disk
sys.path.append(disk)
import palfirst
check(palfirst.value, 'pal')

sys.path.insert(0, sys.path.pop())
import diskfirst
check(diskfirst.value, 'disk')

# namespace packages can be split between the PAL and the disk
import palns.a, palns.b
check(palns.a.value, 'pal')
check(palns.b.value, 'disk')
check(len(palns.__path__), 2)

import palonlyns.a
check(palonlyns.a.value, 'pal')
", scope);
            } finally {
                Directory.Delete(disk, true);
            }
        }
    }
}
