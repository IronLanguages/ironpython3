// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;

using IronPython.Runtime;
using Microsoft.Scripting.Interpreter;
using IronPython.Runtime.Operations;

using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {
    class GetGlobalContextExpression : MSAst.Expression, IInstructionProvider {
        private readonly MSAst.Expression _parentContext;

        public GetGlobalContextExpression(MSAst.Expression parentContext) {
            _parentContext = parentContext;
        }

        public override bool CanReduce {
            get {
                return true;
            }
        }

        public override MSAst.ExpressionType NodeType {
            get {
                return MSAst.ExpressionType.Extension;
            }
        }

        public override Type Type {
            get {
                return typeof(CodeContext);
            }
        }

        public override MSAst.Expression Reduce() {
            return Expression.Call(AstMethods.GetGlobalContext, _parentContext);
        }

        #region IInstructionProvider Members

        public void AddInstructions(LightCompiler compiler) {
            compiler.Compile(_parentContext);
            compiler.Instructions.Emit(GetGlobalContextInstruction.Instance);
        }

        #endregion

        class GetGlobalContextInstruction : Instruction {
            public static readonly GetGlobalContextInstruction Instance = new GetGlobalContextInstruction();

            public override int ConsumedStack {
                get {
                    return 1;
                }
            }

            public override int ProducedStack {
                get {
                    return 1;
                }
            }

            public override int Run(InterpretedFrame frame) {
                frame.Push(PythonOps.GetGlobalContext((CodeContext)frame.Pop()));
                return +1;
            }
        }
    }
}
