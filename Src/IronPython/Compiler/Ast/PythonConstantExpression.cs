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

#if FEATURE_CORE_DLR
using MSAst = System.Linq.Expressions;
#else
using MSAst = Microsoft.Scripting.Ast;
#endif

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
