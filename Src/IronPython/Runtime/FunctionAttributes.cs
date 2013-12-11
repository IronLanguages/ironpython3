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
        /// Set if the function was compiled with future division.
        /// </summary>
        FutureDivision = 0x2000,
        /// <summary>
        /// IronPython specific: Set if the function includes nested exception handling and therefore can alter
        /// sys.exc_info().
        /// </summary>
        CanSetSysExcInfo = 0x4000,
        /// <summary>
        /// IronPython specific: Set if the function includes a try/finally block.
        /// </summary>
        ContainsTryFinally = 0x8000,
    }
}
