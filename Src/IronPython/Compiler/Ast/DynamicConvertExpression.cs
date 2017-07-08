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
using System.Dynamic;
using System.IO;
using System.Runtime.CompilerServices;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Interpreter;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Binding;
using IronPython.Runtime.Operations;

using MSAst = System.Linq.Expressions;

using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {
    internal class DynamicConvertExpression : MSAst.Expression, IInstructionProvider {
        private readonly PythonConversionBinder _binder;
        private readonly CompilationMode _mode;
        private readonly MSAst.Expression _target;

        public DynamicConvertExpression(PythonConversionBinder binder, CompilationMode mode, MSAst.Expression target) {
            _binder = binder;
            _mode = mode;
            _target = target;
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
                return _binder.Type;
            }
        }

        public override MSAst.Expression Reduce() {
            return _mode.ReduceDynamic(
                _binder,
                _binder.Type,
                _target
            );
        }

        #region IInstructionProvider Members

        public void AddInstructions(LightCompiler compiler) {
            compiler.Compile(_target);
            switch (Type.GetTypeCode(_binder.Type)) {
                case TypeCode.Boolean:
                    compiler.Instructions.Emit(BooleanConversionInstruction.Instance);
                    break;
                default:
                    compiler.Instructions.Emit(new TypedConversionInstruction(_binder.Type));
                    break;
            }
        }

        #endregion

        abstract class ConversionInstruction : Instruction {
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
        }

        class BooleanConversionInstruction : ConversionInstruction {
            public static BooleanConversionInstruction Instance = new BooleanConversionInstruction();

            public override int Run(InterpretedFrame frame) {
                frame.Push(Converter.ConvertToBoolean(frame.Pop()));
                return +1;
            }
        }

        class TypedConversionInstruction : ConversionInstruction {
            private readonly Type _type;

            public TypedConversionInstruction(Type type) {
                _type = type;
            }

            public override int Run(InterpretedFrame frame) {
                frame.Push(Converter.Convert(frame.Pop(), _type));
                return +1;
            }
        }
    }

}
