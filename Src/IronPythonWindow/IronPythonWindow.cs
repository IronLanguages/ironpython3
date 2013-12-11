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
    static int Main(string[] args) {
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