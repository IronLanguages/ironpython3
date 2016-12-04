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
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime {
    /// <summary>
    /// This assembly-level attribute specifies which types in the engine represent built-in Python modules.
    /// 
    /// Members of a built-in module type should all be static as an instance is never created.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
    public sealed class PythonModuleAttribute : Attribute {
        private readonly string/*!*/ _name;
        private readonly Type/*!*/ _type;
        private readonly PlatformID[]/*!*/ _invalidPlatforms;

        /// <summary>
        /// Creates a new PythonModuleAttribute that can be used to specify a built-in module that exists
        /// within an assembly.
        /// </summary>
        /// <param name="name">The built-in module name</param>
        /// <param name="type">The type that implements the built-in module.</param>
        /// <param name="invalidPlatforms">The invalid platform identifiers for this module.</param>
        public PythonModuleAttribute(string/*!*/ name, Type/*!*/ type, params PlatformID[] invalidPlatforms) {
            ContractUtils.RequiresNotNull(name, "name");
            ContractUtils.RequiresNotNull(type, "type");

            _name = name;
            _type = type;
            _invalidPlatforms = invalidPlatforms;
        }

        /// <summary>
        /// The built-in module name
        /// </summary>
        public string/*!*/ Name {
            get { return _name; }
        }

        /// <summary>
        /// The type that implements the built-in module
        /// </summary>
        public Type/*!*/ Type {
            get { return _type; }
        }

        public PlatformID[] InvalidPlatforms {
            get { return _invalidPlatforms; }
        }
    }
}
