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
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Operations;

[module: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", Scope = "member", Target = "IronPython.Runtime.Exceptions.TraceBackFrame..ctor(System.Object,System.Object,System.Object)", MessageId = "0#globals")]
[module: System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", Scope = "member", Target = "IronPython.Runtime.Exceptions.TraceBackFrame.Globals", MessageId = "Globals")]

namespace IronPython.Runtime.Exceptions {
    [PythonType("traceback")]
    [Serializable]
    public class TraceBack {
        private readonly TraceBack _next;
        private readonly TraceBackFrame _frame;
        private int _line;

        public TraceBack(TraceBack nextTraceBack, TraceBackFrame fromFrame) {
            _next = nextTraceBack;
            _frame = fromFrame;
        }

        public TraceBack tb_next {
            get {
                return _next;
            }
        }

        public TraceBackFrame tb_frame {
            get {
                return _frame;
            }
        }

        public int tb_lineno {
            get {
                return _line;
            }
        }

        public int tb_lasti {
            get {
                return 0;   // not presently tracked
            }
        }

        internal void SetLine(int lineNumber) {
            _line = lineNumber;
        }

        /// <summary>
        /// returns string containing human readable representation of traceback
        /// </summary>
        internal string Extract() {
            var sb = new StringBuilder();
            var tb = this;
            while (tb != null) {
                var f = tb._frame;
                var lineno = tb._line;
                var co = f.f_code;
                var filename = co.co_filename;
                var name = co.co_name;
                sb.AppendFormat("  File \"{0}\", line {1}, in {2}{3}", filename, lineno, name, Environment.NewLine);
                tb = tb._next;
            }
            return sb.ToString();
        }
    }

    [PythonType("frame")]
    [DebuggerDisplay("Code = {f_code.co_name}, Line = {f_lineno}")]
    [Serializable]
    public class TraceBackFrame {
        private readonly PythonTracebackListener _traceAdapter;
        private TracebackDelegate _trace;
        private object _traceObject;
        internal int _lineNo;
        private readonly PythonDebuggingPayload _debugProperties;
        private readonly Func<IDictionary<object, object>> _scopeCallback;

        private readonly PythonDictionary _globals;
        private readonly object _locals;
        private readonly FunctionCode _code;
        private readonly CodeContext/*!*/ _context;
        private readonly TraceBackFrame _back;

        internal TraceBackFrame(CodeContext/*!*/ context, PythonDictionary globals, object locals, FunctionCode code) {
            _globals = globals;
            _locals = locals;
            _code = code;
            _context = context;
        }

        internal TraceBackFrame(CodeContext/*!*/ context, PythonDictionary globals, object locals, FunctionCode code, TraceBackFrame back) {
            _globals = globals;
            _locals = locals;
            _code = code;
            _context = context;
            _back = back;
        }

        internal TraceBackFrame(PythonTracebackListener traceAdapter, FunctionCode code, TraceBackFrame back, PythonDebuggingPayload debugProperties, Func<IDictionary<object, object>> scopeCallback) {
            _traceAdapter = traceAdapter;
            _code = code;
            _back = back;
            _debugProperties = debugProperties;
            _scopeCallback = scopeCallback;
        }

        [SpecialName, PropertyMethod]
        public object Getf_trace() {
                if (_traceAdapter != null) {
                    return _traceObject;
                } else {
                    return null;
                }
        }

        [SpecialName, PropertyMethod]
        public void Setf_trace(object value) {
            _traceObject = value;
            _trace = (TracebackDelegate)Converter.ConvertToDelegate(value, typeof(TracebackDelegate));
        }

        [SpecialName, PropertyMethod]
        public void Deletef_trace() {
            Setf_trace(null);
        }

        public void clear() {
            // TODO: add actual implementation of clear
        }

        internal CodeContext Context {
            get {
                return _context;
            }
        }

        internal TracebackDelegate TraceDelegate {
            get {
                if (_traceAdapter != null) {
                    return _trace;
                } else {
                    return null;
                }
            }
        }

        public PythonDictionary f_globals {
            get {
                object context;
                if (_scopeCallback != null &&
                    _scopeCallback().TryGetValue(Compiler.Ast.PythonAst.GlobalContextName, out context) && context != null) {
                    return ((CodeContext)context).GlobalDict;
                } else {
                    return _globals;
                }
            }
        }

        public object f_locals {
            get {
                if (_traceAdapter != null && _scopeCallback != null) {
                    if (_code.IsModule) {
                        // don't use the scope callback for locals in a module, use our globals dictionary instead
                        return f_globals;
                    }

                    return new PythonDictionary(new DebuggerDictionaryStorage(_scopeCallback()));
                } else {
                    return _locals;
                }
            }
        }

        public FunctionCode f_code {
            get {
                return _code;
            }
        }

        public object f_builtins {
            get {
                return PythonContext.GetContext(_context).BuiltinModuleDict;
            }
        }

        public TraceBackFrame f_back {
            get {
                return _back;
            }
        }

        public object f_exc_traceback {
            get {
                return null;
            }
        }

        public object f_exc_type {
            get {
                return null;
            }
        }

        public bool f_restricted {
            get {
                return false;
            }
        }

        public object f_lineno {
            get {
                if (_traceAdapter != null) {
                    return _lineNo;
                } else {
                    return 1;
                }
            }
            set {
                if (!(value is int)) {
                    throw PythonOps.ValueError("lineno must be an integer");
                }

                SetLineNumber((int)value);
            }
        }

        private void SetLineNumber(int newLineNum) {
            var pyThread = PythonOps.GetFunctionStackNoCreate();
            if (_traceAdapter == null || !IsTopMostFrame(pyThread)) {
                throw PythonOps.ValueError("f_lineno can only be set by a trace function on the topmost frame");
            }

            FunctionCode funcCode = _debugProperties.Code;
            Dictionary<int, Dictionary<int, bool>> loopAndFinallyLocations = _debugProperties.LoopAndFinallyLocations;
            Dictionary<int, bool> handlerLocations = _debugProperties.HandlerLocations;

            Dictionary<int, bool> currentLoopIds = null;
            bool inForLoopOrFinally = loopAndFinallyLocations != null && loopAndFinallyLocations.TryGetValue(_lineNo, out currentLoopIds);
            
            int originalNewLine = newLineNum;

            if (newLineNum < funcCode.Span.Start.Line) {
                throw PythonOps.ValueError("line {0} comes before the current code block", newLineNum);
            } else if (newLineNum > funcCode.Span.End.Line) {
                throw PythonOps.ValueError("line {0} comes after the current code block", newLineNum);
            }


            while (newLineNum <= funcCode.Span.End.Line) {
                var span = new SourceSpan(new SourceLocation(0, newLineNum, 1), new SourceLocation(0, newLineNum, Int32.MaxValue));

                // Check if we're jumping onto a handler
                bool handlerIsFinally;
                if (handlerLocations != null && handlerLocations.TryGetValue(newLineNum, out handlerIsFinally)) {
                    throw PythonOps.ValueError("can't jump to 'except' line");                    
                }

                // Check if we're jumping into a for-loop
                Dictionary<int, bool> jumpIntoLoopIds;
                if (loopAndFinallyLocations != null && loopAndFinallyLocations.TryGetValue(newLineNum, out jumpIntoLoopIds)) {
                    // If we're not in any loop already - then we can't jump into a loop
                    if (!inForLoopOrFinally) {
                        throw BadForOrFinallyJump(newLineNum, jumpIntoLoopIds);
                    }

                    // If we're in loops - we can only jump if we're not entering a new loop
                    foreach (int jumpIntoLoopId in jumpIntoLoopIds.Keys) {
                        if (!currentLoopIds.ContainsKey(jumpIntoLoopId)) {
                            throw BadForOrFinallyJump(newLineNum, currentLoopIds);
                        }
                    }
                } else if (currentLoopIds != null) {
                    foreach (bool isFinally in currentLoopIds.Values) {
                        if (isFinally) {
                            throw PythonOps.ValueError("can't jump out of 'finally block'");
                        }
                    }
                }

                if (_traceAdapter.PythonContext.TracePipeline.TrySetNextStatement(_code.co_filename, span)) {
                    _lineNo = newLineNum;
                    return;
                }

                ++newLineNum;
            }

            throw PythonOps.ValueError("line {0} is invalid jump location ({1} - {2} are valid)", originalNewLine, funcCode.Span.Start.Line, funcCode.Span.End.Line);
        }

        private bool IsTopMostFrame(List<FunctionStack> pyThread) {
            return pyThread != null && pyThread.Count != 0 && pyThread[pyThread.Count - 1].Frame == this;
        }

        private static Exception BadForOrFinallyJump(int newLineNum, Dictionary<int, bool> jumpIntoLoopIds) {
            foreach (bool isFinally in jumpIntoLoopIds.Values) {
                if (isFinally) {
                    return PythonOps.ValueError("can't jump into 'finally block'", newLineNum);
                }
            }
            return PythonOps.ValueError("can't jump into 'for loop'", newLineNum);
        }
    }

    public delegate TracebackDelegate TracebackDelegate(TraceBackFrame frame, string result, object payload);
}
