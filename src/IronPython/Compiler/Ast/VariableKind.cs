// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

namespace IronPython.Compiler.Ast {

    /// <summary>
    /// Represents different kinds of a Python variable depending on how the variable was defined or declared.
    /// </summary>
    public enum VariableKind {

        /// <summary>
        /// Local variable.
        ///
        /// Local variables can be referenced from nested lambdas.
        /// </summary>
        Local,

        /// <summary>
        /// Parameter to a LambdaExpression.
        ///
        /// Like locals, they can be referenced from nested lambdas.
        /// </summary>
        Parameter,

        /// <summary>
        /// Global variable.
        ///
        /// Should only appear in global (top level) lambda.
        /// </summary>
        Global,

        /// <summary>
        /// Nonlocal variable.
        ///
        /// Provides a by-reference access to a local variable in an outer scope.
        /// </summary>
        Nonlocal,

        /// <summary>
        /// Attrribute variable.
        ///
        /// Like a local variable, but is stored directly in the context dictionary,
        /// rather than a closure cell.
        /// Should only appear in a class lambda.
        /// </summary>
        Attribute
    }
}
