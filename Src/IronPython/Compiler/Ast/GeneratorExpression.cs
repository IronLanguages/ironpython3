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

#if FEATURE_CORE_DLR
using MSAst = System.Linq.Expressions;
#else
using MSAst = Microsoft.Scripting.Ast;
#endif

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

        internal override string CheckAssign() {
            return "can't assign to generator expression";
        }

        internal override string CheckAugmentedAssign() {
            return CheckAssign();
        }

        internal override string CheckDelete() {
            return "can't delete generator expression";
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                _function.Walk(walker);
                _iterable.Walk(walker);
            }
            walker.PostWalk(this);
        }
    }
}
