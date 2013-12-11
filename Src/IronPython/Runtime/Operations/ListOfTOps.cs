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

using System.Collections.Generic;
using System.Text;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Types;

namespace IronPython.Runtime.Operations {
    public static class ListOfTOps<T> {
        public static string __repr__(CodeContext/*!*/ context, List<T> self) {
            List<object> infinite = PythonOps.GetAndCheckInfinite(self);
            if (infinite == null) {
                return "[...]";
            }

            int index = infinite.Count;
            infinite.Add(self);
            try {
                StringBuilder res = new StringBuilder();
                res.Append("List[");
                res.Append(DynamicHelpers.GetPythonTypeFromType(typeof(T)).Name);
                res.Append("](");
                if (self.Count > 0) {
                    res.Append("[");
                    string comma = "";
                    foreach (T obj in self) {
                        res.Append(comma);
                        res.Append(PythonOps.Repr(context, obj));
                        comma = ", ";
                    }
                    res.Append("]");
                }

                res.Append(")");
                return res.ToString();
            } finally {
                System.Diagnostics.Debug.Assert(index == infinite.Count - 1);
                infinite.RemoveAt(index);
            }
        }
    }
}
