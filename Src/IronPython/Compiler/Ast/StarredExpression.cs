// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;

using IronPython.Runtime;
using IronPython.Runtime.Binding;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {
    public class StarredExpression : Expression {
        public StarredExpression(Expression value) {
            if (value is null) throw new ArgumentNullException(nameof(value));
            Value = value;
        }

        public Expression Value { get; }

        public override bool CanReduce => false;

        internal override MSAst.Expression TransformSet(SourceSpan span, MSAst.Expression right, PythonOperationKind op)
            => Value.TransformSet(span, right, op);

        internal override string CheckAssign() => Value.CheckAssign();

        internal override string CheckDelete() => "can use starred expression only as assignment target";

        internal override MSAst.Expression TransformDelete() => Value.TransformDelete();

        public override Type Type => Value.Type;

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Value.Walk(walker);
            }
            walker.PostWalk(this);
        }

        internal override bool CanThrow => Value.CanThrow;
    }

    internal class StarredExpressionChecker : PythonWalker {
        private readonly CompilerContext context;

        private StarredExpressionChecker(CompilerContext context) {
            this.context = context;
        }

        public static void Check(PythonAst ast, CompilerContext context) {
            var finder = new StarredExpressionChecker(context);
            ast.Walk(finder);
        }

        public override bool Walk(AssignmentStatement node) {
            foreach (var expr in node.Left) {
                WalkAssignmentTarget(expr);
            }
            node.Right?.Walk(this);
            return false;
        }

        public override bool Walk(ForStatement node) {
            WalkAssignmentTarget(node.Left);
            node.List?.Walk(this);
            node.Body?.Walk(this);
            node.Else?.Walk(this);
            return false;
        }

        public override bool Walk(StarredExpression node) {
            ReportSyntaxError("can use starred expression only as assignment target", node);
            return base.Walk(node);
        }

        private void ReportSyntaxError(string message, Node node) {
            context.Errors.Add(context.SourceUnit, message, node.Span, ErrorCodes.SyntaxError, Severity.FatalError);
        }

        private void WalkAssignmentTarget(Expression expr) {
            switch (expr) {
                case StarredExpression starred:
                    ReportSyntaxError("starred assignment target must be in a list or tuple", starred);
                    break;
                case SequenceExpression sequenceExpression:
                    WalkItems(sequenceExpression.Items);
                    break;
                default:
                    expr?.Walk(this);
                    break;
            }
        }

        private bool WalkItems(IList<Expression> items) {
            foreach (var item in items) {
                if (item is StarredExpression starred) {
                    starred.Value.Walk(this);
                } else {
                    item.Walk(this);
                }
            }
            return false;
        }
    }
}
