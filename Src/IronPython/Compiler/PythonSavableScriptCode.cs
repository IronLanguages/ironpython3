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
#if FEATURE_REFEMIT

#if FEATURE_CORE_DLR
using System.Linq.Expressions;
#else
using Microsoft.Scripting.Ast;
#endif

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using Microsoft.Scripting;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

namespace IronPython.Compiler {
    /// <summary>
    /// A ScriptCode which can be saved to disk.  We only create this when called via
    /// the clr.CompileModules API.  This ScriptCode does not support running.
    /// </summary>
    class PythonSavableScriptCode : SavableScriptCode, ICustomScriptCodeData {
        private readonly Expression<LookupCompilationDelegate> _code;
        private readonly string[] _names;
        private readonly string _moduleName;
        
        public PythonSavableScriptCode(Expression<LookupCompilationDelegate> code, SourceUnit sourceUnit, string[] names, string moduleName)
            : base(sourceUnit) {
            _code = code;
            _names = names;
            _moduleName = moduleName;
        }

        protected override KeyValuePair<MethodBuilder, Type> CompileForSave(TypeGen typeGen) {
            var lambda = RewriteForSave(typeGen, _code);

            MethodBuilder mb = typeGen.TypeBuilder.DefineMethod(lambda.Name ?? "lambda_method", CompilerHelpers.PublicStatic | MethodAttributes.SpecialName);
            lambda.CompileToMethod(mb);

            mb.SetCustomAttribute(new CustomAttributeBuilder(
                typeof(CachedOptimizedCodeAttribute).GetConstructor(new Type[] { typeof(string[]) }),
                new object[] { _names }
            ));

            return new KeyValuePair<MethodBuilder, Type>(mb, typeof(LookupCompilationDelegate));
        }

        public override object Run() {
            throw new NotSupportedException();
        }

        public override object Run(Scope scope) {
            throw new NotSupportedException();
        }

        public override Scope CreateScope() {
            throw new NotSupportedException();
        }

        #region ICustomScriptCodeData Members

        string ICustomScriptCodeData.GetCustomScriptCodeData() {
            return _moduleName;
        }

        #endregion
    }
}

#endif