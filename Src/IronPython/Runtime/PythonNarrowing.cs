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

using Microsoft.Scripting.Actions.Calls;

namespace IronPython.Runtime {
    /// <summary>
    /// Provides human readable names for how Python maps the various DLR NarrowingLevel's.
    /// </summary>
    static class PythonNarrowing {
        /// <summary>
        /// No narrowing conversions are performed
        /// </summary>
        public const NarrowingLevel None = NarrowingLevel.None;

        /// <summary>
        /// Double/Single to Decimal
        /// PythonTuple to Array
        /// Generic conversions
        /// BigInteger to Int64
        /// </summary>
        public const NarrowingLevel BinaryOperator = NarrowingLevel.Two;

        /// <summary>
        /// Numeric conversions excluding from floating point values
        /// Boolean conversions
        /// Delegate conversions
        /// Enumeration conversions
        /// </summary>
        public const NarrowingLevel IndexOperator = NarrowingLevel.Three;

        /// <summary>
        /// Enables Python protocol conversions (__int__, etc...)
        /// </summary>
        public const NarrowingLevel All = NarrowingLevel.All;
    }
}
