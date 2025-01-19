// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if FEATURE_REFEMIT

using MSAst = System.Linq.Expressions;

using System;
using System.Collections.Generic;

using Microsoft.Scripting;
using Microsoft.Scripting.Ast;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

using AstUtils = Microsoft.Scripting.Ast.Utils;


namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    internal class ToDiskCompilationMode : CollectableCompilationMode {
        public override MSAst.Expression GetConstant(object value) {
            return AstUtils.Constant(value);
        }

        public override void PrepareScope(PythonAst ast, System.Runtime.CompilerServices.ReadOnlyCollectionBuilder<MSAst.ParameterExpression> locals, List<MSAst.Expression> init) {
            locals.Add(PythonAst._globalArray);
            init.Add(
                Ast.Assign(
                    PythonAst._globalArray,
                    Ast.Call(
                        typeof(PythonOps).GetMethod(nameof(PythonOps.GetGlobalArrayFromContext)),
                        PythonAst._globalContext
                    )
                )
            );
        }

        public override LightLambdaExpression ReduceAst(PythonAst instance, string name) {
            return Utils.LightLambda<LookupCompilationDelegate>(
                typeof(object),
                Ast.Block(
                    new[] { PythonAst._globalArray },
                    Ast.Assign(
                        PythonAst._globalArray,
                        Ast.Call(
                            null,
                            typeof(PythonOps).GetMethod(nameof(PythonOps.GetGlobalArrayFromContext)),
                            PythonAst._globalContext
                        )
                    ),
                    AstUtils.Convert(instance.ReduceWorker(), typeof(object))
                ),
                name,
                PythonAst._arrayFuncParams
            );
        }

        public override ScriptCode MakeScriptCode(PythonAst ast) {
            PythonCompilerOptions pco = ast.CompilerContext.Options as PythonCompilerOptions;
            // reduce to LightLambda then to Lambda
            var code = (MSAst.Expression<LookupCompilationDelegate>)ast.Reduce().Reduce();

            return new PythonSavableScriptCode(code, ast.SourceUnit, ast.GetNames(), pco.ModuleName);
        }

    }
}

#endif