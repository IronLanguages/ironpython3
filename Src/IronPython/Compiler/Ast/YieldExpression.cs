// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

using System;
using System.Diagnostics;
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    // New in Pep342 for Python 2.5. Yield is an expression with a return value.
    //    x = yield z
    // The return value (x) is provided by calling Generator.Send()
    public class YieldExpression : Expression { 
        private readonly Expression _expression;

        public YieldExpression(Expression expression) {
            _expression = expression;
        }

        public Expression Expression {
            get { return _expression; }
        }

        // Generate AST statement to call $gen.CheckThrowable() on the Python Generator.
        // This needs to be injected at any yield suspension points, mainly:
        // - at the start of the generator body
        // - after each yield statement.
        internal static MSAst.Expression CreateCheckThrowExpression(SourceSpan span) {
            MSAst.Expression instance = GeneratorRewriter._generatorParam;
            Debug.Assert(instance.Type == typeof(IronPython.Runtime.PythonGenerator));

            MSAst.Expression s2 = LightExceptions.CheckAndThrow(
                Expression.Call(
                    AstMethods.GeneratorCheckThrowableAndReturnSendValue,
                    instance
                )
            );
            return s2;
        }

        public override MSAst.Expression Reduce() {
            // (yield z) becomes:
            // .comma (1) {
            //    .void ( .yield_statement (_expression) ),
            //    $gen.CheckThrowable() // <-- has return result from send            
            //  }

            return Ast.Block(
                AstUtils.YieldReturn(
                    GeneratorLabel,
                    AstUtils.Convert(_expression, typeof(object))
                ),
                CreateCheckThrowExpression(Span) // emits ($gen.CheckThrowable())
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

        public override string NodeName {
            get {
                return "yield expression";
            }
        }
    }
}
