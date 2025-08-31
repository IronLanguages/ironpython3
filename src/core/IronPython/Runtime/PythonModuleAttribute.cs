// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;

using Microsoft.Scripting.Utils;

namespace IronPython.Runtime {
    /// <summary>
    /// This assembly-level attribute specifies which types in the engine represent built-in Python modules.
    /// 
    /// Members of a built-in module type should all be static as an instance is never created.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
    public sealed class PythonModuleAttribute : PlatformsAttribute {
        public PythonModuleAttribute(string/*!*/ name, Type/*!*/ type) {
            ContractUtils.RequiresNotNull(name, nameof(name));
            ContractUtils.RequiresNotNull(type, nameof(type));

            Name = name;
            Type = type;
        }

        /// <summary>
        /// Creates a new PythonModuleAttribute that can be used to specify a built-in module that exists
        /// within an assembly.
        /// </summary>
        /// <param name="name">The built-in module name</param>
        /// <param name="type">The type that implements the built-in module.</param>
        /// <param name="validPlatforms">The valid platform identifiers for this module.</param>
        public PythonModuleAttribute(string/*!*/ name, Type/*!*/ type, params PlatformID[] validPlatforms) : this(name, type) {
            ValidPlatforms = validPlatforms;
        }

        public PythonModuleAttribute(string/*!*/ name, Type/*!*/ type, PlatformFamily validPlatformFamily) : this(name, type) {
            SetValidPlatforms(validPlatformFamily);
        }

        /// <summary>
        /// The built-in module name
        /// </summary>
        public string/*!*/ Name { get; }

        /// <summary>
        /// The type that implements the built-in module
        /// </summary>
        public Type/*!*/ Type { get; }
    }
}
