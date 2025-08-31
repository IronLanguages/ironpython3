// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Dynamic;
using System.Linq.Expressions;

namespace IronPython.Runtime.Binding {
    /// <summary>
    /// Interface used to mark objects as being invokable from Python.  These objects support
    /// calling with splatted positional and keyword arguments.
    /// </summary>
    internal interface IPythonInvokable {
        DynamicMetaObject/*!*/ Invoke(PythonInvokeBinder/*!*/ pythonInvoke, Expression/*!*/ codeContext, DynamicMetaObject/*!*/ target, DynamicMetaObject/*!*/[]/*!*/ args);
    }
}
