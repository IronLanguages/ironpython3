// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;
using System.Numerics;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    internal partial class MetaUserObject : MetaPythonObject, IPythonInvokable, IPythonConvertible, IPythonOperable {
        private readonly DynamicMetaObject _baseMetaObject;            // if we're a subtype of MetaObject this is the base class MO

        public MetaUserObject(Expression/*!*/ expression, BindingRestrictions/*!*/ restrictions, DynamicMetaObject baseMetaObject, IPythonObject value)
            : base(expression, restrictions, value) {
            _baseMetaObject = baseMetaObject;
        }

        #region IPythonInvokable Members

        public DynamicMetaObject/*!*/ Invoke(PythonInvokeBinder/*!*/ pythonInvoke, Expression/*!*/ codeContext, DynamicMetaObject/*!*/ target, DynamicMetaObject/*!*/[]/*!*/ args) {
            return InvokeWorker(pythonInvoke, codeContext, args);
        }

        #endregion

        #region MetaObject Overrides

        public override DynamicMetaObject/*!*/ BindInvokeMember(InvokeMemberBinder/*!*/ action, DynamicMetaObject/*!*/[]/*!*/ args) {
            return new InvokeBinderHelper(this, action, args, PythonContext.GetCodeContextMO(action)).Bind(PythonContext.GetPythonContext(action).SharedContext, action.Name);
        }

        public override DynamicMetaObject BindConvert(ConvertBinder/*!*/ conversion) {
            return ConvertWorker(conversion, conversion.Type, conversion.Type, conversion.Explicit ? ConversionResultKind.ExplicitCast : ConversionResultKind.ImplicitCast);
        }

        public DynamicMetaObject BindConvert(PythonConversionBinder binder) {
            return ConvertWorker(binder, binder.Type, binder.ReturnType, binder.ResultKind);
        }

        public DynamicMetaObject ConvertWorker(DynamicMetaObjectBinder binder, Type type, Type retType, ConversionResultKind kind) {
            PerfTrack.NoteEvent(PerfTrack.Categories.Binding, "Conversion " + type.FullName);
            PerfTrack.NoteEvent(PerfTrack.Categories.BindingTarget, "Conversion");
            ValidationInfo typeTest = BindingHelpers.GetValidationInfo(this, Value.PythonType);

            return BindingHelpers.AddDynamicTestAndDefer(
                binder,
                TryPythonConversion(binder, type) ?? FallbackConvert(binder),
                new DynamicMetaObject[] { this },
                typeTest,
                retType
            );
        }

        public override DynamicMetaObject/*!*/ BindBinaryOperation(BinaryOperationBinder/*!*/ binder, DynamicMetaObject/*!*/ arg) {
            return PythonProtocol.Operation(binder, this, arg, null);
        }

        public override DynamicMetaObject/*!*/ BindUnaryOperation(UnaryOperationBinder/*!*/ binder) {
            return PythonProtocol.Operation(binder, this, null);
        }

        public override DynamicMetaObject/*!*/ BindGetIndex(GetIndexBinder/*!*/ binder, DynamicMetaObject/*!*/[]/*!*/ indexes) {
            return PythonProtocol.Index(binder, PythonIndexType.GetItem, ArrayUtils.Insert(this, indexes));
        }

        public override DynamicMetaObject/*!*/ BindSetIndex(SetIndexBinder/*!*/ binder, DynamicMetaObject/*!*/[]/*!*/ indexes, DynamicMetaObject/*!*/ value) {
            return PythonProtocol.Index(binder, PythonIndexType.SetItem, ArrayUtils.Insert(this, ArrayUtils.Append(indexes, value)));
        }

        public override DynamicMetaObject/*!*/ BindDeleteIndex(DeleteIndexBinder/*!*/ binder, DynamicMetaObject/*!*/[]/*!*/ indexes) {
            return PythonProtocol.Index(binder, PythonIndexType.DeleteItem, ArrayUtils.Insert(this, indexes));
        }        

        public override DynamicMetaObject/*!*/ BindInvoke(InvokeBinder/*!*/ action, DynamicMetaObject/*!*/[]/*!*/ args) {
            Expression context = Ast.Call(
                typeof(PythonOps).GetMethod(nameof(PythonOps.GetPythonTypeContext)),
                Ast.Property(
                    AstUtils.Convert(Expression, typeof(IPythonObject)),
                    "PythonType"
                )
            );

            return InvokeWorker(
                action, 
                context, 
                args
            );
        }

        public override System.Collections.Generic.IEnumerable<string> GetDynamicMemberNames() {
            foreach (object o in Value.PythonType.GetMemberNames(Value.PythonType.PythonContext.SharedContext, Value)) {
                if (o is string) {
                    yield return (string)o;
                }
            }
        }

        #endregion

        #region Invoke Implementation

        private DynamicMetaObject/*!*/ InvokeWorker(DynamicMetaObjectBinder/*!*/ action, Expression/*!*/ codeContext, DynamicMetaObject/*!*/[] args) {
            ValidationInfo typeTest = BindingHelpers.GetValidationInfo(this, Value.PythonType);

            if (Value is PythonType) {
                PythonContext context = PythonContext.GetPythonContext(action);
                // optimization for meta classes.  Don't dispatch to type.__call__ if it's inherited,
                // instead produce a normal type call rule.
                PythonTypeSlot callSlot, typeCallSlot;
                if (Value.PythonType.TryResolveSlot(context.SharedContext, "__call__", out callSlot) &&
                    TypeCache.PythonType.TryResolveSlot(context.SharedContext, "__call__", out typeCallSlot) &&
                    callSlot == typeCallSlot) {
                    
                    return InvokeFallback(action, codeContext, args);
                }
            }

            return BindingHelpers.AddDynamicTestAndDefer(
                action,
                PythonProtocol.Call(action, this, args) ?? InvokeFallback(action, codeContext, args),
                args,
                typeTest
            );
        }

        private DynamicMetaObject InvokeFallback(DynamicMetaObjectBinder action, Expression codeContext, DynamicMetaObject/*!*/[] args) {
            if (action is InvokeBinder ib) {
                if (_baseMetaObject != null) {
                    return _baseMetaObject.BindInvoke(ib, args);
                }

                return ib.FallbackInvoke(Restrict(this.GetLimitType()), args);
            }

            if (action is PythonInvokeBinder pib) {
                if (_baseMetaObject is IPythonInvokable ipi) {
                    return ipi.Invoke(pib, codeContext, this, args);
                }

                if (_baseMetaObject != null) {
                    return pib.InvokeForeignObject(this, args);
                }

                return pib.Fallback(codeContext, this, args);
            }

            // unreachable, we always have one of these binders
            throw new InvalidOperationException();
        }

        #endregion

        #region Conversions

        private DynamicMetaObject TryPythonConversion(DynamicMetaObjectBinder conversion, Type type) {
            if (!type.IsEnum) {
                switch (type.GetTypeCode()) {
                    case TypeCode.Object:
                        if (type == typeof(Complex)) {
                            return MakeConvertRuleForCall(conversion, type, this, "__complex__", "ConvertToComplex",
                                (() => MakeConvertRuleForCall(conversion, type, this, "__float__", "ConvertToFloat",
                                    (() => FallbackConvert(conversion)),
                                    (x) => Ast.Call(null, typeof(PythonOps).GetMethod(nameof(PythonOps.ConvertFloatToComplex)), x))),
                                (x) => x);
                        } else if (type == typeof(BigInteger)) {
                            if (!typeof(Extensible<BigInteger>).IsAssignableFrom(LimitType)) {
                                return MakeConvertRuleForCall(conversion, type, this, "__int__", "ConvertToBigInt",
                                    () => FallbackConvert(conversion),
                                    (x) => Ast.Call(null, typeof(PythonOps).GetMethod(nameof(PythonOps.ConvertIntToBigInt)), x));  // GH #52
                            }
                        } else if (type == typeof(IEnumerable)) {
                            return PythonConversionBinder.ConvertToIEnumerable(conversion, Restrict(Value.GetType()));
                        } else if (type == typeof(IEnumerator)){
                            return PythonConversionBinder.ConvertToIEnumerator(conversion, Restrict(Value.GetType()));
                        } else if (type.IsSubclassOf(typeof(Delegate))) {
                            return MakeDelegateTarget(conversion, type, Restrict(Value.GetType()));
                        }
                        break;
                    case TypeCode.Int32:
                        return MakeConvertRuleForCall(conversion, type, this, "__int__", "ConvertToInt");
                    case TypeCode.Double:
                        return MakeConvertRuleForCall(conversion, type, this, "__float__", "ConvertToFloat");
                    case TypeCode.Boolean:
                        return PythonProtocol.ConvertToBool(
                            conversion,
                            this
                        );
                    case TypeCode.String:
                        if (!typeof(Extensible<string>).IsAssignableFrom(LimitType)) {
                            return MakeConvertRuleForCall(conversion, type, this, "__str__", "ConvertToString");
                        }
                        break;
                }
            }

            return null;
        }

        private DynamicMetaObject/*!*/ MakeConvertRuleForCall(DynamicMetaObjectBinder/*!*/ convertToAction, Type toType, DynamicMetaObject/*!*/ self, string name, string returner, Func<DynamicMetaObject> fallback, Func<Expression, Expression> resultConverter) {
            PythonType pt = ((IPythonObject)self.Value).PythonType;
            PythonTypeSlot pts;
            CodeContext context = PythonContext.GetPythonContext(convertToAction).SharedContext;
            ValidationInfo valInfo = BindingHelpers.GetValidationInfo(this, pt);

            if (pt.TryResolveSlot(context, name, out pts) && !IsBuiltinConversion(context, pts, name, pt)) {
                ParameterExpression tmp = Ast.Variable(typeof(object), "func");

                Expression callExpr = resultConverter(
                    Ast.Call(
                        PythonOps.GetConversionHelper(returner, GetResultKind(convertToAction)),
                        Ast.Dynamic(
                            PythonContext.GetPythonContext(convertToAction).InvokeNone,
                            typeof(object),
                            PythonContext.GetCodeContext(convertToAction),
                            tmp
                        )
                    )
                );

                if (typeof(Extensible<>).MakeGenericType(toType).IsAssignableFrom(self.GetLimitType())) {
                    // if we're doing a conversion to the underlying type and we're an 
                    // Extensible<T> of that type:

                    // if an extensible type returns it's self in a conversion, then we need 
                    // to actually return the underlying value.  If an extensible just keeps 
                    // returning more instances  of it's self a stack overflow occurs - both 
                    // behaviors match CPython.
                    callExpr = AstUtils.Convert(AddExtensibleSelfCheck(convertToAction, toType, self, callExpr), typeof(object));
                }

                return BindingHelpers.AddDynamicTestAndDefer(
                    convertToAction,
                    new DynamicMetaObject(
                        Ast.Condition(
                            MakeTryGetTypeMember(
                                PythonContext.GetPythonContext(convertToAction),
                                pts,
                                self.Expression,
                                tmp
                            ),
                            callExpr,
                            AstUtils.Convert(
                                ConversionFallback(convertToAction),
                                typeof(object)
                            )
                        ),
                        self.Restrict(self.GetRuntimeType()).Restrictions
                    ),
                    new DynamicMetaObject[] { this },
                    valInfo,
                    tmp
                );
            }

            return fallback();
        }

        private DynamicMetaObject/*!*/ MakeConvertRuleForCall(DynamicMetaObjectBinder/*!*/ convertToAction, Type toType, DynamicMetaObject/*!*/ self, string name, string returner) {
            return MakeConvertRuleForCall(convertToAction, toType, self, name, returner, () => FallbackConvert(convertToAction), (x) => x);
        }

        private static Expression/*!*/ AddExtensibleSelfCheck(DynamicMetaObjectBinder/*!*/ convertToAction, Type toType, DynamicMetaObject/*!*/ self, Expression/*!*/ callExpr) {
            ParameterExpression tmp = Ast.Variable(callExpr.Type, "tmp");
            ConversionResultKind resKind = GetResultKind(convertToAction);
            Type retType = (resKind == ConversionResultKind.ExplicitTry || resKind == ConversionResultKind.ImplicitTry) ? typeof(object) : toType;

            callExpr = Ast.Block(
                new ParameterExpression[] { tmp },
                Ast.Block(
                    Ast.Assign(tmp, callExpr),
                    Ast.Condition(
                        Ast.Equal(tmp, self.Expression),
                        AstUtils.Convert(
                            Ast.Property(
                                AstUtils.Convert(self.Expression, self.GetLimitType()),
                                self.GetLimitType().GetProperty("Value")
                            ),
                            retType
                        ),
                        Ast.Dynamic(
                            new PythonConversionBinder(
                                PythonContext.GetPythonContext(convertToAction),
                                toType,
                                GetResultKind(convertToAction)
                            ),
                            retType,
                            tmp
                        )
                    )
                )
            );
            return callExpr;
        }

        private static ConversionResultKind GetResultKind(DynamicMetaObjectBinder convertToAction) {
            if (convertToAction is PythonConversionBinder cb) {
                return cb.ResultKind;
            }

            if (((ConvertBinder)convertToAction).Explicit) {
                return ConversionResultKind.ExplicitCast;
            } else {
                return ConversionResultKind.ImplicitCast;
            }
        }

        private Expression ConversionFallback(DynamicMetaObjectBinder/*!*/ convertToAction) {
            if (convertToAction is PythonConversionBinder cb) {
                return GetConversionFailedReturnValue(cb, this).Expression;
            }

            return convertToAction.GetUpdateExpression(typeof(object));
        }

        private static bool IsBuiltinConversion(CodeContext/*!*/ context, PythonTypeSlot/*!*/ pts, string name, PythonType/*!*/ selfType) {
            Type baseType = selfType.UnderlyingSystemType.BaseType;
            Type tmpType = baseType;
            do {
                if (tmpType.IsGenericType && tmpType.GetGenericTypeDefinition() == typeof(Extensible<>)) {
                    baseType = tmpType.GetGenericArguments()[0];
                    break;
                }
                tmpType = tmpType.BaseType;
            } while (tmpType != null);

            PythonType ptBase = DynamicHelpers.GetPythonTypeFromType(baseType);
            PythonTypeSlot baseSlot;
            if (ptBase.TryResolveSlot(context, name, out baseSlot) && pts == baseSlot) {
                return true;
            }

            return false;
        }

        /// <summary>
        ///  Various helpers related to calling Python __*__ conversion methods 
        /// </summary>
        private static DynamicMetaObject/*!*/ GetConversionFailedReturnValue(PythonConversionBinder/*!*/ convertToAction, DynamicMetaObject/*!*/ self) {
            switch (convertToAction.ResultKind) {
                case ConversionResultKind.ImplicitTry:
                case ConversionResultKind.ExplicitTry:
                    return new DynamicMetaObject(DefaultBinder.GetTryConvertReturnValue(convertToAction.Type), BindingRestrictions.Empty);
                case ConversionResultKind.ExplicitCast:
                case ConversionResultKind.ImplicitCast:
                    DefaultBinder db = PythonContext.GetPythonContext(convertToAction).Binder;
                    return DefaultBinder.MakeError(
                        db.MakeConversionError(
                            convertToAction.Type,
                            self.Expression
                        ), 
                        typeof(object)
                    );
                default:
                    throw new InvalidOperationException(convertToAction.ResultKind.ToString());
            }
        }
        
        /// <summary>
        /// Helper for falling back - if we have a base object fallback to it first (which can
        /// then fallback to the calling site), otherwise fallback to the calling site.
        /// </summary>
        private DynamicMetaObject/*!*/ Fallback(DynamicMetaObjectBinder/*!*/ action, DynamicMetaObject codeContext) {
            if (_baseMetaObject != null) {
                if (_baseMetaObject is IPythonGetable ipyget) {
                    if (action is PythonGetMemberBinder gmb) {
                        return ipyget.GetMember(gmb, codeContext);
                    }
                }

                if (action is GetMemberBinder gma) {
                    return _baseMetaObject.BindGetMember(gma);
                }

                return _baseMetaObject.BindGetMember(
                    PythonContext.GetPythonContext(action).CompatGetMember(
                        GetGetMemberName(action),
                        false
                    )
                );
            }

            return GetMemberFallback(this, action, codeContext);
        }

        /// <summary>
        /// Helper for falling back - if we have a base object fallback to it first (which can
        /// then fallback to the calling site), otherwise fallback to the calling site.
        /// </summary>
        private DynamicMetaObject/*!*/ Fallback(SetMemberBinder/*!*/ action, DynamicMetaObject/*!*/ value) {
            if (_baseMetaObject != null) {
                return _baseMetaObject.BindSetMember(action, value);
            }

            return action.FallbackSetMember(this, value);
        }

        #endregion

        public new IPythonObject Value {
            get {
                return (IPythonObject)base.Value;
            }
        }

        #region IPythonOperable Members

        DynamicMetaObject IPythonOperable.BindOperation(PythonOperationBinder action, DynamicMetaObject[] args) {
            if (action.Operation == PythonOperationKind.IsCallable) {
                DynamicMetaObject self = Restrict(Value.GetType());

                return new DynamicMetaObject(
                    Ast.Call(
                        typeof(PythonOps).GetMethod(nameof(PythonOps.UserObjectIsCallable)),
                        AstUtils.Constant(PythonContext.GetPythonContext(action).SharedContext),
                        self.Expression
                    ),
                    self.Restrictions
                );
            }

            return null;
        }

        #endregion
    }
}
