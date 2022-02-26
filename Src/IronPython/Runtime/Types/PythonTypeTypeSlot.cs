// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using Microsoft.Scripting.Runtime;
using IronPython.Runtime.Operations;

namespace IronPython.Runtime.Types {
    public class PythonTypeTypeSlot : PythonTypeDataSlot {
        public static string __doc__ = "the object's class";

        internal override bool TryGetValue(CodeContext context, object instance, PythonType owner, out object value) {
            if (instance is null) {
                if (owner == TypeCache.Null) {
                    value = owner;
                } else {
                    value = DynamicHelpers.GetPythonType(owner);
                }
            } else if (instance is int) {
                // IronPython uses Int32 objects as "int" for performance reasons (GH #52)
                value = TypeCache.BigInteger;
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

            if (!(instance is IPythonObject sdo)) {
                throw PythonOps.TypeError("__class__ assignment: only for user defined types");
            }

            if (!(value is PythonType dt)) throw PythonOps.TypeError("__class__ must be set to new-style class, not '{0}' object", PythonOps.GetPythonTypeName(value));

            if(dt.UnderlyingSystemType != DynamicHelpers.GetPythonType(instance).UnderlyingSystemType)
                throw PythonOps.TypeErrorForIncompatibleObjectLayout("__class__ assignment", DynamicHelpers.GetPythonType(instance), dt.UnderlyingSystemType);

            sdo.SetPythonType(dt);
            return true;
        }

        internal override bool TryDeleteValue(CodeContext context, object instance, PythonType owner) {
            throw PythonOps.AttributeErrorForReadonlyAttribute(PythonOps.GetPythonTypeName(instance), "__class__");
        }
    }
}
