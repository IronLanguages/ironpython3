// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.
#if FEATURE_REFEMIT

using System.Linq.Expressions;

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

#if !NETCOREAPP2_0 && !NETCOREAPP2_1
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
#endif

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