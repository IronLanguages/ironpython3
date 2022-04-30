// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.
#if FEATURE_FULL_CONSOLE
using System;
using Microsoft.Scripting.Hosting.Shell; 

namespace IronPython.Hosting {
    [CLSCompliant(true)]
    public sealed class PythonConsoleOptions : ConsoleOptions {
        public bool IgnoreEnvironmentVariables { get; set; }

        public bool SkipImportSite { get; set; }

        public string ModuleToRun { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to skip the first line of the code to execute.
        /// This is useful for executing Unix scripts which have the command to execute specified in the first line.
        /// This only apply to the script code executed by the ScriptEngine APIs, but not for other script code 
        /// that happens to get called as a result of the execution.
        /// </summary>
        public bool SkipFirstSourceLine { get; set; }

        public bool BasicConsole { get; set; }

        public bool PrintSysVersion { get; set; }
    }
}
#endif
