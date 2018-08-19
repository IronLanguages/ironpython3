// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;

namespace IronPython.Runtime.Types {
    /// <summary>
    /// Represents a set of attributes that different functions can have.
    /// </summary>
    [Flags]
    public enum FunctionType {
        /// <summary>No flags have been set </summary>
        None = 0x0000,
        /// <summary>This is a function w/ no instance pointer </summary>
        Function = 0x0001,
        /// <summary>This is a method that requires an instance</summary>
        Method = 0x0002,
        /// <summary>Built-in functions can encapsulate both methods and functions, in which case both bits are set</summary>
        FunctionMethodMask = 0x0003,
        /// <summary>True is the function/method should be visible from pure-Python code</summary>
        AlwaysVisible = 0x0004,
        /// <summary>True if this is a __r*__ method for a CLS overloaded operator method</summary>
        ReversedOperator = 0x0020,
        /// <summary>
        /// This method represents a binary operator method for a CLS overloaded operator method.
        /// 
        /// Being a binary operator causes the following special behaviors to kick in:
        ///     A failed binding at call time returns NotImplemented instead of raising an exception
        ///     A reversed operator will automatically be created if:
        ///         1. The parameters are both of the instance type
        ///         2. The parameters are in reversed order (other, this)
        ///         
        /// This enables simple .NET operator methods to be mapped into the Python semantics.
        /// </summary>
        BinaryOperator = 0x0040,

        /// <summary>
        /// A method declared on a built-in module
        /// </summary>
        ModuleMethod = 0x0080
    }
}
