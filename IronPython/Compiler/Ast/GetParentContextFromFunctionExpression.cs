// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

using Microsoft.Scripting.Interpreter;

using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {
    internal class GetParentContextFromFunctionExpression : MSAst.Expression, IInstructionProvider {
        private static readonly MSAst.Expression _parentContext = MSAst.Expression.Call(AstMethods.GetParentContextFromFunction, FunctionDefinition._functionParam);

        public override bool CanReduce => true;

        public override MSAst.ExpressionType NodeType => MSAst.ExpressionType.Extension;

        public override Type Type => typeof(CodeContext);

        public override MSAst.Expression Reduce() => _parentContext;

        #region IInstructionProvider Members

        public void AddInstructions(LightCompiler compiler) {
            compiler.Compile(FunctionDefinition._functionParam);
            compiler.Instructions.Emit(GetParentContextFromFunctionInstruction.Instance);
        }

        #endregion

        private class GetParentContextFromFunctionInstruction : Instruction {
            public static readonly GetParentContextFromFunctionInstruction Instance = new GetParentContextFromFunctionInstruction();

            public override int ProducedStack => 1;

            public override int ConsumedStack => 1;

            public override int Run(InterpretedFrame frame) {
                frame.Push(PythonOps.GetParentContextFromFunction((PythonFunction)frame.Pop()));
                return +1;
            }
        }
    }
}
