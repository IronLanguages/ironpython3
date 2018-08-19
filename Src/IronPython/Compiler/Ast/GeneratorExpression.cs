// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

using System;
using System.Diagnostics;

using Microsoft.Scripting.Actions;

using IronPython.Runtime;
using IronPython.Runtime.Binding;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class GeneratorExpression : Expression {
        private readonly FunctionDefinition _function;
        private readonly Expression _iterable;

        public GeneratorExpression(FunctionDefinition function, Expression iterable) {
            _function = function;
            _iterable = iterable;
        }

        public override MSAst.Expression Reduce() {
            return Ast.Call(
                AstMethods.MakeGeneratorExpression,
                _function.MakeFunctionExpression(),
                _iterable
            );
        }

        public FunctionDefinition Function {
            get {
                return _function;
            }
        }

        public Expression Iterable {
            get {
                return _iterable;
            }
        }

        public override string NodeName => "generator expression";

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                _function.Walk(walker);
                _iterable.Walk(walker);
            }
            walker.PostWalk(this);
        }
    }
}
