// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.Scripting.Interpreter;

using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {
    internal class PythonConstantExpression : MSAst.Expression, IInstructionProvider {
        public PythonConstantExpression(CompilationMode mode, object value) {
            Mode = mode;
            Value = value;
        }

        public override bool CanReduce => true;

        public override MSAst.ExpressionType NodeType => MSAst.ExpressionType.Extension;

        public override Type Type => Mode.GetConstantType(Value);

        public override MSAst.Expression Reduce() => Mode.GetConstant(Value);

        public object Value { get; }

        public CompilationMode Mode { get; }

        #region IInstructionProvider Members

        public void AddInstructions(LightCompiler compiler) {
            compiler.Instructions.EmitLoad(Value);
        }

        #endregion
    }
}
