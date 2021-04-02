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
        internal static readonly Expression[] EmptyArray = Array.Empty<Expression>();

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
