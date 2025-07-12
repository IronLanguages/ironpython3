// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;

namespace IronPython.Runtime {
    /// <summary>
    /// Marks a type so that IronPython will not expose the ICollection interface out as
    /// __len__.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    internal sealed class DontMapICollectionToLenAttribute : Attribute { }
}
