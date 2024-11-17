// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting.Interpreter;
using Microsoft.Scripting.Runtime;

using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class ConstantExpression : Expression, IInstructionProvider {
        public ConstantExpression(object value) {
            Value = value;
        }

        public object Value { get; }

        private static readonly MSAst.Expression EllipsisExpr = Ast.Property(null, typeof(PythonOps).GetProperty(nameof(PythonOps.Ellipsis)));
        private static readonly MSAst.Expression TrueExpr = Ast.Field(null, typeof(ScriptingRuntimeHelpers).GetField(nameof(ScriptingRuntimeHelpers.True)));
        private static readonly MSAst.Expression FalseExpr = Ast.Field(null, typeof(ScriptingRuntimeHelpers).GetField(nameof(ScriptingRuntimeHelpers.False)));

        public override MSAst.Expression Reduce() {
            if (Value == Ellipsis.Value) {
                return EllipsisExpr;
            } else if (Value is bool) {
                return (bool)Value ? TrueExpr : FalseExpr;
            }

            return GlobalParent.Constant(Value);
        }

        internal override ConstantExpression ConstantFold() => this;

        public override Type Type {
            get {
                if (Value is bool) return typeof(object);
                return GlobalParent.CompilationMode.GetConstantType(Value);
            }
        }

        internal override string CheckAssign() {
            if (Value == null || Value is bool) {
                return "can't assign to keyword";
            }

            return base.CheckAssign();
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
            }
            walker.PostWalk(this);
        }

        public override string NodeName => "literal";

        internal override bool CanThrow => false;

        internal override object GetConstantValue() => Value;

        internal override bool IsConstant => true;

        #region IInstructionProvider Members

        void IInstructionProvider.AddInstructions(LightCompiler compiler) {
            if (Value is bool) {
                compiler.Instructions.EmitLoad((bool)Value);
            } else {
                compiler.Instructions.EmitLoad(Value);
            }
        }

        #endregion
    }
}
