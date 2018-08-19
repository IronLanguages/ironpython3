// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

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
                return typeof(object);
            }
        }

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

        class GetMemberInstruction : Instruction {
            private CallSite<Func<CallSite, object, CodeContext, object>> _site;
            private readonly PythonGetMemberBinder _binder;

            public GetMemberInstruction(PythonGetMemberBinder binder) {
                _binder = binder;
            }

            public override int ConsumedStack {
                get {
                    return 2;
                }
            }

            public override int ProducedStack {
                get {
                    return 1;
                }
            }

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
