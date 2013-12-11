/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

using System.Diagnostics;
using System.Threading;
using IronPython.Runtime.Binding;
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
                return (PythonContext)_default.LanguageContext;
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
