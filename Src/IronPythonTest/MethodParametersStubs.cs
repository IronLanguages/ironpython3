// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

using Microsoft.Scripting;

namespace IronPythonTest {
    public static class DefaultParams {
        public static int FuncWithDefaults(int x=1,
           int y=2,
           int z=3) {
            return x + y + z;
        }
    }

    public static class SplatTest1 {
        public static object FuncWithIDictKwargs([ParamDictionary] IDictionary kwargs)
            => kwargs.Count == 1 && (string)kwargs["bla"] == "bla";
        public static object FuncWithIDictGenOOKwargs([ParamDictionary] IDictionary<object, object> kwargs)
            => kwargs.Count == 1 && (string)kwargs["bla"] == "bla";
        public static object FuncWithIDictGenSOKwargs([ParamDictionary] IDictionary<string, object> kwargs)
            => kwargs.Count == 1 && (string)kwargs["bla"] == "bla";
    }

    public static class SplatTest2 {
        public static object FuncWithIDictKwargs([ParamDictionary] IDictionary kwargs)
            => kwargs.Count == 1 && (string)kwargs["bla"] == "bla";
        public static object FuncWithIDictGenOOKwargs([ParamDictionary] IDictionary<object, object> kwargs)
            => kwargs.Count == 1 && (string)kwargs["bla"] == "bla";
        public static object FuncWithIDictGenSOKwargs([ParamDictionary] IDictionary<string, object> kwargs)
            => kwargs.Count == 1 && (string)kwargs["bla"] == "bla";
    }
}
