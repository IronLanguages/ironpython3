// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;

using IronPython.Runtime.Binding;

using Microsoft.Scripting;

using AstUtils = Microsoft.Scripting.Ast.Utils;
using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class ForStatement : Statement, ILoopStatement {
        public ForStatement(Expression left, Expression list, Statement body, Statement else_) {
            Left = left;
            List = list;
            Body = body;
            Else = else_;
        }

        public int HeaderIndex { private get; set; }

        public Expression Left { get; }

        public Statement Body { get; set; }

        public Expression List { get; set; }

        public Statement Else { get; }

        MSAst.LabelTarget ILoopStatement.BreakLabel { get; set; }

        MSAst.LabelTarget ILoopStatement.ContinueLabel { get; set; }

        public override MSAst.Expression Reduce() {
            // Temporary variable for the IEnumerator object
            MSAst.ParameterExpression enumerator = Ast.Variable(typeof(KeyValuePair<IEnumerator, IDisposable>), "foreach_enumerator");

            return Ast.Block(new[] { enumerator }, TransformFor(Parent, enumerator, List, Left, Body, Else, Span, GlobalParent.IndexToLocation(HeaderIndex), ((ILoopStatement)this).BreakLabel, ((ILoopStatement)this).ContinueLabel, true));
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Left?.Walk(walker);
                List?.Walk(walker);
                Body?.Walk(walker);
                Else?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        internal static MSAst.Expression TransformFor(ScopeStatement parent, MSAst.ParameterExpression enumerator,
                                                    Expression list, Expression left, MSAst.Expression body,
                                                    Statement else_, SourceSpan span, SourceLocation header,
                                                    MSAst.LabelTarget breakLabel, MSAst.LabelTarget continueLabel, bool isStatement) {
            // enumerator, isDisposable = Dynamic(GetEnumeratorBinder, list)
            MSAst.Expression init = Ast.Assign(
                    enumerator,
                    new PythonDynamicExpression1<KeyValuePair<IEnumerator, IDisposable>>(
                        Binders.UnaryOperationBinder(
                            parent.GlobalParent.PyContext,
                            PythonOperationKind.GetEnumeratorForIteration
                        ),
                        parent.GlobalParent.CompilationMode,
                        AstUtils.Convert(list, typeof(object))
                    )
                );

            // while enumerator.MoveNext():
            //    left = enumerator.Current
            //    body
            // else:
            //    else
            MSAst.Expression ls = AstUtils.Loop(
                    parent.GlobalParent.AddDebugInfo(
                        Ast.Call(
                            Ast.Property(
                                enumerator,
                                typeof(KeyValuePair<IEnumerator, IDisposable>).GetProperty("Key")
                            ),
                            typeof(IEnumerator).GetMethod("MoveNext")
                        ),
                        left.Span
                    ),
                    null,
                    Ast.Block(
                        left.TransformSet(
                            SourceSpan.None,
                            Ast.Call(
                                Ast.Property(
                                    enumerator,
                                    typeof(KeyValuePair<IEnumerator, IDisposable>).GetProperty("Key")
                                ),
                                typeof(IEnumerator).GetProperty("Current").GetGetMethod()
                            ),
                            PythonOperationKind.None
                        ),
                        body,
                        isStatement ? UpdateLineNumber(parent.GlobalParent.IndexToLocation(list.StartIndex).Line) : AstUtils.Empty(),
                        AstUtils.Empty()
                    ),
                    else_,
                    breakLabel,
                    continueLabel
            );

            return Ast.Block(
                init,
                Ast.TryFinally(
                    ls,
                    Ast.Block(
                        Ast.Call(AstMethods.ForLoopDispose, enumerator),
                        Ast.Assign(enumerator, Ast.New(typeof(KeyValuePair<IEnumerator, IDisposable>)))
                    )
                )
            );
        }

        internal override bool CanThrow {
            get {
                if (Left.CanThrow) {
                    return true;
                }

                if (List.CanThrow) {
                    return true;
                }

                // most constants (int, float, long, etc...) will throw here
                if (List is ConstantExpression ce) {
                    if (ce.Value is string) {
                        return false;
                    }
                    return true;
                }

                return false;
            }
        }
    }
}
