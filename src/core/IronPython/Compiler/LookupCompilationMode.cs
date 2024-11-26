// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

using System;
using System.Collections.Generic;

using Microsoft.Scripting;
using Microsoft.Scripting.Ast;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;

using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    /// <summary>
    /// Provides globals for when we need to lookup into a dictionary for each global access.
    /// 
    /// This is the slowest form of globals and is only used when we need to run against an
    /// arbitrary dictionary given to us by a user.
    /// </summary>
    internal class LookupCompilationMode : CompilationMode {
        public LookupCompilationMode() {
        }

        public override ScriptCode MakeScriptCode(PythonAst ast) {
            return new PythonScriptCode(ast);
        }

        public override LightLambdaExpression ReduceAst(PythonAst instance, string name) {
            return Utils.LightLambda<LookupCompilationDelegate>(
                typeof(object),
                AstUtils.Convert(instance.ReduceWorker(), typeof(object)),
                name,
                PythonAst._arrayFuncParams
            );
        }

        public override MSAst.Expression GetGlobal(MSAst.Expression globalContext, int arrayIndex, PythonVariable variable, PythonGlobal global) {
            return new LookupGlobalVariable(
                globalContext,
                variable.Name,
                variable.Kind == VariableKind.Local
            );
        }
    }
}
