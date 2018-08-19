// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using MSAst = System.Linq.Expressions;

using System;
using System.Diagnostics;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Binding;

namespace IronPython.Compiler.Ast {
    public abstract class Expression : Node {
        internal static Expression[] EmptyArray = new Expression[0];

        internal virtual MSAst.Expression TransformSet(SourceSpan span, MSAst.Expression right, PythonOperationKind op) {
            // unreachable, CheckAssign prevents us from calling this at parse time.
            Debug.Assert(false);
            throw new InvalidOperationException();
        }

        internal virtual MSAst.Expression TransformDelete() {
            Debug.Assert(false);
            throw new InvalidOperationException();
        }

        internal virtual ConstantExpression ConstantFold() {
            return null;
        }

        internal virtual string CheckAssign() {
            return "can't assign to " + NodeName;
        }

        internal virtual string CheckAugmentedAssign() => CheckAssign();

        internal virtual string CheckDelete() {
            return "can't delete " + NodeName;
        }

        internal virtual bool IsConstant {
            get {
                var folded = ConstantFold();
                if (folded != null) {
                    return folded.IsConstant;
                }
                return false;
            }
        }

        internal virtual object GetConstantValue() {            
            var folded = ConstantFold();
            if (folded != null && folded.IsConstant) {
                return folded.GetConstantValue();
            }

            throw new InvalidOperationException(GetType().Name + " is not a constant");
        }

        public override Type Type {
            get {
                return typeof(object);
            }
        }
    }
}
