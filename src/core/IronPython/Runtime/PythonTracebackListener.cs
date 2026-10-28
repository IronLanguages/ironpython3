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

        internal PythonTracebackListener(PythonContext pythonContext, object traceObject, bool isDummyListener) {
            _pythonContext = pythonContext;
            IsDummyListener = isDummyListener;

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

        // dummy listener used to maintain the frame stack on a thread without a trace function
        internal bool IsDummyListener { get; }

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

                    // Module code has no frame push of its own: when exec or a script runner runs it, the caller has just pushed an entry
                    // for it. That entry gets the new frame, rather than a second entry being pushed for the same code.
                    bool pushedByCaller = IsPushedByCaller(thread, properties.Code);

                    pyFrame = new TraceBackFrame(
                        this,
                        properties.Code,
                        GetFrame(thread, pushedByCaller ? thread.Count - 2 : thread.Count - 1),
                        properties,
                        scopeCallback,
                        pushedByCaller
                    );

                    if (pushedByCaller) {
                        FunctionStack entry = thread[thread.Count - 1];
                        thread[thread.Count - 1] = new FunctionStack(entry.Context, entry.Code, pyFrame);
                    } else {
                        thread.Add(new FunctionStack(pyFrame));
                    }

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

                if (traceDispatchObject != null && !_exceptionThrown && !IsDummyListener) {
                    DispatchTrace(thread, kind, payload, traceDispatch, traceDispatchObject, pyFrame);
                }
            } finally {
                if (kind == Debugging.TraceEventKind.FrameExit && thread.Count > 0) {
                    // don't pop frames we didn't push, nor an entry that the caller of module code pushed (the caller pops it)
                    FunctionStack top = thread[thread.Count - 1];
                    if (top.Code == ((PythonDebuggingPayload)customPayload).Code && top.Frame is not { PushedByCaller: true }) {
                        thread.RemoveAt(thread.Count - 1);
                    }
                }
            }            
        }

        #endregion


        /// <summary>
        ///   Determines whether the entry on top of the frame stack was pushed for <paramref name="code"/> by the caller of that code.
        /// </summary>
        /// <remarks>
        ///   Module code has no frame push of its own, so <c>exec</c> and the script runners push an entry for it just before running it.
        ///   No frame object has been created for that entry yet, since no code has run in between.
        /// </remarks>
        private static bool IsPushedByCaller(List<FunctionStack> thread, FunctionCode code) {
            if (!code.IsModule || thread.Count == 0) {
                return false;
            }

            FunctionStack top = thread[thread.Count - 1];
            return top.Code == code && top.Frame is null;
        }


        /// <summary>
        ///   Gets the frame object of the frame stack entry at <paramref name="index"/>, creating it if needed.
        /// </summary>
        /// <remarks>
        ///   An entry pushed by code that is not traced has no frame object until one is asked for.
        ///   Creating it here gives the frame of traced code called from there its <see cref="TraceBackFrame.f_back"/>.
        /// </remarks>
        private static TraceBackFrame GetFrame(List<FunctionStack> thread, int index) {
            if (index < 0) {
                return null;
            }

            return thread[index].Frame ?? SysModule._getframeImpl(thread[index].Context, thread.Count - 1 - index, thread);
        }

        
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
