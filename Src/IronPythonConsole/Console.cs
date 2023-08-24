// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;

using IronPython.Hosting;
using IronPython.Runtime;

using Microsoft.Scripting.Hosting;
using Microsoft.Scripting.Hosting.Providers;
using Microsoft.Scripting.Hosting.Shell;
using Microsoft.Scripting.Utils;

internal sealed class PythonConsoleHost : ConsoleHost {

    protected override Type Provider {
        get { return typeof(PythonContext); }
    }

    protected override CommandLine/*!*/ CreateCommandLine() {
        return new PythonCommandLine();
    }

    protected override OptionsParser/*!*/ CreateOptionsParser() {
        return new PythonOptionsParser();
    }

    protected override ScriptRuntimeSetup CreateRuntimeSetup() {
        ScriptRuntimeSetup srs = base.CreateRuntimeSetup();
        foreach (var langSetup in srs.LanguageSetups) {
            if (langSetup.FileExtensions.Contains(".py")) {
                langSetup.Options["SearchPaths"] = Array.Empty<string>();
            }
        }
        return srs;
    }

    protected override LanguageSetup CreateLanguageSetup() {
        return Python.CreateLanguageSetup(null);
    }

    private PythonConsoleOptions _pyoptions;

    protected override IConsole CreateConsole(ScriptEngine engine, CommandLine commandLine, ConsoleOptions options) {
        PythonConsoleOptions pyoptions = (PythonConsoleOptions)options;
        return pyoptions.BasicConsole ? new BasicConsole(options) : new SuperConsole(commandLine, options);
    }

    protected override ConsoleOptions ParseOptions(string[] args, ScriptRuntimeSetup runtimeSetup, LanguageSetup languageSetup) {
        var options = base.ParseOptions(args, runtimeSetup, languageSetup);
        _pyoptions = (PythonConsoleOptions)options;
        return options;
    }

    protected override void ParseHostOptions(string/*!*/[]/*!*/ args) {
        // Python doesn't want any of the DLR base options.
        foreach (string s in args) {
            Options.IgnoredArgs.Add(s);
        }
    }

    protected override void PrintVersion() {
        if (_pyoptions?.PrintSysVersion == true) {
            Console.WriteLine(GetVersionString(Engine.Setup.DisplayName));
        } else {
            Console.WriteLine(Engine.Setup.DisplayName);
        }

        // replicates PythonContext.GetVersionString
        static string GetVersionString(string displayName) {
            string configuration = ClrModule.IsDebug ? " DEBUG" : string.Empty;
            string bitness = (IntPtr.Size * 8).ToString();

            return $"{displayName}{configuration} ({typeof(ClrModule).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version})\n" +
                $"[{ClrModule.TargetFramework} on {ClrModule.FrameworkDescription} ({bitness}-bit)]";
        }
    }

    public override void PrintLanguageHelp(StringBuilder output) {
        new PythonOptionsParser().GetHelp(out string commandLine, out string[,] options, out string[,] environmentVariables, out string comments);

        // only display language specific options if one or more options exist
        if (commandLine != null || options != null || environmentVariables != null || comments != null) {
            var appendLine = false;

            if (commandLine != null) {
                appendLine = true;
                output.AppendLine(commandLine);
            }

            if (options != null) {
                if (appendLine) output.AppendLine();
                appendLine = true;

                // reformat the implementation-specific options
                var newOptions = new List<KeyValuePair<string, string>>();
                bool first = true;
                for (int i = 0; i < options.GetLength(0); i++) {
                    var pair = new KeyValuePair<string, string>(options[i, 0], ": " + options[i, 1]);
                    if (pair.Key.StartsWith("-X", StringComparison.Ordinal)) {
                        if (first) {
                            first = false;
                            newOptions.Add(new KeyValuePair<string, string>("-X opt", ": set implementation-specific option. The following options are available:"));
                            newOptions.Add(new KeyValuePair<string, string>(string.Empty, string.Empty));
                        }
                        newOptions.Add(new KeyValuePair<string, string>(string.Empty, pair.Key + pair.Value));
                    } else {
                        newOptions.Add(pair);
                    }
                }
                options = new string[newOptions.Count, 2];
                for (int i = 0; i < newOptions.Count; i++) {
                    options[i, 0] = newOptions[i].Key;
                    options[i, 1] = newOptions[i].Value;
                }

                output.AppendLine("Options:");
                PrintTable(output, options);
            }

            if (environmentVariables != null) {
                if (appendLine) output.AppendLine();
                appendLine = true;
                output.AppendLine("Environment variables:");
                ArrayUtils.PrintTable(output, environmentVariables);
            }

            if (comments != null) {
                if (appendLine) output.AppendLine();
                output.AppendLine(comments);
            }
        }

        static void PrintTable(StringBuilder output, string[,] table) {
            int max_width = 0;
            for (int i = 0; i < table.GetLength(0); i++) {
                if (table[i, 0].Length > max_width) {
                    max_width = table[i, 0].Length;
                }
            }

            for (int i = 0; i < table.GetLength(0); i++) {
                output.Append(" ");
                output.Append(table[i, 0]);
                output.Append(' ', max_width - table[i, 0].Length + 1);
                var lines = table[i, 1].Split('\n');
                output.AppendLine(lines[0]);

                for (var j = 1; j < lines.Length; j++) {
                    output.Append(' ', max_width + 4);
                    output.AppendLine(lines[j]);
                }
            }
        }
    }

    protected override void ExecuteInternal() {
        var pc = HostingHelpers.GetLanguageContext(Engine) as PythonContext;
        pc.SetModuleState(typeof(ScriptEngine), Engine);
        base.ExecuteInternal();
    }

#if DEBUG
    private static string[] MaybeAttachDebugger(string[] args) {
        int attachDebugger = Array.IndexOf(args, "-X:Attach");
        if (attachDebugger != -1) {
            // Remove -X:Attach from the arg list, since after this point it's no use
            string[] newArgs = new string[args.Length - 1];
            Array.Copy(args, newArgs, attachDebugger);
            Array.Copy(args, attachDebugger + 1, newArgs, attachDebugger, newArgs.Length - attachDebugger);
            args = newArgs;

            // Launch a debugger. This seems to be more reliable than
            // Debugger.Break().
            if (Debugger.IsAttached == false) Debugger.Launch();
        }

        return args;
    }
#endif

    [STAThread]
    public static int Main(string[] args) {
        // Work around issue w/ pydoc - piping to more doesn't work so
        // instead indicate that we're a dumb terminal
        if (Environment.GetEnvironmentVariable("TERM") == null) {
            Environment.SetEnvironmentVariable("TERM", "dumb");
        }

#if DEBUG
        args = MaybeAttachDebugger(args);
#endif

        return new PythonConsoleHost().Run(args);
    }
}
