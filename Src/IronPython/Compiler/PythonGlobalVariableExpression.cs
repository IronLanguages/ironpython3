﻿/* ****************************************************************************
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

#if FEATURE_CORE_DLR
using System.Linq.Expressions;
#endif

using System;
using System.Diagnostics;
using System.Reflection;

using Microsoft.Scripting;
using Microsoft.Scripting.Ast;
using Microsoft.Scripting.Interpreter;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

namespace IronPython.Compiler {
    interface IPythonVariableExpression  {
        Expression/*!*/ Assign(Expression/*!*/ value);
        Expression/*!*/ Delete();
        Expression/*!*/ Create();
    }

    interface IPythonGlobalExpression : IPythonVariableExpression {
        Expression/*!*/ RawValue();
    }
    /// <summary>
    /// Small reducable node which just fetches the value from a PythonGlobal
    /// object.  The compiler recognizes these on sets and turns them into
    /// assignments on the python global object.
    /// </summary>
    class PythonGlobalVariableExpression : Expression, IInstructionProvider, IPythonGlobalExpression, ILightExceptionAwareExpression {
        private readonly Expression/*!*/ _target;
        private readonly PythonGlobal/*!*/ _global;
        private readonly Ast.PythonVariable/*!*/ _variable;
        private readonly bool _lightEh;
        internal static Expression/*!*/ Uninitialized = Expression.Field(null, typeof(Uninitialized).GetField("Instance"));

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

    class PythonGlobalInstruction : Instruction {
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

    class PythonLightThrowGlobalInstruction : PythonGlobalInstruction {
        public PythonLightThrowGlobalInstruction(PythonGlobal global) : base(global) {
        }

        public override int Run(InterpretedFrame frame) {
            frame.Push(_global.CurrentValueLightThrow);
            return +1;
        }
    }

    class PythonSetGlobalVariableExpression : Expression, IInstructionProvider {
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
                    typeof(PythonGlobal).GetProperty("CurrentValue")
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

    class PythonRawGlobalValueExpression : Expression {
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

    class PythonSetGlobalInstruction : Instruction {
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

    class LookupGlobalVariable : Expression, IInstructionProvider, IPythonGlobalExpression, ILightExceptionAwareExpression {
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
                typeof(PythonOps).GetMethod(_isLocal ? "RawGetLocal" : "RawGetGlobal"),
                _codeContextExpr,
                Utils.Constant(_name)
            );
        }

        public override Expression/*!*/ Reduce() {
            return Expression.Call(
                typeof(PythonOps).GetMethod(_isLocal ? "GetLocal" : "GetGlobal"),
                _codeContextExpr,
                Utils.Constant(_name)
            );
        }

        public Expression/*!*/ Assign(Expression/*!*/ value) {
            return Expression.Call(
                typeof(PythonOps).GetMethod(_isLocal ? "SetLocal" : "SetGlobal"),
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
                typeof(PythonOps).GetMethod(_isLocal ? "DeleteLocal" : "DeleteGlobal"),
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

    class LookupGlobalInstruction : Instruction {
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
