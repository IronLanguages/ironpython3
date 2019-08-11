// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;

namespace IronPython.Runtime {
    /// <summary>
    /// For IList<byte/> arguments: Marks that the argument is typed to accept a bytes or
    /// bytearray object.  This attribute disallows passing a Python list object and
    /// auto-applying our generic conversion.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class BytesConversionAttribute : Attribute { }
}
