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
            ContractUtils.RequiresNotNull(self, "self");
            ContractUtils.RequiresNotNull(other, "other");

            return Delegate.Combine(self, other);            
        }

        public static Delegate/*!*/ InPlaceSubtract(Delegate/*!*/ self, Delegate/*!*/ other) {
            ContractUtils.RequiresNotNull(self, "self");
            ContractUtils.RequiresNotNull(other, "other");

            return Delegate.Remove(self, other);
        }

        public static object Call(CodeContext/*!*/ context, Delegate @delegate, params object[] args) {
            return PythonContext.GetContext(context).CallSplat(@delegate, args);
        }

        public static object Call(CodeContext/*!*/ context, Delegate @delegate, [ParamDictionary]IDictionary<object, object> dict, params object[] args) {
            return PythonContext.GetContext(context).CallWithKeywords(@delegate, args, dict);
        }

    }

    /// <summary>
    /// Interface used for things which can convert to delegates w/o code gen.  Currently
    /// this is just non-overloaded builtin functions and bound builtin functions.  Avoiding
    /// the code gen is not only nice for compilation but it also enables delegates to be added
    /// in C# and removed in Python.
    /// </summary>
    interface IDelegateConvertible {
        Delegate ConvertToDelegate(Type/*!*/ type);
    }
}
