// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.


using System;
using System.Dynamic;
using System.Linq.Expressions;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    internal class MetaMethod : MetaPythonObject, IPythonInvokable, IPythonConvertible {
        public MetaMethod(Expression/*!*/ expression, BindingRestrictions/*!*/ restrictions, Method/*!*/ value)
            : base(expression, BindingRestrictions.Empty, value) {
            Assert.NotNull(value);
        }

        #region IPythonInvokable Members

        public DynamicMetaObject/*!*/ Invoke(PythonInvokeBinder/*!*/ pythonInvoke, Expression/*!*/ codeContext, DynamicMetaObject/*!*/ target, DynamicMetaObject/*!*/[]/*!*/ args)
            => InvokeWorker(pythonInvoke, args);

        #endregion

        #region MetaObject Overrides

        public override DynamicMetaObject/*!*/ BindInvokeMember(InvokeMemberBinder/*!*/ action, DynamicMetaObject/*!*/[]/*!*/ args)
            => BindingHelpers.GenericInvokeMember(action, null, this, args);

        public override DynamicMetaObject/*!*/ BindInvoke(InvokeBinder/*!*/ callAction, params DynamicMetaObject/*!*/[]/*!*/ args)
            => InvokeWorker(callAction, args);

        public override DynamicMetaObject BindConvert(ConvertBinder/*!*/ conversion)
            => ConvertWorker(conversion, conversion.Type, conversion.Explicit ? ConversionResultKind.ExplicitCast : ConversionResultKind.ImplicitCast);

        public DynamicMetaObject BindConvert(PythonConversionBinder binder)
            => ConvertWorker(binder, binder.Type, binder.ResultKind);

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

            // restrict to non-null self (Method is immutable so this is an invariant test)
            restrictions = restrictions.Merge(
                BindingRestrictions.GetExpressionRestriction(
                    Ast.NotEqual(
                        GetSelfExpression(self),
                        AstUtils.Constant(null)
                    )
                )
            );

            DynamicMetaObject[] newArgs = ArrayUtils.Insert(GetMetaFunction(self), GetMetaSelf(self), args);
            var newSig = new CallSignature(ArrayUtils.Insert(new Argument(ArgumentType.Simple), signature.GetArgumentInfos()));

            var call = new DynamicMetaObject(
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

        private DynamicMetaObject GetMetaSelf(DynamicMetaObject /*!*/ self) {
            DynamicMetaObject func;

            if (Value.__self__ is IDynamicMetaObjectProvider ido) {
                func = ido.GetMetaObject(GetSelfExpression(self));
            } else {
                func = new DynamicMetaObject(
                    GetSelfExpression(self),
                    BindingRestrictions.Empty,
                    Value.__self__
                );
            }

            return func;
        }

        private DynamicMetaObject/*!*/ GetMetaFunction(DynamicMetaObject/*!*/ self) {
            DynamicMetaObject func;
            if (Value.__func__ is IDynamicMetaObjectProvider ido) {
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
                typeof(Method).GetProperty(nameof(Method.__func__))
            );
        }

        private static MemberExpression GetSelfExpression(DynamicMetaObject self) {
            return Ast.Property(
                self.Expression,
                typeof(Method).GetProperty(nameof(Method.__self__))
            );
        }

        public new Method/*!*/ Value => (Method)base.Value;

        #endregion
    }
}
