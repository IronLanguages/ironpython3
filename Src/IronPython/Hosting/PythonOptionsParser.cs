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

        /// <exception cref="Exception">On error.</exception>
        protected override void ParseArgument(string/*!*/ arg) {
            ContractUtils.RequiresNotNull(arg, nameof(arg));

            switch (arg) {
                case "-B": break; // dont_write_bytecode always true in IronPython
                case "-U": break; // unicode always true in IronPython
                case "-d": break; // debug output from parser, always False in IronPython

                case "-b": // Not shown in help on CPython
                    LanguageSetup.Options["BytesWarning"] = ScriptingRuntimeHelpers.True;
                    break;

                case "-c":
                    ConsoleOptions.Command = PeekNextArg();
                    string[] arguments = PopRemainingArgs();
                    arguments[0] = arg;
                    LanguageSetup.Options["Arguments"] = arguments;
                    break;

                case "-?":
                    ConsoleOptions.PrintUsage = true;
                    ConsoleOptions.Exit = true;
                    break;

                case "-i":
                    ConsoleOptions.Introspection = true;
                    LanguageSetup.Options["Inspect"] = ScriptingRuntimeHelpers.True;
                    break;

                case "-m":
                    ConsoleOptions.ModuleToRun = PeekNextArg();
                    LanguageSetup.Options["Arguments"] = PopRemainingArgs();
                    break;

                case "-x":
                    ConsoleOptions.SkipFirstSourceLine = true;
                    break;

                // TODO: unbuffered stdout?
                case "-u": break;

                // TODO: create a trace listener?
                case "-v":
                    LanguageSetup.Options["Verbose"] = ScriptingRuntimeHelpers.True;
                    break;

                case "-S":
                    ConsoleOptions.SkipImportSite = true;
                    LanguageSetup.Options["NoSite"] = ScriptingRuntimeHelpers.True;
                    break;

                case "-s":
                    LanguageSetup.Options["NoUserSite"] = ScriptingRuntimeHelpers.True;
                    break;

                case "-E":
                    ConsoleOptions.IgnoreEnvironmentVariables = true;
                    LanguageSetup.Options["IgnoreEnvironment"] = ScriptingRuntimeHelpers.True;
                    break;

                case "-t": LanguageSetup.Options["IndentationInconsistencySeverity"] = Severity.Warning; break;
                case "-tt": LanguageSetup.Options["IndentationInconsistencySeverity"] = Severity.Error; break;

                case "-O":
                    LanguageSetup.Options["Optimize"] = ScriptingRuntimeHelpers.True;
                    break;

                case "-OO":
                    LanguageSetup.Options["Optimize"] = ScriptingRuntimeHelpers.True;
                    LanguageSetup.Options["StripDocStrings"] = ScriptingRuntimeHelpers.True;
                    break;

                case "-V":
                    ConsoleOptions.PrintVersion = true;
                    ConsoleOptions.Exit = true;
                    IgnoreRemainingArgs();
                    break;

                case "-W":
                    if (_warningFilters == null) {
                        _warningFilters = new List<string>();
                    }

                    _warningFilters.Add(PopNextArg());
                    break;

                case "-q":
                    LanguageSetup.Options["Quiet"] = ScriptingRuntimeHelpers.True;
                    break;

                case "-":
                    PushArgBack();
                    LanguageSetup.Options["Arguments"] = PopRemainingArgs();
                    break;

                case "-X:NoFrames":
                    if (LanguageSetup.Options.ContainsKey("Frames") && LanguageSetup.Options["Frames"] != ScriptingRuntimeHelpers.False) {
                        throw new InvalidOptionException("Only one of -X:[Full]Frames/-X:NoFrames may be specified");
                    }
                    LanguageSetup.Options["Frames"] = ScriptingRuntimeHelpers.False;
                    break;
                case "-X:Frames":
                    if (LanguageSetup.Options.ContainsKey("Frames") && LanguageSetup.Options["Frames"] != ScriptingRuntimeHelpers.True) {
                        throw new InvalidOptionException("Only one of -X:[Full]Frames/-X:NoFrames may be specified");
                    }
                    LanguageSetup.Options["Frames"] = ScriptingRuntimeHelpers.True;
                    break;
                case "-X:FullFrames":
                    if (LanguageSetup.Options.ContainsKey("Frames") && LanguageSetup.Options["Frames"] != ScriptingRuntimeHelpers.True) {
                        throw new InvalidOptionException("Only one of -X:[Full]Frames/-X:NoFrames may be specified");
                    }
                    LanguageSetup.Options["Frames"] = LanguageSetup.Options["FullFrames"] = ScriptingRuntimeHelpers.True;
                    break;
                case "-X:Tracing":
                    LanguageSetup.Options["Tracing"] = ScriptingRuntimeHelpers.True;
                    break;
                case "-X:GCStress":
                    int gcStress;
                    if (!int.TryParse(PopNextArg(), out gcStress) || (gcStress < 0 || gcStress > GC.MaxGeneration)) {
                        throw new InvalidOptionException(String.Format("The argument for the {0} option must be between 0 and {1}.", arg, GC.MaxGeneration));
                    }

                    LanguageSetup.Options["GCStress"] = gcStress;
                    break;

                case "-X:MaxRecursion":
                    // we need about 6 frames for starting up, so 10 is a nice round number.
                    int limit;
                    if (!int.TryParse(PopNextArg(), out limit) || limit < 10) {
                        throw new InvalidOptionException(String.Format("The argument for the {0} option must be an integer >= 10.", arg));
                    }

                    LanguageSetup.Options["RecursionLimit"] = limit;
                    break;

                case "-X:EnableProfiler":
                    LanguageSetup.Options["EnableProfiler"] = ScriptingRuntimeHelpers.True;
                    break;

                case "-X:LightweightScopes":
                    LanguageSetup.Options["LightweightScopes"] = ScriptingRuntimeHelpers.True;
                    break;

                case "-X:MTA":
                    ConsoleOptions.IsMta = true;
                    break;

                case "-X:Debug":
                    RuntimeSetup.DebugMode = true;
                    LanguageSetup.Options["Debug"] = ScriptingRuntimeHelpers.True;
                    break;

                case "-X:NoDebug":
                    string regex = PopNextArg();
                    try {
                        LanguageSetup.Options["NoDebug"] = new Regex(regex);
                    } catch {
                        throw InvalidOptionValue("-X:NoDebug", regex);
                    }
                    
                    break;

                case "-X:BasicConsole":
                    ConsoleOptions.BasicConsole = true;
                    break;

                default:
                    if(arg.StartsWith("-W")) {
                        if (_warningFilters == null) {
                            _warningFilters = new List<string>();
                        }

                        _warningFilters.Add(arg.Substring(2));
                        break;
                    }

                    if (arg.StartsWith("-m")) {
                        ConsoleOptions.ModuleToRun = arg.Substring(2);
                        LanguageSetup.Options["Arguments"] = PopRemainingArgs();
                        break;
                    }

                    base.ParseArgument(arg);

                    if (ConsoleOptions.FileName != null) {
                        PushArgBack();
                        LanguageSetup.Options["Arguments"] = PopRemainingArgs();
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
            base.GetHelp(out commandLine, out standardOptions, out environmentVariables, out comments);
#if !IRONPYTHON_WINDOW
            commandLine = "Usage: ipy [options] [file.py|- [arguments]]";
#else
            commandLine = "Usage: ipyw [options] [file.py|- [arguments]]";
#endif

            string[,] pythonOptions = new string[,] {
#if !IRONPYTHON_WINDOW
                { "-v",                     "Verbose (trace import statements) (also PYTHONVERBOSE=x)" },
#endif
                { "-m module",              "run library module as a script"},
                { "-x",                     "Skip first line of the source" },
                { "-u",                     "Unbuffered stdout & stderr" },
                { "-O",                     "generate optimized code" },
                { "-OO",                    "remove doc strings and apply -O optimizations" },
                { "-E",                     "Ignore environment variables" },
                { "-S",                     "Don't imply 'import site' on initialization" },
                { "-s",                     "Don't add user site directory to sys.path" },
                { "-t",                     "Issue warnings about inconsistent tab usage" },
                { "-tt",                    "Issue errors for inconsistent tab usage" },
                { "-W arg",                 "Warning control (arg is action:message:category:module:lineno) also IRONPYTHONWARNINGS=arg" },
                { "-q",                     "don't print version and copyright messages on interactive startup" },

                { "-X:NoFrames",            "Disable sys._getframe support, can improve execution speed" },
                { "-X:Frames",              "Enable basic sys._getframe support" },
                { "-X:FullFrames",          "Enable sys._getframe with access to locals" },
                { "-X:Tracing",             "Enable support for tracing all methods even before sys.settrace is called" },
                { "-X:GCStress",            "Specifies the GC stress level (the generation to collect each statement)" },
                { "-X:MaxRecursion",        "Set the maximum recursion level" },
                { "-X:Debug",               "Enable application debugging (preferred over -D)" },
                { "-X:NoDebug <regex>",     "Provides a regular expression of files which should not be emitted in debug mode"},
                { "-X:MTA",                 "Run in multithreaded apartment" },
                { "-X:EnableProfiler",      "Enables profiling support in the compiler" },
                { "-X:LightweightScopes",   "Generate optimized scopes that can be garbage collected" },
                { "-X:BasicConsole",        "Use only the basic console features" },
            };

            // Ensure the combined options come out sorted
            string[,] allOptions = ArrayUtils.Concatenate(pythonOptions, standardOptions);
            List<string> optName = new List<string>();
            List<int> indiciesList = new List<int>();
            for (int i = 0; i < allOptions.Length / 2; i++) {
                optName.Add(allOptions[i, 0]);
                indiciesList.Add(i);
            }
            
            int[] indicies = indiciesList.ToArray();
            Array.Sort(optName.ToArray(), indicies, StringComparer.OrdinalIgnoreCase);

            options = new string[allOptions.Length / 2, 2];
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
