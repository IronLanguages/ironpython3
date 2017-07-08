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

using MSAst = System.Linq.Expressions;

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Microsoft.Scripting;
using Microsoft.Scripting.Ast;
using Microsoft.Scripting.Interpreter;

using IronPython.Runtime;

using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public class IfStatement : Statement, IInstructionProvider {
        private readonly IfStatementTest[] _tests;
        private readonly Statement _else;

        public IfStatement(IfStatementTest[] tests, Statement else_) {
            _tests = tests;
            _else = else_;
        }

        public IList<IfStatementTest> Tests {
            get { return _tests; }
        }

        public Statement ElseStatement {
            get { return _else; }
        }

        public override MSAst.Expression Reduce() {
            return ReduceWorker(true);
        }

        #region IInstructionProvider Members

        void IInstructionProvider.AddInstructions(LightCompiler compiler) {
            // optimizing bool conversions does no good in the light compiler
            compiler.Compile(ReduceWorker(false));
        }

        #endregion

        private MSAst.Expression ReduceWorker(bool optimizeDynamicConvert) {
            MSAst.Expression result;

            if (_tests.Length > 100) {
                // generate:
                // if(x) {
                //   body
                //   goto end
                // } else { 
                // }
                // elseBody
                // end:
                //
                // to avoid deeply recursive trees which can stack overflow.
                BlockBuilder builder = new BlockBuilder();
                var label = Ast.Label();
                for (int i = 0; i < _tests.Length; i++) {
                    IfStatementTest ist = _tests[i];

                    builder.Add(
                        Ast.Condition(
                            optimizeDynamicConvert ?
                                TransformAndDynamicConvert(ist.Test, typeof(bool)) :
                                GlobalParent.Convert(typeof(bool), Microsoft.Scripting.Actions.ConversionResultKind.ExplicitCast, ist.Test),
                            Ast.Block(
                                TransformMaybeSingleLineSuite(ist.Body, GlobalParent.IndexToLocation(ist.Test.StartIndex)),
                                Ast.Goto(label)
                            ),
                            Utils.Empty()
                        )
                    );
                }

                if (_else != null) {
                    builder.Add(_else);
                }

                builder.Add(Ast.Label(label));
                result = builder.ToExpression();
            } else {
                // Now build from the inside out
                if (_else != null) {
                    result = _else;
                } else {
                    result = AstUtils.Empty();
                }

                int i = _tests.Length;
                while (i-- > 0) {
                    IfStatementTest ist = _tests[i];

                    result = GlobalParent.AddDebugInfoAndVoid(
                        Ast.Condition(
                            optimizeDynamicConvert ?
                                TransformAndDynamicConvert(ist.Test, typeof(bool)) :
                                GlobalParent.Convert(typeof(bool), Microsoft.Scripting.Actions.ConversionResultKind.ExplicitCast, ist.Test),
                            TransformMaybeSingleLineSuite(ist.Body, GlobalParent.IndexToLocation(ist.Test.StartIndex)),
                            result
                        ),
                        new SourceSpan(GlobalParent.IndexToLocation(ist.StartIndex), GlobalParent.IndexToLocation(ist.HeaderIndex))
                    );
                }
            }

            return result;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_tests != null) {
                    foreach (IfStatementTest test in _tests) {
                        test.Walk(walker);
                    }
                }
                if (_else != null) {
                    _else.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }
    }
}
