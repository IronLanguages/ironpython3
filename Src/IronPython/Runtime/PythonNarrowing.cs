// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Microsoft.Scripting.Actions.Calls;

namespace IronPython.Runtime {
    /// <summary>
    /// Provides human readable names for how Python maps the various DLR NarrowingLevel's.
    /// </summary>
    internal static class PythonNarrowing {
        /// <summary>
        /// No narrowing conversions are performed
        /// </summary>
        public const NarrowingLevel None = NarrowingLevel.None;

        /// <summary>
        /// Underlying integer to enum
        /// Numeric (except Complex) to floating point (except Simple)
        /// </summary>
        public const NarrowingLevel Minimal = NarrowingLevel.One;

        /// <summary>
        /// PythonTuple to Array
        /// Generic conversions
        /// Numeric (except Complex) to Simple
        /// </summary>
        public const NarrowingLevel BinaryOperator = NarrowingLevel.Two;

        /// <summary>
        /// Numeric conversions excluding from floating point values
        /// Boolean conversions
        /// Delegate conversions
        /// Enumeration conversions
        /// Implicit conversion operators
        /// </summary>
        public const NarrowingLevel IndexOperator = NarrowingLevel.Three;

        /// <summary>
        /// Enables Python protocol conversions (__int__, etc...)
        /// </summary>
        public const NarrowingLevel All = NarrowingLevel.All;
    }
}
