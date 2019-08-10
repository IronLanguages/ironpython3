// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using IronPython.Runtime.Types;

namespace IronPython.Runtime.Operations {

    public static class DelegateOps {
        [StaticExtensionMethod]
        public static object __new__(CodeContext context, PythonType type, object function) {
            if (type == null) throw PythonOps.TypeError("expected type for 1st param, got null");

            IDelegateConvertible dlgConv = function as IDelegateConvertible;
            if (dlgConv != null) {
                Delegate res = dlgConv.ConvertToDelegate(type.UnderlyingSystemType);
                if (res != null) {
                    return res;
                }
            }

            return context.LanguageContext.DelegateCreator.GetDelegate(function, type.UnderlyingSystemType);
        }

        public static Delegate/*!*/ InPlaceAdd(Delegate/*!*/ self, Delegate/*!*/ other) {
            ContractUtils.RequiresNotNull(self, nameof(self));
            ContractUtils.RequiresNotNull(other, nameof(other));

            return Delegate.Combine(self, other);            
        }

        public static Delegate/*!*/ InPlaceSubtract(Delegate/*!*/ self, Delegate/*!*/ other) {
            ContractUtils.RequiresNotNull(self, nameof(self));
            ContractUtils.RequiresNotNull(other, nameof(other));

            return Delegate.Remove(self, other);
        }

        public static object Call(CodeContext/*!*/ context, Delegate @delegate, params object[] args) {
            return context.LanguageContext.CallSplat(@delegate, args);
        }

        public static object Call(CodeContext/*!*/ context, Delegate @delegate, [ParamDictionary]IDictionary<object, object> dict, params object[] args) {
            return context.LanguageContext.CallWithKeywords(@delegate, args, dict);
        }

    }

    /// <summary>
    /// Interface used for things which can convert to delegates w/o code gen.  Currently
    /// this is just non-overloaded builtin functions and bound builtin functions.  Avoiding
    /// the code gen is not only nice for compilation but it also enables delegates to be added
    /// in C# and removed in Python.
    /// </summary>
    internal interface IDelegateConvertible {
        Delegate ConvertToDelegate(Type/*!*/ type);
    }
}
