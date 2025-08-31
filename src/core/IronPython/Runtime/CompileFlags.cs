// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;

namespace IronPython.Runtime {
    [Flags]
    public enum CompileFlags {
        CO_NESTED = 0x0010,                     // nested_scopes
        CO_DONT_IMPLY_DEDENT = 0x0200,          // report errors if statement isn't dedented
        CO_GENERATOR_ALLOWED = 0x1000,          // generators
        CO_FUTURE_DIVISION = 0x2000,            // division
        CO_FUTURE_ABSOLUTE_IMPORT = 0x4000,     // perform absolute imports by default
        CO_FUTURE_WITH_STATEMENT = 0x8000,      // with statement
        CO_FUTURE_PRINT_FUNCTION = 0x10000,     // print function
        CO_FUTURE_UNICODE_LITERALS = 0x20000,   // default unicode literals
        CO_FUTURE_BARRY_AS_BDFL = 0x40000,      //
        CO_FUTURE_GENERATOR_STOP = 0x80000,     // StopIteration becomes RuntimeError in generators
    }
}
