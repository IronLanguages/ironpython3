// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

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
        public BinaryExpression(PythonOperator op, Expression left, Expression right) {
            ContractUtils.RequiresNotNull(left, nameof(left));
            ContractUtils.RequiresNotNull(right, nameof(right));
            if (op == PythonOperator.None) throw new ValueErrorException("bad operator");

            Operator = op;
            Left = left;
            Right = right;
            StartIndex = left.StartIndex;
            EndIndex = right.EndIndex;
        }

        public Expression Left { get; }

        public Expression Right { get; }

        public PythonOperator Operator { get; }

        private bool IsComparison() {
            switch (Operator) {
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

        private bool NeedComparisonTransformation() => IsComparison() && IsComparison(Right);

        public static bool IsComparison(Expression expression) => expression is BinaryExpression be && be.IsComparison();

        // This is a compound comparison operator like: a < b < c.
        // That's represented as binary operators, but it's not the same as (a<b) < c, so we do special transformations.
        // We need to:
        // - return true iff (a<b) && (b<c), but ensure that b is only evaluated once. 
        // - ensure evaluation order is correct (a,b,c)
        // - don't evaluate c if a<b is false.
        private MSAst.Expression FinishCompare(MSAst.Expression left) {
            Debug.Assert(Right is BinaryExpression);

            BinaryExpression bright = (BinaryExpression)Right;

            // Transform the left child of my right child (the next node in sequence)
            MSAst.Expression rleft = bright.Left;

            // Store it in the temp
            MSAst.ParameterExpression temp = Ast.Parameter(typeof(object), "chained_comparison");

            // Create binary operation: left <_op> (temp = rleft)
            MSAst.Expression comparison = MakeBinaryOperation(
                Operator,
                left,
                Ast.Assign(temp, AstUtils.Convert(rleft, temp.Type)),
                Span
            );

            MSAst.Expression rright;

            // Transform rright, comparing to temp
            if (IsComparison(bright.Right)) {
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
            if (!CanEmitWarning(Operator)) {
                var folded = ConstantFold();
                if (folded != null) {
                    folded.Parent = Parent;
                    return AstUtils.Convert(folded.Reduce(), typeof(object));
                }
            } 
            
            if (Operator == PythonOperator.Mod &&
                (leftConst = Left as ConstantExpression) != null &&
                leftConst.Value is string) {

                return Expression.Call(
                    AstMethods.FormatString,
                    Parent.LocalContext,
                    Left,
                    AstUtils.Convert(Right, typeof(object))
                );
            }

            if (NeedComparisonTransformation()) {
                // This is a compound comparison like: (a < b < c)
                return FinishCompare(Left);
            } else {
                // Simple binary operator.
                return MakeBinaryOperation(Operator, Left, Right, Span);
            }
        }

        #region IInstructionProvider Members

        void IInstructionProvider.AddInstructions(LightCompiler compiler) {
            if (NeedComparisonTransformation()) {
                // chained comparisons aren't supported for optimized light compiling
                compiler.Compile(Reduce());
                return;
            }

            switch (Operator) {
                case PythonOperator.Is:
                    compiler.Compile(Left);
                    compiler.Compile(Right);
                    compiler.Instructions.Emit(IsInstruction.Instance);
                    break;
                case PythonOperator.IsNot:
                    compiler.Compile(Left);
                    compiler.Compile(Right);
                    compiler.Instructions.Emit(IsNotInstruction.Instance);
                    break;
                default:
                    compiler.Compile(Reduce());
                    break;
            }
        }

        private abstract class BinaryInstruction : Instruction {
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

        private class IsInstruction : BinaryInstruction {
            public static readonly IsInstruction Instance = new IsInstruction();

            public override int Run(InterpretedFrame frame) {
                // it’s okay to pop the args in this order due to commutativity of referential equality
                frame.Push(PythonOps.Is(frame.Pop(), frame.Pop()));
                return +1;
            }
        }

        private class IsNotInstruction : BinaryInstruction {
            public static readonly IsNotInstruction Instance = new IsNotInstruction();

            public override int Run(InterpretedFrame frame) {
                // it’s okay to pop the args in this order due to commutativity of referential equality
                frame.Push(PythonOps.IsNot(frame.Pop(), frame.Pop()));
                return +1;
            }
        }



        #endregion

        public override string NodeName => IsComparison() ? "comparison" : "operator";

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
                Left.Walk(walker);
                Right.Walk(walker);
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
                if (Operator == PythonOperator.Is || Operator == PythonOperator.IsNot) {
                    return Left.CanThrow || Right.CanThrow;
                }
                return true;
            }
        }
    }
}
