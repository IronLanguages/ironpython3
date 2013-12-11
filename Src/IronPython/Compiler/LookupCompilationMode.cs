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
using MSAst = System.Linq.Expressions;
#else
using MSAst = Microsoft.Scripting.Ast;
#endif

using System;
using System.Collections.Generic;

using Microsoft.Scripting;
using Microsoft.Scripting.Ast;
using Microsoft.Scripting.Runtime;
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
    class LookupCompilationMode : CompilationMode {
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
