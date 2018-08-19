// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Runtime.CompilerServices;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Operations;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    class MetaMethod : MetaPythonObject, IPythonInvokable, IPythonConvertible {
        public MetaMethod(Expression/*!*/ expression, BindingRestrictions/*!*/ restrictions, Method/*!*/ value)
            : base(expression, BindingRestrictions.Empty, value) {
            Assert.NotNull(value);
        }

        #region IPythonInvokable Members

        public DynamicMetaObject/*!*/ Invoke(PythonInvokeBinder/*!*/ pythonInvoke, Expression/*!*/ codeContext, DynamicMetaObject/*!*/ target, DynamicMetaObject/*!*/[]/*!*/ args) {
            return InvokeWorker(pythonInvoke, args);
        }

        #endregion

        #region MetaObject Overrides

        public override DynamicMetaObject/*!*/ BindInvokeMember(InvokeMemberBinder/*!*/ action, DynamicMetaObject/*!*/[]/*!*/ args) {
            return BindingHelpers.GenericInvokeMember(action, null, this, args);
        }

        public override DynamicMetaObject/*!*/ BindInvoke(InvokeBinder/*!*/ callAction, params DynamicMetaObject/*!*/[]/*!*/ args) {
            return InvokeWorker(callAction, args);
        }

        public override DynamicMetaObject BindConvert(ConvertBinder/*!*/ conversion) {
            return ConvertWorker(conversion, conversion.Type, conversion.Explicit ? ConversionResultKind.ExplicitCast : ConversionResultKind.ImplicitCast);
        }

        public DynamicMetaObject BindConvert(PythonConversionBinder binder) {
            return ConvertWorker(binder, binder.Type, binder.ResultKind);
        }

        public DynamicMetaObject ConvertWorker(DynamicMetaObjectBinder binder, Type toType, ConversionResultKind kind) {
            if (toType.IsSubclassOf(typeof(Delegate))) {
                return MakeDelegateTarget(binder, toType, Restrict(typeof(Method)));
            }

            return FallbackConvert(binder);
        }

        #endregion

        #region Invoke Implementation

        private DynamicMetaObject InvokeWorker(DynamicMetaObjectBinder/*!*/ callAction, DynamicMetaObject/*!*/[] args) {
            PerfTrack.NoteEvent(PerfTrack.Categories.Binding, "Method Invoke " + args.Length);
            PerfTrack.NoteEvent(PerfTrack.Categories.BindingTarget, "Method");

            CallSignature signature = BindingHelpers.GetCallSignature(callAction);
            DynamicMetaObject self = Restrict(typeof(Method));
            BindingRestrictions restrictions = self.Restrictions;

            DynamicMetaObject func = GetMetaFunction(self);
            DynamicMetaObject call;

            if (Value.im_self == null) {
                // restrict to null self (Method is immutable so this is an invariant test)
                restrictions = restrictions.Merge(
                    BindingRestrictions.GetExpressionRestriction(
                        Ast.Equal(
                            GetSelfExpression(self),
                            AstUtils.Constant(null)
                        )
                    )
                );

                if (args.Length == 0) {
                    // this is an error, we pass null which will throw the normal error
                    call = new DynamicMetaObject(
                        Ast.Call(
                            typeof(PythonOps).GetMethod(nameof(PythonOps.MethodCheckSelf)),
                            PythonContext.GetCodeContext(callAction),
                            self.Expression,
                            AstUtils.Constant(null)
                        ),
                        restrictions
                    );
                } else {
                    // this may or may not be an error
                    call = new DynamicMetaObject(
                        Ast.Block(
                            MakeCheckSelf(callAction, signature, args),
                            DynamicExpression.Dynamic(
                                PythonContext.GetPythonContext(callAction).Invoke(
                                    BindingHelpers.GetCallSignature(callAction)
                                ).GetLightExceptionBinder(callAction.SupportsLightThrow()),
                                typeof(object),
                                ArrayUtils.Insert(PythonContext.GetCodeContext(callAction), DynamicUtils.GetExpressions(ArrayUtils.Insert(func, args)))
                            )
                        ),
                        BindingRestrictions.Empty
                    );
                    /*call = func.Invoke(callAction, ArrayUtils.Insert(func, args));
                    call =  new MetaObject(
                        Ast.Comma(
                            Ast.Call(
                                typeof(PythonOps).GetMethod("MethodCheckSelf"),
                                self.Expression,
                                args[0].Expression
                            ),
                            call.Expression
                        ),
                        call.Restrictions                        
                    );*/
                }
            } else {
                // restrict to non-null self (Method is immutable so this is an invariant test)
                restrictions = restrictions.Merge(
                    BindingRestrictions.GetExpressionRestriction(
                        Ast.NotEqual(
                            GetSelfExpression(self),
                            AstUtils.Constant(null)
                        )
                    )
                );

                DynamicMetaObject im_self = GetMetaSelf(self);
                DynamicMetaObject[] newArgs = ArrayUtils.Insert(func, im_self, args);
                CallSignature newSig = new CallSignature(ArrayUtils.Insert(new Argument(ArgumentType.Simple), signature.GetArgumentInfos()));


                call = new DynamicMetaObject(
                    DynamicExpression.Dynamic(
                        PythonContext.GetPythonContext(callAction).Invoke(
                            newSig
                        ).GetLightExceptionBinder(callAction.SupportsLightThrow()),
                        typeof(object),
                        ArrayUtils.Insert(PythonContext.GetCodeContext(callAction), DynamicUtils.GetExpressions(newArgs))
                    ),
                    BindingRestrictions.Empty
                );

                /*
                call = func.Invoke(
                    new CallBinder(
                        PythonContext.GetBinderState(callAction),
                        newSig
                    ),
                    newArgs
                );*/
            }

            if (call.HasValue) {
                return new DynamicMetaObject(
                    call.Expression,
                    restrictions.Merge(call.Restrictions),
                    call.Value
                );
            } else {
                return new DynamicMetaObject(
                    call.Expression,
                    restrictions.Merge(call.Restrictions)
                );
            }
        }

        #endregion

        #region Helpers

        private DynamicMetaObject GetMetaSelf(DynamicMetaObject/*!*/ self) {
            DynamicMetaObject func;

            IDynamicMetaObjectProvider ido = Value.im_self as IDynamicMetaObjectProvider;
            if (ido != null) {
                func = ido.GetMetaObject(GetSelfExpression(self));
            } else if (Value.im_self == null) {
                func = new DynamicMetaObject(
                    GetSelfExpression(self),
                    BindingRestrictions.Empty);
            } else {
                func = new DynamicMetaObject(
                    GetSelfExpression(self),
                    BindingRestrictions.Empty,
                    Value.im_self
                );
            }

            return func;
        }
        
        private DynamicMetaObject/*!*/ GetMetaFunction(DynamicMetaObject/*!*/ self) {
            DynamicMetaObject func;
            IDynamicMetaObjectProvider ido = Value.im_func as IDynamicMetaObjectProvider;
            if (ido != null) {
                func = ido.GetMetaObject(GetFunctionExpression(self));
            } else {
                func = new DynamicMetaObject(
                    GetFunctionExpression(self),
                    BindingRestrictions.Empty
                );
            }
            return func;
        }

        private static MemberExpression GetFunctionExpression(DynamicMetaObject self) {
            return Ast.Property(
                self.Expression,
                typeof(Method).GetProperty("im_func")
            );
        }

        private static MemberExpression GetSelfExpression(DynamicMetaObject self) {
            return Ast.Property(
                self.Expression,
                typeof(Method).GetProperty("im_self")
            );
        }

        public new Method/*!*/ Value {
            get {
                return (Method)base.Value;
            }
        }

        private Expression/*!*/ MakeCheckSelf(DynamicMetaObjectBinder/*!*/ binder, CallSignature signature, DynamicMetaObject/*!*/[]/*!*/ args) {
            ArgumentType firstArgKind = signature.GetArgumentKind(0);

            Expression res;
            if (firstArgKind == ArgumentType.Simple || firstArgKind == ArgumentType.Instance) {
                res = CheckSelf(binder, AstUtils.Convert(Expression, typeof(Method)), args[0].Expression);
            } else if (firstArgKind != ArgumentType.List) {
                res = CheckSelf(binder, AstUtils.Convert(Expression, typeof(Method)), AstUtils.Constant(null));
            } else {
                // list, check arg[0] and then return original list.  If not a list,
                // or we have no items, then check against null & throw.
                res = CheckSelf(
                    binder,
                    AstUtils.Convert(Expression, typeof(Method)),
                    Ast.Condition(
                        Ast.AndAlso(
                            Ast.TypeIs(args[0].Expression, typeof(IList<object>)),
                            Ast.NotEqual(
                                Ast.Property(
                                    Ast.Convert(args[0].Expression, typeof(ICollection)),
                                    typeof(ICollection).GetProperty("Count")
                                ),
                                AstUtils.Constant(0)
                            )
                        ),
                        Ast.Call(
                            Ast.Convert(args[0].Expression, typeof(IList<object>)),
                            typeof(IList<object>).GetMethod("get_Item"),
                            AstUtils.Constant(0)
                        ),
                        AstUtils.Constant(null)
                    )
                );
            }

            return res;
        }

        private static Expression/*!*/ CheckSelf(DynamicMetaObjectBinder/*!*/ binder, Expression/*!*/ method, Expression/*!*/ inst) {
            return Ast.Call(
                typeof(PythonOps).GetMethod(nameof(PythonOps.MethodCheckSelf)),
                PythonContext.GetCodeContext(binder),
                method,
                AstUtils.Convert(inst, typeof(object))
            );
        }

        #endregion
    }
}
