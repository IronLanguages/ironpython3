// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using IronPython.Runtime.Operations;
using Microsoft.Scripting.Runtime;

namespace IronPython.Runtime.Exceptions {
    /// <summary>
    /// .NET exception that is thrown to signal the end of iteration in Python
    /// </summary>

    [Serializable]
    public class StopIterationException : Exception, IPythonAwareException {
        private object _pyExceptionObject;
        private List<DynamicStackFrame> _frames;
        private TraceBack _traceback;

        public StopIterationException() : base() { }
        public StopIterationException(string msg) : base(msg) { }
        public StopIterationException(string message, Exception innerException)
            : base(message, innerException) {
        }
#if FEATURE_SERIALIZATION
        protected StopIterationException(SerializationInfo info, StreamingContext context) : base(info, context) { }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2123:OverrideLinkDemandsShouldBeIdenticalToBase")]
        public override void GetObjectData(SerializationInfo info, StreamingContext context) {
            info.AddValue("frames", _frames);
            info.AddValue("traceback", _traceback);
            base.GetObjectData(info, context);
        }
#endif

        object IPythonAwareException.PythonException {
            get {
                if (_pyExceptionObject == null) {
                    var newEx = new PythonExceptions._StopIteration();
                    newEx.InitializeFromClr(this);
                    _pyExceptionObject = newEx;
                }
                return _pyExceptionObject;
            }
            set { _pyExceptionObject = value; }
        }

        List<DynamicStackFrame> IPythonAwareException.Frames {
            get { return _frames; }
            set { _frames = value; }
        }

        TraceBack IPythonAwareException.TraceBack {
            get { return _traceback; }
            set { _traceback = value; }
        }

        /// <summary>
        /// Result of raise StopError(n)
        /// </summary>
        /// <returns>
        /// value passed to StopError, or null if none
        /// </returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1007:UseGenericsWhereAppropriate")]
        [PythonHidden]
        public object GetValue() {
            object pyObj = PythonExceptions.ToPython(this);

            PythonTuple t;
            if (!PythonOps.TryGetBoundAttr(pyObj, "args", out object args) ||
                (t = args as PythonTuple) == null || t.__len__() == 0) {
                return null;
            }

            return t[0];
        }
    }
}
