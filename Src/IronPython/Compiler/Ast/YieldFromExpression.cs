// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using MSAst = System.Linq.Expressions;

using System;
using System.Diagnostics;
using Microsoft.Scripting;
using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class YieldFromExpression : Expression {
        private readonly Statement statement;
        private readonly NameExpression result;

        public YieldFromExpression(Expression expression) {
            statement = GenYieldFromStatement(expression);
            result = new NameExpression("__yieldfromprefix_r") { Parent = expression.Parent };

            Expression = expression;
        }

        public Expression Expression { get; }

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
            import sys
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

        private static Statement GenYieldFromStatement(Expression expression) {
            var expr = Modules._ast.ConvertToPythonAst(Runtime.DefaultContext.Default, yieldFromAst, "").Body;
            Modules._ast._containsYield = false; // reset state of _ast module
            expr.Parent = expression.Parent;

            return new SuiteStatement(new[] {
                new AssignmentStatement(new[] { new NameExpression("__yieldfromprefix_EXPR") { Parent = expression.Parent } }, expression) { Parent = expression.Parent },
                    expr
                }) { Parent = expression.Parent };
        }

        public override MSAst.Expression Reduce() {
            return Ast.Block(
                typeof(object),
                statement,
                AstUtils.Convert(result, typeof(object))
            ).Reduce();
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Expression?.Walk(walker);
                statement.Walk(walker);
                result.Walk(walker);
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
