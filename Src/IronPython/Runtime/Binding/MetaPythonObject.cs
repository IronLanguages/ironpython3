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
#endif

using System;
using System.Diagnostics;
using System.Dynamic;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Ast;
using Microsoft.Scripting.Runtime;
using IronPython.Runtime.Types;
using IronPython.Runtime.Operations;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    partial class MetaPythonObject : DynamicMetaObject {
        public MetaPythonObject(Expression/*!*/ expression, BindingRestrictions/*!*/ restrictions)
            : base(expression, restrictions) {
        }

        public MetaPythonObject(Expression/*!*/ expression, BindingRestrictions/*!*/ restrictions, object value)
            : base(expression, restrictions, value) {
        }

        public DynamicMetaObject/*!*/ FallbackConvert(DynamicMetaObjectBinder/*!*/ binder) {
            PythonConversionBinder pyBinder = binder as PythonConversionBinder;
            if (pyBinder != null) {
                return pyBinder.FallbackConvert(binder.ReturnType, this, null);
            }

            return ((ConvertBinder)binder).FallbackConvert(this);
        }

        internal static MethodCallExpression MakeTryGetTypeMember(PythonContext/*!*/ PythonContext, PythonTypeSlot dts, Expression self, ParameterExpression tmp) {
            return MakeTryGetTypeMember(
                PythonContext,
                dts, 
                tmp,
                self,
                Ast.Property(
                    Ast.Convert(
                        self,
                        typeof(IPythonObject)),
                    PythonTypeInfo._IPythonObject.PythonType
                )
            );
        }

        internal static MethodCallExpression MakeTryGetTypeMember(PythonContext/*!*/ PythonContext, PythonTypeSlot dts, ParameterExpression tmp, Expression instance, Expression pythonType) {
            return Ast.Call(
                PythonTypeInfo._PythonOps.SlotTryGetBoundValue,
                AstUtils.Constant(PythonContext.SharedContext),
                AstUtils.Convert(Utils.WeakConstant(dts), typeof(PythonTypeSlot)),
                AstUtils.Convert(instance, typeof(object)),
                AstUtils.Convert(
                    pythonType,
                    typeof(PythonType)
                ),
                tmp
            );
        }

        public DynamicMetaObject Restrict(Type type) {
            return MetaObjectExtensions.Restrict(this, type);
        }

        public PythonType/*!*/ PythonType {
            get {
                return DynamicHelpers.GetPythonType(Value);
            }
        }

        public static PythonType/*!*/ GetPythonType(DynamicMetaObject/*!*/ value) {
            if (value.HasValue) {
                return DynamicHelpers.GetPythonType(value.Value);
            }

            return DynamicHelpers.GetPythonTypeFromType(value.GetLimitType());
        }

        /// <summary>
        /// Creates a target which creates a new dynamic method which contains a single
        /// dynamic site that invokes the callable object.
        /// 
        /// TODO: This should be specialized for each callable object
        /// </summary>
        protected static DynamicMetaObject/*!*/ MakeDelegateTarget(DynamicMetaObjectBinder/*!*/ action, Type/*!*/ toType, DynamicMetaObject/*!*/ arg) {
            Debug.Assert(arg != null);

            PythonContext state = PythonContext.GetPythonContext(action);
            CodeContext context;
            if (state != null) {
                context = state.SharedContext;
            } else {
                context = DefaultContext.Default;
            }
            
            return new DynamicMetaObject(
                Ast.Convert(
                    Ast.Call(
                        typeof(PythonOps).GetMethod("GetDelegate"),
                        AstUtils.Constant(context),
                        arg.Expression,
                        AstUtils.Constant(toType)
                    ),
                    toType
                ),
                arg.Restrictions
            );
        }

        protected static DynamicMetaObject GetMemberFallback(DynamicMetaObject self, DynamicMetaObjectBinder member, DynamicMetaObject codeContext) {
            PythonGetMemberBinder gmb = member as PythonGetMemberBinder;
            if (gmb != null) {
                return gmb.Fallback(self, codeContext);
            }

            GetMemberBinder gma = (GetMemberBinder)member;

            return gma.FallbackGetMember(self.Restrict(self.GetLimitType()));
        }

        protected static string GetGetMemberName(DynamicMetaObjectBinder member) {
            PythonGetMemberBinder gmb = member as PythonGetMemberBinder;
            if (gmb != null) {
                return gmb.Name;
            }

            InvokeMemberBinder invoke = member as InvokeMemberBinder;
            if (invoke != null) {
                return invoke.Name;
            }

            GetMemberBinder gma = (GetMemberBinder)member;

            return gma.Name;
        }

    }
}
