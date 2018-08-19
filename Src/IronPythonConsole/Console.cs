// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

using Microsoft.Scripting.Hosting;
using Microsoft.Scripting.Hosting.Providers;
using Microsoft.Scripting.Hosting.Shell;

using IronPython.Hosting;
using IronPython.Runtime;

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
                langSetup.Options["SearchPaths"] = new string[0];
            }
        }
        return srs;
    }

    protected override LanguageSetup CreateLanguageSetup() {
        return Python.CreateLanguageSetup(null);
    }

    protected override IConsole CreateConsole(ScriptEngine engine, CommandLine commandLine, ConsoleOptions options) {
        PythonConsoleOptions pyoptions = (PythonConsoleOptions)options;
        return pyoptions.BasicConsole ? new BasicConsole(options.ColorfulConsole) : new SuperConsole(commandLine, options.ColorfulConsole);
    }

    protected override void ParseHostOptions(string/*!*/[]/*!*/ args) {
        // Python doesn't want any of the DLR base options.
        foreach (string s in args) {
            Options.IgnoredArgs.Add(s);
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