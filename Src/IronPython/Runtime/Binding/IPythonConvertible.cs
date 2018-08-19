// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Linq.Expressions;

using System;
using System.Dynamic;
using Microsoft.Scripting.Actions;

namespace IronPython.Runtime.Binding {
    interface IPythonConvertible {
        DynamicMetaObject BindConvert(PythonConversionBinder/*!*/ binder);
    }
}
