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
        public YieldExpression(Expression expression, bool isYieldFrom = false) {
            Expression = expression;
            IsYieldFrom = isYieldFrom;

            if (isYieldFrom) {
                YieldFromStatement = GenYieldFromStatement(isYieldFrom, expression);
                YieldFromResult = new NameExpression("__yieldfromprefix_r") { Parent = expression.Parent };
            }
        }

        public Expression Expression { get; }

        internal bool IsYieldFrom { get; }

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

        private static readonly Modules._ast.AST yieldFromAst = (Modules._ast.AST)Modules.Builtin.compile(Runtime.DefaultContext.Default, @"
__yieldfromprefix_i = iter(__yieldfromprefix_EXPR)
try:
    __yieldfromprefix_y = next(__yieldfromprefix_i)
except StopIteration as __yieldfromprefix_e:
    __yieldfromprefix_r = __yieldfromprefix_e.value
else:
    while 1:
        try:
            __yieldfromprefix_s = yield __yieldfromprefix_y
        except GeneratorExit as __yieldfromprefix_e:
            try:
                __yieldfromprefix_m = __yieldfromprefix_i.close
            except AttributeError:
                pass
            else:
                __yieldfromprefix_m()
            raise __yieldfromprefix_e
        except BaseException as __yieldfromprefix_e:
            __yieldfromprefix_x = sys.exc_info()
            try:
                __yieldfromprefix_m = __yieldfromprefix_i.throw
            except AttributeError:
                raise __yieldfromprefix_e
            else:
                try:
                    __yieldfromprefix_y = __yieldfromprefix_m(*__yieldfromprefix_x)
                except StopIteration as __yieldfromprefix_e:
                    __yieldfromprefix_r = __yieldfromprefix_e.value
                    break
        else:
            try:
                if __yieldfromprefix_s is None:
                    __yieldfromprefix_y = next(__yieldfromprefix_i)
                else:
                    __yieldfromprefix_y = __yieldfromprefix_i.send(__yieldfromprefix_s)
            except StopIteration as __yieldfromprefix_e:
                __yieldfromprefix_r = __yieldfromprefix_e.value
                break
", "", "exec", flags: Modules._ast.PyCF_ONLY_AST);

        private static Statement GenYieldFromStatement(bool isYieldFrom, Expression expression) {
            if (!isYieldFrom) return null;

            var expr = Modules._ast.ConvertToPythonAst(Runtime.DefaultContext.Default, yieldFromAst, "").Body;
            expr.Parent = expression.Parent;

            return new SuiteStatement(new[] {
                new AssignmentStatement(new[] { new NameExpression("__yieldfromprefix_EXPR") { Parent = expression.Parent } }, expression) { Parent = expression.Parent },
                    expr
                }) { Parent = expression.Parent };
        }

        private Statement YieldFromStatement { get; }

        private NameExpression YieldFromResult { get; }

        public override MSAst.Expression Reduce() {
            if (IsYieldFrom) {
                return Ast.Block(
                    typeof(object),
                    YieldFromStatement,
                    AstUtils.Convert(YieldFromResult, typeof(object))
                ).Reduce();
            }

            // (yield z) becomes:
            // .comma (1) {
            //    .void ( .yield_statement (_expression) ),
            //    $gen.CheckThrowable() // <-- has return result from send            
            //  }
            return Ast.Block(
                AstUtils.YieldReturn(
                    GeneratorLabel,
                    AstUtils.Convert(Expression, typeof(object))
                ),
                CreateCheckThrowExpression(Span) // emits ($gen.CheckThrowable())
            );
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Expression?.Walk(walker);
                YieldFromStatement?.Walk(walker);
                YieldFromResult?.Walk(walker);
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
