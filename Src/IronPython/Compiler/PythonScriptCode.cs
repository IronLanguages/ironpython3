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
using Microsoft.Scripting.Ast;
using MSAst = System.Linq.Expressions;
#else
using Microsoft.Scripting.Ast;
using MSAst = Microsoft.Scripting.Ast;
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

using Microsoft.Scripting;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Compiler.Ast;
using IronPython.Runtime;
using IronPython.Runtime.Operations;

namespace IronPython.Compiler {
    
    /// <summary>
    /// Represents a script code which can be dynamically bound to execute against
    /// arbitrary Scope objects.  This is used for code when the user runs against
    /// a particular scope as well as for exec and eval code as well.  It is also
    /// used when tracing is enabled.
    /// </summary>
    class PythonScriptCode : RunnableScriptCode {
        private CodeContext _defaultContext;
        private LookupCompilationDelegate/*!*/ _target, _tracingTarget; // lazily compiled targets

        public PythonScriptCode(Compiler.Ast.PythonAst/*!*/ ast)
            : base(ast) {
            Assert.NotNull(ast);
            Debug.Assert(ast.Type == typeof(Expression<LookupCompilationDelegate>));
        }

        public override object Run() {
            if (SourceUnit.Kind == SourceCodeKind.Expression) {
                return EvalWrapper(DefaultContext);
            }

            return RunWorker(DefaultContext);
        }

        public override object Run(Scope scope) {
            CodeContext ctx = GetContextForScope(scope, SourceUnit);
            
            if (SourceUnit.Kind == SourceCodeKind.Expression) {
                return EvalWrapper(ctx);
            }

            return RunWorker(ctx);
        }

        private object RunWorker(CodeContext ctx) {
            LookupCompilationDelegate target = GetTarget(true);

            Exception e = PythonOps.SaveCurrentException();
            PushFrame(ctx, _code);
            try {
                return target(ctx, _code);
            } finally {
                PythonOps.RestoreCurrentException(e);
                PopFrame();
            }
        }

        private LookupCompilationDelegate GetTarget(bool register) {
            LookupCompilationDelegate target;
            PythonContext pc = (PythonContext)Ast.CompilerContext.SourceUnit.LanguageContext;
            if (!pc.EnableTracing) {
                EnsureTarget(register);
                target = _target;
            } else {
                EnsureTracingTarget();
                target = _tracingTarget;
            }
            return target;
        }

        public override FunctionCode GetFunctionCode(bool register) {
            GetTarget(register);
            return _code;
        }

        public override Scope/*!*/ CreateScope() {
            return new Scope();
        }

        // wrapper so we can do minimal code gen for eval code
        private object EvalWrapper(CodeContext ctx) {
            try {
                return RunWorker(ctx);
            } catch (Exception e) {
                PythonOps.UpdateStackTrace(e, ctx, Code, 0);
                throw;
            }
        }

        private LookupCompilationDelegate CompileBody(LightExpression<LookupCompilationDelegate> lambda) {
            LookupCompilationDelegate func;

            var extractConstant = ExtractConstant(lambda);

            if (extractConstant != null) {
                // skip compiling for really simple code
                object value = extractConstant.Value;
                return (codeCtx, functionCode) => value;
            }

            PythonContext pc = (PythonContext)Ast.CompilerContext.SourceUnit.LanguageContext;
            if (ShouldInterpret(pc)) {
                func = lambda.Compile(pc.Options.CompilationThreshold);
            } else {
                func = lambda.ReduceToLambda().Compile(pc.EmitDebugSymbols(Ast.CompilerContext.SourceUnit));
            }

            return func;
        }

        private bool ShouldInterpret(PythonContext pc) {
            return pc.ShouldInterpret((PythonCompilerOptions)Ast.CompilerContext.Options, Ast.CompilerContext.SourceUnit);
        }

        private static PythonConstantExpression ExtractConstant(LightExpression<LookupCompilationDelegate> lambda) {
            var body = lambda.Body as BlockExpression;
            if (body == null || 
                body.Expressions.Count != 2 || 
                !(body.Expressions[0] is DebugInfoExpression) || 
                body.Expressions[1].NodeType  != ExpressionType.Convert ||
                !(((MSAst.UnaryExpression)body.Expressions[1]).Operand is PythonConstantExpression)) {
                return null;
            }

            return (PythonConstantExpression)((MSAst.UnaryExpression)body.Expressions[1]).Operand;
        }

        private void EnsureTarget(bool register) {
            if (_target == null) {
                _target = CompileBody((LightExpression<LookupCompilationDelegate>)Ast.GetLambda());
                EnsureFunctionCode(_target, false, register);
            }
        }

        private CodeContext DefaultContext {
            get {
                if (_defaultContext == null) {
                    _defaultContext = CreateTopLevelCodeContext(new PythonDictionary(), Ast.CompilerContext.SourceUnit.LanguageContext);
                }

                return _defaultContext;
            }
        }

        private void EnsureTracingTarget() {
            if (_tracingTarget == null) {
                PythonContext pc = (PythonContext)Ast.CompilerContext.SourceUnit.LanguageContext;

                var debugProperties = new PythonDebuggingPayload(null);

                var debugInfo = new Microsoft.Scripting.Debugging.CompilerServices.DebugLambdaInfo(
                    null,           // IDebugCompilerSupport
                    null,           // lambda alias
                    false,          // optimize for leaf frames
                    null,           // hidden variables
                    null,           // variable aliases
                    debugProperties // custom payload
                );

                var lambda = (Expression<LookupCompilationDelegate>)pc.DebugContext.TransformLambda((MSAst.LambdaExpression)Ast.GetLambda().Reduce(), debugInfo);

                LookupCompilationDelegate func;
                if (ShouldInterpret(pc)) {
                    func = (LookupCompilationDelegate)CompilerHelpers.LightCompile(lambda, pc.Options.CompilationThreshold);
                } else {
                    func = (LookupCompilationDelegate)lambda.Compile(pc.EmitDebugSymbols(Ast.CompilerContext.SourceUnit));
                }

                _tracingTarget = func;
                debugProperties.Code = EnsureFunctionCode(_tracingTarget, true, true);
            }
        }
    }
}
