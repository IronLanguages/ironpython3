// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;

using IronPython.Runtime;
using IronPython.Runtime.Binding;

using Microsoft.Scripting.Interpreter;
using Microsoft.Scripting.Utils;

using MSAst = System.Linq.Expressions;
namespace IronPython.Compiler.Ast {
    internal class DynamicGetMemberExpression : MSAst.Expression, IInstructionProvider {
        private readonly PythonGetMemberBinder/*!*/ _binder;
        private readonly CompilationMode/*!*/ _mode;
        private readonly MSAst.Expression/*!*/ _target, _codeContext;

        public DynamicGetMemberExpression(PythonGetMemberBinder/*!*/ binder, CompilationMode/*!*/ mode, MSAst.Expression/*!*/ target, MSAst.Expression codeContext) {
            Assert.NotNull(binder, mode, target, codeContext);

            _binder = binder;
            _mode = mode;
            _target = target;
            _codeContext = codeContext;
        }

        public override bool CanReduce => true;

        public override MSAst.ExpressionType NodeType => MSAst.ExpressionType.Extension;

        public override Type Type => typeof(object);

        public override MSAst.Expression Reduce() {
            return _mode.ReduceDynamic(
                _binder,
                typeof(object),
                _target,
                _codeContext
            );
        }

        #region IInstructionProvider Members

        public void AddInstructions(LightCompiler compiler) {
            compiler.Compile(_target);
            compiler.Compile(_codeContext);
            compiler.Instructions.Emit(new GetMemberInstruction(_binder));
        }

        #endregion

        private class GetMemberInstruction : Instruction {
            private CallSite<Func<CallSite, object, CodeContext, object>> _site;
            private readonly PythonGetMemberBinder _binder;

            public GetMemberInstruction(PythonGetMemberBinder binder) {
                _binder = binder;
            }

            public override int ConsumedStack => 2;

            public override int ProducedStack => 1;

            public override int Run(InterpretedFrame frame) {
                if (_site == null) {
                    _site = CallSite<Func<CallSite, object, CodeContext, object>>.Create(_binder);
                }

                var context = (CodeContext)frame.Pop();
                frame.Push(_site.Target(_site, frame.Pop(), context));
                return +1;
            }
        }
    }

}
