// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;

using System;
using System.Dynamic;

using Microsoft.Scripting.Actions;

using System.Runtime.CompilerServices;

namespace IronPython.Runtime.Binding {
    internal interface IFastGettable {
        T MakeGetBinding<T>(CallSite<T> site, PythonGetMemberBinder/*!*/ binder, CodeContext/*!*/ state, string/*!*/ name) where T : class;
    }
}
