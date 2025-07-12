// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using IronPython.Runtime.Operations;

namespace IronPython.Runtime.Types {
    public class PythonTypeDataSlot : PythonTypeSlot {
        public virtual void __set__(CodeContext/*!*/ context, object instance, object value) {
            if (!TrySetValue(context, instance, DynamicHelpers.GetPythonType(instance), value)) {
                throw PythonOps.AttributeErrorForObjectMissingAttribute(instance, "__set__");
            }
        }

        public virtual void __delete__(CodeContext/*!*/ context, object instance) {
            if (!TryDeleteValue(context, instance, DynamicHelpers.GetPythonType(instance))) {
                throw PythonOps.AttributeErrorForObjectMissingAttribute(instance, "__delete__");
            }
        }

        internal override bool IsSetDescriptor(CodeContext context, PythonType owner) {
            return true;
        }
    }
}
