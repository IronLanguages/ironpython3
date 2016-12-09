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

using System.Diagnostics;
using IronPython.Runtime.Binding;

namespace IronPython.Compiler.Ast {
    public class AugmentedAssignStatement : Statement {
        private readonly PythonOperator _op;
        private readonly Expression _left;
        private readonly Expression _right;

        public AugmentedAssignStatement(PythonOperator op, Expression left, Expression right) {
            _op = op;
            _left = left; 
            _right = right;
        }

        public PythonOperator Operator {
            get { return _op; }
        }

        public Expression Left {
            get { return _left; }
        }

        public Expression Right {
            get { return _right; }
        }

        public override MSAst.Expression Reduce() {
            return _left.TransformSet(Span, _right, PythonOperatorToAction(_op));
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
                case PythonOperator.FloorDivide:
                    return PythonOperationKind.InPlaceFloorDivide;
                default:
                    Debug.Assert(false, "Unexpected PythonOperator: " + op.ToString());
                    return PythonOperationKind.None;
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_left != null) {
                    _left.Walk(walker);
                }
                if (_right != null) {
                    _right.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }
    }
}
