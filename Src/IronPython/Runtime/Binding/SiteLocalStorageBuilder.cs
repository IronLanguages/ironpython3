// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;
using AstUtils = Microsoft.Scripting.Ast.Utils;
using Microsoft.Scripting.Actions.Calls;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime.Binding {
    public sealed class SiteLocalStorageBuilder : ArgBuilder {
        public SiteLocalStorageBuilder(ParameterInfo info) 
            : base(info) {
        }

        public override int Priority {
            get { return -1; }
        }

        public override int ConsumedArgumentCount {
            get { return 0; }
        }

        protected override Expression ToExpression(OverloadResolver resolver, RestrictedArguments args, bool[] hasBeenUsed) {
            return AstUtils.Constant(Activator.CreateInstance(ParameterInfo.ParameterType));
        }
    }
}
