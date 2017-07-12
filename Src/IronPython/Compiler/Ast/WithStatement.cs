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

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Binding;

using MSAst = System.Linq.Expressions;

using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class WithStatement : Statement {
        private int _headerIndex;
        private readonly Expression _contextManager;
        private readonly Expression _var;
        private Statement _body;

        public WithStatement(Expression contextManager, Expression var, Statement body) {
            _contextManager = contextManager;
            _var = var;
            _body = body;
        }

        public int HeaderIndex {
            set { _headerIndex = value; }
        }

        public new Expression Variable {
            get { return _var; }
        }

        public Expression ContextManager {
            get { return _contextManager; }
        }

        public Statement Body {
            get { return _body; }
        }

        /// <summary>
        /// WithStatement is translated to the DLR AST equivalent to
        /// the following Python code snippet (from with statement spec):
        /// 
        /// mgr = (EXPR)
        /// exit = mgr.__exit__  # Not calling it yet
        /// value = mgr.__enter__()
        /// exc = True
        /// try:
        ///     VAR = value  # Only if "as VAR" is present
        ///     BLOCK
        /// except:
        ///     # The exceptional case is handled here
        ///     exc = False
        ///     if not exit(*sys.exc_info()):
        ///         raise
        ///     # The exception is swallowed if exit() returns true
        /// finally:
        ///     # The normal and non-local-goto cases are handled here
        ///     if exc:
        ///         exit(None, None, None)
        /// 
        /// </summary>
        public override MSAst.Expression Reduce() {
            // Five statements in the result...
            ReadOnlyCollectionBuilder<MSAst.Expression> statements = new ReadOnlyCollectionBuilder<MSAst.Expression>(6);
            ReadOnlyCollectionBuilder<MSAst.ParameterExpression> variables = new ReadOnlyCollectionBuilder<MSAst.ParameterExpression>(6);
            MSAst.ParameterExpression lineUpdated = Ast.Variable(typeof(bool), "$lineUpdated_with");
            variables.Add(lineUpdated);

            //******************************************************************
            // 1. mgr = (EXPR)
            //******************************************************************
            MSAst.ParameterExpression manager = Ast.Variable(typeof(object), "with_manager");
            variables.Add(manager);
            statements.Add(
                GlobalParent.AddDebugInfo(
                    Ast.Assign(
                        manager,
                        _contextManager
                    ),
                    new SourceSpan(GlobalParent.IndexToLocation(StartIndex), GlobalParent.IndexToLocation(_headerIndex))
                )
            );

            //******************************************************************
            // 2. exit = mgr.__exit__  # Not calling it yet
            //******************************************************************
            MSAst.ParameterExpression exit = Ast.Variable(typeof(object), "with_exit");
            variables.Add(exit);
            statements.Add(
                MakeAssignment(
                    exit,
                    GlobalParent.Get(
                        "__exit__",
                        manager
                    )
                )
            );

            //******************************************************************
            // 3. value = mgr.__enter__()
            //******************************************************************
            MSAst.ParameterExpression value = Ast.Variable(typeof(object), "with_value");
            variables.Add(value);
            statements.Add(
                GlobalParent.AddDebugInfoAndVoid(
                    MakeAssignment(
                        value,
                        Parent.Invoke(
                            new CallSignature(0),
                            Parent.LocalContext,
                            GlobalParent.Get(
                                "__enter__",
                                manager
                            )
                        )
                    ),
                    new SourceSpan(GlobalParent.IndexToLocation(StartIndex), GlobalParent.IndexToLocation(_headerIndex))
                )
            );

            //******************************************************************
            // 4. exc = True
            //******************************************************************
            MSAst.ParameterExpression exc = Ast.Variable(typeof(bool), "with_exc");
            variables.Add(exc);
            statements.Add(
                MakeAssignment(
                    exc,
                    AstUtils.Constant(true)
                )
            );

            //******************************************************************
            //  5. The final try statement:
            //
            //  try:
            //      VAR = value  # Only if "as VAR" is present
            //      BLOCK
            //  except:
            //      # The exceptional case is handled here
            //      exc = False
            //      if not exit(*sys.exc_info()):
            //          raise
            //      # The exception is swallowed if exit() returns true
            //  finally:
            //      # The normal and non-local-goto cases are handled here
            //      if exc:
            //          exit(None, None, None)
            //******************************************************************

            MSAst.ParameterExpression exception;
            statements.Add(
                // try:
                AstUtils.Try(
                    AstUtils.Try(// try statement body
                        PushLineUpdated(false, lineUpdated),
                        _var != null ?
                            (MSAst.Expression)Ast.Block(
                // VAR = value
                                _var.TransformSet(SourceSpan.None, value, PythonOperationKind.None),
                // BLOCK
                                _body,
                                AstUtils.Empty()
                            ) :
                // BLOCK
                            (MSAst.Expression)_body // except:, // try statement location
                    ).Catch(exception = Ast.Variable(typeof(Exception), "exception"),
                // Python specific exception handling code
                        TryStatement.GetTracebackHeader(
                            this,
                            exception,
                            GlobalParent.AddDebugInfoAndVoid(
                                Ast.Block(
                // exc = False
                                    MakeAssignment(
                                        exc,
                                        AstUtils.Constant(false)
                                    ),
                //  if not exit(*sys.exc_info()):
                //      raise
                                    AstUtils.IfThen(
                                        GlobalParent.Convert(
                                            typeof(bool),
                                            ConversionResultKind.ExplicitCast,
                                            GlobalParent.Operation(
                                                typeof(bool),
                                                PythonOperationKind.IsFalse,
                                                MakeExitCall(exit, exception)
                                            )
                                        ),
                                        UpdateLineUpdated(true),
                                        Ast.Throw(
                                            Ast.Call(
                                                AstMethods.MakeRethrowExceptionWorker,
                                                exception
                                            )
                                        )
                                    )
                                ),
                                _body.Span
                            )
                        ),
                        PopLineUpdated(lineUpdated),
                        Ast.Empty()
                    )
                // finally:                    
                ).Finally(
                //  if exc:
                //      exit(None, None, None)
                    AstUtils.IfThen(
                        exc,
                        GlobalParent.AddDebugInfoAndVoid(
                            Ast.Block(
                                MSAst.DynamicExpression.Dynamic(
                                    GlobalParent.PyContext.Invoke(
                                        new CallSignature(3)        // signature doesn't include function
                                    ),
                                    typeof(object),
                                    new MSAst.Expression[] {
                                        Parent.LocalContext,
                                        exit,
                                        AstUtils.Constant(null),
                                        AstUtils.Constant(null),
                                        AstUtils.Constant(null)
                                    }
                                ),
                                Ast.Empty()
                            ),
                            _contextManager.Span
                        )
                    )
                )
            );

            statements.Add(AstUtils.Empty());
            return Ast.Block(variables.ToReadOnlyCollection(), statements.ToReadOnlyCollection());
        }

        private MSAst.Expression MakeExitCall(MSAst.ParameterExpression exit, MSAst.Expression exception) {
            // The 'with' statement's exceptional clause explicitly does not set the thread's current exception information.
            // So while the pseudo code says:
            //    exit(*sys.exc_info())
            // we'll actually do:
            //    exit(*PythonOps.GetExceptionInfoLocal($exception))
            return GlobalParent.Convert(
                typeof(bool),
                ConversionResultKind.ExplicitCast,
                Parent.Invoke(
                    new CallSignature(ArgumentType.List),
                    Parent.LocalContext,
                    exit,
                    Ast.Call(
                        AstMethods.GetExceptionInfoLocal,
                        Parent.LocalContext,
                        exception
                    )
                )
            );
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_contextManager != null) {
                    _contextManager.Walk(walker);
                }
                if (_var != null) {
                    _var.Walk(walker);
                }
                if (_body != null) {
                    _body.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }
    }
}
