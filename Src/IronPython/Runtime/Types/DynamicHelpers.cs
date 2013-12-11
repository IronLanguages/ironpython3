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
using Microsoft.Scripting;
using Microsoft.Scripting.Generation;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime.Types {
    public static class DynamicHelpers {
        public static PythonType/*!*/ GetPythonTypeFromType(Type/*!*/ type) {
            ContractUtils.RequiresNotNull(type, "type");

            PerfTrack.NoteEvent(PerfTrack.Categories.DictInvoke, "TypeLookup " + type.FullName);

            return PythonType.GetPythonType(type);
        }

        public static PythonType GetPythonType(object o) {
            IPythonObject dt = o as IPythonObject;
            if (dt != null) return dt.PythonType;
            
            return GetPythonTypeFromType(CompilerHelpers.GetType(o));
        }

        public static ReflectedEvent.BoundEvent MakeBoundEvent(ReflectedEvent eventObj, object instance, Type type) {
            return new ReflectedEvent.BoundEvent(eventObj, instance, DynamicHelpers.GetPythonTypeFromType(type));
        }
    }
}
