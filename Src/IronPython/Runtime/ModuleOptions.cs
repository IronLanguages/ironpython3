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

using System;
using System.Collections.Generic;
using System.Text;

namespace IronPython.Runtime {
    [Flags]
    public enum ModuleOptions {
        None = 0,
        /// <summary>
        /// Enable true division (1/2 == .5)
        /// </summary>
        TrueDivision = 0x0001,
        /// <summary>
        /// Indicates that .NET methods such as .ToString should be available on Python objects.
        /// </summary>
        ShowClsMethods = 0x0002,
        /// <summary>
        /// Indicates that the module should be generated in an optimal form which will result
        /// in it being uncollectable.
        /// </summary>
        Optimized = 0x0004,
        /// <summary>
        /// Indicates when the module should be executed immedatiately upon creation.
        /// </summary>
        Initialize = 0x0008,
        /// <summary>
        /// Enable usage of the with statement
        /// </summary>
        WithStatement = 0x0010,
        /// <summary>
        /// Enable absolute imports
        /// </summary>
        AbsoluteImports = 0x0020,
        /// <summary>
        /// Indiciates that __builtins__ should not be set in the module
        /// </summary>
        NoBuiltins = 0x0040,
        /// <summary>
        /// Indiciates that when the module is initialized it should set __builtins__ to the __builtin__ module
        /// instead of the __builtin__ dictionary.
        /// </summary>
        ModuleBuiltins = 0x0080,
        /// <summary>
        /// Marks code as being created for exec, eval.  Code generated this way will
        /// be capable of running against different scopes and will do lookups at runtime
        /// for free global variables.
        /// </summary>
        ExecOrEvalCode = 0x0100,
        /// <summary>
        /// Indiciates that the first line of code should be skipped.
        /// </summary>
        SkipFirstLine = 0x0200,
        /// <summary>
        /// Enable usage of print as a function for better compatibility with Python 3.0.
        /// </summary>
        PrintFunction = 0x0400,
        /// <summary>
        /// Forces the code to be interpreted rather than compiled
        /// </summary>
        Interpret = 0x1000,
        /// <summary>
        /// String Literals should be parsed as Unicode strings
        /// </summary>
        UnicodeLiterals = 0x2000,
        /// <summary>
        /// Include comments in the parse tree
        /// </summary>
        Verbatim = 0x4000,
        /// <summary>
        /// Generated code should support light exceptions
        /// </summary>
        LightThrow = 0x8000
    }

}
