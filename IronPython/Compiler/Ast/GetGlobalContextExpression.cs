// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

using Microsoft.Scripting.Interpreter;

using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {
    internal class GetGlobalContextExpression : MSAst.Expression, IInstructionProvider {
        private readonly MSAst.Expression _parentContext;

        public GetGlobalContextExpression(MSAst.Expression parentContext) {
            _parentContext = parentContext;
        }

        public override bool CanReduce => true;

        public override MSAst.ExpressionType NodeType => MSAst.ExpressionType.Extension;

        public override Type Type => typeof(CodeContext);

        public override MSAst.Expression Reduce()
            => Expression.Call(AstMethods.GetGlobalContext, _parentContext);

        #region IInstructionProvider Members

        public void AddInstructions(LightCompiler compiler) {
            compiler.Compile(_parentContext);
            compiler.Instructions.Emit(GetGlobalContextInstruction.Instance);
        }

        #endregion

        private class GetGlobalContextInstruction : Instruction {
            public static readonly GetGlobalContextInstruction Instance = new GetGlobalContextInstruction();

            public override int ConsumedStack => 1;

            public override int ProducedStack => 1;

            public override int Run(InterpretedFrame frame) {
                frame.Push(PythonOps.GetGlobalContext((CodeContext)frame.Pop()));
                return +1;
            }
        }
    }
}
