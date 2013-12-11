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
using System.Text;

namespace IronPython {
    /// <summary>
    /// Marks that the return value of a function might include NotImplemented.
    /// 
    /// This is added to an operator method to ensure that all necessary methods are called
    /// if one cannot guarantee that it can perform the comparison.
    /// </summary>
    [AttributeUsage(AttributeTargets.ReturnValue)]
    public sealed class MaybeNotImplementedAttribute : Attribute {
    }
}
