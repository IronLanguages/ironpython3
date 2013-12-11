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
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

[assembly: PythonModule("_warnings", typeof(IronPython.Modules.PythonWarnings))]
namespace IronPython.Modules {
    public static class PythonWarnings {
        public const string __doc__ = "Provides low-level functionality for reporting warnings";

        private static readonly object _keyFields = new object();
        private static readonly string _keyDefaultAction = "default_action";
        private static readonly string _keyFilters = "filters";
        private static readonly string _keyOnceRegistry = "once_registry";

        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, PythonDictionary/*!*/ dict) {
            List defaultFilters = new List();
            if (context.PythonOptions.WarnPython30) {
                defaultFilters.AddNoLock(PythonTuple.MakeTuple("ignore", null, PythonExceptions.DeprecationWarning, null, 0));
            }
            defaultFilters.AddNoLock(PythonTuple.MakeTuple("ignore", null, PythonExceptions.PendingDeprecationWarning, null, 0));
            defaultFilters.AddNoLock(PythonTuple.MakeTuple("ignore", null, PythonExceptions.ImportWarning, null, 0));
            defaultFilters.AddNoLock(PythonTuple.MakeTuple("ignore", null, PythonExceptions.BytesWarning, null, 0));

            context.GetOrCreateModuleState(_keyFields, () => {
                dict.Add(_keyDefaultAction, "default");
                dict.Add(_keyOnceRegistry, new PythonDictionary());
                dict.Add(_keyFilters, defaultFilters);
                return dict;
            });
        }

        #region Public API

        public static void warn(CodeContext context, object message, [DefaultParameterValue(null)]PythonType category, [DefaultParameterValue(1)]int stacklevel) {
            PythonContext pContext = PythonContext.GetContext(context);
            List argv = pContext.GetSystemStateValue("argv") as List;

            if (PythonOps.IsInstance(message, PythonExceptions.Warning)) {
                category = DynamicHelpers.GetPythonType(message);
            }
            if (category == null) {
                category = PythonExceptions.UserWarning;
            }
            if (!category.IsSubclassOf(PythonExceptions.Warning)) {
                throw PythonOps.ValueError("category is not a subclass of Warning");
            }

            TraceBackFrame caller = null;
            PythonDictionary globals;
            int lineno;
            if (PythonContext.GetContext(context).PythonOptions.Frames) {
                try {
                    caller = SysModule._getframeImpl(context, stacklevel);
                } catch (ValueErrorException) { }
            }
            if (caller == null) {
                globals = Builtin.globals(context) as PythonDictionary;
                lineno = 1;
            } else {
                globals = caller.f_globals;
                lineno = (int)caller.f_lineno;
            }

            string module;
            string filename;
            if (globals != null && globals.ContainsKey("__name__")) {
                module = (string)globals.get("__name__");
            } else {
                module = "<string>";
            }

            filename = globals.get("__file__") as string;
            if (filename == null || filename == "") {
                if (module == "__main__") {
                    if (argv != null && argv.Count > 0) {
                        filename = argv[0] as string;
                    } else {
                        // interpreter lacks sys.argv
                        filename = "__main__";
                    }
                }
                if (filename == null || filename == "") {
                    filename = module;
                }
            }

            PythonDictionary registry = (PythonDictionary)globals.setdefault("__warningregistry__", new PythonDictionary());
            warn_explicit(context, message, category, filename, lineno, module, registry, globals);
        }

        public static void warn_explicit(CodeContext context, object message, PythonType category, string filename, int lineno, [DefaultParameterValue(null)]string module, [DefaultParameterValue(null)]PythonDictionary registry, [DefaultParameterValue(null)]object module_globals) {
            PythonContext pContext = PythonContext.GetContext(context);
            PythonDictionary fields = (PythonDictionary)pContext.GetModuleState(_keyFields);
            PythonExceptions.BaseException msg;
            string text; // message text

            if (string.IsNullOrEmpty(module)) {
                module = (filename == null || filename == "") ? "<unknown>" : filename;
                if (module.EndsWith(".py")) {
                    module = module.Substring(0, module.Length - 3);
                }
            }
            if (registry == null) {
                registry = new PythonDictionary();
            }
            if (PythonOps.IsInstance(message, PythonExceptions.Warning)) {
                msg = (PythonExceptions.BaseException)message;
                text = msg.ToString();
                category = DynamicHelpers.GetPythonType(msg);
            } else {
                text = message.ToString();
                msg = PythonExceptions.CreatePythonThrowable(category, message.ToString());
            }

            PythonTuple key = PythonTuple.MakeTuple(text, category, lineno);
            if (registry.ContainsKey(key)) {
                return;
            }

            string action = Converter.ConvertToString(fields[_keyDefaultAction]);
            PythonTuple last_filter = null;
            bool loop_break = false;
            foreach (PythonTuple filter in (List)fields[_keyFilters]) {
                last_filter = filter;
                action = (string)filter._data[0];
                PythonRegex.RE_Pattern fMsg = (PythonRegex.RE_Pattern)filter._data[1];
                PythonType fCat = (PythonType)filter._data[2];
                PythonRegex.RE_Pattern fMod = (PythonRegex.RE_Pattern)filter._data[3];
                int fLno;
                if (filter._data[4] is int) {
                    fLno = (int)filter._data[4];
                } else {
                    fLno = (Extensible<int>)filter._data[4];
                }

                if ((fMsg == null || fMsg.match(text) != null) &&
                    category.IsSubclassOf(fCat) &&
                    (fMod == null || fMod.match(module) != null) &&
                    (fLno == 0 || fLno == lineno)) {
                    loop_break = true;
                    break;
                }
            }
            if (!loop_break) {
                action = Converter.ConvertToString(fields[_keyDefaultAction]);
            }

            switch (action) {
                case "ignore":
                    registry.Add(key, 1);
                    return;
                case "error":
                    throw msg.GetClrException();
                case "once":
                    registry.Add(key, 1);
                    PythonTuple onceKey = PythonTuple.MakeTuple(text, category);
                    PythonDictionary once_reg = (PythonDictionary)fields[_keyOnceRegistry];
                    if (once_reg.ContainsKey(onceKey)) {
                        return;
                    }
                    once_reg.Add(key, 1);
                    break;
                case "always":
                    break;
                case "module":
                    registry.Add(key, 1);
                    PythonTuple altKey = PythonTuple.MakeTuple(text, category, 0);
                    if (registry.ContainsKey(altKey)) {
                        return;
                    }
                    registry.Add(altKey, 1);
                    break;
                case "default":
                    registry.Add(key, 1);
                    break;
                default:
                    throw PythonOps.RuntimeError("Unrecognized action ({0}) in warnings.filters:\n {1}", action, last_filter);
            }

            object warnings = pContext.GetWarningsModule();
            if (warnings != null) {
                PythonCalls.Call(
                    context,
                    PythonOps.GetBoundAttr(context, warnings, "showwarning"),
                    msg, category, filename, lineno, null, null);
            } else {
                showwarning(context, msg, category, filename, lineno, null, null);
            }
        }

        internal static string formatwarning(object message, PythonType category, string filename, int lineno, [DefaultParameterValue(null)]string line) {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("{0}:{1}: {2}: {3}\n", filename, lineno, category.Name, message);
            if (line == null && lineno > 0 && File.Exists(filename)) {
                StreamReader reader = new StreamReader(filename);
                for (int i = 0; i < lineno - 1; i++) {
                    reader.ReadLine();
                }
                line = reader.ReadLine();
            }
            if (line != null) {
                sb.AppendFormat("  {0}\n", line.strip());
            }
            return sb.ToString();
        }

        internal static void showwarning(CodeContext context, object message, PythonType category, string filename, int lineno, [DefaultParameterValue(null)]object file, [DefaultParameterValue(null)]string line) {
            string text = formatwarning(message, category, filename, lineno, line);

            try {
                if (file == null) {
                    PythonContext pContext = PythonContext.GetContext(context);
                    PythonFile stderr = pContext.GetSystemStateValue("stderr") as PythonFile;
                    if (stderr != null) {
                        stderr.write(text);
                    } else {
                        // use CLR stderr if python's is unavailable
                        Console.Error.Write(text);
                    }
                } else {
                    if (file is PythonFile) {
                        ((PythonFile)file).write(text);
                    } else if (file is TextWriter) {
                        ((TextWriter)file).Write(text);
                    } // unrecognized file type - warning is lost
                }
            } catch (IOException) {
                // invalid file - warning is lost
            }
        }


        #endregion
    }
}
