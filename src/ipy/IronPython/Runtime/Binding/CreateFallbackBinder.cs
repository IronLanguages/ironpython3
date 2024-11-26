// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using IronPython.Runtime.Operations;
using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Utils;
using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Runtime.Binding {
    /// <summary>
    /// Fallback action for performing a new() on a foreign IDynamicMetaObjectProvider.  used
    /// when call falls back.
    /// </summary>
    internal class CreateFallback : CreateInstanceBinder, IPythonSite {
        private readonly CompatibilityInvokeBinder/*!*/ _fallback;

        public CreateFallback(CompatibilityInvokeBinder/*!*/ realFallback, CallInfo /*!*/ callInfo)
            : base(callInfo) {
            _fallback = realFallback;
        }

        public override DynamicMetaObject/*!*/ FallbackCreateInstance(DynamicMetaObject/*!*/ target, DynamicMetaObject/*!*/[]/*!*/ args, DynamicMetaObject errorSuggestion) {
            return _fallback.InvokeFallback(target, args, BindingHelpers.GetCallSignature(this), errorSuggestion);
        }

        #region IPythonSite Members

        public PythonContext Context {
            get { return _fallback.Context; }
        }

        #endregion
    }

}
