/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

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
    sealed class DynamicBaseTypeAttribute : Attribute {
    }
}
