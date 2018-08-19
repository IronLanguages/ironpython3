// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;

using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Dynamic;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Actions.Calls;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    class MetaBuiltinFunction : MetaPythonObject, IPythonInvokable, IPythonOperable, IPythonConvertible {
        public MetaBuiltinFunction(Expression/*!*/ expression, BindingRestrictions/*!*/ restrictions, BuiltinFunction/*!*/ value)
            : base(expression, BindingRestrictions.Empty, value) {
            Assert.NotNull(value);
        }

        #region MetaObject Overrides

        public override DynamicMetaObject/*!*/ BindInvoke(InvokeBinder/*!*/ call, params DynamicMetaObject/*!*/[]/*!*/ args) {
            // TODO: Context should come from BuiltinFunction
            return InvokeWorker(call, PythonContext.GetCodeContext(call), args);
        }

        public override DynamicMetaObject BindConvert(ConvertBinder/*!*/ conversion) {
            return ConvertWorker(conversion, conversion.Type, conversion.Explicit ? ConversionResultKind.ExplicitCast : ConversionResultKind.ImplicitCast);
        }

        public DynamicMetaObject BindConvert(PythonConversionBinder binder) {
            return ConvertWorker(binder, binder.Type, binder.ResultKind);
        }

        public DynamicMetaObject ConvertWorker(DynamicMetaObjectBinder binder, Type toType, ConversionResultKind kind) {
            PerfTrack.NoteEvent(PerfTrack.Categories.Binding, "BuiltinFunc Convert " + toType);
            PerfTrack.NoteEvent(PerfTrack.Categories.BindingTarget, "BuiltinFunc Convert");

            if (toType.IsSubclassOf(typeof(Delegate))) {
                return MakeDelegateTarget(binder, toType, Restrict(LimitType));
            }

            return FallbackConvert(binder);
        }

        DynamicMetaObject IPythonOperable.BindOperation(PythonOperationBinder action, DynamicMetaObject[] args) {
            PerfTrack.NoteEvent(PerfTrack.Categories.Binding, "BuiltinFunc Operation " + action.Operation);
            PerfTrack.NoteEvent(PerfTrack.Categories.BindingTarget, "BuiltinFunc Operation");
            switch (action.Operation) {
                case PythonOperationKind.CallSignatures:
                    return PythonProtocol.MakeCallSignatureOperation(this, Value.Targets);
            }

            return null;
        }

        #endregion

        #region IPythonInvokable Members

        public DynamicMetaObject/*!*/ Invoke(PythonInvokeBinder/*!*/ pythonInvoke, Expression/*!*/ codeContext, DynamicMetaObject/*!*/ target, DynamicMetaObject/*!*/[]/*!*/ args) {
            return InvokeWorker(pythonInvoke, codeContext, args);
        }

        #endregion

        #region Invoke Implementation

        private DynamicMetaObject/*!*/ InvokeWorker(DynamicMetaObjectBinder/*!*/ call, Expression/*!*/ codeContext, DynamicMetaObject/*!*/[]/*!*/ args) {
            PerfTrack.NoteEvent(PerfTrack.Categories.Binding, "BuiltinFunc Invoke " + Value.DeclaringType.FullName + "." + Value.Name + " with " + args.Length + " args " + Value.IsUnbound);
            PerfTrack.NoteEvent(PerfTrack.Categories.BindingTarget, "BuiltinFunction " + Value.Targets.Count + ", " + Value.Targets[0].GetParameters().Length);
            PerfTrack.NoteEvent(PerfTrack.Categories.BindingSlow, "BuiltinFunction " + BindingHelpers.GetCallSignature(call));

            if (this.NeedsDeferral()) {
                return call.Defer(ArrayUtils.Insert(this, args));
            }

            for (int i = 0; i < args.Length; i++) {
                if (args[i].NeedsDeferral()) {
                    return call.Defer(ArrayUtils.Insert(this, args));
                }
            }

            if (Value.IsUnbound) {
                return MakeSelflessCall(call, codeContext, args);
            } else {
                return MakeSelfCall(call, codeContext, args);
            }
        }

        private DynamicMetaObject/*!*/ MakeSelflessCall(DynamicMetaObjectBinder/*!*/ call, Expression/*!*/ codeContext, DynamicMetaObject/*!*/[]/*!*/ args) {
            // just check if it's the same built-in function.  Because built-in functions are
            // immutable the identity check will suffice.  Because built-in functions are uncollectible
            // anyway we don't use the typical InstanceRestriction.
            BindingRestrictions selfRestrict = BindingRestrictions.GetExpressionRestriction(Ast.Equal(Expression, AstUtils.Constant(Value))).Merge(Restrictions);

            return Value.MakeBuiltinFunctionCall(
                call,
                codeContext,
                this,
                args,
                false,  // no self
                selfRestrict,
                (newArgs) => {
                    BindingTarget target;
                    var binder = PythonContext.GetPythonContext(call).Binder;

                    DynamicMetaObject res = binder.CallMethod(
                        new PythonOverloadResolver(
                            binder,
                            newArgs,
                            BindingHelpers.GetCallSignature(call),
                            codeContext
                        ), 
                        Value.Targets, 
                        selfRestrict, 
                        Value.Name,
                        PythonNarrowing.None,
                        Value.IsBinaryOperator ? PythonNarrowing.BinaryOperator : NarrowingLevel.All,
                        out target
                    );

                    return BindingHelpers.CheckLightThrow(call, res, target);
                }
            );
        }

        private DynamicMetaObject/*!*/ MakeSelfCall(DynamicMetaObjectBinder/*!*/ call, Expression/*!*/ codeContext, DynamicMetaObject/*!*/[]/*!*/ args) {
            BindingRestrictions selfRestrict = Restrictions.Merge(
                BindingRestrictionsHelpers.GetRuntimeTypeRestriction(
                    Expression,
                    LimitType
                )
            ).Merge(
                BindingRestrictions.GetExpressionRestriction(
                    Value.MakeBoundFunctionTest(
                        AstUtils.Convert(Expression, typeof(BuiltinFunction))
                    )
                )
            );

            Expression instance = Ast.Call(
                typeof(PythonOps).GetMethod(nameof(PythonOps.GetBuiltinFunctionSelf)),
                AstUtils.Convert(
                    Expression,
                    typeof(BuiltinFunction)
                )
            );

            DynamicMetaObject self = GetInstance(instance, CompilerHelpers.GetType(Value.BindingSelf));
                return Value.MakeBuiltinFunctionCall(
                call,
                codeContext,
                this,
                ArrayUtils.Insert(self, args),
                true,   // has self
                selfRestrict,
                (newArgs) => {
                    CallSignature signature = BindingHelpers.GetCallSignature(call);
                    PythonContext state = PythonContext.GetPythonContext(call);
                    BindingTarget target;
                    PythonOverloadResolver resolver;
                    if (Value.IsReversedOperator) {
                        resolver = new PythonOverloadResolver(
                            state.Binder,
                            newArgs,
                            GetReversedSignature(signature),
                            codeContext
                        );
                    } else {
                        resolver = new PythonOverloadResolver(
                            state.Binder,
                            self,
                            args,
                            signature,
                            codeContext
                        );
                    }

                    DynamicMetaObject res = state.Binder.CallMethod(
                        resolver,
                        Value.Targets,
                        self.Restrictions,
                        Value.Name,
                        NarrowingLevel.None,
                        Value.IsBinaryOperator ? PythonNarrowing.BinaryOperator : NarrowingLevel.All,
                        out target
                    );

                    return BindingHelpers.CheckLightThrow(call, res, target);
                }
            );
        }

        private DynamicMetaObject/*!*/ GetInstance(Expression/*!*/ instance, Type/*!*/ testType) {
            Assert.NotNull(instance, testType);
            object instanceValue = Value.BindingSelf;

            BindingRestrictions restrictions = BindingRestrictionsHelpers.GetRuntimeTypeRestriction(instance, testType);
            // cast the instance to the correct type
            if (CompilerHelpers.IsStrongBox(instanceValue)) {
                instance = ReadStrongBoxValue(instance);
                instanceValue = ((IStrongBox)instanceValue).Value;
            } else if (!testType.IsEnum()) {
                // We need to deal w/ wierd types like MarshalByRefObject.  
                // We could have an MBRO whos DeclaringType is completely different.  
                // Therefore we special case it here and cast to the declaring type

                Type selfType = CompilerHelpers.GetType(Value.BindingSelf);
                selfType = CompilerHelpers.GetVisibleType(selfType);

                if (selfType == typeof(object) && Value.DeclaringType.IsInterface()) {
                    selfType = Value.DeclaringType;

                    Type genericTypeDefinition = null;
                    // the behavior is different on Mono, it sets FullName for the DeclaringType
                    if (Value.DeclaringType.IsGenericType() &&
                        (ClrModule.IsMono || Value.DeclaringType.FullName == null) &&
                        Value.DeclaringType.ContainsGenericParameters() &&
                        !Value.DeclaringType.IsGenericTypeDefinition()) {
                        // from MSDN: If the current type contains generic type parameters that have not been replaced by 
                        // specific types (that is, the ContainsGenericParameters property returns true), but the type 
                        // is not a generic type definition (that is, the IsGenericTypeDefinition property returns false), 
                        // this property returns Nothing. For example, consider the classes Base and Derived in the following code.

                        // if this type is completely generic (no type arguments specified) then we'll go ahead and get the
                        // generic type definition for the this parameter - that'll let us successfully type infer on it later.
                        var genericArgs = Value.DeclaringType.GetGenericArguments();
                        bool hasOnlyGenerics = genericArgs.Length > 0;
                        foreach (var genericParam in genericArgs) {
                            if (!genericParam.IsGenericParameter) {
                                hasOnlyGenerics = false;
                                break;
                            }
                        }
                        if (hasOnlyGenerics) {
                            genericTypeDefinition = Value.DeclaringType.GetGenericTypeDefinition();
                        }
                    } else if (Value.DeclaringType.IsGenericTypeDefinition()) {
                        genericTypeDefinition = Value.DeclaringType;
                    }

                    if (genericTypeDefinition != null) {
                        // we're a generic interface method on a non-public type.  
                        // We need to see if we can match any types implemented on 
                        // the concrete selfType.
                        var interfaces = CompilerHelpers.GetType(Value.BindingSelf).GetInterfaces();
                        foreach (var iface in interfaces) {
                            if (iface.IsGenericType() && iface.GetGenericTypeDefinition() == genericTypeDefinition) {
                                selfType = iface;
                                break;
                            }
                        }
                    }
                }

                if (Value.DeclaringType.IsInterface() && selfType.IsValueType()) {
                    // explicit interface implementation dispatch on a value type, don't
                    // unbox the value type before the dispatch.
                    instance = AstUtils.Convert(instance, Value.DeclaringType);
                } else if (selfType.IsValueType()) {
                    // We might be calling a a mutating method (like
                    // Rectangle.Intersect). If so, we want it to mutate
                    // the boxed value directly
                    instance = Ast.Unbox(instance, selfType);
                } else {
#if FEATURE_REMOTING
                    Type convType = selfType == typeof(MarshalByRefObject) ? CompilerHelpers.GetVisibleType(Value.DeclaringType) : selfType;
                    instance = AstUtils.Convert(instance, convType);
#else
                    instance = AstUtils.Convert(instance, selfType);
#endif
                }
            } else {
                // we don't want to cast the enum to its real type, it will unbox it 
                // and turn it into its underlying type.  We presumably want to call 
                // a method on the Enum class though - so we cast to Enum instead.
                instance = AstUtils.Convert(instance, typeof(Enum));
            }
            return new DynamicMetaObject(
                instance,
                restrictions,
                instanceValue
            );
        }

        private MemberExpression/*!*/ ReadStrongBoxValue(Expression instance) {
            return Ast.Field(
                AstUtils.Convert(instance, Value.BindingSelf.GetType()),
                Value.BindingSelf.GetType().GetField("Value")
            );
        }

        internal static CallSignature GetReversedSignature(CallSignature signature) {
            return new CallSignature(ArrayUtils.Append(signature.GetArgumentInfos(), new Argument(ArgumentType.Simple)));
        }

        #endregion

        #region Helpers

        public new BuiltinFunction/*!*/ Value {
            get {
                return (BuiltinFunction)base.Value;
            }
        }

        #endregion
    }
}
