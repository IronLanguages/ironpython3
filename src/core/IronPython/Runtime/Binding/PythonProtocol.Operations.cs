﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;
using System.Numerics;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Reflection;
using System.Text;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Actions.Calls;
using Microsoft.Scripting.Ast;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    internal static partial class PythonProtocol {
        public static DynamicMetaObject/*!*/ Operation(BinaryOperationBinder/*!*/ operation, DynamicMetaObject target, DynamicMetaObject arg, DynamicMetaObject errorSuggestion) {
            PerfTrack.NoteEvent(PerfTrack.Categories.Binding, "Fallback BinaryOperator " + target.LimitType.FullName + " " + operation.Operation + " " + arg.LimitType.FullName);
            PerfTrack.NoteEvent(PerfTrack.Categories.BindingTarget, operation.Operation.ToString());

            DynamicMetaObject[] args = new[] { target, arg };
            if (BindingHelpers.NeedsDeferral(args)) {
                return operation.Defer(target, arg);
            }

            ValidationInfo valInfo = BindingHelpers.GetValidationInfo(args);

            PythonOperationKind? pyOperator = null;
            switch (operation.Operation) {
                case ExpressionType.Add: pyOperator = PythonOperationKind.Add; break;
                case ExpressionType.And: pyOperator = PythonOperationKind.BitwiseAnd; break;
                case ExpressionType.Divide: pyOperator = PythonOperationKind.TrueDivide; break;
                case ExpressionType.ExclusiveOr: pyOperator = PythonOperationKind.ExclusiveOr; break;
                case ExpressionType.Modulo: pyOperator = PythonOperationKind.Mod; break;
                case ExpressionType.Multiply: pyOperator = PythonOperationKind.Multiply; break;
                case ExpressionType.Or: pyOperator = PythonOperationKind.BitwiseOr; break;
                case ExpressionType.Power: pyOperator = PythonOperationKind.Power; break;
                case ExpressionType.RightShift: pyOperator = PythonOperationKind.RightShift; break;
                case ExpressionType.LeftShift: pyOperator = PythonOperationKind.LeftShift; break;
                case ExpressionType.Subtract: pyOperator = PythonOperationKind.Subtract; break;

                case ExpressionType.AddAssign: pyOperator = PythonOperationKind.InPlaceAdd; break;
                case ExpressionType.AndAssign: pyOperator = PythonOperationKind.InPlaceBitwiseAnd; break;
                case ExpressionType.DivideAssign: pyOperator = PythonOperationKind.InPlaceTrueDivide; break;
                case ExpressionType.ExclusiveOrAssign: pyOperator = PythonOperationKind.InPlaceExclusiveOr; break;
                case ExpressionType.ModuloAssign: pyOperator = PythonOperationKind.InPlaceMod; break;
                case ExpressionType.MultiplyAssign: pyOperator = PythonOperationKind.InPlaceMultiply; break;
                case ExpressionType.OrAssign: pyOperator = PythonOperationKind.InPlaceBitwiseOr; break;
                case ExpressionType.PowerAssign: pyOperator = PythonOperationKind.InPlacePower; break;
                case ExpressionType.RightShiftAssign: pyOperator = PythonOperationKind.InPlaceRightShift; break;
                case ExpressionType.LeftShiftAssign: pyOperator = PythonOperationKind.InPlaceLeftShift; break;
                case ExpressionType.SubtractAssign: pyOperator = PythonOperationKind.InPlaceSubtract; break;

                case ExpressionType.Equal: pyOperator = PythonOperationKind.Equal; break;
                case ExpressionType.GreaterThan: pyOperator = PythonOperationKind.GreaterThan; break;
                case ExpressionType.GreaterThanOrEqual: pyOperator = PythonOperationKind.GreaterThanOrEqual; break;
                case ExpressionType.LessThan: pyOperator = PythonOperationKind.LessThan; break;
                case ExpressionType.LessThanOrEqual: pyOperator = PythonOperationKind.LessThanOrEqual; break;
                case ExpressionType.NotEqual: pyOperator = PythonOperationKind.NotEqual; break;
            }

            DynamicMetaObject res = null;
            if (pyOperator != null) {
                res = MakeBinaryOperation(operation, args, pyOperator.Value, errorSuggestion);
            } else {
                res = operation.FallbackBinaryOperation(target, arg);
            }

            return BindingHelpers.AddDynamicTestAndDefer(operation, BindingHelpers.AddPythonBoxing(res), args, valInfo);
        }

        public static DynamicMetaObject/*!*/ Operation(UnaryOperationBinder/*!*/ operation, DynamicMetaObject arg, DynamicMetaObject errorSuggestion) {
            PerfTrack.NoteEvent(PerfTrack.Categories.Binding, "Fallback UnaryOperator " + " " + operation.Operation + " " + arg.LimitType.FullName);
            PerfTrack.NoteEvent(PerfTrack.Categories.BindingTarget, operation.Operation.ToString());

            DynamicMetaObject[] args = new[] { arg };
            if (arg.NeedsDeferral()) {
                return operation.Defer(arg);
            }

            ValidationInfo valInfo = BindingHelpers.GetValidationInfo(args);

            DynamicMetaObject res = null;
            Type retType = typeof(object);
            switch (operation.Operation) {
                case ExpressionType.UnaryPlus:
                    res = BindingHelpers.AddPythonBoxing(MakeUnaryOperation(operation, arg, "__pos__", errorSuggestion));
                    break;
                case ExpressionType.Negate:
                    res = BindingHelpers.AddPythonBoxing(MakeUnaryOperation(operation, arg, "__neg__", errorSuggestion));
                    break;
                case ExpressionType.OnesComplement:
                    res = BindingHelpers.AddPythonBoxing(MakeUnaryOperation(operation, arg, "__invert__", errorSuggestion));
                    break;
                case ExpressionType.Not:
                    res = MakeUnaryNotOperation(operation, arg, typeof(object), errorSuggestion);
                    break;
                case ExpressionType.IsFalse:
                    res = MakeUnaryNotOperation(operation, arg, typeof(bool), errorSuggestion);
                    retType = typeof(bool);
                    break;
                case ExpressionType.IsTrue:
                    res = ConvertToBool(operation, arg);
                    retType = typeof(bool);
                    break;
                default:
                    res = TypeError(operation, "unknown operation: " + operation.ToString(), args);
                    break;

            }

            return BindingHelpers.AddDynamicTestAndDefer(operation, res, args, valInfo, retType);
        }

        public static DynamicMetaObject/*!*/ Index(DynamicMetaObjectBinder/*!*/ operation, PythonIndexType index, DynamicMetaObject[] args) {
            return Index(operation, index, args, null);
        }

        public static DynamicMetaObject/*!*/ Index(DynamicMetaObjectBinder/*!*/ operation, PythonIndexType index, DynamicMetaObject[] args, DynamicMetaObject errorSuggestion) {
            if (args.Length >= 3) {
                PerfTrack.NoteEvent(PerfTrack.Categories.Binding, "Fallback Index " + " " + index + " " + args[0].LimitType + ", " + args[1].LimitType + ", " + args[2].LimitType + args.Length);
            } else {
                PerfTrack.NoteEvent(PerfTrack.Categories.Binding, "Fallback Index " + " " + index + " " + args[0].LimitType + ", " + args[1].LimitType + args.Length);
            }
            PerfTrack.NoteEvent(PerfTrack.Categories.BindingTarget, index.ToString());
            if (BindingHelpers.NeedsDeferral(args)) {
                return operation.Defer(args);
            }

            ValidationInfo valInfo = BindingHelpers.GetValidationInfo(args[0]);

            DynamicMetaObject res = BindingHelpers.AddPythonBoxing(MakeIndexerOperation(operation, index, args, errorSuggestion));

            return BindingHelpers.AddDynamicTestAndDefer(operation, res, args, valInfo);
        }

        public static DynamicMetaObject/*!*/ Operation(PythonOperationBinder/*!*/ operation, params DynamicMetaObject/*!*/[]/*!*/ args) {
            if (args.Length == 1) {
                PerfTrack.NoteEvent(PerfTrack.Categories.Binding, "Fallback PythonOp " + " " + operation.Operation + " " + args[0].LimitType);
            } else {
                PerfTrack.NoteEvent(PerfTrack.Categories.Binding, "Fallback PythonOp " + " " + operation.Operation + " " + args[0].LimitType + ", " + args[1].LimitType);
            }
            PerfTrack.NoteEvent(PerfTrack.Categories.BindingTarget, operation.Operation.ToString());
            if (BindingHelpers.NeedsDeferral(args)) {
                return operation.Defer(args);
            }

            return MakeOperationRule(operation, args);
        }

        private static DynamicMetaObject/*!*/ MakeOperationRule(PythonOperationBinder/*!*/ operation, DynamicMetaObject/*!*/[]/*!*/ args) {
            ValidationInfo valInfo = BindingHelpers.GetValidationInfo(args);
            DynamicMetaObject res;

            Type deferType = typeof(object);
            switch (operation.Operation) {
                case PythonOperationKind.Documentation:
                    res = BindingHelpers.AddPythonBoxing(MakeDocumentationOperation(operation, args));
                    break;
                case PythonOperationKind.CallSignatures:
                    res = BindingHelpers.AddPythonBoxing(MakeCallSignatureOperation(args[0], CompilerHelpers.GetMethodTargets(args[0].Value)));
                    break;
                case PythonOperationKind.IsCallable:
                    res = MakeIscallableOperation(operation, args);
                    break;
                case PythonOperationKind.Hash:
                    res = MakeHashOperation(operation, args[0]);
                    break;
                case PythonOperationKind.Contains:
                    res = MakeContainsOperation(operation, args);
                    break;
                case PythonOperationKind.AbsoluteValue:
                    res = BindingHelpers.AddPythonBoxing(MakeUnaryOperation(operation, args[0], "__abs__", null));
                    break;
                case PythonOperationKind.GetEnumeratorForIteration:
                    res = MakeEnumeratorOperation(operation, args[0]);
                    break;
                default:
                    res = BindingHelpers.AddPythonBoxing(MakeBinaryOperation(operation, args, operation.Operation, null));
                    break;
            }


            return BindingHelpers.AddDynamicTestAndDefer(operation, res, args, valInfo, deferType);

        }

        private static DynamicMetaObject MakeBinaryOperation(DynamicMetaObjectBinder operation, DynamicMetaObject/*!*/[] args, PythonOperationKind opStr, DynamicMetaObject errorSuggestion) {
            if (IsComparison(opStr)) {
                return MakeComparisonOperation(args, operation, opStr, errorSuggestion);
            }

            return MakeSimpleOperation(args, operation, opStr, errorSuggestion);
        }

        #region Unary Operations

        /// <summary>
        /// Creates a rule for the contains operator.  This is exposed via "x in y" in 
        /// IronPython.  It is implemented by calling the __contains__ method on x and
        /// passing in y.  
        /// 
        /// If a type doesn't define __contains__ but does define __getitem__ then __getitem__ is 
        /// called repeatedly in order to see if the object is there.
        /// 
        /// For normal .NET enumerables we'll walk the iterator and see if it's present.
        /// </summary>
        private static DynamicMetaObject/*!*/ MakeContainsOperation(PythonOperationBinder/*!*/ operation, DynamicMetaObject/*!*/[]/*!*/ types) {
            DynamicMetaObject res;
            // the paramteres come in backwards from how we look up __contains__, flip them.
            Debug.Assert(types.Length == 2);
            ArrayUtils.SwapLastTwo(types);

            PythonContext state = PythonContext.GetPythonContext(operation);
            SlotOrFunction sf = SlotOrFunction.GetSlotOrFunction(state, "__contains__", types);

            if (sf.Success) {
                // just a call to __contains__
                res = sf.Target;
            } else {
                var types1 = types[1];
                RestrictTypes(types);

                sf = SlotOrFunction.GetSlotOrFunction(state, "__iter__", types[0]);
                if (sf.Success) {
                    types[1] = types1; // restore types[1] value to prevent reboxing
                    // iterate using __iter__
                    res = new DynamicMetaObject(
                        Ast.Call(
                            typeof(PythonOps).GetMethod(nameof(PythonOps.ContainsFromEnumerable)),
                            AstUtils.Constant(state.SharedContext),
                            sf.Target.Expression,
                            AstUtils.Convert(types[1].Expression, typeof(object))
                        ),
                        BindingRestrictions.Combine(types)
                    );
                } else {
                    ParameterExpression curIndex = Ast.Variable(typeof(int), "count");
                    sf = SlotOrFunction.GetSlotOrFunction(state, "__getitem__", types[0], new DynamicMetaObject(curIndex, BindingRestrictions.Empty));
                    if (sf.Success) {
                        // defines __getitem__, need to loop over the indexes and see if we match

                        ParameterExpression getItemRes = Ast.Variable(sf.ReturnType, "getItemRes");
                        ParameterExpression containsRes = Ast.Variable(typeof(bool), "containsRes");

                        LabelTarget target = Ast.Label();
                        res = new DynamicMetaObject(
                            Ast.Block(
                                new ParameterExpression[] { curIndex, getItemRes, containsRes },
                                Utils.Loop(
                                    null,                                                     // test
                                    Ast.Assign(curIndex, Ast.Add(curIndex, AstUtils.Constant(1))), // increment
                                    Ast.Block(                                            // body
                            // getItemRes = param0.__getitem__(curIndex)
                                        Utils.Try(
                                            Ast.Block(
                                                Ast.Assign(
                                                    getItemRes,
                                                    sf.Target.Expression
                                                ),
                                                Ast.Empty()
                                            )
                                        ).Catch(
                            // end of indexes, return false
                                            typeof(IndexOutOfRangeException),
                                            Ast.Break(target)
                                        ),
                            // if(getItemRes == param1) return true
                                        Utils.If(
                                            DynamicExpression.Dynamic(
                                                state.BinaryOperationRetType(
                                                    state.BinaryOperation(ExpressionType.Equal),
                                                    state.Convert(typeof(bool), ConversionResultKind.ExplicitCast)
                                                ),
                                                typeof(bool),
                                                types[1].Expression,
                                                getItemRes
                                            ),
                                            Ast.Assign(containsRes, AstUtils.Constant(true)),
                                            Ast.Break(target)
                                        ),
                                        AstUtils.Empty()
                                    ),
                                    null,                                               // loop else
                                    target,                                             // break label target
                                    null
                                ),
                                containsRes
                            ),
                            BindingRestrictions.Combine(types)
                        );
                    } else {
                        // non-iterable object
                        res = new DynamicMetaObject(
                            operation.Throw(
                                Ast.Call(
                                    typeof(PythonOps).GetMethod(nameof(PythonOps.TypeErrorForNonIterableObject)),
                                    AstUtils.Convert(
                                        types[0].Expression,
                                        typeof(object)
                                    )
                                ),
                                typeof(bool)
                            ),
                            BindingRestrictions.Combine(types)
                        );
                    }
                }
            }

            if (res.GetLimitType() != typeof(bool) && res.GetLimitType() != typeof(void)) {
                res = new DynamicMetaObject(
                    DynamicExpression.Dynamic(
                        state.Convert(
                            typeof(bool),
                            ConversionResultKind.ExplicitCast
                        ),
                        typeof(bool),
                        res.Expression
                    ),
                    res.Restrictions
                );
            }

            return res;
        }

        private static void RestrictTypes(DynamicMetaObject/*!*/[] types) {
            for (int i = 0; i < types.Length; i++) {
                types[i] = types[i].Restrict(types[i].GetLimitType());
            }
        }

        private static DynamicMetaObject/*!*/ MakeHashOperation(PythonOperationBinder/*!*/ operation, DynamicMetaObject/*!*/ self) {
            self = self.Restrict(self.GetLimitType());

            PythonContext state = PythonContext.GetPythonContext(operation);
            SlotOrFunction func = SlotOrFunction.GetSlotOrFunction(state, "__hash__", self);
            DynamicMetaObject res = func.Target;

            if (func.IsNull) {
                // Python 2.6 setting __hash__ = None makes the type unhashable
                res = new DynamicMetaObject(
                    operation.Throw(
                        Expression.Call(
                            typeof(PythonOps).GetMethod(nameof(PythonOps.TypeErrorForUnhashableObject)),
                            self.Expression
                        ),
                        typeof(int)
                    ),
                    res.Restrictions
                );
            } else if (func.ReturnType != typeof(int)) {
                if (func.ReturnType == typeof(BigInteger)) {
                    // Python 2.5 defines the result of returning a long as hashing the long
                    res = new DynamicMetaObject(
                        HashBigInt(operation, res.Expression),
                        res.Restrictions
                    );
                } else if (func.ReturnType == typeof(object)) {
                    // need to get the integer value here...
                    ParameterExpression tempVar = Ast.Parameter(typeof(object), "hashTemp");

                    res = new DynamicMetaObject(
                            Expression.Block(
                                new[] { tempVar },
                                Expression.Assign(tempVar, res.Expression),
                                Expression.Condition(
                                    Expression.TypeIs(tempVar, typeof(int)),
                                    Expression.Convert(tempVar, typeof(int)),
                                    Expression.Condition(
                                        Expression.TypeIs(tempVar, typeof(BigInteger)),
                                        HashBigInt(operation, tempVar),
                                        HashConvertToInt(state, tempVar)
                                    )
                                )
                            ),
                            res.Restrictions
                        );
                } else {
                    // need to convert unknown value to object
                    res = new DynamicMetaObject(
                        HashConvertToInt(state, res.Expression),
                        res.Restrictions
                    );
                }
            }

            return res;
        }

        private static DynamicExpression/*!*/ HashBigInt(PythonOperationBinder/*!*/ operation, Expression/*!*/ expression) {
            return DynamicExpression.Dynamic(
                operation,
                typeof(int),
                expression
            );
        }

        private static DynamicExpression/*!*/ HashConvertToInt(PythonContext/*!*/ state, Expression/*!*/ expression) {
            return DynamicExpression.Dynamic(
                state.Convert(
                    typeof(int),
                    ConversionResultKind.ExplicitCast
                ),
                typeof(int),
                expression
            );
        }

        private static DynamicMetaObject MakeUnaryOperation(DynamicMetaObjectBinder binder, DynamicMetaObject self, string symbol, DynamicMetaObject errorSuggestion) {
            self = self.Restrict(self.GetLimitType());

            SlotOrFunction func = SlotOrFunction.GetSlotOrFunction(PythonContext.GetPythonContext(binder), symbol, self);

            if (!func.Success) {
                // we get the error message w/ {0} so that PythonBinderHelper.TypeError formats it correctly
                return errorSuggestion ?? TypeError(binder, MakeUnaryOpErrorMessage(symbol, "{0}"), self);
            }

            return func.Target;
        }

        private static DynamicMetaObject MakeEnumeratorOperation(PythonOperationBinder operation, DynamicMetaObject self) {
            if (self.GetLimitType() == typeof(string)) {
                self = self.Restrict(self.GetLimitType());

                return new DynamicMetaObject(
                    Expression.Call(
                        typeof(PythonOps).GetMethod(nameof(PythonOps.StringEnumerator)),
                        self.Expression
                    ),
                    self.Restrictions
                );
            } else if (self.GetLimitType() == typeof(Bytes)) {
                self = self.Restrict(self.GetLimitType());

                return new DynamicMetaObject(
                    Expression.Call(
                        typeof(PythonOps).GetMethod(nameof(PythonOps.BytesEnumerator)),
                        self.Expression
                    ),
                    self.Restrictions
                );
            } else if ((self.Value is IEnumerable ||
                    typeof(IEnumerable).IsAssignableFrom(self.GetLimitType())) && !(self.Value is PythonGenerator)) {
                self = self.Restrict(self.GetLimitType());

                return new DynamicMetaObject(
                    Expression.Call(
                        typeof(PythonOps).GetMethod(nameof(PythonOps.GetEnumeratorFromEnumerable)),
                        Expression.Convert(
                            self.Expression,
                            typeof(IEnumerable)
                        )
                    ),
                    self.Restrictions
                );

            } else if (self.Value is IEnumerator ||                              // check for COM object (and fast check when we have values)
                    typeof(IEnumerator).IsAssignableFrom(self.GetLimitType())) { // check if we don't have a value
                DynamicMetaObject ieres = new DynamicMetaObject(
                    MakeEnumeratorResult(
                        Ast.Convert(
                            self.Expression,
                            typeof(IEnumerator)
                        )
                    ),
                    self.Restrict(self.GetLimitType()).Restrictions
                );

#if FEATURE_COM
                if (Microsoft.Scripting.ComInterop.ComBinder.IsComObject(self.Value)) {
                    ieres = new DynamicMetaObject(
                         MakeEnumeratorResult(
                                Expression.Convert(
                                 self.Expression,
                                 typeof(IEnumerator)
                             )
                         ),
                         ieres.Restrictions.Merge(
                            BindingRestrictions.GetExpressionRestriction(
                                Ast.TypeIs(self.Expression, typeof(IEnumerator))
                            )
                        )
                    );
                }
#endif

                return ieres;
            }

            ParameterExpression tmp = Ast.Parameter(typeof(IEnumerator), "enum");
            PythonConversionBinder convBinder = PythonContext.GetPythonContext(operation).Convert(typeof(IEnumerator), ConversionResultKind.ExplicitTry);

            DynamicMetaObject res = self is IPythonConvertible pyConv
                ? pyConv.BindConvert(convBinder)
                : convBinder.Bind(self, Array.Empty<DynamicMetaObject>());

            return new DynamicMetaObject(
                Expression.Block(
                    new[] { tmp },
                    Ast.Condition(
                        Ast.NotEqual(
                            Ast.Assign(tmp, res.Expression),
                            AstUtils.Constant(null)
                        ),
                        MakeEnumeratorResult(tmp),
                        Ast.Call(
                            typeof(PythonOps).GetMethod(nameof(PythonOps.ThrowTypeErrorForBadIteration)),
                            PythonContext.GetCodeContext(operation),
                            self.Expression
                        )
                    )
                ),
                res.Restrictions
            );
        }

        private static NewExpression MakeEnumeratorResult(Expression tmp) {
            return Expression.New(
                typeof(KeyValuePair<IEnumerator, IDisposable>).GetConstructor(new[] { typeof(IEnumerator), typeof(IDisposable) }),
                tmp,
                Expression.Constant(null, typeof(IDisposable))
            );
        }

        private static DynamicMetaObject/*!*/ MakeUnaryNotOperation(DynamicMetaObjectBinder/*!*/ operation, DynamicMetaObject/*!*/ self, Type retType, DynamicMetaObject errorSuggestion) {
            self = self.Restrict(self.GetLimitType());

            Expression notExpr;

            var res = ConvertToBool(operation, self);
            if (res is null) {
                // no __len__ or __bool__, for None this is always false, everything else is True.  If we have
                // an error suggestion though we'll go with that.
                if (errorSuggestion == null) {
                    notExpr = (self.GetLimitType() == typeof(DynamicNull)) ? AstUtils.Constant(true) : AstUtils.Constant(false);
                } else {
                    notExpr = errorSuggestion.Expression;
                }
            }
            else {
                Debug.Assert(res.Expression.Type == typeof(bool));
                notExpr = Ast.IsFalse(res.Expression);
                self = res;
            }

            if (retType == typeof(object) && notExpr.Type == typeof(bool)) {
                notExpr = BindingHelpers.AddPythonBoxing(notExpr);
            }

            return new DynamicMetaObject(
                notExpr,
                self.Restrictions
            );
        }

        #endregion

        #region Reflective Operations

        private static DynamicMetaObject/*!*/ MakeDocumentationOperation(PythonOperationBinder/*!*/ operation, DynamicMetaObject/*!*/[]/*!*/ args) {
            PythonContext state = PythonContext.GetPythonContext(operation);

            return new DynamicMetaObject(
                Binders.Convert(
                    PythonContext.GetCodeContext(operation),
                    state,
                    typeof(string),
                    ConversionResultKind.ExplicitCast,
                    Binders.Get(
                        PythonContext.GetCodeContext(operation),
                        state,
                        typeof(object),
                        "__doc__",
                        args[0].Expression
                    )
                ),
                args[0].Restrictions
            );
        }

        internal static DynamicMetaObject/*!*/ MakeCallSignatureOperation(DynamicMetaObject/*!*/ self, IList<MethodBase/*!*/>/*!*/ targets) {
            List<string> arrres = new List<string>();
            foreach (MethodBase mb in targets) {
                StringBuilder res = new StringBuilder();
                string comma = "";

                Type retType = mb.GetReturnType();
                if (retType != typeof(void)) {
                    res.Append(DynamicHelpers.GetPythonTypeFromType(retType).Name);
                    res.Append(" ");
                }

                if (mb is MethodInfo mi) {
                    string name;
                    NameConverter.TryGetName(DynamicHelpers.GetPythonTypeFromType(mb.DeclaringType), mi, out name);
                    res.Append(name);
                } else {
                    res.Append(DynamicHelpers.GetPythonTypeFromType(mb.DeclaringType).Name);
                }

                res.Append("(");
                if (!CompilerHelpers.IsStatic(mb)) {
                    res.Append("self");
                    comma = ", ";
                }

                foreach (ParameterInfo pi in mb.GetParameters()) {
                    if (pi.ParameterType == typeof(CodeContext)) continue;

                    res.Append(comma);
                    res.Append(DynamicHelpers.GetPythonTypeFromType(pi.ParameterType).Name + " " + pi.Name);
                    comma = ", ";
                }
                res.Append(")");
                arrres.Add(res.ToString());
            }

            return new DynamicMetaObject(
                AstUtils.Constant(arrres.ToArray()),
                self.Restrictions.Merge(BindingRestrictions.GetInstanceRestriction(self.Expression, self.Value))
            );
        }

        private static DynamicMetaObject/*!*/ MakeIscallableOperation(PythonOperationBinder/*!*/ operation, DynamicMetaObject/*!*/[]/*!*/ args) {
            // Certain non-python types (encountered during interop) are callable, but don't have 
            // a __call__ attribute. The default base binder also checks these, but since we're overriding
            // the base binder, we check them here.
            DynamicMetaObject self = args[0];

            // only applies when called from a Python site
            if (typeof(Delegate).IsAssignableFrom(self.GetLimitType()) ||
                typeof(MethodGroup).IsAssignableFrom(self.GetLimitType())) {
                return new DynamicMetaObject(
                    AstUtils.Constant(true),
                    self.Restrict(self.GetLimitType()).Restrictions
                );
            }

            PythonContext state = PythonContext.GetPythonContext(operation);
            Expression isCallable = Ast.NotEqual(
                Binders.TryGet(
                    PythonContext.GetCodeContext(operation),
                    state,
                    typeof(object),
                    "__call__",
                    self.Expression
                ),
                AstUtils.Constant(OperationFailed.Value)
            );

            return new DynamicMetaObject(
                isCallable,
                self.Restrict(self.GetLimitType()).Restrictions
            );
        }

        #endregion

        #region Common Binary Operations

        private static DynamicMetaObject/*!*/ MakeSimpleOperation(DynamicMetaObject/*!*/[]/*!*/ types, DynamicMetaObjectBinder/*!*/ binder, PythonOperationKind operation, DynamicMetaObject errorSuggestion) {
            RestrictTypes(types);

            SlotOrFunction fbinder;
            SlotOrFunction rbinder;
            PythonTypeSlot fSlot;
            PythonTypeSlot rSlot;
            GetOperatorMethods(types, operation, PythonContext.GetPythonContext(binder), out fbinder, out rbinder, out fSlot, out rSlot);

            return MakeBinaryOperatorResult(types, binder, operation, fbinder, rbinder, fSlot, rSlot, errorSuggestion);
        }

        private static void GetOperatorMethods(DynamicMetaObject/*!*/[]/*!*/ types, PythonOperationKind oper, PythonContext state, out SlotOrFunction fbinder, out SlotOrFunction rbinder, out PythonTypeSlot fSlot, out PythonTypeSlot rSlot) {
            oper &= ~PythonOperationKind.InPlace;

            string op, rop;
            if (!IsReverseOperator(oper)) {
                op = Symbols.OperatorToSymbol(oper);
                rop = Symbols.OperatorToReversedSymbol(oper);
            } else {
                // coming back after coercion, just try reverse operator.
                rop = Symbols.OperatorToSymbol(oper);
                op = Symbols.OperatorToReversedSymbol(oper);
            }

            fSlot = null;
            rSlot = null;
            PythonType fParent, rParent;

            bool isSequence = IsSequence(types[0]);

            if (oper == PythonOperationKind.Multiply &&
                isSequence &&
                !PythonOps.IsNonExtensibleNumericType(types[1].GetLimitType())) {
                // class M:
                //      def __rmul__(self, other):
                //          print("CALLED")
                //          return 1
                //
                // print [1,2] * M()
                //
                // in CPython this results in a successful call to __rmul__ on the type ignoring the forward
                // multiplication.  But calling the __mul__ method directly does NOT return NotImplemented like
                // one might expect.  Therefore we explicitly convert the MetaObject argument into an Index
                // for binding purposes.  That allows this to work at multiplication time but not with
                // a direct call to __mul__.

                DynamicMetaObject[] newTypes = new DynamicMetaObject[2];
                newTypes[0] = types[0];
                newTypes[1] = new DynamicMetaObject(
                    Ast.New(
                        typeof(Index).GetConstructor(new Type[] { typeof(object) }),
                        AstUtils.Convert(types[1].Expression, typeof(object))
                    ),
                    BindingRestrictions.Empty
                );
                types = newTypes;
            }

            if (!SlotOrFunction.TryGetBinder(state, types, op, null, out fbinder, out fParent)) {
                foreach (PythonType pt in MetaPythonObject.GetPythonType(types[0]).ResolutionOrder) {
                    if (pt.TryLookupSlot(state.SharedContext, op, out fSlot)) {
                        fParent = pt;
                        break;
                    }
                }
            }

            if (!SlotOrFunction.TryGetBinder(state, types, null, rop, out rbinder, out rParent)) {
                foreach (PythonType pt in MetaPythonObject.GetPythonType(types[1]).ResolutionOrder) {
                    if (pt.TryLookupSlot(state.SharedContext, rop, out rSlot)) {
                        rParent = pt;
                        break;
                    }
                }
            }

            // TODO: this is incorrect - if the rslot returns NotImplemented then fslot is never called and a TypeError will be raised (https://github.com/IronLanguages/ironpython3/issues/1479)
            if (fParent != null && (rbinder.Success || rSlot != null) && rParent != fParent && rParent.IsSubclassOf(fParent)) {
                // Python says if x + subx and subx defines __r*__ we should call r*.
                fbinder = SlotOrFunction.Empty;
                fSlot = null;
            }

            // TODO: this is incorrect - if the rslot returns NotImplemented then fslot is never called and a TypeError will be raised (https://github.com/IronLanguages/ironpython3/issues/560)
            if (oper == PythonOperationKind.Add && fParent != null && (rbinder.Success || rSlot != null) && rParent != fParent && isSequence) {
                // if the left operand is a sequence and the right operand defines __radd__, we should call __radd__.
                fbinder = SlotOrFunction.Empty;
                fSlot = null;
            }
        }

        private static bool IsReverseOperator(PythonOperationKind oper) {
            return (oper & PythonOperationKind.Reversed) != 0;
        }

        private static bool IsSequence(DynamicMetaObject/*!*/ metaObject) {
            var limitType = metaObject.GetLimitType();
            if (typeof(PythonList).IsAssignableFrom(limitType) ||
                typeof(PythonTuple).IsAssignableFrom(limitType) ||
                typeof(ByteArray).IsAssignableFrom(limitType) ||
                typeof(Bytes).IsAssignableFrom(limitType) ||
                typeof(string).IsAssignableFrom(limitType)) {
                return true;
            }
            return false;
        }

        private static DynamicMetaObject/*!*/ MakeBinaryOperatorResult(DynamicMetaObject/*!*/[]/*!*/ types, DynamicMetaObjectBinder/*!*/ operation, PythonOperationKind op, SlotOrFunction/*!*/ fCand, SlotOrFunction/*!*/ rCand, PythonTypeSlot fSlot, PythonTypeSlot rSlot, DynamicMetaObject errorSuggestion) {
            Assert.NotNull(operation, fCand, rCand);

            SlotOrFunction fTarget, rTarget;
            PythonContext state = PythonContext.GetPythonContext(operation);

            ConditionalBuilder bodyBuilder = new ConditionalBuilder(operation);

            if ((op & PythonOperationKind.InPlace) != 0) {
                // in place operator, see if there's a specific method that handles it.
                SlotOrFunction function = SlotOrFunction.GetSlotOrFunction(PythonContext.GetPythonContext(operation), Symbols.OperatorToSymbol(op), types);

                // we don't do a coerce for in place operators if the lhs implements __iop__
                if (!MakeOneCompareGeneric(function, false, types, MakeCompareReturn, bodyBuilder, typeof(object))) {
                    // the method handles it and always returns a useful value.
                    return bodyBuilder.GetMetaObject(types);
                }
            }

            if (!SlotOrFunction.GetCombinedTargets(fCand, rCand, out fTarget, out rTarget) &&
                fSlot == null &&
                rSlot == null &&
                bodyBuilder.NoConditions) {
                return MakeRuleForNoMatch(operation, op, errorSuggestion, types);
            }

            if (MakeOneTarget(PythonContext.GetPythonContext(operation), fTarget, fSlot, bodyBuilder, false, types)) {
                if (rSlot != null) {
                    MakeSlotCall(PythonContext.GetPythonContext(operation), types, bodyBuilder, rSlot, true);
                    bodyBuilder.FinishCondition(MakeBinaryThrow(operation, op, types).Expression, typeof(object));
                } else if (MakeOneTarget(PythonContext.GetPythonContext(operation), rTarget, rSlot, bodyBuilder, false, types)) {
                    // need to fallback to throwing or coercion
                    bodyBuilder.FinishCondition(MakeBinaryThrow(operation, op, types).Expression, typeof(object));
                }
            }

            return bodyBuilder.GetMetaObject(types);
        }

        private static void MakeCompareReturn(ConditionalBuilder/*!*/ bodyBuilder, Expression retCondition, Expression/*!*/ retValue, bool isReverse, Type retType) {
            if (retCondition != null) {
                bodyBuilder.AddCondition(retCondition, retValue);
            } else {
                bodyBuilder.FinishCondition(retValue, retType);
            }
        }

        /// <summary>
        /// Delegate for finishing the comparison.   This takes in a condition and a return value and needs to update the ConditionalBuilder
        /// with the appropriate resulting body.  The condition may be null.
        /// </summary>
        private delegate void ComparisonHelper(ConditionalBuilder/*!*/ bodyBuilder, Expression retCondition, Expression/*!*/ retValue, bool isReverse, Type retType);

        /// <summary>
        /// Helper to handle a comparison operator call.  Checks to see if the call can
        /// return NotImplemented and allows the caller to modify the expression that
        /// is ultimately returned (e.g. to turn __cmp__ into a bool after a comparison)
        /// </summary>
        private static bool MakeOneCompareGeneric(SlotOrFunction/*!*/ target, bool reverse, DynamicMetaObject/*!*/[]/*!*/ types, ComparisonHelper returner, ConditionalBuilder/*!*/ bodyBuilder, Type retType) {
            if (target == SlotOrFunction.Empty || !target.Success) return true;

            ParameterExpression tmp;

            if (target.ReturnType == typeof(bool)) {
                tmp = bodyBuilder.CompareRetBool;
            } else {
                tmp = Ast.Variable(target.ReturnType, "compareRetValue");
                bodyBuilder.AddVariable(tmp);
            }

            if (target.MaybeNotImplemented) {
                Expression call = target.Target.Expression;
                Expression assign = Ast.Assign(tmp, call);

                returner(
                    bodyBuilder,
                    Ast.NotEqual(
                        assign,
                        AstUtils.Constant(PythonOps.NotImplemented)
                    ),
                    tmp,
                    reverse,
                    retType);
                return true;
            } else {
                returner(
                    bodyBuilder,
                    null,
                    target.Target.Expression,
                    reverse,
                    retType
                );
                return false;
            }
        }

        private static bool MakeOneTarget(PythonContext/*!*/ state, SlotOrFunction/*!*/ target, PythonTypeSlot slotTarget, ConditionalBuilder/*!*/ bodyBuilder, bool reverse, DynamicMetaObject/*!*/[]/*!*/ types) {
            if (target == SlotOrFunction.Empty && slotTarget == null) return true;

            if (slotTarget != null) {
                MakeSlotCall(state, types, bodyBuilder, slotTarget, reverse);
                return true;
            } else if (target.MaybeNotImplemented) {
                Debug.Assert(target.ReturnType == typeof(object));

                ParameterExpression tmp = Ast.Variable(typeof(object), "slot");
                bodyBuilder.AddVariable(tmp);

                bodyBuilder.AddCondition(
                    Ast.NotEqual(
                        Ast.Assign(
                            tmp,
                            target.Target.Expression
                        ),
                        Ast.Property(null, typeof(PythonOps).GetProperty(nameof(PythonOps.NotImplemented)))
                    ),
                    tmp
                );

                return true;
            } else {
                bodyBuilder.FinishCondition(target.Target.Expression, typeof(object));
                return false;
            }
        }

        private static void MakeSlotCall(PythonContext/*!*/ state, DynamicMetaObject/*!*/[]/*!*/ types, ConditionalBuilder/*!*/ bodyBuilder, PythonTypeSlot/*!*/ slotTarget, bool reverse) {
            Debug.Assert(slotTarget != null);

            Expression self, other;
            if (reverse) {
                self = types[1].Expression;
                other = types[0].Expression;
            } else {
                self = types[0].Expression;
                other = types[1].Expression;
            }

            MakeSlotCallWorker(state, slotTarget, self, bodyBuilder, other);
        }

        private static void MakeSlotCallWorker(PythonContext/*!*/ state, PythonTypeSlot/*!*/ slotTarget, Expression/*!*/ self, ConditionalBuilder/*!*/ bodyBuilder, params Expression/*!*/[]/*!*/ args) {
            // Generate:
            // 
            // SlotTryGetValue(context, slot, selfType, out callable) && (tmp=callable(args)) != NotImplemented) ?
            //      tmp :
            //      RestOfOperation
            //
            ParameterExpression callable = Ast.Variable(typeof(object), "slot");
            ParameterExpression tmp = Ast.Variable(typeof(object), "slot");

            bodyBuilder.AddCondition(
                Ast.AndAlso(
                    Ast.Call(
                        typeof(PythonOps).GetMethod(nameof(PythonOps.SlotTryGetValue)),
                        AstUtils.Constant(state.SharedContext),
                        AstUtils.Convert(Utils.WeakConstant(slotTarget), typeof(PythonTypeSlot)),
                        AstUtils.Convert(self, typeof(object)),
                        Ast.Call(
                            typeof(DynamicHelpers).GetMethod(nameof(DynamicHelpers.GetPythonType)),
                            AstUtils.Convert(self, typeof(object))
                        ),
                        callable
                    ),
                    Ast.NotEqual(
                        Ast.Assign(
                            tmp,
                            DynamicExpression.Dynamic(
                                state.Invoke(
                                    new CallSignature(args.Length)
                                ),
                                typeof(object),
                                ArrayUtils.Insert(AstUtils.Constant(state.SharedContext), (Expression)callable, args)
                            )
                        ),
                        Ast.Property(null, typeof(PythonOps).GetProperty(nameof(PythonOps.NotImplemented)))
                    )
                ),
                tmp
            );
            bodyBuilder.AddVariable(callable);
            bodyBuilder.AddVariable(tmp);
        }

        #endregion

        #region Comparison Operations

        private static DynamicMetaObject/*!*/ MakeComparisonOperation(DynamicMetaObject/*!*/[]/*!*/ types, DynamicMetaObjectBinder/*!*/ operation, PythonOperationKind op, DynamicMetaObject errorSuggestion) {
            RestrictTypes(types);

            PythonContext state = PythonContext.GetPythonContext(operation);
            Debug.Assert(types.Length == 2);
            DynamicMetaObject xType = types[0], yType = types[1];
            string opSym = Symbols.OperatorToSymbol(op);
            string ropSym = Symbols.OperatorToReversedSymbol(op);
            // reverse
            DynamicMetaObject[] rTypes = new DynamicMetaObject[] { types[1], types[0] };

            SlotOrFunction fop = SlotOrFunction.GetSlotOrFunction(state, opSym, types);
            SlotOrFunction rop = SlotOrFunction.GetSlotOrFunction(state, ropSym, rTypes);

            ConditionalBuilder bodyBuilder = new ConditionalBuilder(operation);

            SlotOrFunction.GetCombinedTargets(fop, rop, out fop, out rop);

            bool shouldWarn = false;
            
            // if the left operand is an instance of a built-in type or a new-style class, and the right operand is an instance of a proper subclass of that type or class
            // and overrides the base's __rop__() method, the right operand's __rop__() method is tried before the left operand's __op__() method.
            PythonType xPythonType = MetaPythonObject.GetPythonType(xType);
            PythonType yPythonType = MetaPythonObject.GetPythonType(yType);
            bool opReverse = false;
            if ((rop != null) && (yPythonType != xPythonType) && BindingHelpers.IsSubclassOf(yType, xType)) {
                SlotOrFunction tmp = rop;
                rop = fop;
                fop = tmp;
                opReverse = true;
            }

            // first try __op__ or __rop__ and return the value
            shouldWarn = fop.ShouldWarn(state, out WarningInfo info);
            if (MakeOneCompareGeneric(fop, opReverse, types, MakeCompareReturn, bodyBuilder, typeof(object))) {
                shouldWarn = shouldWarn || rop.ShouldWarn(state, out info);
                if (MakeOneCompareGeneric(rop, !opReverse, types, MakeCompareReturn, bodyBuilder, typeof(object))) {
                    if (errorSuggestion != null) {
                        bodyBuilder.FinishCondition(errorSuggestion.Expression, typeof(object));
                    } else {
                        bodyBuilder.FinishCondition(BindingHelpers.AddPythonBoxing(MakeFallbackCompare(operation, op, types)), typeof(object));
                    }
                }
            }

            DynamicMetaObject res = bodyBuilder.GetMetaObject(types);
            if (!shouldWarn || res == null) {
                return res;
            } else {
                return info.AddWarning(Ast.Constant(state.SharedContext), res);
            }
        }

        private static Expression/*!*/ MakeFallbackCompare(DynamicMetaObjectBinder/*!*/ binder, PythonOperationKind op, DynamicMetaObject[] types) {
            if (op == PythonOperationKind.Equal || op == PythonOperationKind.NotEqual) {
                return Ast.Call(
                    GetComparisonFallbackMethod(op),
                    PythonContext.GetCodeContext(binder),
                    AstUtils.Convert(types[0].Expression, typeof(object)),
                    AstUtils.Convert(types[1].Expression, typeof(object))
                );
            }

            return MakeBinaryThrow(binder, op, types).Expression;

            static MethodInfo/*!*/ GetComparisonFallbackMethod(PythonOperationKind op) {
                string name;
                switch (op) {
                    case PythonOperationKind.Equal: name = nameof(PythonOps.CompareTypesEqual); break;
                    case PythonOperationKind.NotEqual: name = nameof(PythonOps.CompareTypesNotEqual); break;
                    default: throw new InvalidOperationException();
                }
                return typeof(PythonOps).GetMethod(name);
            }
        }

        #endregion

        #region Index Operations

        /// <summary>
        /// Python has three protocols for slicing:
        ///    Simple Slicing x[i:j]
        ///    Extended slicing x[i,j,k,...]
        ///    Long Slice x[start:stop:step]
        /// 
        /// These protocols map to __*item__ (get, set, and del).
        ///    This receives a single index which is either a Tuple or a Slice object (which 
        ///    encapsulates the start, stop, and step values) 
        /// 
        /// This is in addition to a simple indexing x[y].
        /// 
        /// For simple slicing and long slicing Python generates Operators.*Slice.  For
        /// the extended slicing and simple indexing Python generates a Operators.*Item
        /// action.
        /// 
        /// Extended slicing maps to the normal .NET multi-parameter input.  
        /// </summary>
        private static DynamicMetaObject/*!*/ MakeIndexerOperation(DynamicMetaObjectBinder/*!*/ operation, PythonIndexType op, DynamicMetaObject/*!*/[]/*!*/ types, DynamicMetaObject errorSuggestion) {
            string item;
            DynamicMetaObject indexedType = types[0].Restrict(types[0].GetLimitType());
            PythonContext state = PythonContext.GetPythonContext(operation);
            BuiltinFunction itemFunc = null;
            PythonTypeSlot itemSlot = null;

            GetIndexOperators(op, out item, out _);

            if (!BindingHelpers.TryGetStaticFunction(state, item, indexedType, out itemFunc)) {
                MetaPythonObject.GetPythonType(indexedType).TryResolveSlot(state.SharedContext, item, out itemSlot);
            }

            // make the Callable object which does the actual call to the function or slot
            Callable callable = Callable.MakeCallable(state, op, itemFunc, itemSlot);
            if (callable == null) {
                return errorSuggestion ?? MakeUnindexableError(operation, op, types, indexedType, state);
            }

            // prepare the arguments and make the builder which will call __*item__
            DynamicMetaObject[] args;
            ItemBuilder builder = new ItemBuilder(types, callable);
            if (IsSlice(op)) {
                // we need to create a new Slice object.
                args = GetItemSliceArguments(state, op, types);
            } else {
                // no need to restrict the arguments.  We're not
                // a slice and so restrictions are not necessary
                // here because it's not dependent upon our types.
                args = (DynamicMetaObject[])types.Clone();

                // but we do need to restrict based upon the type
                // of object we're calling on.
                args[0] = types[0].Restrict(types[0].GetLimitType());
            }

            return builder.MakeRule(operation, state, args);
        }

        private static DynamicMetaObject MakeUnindexableError(DynamicMetaObjectBinder operation, PythonIndexType op, DynamicMetaObject/*!*/[] types, DynamicMetaObject indexedType, PythonContext state) {
            DynamicMetaObject[] newTypes = (DynamicMetaObject[])types.Clone();
            newTypes[0] = indexedType;

            if (op != PythonIndexType.GetItem &&
                op != PythonIndexType.GetSlice &&
                DynamicHelpers.GetPythonType(indexedType.Value).TryResolveSlot(state.SharedContext, "__getitem__", out _)) {
                // object supports indexing but not setting/deletion
                if (op == PythonIndexType.SetItem || op == PythonIndexType.SetSlice) {
                    return TypeError(operation, "'{0}' object does not support item assignment", newTypes);
                } else {
                    return TypeError(operation, "'{0}' object doesn't support item deletion", newTypes);
                }
            }
            return TypeError(operation, "'{0}' object is not subscriptable", newTypes);
        }

        /// <summary>
        /// Helper to convert all of the arguments to their known types.
        /// </summary>
        private static DynamicMetaObject/*!*/[]/*!*/ ConvertArgs(DynamicMetaObject/*!*/[]/*!*/ types) {
            DynamicMetaObject[] res = new DynamicMetaObject[types.Length];
            for (int i = 0; i < types.Length; i++) {
                res[i] = types[i].Restrict(types[i].GetLimitType());
            }
            return res;
        }

        /// <summary>
        /// Gets the arguments that need to be provided to __*item__ when we need to pass a slice object.
        /// </summary>
        private static DynamicMetaObject/*!*/[]/*!*/ GetItemSliceArguments(PythonContext state, PythonIndexType op, DynamicMetaObject/*!*/[]/*!*/ types) {
            DynamicMetaObject[] args;
            if (op == PythonIndexType.SetSlice) {
                args = new DynamicMetaObject[] { 
                    types[0].Restrict(types[0].GetLimitType()),
                    GetSetSlice(state, types), 
                    types[types.Length- 1].Restrict(types[types.Length - 1].GetLimitType())
                };
            } else {
                Debug.Assert(op == PythonIndexType.GetSlice || op == PythonIndexType.DeleteSlice);

                args = new DynamicMetaObject[] { 
                    types[0].Restrict(types[0].GetLimitType()),
                    GetGetOrDeleteSlice(state, types)
                };
            }
            return args;
        }

        /// <summary>
        /// Base class for calling indexers.  We have two subclasses that target built-in functions and user defined callable objects.
        /// 
        /// The Callable objects get handed off to ItemBuilder's which then call them with the appropriate arguments.
        /// </summary>
        private abstract class Callable {
            private readonly PythonContext/*!*/ _binder;
            private readonly PythonIndexType _op;

            protected Callable(PythonContext/*!*/ binder, PythonIndexType op) {
                Assert.NotNull(binder);

                _binder = binder;
                _op = op;
            }

            /// <summary>
            /// Creates a new CallableObject.  If BuiltinFunction is available we'll create a BuiltinCallable otherwise
            /// we create a SlotCallable.
            /// </summary>
            public static Callable MakeCallable(PythonContext/*!*/ binder, PythonIndexType op, BuiltinFunction itemFunc, PythonTypeSlot itemSlot) {
                if (itemFunc != null) {
                    // we'll call a builtin function to produce the rule
                    return new BuiltinCallable(binder, op, itemFunc);
                } else if (itemSlot != null) {
                    // we'll call a PythonTypeSlot to produce the rule
                    return new SlotCallable(binder, op, itemSlot);
                }

                return null;
            }

            /// <summary>
            /// Gets the arguments in a form that should be used for extended slicing.
            /// 
            /// Python defines that multiple tuple arguments received (x[1,2,3]) get 
            /// packed into a Tuple.  For most .NET methods we just want to expand
            /// this into the multiple index arguments.  For slots and old-instances
            /// we want to pass in the tuple
            /// </summary>
            public virtual DynamicMetaObject[] GetTupleArguments(DynamicMetaObject[] arguments) {
                if (IsSetter) {
                    if (arguments.Length == 3) {
                        // simple setter, no extended slicing, no need to pack arguments into tuple
                        return arguments;
                    }

                    // we want self, (tuple, of, args, ...), value
                    Expression[] tupleArgs = new Expression[arguments.Length - 2];
                    BindingRestrictions restrictions = BindingRestrictions.Empty;
                    for (int i = 1; i < arguments.Length - 1; i++) {
                        tupleArgs[i - 1] = AstUtils.Convert(arguments[i].Expression, typeof(object));
                        restrictions = restrictions.Merge(arguments[i].Restrictions);
                    }
                    return new DynamicMetaObject[] {
                        arguments[0],
                        new DynamicMetaObject(
                            Ast.Call(
                                typeof(PythonOps).GetMethod(nameof(PythonOps.MakeTuple)),
                                Ast.NewArrayInit(typeof(object), tupleArgs)
                            ),
                            restrictions
                        ),
                        arguments[arguments.Length-1]
                    };
                } else if (arguments.Length == 2) {
                    // simple getter, no extended slicing, no need to pack arguments into tuple
                    return arguments;
                } else {
                    // we want self, (tuple, of, args, ...)
                    Expression[] tupleArgs = new Expression[arguments.Length - 1];
                    for (int i = 1; i < arguments.Length; i++) {
                        tupleArgs[i - 1] = AstUtils.Convert(arguments[i].Expression, typeof(object));
                    }
                    return new DynamicMetaObject[] {
                        arguments[0],
                        new DynamicMetaObject(
                            Ast.Call(
                                typeof(PythonOps).GetMethod(nameof(PythonOps.MakeTuple)),
                                Ast.NewArrayInit(typeof(object), tupleArgs)
                            ),
                            BindingRestrictions.Combine(ArrayUtils.RemoveFirst(arguments))
                        )
                    };
                }
            }

            /// <summary>
            /// Adds the target of the call to the rule.
            /// </summary>
            public abstract DynamicMetaObject/*!*/ CompleteRuleTarget(DynamicMetaObjectBinder/*!*/ metaBinder, DynamicMetaObject[] args, Func<DynamicMetaObject> customFailure);

            protected PythonBinder Binder {
                get { return _binder.Binder; }
            }

            protected PythonContext PythonContext {
                get { return _binder; }
            }

            protected bool IsSetter {
                get { return _op == PythonIndexType.SetItem || _op == PythonIndexType.SetSlice; }
            }
        }

        /// <summary>
        /// Subclass of Callable for a built-in function.  This calls a .NET method performing
        /// the appropriate bindings.
        /// </summary>
        private class BuiltinCallable : Callable {
            private readonly BuiltinFunction/*!*/ _bf;

            public BuiltinCallable(PythonContext/*!*/ binder, PythonIndexType op, BuiltinFunction/*!*/ func)
                : base(binder, op) {
                Assert.NotNull(func);

                _bf = func;
            }

            public override DynamicMetaObject[] GetTupleArguments(DynamicMetaObject[] arguments) {
                return arguments;
            }

            public override DynamicMetaObject/*!*/ CompleteRuleTarget(DynamicMetaObjectBinder/*!*/ metaBinder, DynamicMetaObject/*!*/[]/*!*/ args, Func<DynamicMetaObject> customFailure) {
                Assert.NotNull(args);
                Assert.NotNullItems(args);

                BindingTarget target;

                var resolver = new PythonOverloadResolver(
                    Binder,
                    args[0],
                    ArrayUtils.RemoveFirst(args),
                    new CallSignature(args.Length - 1),
                    AstUtils.Constant(PythonContext.SharedContext)
                );

                DynamicMetaObject res = Binder.CallMethod(
                    resolver,
                    _bf.Targets,
                    BindingRestrictions.Combine(args),
                    _bf.Name,
                    PythonNarrowing.None,
                    PythonNarrowing.IndexOperator,
                    out target
                );

                res = BindingHelpers.CheckLightThrowMO(metaBinder, res, target);

                if (target.Success) {
                    if (IsSetter) {
                        res = new DynamicMetaObject(
                            Ast.Block(res.Expression, args[args.Length - 1].Expression),
                            res.Restrictions
                        );
                    }

                    if (BindingWarnings.ShouldWarn(Binder.Context, target.Overload, out WarningInfo info)) {
                        res = info.AddWarning(Ast.Constant(PythonContext.SharedContext), res);
                    }
                } else if (customFailure == null || (res = customFailure()) == null) {
                    res = DefaultBinder.MakeError(resolver.MakeInvalidParametersError(target), BindingRestrictions.Combine(ConvertArgs(args)), typeof(object));
                }

                return res;
            }
        }

        /// <summary>
        /// Callable to a user-defined callable object.  This could be a Python function,
        /// a class defining __call__, etc...
        /// </summary>
        private class SlotCallable : Callable {
            private PythonTypeSlot _slot;

            public SlotCallable(PythonContext/*!*/ binder, PythonIndexType op, PythonTypeSlot slot)
                : base(binder, op) {
                _slot = slot;
            }

            public override DynamicMetaObject/*!*/ CompleteRuleTarget(DynamicMetaObjectBinder/*!*/ metaBinder, DynamicMetaObject/*!*/[]/*!*/ args, Func<DynamicMetaObject> customFailure) {
                ConditionalBuilder cb = new ConditionalBuilder();
                _slot.MakeGetExpression(
                    Binder,
                    AstUtils.Constant(PythonContext.SharedContext),
                    args[0],
                    new DynamicMetaObject(
                        Ast.Call(
                            typeof(DynamicHelpers).GetMethod(nameof(DynamicHelpers.GetPythonType)),
                            AstUtils.Convert(args[0].Expression, typeof(object))
                        ),
                        BindingRestrictions.Empty,
                        DynamicHelpers.GetPythonType(args[0].Value)
                    ),
                    cb
                );
                if (!cb.IsFinal) {
                    cb.FinishCondition(metaBinder.Throw(Ast.New(typeof(InvalidOperationException))));
                }

                Expression callable = cb.GetMetaObject().Expression;
                Expression[] exprArgs = new Expression[args.Length - 1];
                for (int i = 1; i < args.Length; i++) {
                    exprArgs[i - 1] = args[i].Expression;
                }

                Expression retVal = DynamicExpression.Dynamic(
                    PythonContext.Invoke(
                        new CallSignature(exprArgs.Length)
                    ),
                    typeof(object),
                    ArrayUtils.Insert(AstUtils.Constant(PythonContext.SharedContext), (Expression)callable, exprArgs)
                );

                if (IsSetter) {
                    retVal = Ast.Block(retVal, args[args.Length - 1].Expression);
                }

                return new DynamicMetaObject(
                    retVal,
                    BindingRestrictions.Combine(args)
                );
            }
        }

        /// <summary>
        /// Base class for building a __*item__ call.
        /// </summary>
        private abstract class IndexBuilder {
            private readonly Callable/*!*/ _callable;
            private readonly DynamicMetaObject/*!*/[]/*!*/ _types;

            public IndexBuilder(DynamicMetaObject/*!*/[]/*!*/ types, Callable/*!*/ callable) {
                _callable = callable;
                _types = types;
            }

            public abstract DynamicMetaObject/*!*/ MakeRule(DynamicMetaObjectBinder/*!*/ metaBinder, PythonContext/*!*/ binder, DynamicMetaObject/*!*/[]/*!*/ args);

            protected Callable/*!*/ Callable {
                get { return _callable; }
            }

            protected PythonType/*!*/ GetTypeAt(int index) {
                return MetaPythonObject.GetPythonType(_types[index]);
            }
        }

        /// <summary>
        /// Derived IndexBuilder for calling __*item__ methods.
        /// </summary>
        private class ItemBuilder : IndexBuilder {
            public ItemBuilder(DynamicMetaObject/*!*/[]/*!*/ types, Callable/*!*/ callable)
                : base(types, callable) {
            }

            public override DynamicMetaObject/*!*/ MakeRule(DynamicMetaObjectBinder/*!*/ metaBinder, PythonContext/*!*/ binder, DynamicMetaObject/*!*/[]/*!*/ args) {
                DynamicMetaObject[] tupleArgs = Callable.GetTupleArguments(args);
                return Callable.CompleteRuleTarget(metaBinder, tupleArgs, delegate() {
                    if (args[1].GetLimitType() != typeof(Slice) && GetTypeAt(1).TryResolveSlot(binder.SharedContext, "__index__", out _)) {
                        args[1] = new DynamicMetaObject(
                            DynamicExpression.Dynamic(
                                binder.Convert(
                                    typeof(int),
                                    ConversionResultKind.ExplicitCast
                                ),
                                typeof(int),
                                DynamicExpression.Dynamic(
                                    binder.InvokeNone,
                                    typeof(object),
                                    AstUtils.Constant(binder.SharedContext),
                                    Binders.Get(
                                        AstUtils.Constant(binder.SharedContext),
                                        binder,
                                        typeof(object),
                                        "__index__",
                                        args[1].Expression
                                    )
                                )
                            ),
                            BindingRestrictions.Empty
                        );

                        return Callable.CompleteRuleTarget(metaBinder, tupleArgs, null);
                    }
                    return null;
                });
            }
        }

        private static bool HasOnlyNumericTypes(DynamicMetaObjectBinder/*!*/ action, DynamicMetaObject/*!*/[]/*!*/ types, bool skipLast) {
            bool onlyNumeric = true;
            PythonContext state = PythonContext.GetPythonContext(action);

            for (int i = 1; i < (skipLast ? types.Length - 1 : types.Length); i++) {
                DynamicMetaObject obj = types[i];
                if (!IsIndexType(state, obj)) {
                    onlyNumeric = false;
                    break;
                }
            }
            return onlyNumeric;
        }

        private static bool IsIndexType(PythonContext/*!*/ state, DynamicMetaObject/*!*/ obj) {
            bool numeric = true;
            if (obj.GetLimitType() != typeof(MissingParameter) &&
                !PythonOps.IsNumericType(obj.GetLimitType())) {

                PythonType curType = MetaPythonObject.GetPythonType(obj);

                if (!curType.TryResolveSlot(state.SharedContext, "__index__", out _)) {
                    numeric = false;
                }
            }
            return numeric;
        }

        private static bool IsSlice(PythonIndexType op) {
            return op >= PythonIndexType.GetSlice;
        }

        /// <summary>
        /// Helper to get the symbols for __*item__ based upon if we're doing
        /// a get/set/delete and the minimum number of arguments required for each of those.
        /// </summary>
        private static void GetIndexOperators(PythonIndexType op, out string item, out int mandatoryArgs) {
            switch (op) {
                case PythonIndexType.GetItem:
                case PythonIndexType.GetSlice:
                    item = "__getitem__";
                    mandatoryArgs = 2;
                    return;
                case PythonIndexType.SetItem:
                case PythonIndexType.SetSlice:
                    item = "__setitem__";
                    mandatoryArgs = 3;
                    return;
                case PythonIndexType.DeleteItem:
                case PythonIndexType.DeleteSlice:
                    item = "__delitem__";
                    mandatoryArgs = 2;
                    return;
            }

            throw new InvalidOperationException();
        }

        private static DynamicMetaObject/*!*/ GetSetSlice(PythonContext state, DynamicMetaObject/*!*/[]/*!*/ args) {
            DynamicMetaObject[] newArgs = (DynamicMetaObject[])args.Clone();
            for (int i = 1; i < newArgs.Length; i++) {
                if (!IsIndexType(state, newArgs[i])) {
                    newArgs[i] = newArgs[i].Restrict(newArgs[i].GetLimitType());
                }
            }

            return new DynamicMetaObject(
                Ast.Call(
                    typeof(PythonOps).GetMethod(nameof(PythonOps.MakeSlice)),
                    AstUtils.Convert(GetSetParameter(newArgs, 1), typeof(object)),
                    AstUtils.Convert(GetSetParameter(newArgs, 2), typeof(object)),
                    AstUtils.Convert(GetSetParameter(newArgs, 3), typeof(object))
                ),
                BindingRestrictions.Combine(newArgs)
            );
        }

        private static DynamicMetaObject/*!*/ GetGetOrDeleteSlice(PythonContext state, DynamicMetaObject/*!*/[]/*!*/ args) {
            DynamicMetaObject[] newArgs = (DynamicMetaObject[])args.Clone();
            for (int i = 1; i < newArgs.Length; i++) {
                if (!IsIndexType(state, newArgs[i])) {
                    newArgs[i] = newArgs[i].Restrict(newArgs[i].GetLimitType());
                }
            }

            return new DynamicMetaObject(
                Ast.Call(
                    typeof(PythonOps).GetMethod(nameof(PythonOps.MakeSlice)),
                    AstUtils.Convert(GetGetOrDeleteParameter(newArgs, 1), typeof(object)),
                    AstUtils.Convert(GetGetOrDeleteParameter(newArgs, 2), typeof(object)),
                    AstUtils.Convert(GetGetOrDeleteParameter(newArgs, 3), typeof(object))
                ),
                BindingRestrictions.Combine(newArgs)
            );
        }

        private static Expression/*!*/ GetGetOrDeleteParameter(DynamicMetaObject/*!*/[]/*!*/ args, int index) {
            if (args.Length > index) {
                return CheckMissing(args[index].Expression);
            }
            return AstUtils.Constant(null);
        }

        private static Expression GetSetParameter(DynamicMetaObject[] args, int index) {
            if (args.Length > (index + 1)) {
                return CheckMissing(args[index].Expression);
            }

            return AstUtils.Constant(null);
        }


        #endregion

        #region Helpers

        private static bool IsComparison(PythonOperationKind op) {
            return (op & PythonOperationKind.Comparison) != 0;
        }

        private static Expression/*!*/ GetCompareNode(PythonOperationKind op, bool reverse, Expression expr) {
            switch (reverse ? OperatorToReverseOperator(op) : op) {
                case PythonOperationKind.Equal: return Ast.Equal(expr, AstUtils.Constant(0));
                case PythonOperationKind.NotEqual: return Ast.NotEqual(expr, AstUtils.Constant(0));
                case PythonOperationKind.GreaterThan: return Ast.GreaterThan(expr, AstUtils.Constant(0));
                case PythonOperationKind.GreaterThanOrEqual: return Ast.GreaterThanOrEqual(expr, AstUtils.Constant(0));
                case PythonOperationKind.LessThan: return Ast.LessThan(expr, AstUtils.Constant(0));
                case PythonOperationKind.LessThanOrEqual: return Ast.LessThanOrEqual(expr, AstUtils.Constant(0));
                default: throw new InvalidOperationException();
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        public static PythonOperationKind OperatorToReverseOperator(PythonOperationKind op) {
            switch (op) {
                case PythonOperationKind.LessThan: return PythonOperationKind.GreaterThan;
                case PythonOperationKind.LessThanOrEqual: return PythonOperationKind.GreaterThanOrEqual;
                case PythonOperationKind.GreaterThan: return PythonOperationKind.LessThan;
                case PythonOperationKind.GreaterThanOrEqual: return PythonOperationKind.LessThanOrEqual;
                case PythonOperationKind.Equal: return PythonOperationKind.Equal;
                case PythonOperationKind.NotEqual: return PythonOperationKind.NotEqual;
                case PythonOperationKind.DivMod: return PythonOperationKind.ReverseDivMod;
                case PythonOperationKind.ReverseDivMod: return PythonOperationKind.DivMod;
                default:
                    return op & ~PythonOperationKind.Reversed;
            }
        }
        private static Expression/*!*/ GetCompareExpression(PythonOperationKind op, bool reverse, Expression/*!*/ value) {
            Debug.Assert(value.Type == typeof(int));

            Expression zero = AstUtils.Constant(0);
            Expression res;
            switch (reverse ? OperatorToReverseOperator(op) : op) {
                case PythonOperationKind.Equal: res = Ast.Equal(value, zero); break;
                case PythonOperationKind.NotEqual: res = Ast.NotEqual(value, zero); break;
                case PythonOperationKind.GreaterThan: res = Ast.GreaterThan(value, zero); break;
                case PythonOperationKind.GreaterThanOrEqual: res = Ast.GreaterThanOrEqual(value, zero); break;
                case PythonOperationKind.LessThan: res = Ast.LessThan(value, zero); break;
                case PythonOperationKind.LessThanOrEqual: res = Ast.LessThanOrEqual(value, zero); break;
                default: throw new InvalidOperationException();
            }

            return BindingHelpers.AddPythonBoxing(res);
        }

        internal static Expression/*!*/ CheckMissing(Expression/*!*/ toCheck) {
            if (toCheck.Type == typeof(MissingParameter)) {
                return AstUtils.Constant(null);
            }
            if (toCheck.Type != typeof(object)) {
                return toCheck;
            }

            return Ast.Condition(
                Ast.TypeIs(toCheck, typeof(MissingParameter)),
                AstUtils.Constant(null),
                toCheck
            );
        }

        private static DynamicMetaObject/*!*/ MakeRuleForNoMatch(DynamicMetaObjectBinder/*!*/ operation, PythonOperationKind op, DynamicMetaObject errorSuggestion, params DynamicMetaObject/*!*/[]/*!*/ types) {
            // we get the error message w/ {0}, {1} so that TypeError formats it correctly
            return errorSuggestion ?? TypeError(
                   operation,
                   MakeBinaryOpErrorMessage(op, "{0}", "{1}"),
                   types);
        }

        internal static string/*!*/ MakeUnaryOpErrorMessage(string op, string/*!*/ xType) {
            if (op == "__invert__") {
                return string.Format("bad operand type for unary ~: '{0}'", xType);
            } else if (op == "__abs__") {
                return string.Format("bad operand type for abs(): '{0}'", xType);
            } else if (op == "__pos__") {
                return string.Format("bad operand type for unary +: '{0}'", xType);
            } else if (op == "__neg__") {
                return string.Format("bad operand type for unary -: '{0}'", xType);
            }

            // unreachable
            throw new InvalidOperationException();
        }


        internal static string/*!*/ MakeBinaryOpErrorMessage(PythonOperationKind op, string/*!*/ xType, string/*!*/ yType) {
            return string.Format("unsupported operand type(s) for {2}: '{0}' and '{1}'",
                                xType, yType, GetOperatorDisplay(op));
        }

        internal static string/*!*/ GetOperatorDisplay(PythonOperationKind op) {
            switch (op) {
                case PythonOperationKind.Add: return "+";
                case PythonOperationKind.Subtract: return "-";
                case PythonOperationKind.Power: return "**";
                case PythonOperationKind.Multiply: return "*";
                case PythonOperationKind.MatMult: return "@";
                case PythonOperationKind.FloorDivide: return "//";
                case PythonOperationKind.TrueDivide: return "/";
                case PythonOperationKind.Mod: return "%";
                case PythonOperationKind.LeftShift: return "<<";
                case PythonOperationKind.RightShift: return ">>";
                case PythonOperationKind.BitwiseAnd: return "&";
                case PythonOperationKind.BitwiseOr: return "|";
                case PythonOperationKind.ExclusiveOr: return "^";
                case PythonOperationKind.LessThan: return "<";
                case PythonOperationKind.GreaterThan: return ">";
                case PythonOperationKind.LessThanOrEqual: return "<=";
                case PythonOperationKind.GreaterThanOrEqual: return ">=";
                case PythonOperationKind.Equal: return "==";
                case PythonOperationKind.NotEqual: return "!=";
                case PythonOperationKind.InPlaceAdd: return "+=";
                case PythonOperationKind.InPlaceSubtract: return "-=";
                case PythonOperationKind.InPlacePower: return "**=";
                case PythonOperationKind.InPlaceMultiply: return "*=";
                case PythonOperationKind.InPlaceMatMult: return "@=";
                case PythonOperationKind.InPlaceFloorDivide: return "//=";
                case PythonOperationKind.InPlaceTrueDivide: return "/=";
                case PythonOperationKind.InPlaceMod: return "%=";
                case PythonOperationKind.InPlaceLeftShift: return "<<=";
                case PythonOperationKind.InPlaceRightShift: return ">>=";
                case PythonOperationKind.InPlaceBitwiseAnd: return "&=";
                case PythonOperationKind.InPlaceBitwiseOr: return "|=";
                case PythonOperationKind.InPlaceExclusiveOr: return "^=";
                case PythonOperationKind.ReverseAdd: return "+";
                case PythonOperationKind.ReverseSubtract: return "-";
                case PythonOperationKind.ReversePower: return "**";
                case PythonOperationKind.ReverseMultiply: return "*";
                case PythonOperationKind.ReverseMatMult: return "@";
                case PythonOperationKind.ReverseFloorDivide: return "//";
                case PythonOperationKind.ReverseTrueDivide: return "/";
                case PythonOperationKind.ReverseMod: return "%";
                case PythonOperationKind.ReverseLeftShift: return "<<";
                case PythonOperationKind.ReverseRightShift: return ">>";
                case PythonOperationKind.ReverseBitwiseAnd: return "&";
                case PythonOperationKind.ReverseBitwiseOr: return "|";
                case PythonOperationKind.ReverseExclusiveOr: return "^";
                default: return op.ToString();
            }
        }

        private static DynamicMetaObject/*!*/ MakeBinaryThrow(DynamicMetaObjectBinder/*!*/ action, PythonOperationKind op, DynamicMetaObject/*!*/[]/*!*/ args) {
            if (action is IPythonSite) {
                // produce the custom Python error message
                return new DynamicMetaObject(
                    action.Throw(
                        Ast.Call(
                            typeof(PythonOps).GetMethod(nameof(PythonOps.TypeErrorForBinaryOp)),
                            AstUtils.Constant(GetOperatorDisplay(op)),
                            AstUtils.Convert(args[0].Expression, typeof(object)),
                            AstUtils.Convert(args[1].Expression, typeof(object))
                        ),
                        typeof(object)
                    ),
                    BindingRestrictions.Combine(args)
                );
            }

            // let the site produce its own error
            return GenericFallback(action, args);
        }

        #endregion

        /// <summary>
        /// Produces an error message for the provided message and type names.  The error message should contain
        /// string formatting characters ({0}, {1}, etc...) for each of the type names.
        /// </summary>
        public static DynamicMetaObject/*!*/ TypeError(DynamicMetaObjectBinder/*!*/ action, string message, params DynamicMetaObject[] types) {
            if (action is IPythonSite) {
                message = string.Format(message, ArrayUtils.ConvertAll(types, x => MetaPythonObject.GetPythonTypeName(x)));

                Expression error = action.Throw(
                    Ast.Call(
                        typeof(PythonOps).GetMethod(nameof(PythonOps.SimpleTypeError)),   
                        Ast.Constant(message)
                    ),
                    typeof(object)
                );

                return new DynamicMetaObject(
                    error,
                    BindingRestrictions.Combine(types)
                );
            }

            return GenericFallback(action, types);
        }

        private static DynamicMetaObject GenericFallback(DynamicMetaObjectBinder action, DynamicMetaObject[] types) {
            if (action is GetIndexBinder) {
                return ((GetIndexBinder)action).FallbackGetIndex(types[0], ArrayUtils.RemoveFirst(types));
            } else if (action is SetIndexBinder) {
                return ((SetIndexBinder)action).FallbackSetIndex(types[0], ArrayUtils.RemoveLast(ArrayUtils.RemoveFirst(types)), types[types.Length - 1]);
            } else if (action is DeleteIndexBinder) {
                return ((DeleteIndexBinder)action).FallbackDeleteIndex(types[0], ArrayUtils.RemoveFirst(types));
            } else if (action is UnaryOperationBinder) {
                return ((UnaryOperationBinder)action).FallbackUnaryOperation(types[0]);
            } else if (action is BinaryOperationBinder) {
                return ((BinaryOperationBinder)action).FallbackBinaryOperation(types[0], types[1]);
            } else {
                throw new NotImplementedException();
            }
        }
    }
}
