// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

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
