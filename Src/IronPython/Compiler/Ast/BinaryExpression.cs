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
using System.Diagnostics;
using System.Reflection;

using Microsoft.Scripting;
using Microsoft.Scripting.Interpreter;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Binding;

using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;
    
    public partial class BinaryExpression : Expression, IInstructionProvider {
        private readonly Expression _left, _right;
        private readonly PythonOperator _op;

        public BinaryExpression(PythonOperator op, Expression left, Expression right) {
            ContractUtils.RequiresNotNull(left, "left");
            ContractUtils.RequiresNotNull(right, "right");
            if (op == PythonOperator.None) throw new ValueErrorException("bad operator");

            _op = op;
            _left = left;
            _right = right;
            StartIndex = left.StartIndex;
            EndIndex = right.EndIndex;
        }

        public Expression Left {
            get { return _left; }
        }

        public Expression Right {
            get { return _right; }
        }

        public PythonOperator Operator {
            get { return _op; }
        }

        private bool IsComparison() {
            switch (_op) {
                case PythonOperator.LessThan:
                case PythonOperator.LessThanOrEqual:
                case PythonOperator.GreaterThan:
                case PythonOperator.GreaterThanOrEqual:
                case PythonOperator.Equal:
                case PythonOperator.NotEqual:
                case PythonOperator.In:
                case PythonOperator.NotIn:
                case PythonOperator.IsNot:
                case PythonOperator.Is:
                    return true;
            }
            return false;
        }

        private bool NeedComparisonTransformation() {
            return IsComparison() && IsComparison(_right);
        }

        public static bool IsComparison(Expression expression) {
            BinaryExpression be = expression as BinaryExpression;
            return be != null && be.IsComparison();
        }

        // This is a compound comparison operator like: a < b < c.
        // That's represented as binary operators, but it's not the same as (a<b) < c, so we do special transformations.
        // We need to:
        // - return true iff (a<b) && (b<c), but ensure that b is only evaluated once. 
        // - ensure evaluation order is correct (a,b,c)
        // - don't evaluate c if a<b is false.
        private MSAst.Expression FinishCompare(MSAst.Expression left) {
            Debug.Assert(_right is BinaryExpression);

            BinaryExpression bright = (BinaryExpression)_right;

            // Transform the left child of my right child (the next node in sequence)
            MSAst.Expression rleft = bright.Left;

            // Store it in the temp
            MSAst.ParameterExpression temp = Ast.Parameter(typeof(object), "chained_comparison");

            // Create binary operation: left <_op> (temp = rleft)
            MSAst.Expression comparison = MakeBinaryOperation(
                _op,
                left,
                Ast.Assign(temp, AstUtils.Convert(rleft, temp.Type)),
                Span
            );

            MSAst.Expression rright;

            // Transform rright, comparing to temp
            if (IsComparison(bright._right)) {
                rright = bright.FinishCompare(temp);
            } else {
                MSAst.Expression transformedRight = bright.Right;
                rright = MakeBinaryOperation(
                    bright.Operator,
                    temp,
                    transformedRight,
                    bright.Span
                );
            }

            // return (left (op) (temp = rleft)) and (rright)
            MSAst.ParameterExpression tmp;
            MSAst.Expression res = AstUtils.CoalesceTrue(
                comparison,
                rright,
                AstMethods.IsTrue,
                out tmp
            );

            return Ast.Block(
                new[] { temp, tmp },
                res
            );
        }

        public override MSAst.Expression Reduce() {
            ConstantExpression leftConst;
            if (!CanEmitWarning(_op)) {
                var folded = ConstantFold();
                if (folded != null) {
                    folded.Parent = Parent;
                    return AstUtils.Convert(folded.Reduce(), typeof(object));
                }
            } 
            
            if (_op == PythonOperator.Mod && 
                (leftConst = _left as ConstantExpression) != null && 
                leftConst.Value is string) {

                return Expression.Call(
                    AstMethods.FormatString,
                    Parent.LocalContext,
                    _left,
                    AstUtils.Convert(_right, typeof(object))
                );
            }

            if (NeedComparisonTransformation()) {
                // This is a compound comparison like: (a < b < c)
                return FinishCompare(_left);
            } else {
                // Simple binary operator.
                return MakeBinaryOperation(_op, _left, _right, Span);
            }
        }

        #region IInstructionProvider Members

        void IInstructionProvider.AddInstructions(LightCompiler compiler) {
            if (NeedComparisonTransformation()) {
                // chained comparisons aren't supported for optimized light compiling
                compiler.Compile(Reduce());
                return;
            }

            switch (_op) {
                case PythonOperator.Is:
                    compiler.Compile(_left);
                    compiler.Compile(_right);
                    compiler.Instructions.Emit(IsInstruction.Instance);
                    break;
                case PythonOperator.IsNot:
                    compiler.Compile(_left);
                    compiler.Compile(_right);
                    compiler.Instructions.Emit(IsNotInstruction.Instance);
                    break;
                default:
                    compiler.Compile(Reduce());
                    break;
            }
        }

        abstract class BinaryInstruction : Instruction {
            public override int ConsumedStack {
                get {
                    return 2;
                }
            }

            public override int ProducedStack {
                get {
                    return 1;
                }
            }
        }

        class IsInstruction : BinaryInstruction {
            public static readonly IsInstruction Instance = new IsInstruction();

            public override int Run(InterpretedFrame frame) {
                // it’s okay to pop the args in this order due to commutativity of referential equality
                frame.Push(PythonOps.Is(frame.Pop(), frame.Pop()));
                return +1;
            }
        }


        class IsNotInstruction : BinaryInstruction {
            public static readonly IsNotInstruction Instance = new IsNotInstruction();

            public override int Run(InterpretedFrame frame) {
                // it’s okay to pop the args in this order due to commutativity of referential equality
                frame.Push(PythonOps.IsNot(frame.Pop(), frame.Pop()));
                return +1;
            }
        }



        #endregion

        internal override string CheckAssign() {
            return "can't assign to operator";
        }

        private MSAst.Expression MakeBinaryOperation(PythonOperator op, MSAst.Expression left, MSAst.Expression right, SourceSpan span) {
            if (op == PythonOperator.NotIn) {
                return AstUtils.Convert(
                    Ast.Not(
                        GlobalParent.Operation(
                            typeof(bool),
                            PythonOperationKind.Contains,
                            left,
                            right
                        )
                    ),
                    typeof(object)
                );
            } else if (op == PythonOperator.In) {
                return AstUtils.Convert(
                    GlobalParent.Operation(
                        typeof(bool),
                        PythonOperationKind.Contains,
                        left,
                        right
                    ),
                    typeof(object)
                );
            }

            PythonOperationKind action = PythonOperatorToAction(op);
            if (action != PythonOperationKind.None) {
                return GlobalParent.Operation(
                    typeof(object),
                    action,
                    left,
                    right
                );
            } else {
                // Call helper method
                return Ast.Call(
                    GetHelperMethod(op),
                    ConvertIfNeeded(left, typeof(object)),
                    ConvertIfNeeded(right, typeof(object))
                );
            }
        }

        private bool CanEmitWarning(PythonOperator op) {
            return false;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                _left.Walk(walker);
                _right.Walk(walker);
            }
            walker.PostWalk(this);
        }

        private static PythonOperationKind PythonOperatorToAction(PythonOperator op) {
            switch (op) {
                // Binary
                case PythonOperator.Add:
                    return PythonOperationKind.Add;
                case PythonOperator.Subtract:
                    return PythonOperationKind.Subtract;
                case PythonOperator.Multiply:
                    return PythonOperationKind.Multiply;
                case PythonOperator.TrueDivide:
                    return PythonOperationKind.TrueDivide;
                case PythonOperator.Mod:
                    return PythonOperationKind.Mod;
                case PythonOperator.BitwiseAnd:
                    return PythonOperationKind.BitwiseAnd;
                case PythonOperator.BitwiseOr:
                    return PythonOperationKind.BitwiseOr;
                case PythonOperator.Xor:
                    return PythonOperationKind.ExclusiveOr;
                case PythonOperator.LeftShift:
                    return PythonOperationKind.LeftShift;
                case PythonOperator.RightShift:
                    return PythonOperationKind.RightShift;
                case PythonOperator.Power:
                    return PythonOperationKind.Power;
                case PythonOperator.FloorDivide:
                    return PythonOperationKind.FloorDivide;

                // Comparisons
                case PythonOperator.LessThan:
                    return PythonOperationKind.LessThan;
                case PythonOperator.LessThanOrEqual:
                    return PythonOperationKind.LessThanOrEqual;
                case PythonOperator.GreaterThan:
                    return PythonOperationKind.GreaterThan;
                case PythonOperator.GreaterThanOrEqual:
                    return PythonOperationKind.GreaterThanOrEqual;
                case PythonOperator.Equal:
                    return PythonOperationKind.Equal;
                case PythonOperator.NotEqual:
                    return PythonOperationKind.NotEqual;

                case PythonOperator.In:
                    return PythonOperationKind.Contains;

                case PythonOperator.NotIn:
                case PythonOperator.IsNot:
                case PythonOperator.Is:
                    return PythonOperationKind.None;

                default:
                    Debug.Assert(false, "Unexpected PythonOperator: " + op.ToString());
                    return PythonOperationKind.None;
            }
        }

        private static MethodInfo GetHelperMethod(PythonOperator op) {
            switch (op) {
                case PythonOperator.IsNot:
                    return AstMethods.IsNot;
                case PythonOperator.Is:
                    return AstMethods.Is;

                default:
                    Debug.Assert(false, "Invalid PythonOperator: " + op.ToString());
                    return null;
            }
        }

        internal override bool CanThrow {
            get {
                if (_op == PythonOperator.Is || _op == PythonOperator.IsNot) {
                    return _left.CanThrow || _right.CanThrow;
                }
                return true;
            }
        }
    }
}
