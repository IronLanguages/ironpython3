// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

namespace IronPython.Compiler.Ast {

    internal enum VariableKind {

        /// <summary>
        /// Local variable.
        /// 
        /// Local variables can be referenced from nested lambdas
        /// </summary>
        Local,

        /// <summary>
        /// Parameter to a LambdaExpression
        /// 
        /// Like locals, they can be referenced from nested lambdas
        /// </summary>
        Parameter,

        /// <summary>
        /// Global variable
        /// 
        /// Should only appear in global (top level) lambda.
        /// </summary>
        Global
    }
}