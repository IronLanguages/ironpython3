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
using System.Reflection;
using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime.Types {

    public class ExtensionPropertyInfo {
        private MethodInfo _getter, _setter, _deleter;
        private Type _declaringType;

        public ExtensionPropertyInfo(Type logicalDeclaringType, MethodInfo mi) {
            _declaringType = logicalDeclaringType;

            string methodName = mi.Name;
            string prefix = "";

#if FEATURE_REFEMIT
            if (methodName.StartsWith(NewTypeMaker.BaseMethodPrefix)) {
                methodName = methodName.Substring(NewTypeMaker.BaseMethodPrefix.Length);
                prefix = NewTypeMaker.BaseMethodPrefix;
            }
#endif

            if (methodName.StartsWith("Get") || methodName.StartsWith("Set")) {
                GetPropertyMethods(mi, methodName, prefix, "Get", "Set", "Delete");
            } else if(methodName.StartsWith("get_") || methodName.StartsWith("set_")) {
                GetPropertyMethods(mi, methodName, prefix, "get_", "set_", null);
            } 
#if FEATURE_REFEMIT
            else if (methodName.StartsWith(NewTypeMaker.FieldGetterPrefix) || methodName.StartsWith(NewTypeMaker.FieldSetterPrefix)) {
                GetPropertyMethods(mi, methodName, prefix, NewTypeMaker.FieldGetterPrefix, NewTypeMaker.FieldSetterPrefix, null);
            }
#endif
        }

        private void GetPropertyMethods(MethodInfo mi, string methodName, string prefix, string get, string set, string delete) {
            string propname = methodName.Substring(get.Length);

            if (String.Compare(mi.Name, 0, get, 0, get.Length) == 0) {
                _getter = mi;
                _setter = mi.DeclaringType.GetMethod(prefix + set + propname);
            } else {
                _getter = mi.DeclaringType.GetMethod(prefix + get + propname);
                _setter = mi;
            }

            if (delete != null) {
                _deleter = mi.DeclaringType.GetMethod(prefix + delete + propname);
            }
        }

        public MethodInfo Getter {
            get { return _getter; }
        }

        public MethodInfo Setter {
            get { return _setter; }
        }

        public MethodInfo Deleter {
            get { return _deleter; }
        }

        public Type DeclaringType {
            get { return _declaringType; }
        }

        public string Name {
            get {
                // remove Get or Set from name
                if (_getter != null) return _getter.Name.Substring(3);
                return _setter.Name.Substring(3);
            }
        }
    }
}
