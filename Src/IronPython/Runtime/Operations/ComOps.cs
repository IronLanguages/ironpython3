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

#if !SILVERLIGHT

using System;
using System.Collections.Generic;
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using IronPython.Runtime.Binding;
using IronPython.Runtime.Types;
using System.Runtime.InteropServices;

namespace IronPython.Runtime.Operations {
    public static class ComOps {
        public static string __str__(object/*!*/ self) {
            return self.ToString();
        }

        public static string/*!*/ __repr__(object/*!*/ self) {
            return String.Format("<{0} object at {1}>",
                self.ToString(),
                PythonOps.HexId(self)
            );
        }
    }
}

#endif