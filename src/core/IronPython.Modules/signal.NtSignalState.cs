// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

using IronPython.Runtime;

#if FEATURE_PROCESS

namespace IronPython.Modules {
    public static partial class PythonSignal {
        [SupportedOSPlatform("windows")]
        private class NtSignalState : PythonSignalState {
            // We use a single Windows event handler to process all signals. This handler simply
            // delegates the work out to PySignalToPyHandler.
            public NativeWindowsSignal.WinSignalsHandler WinAllSignalsHandlerDelegate;


            public NtSignalState(PythonContext pc) : base(pc) {
                WinAllSignalsHandlerDelegate = new NativeWindowsSignal.WinSignalsHandler(WindowsEventHandler);
                NativeWindowsSignal.SetConsoleCtrlHandler(this.WinAllSignalsHandlerDelegate, true);
            }


            // Our implementation of WinSignalsHandler
            private bool WindowsEventHandler(uint winSignal) {
                bool retVal;
                int pySignal;

                switch (winSignal) {
                    case CTRL_C_EVENT:
                        pySignal = SIGINT;
                        break;
                    case CTRL_BREAK_EVENT:
                        pySignal = SIGBREAK;
                        break;
                    case CTRL_CLOSE_EVENT:
                        pySignal = SIGBREAK;
                        break;
                    case CTRL_LOGOFF_EVENT:
                        pySignal = SIGBREAK;
                        break;
                    case CTRL_SHUTDOWN_EVENT:
                        pySignal = SIGBREAK;
                        break;
                    default:
                        throw new InvalidOperationException("unreachable");
                }
                object? handler = PySignalToPyHandler[pySignal];

                if (handler is int tempId) {
                    if (tempId == SIG_DFL) {
                        // SIG_DFL - we let Windows do whatever it normally would
                        retVal = false;
                    } else if (tempId == SIG_IGN) {
                        // SIG_IGN - we do nothing, but tell Windows we handled the signal
                        retVal = true;
                    } else {
                        throw new InvalidOperationException("unreachable");
                    }
                } else if (handler == default_int_handler && pySignal == SIGINT) {
                    // Let the real interrupt handler throw a KeyboardInterrupt for SIGINT.
                    // It handles this far more gracefully than we can
                    retVal = false;
                } else {
                    // We're dealing with a callable matching PySignalHandler's signature
                    CallPythonHandler(pySignal, handler);
                    retVal = true;
                }

                return retVal;
            }
        }


        [SupportedOSPlatform("windows")]
        internal static class NativeWindowsSignal {
            // Windows API expects to be given a function pointer like this to handle signals
            internal delegate bool WinSignalsHandler(uint winSignal);

            [DllImport("Kernel32")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool SetConsoleCtrlHandler(WinSignalsHandler Handler, [MarshalAs(UnmanagedType.Bool)]bool Add);

            [DllImport("Kernel32")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);
        }
    }
}

#endif
