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
    class PythonConstantExpression : MSAst.Expression, IInstructionProvider {
        private readonly CompilationMode _mode;
        private readonly object _value;

        public PythonConstantExpression(CompilationMode mode, object value) {
            _mode = mode;
            _value = value;
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
                return _mode.GetConstantType(_value);
            }
        }

        public override MSAst.Expression Reduce() {
            return _mode.GetConstant(_value);
        }

        public object Value {
            get {
                return _value;
            }
        }

        public CompilationMode Mode {
            get {
                return _mode;
            }
        }

        #region IInstructionProvider Members

        public void AddInstructions(LightCompiler compiler) {
            compiler.Instructions.EmitLoad(_value);
        }

        #endregion
    }
}
