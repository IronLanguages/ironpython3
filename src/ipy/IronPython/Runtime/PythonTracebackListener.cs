// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Microsoft.Scripting;

using IronPython.Modules;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Debugging = Microsoft.Scripting.Debugging;

namespace IronPython.Runtime {
    internal sealed class PythonTracebackListener : Debugging.ITraceCallback {
        private readonly PythonContext _pythonContext;
        private object _traceObject;
        private TracebackDelegate _traceDispatch;
        private bool _inTraceBack;
        private bool _exceptionThrown;
        
#if PROFILE_SUPPORT
        private bool _profile;
#endif

        internal PythonTracebackListener(PythonContext pythonContext, object traceObject) {
            _pythonContext = pythonContext;

            if (traceObject != null) {
                _traceObject = traceObject;
                _traceDispatch = (TracebackDelegate)Converter.ConvertToDelegate(traceObject, typeof(TracebackDelegate));
            }
        }

        internal PythonContext PythonContext {
            get {
                return _pythonContext;
            }
        }

        internal object TraceObject {
            get {
                return _traceObject;
            }
        }

        internal bool InTraceBack {
            get {
                return _inTraceBack;
            }
            set {
                // primarily used for PythonContext.CallTracing
                _inTraceBack = value;
            }
        }

        internal bool ExceptionThrown {
            get {
                return _exceptionThrown;
            }
        }

#if PROFILE_SUPPORT
        internal void SetProfile(TracebackDelegate traceDispatch) {
            _traceDispatch = traceDispatch;
            _profile = true;
        }
#endif

        #region ITraceCallback Members

        public void OnTraceEvent(Debugging.TraceEventKind kind, string name, string sourceFileName, SourceSpan sourceSpan, Func<IDictionary<object, object>> scopeCallback, object payload, object customPayload) {        
            if (kind == Debugging.TraceEventKind.ThreadExit ||                  // We don't care about thread-exit events
#if PROFILE_SUPPORT
                (_profile && kind == Debugging.TraceEventKind.TracePoint) ||    // Ignore code execute tracebacks when in profile mode
#endif
                kind == Debugging.TraceEventKind.ExceptionUnwind) {             // and we always have a try/catch so we don't care about methods unwinding.
                return;
            }

            TracebackDelegate traceDispatch = null;
            object traceDispatchObject = null;
            var thread = PythonOps.GetFunctionStack();
            TraceBackFrame pyFrame;

            if (_inTraceBack) {
                return;
            }

            try {
                if (kind == Debugging.TraceEventKind.FrameEnter) {
                    traceDispatch = _traceDispatch;
                    traceDispatchObject = _traceObject;

                    var properties = (PythonDebuggingPayload)customPayload;

                    // push the new frame
                    pyFrame = new TraceBackFrame(
                        this,
                        properties.Code,
                        thread.Count == 0 ? null : thread[thread.Count - 1].Frame,
                        properties,
                        scopeCallback
                    );

                    thread.Add(new FunctionStack(pyFrame));

                    if (traceDispatchObject == null) {
                        return;
                    }
                    
                    pyFrame.Setf_trace(traceDispatchObject);
                } else {
                    if (thread.Count == 0) {
                        return;
                    }
                    pyFrame = thread[thread.Count - 1].Frame;
                    if (pyFrame == null) {
                        // force creation of the Python frame
                        pyFrame = SysModule._getframeImpl(thread[thread.Count - 1].Context, 0);
                    }
                    traceDispatch = pyFrame.TraceDelegate;
                    traceDispatchObject = pyFrame.Getf_trace();
                }

                // Update the current line
                if (kind != Debugging.TraceEventKind.FrameExit) {
                    pyFrame._lineNo = sourceSpan.Start.Line;
                }

                if (traceDispatchObject != null && !_exceptionThrown) {
                    DispatchTrace(thread, kind, payload, traceDispatch, traceDispatchObject, pyFrame);
                }
            } finally {
                if (kind == Debugging.TraceEventKind.FrameExit && thread.Count > 0) {
                    // don't pop frames we didn't push
                    if (thread[thread.Count - 1].Code == ((PythonDebuggingPayload)customPayload).Code) {
                        thread.RemoveAt(thread.Count - 1);
                    }
                }
            }            
        }

        #endregion

        
        private void DispatchTrace(List<FunctionStack> thread, Debugging.TraceEventKind kind, object payload, TracebackDelegate traceDispatch, object traceDispatchObject, TraceBackFrame pyFrame) {
            object args = null;

            // Prepare the event
            string traceEvent = String.Empty;
            switch (kind) {
                case Debugging.TraceEventKind.FrameEnter: traceEvent = "call"; break;
                case Debugging.TraceEventKind.TracePoint: traceEvent = "line"; break;
                case Debugging.TraceEventKind.Exception:
                    traceEvent = "exception";
                    object pyException = PythonExceptions.ToPython((Exception)payload);
                    object pyType = ((IPythonObject)pyException).PythonType;
                    args = PythonTuple.MakeTuple(pyType, pyException, new TraceBack(null, pyFrame));
                    break;
                case Debugging.TraceEventKind.FrameExit:
                    traceEvent = "return";
                    args = payload;
                    break;
            }

            bool traceDispatchThrew = true;
            _inTraceBack = true;
            try {
                TracebackDelegate dlg = traceDispatch(pyFrame, traceEvent, args);
                traceDispatchThrew = false;
                pyFrame.Setf_trace(dlg);
            } finally {
                _inTraceBack = false;
                if (traceDispatchThrew) {
                    // We're matching CPython's behavior here.  If the trace dispatch throws any exceptions
                    // we don't re-enable tracebacks.  We need to leave the trace callback in place though
                    // so that we can pop our frames.
                    _traceObject = _traceDispatch = null;
                    _exceptionThrown = true;
                }
            }
        }
    }
}
