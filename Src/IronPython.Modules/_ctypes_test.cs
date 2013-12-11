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

using IronPython.Runtime;

#if FEATURE_NATIVE
[assembly: PythonModule("_ctypes_test", typeof(IronPython.Modules.CTypesTest))]
namespace IronPython.Modules {
    public static class CTypesTest {
        // TODO: This isn't right
        public static string __file__ = Environment.GetEnvironmentVariable("DLR_ROOT") + "\\External.LCA_RESTRICTED\\Languages\\IronPython\\27\\DLLs\\_ctypes_test.pyd";

        public static void func() {
        }

        public static void func_si(string s, int i) {
        }
    }
}
#endif