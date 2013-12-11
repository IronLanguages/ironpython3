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
