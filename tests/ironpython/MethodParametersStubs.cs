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

    public static class Variadics {
        public static object FuncWithIDictKwargs([ParamDictionary] IDictionary kwargs) {
            foreach (DictionaryEntry kvp in kwargs) {
                if (kvp.Value is string val && (string)kvp.Key != val) return kvp.Key;
            }
            int num = kwargs.Count;

            // check that kwargs is mutable
            kwargs["$"] = 1;
            if ((int)kwargs["$"] != 1) return "$";

            kwargs.Remove("$");
            if (kwargs.Count != num) return "$-";

            return num;
        }

        public static object FuncWithDictGenOOKwargs([ParamDictionary] Dictionary<object, object> kwargs)
            => FuncWithIDictGenOOKwargs(kwargs);

        public static object FuncWithIDictGenOOKwargs([ParamDictionary] IDictionary<object, object> kwargs) {
            foreach (KeyValuePair<object, object> kvp in kwargs) {
                if (kvp.Value is string val && (string)kvp.Key != val) return kvp.Key;
            }
            int num = kwargs.Count;

            // check that kwargs is mutable
            kwargs["$"] = 1;
            if ((int)kwargs["$"] != 1) return "$";

            kwargs.Remove("$");
            if (kwargs.Count != num) return "$-";

            return num;
        }

        public static object FuncWithDictGenSOKwargs([ParamDictionary] Dictionary<string, object> kwargs)
            => FuncWithIDictGenSOKwargs(kwargs);

        public static object FuncWithIDictGenSOKwargs([ParamDictionary] IDictionary<string, object> kwargs) {
            foreach (KeyValuePair<string, object> kvp in kwargs) {
                if (kvp.Value is string val && kvp.Key != val) return kvp.Key;
            }
            int num = kwargs.Count;

            // check that kwargs is mutable
            kwargs["$"] = 1;
            if ((int)kwargs["$"] != 1) return "$";

            kwargs.Remove("$");
            if (kwargs.Count != num) return "$-";

            return num;
        }

        public static object FuncWithIDictGenSIKwargs([ParamDictionary] IDictionary<string, int> kwargs) {
            foreach (KeyValuePair<string, int> kvp in kwargs) {
                if (kvp.Key != $"arg{kvp.Value}") return kvp.Key;
            }
            int num = kwargs.Count;

            // check that kwargs is mutable
            kwargs["$"] = 1;
            if ((int)kwargs["$"] != 1) return "$";

            kwargs.Remove("$");
            if (kwargs.Count != num) return "$-";

            return num;
        }

        public static object FuncWithIRoDictGenSOKwargs([ParamDictionary] IReadOnlyDictionary<string, object> kwargs) {
            foreach (KeyValuePair<string, object> kvp in kwargs) {
                if (kvp.Value is string val && kvp.Key != val) return kvp.Key;
            }
            return kwargs.Count;
        }

        public static object FuncWithIRoDictGenOOKwargs([ParamDictionary] IReadOnlyDictionary<object, object> kwargs) {
            foreach (KeyValuePair<object, object> kvp in kwargs) {
                if (kvp.Value is string val && (string)kvp.Key != val) return kvp.Key;
            }
            return kwargs.Count;
        }

        public static object FuncWithIRoDictGenSIKwargs([ParamDictionary] IReadOnlyDictionary<string, int> kwargs) {
            foreach (KeyValuePair<string, int> kvp in kwargs) {
                if (kvp.Key != $"arg{kvp.Value}") return kvp.Key;
            }
            return kwargs.Count;
        }

        public static object FuncWithAttribColKwargs([ParamDictionary] /*unsupported*/ AttributeCollection kwargs) {
            return kwargs.Count;
        }
    }
}
