// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;

namespace IronPython.Runtime {
    /// <summary>
    /// For <c>IList〈byte〉</c> and <c>IReadOnlyList〈byte〉</c> parameters:
    /// Marks that the parameter is typed to accept a bytes-like object.
    /// <br/>
    /// It also disallows passing
    /// a Python list object and auto-applying our generic conversion.
    /// </summary>
    /// <remarks>
    /// A bytes-like object is any object of type implementing IBufferProtocol.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class BytesLikeAttribute : Attribute { }
}
