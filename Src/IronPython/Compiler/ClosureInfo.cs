// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

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
    internal class ReferenceClosureInfo {
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
