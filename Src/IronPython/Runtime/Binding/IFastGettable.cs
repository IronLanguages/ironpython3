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

#if FEATURE_CORE_DLR
using System.Linq.Expressions;
#else
using Microsoft.Scripting.Ast;
#endif

using System;
using System.Dynamic;

using Microsoft.Scripting.Actions;

using Microsoft.Scripting.Runtime;
using System.Runtime.CompilerServices;

namespace IronPython.Runtime.Binding {
    interface IFastGettable {
        T MakeGetBinding<T>(CallSite<T> site, PythonGetMemberBinder/*!*/ binder, CodeContext/*!*/ state, string/*!*/ name) where T : class;
    }
}
