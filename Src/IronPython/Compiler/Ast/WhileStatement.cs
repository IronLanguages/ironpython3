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

using Microsoft.Scripting;
using Microsoft.Scripting.Interpreter;

using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {

    public class WhileStatement : Statement, ILoopStatement, IInstructionProvider {
        // Marks the end of the condition of the while loop
        private int _indexHeader;
        private readonly Expression _test;
        private readonly Statement _body;
        private readonly Statement _else;
        private MSAst.LabelTarget _break, _continue;

        public WhileStatement(Expression test, Statement body, Statement else_) {
            _test = test;
            _body = body;
            _else = else_;
        }

        public Expression Test {
            get { return _test;}
        }

        public Statement Body {
            get { return _body; }
        }

        public Statement ElseStatement {
            get { return _else; }
        }

        private SourceSpan Header {
            get { return new SourceSpan(GlobalParent.IndexToLocation(StartIndex), GlobalParent.IndexToLocation(_indexHeader)); }
        }

        public void SetLoc(PythonAst globalParent, int start, int header, int end) {
            SetLoc(globalParent, start, end);
            _indexHeader = header;
        }

        MSAst.LabelTarget ILoopStatement.BreakLabel {
            get {
                return _break;
            }
            set {
                _break = value;
            }
        }

        MSAst.LabelTarget ILoopStatement.ContinueLabel {
            get {
                return _continue;
            }
            set {
                _continue = value;
            }
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
            // Only the body is "in the loop" for the purposes of break/continue
            // The "else" clause is outside

            ConstantExpression constTest = _test as ConstantExpression;
            if (constTest != null && constTest.Value is int) {
                // while 0: / while 1:
                int val = (int)constTest.Value;
                if (val == 0) {
                    // completely optimize the loop away
                    if (_else == null) {
                        return MSAst.Expression.Empty();
                    } else {
                        return _else;
                    }
                }

                MSAst.Expression test = MSAst.Expression.Constant(true);
                MSAst.Expression res = AstUtils.While(
                    test,
                    _body,
                    _else,
                    _break,
                    _continue
                );

                if (GlobalParent.IndexToLocation(_test.StartIndex).Line != GlobalParent.IndexToLocation(_body.StartIndex).Line) {
                    res = GlobalParent.AddDebugInfoAndVoid(res, _test.Span);
                }

                return res;
            }

            return AstUtils.While(
                GlobalParent.AddDebugInfo(
                    optimizeDynamicConvert ?
                        TransformAndDynamicConvert(_test, typeof(bool)) :
                        GlobalParent.Convert(typeof(bool), Microsoft.Scripting.Actions.ConversionResultKind.ExplicitCast, _test),
                    Header
                ),
                _body,
                _else,
                _break,
                _continue
            );
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_test != null) {
                    _test.Walk(walker);
                }
                if (_body != null) {
                    _body.Walk(walker);
                }
                if (_else != null) {
                    _else.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }
    }
}
