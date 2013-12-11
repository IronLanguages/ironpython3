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
using System.Collections.Generic;
using System.Text;

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
