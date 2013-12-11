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
    /// For IList<byte/> arguments: Marks that the argument is typed to accept a bytes or
    /// bytearray object.  This attribute disallows passing a Python list object and
    /// auto-applying our generic conversion.  It also enables conversion of a string to
    /// a IList of byte in IronPython 2.6.
    /// 
    /// For string arguments: Marks that the argument is typed to accept a bytes object
    /// as well. (2.6 only)
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class BytesConversionAttribute : Attribute {
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class BytesConversionNoStringAttribute : Attribute {
    }
}
