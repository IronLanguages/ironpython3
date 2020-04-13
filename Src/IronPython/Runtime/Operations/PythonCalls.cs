// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;

namespace IronPython.Runtime.Operations {
    public static partial class PythonCalls {
        public static object Call(object func, params object?[] args) {
            return DefaultContext.DefaultPythonContext.CallSplat(func, args);
        }

        public static object Call(CodeContext context, object func) {
            return context.LanguageContext.Call(context, func);
        }

        public static object Call(CodeContext/*!*/ context, object func, object? arg0) {
            return context.LanguageContext.Call(context, func, arg0);
        }

        public static object Call(CodeContext/*!*/ context, object func, object? arg0, object? arg1) {
            return context.LanguageContext.Call(context, func, arg0, arg1);
        }

        public static object Call(CodeContext/*!*/ context, object func, params object?[] args) {
            return context.LanguageContext.CallSplat(context, func, args);
        }

        public static object CallWithKeywordArgs(CodeContext/*!*/ context, object func, object?[] args, string[] names) {
            PythonDictionary dict = new PythonDictionary();
            for (int i = 0; i < names.Length; i++) {
                dict[names[i]] = args[args.Length - names.Length + i];
            }
            object?[] newargs = new object[args.Length - names.Length];
            for (int i = 0; i < newargs.Length; i++) {
                newargs[i] = args[i];
            }

            return CallWithKeywordArgs(context, func, newargs, dict);
        }

        public static object CallWithKeywordArgs(CodeContext context, object func, object?[] args, IDictionary<object, object> dict) {
            return context.LanguageContext.CallWithKeywords(func, args, dict);
        }        
    }
}
