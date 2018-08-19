// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;

namespace IronPython.Runtime {
    /// <summary>
    /// Marks a method/field/property as being a wrapper descriptor.  A wrapper desriptor
    /// is a member defined on PythonType but is available both for type and other
    /// instances of type.  For example type.__bases__.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    internal sealed class WrapperDescriptorAttribute : Attribute {
    }
}
