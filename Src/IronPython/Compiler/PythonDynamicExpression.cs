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

#if FEATURE_CORE_DLR
using System.Linq.Expressions;
#endif

using System;
using System.Dynamic;
using System.Runtime.CompilerServices;
using IronPython.Runtime;
using Microsoft.Scripting.Ast;
using Microsoft.Scripting.Interpreter;
using Microsoft.Scripting.Utils;
using System.Collections.Generic;

namespace IronPython.Compiler {
    internal sealed class PythonDynamicExpression1 : LightDynamicExpression1 {
        private readonly CompilationMode/*!*/ _mode;

        public PythonDynamicExpression1(CallSiteBinder/*!*/ binder, CompilationMode/*!*/ mode, Expression/*!*/ arg0) 
            : base(binder, arg0) {
            _mode = mode;
        }

        protected override Expression Rewrite(CallSiteBinder binder, Expression arg0) {
            return new PythonDynamicExpression1(binder, _mode, arg0);
        }

        public override Expression/*!*/ Reduce() {
            return _mode.ReduceDynamic((DynamicMetaObjectBinder)Binder, Type, Argument0);
        }
    }

    internal sealed class PythonDynamicExpression1<T> : LightDynamicExpression1 {
        private readonly CompilationMode/*!*/ _mode;

        public PythonDynamicExpression1(CallSiteBinder/*!*/ binder, CompilationMode/*!*/ mode, Expression/*!*/ arg0)
            : base(binder, arg0) {
            _mode = mode;
        }

        protected override Expression Rewrite(CallSiteBinder binder, Expression arg0) {
            return new PythonDynamicExpression1<T>(binder, _mode, arg0);
        }

        public override Expression/*!*/ Reduce() {
            return _mode.ReduceDynamic((DynamicMetaObjectBinder)Binder, Type, Argument0);
        }

        public override Type Type {
            get {
                return typeof(T);
            }
        }

        public override void AddInstructions(LightCompiler compiler) {
            compiler.Compile(Argument0);
            compiler.Instructions.EmitDynamic<object, T>(Binder);
        }
    }

    internal class PythonDynamicExpression2 : LightDynamicExpression2 {
        private readonly CompilationMode/*!*/ _mode;

        public PythonDynamicExpression2(CallSiteBinder/*!*/ binder, CompilationMode/*!*/ mode, Expression/*!*/ arg0, Expression/*!*/ arg1)
            : base(binder, arg0, arg1) {
            _mode = mode;
        }

        public override Expression/*!*/ Reduce() {
            return _mode.ReduceDynamic((DynamicMetaObjectBinder)Binder, Type, Argument0, Argument1);
        }

        protected override Expression Rewrite(CallSiteBinder binder, Expression arg0, Expression arg1) {
            return new PythonDynamicExpression2(binder, _mode, arg0, arg1);
        }

        public override void AddInstructions(LightCompiler compiler) {
            if (Argument0.Type == typeof(CodeContext)) {
                compiler.Compile(Argument0);
                compiler.Compile(Argument1);
                compiler.Instructions.EmitDynamic<CodeContext, object, object>(Binder);
            } else if (Argument1.Type == typeof(CodeContext)) {
                // GetMember sites
                compiler.Compile(Argument0);
                compiler.Compile(Argument1);
                compiler.Instructions.EmitDynamic<object, CodeContext, object>(Binder);
            } else {
                base.AddInstructions(compiler);
            }
        }
    }

    internal sealed class PythonDynamicExpression2<T> : PythonDynamicExpression2 {
        private readonly CompilationMode/*!*/ _mode;

        public PythonDynamicExpression2(CallSiteBinder/*!*/ binder, CompilationMode/*!*/ mode, Expression/*!*/ arg0, Expression/*!*/ arg1)
            : base(binder, mode, arg0, arg1) {
            _mode = mode;
        }

        public override Expression/*!*/ Reduce() {
            return _mode.ReduceDynamic((DynamicMetaObjectBinder)Binder, Type, Argument0, Argument1);
        }

        protected override Expression Rewrite(CallSiteBinder binder, Expression arg0, Expression arg1) {
            return new PythonDynamicExpression2<T>(binder, _mode, arg0, arg1);
        }

        public override Type Type {
            get {
                return typeof(T);
            }
        }

        public override void AddInstructions(LightCompiler compiler) {
            if (Argument0.Type == typeof(CodeContext)) {
                compiler.Compile(Argument0);
                compiler.Compile(Argument1);
                compiler.Instructions.EmitDynamic<CodeContext, object, T>(Binder);
            } else if (Argument1.Type == typeof(CodeContext)) {
                // GetMember sites
                compiler.Compile(Argument0);
                compiler.Compile(Argument1);
                compiler.Instructions.EmitDynamic<object, CodeContext, T>(Binder);
            } else {
                compiler.Compile(Argument0);
                compiler.Compile(Argument1);
                compiler.Instructions.EmitDynamic<object, object, T>(Binder);
            }
        }
    }

    internal sealed class PythonDynamicExpression3 : LightDynamicExpression3 {
        private readonly CompilationMode/*!*/ _mode;

        public PythonDynamicExpression3(CallSiteBinder/*!*/ binder, CompilationMode/*!*/ mode, Expression/*!*/ arg0, Expression/*!*/ arg1, Expression/*!*/ arg2)
            : base(binder, arg0, arg1, arg2) {
            _mode = mode;
        }

        public override Expression/*!*/ Reduce() {
            return _mode.ReduceDynamic((DynamicMetaObjectBinder)Binder, Type, Argument0, Argument1, Argument2);
        }

        protected override Expression Rewrite(CallSiteBinder binder, Expression arg0, Expression arg1, Expression arg2) {
            return new PythonDynamicExpression3(binder, _mode, arg0, arg1, arg2);
        }

        public override void AddInstructions(LightCompiler compiler) {
            if (Argument0.Type == typeof(CodeContext)) {
                compiler.Compile(Argument0);
                compiler.Compile(Argument1);
                compiler.Compile(Argument2);
                compiler.Instructions.EmitDynamic<CodeContext, object, object, object>(Binder);
            } else {
                base.AddInstructions(compiler);
            }
        }
    }

    internal sealed class PythonDynamicExpression4 : LightDynamicExpression4 {
        private readonly CompilationMode/*!*/ _mode;

        public PythonDynamicExpression4(CallSiteBinder/*!*/ binder, CompilationMode/*!*/ mode, Expression/*!*/ arg0, Expression/*!*/ arg1, Expression/*!*/ arg2, Expression/*!*/ arg3)
            : base(binder, arg0, arg1, arg2, arg3) {
            _mode = mode;
        }

        public override Expression/*!*/ Reduce() {
            return _mode.ReduceDynamic((DynamicMetaObjectBinder)Binder, Type, Argument0, Argument1, Argument2, Argument3);
        }

        protected override Expression Rewrite(CallSiteBinder binder, Expression arg0, Expression arg1, Expression arg2, Expression arg3) {
            return new PythonDynamicExpression4(binder, _mode, arg0, arg1, arg2, arg3);
        }

        public override void AddInstructions(LightCompiler compiler) {
            if (Argument0.Type == typeof(CodeContext)) {
                compiler.Compile(Argument0);
                compiler.Compile(Argument1);
                compiler.Compile(Argument2);
                compiler.Compile(Argument3);
                compiler.Instructions.EmitDynamic<CodeContext, object, object, object, object>(Binder);
                return;
            } else {
                base.AddInstructions(compiler);
            }
        }
    }

    internal sealed class PythonDynamicExpressionN : LightTypedDynamicExpressionN {
        private readonly CompilationMode/*!*/ _mode;

        public PythonDynamicExpressionN(CallSiteBinder/*!*/ binder, CompilationMode/*!*/ mode, IList<Expression>/*!*/ args)
            : base(binder, typeof(object), args) {
            _mode = mode;
        }

        protected override Expression Rewrite(CallSiteBinder binder, IList<Expression> args) {
            return new PythonDynamicExpressionN(binder, _mode, args);
        }

        public override Expression/*!*/ Reduce() {
            return _mode.ReduceDynamic((DynamicMetaObjectBinder)Binder, Type, ArrayUtils.ToArray(Arguments));
        }

        public override void AddInstructions(LightCompiler compiler) {
            if (ArgumentCount > 15) {
                compiler.Compile(Reduce());
            } else if (GetArgument(0).Type == typeof(CodeContext)) {
                for (int i = 0; i < ArgumentCount; i++) {
                    compiler.Compile(GetArgument(i));
                }

                switch(ArgumentCount) {
                    case 1: compiler.Instructions.EmitDynamic<CodeContext, object>(Binder); break;
                    case 2: compiler.Instructions.EmitDynamic<CodeContext, object, object>(Binder); break;
                    case 3: compiler.Instructions.EmitDynamic<CodeContext, object, object, object>(Binder); break;
                    case 4: compiler.Instructions.EmitDynamic<CodeContext, object, object, object, object>(Binder); break;
                    case 5: compiler.Instructions.EmitDynamic<CodeContext, object, object, object, object, object>(Binder); break;
                    case 6: compiler.Instructions.EmitDynamic<CodeContext, object, object, object, object, object, object>(Binder); break;
                    case 7: compiler.Instructions.EmitDynamic<CodeContext, object, object, object, object, object, object, object>(Binder); break;
                    case 8: compiler.Instructions.EmitDynamic<CodeContext, object, object, object, object, object, object, object, object>(Binder); break;
                    case 9: compiler.Instructions.EmitDynamic<CodeContext, object, object, object, object, object, object, object, object, object>(Binder); break;
                    case 10: compiler.Instructions.EmitDynamic<CodeContext, object, object, object, object, object, object, object, object, object, object>(Binder); break;
                    case 11: compiler.Instructions.EmitDynamic<CodeContext, object, object, object, object, object, object, object, object, object, object, object>(Binder); break;
                    case 12: compiler.Instructions.EmitDynamic<CodeContext, object, object, object, object, object, object, object, object, object, object, object, object>(Binder); break;
                    case 13: compiler.Instructions.EmitDynamic<CodeContext, object, object, object, object, object, object, object, object, object, object, object, object, object>(Binder); break;
                    case 14: compiler.Instructions.EmitDynamic<CodeContext, object, object, object, object, object, object, object, object, object, object, object, object, object, object>(Binder); break;
                    case 15: compiler.Instructions.EmitDynamic<CodeContext, object, object, object, object, object, object, object, object, object, object, object, object, object, object, object>(Binder); break;                    
                }
            } else {
                base.AddInstructions(compiler);
            }
        }
    }
}
