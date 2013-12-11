/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;

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
        ScriptRuntimeSetup srs = ScriptRuntimeSetup.ReadConfiguration();
        foreach (var langSetup in srs.LanguageSetups) {
            if (langSetup.FileExtensions.Contains(".py")) {
                langSetup.Options["SearchPaths"] = new string[0];
            }
        }
        return srs;
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

    [STAThread]
    public static int Main(string[] args) {
        // Work around issue w/ pydoc - piping to more doesn't work so
        // instead indicate that we're a dumb terminal
        if (Environment.GetEnvironmentVariable("TERM") == null) {
            Environment.SetEnvironmentVariable("TERM", "dumb");
        }

        return new PythonConsoleHost().Run(args);
    }
}