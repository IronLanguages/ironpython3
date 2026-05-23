// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

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
#if FEATURE_NET_ASYNC
        private static readonly System.Reflection.MethodInfo s_captureMethod
            = typeof(System.Runtime.ExceptionServices.ExceptionDispatchInfo).GetMethod(nameof(System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture))!;
        private static readonly System.Reflection.MethodInfo s_throwMethod
            = typeof(System.Runtime.ExceptionServices.ExceptionDispatchInfo).GetMethod(nameof(System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw), Type.EmptyTypes)!;
#endif

        public YieldExpression(Expression? expression) {
            Expression = expression;
        }

        public Expression? Expression { get; }

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
            MSAst.Expression yieldValue = Expression == null ? AstUtils.Constant(null) : AstUtils.Convert(Expression, typeof(object));

#if FEATURE_NET_ASYNC
            // An async generator (`async def` with `yield`) is lowered via AsyncEnumerableExpression and has no
            // backing PythonGenerator, so there is no `$generator` to call CheckThrowable() on. Instead the
            // resume reads two per-generator cells that PythonAsyncGenerator writes before advancing:
            //   AsyncThrowSlot — if set (athrow/aclose), rethrow it here (preserving stack); cleared first so a
            //                    body that catches it and yields again doesn't re-throw on the next resume.
            //   AsyncSendSlot  — the value of the yield expression: the asend(v) value, or None.
            if (Parent is FunctionDefinition { IsAsync: true } fd) {
                MSAst.ParameterExpression sendSlot = fd.AsyncSendSlot;
                MSAst.ParameterExpression throwSlot = fd.AsyncThrowSlot;
                MSAst.ParameterExpression pending = Ast.Variable(typeof(Exception), "$athrow");
                return Ast.Block(
                    typeof(object),
                    new[] { pending },
                    AstUtils.YieldReturn(GeneratorLabel, yieldValue),
                    Ast.Assign(pending, Ast.Field(throwSlot, nameof(System.Runtime.CompilerServices.StrongBox<Exception>.Value))),
                    Ast.Assign(Ast.Field(throwSlot, nameof(System.Runtime.CompilerServices.StrongBox<Exception>.Value)), Ast.Constant(null, typeof(Exception))),
                    Ast.IfThen(
                        Ast.ReferenceNotEqual(pending, Ast.Constant(null, typeof(Exception))),
                        Ast.Call(Ast.Call(s_captureMethod, pending), s_throwMethod)),
                    Ast.Field(sendSlot, nameof(System.Runtime.CompilerServices.StrongBox<object>.Value))
                );
            }
#endif

            // (yield z) becomes:
            // .comma (1) {
            //    .void ( .yield_statement (_expression) ),
            //    $gen.CheckThrowable() // <-- has return result from send
            //  }
            return Ast.Block(
                AstUtils.YieldReturn(GeneratorLabel, yieldValue),
                CreateCheckThrowExpression(Span) // emits ($gen.CheckThrowable())
            );
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Expression?.Walk(walker);
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
