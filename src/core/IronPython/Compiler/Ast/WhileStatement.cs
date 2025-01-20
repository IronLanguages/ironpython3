// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

using Microsoft.Scripting;
using Microsoft.Scripting.Interpreter;

using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {

    public class WhileStatement : Statement, ILoopStatement, IInstructionProvider {
        // Marks the end of the condition of the while loop
        private int _indexHeader;
        private MSAst.LabelTarget _break, _continue;

        public WhileStatement(Expression test, Statement body, Statement else_) {
            Test = test;
            Body = body;
            ElseStatement = else_;
        }

        public Expression Test { get; }

        public Statement Body { get; }

        public Statement ElseStatement { get; }

        private SourceSpan Header
            => new SourceSpan(GlobalParent.IndexToLocation(StartIndex), GlobalParent.IndexToLocation(_indexHeader));

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

            if (Test is ConstantExpression constTest && constTest.Value is int val) {
                // while 0: / while 1:
                if (val == 0) {
                    // completely optimize the loop away
                    if (ElseStatement == null) {
                        return MSAst.Expression.Empty();
                    } else {
                        return ElseStatement;
                    }
                }

                MSAst.Expression test = MSAst.Expression.Constant(true);
                MSAst.Expression res = AstUtils.While(
                    test,
                    Body,
                    ElseStatement,
                    _break,
                    _continue
                );

                if (GlobalParent.IndexToLocation(Test.StartIndex).Line != GlobalParent.IndexToLocation(Body.StartIndex).Line) {
                    res = GlobalParent.AddDebugInfoAndVoid(res, Test.Span);
                }

                return res;
            }

            return AstUtils.While(
                GlobalParent.AddDebugInfo(
                    optimizeDynamicConvert ?
                        TransformAndDynamicConvert(Test, typeof(bool)) :
                        GlobalParent.Convert(typeof(bool), Microsoft.Scripting.Actions.ConversionResultKind.ExplicitCast, Test),
                    Header
                ),
                Body,
                ElseStatement,
                _break,
                _continue
            );
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Test?.Walk(walker);
                Body?.Walk(walker);
                ElseStatement?.Walk(walker);
            }
            walker.PostWalk(this);
        }
    }
}
