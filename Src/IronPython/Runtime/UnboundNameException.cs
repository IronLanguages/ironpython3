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
using System.Runtime.Serialization;

namespace IronPython.Runtime {
    [Serializable]
    public class UnboundNameException : Exception {
        public UnboundNameException() : base() { }
        public UnboundNameException(string msg) : base(msg) { }
        public UnboundNameException(string message, Exception innerException)
            : base(message, innerException) {
        }
#if FEATURE_SERIALIZATION
        protected UnboundNameException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif
    }

    [Serializable]
    public class UnboundLocalException : UnboundNameException {
        public UnboundLocalException() : base() { }
        public UnboundLocalException(string msg) : base(msg) { }
        public UnboundLocalException(string message, Exception innerException)
            : base(message, innerException) {
        }
#if FEATURE_SERIALIZATION
        protected UnboundLocalException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif
    }
}
