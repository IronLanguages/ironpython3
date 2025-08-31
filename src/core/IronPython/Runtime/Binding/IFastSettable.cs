// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Runtime.CompilerServices;

namespace IronPython.Runtime.Binding {
    internal interface IFastSettable {
        T MakeSetBinding<T>(CallSite<T> site, PythonSetMemberBinder/*!*/ binder) where T : class;
    }
}
