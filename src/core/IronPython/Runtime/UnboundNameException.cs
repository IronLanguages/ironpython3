// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

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
