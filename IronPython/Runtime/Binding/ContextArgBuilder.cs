// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;
using System.Reflection;

using Microsoft.Scripting.Actions.Calls;

namespace IronPython.Runtime.Binding {

    /// <summary>
    /// ArgBuilder which provides the CodeContext parameter to a method.
    /// </summary>
    public sealed class ContextArgBuilder : ArgBuilder {
        public ContextArgBuilder(ParameterInfo info)
            : base(info) {
        }

        public override int Priority {
            get { return -1; }
        }

        public override int ConsumedArgumentCount {
            get { return 0; }
        }

        protected override Expression ToExpression(OverloadResolver resolver, RestrictedArguments args, bool[] hasBeenUsed) {
            return ((PythonOverloadResolver)resolver).ContextExpression;
        }
    }
}
