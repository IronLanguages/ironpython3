// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Windows.Forms;
using IronPython.Hosting;
using IronPython.Runtime;
using Microsoft.Scripting.Hosting;
using Microsoft.Scripting.Hosting.Providers;
using Microsoft.Scripting.Hosting.Shell;

internal sealed class PythonWindowsConsoleHost : ConsoleHost {

    protected override Type Provider {
        get { return typeof(PythonContext); }
    }

    protected override CommandLine/*!*/ CreateCommandLine() {
        return new PythonCommandLine();
    }

    protected override OptionsParser/*!*/ CreateOptionsParser() {
        return new PythonOptionsParser();
    }

    protected override LanguageSetup/*!*/ CreateLanguageSetup() {
        return Python.CreateLanguageSetup(null);
    }

    protected override string/*!*/ GetHelp() {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine(PythonCommandLine.GetLogoDisplay());
        PrintLanguageHelp(sb);
        sb.AppendLine();

        return sb.ToString();
    }

    protected override void ExecuteInternal() {
        var pc = HostingHelpers.GetLanguageContext(Engine) as PythonContext;
        pc.SetModuleState(typeof(ScriptEngine), Engine);
        base.ExecuteInternal();
    }

    [STAThread]
    private static int Main(string[] args) {
        if (args.Length == 0) {
            new PythonWindowsConsoleHost().PrintHelp();
            return 1;
        }

        return new PythonWindowsConsoleHost().Run(args);
    }

    protected override void PrintHelp() {
        MessageBox.Show(GetHelp(), "IronPython Window Console Help");
    }
}
