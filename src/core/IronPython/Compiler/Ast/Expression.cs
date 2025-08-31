// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

using IronPython.Runtime.Binding;

using Microsoft.Scripting;

using AstUtils = Microsoft.Scripting.Ast.Utils;
using MSAst = System.Linq.Expressions;

namespace IronPython.Compiler.Ast {
    public abstract class Expression : Node {
        protected internal static MSAst.BlockExpression UnpackSequenceHelper<T>(ReadOnlySpan<Expression> items, MethodInfo makeEmpty, MethodInfo append, MethodInfo extend) {
            var expressions = new ReadOnlyCollectionBuilder<MSAst.Expression>(items.Length + 2);
            var varExpr = Expression.Variable(typeof(T), "$coll");
            expressions.Add(Expression.Assign(varExpr, Expression.Call(makeEmpty)));
            foreach (var item in items) {
                if (item is StarredExpression starredExpression) {
                    expressions.Add(Expression.Call(extend, varExpr, AstUtils.Convert(starredExpression.Value, typeof(object))));
                } else {
                    expressions.Add(Expression.Call(append, varExpr, AstUtils.Convert(item, typeof(object))));
                }
            }
            expressions.Add(varExpr);
            return Expression.Block(typeof(T), new MSAst.ParameterExpression[] { varExpr }, expressions);
        }

        internal virtual MSAst.Expression TransformSet(SourceSpan span, MSAst.Expression right, PythonOperationKind op) {
            // unreachable, CheckAssign prevents us from calling this at parse time.
            Debug.Assert(false);
            throw new InvalidOperationException();
        }

        internal virtual MSAst.Expression TransformDelete() {
            Debug.Assert(false);
            throw new InvalidOperationException();
        }

        internal virtual ConstantExpression? ConstantFold() => null;

        internal virtual string? CheckAssign() => "can't assign to " + NodeName;

        internal virtual string? CheckAugmentedAssign() => CheckAssign();

        internal virtual string? CheckDelete() => "can't delete " + NodeName;

        internal virtual bool IsConstant => ConstantFold()?.IsConstant ?? false;

        internal virtual object GetConstantValue() {            
            var folded = ConstantFold();
            if (folded != null && folded.IsConstant) {
                return folded.GetConstantValue();
            }

            throw new InvalidOperationException(GetType().Name + " is not a constant");
        }

        public override Type Type => typeof(object);
    }
}
