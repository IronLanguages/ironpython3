// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;

using IronPython.Runtime;
using IronPython.Runtime.Binding;

using Microsoft.Scripting.Interpreter;

using MSAst = System.Linq.Expressions;

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

        public override bool CanReduce => true;

        public override MSAst.ExpressionType NodeType => MSAst.ExpressionType.Extension;

        public override Type Type => _binder.Type;

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

        private abstract class ConversionInstruction : Instruction {
            public override int ConsumedStack => 1;

            public override int ProducedStack => 1;
        }

        private class BooleanConversionInstruction : ConversionInstruction {
            public static BooleanConversionInstruction Instance = new BooleanConversionInstruction();

            public override int Run(InterpretedFrame frame) {
                frame.Push(Converter.ConvertToBoolean(frame.Pop()));
                return +1;
            }
        }

        private class TypedConversionInstruction : ConversionInstruction {
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
