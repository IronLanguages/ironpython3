// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;

namespace IronPython.Runtime.Binding {
    internal interface IFastInvokable {
        FastBindResult<T> MakeInvokeBinding<T>(CallSite<T> site, PythonInvokeBinder/*!*/ binder, CodeContext/*!*/ context, object[] args) where T : class;
    }
}
