// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;

using IronPython.Runtime.Operations;

namespace IronPython.Runtime.Types {
    /// <summary>
    /// Represents a ReflectedProperty created for an extension method.  Logically the property is an
    /// instance property but the method implementing it is static.
    /// </summary>
    [PythonType("member_descriptor")]
    public class ReflectedExtensionProperty : ReflectedGetterSetter {
        private readonly MethodInfo _deleter;
        private readonly ExtensionPropertyInfo/*!*/ _extInfo;

        public ReflectedExtensionProperty(ExtensionPropertyInfo info, NameType nt)
            : base(new MethodInfo[] { info.Getter }, new MethodInfo[] { info.Setter }, nt) {
            _extInfo = info;
            _deleter = info.Deleter;
        }

        internal override bool TryGetValue(CodeContext context, object instance, PythonType owner, out object value) {
            if (Getter.Length == 0 || (instance == null && Getter[0].IsDefined(typeof(WrapperDescriptorAttribute), false))) {
                value = null;
                return false;
            }

            value = CallGetter(context, null, instance, []);
            return true;
        }

        internal override bool TrySetValue(CodeContext context, object instance, PythonType owner, object value) {
            if (Setter.Length == 0 || instance == null) return false;

            return CallSetter(context, null, instance, [], value);
        }

        internal override bool TryDeleteValue(CodeContext context, object instance, PythonType owner) {
            if (_deleter == null || instance == null) {
                return base.TryDeleteValue(context, instance, owner);
            }

            CallTarget(context, null, new MethodInfo[] { _deleter }, instance);
            return true;
        }

        internal override bool IsSetDescriptor(CodeContext context, PythonType owner) {
            return Setter.Length > 0;
        }

        internal override bool GetAlwaysSucceeds {
            get {
                // wrapper descriptors need to not work so we can pass the call onto the types wrapper descriptor
                return Getter.Length != 0 && !Getter[0].IsDefined(typeof(WrapperDescriptorAttribute), false);
            }
        }

        internal override bool CanOptimizeGets {
            get {
                return true;
            }
        }

        public void __set__(CodeContext context, object instance, object value) {
            if (!TrySetValue(context, instance, DynamicHelpers.GetPythonType(instance), value)) {
                throw PythonOps.AttributeError("readonly attribute");
            }
        }

        public void __delete__(CodeContext/*!*/ context, object instance) {
            if (!TryDeleteValue(context, instance, DynamicHelpers.GetPythonType(instance))) {
                throw PythonOps.AttributeErrorForObjectMissingAttribute(instance, "__delete__");
            }
        }

        internal override Type DeclaringType {
            get {
                return _extInfo.DeclaringType;
            }
        }

        internal ExtensionPropertyInfo ExtInfo {
            get {
                return _extInfo;
            }
        }

        public override string __name__ {
            get {
                return _extInfo.Name;
            }
        }

        public string __doc__ {
            get {
                return DocBuilder.DocOneInfo(ExtInfo);
            }
        }
    }
}
