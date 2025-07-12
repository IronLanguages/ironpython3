// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;

namespace IronPython.Runtime {
    /// <summary>
    /// Marks that the return value of a function might include NotImplemented.
    /// 
    /// This is added to an operator method to ensure that all necessary methods are called
    /// if one cannot guarantee that it can perform the comparison.
    /// </summary>
    [AttributeUsage(AttributeTargets.ReturnValue)]
    public sealed class MaybeNotImplementedAttribute : Attribute { }
}
