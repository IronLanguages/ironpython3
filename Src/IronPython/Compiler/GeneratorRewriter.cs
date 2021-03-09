// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.Scripting;
using Microsoft.Scripting.Ast;
using Microsoft.Scripting.Utils;

using IronPython.Runtime;
using IronPython.Runtime.Operations;

using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler {
    /// <summary>
    /// When finding a yield return or yield break, this rewriter flattens out
    /// containing blocks, scopes, and expressions with stack state. All
    /// scopes encountered have their variables promoted to the generator's
    /// closure, so they survive yields.
    /// </summary>
    internal sealed class GeneratorRewriter : DynamicExpressionVisitor {
        private readonly Expression _body;
        private readonly string _name;
        private readonly StrongBox<Type> _tupleType = new StrongBox<Type>(null);
        private readonly StrongBox<int> _tupleSize = new StrongBox<int>();
        private readonly StrongBox<ParameterExpression> _tupleExpr = new StrongBox<ParameterExpression>(null);

        // The one return label, or more than one if we're in a finally
        private readonly Stack<LabelTarget> _returnLabels = new Stack<LabelTarget>();
        private readonly ParameterExpression _gotoRouter;
        private bool _inTryWithFinally;

        private readonly List<YieldMarker> _yields = new List<YieldMarker>();

        private readonly Dictionary<ParameterExpression, DelayedTupleExpression> _vars = new Dictionary<ParameterExpression, DelayedTupleExpression>();
        private readonly List<KeyValuePair<ParameterExpression, DelayedTupleExpression>> _orderedVars = new List<KeyValuePair<ParameterExpression, DelayedTupleExpression>>();

        // Possible optimization: reuse temps. Requires scoping them correctly,
        // and then storing them back in a free list
        private readonly List<ParameterExpression> _temps = new List<ParameterExpression>();
        private Expression _state, _current;
        // These two constants are used internally. They should not conflict
        // with valid yield states.
        private const int GotoRouterYielding = 0;
        private const int GotoRouterNone = -1;
        // The state of the generator before it starts and when it's done
        internal const int NotStarted = -1;
        internal const int Finished = 0;
        internal static ParameterExpression _generatorParam = Expression.Parameter(typeof(PythonGenerator), "$generator");
        
        internal GeneratorRewriter(string name, Expression body) {
            _body = body;
            _name = name;
            _returnLabels.Push(Expression.Label("retLabel"));
            _gotoRouter = Expression.Variable(typeof(int), "$gotoRouter");
        }

        internal Expression Reduce(bool shouldInterpret, bool emitDebugSymbols, int compilationThreshold, 
            IList<ParameterExpression> parameters, Func<Expression<Func<MutableTuple, object>>, 
            Expression<Func<MutableTuple, object>>> bodyConverter) {

            _state = LiftVariable(Expression.Parameter(typeof(int), "state"));
            _current = LiftVariable(Expression.Parameter(typeof(object), "current"));

            // lift the parameters into the tuple
            foreach (ParameterExpression pe in parameters) {
                LiftVariable(pe);
            }
            DelayedTupleExpression liftedGen = LiftVariable(_generatorParam);
            // Visit body
            Expression body = Visit(_body);
            Debug.Assert(_returnLabels.Count == 1);

            // Add the switch statement to the body
            int count = _yields.Count;
            var cases = new SwitchCase[count + 1];
            for (int i = 0; i < count; i++) {
                cases[i] = Expression.SwitchCase(Expression.Goto(_yields[i].Label), AstUtils.Constant(_yields[i].State));
            }
            cases[count] = Expression.SwitchCase(Expression.Goto(_returnLabels.Peek()), AstUtils.Constant(Finished));

            // Create the lambda for the PythonGeneratorNext, hoisting variables
            // into a tuple outside the lambda
            Expression[] tupleExprs = new Expression[_vars.Count];
            foreach (var variable in _orderedVars) {
                // first 2 are our state & out var
                if (variable.Value.Index >= 2 && variable.Value.Index < (parameters.Count + 2)) {
                    tupleExprs[variable.Value.Index] = parameters[variable.Value.Index - 2];
                } else {
                    tupleExprs[variable.Value.Index] = Expression.Default(variable.Key.Type);
                }
            }

            Expression newTuple = MutableTuple.Create(tupleExprs);
            Type tupleType = _tupleType.Value = newTuple.Type;
            _tupleSize.Value = tupleExprs.Length;
            ParameterExpression tupleExpr = _tupleExpr.Value = Expression.Parameter(tupleType, "tuple");
            ParameterExpression tupleArg = Expression.Parameter(typeof(MutableTuple), "tupleArg");
            _temps.Add(_gotoRouter);
            _temps.Add(tupleExpr);

            // temps for the outer lambda
            ParameterExpression tupleTmp = Expression.Parameter(tupleType, "tuple");
            ParameterExpression ret = Expression.Parameter(typeof(PythonGenerator), "ret");

            var innerLambda = Expression.Lambda<Func<MutableTuple, object>>(
                Expression.Block(
                    _temps.ToArray(),
                    Expression.Assign(
                        tupleExpr,
                        Expression.Convert(
                            tupleArg,
                            tupleType
                        )
                    ),
                    Expression.Switch(Expression.Assign(_gotoRouter, _state), cases),
                    body,
                    MakeAssign(_current, AstUtils.Constant(null)),
                    MakeAssign(_state, AstUtils.Constant(Finished)),
                    Expression.Label(_returnLabels.Peek()),
                    _current
                ),
                _name,
                new ParameterExpression[] { tupleArg }
            );
            
            // Generate a call to PythonOps.MakeGeneratorClosure(Tuple data, object generatorCode)
            return Expression.Block(
                new[] { tupleTmp, ret },
                Expression.Assign(
                    ret,
                    Expression.Call(
                        typeof(PythonOps).GetMethod(nameof(PythonOps.MakeGenerator)),
                        parameters[0],
                        Expression.Assign(tupleTmp, newTuple),
                        emitDebugSymbols ?
                            (Expression)bodyConverter(innerLambda) :
                            (Expression)Expression.Constant(
                                new LazyCode<Func<MutableTuple, object>>(
                                    bodyConverter(innerLambda),
                                    shouldInterpret,
                                    compilationThreshold
                                ),
                                typeof(object)
                            )
                    )
                ),
                new DelayedTupleAssign(
                    new DelayedTupleExpression(liftedGen.Index, new StrongBox<ParameterExpression>(tupleTmp), _tupleType, _tupleSize, typeof(PythonGenerator)),
                    ret
                ),
                ret
            );
        }

        private YieldMarker GetYieldMarker(YieldExpression node) {
            YieldMarker result = new YieldMarker(_yields.Count + 1);
            _yields.Add(result);
            Debug.Assert(node.YieldMarker == -1);
            return result;
        }

        /// <summary>
        /// Spills the right side into a temp, and replaces it with its temp.
        /// Returns the expression that initializes the temp.
        /// </summary>
        private Expression ToTemp(ref Expression e) {
            Debug.Assert(e != null);
            var temp = LiftVariable(Expression.Variable(e.Type, "generatorTemp" + _temps.Count));
            var result = MakeAssign(temp, e);
            e = temp;
            return result;
        }

        /// <summary>
        /// Makes an assignment to this variable. Pushes the assignment as far
        /// into the right side as possible, to allow jumps into it.
        /// </summary>
        private Expression MakeAssign(Expression variable, Expression value) {
            // TODO: this is not complete.
            // It may end up generating a bad tree if any of these nodes
            // contain yield and return a value: Switch, Loop, or Goto.
            // Those are not supported, but we can't throw here because we may
            // end up disallowing valid uses (if some other expression contains
            // yield, but not this one).
            switch (value.NodeType) {
                case ExpressionType.Block:
                    return MakeAssignBlock(variable, value);
                case ExpressionType.Conditional:
                    return MakeAssignConditional(variable, value);
                case ExpressionType.Label:
                    return MakeAssignLabel(variable, (LabelExpression)value);
            }
            return DelayedAssign(variable, value);
        }

        private readonly struct GotoRewriteInfo {
            public readonly Expression Variable;
            public readonly LabelTarget VoidTarget;

            public GotoRewriteInfo(Expression variable, LabelTarget voidTarget) {
                Variable = variable;
                VoidTarget = voidTarget;
            }
        }

        private Expression MakeAssignLabel(Expression variable, LabelExpression value) {
            GotoRewriteInfo curVariable = new GotoRewriteInfo(variable, Expression.Label(value.Target.Name + "_voided"));

            var defaultValue = new GotoRewriter(this, curVariable, value.Target).Visit(value.DefaultValue);

            return MakeAssignLabel(variable, curVariable, value.Target, defaultValue);
        }

        private Expression MakeAssignLabel(Expression variable, GotoRewriteInfo curVariable, LabelTarget target, Expression defaultValue) {
            return Expression.Label(
                curVariable.VoidTarget,
                MakeAssign(variable, defaultValue)
            );
        }

        private class GotoRewriter : ExpressionVisitor {
            private readonly GotoRewriteInfo _gotoInfo;
            private readonly LabelTarget _target;
            private readonly GeneratorRewriter _rewriter;
            
            public GotoRewriter(GeneratorRewriter rewriter, GotoRewriteInfo gotoInfo, LabelTarget target) {
                _gotoInfo = gotoInfo;
                _target = target;
                _rewriter = rewriter;
            }

            protected override Expression VisitGoto(GotoExpression node) {
                if (node.Target == _target) {
                    return Expression.Goto(
                        _gotoInfo.VoidTarget,
                        Expression.Block(
                            _rewriter.MakeAssign(_gotoInfo.Variable, node.Value),
                            Expression.Default(typeof(void))
                        ),
                        node.Type
                    );
                }
                return base.VisitGoto(node);
            }
        }

        private Expression MakeAssignBlock(Expression variable, Expression value) {
            var node = (BlockExpression)value;
            var newBlock = new ReadOnlyCollectionBuilder<Expression>(node.Expressions);

            Expression blockRhs = newBlock[newBlock.Count - 1];
            if (blockRhs.NodeType == ExpressionType.Label) {
                var label = (LabelExpression)blockRhs;
                GotoRewriteInfo curVariable = new GotoRewriteInfo(variable, Expression.Label(label.Target.Name + "_voided"));
                
                var rewriter = new GotoRewriter(this, curVariable, label.Target);
                for (int i = 0; i < newBlock.Count - 1; i++) {
                    newBlock[i] = rewriter.Visit(newBlock[i]);
                }
                
                newBlock[newBlock.Count - 1] = MakeAssignLabel(variable, curVariable, label.Target, rewriter.Visit(label.DefaultValue));
            } else {
                newBlock[newBlock.Count - 1] = MakeAssign(variable, newBlock[newBlock.Count - 1]);
            }

            return Expression.Block(node.Variables, newBlock);
        }

        private Expression MakeAssignConditional(Expression variable, Expression value) {
            var node = (ConditionalExpression)value;
            return Expression.Condition(node.Test, MakeAssign(variable, node.IfTrue), MakeAssign(variable, node.IfFalse));
        }

        private BlockExpression ToTemp(ref ReadOnlyCollection<Expression> args) {
            int count = args.Count;
            var block = new Expression[count];
            var newArgs = new Expression[count];
            args.CopyTo(newArgs, 0);
            for (int i = 0; i < count; i++) {
                block[i] = ToTemp(ref newArgs[i]);
            }
            args = new ReadOnlyCollection<Expression>(newArgs);
            return Expression.Block(block);
        }

        #region VisitTry

        protected override Expression VisitTry(TryExpression node) {
            int startYields = _yields.Count;

            bool savedInTryWithFinally = _inTryWithFinally;
            if (node.Finally != null || node.Fault != null) {
                _inTryWithFinally = true;
            }
            Expression @try = Visit(node.Body);
            int tryYields = _yields.Count;

            IList<CatchBlock> handlers = Visit(node.Handlers, VisitCatchBlock);
            int catchYields = _yields.Count;

            // push a new return label in case the finally block yields
            _returnLabels.Push(Expression.Label("tryLabel"));
            // only one of these can be non-null
            Expression @finally = Visit(node.Finally);
            Expression fault = Visit(node.Fault);
            LabelTarget finallyReturn = _returnLabels.Pop();
            int finallyYields = _yields.Count;

            _inTryWithFinally = savedInTryWithFinally;

            if (@try == node.Body &&
                handlers == node.Handlers &&
                @finally == node.Finally &&
                fault == node.Fault) {
                return node;
            }

            // No yields, just return
            if (startYields == _yields.Count) {
                Debug.Assert(@try.Type == node.Type);
                Debug.Assert(handlers == null || handlers.Count == 0 || handlers[0].Body.Type == node.Type);
                return Expression.MakeTry(null, @try, @finally, fault, handlers);
            }

            if (fault != null && finallyYields != catchYields) {
                // No one needs this yet, and it's not clear how we should get back to
                // the fault
                throw new NotSupportedException("yield in fault block is not supported");
            }

            // If try has yields, we need to build a new try body that
            // dispatches to the yield labels
            var tryStart = Expression.Label("tryStart");
            if (tryYields != startYields) {
                @try = Expression.Block(MakeYieldRouter(node.Body.Type, startYields, tryYields, tryStart), @try);
                Debug.Assert(@try.Type == node.Body.Type);
            }

            // Transform catches with yield to deferred handlers
            if (catchYields != tryYields) {
                var block = new List<Expression>();

                block.Add(MakeYieldRouter(node.Body.Type, tryYields, catchYields, tryStart));
                block.Add(null); // empty slot to fill in later

                for (int i = 0, n = handlers.Count; i < n; i++) {
                    CatchBlock c = handlers[i];

                    if (c == node.Handlers[i]) {
                        continue;
                    }

                    if (handlers.IsReadOnly) {
                        handlers = ArrayUtils.ToArray(handlers);
                    }

                    // the variable that will be scoped to the catch block
                    var exceptionVar = Expression.Variable(c.Test, null);

                    // the variable that the catch block body will use to
                    // access the exception. We reuse the original variable if
                    // the catch block had one. It needs to be hoisted because
                    // the catch might contain yields.
                    var deferredVar = c.Variable ?? Expression.Variable(c.Test, null);
                    LiftVariable(deferredVar);                    

                    // We need to ensure that filters can access the exception
                    // variable
                    Expression filter = c.Filter;
                    if (filter != null && c.Variable != null) {
                        filter = Expression.Block(new[] { c.Variable }, Expression.Assign(c.Variable, exceptionVar), filter);
                    }

                    // catch (ExceptionType exceptionVar) {
                    //     deferredVar = exceptionVar;
                    // }
                    handlers[i] = Expression.Catch(
                        exceptionVar,
                        Expression.Block(
                            DelayedAssign(Visit(deferredVar), exceptionVar),
                            Expression.Default(node.Body.Type)
                        ),
                        filter
                    );

                    // We need to rewrite rethrows into "throw deferredVar"
                    var catchBody = new RethrowRewriter { Exception = deferredVar }.Visit(c.Body);
                    
                    // if (deferredVar != null) {
                    //     ... catch body ...
                    // }
                    block.Add(
                        Expression.Condition(
                            Expression.NotEqual(Visit(deferredVar), AstUtils.Constant(null, deferredVar.Type)),
                            catchBody,
                            Expression.Default(node.Body.Type)
                        )
                    );
                }

                block[1] = Expression.MakeTry(null, @try, null, null, new ReadOnlyCollection<CatchBlock>(handlers));
                @try = Expression.Block(block);
                Debug.Assert(@try.Type == node.Body.Type);
                handlers = new CatchBlock[0]; // so we don't reuse these
            }

            if (finallyYields != catchYields) {
                // We need to add a catch block to save the exception, so we
                // can rethrow in case there is a yield in the finally. Also,
                // add logic for returning. It looks like this:
                //
                // try { ... } catch (Exception all) { saved = all; }
                // finally {
                //  if (_finallyReturnVar) goto finallyReturn;
                //   ...
                //   if (saved != null) throw saved;
                //   finallyReturn:
                // }
                // if (_finallyReturnVar) goto _return;

                // We need to add a catch(Exception), so if we have catches,
                // wrap them in a try
                if (handlers.Count > 0) {
                    @try = Expression.MakeTry(null, @try, null, null, handlers);
                    Debug.Assert(@try.Type == node.Body.Type);
                    handlers = new CatchBlock[0];
                }

                // NOTE: the order of these routers is important
                // The first call changes the labels to all point at "tryEnd",
                // so the second router will jump to "tryEnd"
                var tryEnd = Expression.Label("tryEnd");
                Expression inFinallyRouter = MakeYieldRouter(node.Body.Type, catchYields, finallyYields, tryEnd);
                Expression inTryRouter = MakeYieldRouter(node.Body.Type, catchYields, finallyYields, tryStart);

                var all = Expression.Variable(typeof(Exception), "e");
                var saved = Expression.Variable(typeof(Exception), "$saved$" + _temps.Count);
                LiftVariable(saved);
                @try = Expression.Block(
                    Expression.TryCatchFinally(
                        Expression.Block(
                            inTryRouter,
                            @try,
                            DelayedAssign(Visit(saved), AstUtils.Constant(null, saved.Type)),
                            Expression.Label(tryEnd)
                        ),
                        Expression.Block(
                            MakeSkipFinallyBlock(finallyReturn),
                            inFinallyRouter,
                            @finally,
                            Expression.Condition(
                                Expression.NotEqual(Visit(saved), AstUtils.Constant(null, saved.Type)),
                                Expression.Throw(Visit(saved)),
                                Utils.Empty()
                            ),
                            Expression.Label(finallyReturn)
                        ),
                        Expression.Catch(all, Utils.Void(DelayedAssign(Visit(saved), all)))
                    ),
                    Expression.Condition(
                        Expression.Equal(_gotoRouter, AstUtils.Constant(GotoRouterYielding)),
                        Expression.Goto(_returnLabels.Peek()),
                        Utils.Empty()
                    )
                );

                @finally = null;
            } else if (@finally != null) {
                // try or catch had a yield, modify finally so we can skip over it
                @finally = Expression.Block(
                    MakeSkipFinallyBlock(finallyReturn),
                    @finally,
                    Expression.Label(finallyReturn)
                );
            }

            // Make the outer try, if needed
            if (handlers.Count > 0 || @finally != null || fault != null) {
                @try = Expression.MakeTry(null, @try, @finally, fault, handlers);
            }
            Debug.Assert(@try.Type == node.Body.Type);
            return Expression.Block(Expression.Label(tryStart), @try);
        }

        private class RethrowRewriter : ExpressionVisitor {
            internal Expression Exception;

            protected override Expression VisitUnary(UnaryExpression node) {
                if (node.NodeType == ExpressionType.Throw && node.Operand == null) {
                    return Expression.Throw(Exception, node.Type);
                }
                return base.VisitUnary(node);
            }

            protected override Expression VisitLambda<T>(Expression<T> node) {
                return node; // don't recurse into lambdas 
            }

            protected override Expression VisitTry(TryExpression node) {
                return node; // don't recurse into other try's
            }

            protected override Expression VisitExtension(Expression node) {
                if (node is DelayedTupleExpression) {
                    return node;
                }

                return base.VisitExtension(node);
            }
        }

        // Skip the finally block if we are yielding, but not if we're doing a
        // yield break
        private Expression MakeSkipFinallyBlock(LabelTarget target) {
            return Expression.Condition(
                Expression.AndAlso(
                    Expression.Equal(_gotoRouter, AstUtils.Constant(GotoRouterYielding)),
                    Expression.NotEqual(_state, AstUtils.Constant(Finished))
                ),
                Expression.Goto(target),
                Utils.Empty()
            );
        }

        // Mostly copied from the base implementation. 
        // - makes sure we disallow yield in filters
        // - lifts exception variable
        protected override CatchBlock VisitCatchBlock(CatchBlock node) {
            if (node.Variable != null) {
                LiftVariable(node.Variable);
            }

            Expression v = Visit(node.Variable);
            int yields = _yields.Count;
            Expression f = Visit(node.Filter);
            if (yields != _yields.Count) {
                // No one needs this yet, and it's not clear what it should even do
                throw new NotSupportedException("yield in filter is not allowed");
            }
            
            Expression b = Visit(node.Body);
            if (v == node.Variable && b == node.Body && f == node.Filter) {
                return node;
            }

            // if we have variable and no yields in the catch block then
            // we need to hoist the variable into a closure
            if (v != node.Variable && yields == _yields.Count) {
                return Expression.MakeCatchBlock(
                    node.Test,
                    node.Variable,
                    Expression.Block(
                        new DelayedTupleAssign(v, node.Variable),
                        b
                    ),
                    f);
            }

            return Expression.MakeCatchBlock(node.Test, node.Variable, b, f);
        }

        #endregion

        private SwitchExpression MakeYieldRouter(Type type, int start, int end, LabelTarget newTarget) {
            Debug.Assert(end > start);
            var cases = new SwitchCase[end - start];
            for (int i = start; i < end; i++) {
                YieldMarker y = _yields[i];
                cases[i - start] = Expression.SwitchCase(Expression.Goto(y.Label, type), AstUtils.Constant(y.State));
                // Any jumps from outer switch statements should go to the this
                // router, not the original label (which they cannot legally jump to)
                y.Label = newTarget;
            }
            return Expression.Switch(_gotoRouter, Expression.Default(type), cases);
        }

        protected override Expression VisitExtension(Expression node) {
            if (node is YieldExpression yield) {
                return VisitYield(yield);
            }

            if (node is FinallyFlowControlExpression ffc) {
                return Visit(node.ReduceExtensions());
            }

            return Visit(node.ReduceExtensions());
        }

        private Expression VisitYield(YieldExpression node) {
            var value = Visit(node.Value);

            var block = new List<Expression>();

            if (node.YieldMarker == -2) {
                // Yield break with a return value
                block.Add(MakeAssign(_current, value));
                value = null;
            }

            if (value == null) {
                // Yield break
                block.Add(MakeAssign(_state, AstUtils.Constant(Finished)));
                if (_inTryWithFinally) {
                    block.Add(Expression.Assign(_gotoRouter, AstUtils.Constant(GotoRouterYielding)));
                }
                block.Add(Expression.Goto(_returnLabels.Peek()));
                return Expression.Block(block);
            }

            // Yield return
            block.Add(MakeAssign(_current, value));
            YieldMarker marker = GetYieldMarker(node);
            block.Add(MakeAssign(_state, AstUtils.Constant(marker.State)));
            if (_inTryWithFinally) {
                block.Add(Expression.Assign(_gotoRouter, AstUtils.Constant(GotoRouterYielding)));
            }
            block.Add(Expression.Goto(_returnLabels.Peek()));
            block.Add(Expression.Label(marker.Label));
            block.Add(Expression.Assign(_gotoRouter, AstUtils.Constant(GotoRouterNone)));
            block.Add(Utils.Empty());
            return Expression.Block(block);
        }

        protected override Expression VisitBlock(BlockExpression node) {
            // save the variables for later
            // (they'll be hoisted outside of the lambda)
            foreach (ParameterExpression param in node.Variables) {
                LiftVariable(param);
            }

            int yields = _yields.Count;
            var b = Visit(node.Expressions);
            if (b == node.Expressions) {
                return node;
            }
            if (yields == _yields.Count) {
                return Expression.Block(node.Type, node.Variables, b);
            }


            // Return a new block expression with the rewritten body except for that
            // all the variables are removed.
            return Expression.Block(node.Type, b);
        }

        private DelayedTupleExpression LiftVariable(ParameterExpression param) {
            DelayedTupleExpression res;
            if (!_vars.TryGetValue(param, out res)) {
                _vars[param] = res = new DelayedTupleExpression(_vars.Count, _tupleExpr, _tupleType, _tupleSize, param.Type);
                _orderedVars.Add(new KeyValuePair<ParameterExpression, DelayedTupleExpression>(param, res));
            }

            return res;
        }

        protected override Expression VisitParameter(ParameterExpression node) {
            return _vars[node];
        }

        protected override Expression VisitLambda<T>(Expression<T> node) {
            // don't recurse into nested lambdas
            return node;
        }

        #region stack spilling (to permit yield in the middle of an expression)

        private Expression VisitAssign(BinaryExpression node) {
            int yields = _yields.Count;
            Expression left = Visit(node.Left);
            Expression right = Visit(node.Right);
            if (left == node.Left && right == node.Right) {
                return node;
            }
            if (yields == _yields.Count) {
                if (left is DelayedTupleExpression) {
                    return new DelayedTupleAssign(left, right);
                }
                return Expression.Assign(left, right);
            }

            var block = new List<Expression>();

            // If the left hand side did not rewrite itself, we may still need
            // to rewrite to ensure proper evaluation order. Essentially, we
            // want all of the left side evaluated first, then the value, then
            // the assignment
            if (left == node.Left) {
                switch (left.NodeType) {
                    case ExpressionType.MemberAccess:
                        var member = (MemberExpression)node.Left;
                        Expression e = Visit(member.Expression);
                        block.Add(ToTemp(ref e));
                        left = Expression.MakeMemberAccess(e, member.Member);
                        break;
                    case ExpressionType.Index:
                        var index = (IndexExpression)node.Left;
                        Expression o = Visit(index.Object);
                        ReadOnlyCollection<Expression> a = Visit(index.Arguments);
                        if (o == index.Object && a == index.Arguments) {
                            return index;
                        }
                        block.Add(ToTemp(ref o));
                        block.Add(ToTemp(ref a));
                        left = Expression.MakeIndex(o, index.Indexer, a);
                        break;
                    case ExpressionType.Parameter:
                        // no action needed
                        break;
                    default:
                        // Extension should've been reduced by Visit above,
                        // and returned a different node
                        throw Assert.Unreachable;
                }
            } else if (left is BlockExpression) {
                // Get the last expression of the rewritten left side
                var leftBlock = (BlockExpression)left;
                left = leftBlock.Expressions[leftBlock.Expressions.Count - 1];
                block.AddRange(leftBlock.Expressions);
                block.RemoveAt(block.Count - 1);
            }

            if (right != node.Right) {
                block.Add(ToTemp(ref right));
            }

            if (left is DelayedTupleExpression) {
                block.Add(DelayedAssign(left, right));
            } else {
                block.Add(Expression.Assign(left, right));
            }
            return Expression.Block(block);
        }

        protected override Expression VisitDynamic(DynamicExpression node) {
            int yields = _yields.Count;
            ReadOnlyCollection<Expression> a = Visit(node.Arguments);
            if (a == node.Arguments) {
                return node;
            }
            if (yields == _yields.Count) {
                return DynamicExpression.MakeDynamic(node.DelegateType, node.Binder, a);
            }
            return Expression.Block(
                ToTemp(ref a),
                DynamicExpression.MakeDynamic(node.DelegateType, node.Binder, a)
            );
        }

        protected override Expression VisitIndex(IndexExpression node) {
            int yields = _yields.Count;
            Expression o = Visit(node.Object);
            ReadOnlyCollection<Expression> a = Visit(node.Arguments);
            if (o == node.Object && a == node.Arguments) {
                return node;
            }
            if (yields == _yields.Count) {
                return Expression.MakeIndex(o, node.Indexer, a);
            }
            return Expression.Block(
                ToTemp(ref o),
                ToTemp(ref a),
                Expression.MakeIndex(o, node.Indexer, a)
            );
        }

        protected override Expression VisitInvocation(InvocationExpression node) {
            int yields = _yields.Count;
            Expression e = Visit(node.Expression);
            ReadOnlyCollection<Expression> a = Visit(node.Arguments);
            if (e == node.Expression && a == node.Arguments) {
                return node;
            }
            if (yields == _yields.Count) {
                return Expression.Invoke(e, a);
            }
            return Expression.Block(
                ToTemp(ref e),
                ToTemp(ref a),
                Expression.Invoke(e, a)
            );
        }

        protected override Expression VisitMethodCall(MethodCallExpression node) {
            int yields = _yields.Count;
            Expression o = Visit(node.Object);
            ReadOnlyCollection<Expression> a = Visit(node.Arguments);
            if (o == node.Object && a == node.Arguments) {
                return node;
            }
            if (yields == _yields.Count) {
                return Expression.Call(o, node.Method, a);
            }
            if (o == null) {
                return Expression.Block(
                    ToTemp(ref a),
                    Expression.Call(null, node.Method, a)
                );
            }
            return Expression.Block(
                ToTemp(ref o),
                ToTemp(ref a),
                Expression.Call(o, node.Method, a)
            );
        }

        protected override Expression VisitNew(NewExpression node) {
            int yields = _yields.Count;
            ReadOnlyCollection<Expression> a = Visit(node.Arguments);
            if (a == node.Arguments) {
                return node;
            }
            if (yields == _yields.Count) {
                return (node.Members != null)
                    ? Expression.New(node.Constructor, a, node.Members)
                    : Expression.New(node.Constructor, a);
            }
            return Expression.Block(
                ToTemp(ref a),
                (node.Members != null)
                    ? Expression.New(node.Constructor, a, node.Members)
                    : Expression.New(node.Constructor, a)
            );
        }

        protected override Expression VisitNewArray(NewArrayExpression node) {
            int yields = _yields.Count;
            ReadOnlyCollection<Expression> e = Visit(node.Expressions);
            if (e == node.Expressions) {
                return node;
            }
            if (yields == _yields.Count) {
                return (node.NodeType == ExpressionType.NewArrayInit)
                    ? Expression.NewArrayInit(node.Type.GetElementType(), e)
                    : Expression.NewArrayBounds(node.Type.GetElementType(), e);
            }
            return Expression.Block(
                ToTemp(ref e),
                (node.NodeType == ExpressionType.NewArrayInit)
                    ? Expression.NewArrayInit(node.Type.GetElementType(), e)
                    : Expression.NewArrayBounds(node.Type.GetElementType(), e)
            );
        }

        protected override Expression VisitMember(MemberExpression node) {
            int yields = _yields.Count;
            Expression e = Visit(node.Expression);
            if (e == node.Expression) {
                return node;
            }
            if (yields == _yields.Count) {
                return Expression.MakeMemberAccess(e, node.Member);
            }
            return Expression.Block(
                ToTemp(ref e),
                Expression.MakeMemberAccess(e, node.Member)
            );
        }

        protected override Expression VisitBinary(BinaryExpression node) {
            if (node.NodeType == ExpressionType.Assign) {
                return VisitAssign(node);
            }
            // For OpAssign nodes: if has a yield, we need to do the generator
            // transformation on the reduced value.
            if (node.CanReduce) {
                return Visit(node.Reduce());
            }

            int yields = _yields.Count;
            Expression left = Visit(node.Left);
            Expression right = Visit(node.Right);
            if (left == node.Left && right == node.Right) {
                return node;
            }
            if (yields == _yields.Count) {
                return Expression.MakeBinary(node.NodeType, left, right, node.IsLiftedToNull, node.Method, node.Conversion);
            }

            return Expression.Block(
                ToTemp(ref left),
                ToTemp(ref right),
                Expression.MakeBinary(node.NodeType, left, right, node.IsLiftedToNull, node.Method, node.Conversion)
            );
        }

        protected override Expression VisitTypeBinary(TypeBinaryExpression node) {
            int yields = _yields.Count;
            Expression e = Visit(node.Expression);
            if (e == node.Expression) {
                return node;
            }
            if (yields == _yields.Count) {
                return (node.NodeType == ExpressionType.TypeIs)
                    ? Expression.TypeIs(e, node.TypeOperand)
                    : Expression.TypeEqual(e, node.TypeOperand);
            }
            return Expression.Block(
                ToTemp(ref e),
                (node.NodeType == ExpressionType.TypeIs)
                    ? Expression.TypeIs(e, node.TypeOperand)
                    : Expression.TypeEqual(e, node.TypeOperand)
            );
        }

        protected override Expression VisitUnary(UnaryExpression node) {
            // For OpAssign nodes: if has a yield, we need to do the generator
            // transformation on the reduced value.
            if (node.CanReduce) {
                return Visit(node.Reduce());
            }

            int yields = _yields.Count;
            Expression o = Visit(node.Operand);
            if (o == node.Operand) {
                return node;
            }
            // Void convert can be jumped into, no need to spill
            // TODO: remove when that feature goes away.
            if (yields == _yields.Count ||
                (node.NodeType == ExpressionType.Convert && node.Type == typeof(void))) {
                return Expression.MakeUnary(node.NodeType, o, node.Type, node.Method);
            }
            return Expression.Block(
                ToTemp(ref o),
                Expression.MakeUnary(node.NodeType, o, node.Type, node.Method)
            );
        }

        protected override Expression VisitMemberInit(MemberInitExpression node) {
            // See if anything changed
            int yields = _yields.Count;
            Expression e = base.VisitMemberInit(node);
            if (yields == _yields.Count) {
                return e;
            }
            // It has a yield. Reduce to basic nodes so we can jump in
            return e.Reduce();
        }

        protected override Expression VisitListInit(ListInitExpression node) {
            // See if anything changed
            int yields = _yields.Count;
            Expression e = base.VisitListInit(node);
            if (yields == _yields.Count) {
                return e;
            }
            // It has a yield. Reduce to basic nodes so we can jump in
            return e.Reduce();
        }


        private static Expression DelayedAssign(Expression lhs, Expression rhs) {
            return new DelayedTupleAssign(lhs, rhs);
        }

        #endregion

        private sealed class YieldMarker {
            // Note: Label can be mutated as we generate try blocks
            internal LabelTarget Label = Expression.Label("yieldMarker");
            internal readonly int State;

            internal YieldMarker(int state) {
                State = state;
            }
        }        
    }

    /// <summary>
    /// Accesses the property of a tuple.  The node can be created first and then the tuple and index
    /// type can be filled in before the tree is actually generated.  This enables creation of these
    /// nodes before the tuple type is actually known.
    /// </summary>
    internal sealed class DelayedTupleExpression : Expression {
        public readonly int Index;
        private readonly StrongBox<Type> _tupleType;
        private readonly StrongBox<int> _tupleSize;
        private readonly StrongBox<ParameterExpression> _tupleExpr;
        private readonly Type _type;

        public DelayedTupleExpression(int index, StrongBox<ParameterExpression> tupleExpr, StrongBox<Type> tupleType, StrongBox<int> tupleSize, Type type) {
            Index = index;
            _tupleType = tupleType;
            _tupleSize = tupleSize;
            _tupleExpr = tupleExpr;
            _type = type;
        }

        public override Expression Reduce() {
            Expression res = _tupleExpr.Value;
            foreach (PropertyInfo pi in MutableTuple.GetAccessPath(_tupleType.Value, _tupleSize.Value, Index)) {
                res = Expression.Property(res, pi);
            }
            return res;
        }

        public sealed override ExpressionType NodeType {
            get { return ExpressionType.Extension; }
        }

        public sealed override Type/*!*/ Type {
            get { return _type; }
        }

        public override bool CanReduce {
            get {
                return true;
            }
        }

        protected override Expression VisitChildren(ExpressionVisitor visitor) {
            return this;
        }
    }

    internal sealed class DelayedTupleAssign : Expression {
        private readonly Expression _lhs, _rhs;
        
        public DelayedTupleAssign(Expression lhs, Expression rhs) {
            _lhs = lhs;
            _rhs = rhs;
        }

        public override Expression Reduce() {
            // we assign to a temporary and then assign that to the tuple
            // because there may be branches in the RHS which can cause
            // us to not have the tuple instance
            return Expression.Assign(_lhs.Reduce(), _rhs);
        }

        public sealed override ExpressionType NodeType {
            get { return ExpressionType.Extension; }
        }

        public sealed override Type/*!*/ Type {
            get { return _lhs.Type; }
        }

        public override bool CanReduce {
            get {
                return true;
            }
        }

        protected override Expression VisitChildren(ExpressionVisitor visitor) {
            Expression rhs = visitor.Visit(_rhs);
            if (rhs != _rhs) {
                return new DelayedTupleAssign(_lhs, rhs);
            }

            return this;
        }
    }

    internal sealed class PythonGeneratorExpression : Expression {
        private readonly LightLambdaExpression _lambda;
        private readonly int _compilationThreshold;

        public PythonGeneratorExpression(LightLambdaExpression lambda, int compilationThreshold) {
            _lambda = lambda;
            _compilationThreshold = compilationThreshold;
        }

        public override Expression Reduce() {
            return _lambda.ToGenerator(false, true, _compilationThreshold);
        }

        public sealed override ExpressionType NodeType {
            get { return ExpressionType.Extension; }
        }

        public sealed override Type/*!*/ Type {
            get { return _lambda.Type; }
        }

        public override bool CanReduce {
            get {
                return true;
            }
        }
    }

}
