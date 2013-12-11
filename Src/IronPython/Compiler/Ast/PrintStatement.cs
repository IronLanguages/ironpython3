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

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    public class PrintStatement : Statement {
        private readonly Expression _dest;
        private readonly Expression[] _expressions;
        private readonly bool _trailingComma;

        public PrintStatement(Expression destination, Expression[] expressions, bool trailingComma) {
            _dest = destination;
            _expressions = expressions;
            _trailingComma = trailingComma;
        }

        public Expression Destination {
            get { return _dest; }
        }

        public IList<Expression> Expressions {
            get { return _expressions; }
        }

        public bool TrailingComma {
            get { return _trailingComma; }
        }

        public override MSAst.Expression Reduce() {
            MSAst.Expression destination = _dest;

            if (_expressions.Length == 0) {
                MSAst.Expression result;
                if (destination != null) {
                    result = Ast.Call(
                        AstMethods.PrintNewlineWithDest,
                        Parent.LocalContext,
                        destination
                    );
                } else {
                    result = Ast.Call(
                        AstMethods.PrintNewline,
                        Parent.LocalContext
                    );
                }
                return GlobalParent.AddDebugInfo(result, Span);
            } else {
                // Create list for the individual statements
                ReadOnlyCollectionBuilder<MSAst.Expression> statements = new ReadOnlyCollectionBuilder<MSAst.Expression>();

                // Store destination in a temp, if we have one
                MSAst.ParameterExpression temp = null;
                if (destination != null) {
                    temp = Ast.Variable(typeof(object), "destination");

                    statements.Add(MakeAssignment(temp, destination));

                    destination = temp;
                }
                for (int i = 0; i < _expressions.Length; i++) {
                    bool withComma = (i < _expressions.Length - 1 || _trailingComma);// ? "PrintComma" : "Print";
                    Expression current = _expressions[i];
                    MSAst.MethodCallExpression mce;

                    if (destination != null) {
                        mce = Ast.Call(
                            withComma ? AstMethods.PrintCommaWithDest : AstMethods.PrintWithDest,
                            Parent.LocalContext,
                            destination,
                            AstUtils.Convert(current, typeof(object))
                        );
                    } else {
                        mce = Ast.Call(
                            withComma ? AstMethods.PrintComma : AstMethods.Print,
                            Parent.LocalContext,
                            AstUtils.Convert(current, typeof(object))
                        );
                    }

                    statements.Add(mce);
                }

                statements.Add(AstUtils.Empty());
                MSAst.Expression res;
                if (temp != null) {
                    res = Ast.Block(new[] { temp }, statements.ToReadOnlyCollection());
                } else {
                    res = Ast.Block(statements.ToReadOnlyCollection());
                }
                return GlobalParent.AddDebugInfo(res, Span);
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_dest != null) {
                    _dest.Walk(walker);
                }
                if (_expressions != null) {
                    foreach (Expression expression in _expressions) {
                        expression.Walk(walker);
                    }
                }
            }
            walker.PostWalk(this);
        }
    }
}
