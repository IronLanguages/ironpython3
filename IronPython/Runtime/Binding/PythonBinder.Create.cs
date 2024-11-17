// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;

using IronPython.Runtime.Operations;

using Microsoft.Scripting.Actions;

using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;

    internal partial class PythonBinder : DefaultBinder {
        public DynamicMetaObject Create(CallSignature signature, DynamicMetaObject target, DynamicMetaObject[] args, Expression contextExpression) {

            Type t = GetTargetType(target.Value);

            if (t != null) {

                if (typeof(Delegate).IsAssignableFrom(t.GetTypeInfo()) && args.Length == 1) {
                    // PythonOps.GetDelegate(CodeContext context, object callable, Type t);
                    return new DynamicMetaObject(
                        Ast.Call(
                            typeof(PythonOps).GetMethod(nameof(PythonOps.GetDelegate)),
                            contextExpression,
                            AstUtils.Convert(args[0].Expression, typeof(object)),
                            Expression.Constant(t)
                        ),
                        target.Restrictions.Merge(BindingRestrictions.GetInstanceRestriction(target.Expression, target.Value))
                    );
                }

                return CallMethod(
                    new PythonOverloadResolver(
                        this,
                        args,
                        signature,
                        contextExpression
                    ),
                    PythonTypeOps.GetConstructors(t, PrivateBinding),
                    target.Restrictions.Merge(BindingRestrictions.GetInstanceRestriction(target.Expression, target.Value))
                );
            }

            return null;
        }

        private static Type GetTargetType(object target) {
            if (target is TypeTracker tt) {
                return tt.Type;
            }
            return target as Type;
        }
    }
}
