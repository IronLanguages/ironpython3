/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

using System;
using System.Runtime.InteropServices;
using IronPython.Runtime;

#if FEATURE_PROCESS

namespace IronPython.Modules {
    public static partial class PythonSignal {
        internal class NtSignalState : PythonSignalState {
            //We use a single Windows event handler to process all signals. This handler simply
            //delegates the work out to PySignalToPyHandler.
            public NativeSignal.WinSignalsHandler WinAllSignalsHandlerDelegate;

            public NtSignalState(PythonContext pc) : base(pc) {
                WinAllSignalsHandlerDelegate = new NativeSignal.WinSignalsHandler(WindowsEventHandler);
                NativeSignal.SetConsoleCtrlHandler(this.WinAllSignalsHandlerDelegate, true);
            }

            //Our implementation of WinSignalsHandler
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
                        throw new Exception("unreachable");
                }

                lock (PySignalToPyHandler) {
                    if (PySignalToPyHandler[pySignal].GetType() == typeof(int)) {
                        int tempId = (int)PySignalToPyHandler[pySignal];

                        if (tempId == SIG_DFL) {
                            //SIG_DFL - we let Windows do whatever it normally would
                            retVal = false;
                        } else if (tempId == SIG_IGN) {
                            //SIG_IGN - we do nothing, but tell Windows we handled the signal
                            retVal = true;
                        } else {
                            throw new Exception("unreachable");
                        }
                    } else if (PySignalToPyHandler[pySignal] == default_int_handler) {
                        if (pySignal != SIGINT) {
                            //We're dealing with the default_int_handlerImpl which we
                            //know doesn't care about the frame parameter
                            retVal = true;
                            default_int_handlerImpl(pySignal, null);
                        } else {
                            //Let the real interrupt handler throw a KeyboardInterrupt for SIGINT.
                            //It handles this far more gracefully than we can
                            retVal = false;
                        }
                    } else {
                        //We're dealing with a callable matching PySignalHandler's signature
                        retVal = true;
                        PySignalHandler temp = (PySignalHandler)Converter.ConvertToDelegate(PySignalToPyHandler[pySignal],
                                                                                            typeof(PySignalHandler));

                        try {
                            if (SignalPythonContext.PythonOptions.Frames) {
                                temp.Invoke(pySignal, SysModule._getframeImpl(null,
                                                                              0,
                                                                              SignalPythonContext._mainThreadFunctionStack));
                            } else {
                                temp.Invoke(pySignal, null);
                            }
                        } catch (Exception e) {
                            System.Console.WriteLine(SignalPythonContext.FormatException(e));
                        }
                    }
                }

                return retVal;
            }
        }

        internal static class NativeSignal {
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