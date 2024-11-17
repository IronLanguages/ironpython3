// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

using IronPython.Runtime.Binding;

using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {
    public class AugmentedAssignStatement : Statement {
        public AugmentedAssignStatement(PythonOperator op, Expression left, Expression right) {
            Operator = op;
            Left = left;
            Right = right;
        }

        public PythonOperator Operator { get; }

        public Expression Left { get; }

        public Expression Right { get; }

        public override MSAst.Expression Reduce() {
            return Left.TransformSet(Span, Right, PythonOperatorToAction(Operator));
        }

        private static PythonOperationKind PythonOperatorToAction(PythonOperator op) {
            switch (op) {
                // Binary
                case PythonOperator.Add:
                    return PythonOperationKind.InPlaceAdd;
                case PythonOperator.Subtract:
                    return PythonOperationKind.InPlaceSubtract;
                case PythonOperator.Multiply:
                    return PythonOperationKind.InPlaceMultiply;
                case PythonOperator.MatMult:
                    return PythonOperationKind.InPlaceMatMult;
                case PythonOperator.FloorDivide:
                    return PythonOperationKind.InPlaceFloorDivide;
                case PythonOperator.TrueDivide:
                    return PythonOperationKind.InPlaceTrueDivide;
                case PythonOperator.Mod:
                    return PythonOperationKind.InPlaceMod;
                case PythonOperator.BitwiseAnd:
                    return PythonOperationKind.InPlaceBitwiseAnd;
                case PythonOperator.BitwiseOr:
                    return PythonOperationKind.InPlaceBitwiseOr;
                case PythonOperator.Xor:
                    return PythonOperationKind.InPlaceExclusiveOr;
                case PythonOperator.LeftShift:
                    return PythonOperationKind.InPlaceLeftShift;
                case PythonOperator.RightShift:
                    return PythonOperationKind.InPlaceRightShift;
                case PythonOperator.Power:
                    return PythonOperationKind.InPlacePower;
                default:
                    Debug.Assert(false, "Unexpected PythonOperator: " + op.ToString());
                    return PythonOperationKind.None;
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Left?.Walk(walker);
                Right?.Walk(walker);
            }
            walker.PostWalk(this);
        }
    }
}
