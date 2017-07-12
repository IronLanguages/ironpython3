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

using Microsoft.Scripting;
using Microsoft.Scripting.Interpreter;
using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Binding;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using MSAst = System.Linq.Expressions;

using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class ConstantExpression : Expression, IInstructionProvider {
        private readonly object _value;

        public ConstantExpression(object value) {
            _value = value;
        }

        public object Value {
            get {
                return _value; 
            }
        }

        private static readonly MSAst.Expression EllipsisExpr = Ast.Property(null, typeof(PythonOps).GetProperty(nameof(PythonOps.Ellipsis)));
        private static readonly MSAst.Expression TrueExpr = Ast.Field(null, typeof(ScriptingRuntimeHelpers).GetField(nameof(ScriptingRuntimeHelpers.True)));
        private static readonly MSAst.Expression FalseExpr = Ast.Field(null, typeof(ScriptingRuntimeHelpers).GetField(nameof(ScriptingRuntimeHelpers.False)));

        public override MSAst.Expression Reduce() {
            if (_value == Ellipsis.Value) {
                return EllipsisExpr;
            } else if (_value is bool) {
                return (bool)_value ? TrueExpr : FalseExpr;
            }

            return GlobalParent.Constant(_value);
        }

        internal override ConstantExpression ConstantFold() {
            return this;
        }

        public override Type Type {
            get {
                if (_value is bool) return typeof(object);
                return GlobalParent.CompilationMode.GetConstantType(Value);
            }
        }

        internal override string CheckAssign() {
            if (_value == null || _value is bool) {
                return "can't assign to keyword";
            }

            return "can't assign to literal";
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
            }
            walker.PostWalk(this);
        }

        public override string NodeName {
            get {
                return "literal";
            }
        }

        internal override bool CanThrow {
            get {
                return false;
            }
        }

        internal override object GetConstantValue() {
            return Value;
        }

        internal override bool IsConstant {
            get {
                return true;
            }
        }

        #region IInstructionProvider Members

        void IInstructionProvider.AddInstructions(LightCompiler compiler) {
            if (_value is bool) {
                compiler.Instructions.EmitLoad((bool)_value);
            } else {
                compiler.Instructions.EmitLoad(_value);
            }
        }

        #endregion
    }
}
