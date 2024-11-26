// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;
using Microsoft.Scripting.Ast;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Dynamic;
using Microsoft.Scripting.Actions;

namespace IronPython.Compiler {
    /// <summary>
    /// Provides a wrapper around "dynamic" expressions which we've opened coded (for optimized code generation).
    /// 
    /// This lets us recognize both normal Dynamic and our own Dynamic expressions and apply the combo binder on them.
    /// </summary>
    internal class ReducableDynamicExpression : Expression, ILightExceptionAwareExpression {
        private readonly Expression/*!*/ _reduction;

        public ReducableDynamicExpression(Expression/*!*/ reduction, DynamicMetaObjectBinder/*!*/ binder, IList<Expression/*!*/>/*!*/ args) {
            _reduction = reduction;
            Binder = binder;
            Args = args;
        }

        public DynamicMetaObjectBinder/*!*/ Binder { get; }

        public IList<Expression/*!*/>/*!*/ Args { get; }

        public override bool CanReduce {
            get {
                return true;
            }
        }

        public sealed override ExpressionType NodeType {
            get { return ExpressionType.Extension; }
        }

        public sealed override Type/*!*/ Type {
            get { return _reduction.Type; }
        }

        public override Expression Reduce() {
            return _reduction;
        }

        #region ILightExceptionAwareExpression Members

        Expression ILightExceptionAwareExpression.ReduceForLightExceptions() {
            if (Binder is ILightExceptionBinder binder) {
                var lightBinder = binder.GetLightExceptionBinder() as DynamicMetaObjectBinder;
                if (lightBinder != binder) {
                    return DynamicExpression.Dynamic(
                        lightBinder,
                        Type,
                        Args);
                }
            }
            return this;
        }

        #endregion
    }
}
