// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

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
