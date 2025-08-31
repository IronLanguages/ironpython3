// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

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
        private object? _value;
        private ModuleGlobalCache? _global;
        private readonly string _name;
        private readonly CodeContext/*!*/ _context;

        internal static PropertyInfo/*!*/ CurrentValueProperty = typeof(PythonGlobal).GetProperty(nameof(CurrentValue))!;
        internal static PropertyInfo/*!*/ RawValueProperty = typeof(PythonGlobal).GetProperty(nameof(RawValue))!;

        public PythonGlobal(CodeContext/*!*/ context, string name) {
            Assert.NotNull(context);

            _value = Uninitialized.Instance;
            _context = context;
            _name = name;
        }

        public object? CurrentValue {
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

        public object? CurrentValueLightThrow {
            get {
                if (_value != Uninitialized.Instance) {
                    return _value;
                }

                return GetCachedValue(true);
            }
        }

        public string Name => _name;

        private object? GetCachedValue(bool lightThrow) {
            _global ??= _context.LanguageContext.GetModuleGlobalCache(_name);

            if (_global.IsCaching) {
                if (_global.HasValue) {
                    return _global.Value;
                }
            } else {
                if (_context.TryLookupBuiltin(_name, out object? value)) {
                    return value;
                }
            }

            var ex = PythonOps.NameError(_name);
            if (lightThrow) {
                return LightExceptions.Throw(ex);
            }
            throw ex;
        }

        public object? RawValue {
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

        private static string GetStringDisplay(object? val) {
            return val is null ? "(null)" : val.ToString() ?? "<no representation>";
        }

        public override string ToString() {
            return string.Format("ModuleGlobal: {0} Value: {1} ({2})",
                _name,
                _value,
                RawValue == Uninitialized.Instance ? "Module Local" : "Global");
        }


    }
}
