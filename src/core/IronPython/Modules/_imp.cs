// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

[assembly: PythonModule("_imp", typeof(IronPython.Modules.PythonImport))]
namespace IronPython.Modules {
    public static class PythonImport {
        public const string __doc__ = "Provides functions for programmatically creating and importing modules and packages.";

        internal const int PythonSource = 1;
        internal const int PythonCompiled = 2;
        internal const int CExtension = 3;
        internal const int PythonResource = 4;
        internal const int PackageDirectory = 5;
        internal const int CBuiltin = 6;
        internal const int PythonFrozen = 7;
        internal const int PythonCodeResource = 8;
        internal const int SearchError = 0;
        internal const int ImporterHook = 9;

        public static PythonList extension_suffixes() {
            // TODO: support extensions?
            return new PythonList();
        }

        public static bool lock_held(CodeContext/*!*/ context) {
            // held by any thread
            Lock importLock = context.LanguageContext.ImportLock;
            if (importLock.IsHeldByCurrentThread) return true; // the current thread already holds the lock
            if (!importLock.TryEnter()) return true; // try an exclusive lock, another thread might already hold the lock
            importLock.Exit();
            return false;
        }

        public static void acquire_lock(CodeContext/*!*/ context) {
            context.LanguageContext.ImportLock.Enter();
        }

        public static void release_lock(CodeContext/*!*/ context) {
            Lock importLock = context.LanguageContext.ImportLock;
            if (!importLock.IsHeldByCurrentThread) {
                throw PythonOps.RuntimeError("not holding the import lock");
            }
            importLock.Exit();
        }

        public static object init_builtin(CodeContext/*!*/ context, [NotNone] string/*!*/ name) {
            if (name == null) throw PythonOps.TypeError("init_builtin() argument 1 must be string, not None");

            PythonContext pc = context.LanguageContext;

            // check if the module is already in sys.modules and reload it if it is
            if (pc.SystemStateModules.TryGetValue(name, out var moduleObj) && moduleObj is PythonModule module && module.GetName() == name && module.IsBuiltin) {
                Debug.Assert(module.__dict__._storage is ModuleDictionaryStorage);
                ((ModuleDictionaryStorage)module.__dict__._storage).Reload();
                return module;
            }

            return Importer.ImportBuiltin(context, name);
        }

        // new in Python 3.5
        public static object create_builtin(CodeContext/*!*/ context, object spec) {
            // TODO: implement this properly - https://github.com/IronLanguages/ironpython3/issues/1430
            var name = PythonOps.GetBoundAttr(context, spec, "name") as string;
            return init_builtin(context, name);
        }

        // new in Python 3.5
        public static void exec_builtin(CodeContext/*!*/ context, object mod) {
            // TODO: implement this properly - https://github.com/IronLanguages/ironpython3/issues/1430
        }

        public static object init_frozen(string name) {
            return null;
        }

        public static object get_frozen_object(string name) {
            throw PythonOps.ImportError("No such frozen object named {0}", name);
        }

        public static int is_builtin(CodeContext/*!*/ context, string/*!*/ name) {
            if (name == null) throw PythonOps.TypeError("is_builtin() argument 1 must be string, not None");
            if (context.LanguageContext.BuiltinModules.TryGetValue(name, out Type ty)) {
                if (ty.Assembly == typeof(PythonContext).Assembly) {
                    // supposedly these can't be re-initialized and return -1 to
                    // indicate that here, but CPython does allow passing them
                    // to init_builtin.
                    return -1;
                }


                return 1;
            }
            return 0;
        }

        public static bool is_frozen(string name) {
            return false;
        }

        public static bool is_frozen_package(string name) {
            return false;
        }

        public static void _fix_co_filename() {
            throw new NotImplementedException();
        }
    }
}
