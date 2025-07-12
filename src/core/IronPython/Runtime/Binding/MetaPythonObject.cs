// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Dynamic;
using System.Linq.Expressions;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting.Ast;
using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;
    using AstUtils = Microsoft.Scripting.Ast.Utils;

    internal partial class MetaPythonObject : DynamicMetaObject {
        public MetaPythonObject(Expression/*!*/ expression, BindingRestrictions/*!*/ restrictions)
            : base(expression, restrictions) {
        }

        public MetaPythonObject(Expression/*!*/ expression, BindingRestrictions/*!*/ restrictions, object value)
            : base(expression, restrictions, value) {
        }

        public DynamicMetaObject/*!*/ FallbackConvert(DynamicMetaObjectBinder/*!*/ binder) {
            if (binder is PythonConversionBinder pyBinder) {
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

        public static string/*!*/ GetPythonTypeName(DynamicMetaObject/*!*/ value) {
            if (value.HasValue) {
                return PythonOps.GetPythonTypeName(value.Value);
            }

            return DynamicHelpers.GetPythonTypeFromType(value.GetLimitType()).Name;
        }

        /// <summary>
        /// Creates a target which creates a new dynamic method which contains a single
        /// dynamic site that invokes the callable object.
        /// 
        /// TODO: This should be specialized for each callable object
        /// </summary>
        protected static DynamicMetaObject/*!*/ MakeDelegateTarget(DynamicMetaObjectBinder/*!*/ action, Type/*!*/ toType, DynamicMetaObject/*!*/ arg) {
            PythonContext state = PythonContext.GetPythonContext(action);
            CodeContext context = state != null ? state.SharedContext : DefaultContext.Default;

            return new DynamicMetaObject(
                Ast.Convert(
                    Ast.Call(
                        typeof(PythonOps).GetMethod(nameof(PythonOps.GetDelegate))!,
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
            if (member is PythonGetMemberBinder gmb) {
                return gmb.Fallback(self, codeContext);
            }

            GetMemberBinder gma = (GetMemberBinder)member;

            return gma.FallbackGetMember(self.Restrict(self.GetLimitType()));
        }

        protected static string GetGetMemberName(DynamicMetaObjectBinder member) {
            if (member is PythonGetMemberBinder gmb) {
                return gmb.Name;
            }

            if (member is InvokeMemberBinder invoke) {
                return invoke.Name;
            }

            GetMemberBinder gma = (GetMemberBinder)member;

            return gma.Name;
        }
    }
}
