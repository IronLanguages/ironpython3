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
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Operations;

using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Runtime.Binding {
    using Ast = Expression;

    partial class PythonBinder : DefaultBinder {
        public DynamicMetaObject Create(CallSignature signature, DynamicMetaObject target, DynamicMetaObject[] args, Expression contextExpression) {

            Type t = GetTargetType(target.Value);

            if (t != null) {

                if (typeof(Delegate).GetTypeInfo().IsAssignableFrom(t.GetTypeInfo()) && args.Length == 1) {
                    // PythonOps.GetDelegate(CodeContext context, object callable, Type t);
                    return new DynamicMetaObject(
                        Ast.Call(
                            typeof(PythonOps).GetMethod("GetDelegate"),
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
                    CompilerHelpers.GetConstructors(t, PrivateBinding),
                    target.Restrictions.Merge(BindingRestrictions.GetInstanceRestriction(target.Expression, target.Value))
                );
            }

            return null;
        }

        private static Type GetTargetType(object target) {
            TypeTracker tt = target as TypeTracker;
            if (tt != null) {
                return tt.Type;
            }
            return target as Type;
        }
    }
}
