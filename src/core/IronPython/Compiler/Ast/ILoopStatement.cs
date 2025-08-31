// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {
    internal interface ILoopStatement {
        MSAst.LabelTarget BreakLabel { get; set; }
        MSAst.LabelTarget ContinueLabel { get; set; }
    }
}
