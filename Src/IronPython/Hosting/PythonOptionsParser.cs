// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.
#if FEATURE_FULL_CONSOLE
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using Microsoft.Scripting.Hosting.Shell;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace IronPython.Hosting {

    public sealed class PythonOptionsParser : OptionsParser<PythonConsoleOptions> {
        private List<string> _warningFilters;

        public PythonOptionsParser() {
        }

        private ReadOnlySpan<char> HandleOptions(ReadOnlySpan<char> options) {
            switch (options[0]) {
                case '?':
                case 'h':
                    ConsoleOptions.PrintUsage = true;
                    ConsoleOptions.Exit = true;
                    IgnoreRemainingArgs();
                    return default;

                case 'B': break; // dont_write_bytecode always true in IronPython
                case 'd': break; // debug output from parser, always False in IronPython

                case 'b':
                    LanguageSetup.Options["BytesWarning"] = LanguageSetup.Options.ContainsKey("BytesWarning") ? Severity.Error : Severity.Warning;
                    break;

                case 'c': {
                        if (options.Length > 1) {
                            ConsoleOptions.Command = options.Slice(1).ToString();
                            PushArgBack();
                        } else {
                            ConsoleOptions.Command = PeekNextArg();
                        }
                        string[] arguments = PopRemainingArgs();
                        arguments[0] = "-c";
                        LanguageSetup.Options["Arguments"] = arguments;
                        return default;
                    }

                case 'i':
                    ConsoleOptions.Introspection = true;
                    LanguageSetup.Options["Inspect"] = ScriptingRuntimeHelpers.True;
                    break;

                case 'm': {
                        if (options.Length > 1) {
                            ConsoleOptions.ModuleToRun = options.Slice(1).ToString();
                            PushArgBack();
                        } else {
                            ConsoleOptions.ModuleToRun = PeekNextArg();
                        }
                        string[] arguments = PopRemainingArgs();
                        arguments[0] = ConsoleOptions.ModuleToRun;
                        LanguageSetup.Options["Arguments"] = arguments;
                        return default;
                    }

                case 'x':
                    ConsoleOptions.SkipFirstSourceLine = true;
                    break;

                // TODO: unbuffered stdout?
                case 'u': break;

                // TODO: create a trace listener?
                case 'v':
                    LanguageSetup.Options["Verbose"] = ScriptingRuntimeHelpers.True;
                    break;

                case 'I': // also implies both -E and -s
                    ConsoleOptions.IgnoreEnvironmentVariables = true;
                    LanguageSetup.Options["IgnoreEnvironment"] = ScriptingRuntimeHelpers.True;
                    LanguageSetup.Options["NoUserSite"] = ScriptingRuntimeHelpers.True;
                    LanguageSetup.Options["Isolated"] = ScriptingRuntimeHelpers.True;
                    break;

                case 'S':
                    ConsoleOptions.SkipImportSite = true;
                    LanguageSetup.Options["NoSite"] = ScriptingRuntimeHelpers.True;
                    break;

                case 's':
                    LanguageSetup.Options["NoUserSite"] = ScriptingRuntimeHelpers.True;
                    break;

                case 'E':
                    ConsoleOptions.IgnoreEnvironmentVariables = true;
                    LanguageSetup.Options["IgnoreEnvironment"] = ScriptingRuntimeHelpers.True;
                    break;

                case 't': break; // ignore for backwards compatibility

                case 'O':
                    if (LanguageSetup.Options.ContainsKey("Optimize")) LanguageSetup.Options["StripDocStrings"] = ScriptingRuntimeHelpers.True;
                    LanguageSetup.Options["Optimize"] = ScriptingRuntimeHelpers.True;
                    break;

                case 'V':
                    ConsoleOptions.PrintVersion = true;
                    ConsoleOptions.Exit = true;
                    IgnoreRemainingArgs();
                    return default;

                case 'W':
                    _warningFilters ??= new List<string>();
                    _warningFilters.Add(options.Length > 1 ? options.Slice(1).ToString() : PopNextArg());
                    return default;

                case 'q':
                    LanguageSetup.Options["Quiet"] = ScriptingRuntimeHelpers.True;
                    break;

                case 'X':
                    var split = (options.Length > 1 ? options.Slice(1).ToString() : PopNextArg()).Split(new[] { '=' }, 2);
                    HandleImplementationSpecificOption(split[0], split.Length > 1 ? split[1] : null);
                    return default;

                default:
                    throw new InvalidOptionException($"Unknown option: {options[0]}");
            }
            return options.Slice(1);
        }

        /// <exception cref="Exception">On error.</exception>
        protected override void ParseArgument(string/*!*/ arg) {
            ContractUtils.RequiresNotNull(arg, nameof(arg));

            if (arg == "-") {
                PushArgBack();
                LanguageSetup.Options["Arguments"] = PopRemainingArgs();
                return;
            }

            if (arg == "/?" || arg == "--help") {
                HandleOptions("h".AsSpan());
                return;
            }

            if (arg.StartsWith("-", StringComparison.Ordinal)) {
                if (arg.StartsWith("-X:", StringComparison.Ordinal)) {
                    // old implementation specific options for compat
                    switch (arg) {
                        case "-X:NoFrames":
                        case "-X:Frames":
                        case "-X:FullFrames":
                        case "-X:Tracing":
                        case "-X:EnableProfiler":
                        case "-X:LightweightScopes":
                        case "-X:MTA":
                        case "-X:Debug":
                        case "-X:BasicConsole":
#if DEBUG
                        case "-X:NoImportLib":
#endif
                            HandleImplementationSpecificOption(arg.Substring(3), null);
                            break;

                        case "-X:GCStress":
                        case "-X:MaxRecursion":
                        case "-X:NoDebug":
                            HandleImplementationSpecificOption(arg.Substring(3), PopNextArg());
                            break;

                        default:
                            base.ParseArgument(arg);
                            if (ConsoleOptions.FileName != null) {
                                // Note that CPython ignores unknown implementation-specific options
                                // but we'll throw when -X:Unknown is used to avoid potential mixing
                                // of old/new style errors (e.g. -X:CompilationThreshold=1).
                                throw new InvalidOptionException($"Unknown option: {arg}");
                            }
                            break;
                    }
                } else {
                    var options = arg.AsSpan(1);
                    while (options.Length > 0) {
                        options = HandleOptions(options);
                    }
                }
            } else {
                ConsoleOptions.FileName = arg.Trim();
                PushArgBack();
                LanguageSetup.Options["Arguments"] = PopRemainingArgs();
            }
        }

        protected override void HandleImplementationSpecificOption(string arg, string val) {
            switch (arg) {
                case "NoFrames":
                    if (LanguageSetup.Options.ContainsKey("Frames") && LanguageSetup.Options["Frames"] != ScriptingRuntimeHelpers.False) {
                        throw new InvalidOptionException("Only one of -X [Full]Frames/NoFrames may be specified");
                    }
                    LanguageSetup.Options["Frames"] = ScriptingRuntimeHelpers.False;
                    break;

                case "Frames":
                    if (LanguageSetup.Options.ContainsKey("Frames") && LanguageSetup.Options["Frames"] != ScriptingRuntimeHelpers.True) {
                        throw new InvalidOptionException("Only one of -X [Full]Frames/NoFrames may be specified");
                    }
                    LanguageSetup.Options["Frames"] = ScriptingRuntimeHelpers.True;
                    break;

                case "FullFrames":
                    if (LanguageSetup.Options.ContainsKey("Frames") && LanguageSetup.Options["Frames"] != ScriptingRuntimeHelpers.True) {
                        throw new InvalidOptionException("Only one of -X [Full]Frames/NoFrames may be specified");
                    }
                    LanguageSetup.Options["Frames"] = LanguageSetup.Options["FullFrames"] = ScriptingRuntimeHelpers.True;
                    break;

                case "Tracing":
                    LanguageSetup.Options["Tracing"] = ScriptingRuntimeHelpers.True;
                    break;

                case "GCStress":
                    int gcStress;
                    if (!int.TryParse(val, out gcStress) || gcStress < 0 || gcStress > GC.MaxGeneration) {
                        throw new InvalidOptionException(string.Format("The argument for the -X {0} option must be between 0 and {1}.", arg, GC.MaxGeneration));
                    }
                    LanguageSetup.Options["GCStress"] = gcStress;
                    break;

                case "MaxRecursion":
                    // we need about 6 frames for starting up, so 10 is a nice round number.
                    int limit;
                    if (!int.TryParse(val, out limit) || limit < 10) {
                        throw new InvalidOptionException($"The argument for the -X {arg} option must be an integer >= 10.");
                    }
                    LanguageSetup.Options["RecursionLimit"] = limit;
                    break;

                case "EnableProfiler":
                    LanguageSetup.Options["EnableProfiler"] = ScriptingRuntimeHelpers.True;
                    break;

                case "LightweightScopes":
                    LanguageSetup.Options["LightweightScopes"] = ScriptingRuntimeHelpers.True;
                    break;

                case "MTA":
                    ConsoleOptions.IsMta = true;
                    break;

                case "Debug":
                    RuntimeSetup.DebugMode = true;
                    LanguageSetup.Options["Debug"] = ScriptingRuntimeHelpers.True;
                    break;

                case "NoDebug":
                    if (string.IsNullOrEmpty(val)) throw new InvalidOptionException($"Argument expected for the -X NoDebug option.");
                    try {
                        LanguageSetup.Options["NoDebug"] = new Regex(val);
                    } catch {
                        throw new InvalidOptionException($"The argument for the -X {arg} option must be a valid regex pattern.");
                    }
                    break;

                case "BasicConsole":
                    ConsoleOptions.BasicConsole = true;
                    break;

#if DEBUG
                case "NoImportLib":
                    LanguageSetup.Options["NoImportLib"] = ScriptingRuntimeHelpers.True;
                    break;
#endif

                default:
                    try {
                        base.HandleImplementationSpecificOption(arg, val);
                    } catch (InvalidOptionException e) {
                        throw new InvalidOptionException(e.Message.Replace("-X:", "-X "));
                    }
                    break;
            }
        }

        protected override void AfterParse() {
            if (_warningFilters != null) {
                LanguageSetup.Options["WarningFilters"] = _warningFilters.ToArray();
            }
        }

        public override void GetHelp(out string commandLine, out string[,] options, out string[,] environmentVariables, out string comments) {
            string[,] standardOptions;
            base.GetHelp(out _, out standardOptions, out environmentVariables, out comments);

            commandLine = "[option] ... [-c cmd | -m mod | file | -] [arg] ...";

            // only keep the implementation-specific options from the DLR
            List<KeyValuePair<string, string>> standardOptionsList = new();
            for (var i = 0; i < standardOptions.GetLength(0); i++) {
                if (standardOptions[i, 0].StartsWith("-X:", StringComparison.Ordinal)) {
                    standardOptionsList.Add(new KeyValuePair<string, string>(standardOptions[i, 0].Replace(' ', '=').Replace(':', ' '), standardOptions[i, 1]));
                }
            }
            standardOptions = new string[standardOptionsList.Count, 2];
            for (var i = 0; i < standardOptionsList.Count; i++) {
                standardOptions[i, 0] = standardOptionsList[i].Key;
                standardOptions[i, 1] = standardOptionsList[i].Value;
            }

            string[,] pythonOptions = new string[,] {
                { "-b",                     "issue warnings about str(bytes_instance), str(bytearray_instance) and comparing bytes/bytearray with str. (-bb: issue errors)"},
                { "-c cmd",                 "Program passed in as string (terminates option list)" },
                { "-E",                     "Ignore environment variables" },
                { "-h",                     "Display usage" },
                { "-i",                     "Inspect interactively after running script" },
                { "-I",                     "isolate IronPython from the user's environment (implies -E and -s)" },
                { "-m mod",                 "run library module as a script"},
                { "-O",                     "generate optimized code" },
                { "-OO",                    "remove doc strings and apply -O optimizations" },
                { "-q",                     "don't print version and copyright messages on interactive startup" },
                { "-s",                     "Don't add user site directory to sys.path" },
                { "-S",                     "Don't imply 'import site' on initialization" },
                { "-u",                     "Unbuffered stdout & stderr" },
#if !IRONPYTHON_WINDOW
                { "-v",                     "Verbose (trace import statements) (also PYTHONVERBOSE=x)" },
#endif
                { "-V",                     "Print the version number and exit" },
                { "-W arg",                 "Warning control (arg is action:message:category:module:lineno) also IRONPYTHONWARNINGS=arg" },
                { "-x",                     "Skip first line of the source" },

                { "-X NoFrames",            "Disable sys._getframe support, can improve execution speed" },
                { "-X Frames",              "Enable basic sys._getframe support" },
                { "-X FullFrames",          "Enable sys._getframe with access to locals" },
                { "-X Tracing",             "Enable support for tracing all methods even before sys.settrace is called" },
                { "-X GCStress=<level>",    "Specifies the GC stress level (the generation to collect each statement)" },
                { "-X MaxRecursion=<level>","Set the maximum recursion level" },
                { "-X Debug",               "Enable application debugging (preferred over -D)" },
                { "-X NoDebug=<regex>",     "Provides a regular expression of files which should not be emitted in debug mode"},
                { "-X MTA",                 "Run in multithreaded apartment" },
                { "-X EnableProfiler",      "Enables profiling support in the compiler" },
                { "-X LightweightScopes",   "Generate optimized scopes that can be garbage collected" },
                { "-X BasicConsole",        "Use only the basic console features" },
#if DEBUG
                { "-X NoImportLib",         "Don't bootstrap importlib [debug only]" },
#endif
            };

            // Ensure the combined options come out sorted
            string[,] allOptions = ArrayUtils.Concatenate(pythonOptions, standardOptions);
            var opts = new List<KeyValuePair<string, string>>();
            for (var i = 0; i < allOptions.GetLength(0); i++) {
                opts.Add(new KeyValuePair<string, string>(allOptions[i, 0], allOptions[i, 1]));
            }
            List<string> optName = new List<string>();
            List<int> indiciesList = new List<int>();
            for (int i = 0; i < allOptions.GetLength(0); i++) {
                optName.Add(allOptions[i, 0]);
                indiciesList.Add(i);
            }

            var keys = optName.ToArray();
            var indicies = indiciesList.ToArray();
            Array.Sort(keys, indicies, StringComparer.OrdinalIgnoreCase);

            options = new string[allOptions.GetLength(0), 2];
            for (int i = 0; i < indicies.Length; i++) {
                options[i, 0] = allOptions[indicies[i], 0];
                options[i, 1] = allOptions[indicies[i], 1];
            }

            Debug.Assert(environmentVariables.GetLength(0) == 0); // No need to append if the default is empty
            environmentVariables = new string[,] {
                { "IRONPYTHONPATH",        "Path to search for module" },
                { "IRONPYTHONSTARTUP",     "Startup module" }
            };
        }
    }
}
#endif
