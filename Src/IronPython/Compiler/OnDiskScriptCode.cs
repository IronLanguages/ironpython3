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
using System.Text;
using System.Reflection;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

namespace IronPython.Compiler {
    /// <summary>
    /// A ScriptCode which has been loaded from an assembly which is saved on disk.
    /// </summary>
    class OnDiskScriptCode : RunnableScriptCode {
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
