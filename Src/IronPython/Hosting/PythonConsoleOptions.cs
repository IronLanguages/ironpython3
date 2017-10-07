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
#if FEATURE_FULL_CONSOLE
using System;
using Microsoft.Scripting.Hosting.Shell; 

namespace IronPython.Hosting {
    [CLSCompliant(true)]
    public /* TODO: sealed */ class PythonConsoleOptions : ConsoleOptions {
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
    }
}
#endif