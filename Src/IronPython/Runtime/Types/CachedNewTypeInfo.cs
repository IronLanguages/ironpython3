// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Microsoft.Scripting.Utils;

namespace IronPython.Runtime.Types {
    public class CachedNewTypeInfo {
        private readonly Type _type;
        private readonly Dictionary<string, string[]> _specialNames;
        private readonly Type[] _interfaceTypes;

        public CachedNewTypeInfo(Type type, Dictionary<string, string[]> specialNames, Type[] interfaceTypes) {
            _type = type;
            _specialNames = specialNames;
            _interfaceTypes = interfaceTypes ?? ReflectionUtils.EmptyTypes;
        }

        public IList<Type> InterfaceTypes {
            get { return _interfaceTypes; }
        }

        public Type Type {
            get { return _type; }
        }

        public Dictionary<string, string[]> SpecialNames {
            get { return _specialNames; }
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class PythonCachedTypeInfoAttribute : Attribute {
    }
}
