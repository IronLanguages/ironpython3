// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;

namespace IronPython.Runtime {
    /// <summary>
    /// Marks a class as being hidden from the Python hierarchy.  This is applied to the base class
    /// and then all derived types will not see the base class in their hierarchy and will not be
    /// able to access members declaredo on the base class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class PythonHiddenBaseClassAttribute : Attribute {
    }
}
