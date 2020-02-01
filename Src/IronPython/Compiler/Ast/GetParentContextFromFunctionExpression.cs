// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.Scripting.Interpreter;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

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


    internal class GetGrandParentContextFromFunctionExpression : MSAst.Expression, IInstructionProvider {
        private static readonly MSAst.Expression _gparentContext = MSAst.Expression.Call(AstMethods.GetGrandParentContextFromFunction, FunctionDefinition._functionParam);

        public override bool CanReduce => true;

        public override MSAst.ExpressionType NodeType => MSAst.ExpressionType.Extension;

        public override Type Type => typeof(CodeContext);

        public override MSAst.Expression Reduce() => _gparentContext;

        #region IInstructionProvider Members

        public void AddInstructions(LightCompiler compiler) {
            compiler.Compile(FunctionDefinition._functionParam);
            compiler.Instructions.Emit(GetGrandParentContextFromFunctionInstruction.Instance);
        }

        #endregion

        private class GetGrandParentContextFromFunctionInstruction : Instruction {
            public static readonly GetGrandParentContextFromFunctionInstruction Instance = new GetGrandParentContextFromFunctionInstruction();

            public override int ProducedStack => 1;

            public override int ConsumedStack => 1;

            public override int Run(InterpretedFrame frame) {
                frame.Push(PythonOps.GetGrandParentContextFromFunction((PythonFunction)frame.Pop()));
                return +1;
            }
        }
    }
}
