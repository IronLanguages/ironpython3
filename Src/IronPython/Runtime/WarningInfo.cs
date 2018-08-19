// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;

using System;
using System.Dynamic;

using Microsoft.Scripting.Utils;
using AstUtils = Microsoft.Scripting.Ast.Utils;

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Actions.Calls;

namespace IronPython.Runtime.Binding {
    class WarningInfo {
        private readonly string/*!*/ _message;
        private readonly PythonType/*!*/ _type;
        private readonly Expression _condition;

        public WarningInfo(PythonType/*!*/ type, string/*!*/ message) {
            _message = message;
            _type = type;
        }

        public WarningInfo(PythonType/*!*/ type, string/*!*/ message, Expression condition) {
            _message = message;
            _type = type;
            _condition = condition;
        }

        public DynamicMetaObject/*!*/ AddWarning(Expression/*!*/ codeContext, DynamicMetaObject/*!*/ result) {
            Expression warn = Expression.Call(
                typeof(PythonOps).GetMethod(nameof(PythonOps.Warn)),
                codeContext,
                AstUtils.Constant(_type),
                AstUtils.Constant(_message),
                AstUtils.Constant(ArrayUtils.EmptyObjects)
            );

            if (_condition != null) {
                warn = Expression.Condition(_condition, warn, AstUtils.Empty());
            }

            return new DynamicMetaObject(
                    Expression.Block(
                    warn,
                    result.Expression
                ),
                result.Restrictions
            );
        }
    }
}
