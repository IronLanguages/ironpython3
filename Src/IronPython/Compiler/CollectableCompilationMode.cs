// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using IronPython.Runtime;

using Microsoft.Scripting.Ast;
using Microsoft.Scripting.Utils;

using AstUtils = Microsoft.Scripting.Ast.Utils;
using MSAst = System.Linq.Expressions;


namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    /// <summary>
    /// A global allocator that puts all of the globals into an array access.  The array is an
    /// array of PythonGlobal objects.  We then just close over the array for any inner functions.
    /// 
    /// Once compiled a RuntimeScriptCode is produced which is closed over the entire execution
    /// environment.
    /// </summary>
    internal class CollectableCompilationMode : CompilationMode {

        public override LightLambdaExpression ReduceAst(PythonAst instance, string name) {
            return Utils.LightLambda<Func<FunctionCode, object>>(
                    typeof(object),
                    Ast.Block(
                        new[] { PythonAst._globalArray, PythonAst._globalContext },
                        Ast.Assign(PythonAst._globalArray, instance.GlobalArrayInstance),
                        Ast.Assign(PythonAst._globalContext, Ast.Constant(instance.ModuleContext.GlobalContext)),
                        AstUtils.Convert(instance.ReduceWorker(), typeof(object))
                    ),
                    name,
                    new[] { PythonAst._functionCode }
                );
        }

        public override void PrepareScope(PythonAst ast, ReadOnlyCollectionBuilder<MSAst.ParameterExpression> locals, List<MSAst.Expression> init) {
            locals.Add(PythonAst._globalArray);
            init.Add(Ast.Assign(PythonAst._globalArray, ast._arrayExpression));
        }


        public override MSAst.Expression GetGlobal(MSAst.Expression globalContext, int arrayIndex, PythonVariable variable, PythonGlobal global) {
            Assert.NotNull(global);

            return new PythonGlobalVariableExpression(
                Ast.ArrayIndex(
                    PythonAst._globalArray,
                    Ast.Constant(arrayIndex)
                ),
                variable,
                global
            );
        }

        public override Type DelegateType {
            get {
                return typeof(MSAst.Expression<Func<FunctionCode, object>>);
            }
        }
    }
}
