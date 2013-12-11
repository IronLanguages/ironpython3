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
using System.Diagnostics;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Binding;
using IronPython.Runtime.Operations;

#if FEATURE_CORE_DLR
using MSAst = System.Linq.Expressions;
#else
using MSAst = Microsoft.Scripting.Ast;
#endif

using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class AssignmentStatement : Statement {
        // _left.Length is 1 for simple assignments like "x = 1"
        // _left.Length will be 3 for "x = y = z = 1"
        private readonly Expression[] _left;
        private readonly Expression _right;

        public AssignmentStatement(Expression[] left, Expression right) {
            _left = left;
            _right = right;
        }

        public IList<Expression> Left {
            get { return _left; }
        }

        public Expression Right {
            get { return _right; }
        }

        public override MSAst.Expression Reduce() {
            if (_left.Length == 1) {
                // Do not need temps for simple assignment
                return AssignOne();
            } else {
                return AssignComplex(_right);
            }
        }

        private MSAst.Expression AssignComplex(MSAst.Expression right) {
            // Python assignment semantics:
            // - only evaluate RHS once. 
            // - evaluates assignment from left to right
            // - does not evaluate getters.
            // 
            // So 
            //   a=b[c]=d=f() 
            // should be:
            //   $temp = f()
            //   a = $temp
            //   b[c] = $temp
            //   d = $temp

            List<MSAst.Expression> statements = new List<MSAst.Expression>();

            // 1. Create temp variable for the right value
            MSAst.ParameterExpression right_temp = Expression.Variable(typeof(object), "assignment");

            // 2. right_temp = right
            statements.Add(MakeAssignment(right_temp, right));

            // Do left to right assignment
            foreach (Expression e in _left) {
                if (e == null) {
                    continue;
                }

                // 3. e = right_temp
                MSAst.Expression transformed = e.TransformSet(Span, right_temp, PythonOperationKind.None);

                statements.Add(transformed);
            }

            // 4. Create and return the resulting suite
            statements.Add(AstUtils.Empty());
            return GlobalParent.AddDebugInfoAndVoid(
                Ast.Block(new[] { right_temp }, statements.ToArray()),
                Span
            );
        }

        private MSAst.Expression AssignOne() {
            Debug.Assert(_left.Length == 1);

            SequenceExpression seLeft = _left[0] as SequenceExpression;
            SequenceExpression seRight = _right as SequenceExpression;

            if (seLeft != null && seRight != null && seLeft.Items.Count == seRight.Items.Count) {
                int cnt = seLeft.Items.Count;
                
                // a, b = 1, 2, or [a,b] = 1,2 - not something like a, b = range(2)
                // we can do a fast parallel assignment
                MSAst.ParameterExpression[] tmps = new MSAst.ParameterExpression[cnt];
                MSAst.Expression[] body = new MSAst.Expression[cnt * 2 + 1];

                // generate the body, the 1st n are the temporary assigns, the
                // last n are the assignments to the left hand side
                // 0: tmp0 = right[0]
                // ...
                // n-1: tmpn-1 = right[n-1]
                // n: right[0] = tmp0
                // ...
                // n+n-1: right[n-1] = tmpn-1

                // allocate the temps first before transforming so we don't pick up a bad temp...
                for (int i = 0; i < cnt; i++) {
                    MSAst.Expression tmpVal = seRight.Items[i];
                    tmps[i] = Ast.Variable(tmpVal.Type, "parallelAssign");

                    body[i] = Ast.Assign(tmps[i], tmpVal);
                }

                // then transform which can allocate more temps
                for (int i = 0; i < cnt; i++) {
                    body[i + cnt] = seLeft.Items[i].TransformSet(SourceSpan.None, tmps[i], PythonOperationKind.None);
                }
                
                // 4. Create and return the resulting suite
                body[cnt * 2] = AstUtils.Empty();
                return GlobalParent.AddDebugInfoAndVoid(Ast.Block(tmps, body), Span);
            }

            return _left[0].TransformSet(Span, _right, PythonOperationKind.None);
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                foreach (Expression e in _left) {
                    e.Walk(walker);
                }
                _right.Walk(walker);
            }
            walker.PostWalk(this);
        }
    }
}
