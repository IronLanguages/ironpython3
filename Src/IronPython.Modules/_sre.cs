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
using System.Globalization;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime;

[assembly: PythonModule("_sre", typeof(IronPython.Modules.PythonSRegEx))]
namespace IronPython.Modules {
    public static class PythonSRegEx {
        public const string __doc__ = "non-functional _sre module.  Included only for completeness.";

        public const int MAGIC = 20031017;
        public const int CODESIZE = 2;

        public static object getlower(CodeContext/*!*/ context, object val, object encoding) {
            int encInt = context.LanguageContext.ConvertToInt32(val);
            int charVal = context.LanguageContext.ConvertToInt32(val);

            if (encInt == (int)PythonRegex.UNICODE) {
                return (int)Char.ToLower((char)charVal);
            } else {
                return (int)Char.ToLower((char)charVal, CultureInfo.InvariantCulture);
            }
        }

        public static object compile(object a, object b, object c) {
            return null;
        }
    }
}