// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;

using IronPython.Runtime;

[assembly: PythonModule("_string", typeof(IronPython.Modules.PythonString))]
namespace IronPython.Modules {
    public static class PythonString {
        public static IEnumerable<PythonTuple>/*!*/ formatter_parser([NotNone] string/*!*/ self)
            => NewStringFormatter.GetFormatInfo(self);

        public static PythonTuple/*!*/ formatter_field_name_split([NotNone] string/*!*/ self)
            => NewStringFormatter.GetFieldNameInfo(self);
    }
}
