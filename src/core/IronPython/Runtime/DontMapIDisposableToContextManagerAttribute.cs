// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;

namespace IronPython.Runtime {
    /// <summary>
    /// Marks a type so that IronPython will not expose the IDisposable interface out as
    /// __enter__ and __exit__ methods of a context manager.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    internal sealed class DontMapIDisposableToContextManagerAttribute : Attribute { }
}
