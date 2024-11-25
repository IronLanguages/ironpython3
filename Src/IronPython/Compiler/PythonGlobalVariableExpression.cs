// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Linq.Expressions;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

using Microsoft.Scripting.Ast;
using Microsoft.Scripting.Interpreter;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace IronPython.Compiler {
    internal interface IPythonVariableExpression {
        Expression/*!*/ Assign(Expression/*!*/ value);
        Expression/*!*/ Delete();
        Expression/*!*/ Create();
    }

    internal interface IPythonGlobalExpression : IPythonVariableExpression {
        Expression/*!*/ RawValue();
    }

    /// <summary>
    /// Small reducable node which just fetches the value from a PythonGlobal
    /// object.  The compiler recognizes these on sets and turns them into
    /// assignments on the python global object.
    /// </summary>
    internal class PythonGlobalVariableExpression : Expression, IInstructionProvider, IPythonGlobalExpression, ILightExceptionAwareExpression {
        private readonly Expression/*!*/ _target;
        private readonly PythonGlobal/*!*/ _global;
        private readonly Ast.PythonVariable/*!*/ _variable;
        private readonly bool _lightEh;
        internal static Expression/*!*/ Uninitialized = Expression.Field(null, typeof(Uninitialized).GetField(nameof(Microsoft.Scripting.Runtime.Uninitialized.Instance)));

        public PythonGlobalVariableExpression(Expression/*!*/ globalExpr, Ast.PythonVariable/*!*/ variable, PythonGlobal/*!*/ global)
            : this(globalExpr, variable, global, false) {
        }

        internal PythonGlobalVariableExpression(Expression/*!*/ globalExpr, Ast.PythonVariable/*!*/ variable, PythonGlobal/*!*/ global, bool lightEh) {
            Assert.NotNull(globalExpr, variable);

            _target = globalExpr;
            _global = global;
            _variable = variable;
            _lightEh = lightEh;
        }

        public Expression/*!*/ Target {
            get {
                return _target;
            }
        }

        public new Ast.PythonVariable/*!*/ Variable {
            get {
                return _variable;
            }
        }

        public PythonGlobal Global {
            get {
                return _global;
            }
        }

        public sealed override ExpressionType NodeType {
            get { return ExpressionType.Extension; }
        }

        public sealed override Type/*!*/ Type {
            get { return typeof(object); }
        }

        public override bool CanReduce {
            get {
                return true;
            }
        }

        public override Expression/*!*/ Reduce() {
            return Expression.Property(
                _target,
                PythonGlobal.CurrentValueProperty
            );
        }

        public Expression/*!*/ RawValue() {
            return new PythonRawGlobalValueExpression(this);
        }

        public Expression/*!*/ Assign(Expression/*!*/ value) {
            return new PythonSetGlobalVariableExpression(this, value);
        }

        public Expression/*!*/ Delete() {
            return new PythonSetGlobalVariableExpression(this, Uninitialized);
        }

        public Expression Create() {
            return null;
        }

        protected override Expression VisitChildren(ExpressionVisitor visitor) {
            Expression v = visitor.Visit(_target);
            if (v == _target) {
                return this;
            }
            return new PythonGlobalVariableExpression(v, _variable, _global, _lightEh);
        }

        #region IInstructionProvider Members

        public void AddInstructions(LightCompiler compiler) {
            if (_lightEh) {
                compiler.Instructions.Emit(new PythonLightThrowGlobalInstruction(_global));
            } else {
                compiler.Instructions.Emit(new PythonGlobalInstruction(_global));
            }
        }

        #endregion

        #region ILightExceptionAwareExpression Members

        Expression ILightExceptionAwareExpression.ReduceForLightExceptions() {
            if (_lightEh) {
                return this;
            }
            return new PythonGlobalVariableExpression(
                _target,
                _variable,
                _global,
                true
            );
        }

        #endregion
    }

    internal class PythonGlobalInstruction : Instruction {
        protected readonly PythonGlobal _global;
        public PythonGlobalInstruction(PythonGlobal global) {
            _global = global;
        }

        public override int ProducedStack { get { return 1; } }
        public override int Run(InterpretedFrame frame) {
            frame.Push(_global.CurrentValue);
            return +1;
        }

        public override string ToString() {
            return "GetGlobal(" + _global + ")";
        }
    }

    internal class PythonLightThrowGlobalInstruction : PythonGlobalInstruction {
        public PythonLightThrowGlobalInstruction(PythonGlobal global) : base(global) {
        }

        public override int Run(InterpretedFrame frame) {
            frame.Push(_global.CurrentValueLightThrow);
            return +1;
        }
    }

    internal class PythonSetGlobalVariableExpression : Expression, IInstructionProvider {
        private readonly PythonGlobalVariableExpression/*!*/ _global;
        private readonly Expression/*!*/ _value;

        public PythonSetGlobalVariableExpression(PythonGlobalVariableExpression/*!*/ global, Expression/*!*/ value) {
            _global = global;
            _value = value;
        }

        public sealed override ExpressionType NodeType {
            get { return ExpressionType.Extension; }
        }

        public sealed override Type/*!*/ Type {
            get { return typeof(object); }
        }

        public Expression Value {
            get {
                return _value;
            }
        }

        public override bool CanReduce {
            get {
                return true;
            }
        }

        public PythonGlobalVariableExpression Global {
            get {
                return _global;
            }
        }

        public override Expression Reduce() {
            return Expression.Assign(
                Expression.Property(
                    _global.Target,
                    typeof(PythonGlobal).GetProperty(nameof(PythonGlobal.CurrentValue))
                ),
                Utils.Convert(_value, typeof(object))
            );
        }


        protected override Expression VisitChildren(ExpressionVisitor visitor) {
            var v = visitor.Visit(_value);
            if (v == _value) {
                return this;
            }
            return new PythonSetGlobalVariableExpression(_global, v);
        }

        #region IInstructionProvider Members

        public void AddInstructions(LightCompiler compiler) {
            compiler.Compile(_value);
            compiler.Instructions.Emit(new PythonSetGlobalInstruction(_global.Global));
        }

        #endregion
    }

    internal class PythonRawGlobalValueExpression : Expression {
        private readonly PythonGlobalVariableExpression/*!*/ _global;

        public PythonRawGlobalValueExpression(PythonGlobalVariableExpression/*!*/ global) {
            _global = global;
        }

        public sealed override ExpressionType NodeType {
            get { return ExpressionType.Extension; }
        }

        public sealed override Type/*!*/ Type {
            get { return typeof(object); }
        }

        public override bool CanReduce {
            get {
                return true;
            }
        }

        public PythonGlobalVariableExpression Global {
            get {
                return _global;
            }
        }

        public override Expression Reduce() {
            return Expression.Property(
                _global.Target,
                PythonGlobal.RawValueProperty
            );
        }


        protected override Expression VisitChildren(ExpressionVisitor visitor) {
            return this;
        }
    }

    internal class PythonSetGlobalInstruction : Instruction {
        private readonly PythonGlobal _global;
        public PythonSetGlobalInstruction(PythonGlobal global) {
            _global = global;
        }

        public override int ProducedStack { get { return 1; } }
        public override int ConsumedStack { get { return 1; } }
        public override int Run(InterpretedFrame frame) {
            _global.CurrentValue = frame.Peek();
            return +1;
        }

        public override string ToString() {
            return "SetGlobal(" + _global + ")";
        }
    }

    internal class LookupGlobalVariable : Expression, IInstructionProvider, IPythonGlobalExpression, ILightExceptionAwareExpression {
        private readonly string/*!*/ _name;
        private readonly bool/*!*/ _isLocal, _lightThrow;
        private readonly Expression/*!*/ _codeContextExpr;

        public LookupGlobalVariable(Expression/*!*/ codeContextExpr, string/*!*/  name, bool isLocal)
            : this(codeContextExpr, name, isLocal, false) {
        }

        public LookupGlobalVariable(Expression/*!*/ codeContextExpr, string/*!*/  name, bool isLocal, bool lightThrow) {
            Debug.Assert(codeContextExpr.Type == typeof(CodeContext));
            Assert.NotNull(name);

            _name = name;
            _isLocal = isLocal;
            _codeContextExpr = codeContextExpr;
            _lightThrow = lightThrow;
        }

        public sealed override ExpressionType NodeType {
            get { return ExpressionType.Extension; }
        }

        public sealed override Type/*!*/ Type {
            get { return typeof(object); }
        }

        public override bool CanReduce {
            get {
                return true;
            }
        }

        protected override Expression VisitChildren(ExpressionVisitor visitor) {
            return this;
        }

        public Expression/*!*/ RawValue() {
            return Expression.Call(
                typeof(PythonOps).GetMethod(_isLocal ? nameof(PythonOps.RawGetLocal) : nameof(PythonOps.RawGetGlobal)),
                _codeContextExpr,
                Utils.Constant(_name)
            );
        }

        public override Expression/*!*/ Reduce() {
            return Expression.Call(
                typeof(PythonOps).GetMethod(_isLocal ? nameof(PythonOps.GetLocal) : nameof(PythonOps.GetGlobal)),
                _codeContextExpr,
                Utils.Constant(_name)
            );
        }

        public Expression/*!*/ Assign(Expression/*!*/ value) {
            return Expression.Call(
                typeof(PythonOps).GetMethod(_isLocal ? nameof(PythonOps.SetLocal) : nameof(PythonOps.SetGlobal)),
                _codeContextExpr,
                Utils.Constant(_name),
                value
            );
        }

        public Expression Create() {
            return null;
        }

        public bool IsLocal {
            get {
                return _isLocal;
            }
        }

        public Expression CodeContext {
            get {
                return _codeContextExpr;
            }
        }

        public string Name {
            get {
                return _name;
            }
        }

        public Expression/*!*/ Delete() {
            return Expression.Call(
                typeof(PythonOps).GetMethod(_isLocal ? nameof(PythonOps.DeleteLocal) : nameof(PythonOps.DeleteGlobal)),
                _codeContextExpr,
                Utils.Constant(_name)
            );
        }

        #region IInstructionProvider Members

        void IInstructionProvider.AddInstructions(LightCompiler compiler) {
            compiler.Compile(_codeContextExpr);
            compiler.Instructions.Emit(new LookupGlobalInstruction(_name, _isLocal, _lightThrow));
        }

        #endregion

        #region ILightExceptionAwareExpression Members

        Expression ILightExceptionAwareExpression.ReduceForLightExceptions() {
            if (_lightThrow) {
                return this;
            }

            return new LookupGlobalVariable(_codeContextExpr, _name, _isLocal, true);
        }

        #endregion
    }

    internal class LookupGlobalInstruction : Instruction {
        private readonly string _name;
        private readonly bool _isLocal, _lightThrow;
        public LookupGlobalInstruction(string name, bool isLocal, bool lightThrow) {
            _name = name;
            _isLocal = isLocal;
            _lightThrow = lightThrow;
        }
        public override int ConsumedStack { get { return 1; } }
        public override int ProducedStack { get { return 1; } }
        public override int Run(InterpretedFrame frame) {
            frame.Push(PythonOps.GetVariable((CodeContext)frame.Pop(), _name, !_isLocal, _lightThrow));
            return +1;
        }

        public override string ToString() {
            return "LookupGlobal(" + _name + ", isLocal=" + _isLocal + ")";
        }
    }
}
