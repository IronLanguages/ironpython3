// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Dynamic;
using System.Linq.Expressions;

using Microsoft.Scripting.Actions;

namespace IronPython.Runtime.Binding {
    internal static class Binders {
        /// <summary>
        /// Backwards compatible Convert for the old sites that need to flow CodeContext
        /// </summary>
        public static Expression/*!*/ Convert(Expression/*!*/ codeContext, PythonContext/*!*/ binder, Type/*!*/ type, ConversionResultKind resultKind, Expression/*!*/ target) {
            return DynamicExpression.Dynamic(
                binder.Convert(type, resultKind),
                type,
                target
            );
        }


        public static Expression/*!*/ Get(Expression/*!*/ codeContext, PythonContext/*!*/ binder, Type/*!*/ resultType, string/*!*/ name, Expression/*!*/ target) {
            return DynamicExpression.Dynamic(
                binder.GetMember(name),
                resultType,
                target,
                codeContext
            );
        }

        public static Expression/*!*/ TryGet(Expression/*!*/ codeContext, PythonContext/*!*/ binder, Type/*!*/ resultType, string/*!*/ name, Expression/*!*/ target) {
            return DynamicExpression.Dynamic(
                binder.GetMember(
                    name,
                    true
                ),
                resultType,
                target,
                codeContext
            );
        }

        public static DynamicMetaObjectBinder UnaryOperationBinder(PythonContext state, PythonOperationKind operatorName) {
            ExpressionType? et = GetExpressionTypeFromUnaryOperator(operatorName);

            if (et == null) {
                return state.Operation(
                    operatorName
                );
            }

            return state.UnaryOperation(et.Value);
        }

        private static ExpressionType? GetExpressionTypeFromUnaryOperator(PythonOperationKind operatorName) {
            switch (operatorName) {
                case PythonOperationKind.Positive: return ExpressionType.UnaryPlus;
                case PythonOperationKind.Negate: return ExpressionType.Negate;
                case PythonOperationKind.OnesComplement: return ExpressionType.OnesComplement;
                case PythonOperationKind.Not: return ExpressionType.Not;
                case PythonOperationKind.IsFalse: return ExpressionType.IsFalse;
            }
            return null;
        }

        public static DynamicMetaObjectBinder BinaryOperationBinder(PythonContext state, PythonOperationKind operatorName) {
            ExpressionType? et = GetExpressionTypeFromBinaryOperator(operatorName);

            if (et == null) {
                return state.Operation(
                    operatorName
                );
            }

            return state.BinaryOperation(et.Value);
        }

        private static ExpressionType? GetExpressionTypeFromBinaryOperator(PythonOperationKind operatorName) {
            switch (operatorName) {
                case PythonOperationKind.Add: return ExpressionType.Add;
                case PythonOperationKind.BitwiseAnd: return ExpressionType.And;
                case PythonOperationKind.ExclusiveOr: return ExpressionType.ExclusiveOr;
                case PythonOperationKind.Mod: return ExpressionType.Modulo;
                case PythonOperationKind.Multiply: return ExpressionType.Multiply;
                case PythonOperationKind.BitwiseOr: return ExpressionType.Or;
                case PythonOperationKind.Power: return ExpressionType.Power;
                case PythonOperationKind.RightShift: return ExpressionType.RightShift;
                case PythonOperationKind.LeftShift: return ExpressionType.LeftShift;
                case PythonOperationKind.Subtract: return ExpressionType.Subtract;
                case PythonOperationKind.TrueDivide: return ExpressionType.Divide;

                case PythonOperationKind.InPlaceAdd: return ExpressionType.AddAssign;
                case PythonOperationKind.InPlaceBitwiseAnd: return ExpressionType.AndAssign;
                case PythonOperationKind.InPlaceExclusiveOr: return ExpressionType.ExclusiveOrAssign;
                case PythonOperationKind.InPlaceMod: return ExpressionType.ModuloAssign;
                case PythonOperationKind.InPlaceMultiply: return ExpressionType.MultiplyAssign;
                case PythonOperationKind.InPlaceBitwiseOr: return ExpressionType.OrAssign;
                case PythonOperationKind.InPlacePower: return ExpressionType.PowerAssign;
                case PythonOperationKind.InPlaceRightShift: return ExpressionType.RightShiftAssign;
                case PythonOperationKind.InPlaceLeftShift: return ExpressionType.LeftShiftAssign;
                case PythonOperationKind.InPlaceSubtract: return ExpressionType.SubtractAssign;
                case PythonOperationKind.InPlaceTrueDivide: return ExpressionType.DivideAssign;

                case PythonOperationKind.Equal: return ExpressionType.Equal;
                case PythonOperationKind.GreaterThan: return ExpressionType.GreaterThan;
                case PythonOperationKind.GreaterThanOrEqual: return ExpressionType.GreaterThanOrEqual;
                case PythonOperationKind.LessThan: return ExpressionType.LessThan;
                case PythonOperationKind.LessThanOrEqual: return ExpressionType.LessThanOrEqual;
                case PythonOperationKind.NotEqual: return ExpressionType.NotEqual;
            }
            return null;
        }

        /// <summary>
        /// Creates a new InvokeBinder which will call with positional splatting.
        /// 
        /// The signature of the target site should be object(function), object[], retType
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public static PythonInvokeBinder/*!*/ InvokeSplat(PythonContext/*!*/ state) {
            return state.Invoke(
                new CallSignature(new Argument(ArgumentType.List))
            );
        }

        /// <summary>
        /// Creates a new InvokeBinder which will call with positional and keyword splatting.
        /// 
        /// The signature of the target site should be object(function), object[], dictionary, retType
        /// </summary>
        public static PythonInvokeBinder/*!*/ InvokeKeywords(PythonContext/*!*/ state) {
            return state.Invoke(
                new CallSignature(new Argument(ArgumentType.List), new Argument(ArgumentType.Dictionary))
            );
        }


    }
}
