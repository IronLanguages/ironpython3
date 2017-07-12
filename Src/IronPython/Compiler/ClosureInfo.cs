﻿/* ****************************************************************************
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

using Microsoft.Scripting;

using IronPython.Compiler.Ast;

using MSAst = System.Linq.Expressions;

using Microsoft.Scripting.Utils;

namespace IronPython.Compiler {
    /// <summary>
    /// Tracking for variables lifted into closure objects. Used to store information in a function
    /// about the outer variables it accesses.
    /// </summary>
    class ReferenceClosureInfo {
        public readonly PythonVariable/*!*/ Variable;
        public bool IsClosedOver;

        public ReferenceClosureInfo(PythonVariable/*!*/ variable, int index, MSAst.Expression/*!*/ tupleExpr, bool accessedInThisScope) {
            Assert.NotNull(variable);

            Variable = variable;
            IsClosedOver = accessedInThisScope;
        }

        public PythonVariable/*!*/ PythonVariable {
            get {
                return Variable;
            }
        }
    }
}
