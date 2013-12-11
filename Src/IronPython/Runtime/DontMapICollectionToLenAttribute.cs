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

namespace IronPython.Runtime {
    /// <summary>
    /// Marks a type so that IronPython will not expose the ICollection interface out as
    /// __len__.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    sealed class DontMapICollectionToLenAttribute : Attribute {
    }
}
