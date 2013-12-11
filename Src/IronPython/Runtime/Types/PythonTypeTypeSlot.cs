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
