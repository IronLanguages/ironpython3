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
using System.Reflection;
using Microsoft.Scripting.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    public class ReturnStatement : Statement {
        private readonly Expression _expression;        

        public ReturnStatement(Expression expression) {
            _expression = expression;
        }

        public Expression Expression {
            get { return _expression; }
        }

        public override MSAst.Expression Reduce() {
            if (Parent.IsGeneratorMethod) {
                if (_expression != null) {
                    // Statements can't return null, so return a rethrow. 
                    // Callers should detecet the ag.AddError and avoid trying to execute the tree, 
                    // but if they accidentally do, use Throw instead of empty so that
                    // we'll get an exception.
                    return Ast.Throw(
                        Ast.New(
                            typeof(InvalidOperationException).GetConstructor(ReflectionUtils.EmptyTypes)
                        )
                    );
                }

                return GlobalParent.AddDebugInfo(AstUtils.YieldBreak(GeneratorLabel), Span);
            }

            return GlobalParent.AddDebugInfo(
                Ast.Return(
                    FunctionDefinition._returnLabel,
                    TransformOrConstantNull(_expression, typeof(object))
                ),
                Span
            );
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_expression != null) {
                    _expression.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        internal override bool CanThrow {
            get {
                if (_expression == null) {
                    return false;
                }

                return _expression.CanThrow;
            }
        }
    }
}
