// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

namespace IronPython.Compiler {    
    abstract class RunnableScriptCode : ScriptCode {
        internal FunctionCode _code;
        private readonly Compiler.Ast.PythonAst _ast;

        public RunnableScriptCode(Compiler.Ast.PythonAst ast)
            : base(ast.SourceUnit) {
            _ast = ast;
        }

        public override object Run() {
            return base.Run();
        }

        public override object Run(Scope scope) {
            throw new NotImplementedException();
        }

        protected static CodeContext/*!*/ CreateTopLevelCodeContext(PythonDictionary/*!*/ dict, LanguageContext/*!*/ context) {
            ModuleContext modContext = new ModuleContext(dict, (PythonContext)context);
            return modContext.GlobalContext;
        }

        protected static CodeContext GetContextForScope(Scope scope, SourceUnit sourceUnit) {
            var ext = scope.GetExtension(sourceUnit.LanguageContext.ContextId) as PythonScopeExtension;
            if (ext == null) {
                ext = sourceUnit.LanguageContext.EnsureScopeExtension(scope) as PythonScopeExtension;
            }

            CodeContext ctx = ext.ModuleContext.GlobalContext;
            return ctx;
        }

        protected FunctionCode EnsureFunctionCode(Delegate/*!*/ dlg) {
            return EnsureFunctionCode(dlg, false, true);
        }

        protected FunctionCode EnsureFunctionCode(Delegate/*!*/ dlg, bool tracing, bool register) {
            Debug.Assert(dlg != null);

            if (_code == null) {
                Interlocked.CompareExchange(
                    ref _code,
                    new FunctionCode(
                        (PythonContext)SourceUnit.LanguageContext,
                        dlg,
                        _ast,
                        _ast.GetDocumentation(_ast),
                        tracing,
                        register
                    ),
                    null
                );
            }
            return _code;
        }

        public Compiler.Ast.PythonAst Ast {
            get {
                return _ast;
            }
        }

        public FunctionCode Code {
            get {
                return _code;
            }
        }

        public abstract FunctionCode GetFunctionCode(bool register);
                
        protected void PushFrame(CodeContext context, FunctionCode code) {
            if (((PythonContext)SourceUnit.LanguageContext).PythonOptions.Frames) {
                PythonOps.PushFrame(context, code);
            }
        }

        protected void PopFrame() {
            if (((PythonContext)SourceUnit.LanguageContext).PythonOptions.Frames) {
                List<FunctionStack> stack = PythonOps.GetFunctionStack();
                stack.RemoveAt(stack.Count - 1);
            }
        }
    }
}
