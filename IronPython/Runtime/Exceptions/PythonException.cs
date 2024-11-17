// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime.Exceptions {
    [Serializable]
    internal class PythonException : Exception, IPythonAwareException {
        private PythonExceptions.BaseException? _pyExceptionObject;
        private List<DynamicStackFrame>? _frames;
        private TraceBack? _traceback;

        public PythonException() : base() { }
        public PythonException(string msg) : base(msg) { }
        public PythonException(string message, Exception innerException)
            : base(message, innerException) {
        }
#if FEATURE_SERIALIZATION
        protected PythonException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif

        PythonExceptions.BaseException? IPythonAwareException.PythonException {
            get {
                return _pyExceptionObject;
            }
            set {
                _pyExceptionObject = value;
            }
        }

        List<DynamicStackFrame>? IPythonAwareException.Frames {
            get { return _frames; }
            set { _frames = value; }
        }

        TraceBack? IPythonAwareException.TraceBack {
            get { return _traceback; }
            set { _traceback = value; }
        }
    }

    internal interface IPythonAwareException {
        PythonExceptions.BaseException? PythonException { get; set; }
        List<DynamicStackFrame>? Frames { get; set; }
        TraceBack? TraceBack { get; set; }
    }
}
