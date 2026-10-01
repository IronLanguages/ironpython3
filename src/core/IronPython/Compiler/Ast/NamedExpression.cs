// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using IronPython.Runtime;
using IronPython.Runtime.Binding;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace IronPython.Compiler.Ast {
    public partial class NamedExpression : Expression {
        public NamedExpression(NameExpression target, Expression value) {
            ContractUtils.RequiresNotNull(target, nameof(target));
            ContractUtils.RequiresNotNull(value, nameof(value));

            Target = target;
            Value = value;
        }

        public NameExpression Target { get; }

        public Expression Value { get; }

        public override System.Linq.Expressions.Expression Reduce() {
            var value = Variable(typeof(object), "namedExpression");
            return Block(
                [value],
                MakeAssignment(value, Value),
                Target.TransformSet(Span, value, PythonOperationKind.None),
                value
            );
        }

        public override string NodeName => "named expression";

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Target.Walk(walker);
                Value.Walk(walker);
            }
            walker.PostWalk(this);
        }
    }

    internal class NamedExpressionChecker : PythonWalker {
        private readonly CompilerContext _context;

        private NamedExpressionChecker(CompilerContext context) {
            _context = context;
        }

        public static void Check(PythonAst ast, CompilerContext context) {
            var checker = new NamedExpressionChecker(context);
            ast.Walk(checker);
        }

        private int _inComprehensionIterable;
        private int _inGeneratorExpression;

        private void WalkIterable(Expression iterable) {
            _inComprehensionIterable++;
            iterable.Walk(this);
            _inComprehensionIterable--;
        }

        public override bool Walk(ComprehensionFor node) {
            WalkIterable(node.List);
            node.Left.Walk(this);
            return false;
        }

        public override bool Walk(GeneratorExpression node) {
            WalkIterable(node.Iterable);
            node.Function.Walk(this);
            return false;
        }

        public override bool Walk(FunctionDefinition node) {
            if (node.IsGeneratorExpression) _inGeneratorExpression++;
            return true;
        }

        public override void PostWalk(FunctionDefinition node) {
            if (node.IsGeneratorExpression) _inGeneratorExpression--;
        }

        // the for clauses of a generator expression are parsed as nested for statements
        public override bool Walk(ForStatement node) {
            if (_inGeneratorExpression == 0) return true;
            node.Left.Walk(this);
            WalkIterable(node.List);
            node.Body.Walk(this);
            return false;
        }

        public override bool Walk(NamedExpression node) {
            if (_inComprehensionIterable > 0) {
                ReportSyntaxError("assignment expression cannot be used in a comprehension iterable expression", node);
            }
            return base.Walk(node);
        }

        private void ReportSyntaxError(string message, Node node) {
            _context.Errors.Add(_context.SourceUnit, message, node.Span, ErrorCodes.SyntaxError, Severity.FatalError);
        }
    }
}
