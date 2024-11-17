// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using IronPython.Runtime;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

namespace IronPython.Compiler {
    /// <summary>
    /// A ScriptCode which has been loaded from an assembly which is saved on disk.
    /// </summary>
    internal class OnDiskScriptCode : RunnableScriptCode {
        private readonly LookupCompilationDelegate _target;
        private CodeContext _optimizedContext;
        private readonly string _moduleName;

        public OnDiskScriptCode(LookupCompilationDelegate code, SourceUnit sourceUnit, string moduleName) :
            base(MakeAstFromSourceUnit(sourceUnit)) {
            _target = code;
            _moduleName = moduleName;
        }

        /// <summary>
        /// Creates a fake PythonAst object which is represenative of the on-disk script code.
        /// </summary>
        private static Ast.PythonAst MakeAstFromSourceUnit(SourceUnit sourceUnit) {
            var compCtx = new CompilerContext(sourceUnit, new PythonCompilerOptions(), ErrorSink.Null);

            return new Ast.PythonAst(compCtx);
        }

        public override object Run() {
            CodeContext ctx = CreateContext();
            try {
                var funcCode = EnsureFunctionCode(_target, false, true);
                PushFrame(ctx, funcCode);
                return _target(ctx, funcCode);
            } finally {
                PopFrame();
            }
        }

        public override object Run(Scope scope) {
            if (scope == CreateScope()) {
                return Run();
            }

            throw new NotSupportedException();
        }

        public string ModuleName {
            get {
                return _moduleName;
            }
        }

        public override FunctionCode GetFunctionCode(bool register) {
            return EnsureFunctionCode(_target, false, register);
        }

        public override Scope CreateScope() {
            return CreateContext().GlobalScope;
        }

        internal CodeContext CreateContext() {
            if (_optimizedContext == null) {
                CachedOptimizedCodeAttribute[] attrs = (CachedOptimizedCodeAttribute[])_target.Method.GetCustomAttributes(typeof(CachedOptimizedCodeAttribute), false);

                // create the CompilerContext for the ScriptCode
                CachedOptimizedCodeAttribute optimizedCode = attrs[0];

                // create the storage for the global scope
                Dictionary<string, PythonGlobal> globals = new Dictionary<string, PythonGlobal>(StringComparer.Ordinal);
                PythonGlobal[] globalArray = new PythonGlobal[optimizedCode.Names.Length];
                var dict = new PythonDictionary(new GlobalDictionaryStorage(globals, globalArray));

                ModuleContext mc = new ModuleContext(dict, (PythonContext)SourceUnit.LanguageContext);
                CodeContext res = mc.GlobalContext;

                for (int i = 0; i < optimizedCode.Names.Length; i++) {
                    string name = optimizedCode.Names[i];
                    globalArray[i] = globals[name] = new PythonGlobal(res, name);
                }

                _optimizedContext = CreateTopLevelCodeContext(dict, (PythonContext)SourceUnit.LanguageContext);
            }
            return _optimizedContext;
        }
    }
}
