// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;

namespace IronPython.Runtime {
    /// <summary>
    /// Marks a type as being a PythonType for purposes of member lookup, creating instances, etc...  
    /// 
    /// If defined a PythonType will use __new__ / __init__ when creating instances.  This allows the
    /// object to match the native Python behavior such as returning cached values from __new__ or
    /// supporting initialization to run multiple times via __init__.
    ///
    /// The attribute also allows you to specify an alternate type name.  This allows the .NET name to
    /// be different from the Python name so they can follow .NET naming conventions.
    /// 
    /// Types defining this attribute also don't show CLR methods such as Equals, GetHashCode, etc... until
    /// the user has done an import clr.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct, Inherited = false)]
    public sealed class PythonTypeAttribute : Attribute {
        private readonly string _name;

        public PythonTypeAttribute() {
        }

        public PythonTypeAttribute(string name) {
            _name = name;
        }

        public string Name {
            get {
                return _name;
            }
        }
    }

}
