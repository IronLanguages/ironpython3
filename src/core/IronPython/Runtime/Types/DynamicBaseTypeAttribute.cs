// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;

namespace IronPython.Runtime.Types {
    /// <summary>
    /// Marks a type as being a suitable type to be used for user-defined classes.
    /// 
    /// The requirements for this are that a type has to follow the patterns
    /// that NewTypeMaker derived types follow.  This includes:
    ///     The type's constructors must all take PythonType as the 1st parameter
    ///         which sets the underlying type for the actual object
    ///     The type needs to implement IPythonObject
    ///     Dictionary-based storage needs to be provided for setting individual members
    ///     Virtual methods exposed to Python need to support checking the types dictionary for invocations
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple=false, Inherited=true)]
    internal sealed class DynamicBaseTypeAttribute : Attribute {
    }
}
