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
using System.Linq.Expressions;
#else
using Microsoft.Scripting.Ast;
#endif

using System;
using System.Dynamic;
using System.Reflection;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Actions.Calls;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    class MetaBuiltinMethodDescriptor : MetaPythonObject, IPythonInvokable, IPythonOperable {
        public MetaBuiltinMethodDescriptor(Expression/*!*/ expression, BindingRestrictions/*!*/ restrictions, BuiltinMethodDescriptor/*!*/ value)
            : base(expression, BindingRestrictions.Empty, value) {
            Assert.NotNull(value);
        }

        #region IPythonInvokable Members

        public DynamicMetaObject/*!*/ Invoke(PythonInvokeBinder/*!*/ pythonInvoke, Expression/*!*/ codeContext, DynamicMetaObject/*!*/ target, DynamicMetaObject/*!*/[]/*!*/ args) {
            return InvokeWorker(pythonInvoke, codeContext, args);
        }

        #endregion

        #region MetaObject Overrides

        public override DynamicMetaObject/*!*/ BindInvokeMember(InvokeMemberBinder/*!*/ action, DynamicMetaObject/*!*/[]/*!*/ args) {
            return BindingHelpers.GenericInvokeMember(action, null, this, args);
        }

        public override DynamicMetaObject/*!*/ BindInvoke(InvokeBinder/*!*/ call, params DynamicMetaObject/*!*/[]/*!*/ args) {
            // TODO: Context should come from BuiltinFunction
            return InvokeWorker(call, PythonContext.GetCodeContext(call), args);
        }

        #endregion

        #region Invoke Implementation

        private DynamicMetaObject/*!*/ InvokeWorker(DynamicMetaObjectBinder/*!*/ call, Expression/*!*/ codeContext, DynamicMetaObject/*!*/[] args) {
            PerfTrack.NoteEvent(PerfTrack.Categories.Binding, "BuiltinMethodDesc Invoke " + Value.DeclaringType + "." + Value.__name__ + " w/ " + args.Length + " args");
            PerfTrack.NoteEvent(PerfTrack.Categories.BindingTarget, "BuiltinMethodDesc Invoke");

            CallSignature signature = BindingHelpers.GetCallSignature(call);
            BindingRestrictions selfRestrict = BindingRestrictions.GetInstanceRestriction(Expression, Value).Merge(Restrictions);

            selfRestrict = selfRestrict.Merge(
                BindingRestrictions.GetExpressionRestriction(
                    MakeFunctionTest(
                        Ast.Call(
                            typeof(PythonOps).GetMethod("GetBuiltinMethodDescriptorTemplate"),
                            Ast.Convert(Expression, typeof(BuiltinMethodDescriptor))
                        )
                    )
                )
            );

            return Value.Template.MakeBuiltinFunctionCall(
                call,
                codeContext,
                this,
                args,
                false,  // no self
                selfRestrict,
                (newArgs) => {
                    BindingTarget target;
                    PythonContext state = PythonContext.GetPythonContext(call);

                    DynamicMetaObject res = state.Binder.CallMethod(
                        new PythonOverloadResolver(
                            state.Binder,
                            newArgs,
                            signature,
                            codeContext
                        ),
                        Value.Template.Targets,
                        selfRestrict,
                        Value.Template.Name,
                        NarrowingLevel.None,
                        Value.Template.IsBinaryOperator ? PythonNarrowing.BinaryOperator : NarrowingLevel.All,
                        out target
                    );

                    return BindingHelpers.CheckLightThrow(call, res, target);
                });            
        }

        internal Expression MakeFunctionTest(Expression functionTarget) {
            return Ast.Equal(
                functionTarget,
                AstUtils.Constant(Value.Template)
            );
        }

        #endregion

        #region Helpers

        public new BuiltinMethodDescriptor/*!*/ Value {
            get {
                return (BuiltinMethodDescriptor)base.Value;
            }
        }

        #endregion

        #region IPythonOperable Members

        DynamicMetaObject IPythonOperable.BindOperation(PythonOperationBinder action, DynamicMetaObject[] args) {
            switch (action.Operation) {
                case PythonOperationKind.CallSignatures:
                    return PythonProtocol.MakeCallSignatureOperation(this, Value.Template.Targets);
            }

            return null;
        }

        #endregion
    }
}
