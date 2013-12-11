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

using Microsoft.Scripting.Runtime;

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
