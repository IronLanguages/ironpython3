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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Runtime.CompilerServices;

using Microsoft.Scripting;
using Microsoft.Scripting.Interpreter;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {
    class GetParentContextFromFunctionExpression : MSAst.Expression, IInstructionProvider {
        private static MSAst.Expression _parentContext = MSAst.Expression.Call(AstMethods.GetParentContextFromFunction, FunctionDefinition._functionParam);

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
            return _parentContext;
        }

        #region IInstructionProvider Members

        public void AddInstructions(LightCompiler compiler) {
            compiler.Compile(FunctionDefinition._functionParam);
            compiler.Instructions.Emit(GetParentContextFromFunctionInstruction.Instance);
        }

        #endregion

        class GetParentContextFromFunctionInstruction : Instruction {
            public static readonly GetParentContextFromFunctionInstruction Instance = new GetParentContextFromFunctionInstruction();

            public override int ProducedStack {
                get {
                    return 1;
                }
            }

            public override int ConsumedStack {
                get {
                    return 1;
                }
            }

            public override int Run(InterpretedFrame frame) {
                frame.Push(PythonOps.GetParentContextFromFunction((PythonFunction)frame.Pop()));
                return +1;
            }
        }
    }
}
