// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;

namespace IronPython.Runtime {
    /// <summary>
    /// Provides more specific type information for Python dictionaries which are not strongly typed.
    /// 
    /// This attribute can be applied to fields, parameters, proeprties, and return values.  It can be
    /// inspected to get type information about the types of the keys and values of the expected 
    /// dictionary or the returned dictionary.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue, AllowMultiple = false)]
    public sealed class DictionaryTypeInfoAttribute : Attribute {
        public DictionaryTypeInfoAttribute(Type keyType, Type valueType) {
            KeyType = keyType;
            ValueType = valueType;
        }

        public Type KeyType { get; }

        public Type ValueType { get; }
    }
}
