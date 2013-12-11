/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * ironruby@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

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