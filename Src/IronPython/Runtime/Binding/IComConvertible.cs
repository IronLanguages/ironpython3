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
using System.Dynamic;
using System.Text;

namespace IronPython.Runtime.Binding {
    /// <summary>
    /// An interface that is implemented on DynamicMetaObjects.
    /// 
    /// This allows objects to opt-into custom conversions when calling
    /// COM APIs.  The IronPython binders all call this interface before
    /// doing any COM binding.
    /// </summary>
    interface IComConvertible {
        DynamicMetaObject GetComMetaObject();
    }
}
