// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;

using IronPython.Compiler.Ast;
using IronPython.Runtime;
using IronPython.Runtime.Operations;

using Microsoft.Scripting;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Runtime;

using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler {
    /// <summary>
    /// Represents a script code which can be consumed at runtime as-is.  This code has
    /// no external dependencies and is closed over its scope.  
    /// </summary>
    internal class RuntimeScriptCode : RunnableScriptCode {
        private readonly CodeContext/*!*/ _optimizedContext;
        private Func<FunctionCode, object> _optimizedTarget;

        private ScriptCode _unoptimizedCode;

        public RuntimeScriptCode(PythonAst/*!*/ ast, CodeContext/*!*/ codeContext)
            : base(ast) {
            Debug.Assert(codeContext.GlobalScope.GetExtension(codeContext.LanguageContext.ContextId) != null);
            Debug.Assert(ast.Type == typeof(MSAst.Expression<Func<FunctionCode, object>>));

            _optimizedContext = codeContext;
        }

        public override object Run() {
            return InvokeTarget(CreateScope());
        }

        public override object Run(Scope scope) {
            return InvokeTarget(scope);
        }

        public override FunctionCode GetFunctionCode(bool register) {
            EnsureCompiled();

            return EnsureFunctionCode(_optimizedTarget, false, register);
        }

        private object InvokeTarget(Scope scope) {
            if (scope == _optimizedContext.GlobalScope && !_optimizedContext.LanguageContext.EnableTracing) {
                EnsureCompiled();

                Exception e = PythonOps.SaveCurrentException();
                var funcCode = EnsureFunctionCode(_optimizedTarget, false, true);
                PushFrame(_optimizedContext, funcCode);
                try {
                    if (Ast.CompilerContext.SourceUnit.Kind == SourceCodeKind.Expression) {
                        return OptimizedEvalWrapper(funcCode);
                    }
                    return _optimizedTarget(funcCode);
                } finally {
                    PythonOps.RestoreCurrentException(e);
                    PopFrame();
                }
            }

            // if we're running against a different scope or we need tracing then re-compile the code.
            if (_unoptimizedCode == null) {
                // TODO: Copy instead of mutate
                ((PythonCompilerOptions)Ast.CompilerContext.Options).Optimized = false;
                Interlocked.CompareExchange(
                    ref _unoptimizedCode,
                    Ast.MakeLookupCode().ToScriptCode(),
                    null
                );
            }

            // This is a brand new ScriptCode which also handles all appropriate ScriptCode
            // things such as pushing a function code or updating the stack trace for
            // exec/eval code.  Therefore we don't need to do any of that here.
            return _unoptimizedCode.Run(scope);
        }

        private object OptimizedEvalWrapper(FunctionCode funcCode) {
            try {
                return _optimizedTarget(funcCode);
            } catch (Exception e) {
                PythonOps.UpdateStackTrace(e, _optimizedContext, Code, 0);
                throw;
            }
        }

        public override Scope/*!*/ CreateScope() {
            return _optimizedContext.GlobalScope;
        }

        private void EnsureCompiled() {
            if (_optimizedTarget == null) {
                Interlocked.CompareExchange(ref _optimizedTarget, Compile(), null);
            }
        }

        private Func<FunctionCode, object>/*!*/ Compile() {
            var pco = (PythonCompilerOptions)Ast.CompilerContext.Options;
            var pc = (PythonContext)SourceUnit.LanguageContext;

            if (pc.ShouldInterpret(pco, SourceUnit)) {
                return ((Microsoft.Scripting.Ast.LightExpression<Func<FunctionCode, object>>)Ast.GetLambda()).Compile(pc.Options.CompilationThreshold);
            } else {
                return ((Microsoft.Scripting.Ast.LightExpression<Func<FunctionCode, object>>)Ast.GetLambda()).ReduceToLambda().Compile(pc.EmitDebugSymbols(SourceUnit));
            }
        }
    }
}
