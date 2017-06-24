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

using System;
using System.Diagnostics;
using System.Reflection;

using Microsoft.Scripting;
using Microsoft.Scripting.Ast;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

namespace IronPython.Compiler {
    /// <summary>
    /// Provides cached global variable for modules to enable optimized access to
    /// module globals.  Both the module global value and the cached value can be held
    /// onto and the cached value can be invalidated by the providing LanguageContext.
    /// 
    /// The cached value is provided by the LanguageContext.GetModuleCache API.
    /// </summary>
    [DebuggerDisplay("{Display}")]
    public sealed class PythonGlobal {
        private object _value;
        private ModuleGlobalCache _global;
        private string _name;
        private CodeContext/*!*/ _context;

        internal static PropertyInfo/*!*/ CurrentValueProperty = typeof(PythonGlobal).GetProperty("CurrentValue");
        internal static PropertyInfo/*!*/ RawValueProperty = typeof(PythonGlobal).GetProperty("RawValue");

        public PythonGlobal(CodeContext/*!*/ context, string name) {
            Assert.NotNull(context);

            _value = Uninitialized.Instance;
            _context = context;
            _name = name;
        }

        public object CurrentValue {
            get {
                if (_value != Uninitialized.Instance) {
                    return _value;
                }

                return GetCachedValue(false);
            }
            set {
                if (value == Uninitialized.Instance && _value == Uninitialized.Instance) {
                    throw PythonOps.GlobalNameError(_name);

                }
                _value = value;
            }
        }

        public object CurrentValueLightThrow {
            get {
                if (_value != Uninitialized.Instance) {
                    return _value;
                }

                return GetCachedValue(true);
            }
        }

        public string Name { get { return _name; } }

        private object GetCachedValue(bool lightThrow) {
            if (_global == null) {                
                _global = ((PythonContext)_context.LanguageContext).GetModuleGlobalCache(_name);
            }

            if (_global.IsCaching) {
                if (_global.HasValue) {
                    return _global.Value;
                }
            } else {
                object value;

                if (_context.TryLookupBuiltin(_name, out value)) {
                    return value;
                }
            }

            if (lightThrow) {
                return LightExceptions.Throw(PythonOps.GlobalNameError(_name));
            }
            throw PythonOps.GlobalNameError(_name);
        }

        public object RawValue {
            get {
                return _value;
            }
            internal set {
                _value = value;
            }
        }

        public string Display {
            get {
                try {
                    return GetStringDisplay(CurrentValue);
                } catch (MissingMemberException) {
                    return "<uninitialized>";
                }
            }
        }

        private static string GetStringDisplay(object val) {
            return val == null ? "(null)" : val.ToString();
        }

        public override string ToString() {
            return string.Format("ModuleGlobal: {0} Value: {1} ({2})",
                _name,
                _value,
                RawValue == Uninitialized.Instance ? "Module Local" : "Global");
        }


    }
}
