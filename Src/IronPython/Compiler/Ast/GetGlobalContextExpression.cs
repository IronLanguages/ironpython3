/* ****************************************************************************
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
using System.Collections.Generic;
using System.Text;

using IronPython.Runtime;
using Microsoft.Scripting.Interpreter;
using IronPython.Runtime.Operations;

#if FEATURE_CORE_DLR
using MSAst = System.Linq.Expressions;
#else
using MSAst = Microsoft.Scripting.Ast;
#endif

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
