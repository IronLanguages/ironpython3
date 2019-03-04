// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;

using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

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
        private static readonly object _lockCountKey = new object();

        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, PythonDictionary/*!*/ dict) {
            // set the lock count to zero on the 1st load, don't reset the lock count on reloads
            if (!context.HasModuleState(_lockCountKey)) {
                context.SetModuleState(_lockCountKey, 0L);
            }
        }

        public static PythonList extension_suffixes() {
            // TODO: support extensions?
            return new PythonList();
        }

        public static bool lock_held(CodeContext/*!*/ context) {
            return GetLockCount(context) != 0;
        }

        public static void acquire_lock(CodeContext/*!*/ context) {
            lock (_lockCountKey) {
                SetLockCount(context, GetLockCount(context) + 1);
            }
        }

        public static void release_lock(CodeContext/*!*/ context) {
            lock (_lockCountKey) {
                long lockCount = GetLockCount(context);
                if (lockCount == 0) {
                    throw PythonOps.RuntimeError("not holding the import lock");
                }
                SetLockCount(context, lockCount - 1);
            }            
        }

        public static object init_builtin(CodeContext/*!*/ context, string/*!*/ name) {
            if (name == null) throw PythonOps.TypeError("init_builtin() argument 1 must be string, not None");
            return LoadBuiltinModule(context, name);
        }

        public static object init_frozen(string name) {
            return null;
        }

        public static object get_frozen_object(string name) {
            throw PythonOps.ImportError("No such frozen object named {0}", name);
        }

        public static int is_builtin(CodeContext/*!*/ context, string/*!*/ name) {
            if (name == null) throw PythonOps.TypeError("is_builtin() argument 1 must be string, not None");
            Type ty;
            if (context.LanguageContext.BuiltinModules.TryGetValue(name, out ty)) {
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

        public static object load_dynamic(string name, string pathname, object file = null) {
            return null;
        }

        public static void _fix_co_filename() {
            throw new NotImplementedException();
        }

        #region Implementation

        private static object LoadBuiltinModule(CodeContext/*!*/ context, string/*!*/ name) {
            Assert.NotNull(context, name);
            return Importer.ImportBuiltin(context, name);
        }

        #endregion

        private static long GetLockCount(CodeContext/*!*/ context) {
            return (long)context.LanguageContext.GetModuleState(_lockCountKey);
        }

        private static void SetLockCount(CodeContext/*!*/ context, long lockCount) {
            context.LanguageContext.SetModuleState(_lockCountKey, lockCount);
        }

        [PythonType]
        public sealed class NullImporter {
            public NullImporter(string path_string) {
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
            public object find_module(params object[] args) {
                return null;
            }
        }
    }
}
