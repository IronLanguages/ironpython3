// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;

namespace IronPython.Runtime {
    [Flags]
    public enum FunctionAttributes {
        None = 0,
        /// <summary>
        /// Set if the function includes a *args argument list.
        /// </summary>
        ArgumentList = 0x04,
        /// <summary>
        /// Set if the function includes a **kwargs argument dictionary.
        /// </summary>
        KeywordDictionary = 0x08,
        /// <summary>
        /// Set if the function is a generator.
        /// </summary>
        Generator = 0x20,
        /// <summary>
        /// IronPython specific: Set if the function includes nested exception handling and therefore can alter
        /// sys.exc_info().
        /// </summary>
        CanSetSysExcInfo = 0x4000,
        /// <summary>
        /// IronPython specific: Set if the function includes a try/finally block.
        /// </summary>
        ContainsTryFinally = 0x8000,
        GeneratorStop = 0x80000, // TODO: delete me in 3.7
    }
}
