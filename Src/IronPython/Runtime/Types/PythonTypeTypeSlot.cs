// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Microsoft.Scripting.Runtime;
using IronPython.Runtime.Operations;

namespace IronPython.Runtime.Types {
    public class PythonTypeTypeSlot : PythonTypeDataSlot {
        public static string __doc__ = "the object's class";

        internal override bool TryGetValue(CodeContext context, object instance, PythonType owner, out object value) {
            if (instance == null) {
                if (owner == TypeCache.Null) {
                    value = owner;
                } else {
                    value = DynamicHelpers.GetPythonType(owner);
                }
            } else {
                value = DynamicHelpers.GetPythonType(instance);
            }

            return true;
        }

        internal override bool GetAlwaysSucceeds {
            get {
                return true;
            }
        }

        internal override bool TrySetValue(CodeContext context, object instance, PythonType owner, object value) {
            if (instance == null) return false;

            IPythonObject sdo = instance as IPythonObject;
            if (sdo == null) {
                throw PythonOps.TypeError("__class__ assignment: only for user defined types");
            }

            PythonType dt = value as PythonType;
            if (dt == null) throw PythonOps.TypeError("__class__ must be set to new-style class, not '{0}' object", DynamicHelpers.GetPythonType(value).Name);

            if(dt.UnderlyingSystemType != DynamicHelpers.GetPythonType(instance).UnderlyingSystemType)
                throw PythonOps.TypeErrorForIncompatibleObjectLayout("__class__ assignment", DynamicHelpers.GetPythonType(instance), dt.UnderlyingSystemType);

            sdo.SetPythonType(dt);
            return true;
        }

        internal override bool TryDeleteValue(CodeContext context, object instance, PythonType owner) {
            throw PythonOps.AttributeErrorForReadonlyAttribute(PythonTypeOps.GetName(instance), "__class__");
        }
    }
}
