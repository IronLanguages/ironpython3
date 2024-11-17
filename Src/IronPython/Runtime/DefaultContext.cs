// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Threading;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime {
    public static class DefaultContext {
        [MultiRuntimeAware]
        internal static CodeContext _default;
        [MultiRuntimeAware]
        internal static CodeContext _defaultCLS;

        public static ContextId Id {
            get {
                return Default.LanguageContext.ContextId;
            }
        }

        public static CodeContext Default {
            get {
                Debug.Assert(_default != null);
                return _default;
            }
        }

        public static PythonContext DefaultPythonContext {
            get {
                Debug.Assert(_default != null);
                return _default.LanguageContext;
            }
        }

        public static CodeContext DefaultCLS {
            get {
                Debug.Assert(_defaultCLS != null);
                return _defaultCLS;
            }
        }

        internal static CodeContext/*!*/ CreateDefaultCLSContext(PythonContext/*!*/ context) {
            ModuleContext mc = new ModuleContext(new PythonDictionary(), context);
            mc.ShowCls = true;
            return mc.GlobalContext;
        }

        internal static void InitializeDefaults(CodeContext defaultContext, CodeContext defaultClsCodeContext) {
            Interlocked.CompareExchange(ref _default, defaultContext, null);
            Interlocked.CompareExchange(ref _defaultCLS, defaultClsCodeContext, null);

        }
    }
}
