// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;

using System;
using System.Dynamic;
using System.Runtime.CompilerServices;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime.Binding {
    internal interface IFastInvokable {
        FastBindResult<T> MakeInvokeBinding<T>(CallSite<T> site, PythonInvokeBinder/*!*/ binder, CodeContext/*!*/ context, object[] args) where T : class;
    }
}
