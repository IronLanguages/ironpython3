// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using IronPython.Runtime.Types;

namespace IronPython.Runtime.Operations {
    public static class DictionaryOfTOps<K, V> where K : notnull {
        public static string __repr__(CodeContext/*!*/ context, Dictionary<K, V> self) {
            List<object>? infinite = PythonOps.GetAndCheckInfinite(self);
            if (infinite == null) {
                return "{...}";
            }

            int index = infinite.Count;
            infinite.Add(self);
            try {
                StringBuilder res = new StringBuilder();
                res.Append("Dictionary[");
                res.Append(DynamicHelpers.GetPythonTypeFromType(typeof(K)).Name);
                res.Append(", ");
                res.Append(DynamicHelpers.GetPythonTypeFromType(typeof(V)).Name);
                res.Append("](");
                if (self.Count > 0) {
                    res.Append("{");
                    string comma = "";
                    foreach (KeyValuePair<K, V> obj in self) {
                        res.Append(comma);
                        res.Append(PythonOps.Repr(context, obj.Key));
                        res.Append(" : ");
                        res.Append(PythonOps.Repr(context, obj.Value));
                        comma = ", ";
                    }
                    res.Append("}");
                }

                res.Append(")");
                return res.ToString();
            } finally {
                Debug.Assert(index == infinite.Count - 1);
                infinite.RemoveAt(index);
            }
        }
    }
}
