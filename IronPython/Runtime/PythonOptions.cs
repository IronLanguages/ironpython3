// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime {

    [Serializable, CLSCompliant(true)]
    public sealed class PythonOptions : LanguageOptions {
        /// <summary>
        /// Gets the collection of command line arguments.
        /// </summary>
        public ReadOnlyCollection<string>/*!*/ Arguments { get; }

        /// <summary>
        ///  Should we strip out all doc strings (the -O command line option).
        /// </summary>
        public bool Optimize { get; }

        /// <summary>
        ///  Should we strip out all doc strings (the -OO command line option).
        /// </summary>
        public bool StripDocStrings { get; }

        /// <summary>
        ///  List of -W (warning filter) options collected from the command line.
        /// </summary>
        public ReadOnlyCollection<string>/*!*/ WarningFilters { get; }

        public Severity BytesWarning { get; }

        /// <summary>
        /// Enables debugging support.  When enabled a .NET debugger can be attached
        /// to the process to step through Python code.
        /// </summary>
        public bool Debug { get; }

        /// <summary>
        /// Enables inspect mode.  After running the main module the REPL will be started
        /// within that modules context.
        /// </summary>
        public bool Inspect { get; }

        /// <summary>
        /// Suppresses addition of the user site directory.  This is ignored by IronPython
        /// except for updating sys.flags.
        /// </summary>
        public bool NoUserSite { get; }

        /// <summary>
        /// Disables import site on startup.
        /// </summary>
        public bool NoSite { get; }

        /// <summary>
        /// Ignore environment variables that configure the IronPython context.
        /// </summary>
        public bool IgnoreEnvironment { get; }

        /// <summary>
        /// Run in isolated mode.
        /// </summary>
        public bool Isolated { get; }

        /// <summary>
        /// Run in UTF-8 mode.
        /// </summary>
        internal bool Utf8Mode { get; }

        /// <summary>
        /// Enables the verbose option which traces import statements.  This is ignored by IronPython
        /// except for setting sys.flags.
        /// </summary>
        public bool Verbose { get; }

        /// <summary>
        /// Sets the maximum recursion depth.  Setting to Int32.MaxValue will disable recursion
        /// enforcement.
        /// </summary>
        public int RecursionLimit { get; }

        /// <summary>
        /// Makes available sys._getframe.  Local variables will not be available in frames unless the
        /// function calls locals(), dir(), vars(), etc...  For ensuring locals are always available use
        /// the FullFrames option.
        /// </summary>
        public bool Frames { get; }

        /// <summary>
        /// Makes available sys._getframe.  All locals variables will live on the heap (for a considerable
        /// performance cost) enabling introspection of all code.
        /// </summary>
        public bool FullFrames { get; }

        /// <summary>
        /// Tracing is always available.  Without this option tracing is only enabled when sys.settrace
        /// is called. This means code that was already running before sys.settrace will not be debuggable.
        /// 
        /// With this option pdb.set_trace and pdb.post_mortem will always work properly.
        /// </summary>
        public bool Tracing { get; }

        /// <summary>
        /// Forces all code to be compiled in a mode in which the code can be reliably collected by the CLR.
        /// </summary>
        public bool LightweightScopes { get; }

        /// <summary>
        /// Enable profiling code
        /// </summary>
        public bool EnableProfiler { get; set; }

        public int? GCStress { get; }

        /// <summary>
        /// Returns a regular expression of Python files which should not be emitted in debug mode.
        /// </summary>
        public Regex NoDebug { get; }

        public bool Quiet { get; }

        /// <summary>
        /// On Basic level, console IO streams are emulated using console writer/reader.
        /// </summary>
        public SharedIO.SupportLevel ConsoleSupportLevel { get; }

        internal bool NoImportLib { get; } // TODO: get rid of me when we no longer bootstrap importlib

        public PythonOptions()
            : this(null) {
        }

        public PythonOptions(IDictionary<string, object> options)
            : base(EnsureSearchPaths(options)) {

            Arguments = GetStringCollectionOption(options, "Arguments") ?? EmptyStringCollection;
            WarningFilters = GetStringCollectionOption(options, "WarningFilters", ';', ',') ?? EmptyStringCollection;

            BytesWarning = GetEnumOption(options, "BytesWarning", Severity.Ignore);
            Debug = GetOption(options, "Debug", false);
            Inspect = GetOption(options, "Inspect", false);
            NoUserSite = GetOption(options, "NoUserSite", false);
            NoSite = GetOption(options, "NoSite", false);
            IgnoreEnvironment = GetOption(options, "IgnoreEnvironment", false);
            Verbose = GetOption(options, "Verbose", false);
            Optimize = GetOption(options, "Optimize", false);
            StripDocStrings = GetOption(options, "StripDocStrings", false);
            RecursionLimit = GetOption(options, "RecursionLimit", Int32.MaxValue);
            EnableProfiler = GetOption(options, "EnableProfiler", false);
            LightweightScopes = GetOption(options, "LightweightScopes", false);
            FullFrames = GetOption(options, "FullFrames", false);
            Frames = FullFrames || GetOption(options, "Frames", true);
            GCStress = GetOption<int?>(options, "GCStress", null);
            Tracing = GetOption(options, "Tracing", false);
            NoDebug = GetOption(options, "NoDebug", (Regex)null);
            Quiet = GetOption(options, "Quiet", false);
            NoImportLib = GetOption(options, "NoImportLib", false);
            Isolated = GetOption(options, "Isolated", false);
            Utf8Mode = GetOption(options, "Utf8Mode", false);
            ConsoleSupportLevel = GetEnumOption(options, "ConsoleSupportLevel", SharedIO.SupportLevel.Full);
        }

        private static IDictionary<string, object> EnsureSearchPaths(IDictionary<string, object> options) {
            if (options == null) {
                return new Dictionary<string, object>() { { "SearchPaths", new[] { "." } } };
            } else if (!options.ContainsKey("SearchPaths")) {
                options["SearchPaths"] = new[] { "." };
            }
            return options;
        }

        private static T GetEnumOption<T>(IDictionary<string, object> options, string name, T defaultValue) where T : struct, Enum {
            if (options != null && options.TryGetValue(name, out object value)) {
                if (value is T variable) {
                    return variable;
                }
                Type rettype = typeof(T);
                if (value is string strval) {
                    return (T)Enum.Parse(rettype, strval, ignoreCase: false);
                }
                return (T)Convert.ChangeType(value, Enum.GetUnderlyingType(rettype), CultureInfo.CurrentCulture);
            }
            return defaultValue;
        }
    }
}
