// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

#if FEATURE_PROCESS

[assembly: PythonModule("signal", typeof(IronPython.Modules.PythonSignal))]
namespace IronPython.Modules {
    public static partial class PythonSignal {
        public const string __doc__ = @"This module provides mechanisms to use signal handlers in Python.

Functions:

signal() -- set the action for a given signal
getsignal() -- get the signal action for a given signal
default_int_handler() -- default SIGINT handler

signal constants:
SIG_DFL -- used to refer to the system default handler
SIG_IGN -- used to ignore the signal
NSIG -- number of defined signals
SIGINT, SIGTERM, etc. -- signal numbers

*** IMPORTANT NOTICE ***
A signal handler function is called with two arguments:
the first is the signal number, the second is the interrupted stack frame.";

        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, PythonDictionary/*!*/ dict) {
            context.SetModuleState(_PythonSignalStateKey, MakeSignalState(context));
        }

        private static PythonSignalState MakeSignalState(PythonContext context) {
            if (Environment.OSVersion.Platform == PlatformID.Unix
                || Environment.OSVersion.Platform == PlatformID.MacOSX) {
                return MakePosixSignalState(context);
            } else {
                return MakeNtSignalState(context);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static PythonSignalState MakeNtSignalState(PythonContext context) {
            Debug.Assert(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
            return new NtSignalState(context);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static PythonSignalState MakePosixSignalState(PythonContext context) {
            // Use SimpleSignalState until the real Posix one is written
            return new SimpleSignalState(context);
        }

        //Python signals
        public const int NSIG = 23;
        public const int SIGABRT = 22;
        public const int SIGBREAK = 21;
        public const int SIGFPE = 8;
        public const int SIGILL = 4;
        public const int SIGINT = 2;
        public const int SIGSEGV = 11;
        public const int SIGTERM = 15;
        public const int SIG_DFL = 0;
        public const int SIG_IGN = 1;

        //Windows signals
        [SupportedOSPlatform("windows"), PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        public const int CTRL_C_EVENT = 0;
        [SupportedOSPlatform("windows"), PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        public const int CTRL_BREAK_EVENT = 1;
        [SupportedOSPlatform("windows"), PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        public const int CTRL_CLOSE_EVENT = 2;
        [SupportedOSPlatform("windows"), PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        public const int CTRL_LOGOFF_EVENT = 5;
        [SupportedOSPlatform("windows"), PythonHidden(PlatformsAttribute.PlatformFamily.Unix)]
        public const int CTRL_SHUTDOWN_EVENT = 6;

        public static BuiltinFunction default_int_handler = BuiltinFunction.MakeFunction("default_int_handler",
                    ArrayUtils.ConvertAll(typeof(PythonSignal).GetMember("default_int_handlerImpl"), (x) => (MethodBase)x),
                    typeof(PythonSignal)
                );
        //This must be kept public, but hidden from Python for the __doc__ member to show up on default_int_handler
        [PythonHidden]
        [Documentation(@"default_int_handler(...)

The default handler for SIGINT installed by Python.
It raises KeyboardInterrupt.")]
        public static object default_int_handlerImpl(int signalnum, TraceBackFrame frame) {
            throw new KeyboardInterruptException("");
        }

        [Documentation(@"getsignal(sig) -> action

Return the current action for the given signal.  The return value can be:
SIG_IGN -- if the signal is being ignored
SIG_DFL -- if the default action for the signal is in effect
None -- if an unknown handler is in effect
anything else -- the callable Python object used as a handler")]
        public static object getsignal(CodeContext/*!*/ context, int signalnum) {
            lock (GetPythonSignalState(context).PySignalToPyHandler) {
                //Negative Scenarios
                if (signalnum < 1 || signalnum > 22) {
                    throw PythonOps.ValueError("signal number out of range");
                } else if (GetPythonSignalState(context).PySignalToPyHandler.TryGetValue(signalnum, out object value)) {
                    //Default
                    return value;
                } else {
                    //Handles the special case of SIG_IGN. This is not really a signal,
                    //but CPython returns null for it any ways
                    return null;
                }
            }
        }

        [Documentation(@"signal(sig, action) -> action

Set the action for the given signal.  The action can be SIG_DFL,
SIG_IGN, or a callable Python object.  The previous action is
returned.  See getsignal() for possible return values.

*** IMPORTANT NOTICE ***
A signal handler function is called with two arguments:
the first is the signal number, the second is the interrupted stack frame.")]
        public static object signal(CodeContext/*!*/ context, int sig, object action) {
            //Negative scenarios - sig
            if (sig < 1 || sig >= NSIG) {
                throw PythonOps.ValueError("signal number out of range");
            } else if (Array.IndexOf(_PySupportedSignals, sig) == -1) {
                throw new RuntimeException("no IronPython support for given signal");
            }
            //Negative scenarios - action
            if (action == null) {
                throw PythonOps.TypeError("signal handler must be signal.SIG_IGN, signal.SIG_DFL, or a callable object");
            } else if (action.GetType() == typeof(int)) {
                int tempAction = (int)action;
                if (tempAction != SIG_DFL && tempAction != SIG_IGN) {
                    throw PythonOps.TypeError("signal handler must be signal.SIG_IGN, signal.SIG_DFL, or a callable object");
                }
            } else if (action == default_int_handler) {
                //no-op
            } else {
                //Must match the signature of PySignalHandler
                PythonFunction result = action as PythonFunction;
                if (result == null) {
                    //It could still be something like a type that implements __call__
                    if (!PythonOps.IsCallable(context, action)) {
                        throw PythonOps.TypeError("signal handler must be signal.SIG_IGN, signal.SIG_DFL, or a callable object");
                    }
                }
            }

            object last_handler = null;
            lock (GetPythonSignalState(context).PySignalToPyHandler) {
                //CPython returns the previous handler for the signal
                last_handler = getsignal(context, sig);
                //Set the new action
                GetPythonSignalState(context).PySignalToPyHandler[sig] = action;
            }

            return last_handler;
        }

        [Documentation(@"NOT YET IMPLEMENTED

set_wakeup_fd(fd) -> fd

Sets the fd to be written to (with '\0') when a signal
comes in.  A library can use this to wakeup select or poll.
The previous fd is returned.

The fd must be non-blocking.")]
        public static void set_wakeup_fd(CodeContext/*!*/ context, uint fd) {
            throw new NotImplementedException(); //TODO
        }

        private static readonly object _PythonSignalStateKey = new object();

        private static PythonSignalState GetPythonSignalState(CodeContext/*!*/ context) {
            return (PythonSignalState)context.LanguageContext.GetModuleState(_PythonSignalStateKey);
        }

        private static void SetPythonSignalState(CodeContext/*!*/ context, PythonSignalState pss) {
            context.LanguageContext.SetModuleState(_PythonSignalStateKey, pss);
        }

        internal class PythonSignalState {
            //this provides us with access to the Main thread's stack
            public PythonContext SignalPythonContext;

            //Map out signal identifiers to their actual handlers
            public Dictionary<int, object> PySignalToPyHandler;

            public PythonSignalState(PythonContext pc) {
                SignalPythonContext = pc;
                PySignalToPyHandler = new Dictionary<int, object>() {
                    { SIGABRT, SIG_DFL},
                    { SIGBREAK, SIG_DFL},
                    { SIGFPE, SIG_DFL},
                    { SIGILL, SIG_DFL},
                    { SIGINT, default_int_handler},
                    { SIGSEGV, SIG_DFL},
                    { SIGTERM, SIG_DFL},
                };
            }
        }

        //List of all Signals CPython supports on Windows.  Notice the addition of '6'
        private static readonly int[] _PySupportedSignals = { SIGABRT, SIGBREAK, SIGFPE, SIGILL, SIGINT, SIGSEGV, SIGTERM, 6 };

        //Signature of Python functions that signal.signal(...) expects to be given
        private delegate object PySignalHandler(int signalnum, TraceBackFrame frame);
    }
}

#endif
