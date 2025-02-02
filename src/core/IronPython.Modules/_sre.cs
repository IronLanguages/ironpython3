// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Globalization;

using IronPython.Runtime;

[assembly: PythonModule("_sre", typeof(IronPython.Modules.PythonSRegEx))]
namespace IronPython.Modules {
    public static class PythonSRegEx {
        public const string __doc__ = "non-functional _sre module.  Included only for completeness.";

        public const int MAGIC = 20031017;
        public const int CODESIZE = 2;
        public const int MAXREPEAT = 65535;
        public const int MAXGROUPS = int.MaxValue;

        public static object getlower(CodeContext/*!*/ context, object? val, object? encoding) {
            int encInt = context.LanguageContext.ConvertToInt32(val);
            int charVal = context.LanguageContext.ConvertToInt32(val);

            if (encInt == (int)PythonRegex.UNICODE) {
                return (int)Char.ToLower((char)charVal);
            } else {
                return (int)Char.ToLower((char)charVal, CultureInfo.InvariantCulture);
            }
        }

        public static object? compile(object? a, object? b, object? c) {
            return null;
        }
    }
}
