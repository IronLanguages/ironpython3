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

#if !FEATURE_APPLICATIONEXCEPTION
using System;

namespace IronPython.Runtime.Exceptions {
    [Serializable]
    public class ApplicationException : Exception {
        public ApplicationException() : base() { }
        public ApplicationException(string msg) : base(msg) { }
        public ApplicationException(string message, Exception innerException)
            : base(message, innerException) {
        }
    }
}

#endif
