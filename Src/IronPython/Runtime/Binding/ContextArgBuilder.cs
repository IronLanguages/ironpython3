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

using System.Linq.Expressions;

using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System;
using System.Dynamic;
using Microsoft.Scripting.Actions.Calls;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime.Binding {

    /// <summary>
    /// ArgBuilder which provides the CodeContext parameter to a method.
    /// </summary>
    public sealed class ContextArgBuilder : ArgBuilder {
        public ContextArgBuilder(ParameterInfo info) 
            : base(info){
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
