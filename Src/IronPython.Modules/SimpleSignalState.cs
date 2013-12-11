using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IronPython.Runtime;

#if FEATURE_PROCESS

namespace IronPython.Modules {
    public static partial class PythonSignal {
        internal class SimpleSignalState : PythonSignalState {
            public SimpleSignalState(PythonContext pc)
                : base(pc) {
                Console.CancelKeyPress += new ConsoleCancelEventHandler(Console_CancelKeyPress);
            }

            void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e) {
                int pySignal;
                switch(e.SpecialKey) {
                    case ConsoleSpecialKey.ControlC:
                        pySignal = SIGINT;
                        break;

                    case ConsoleSpecialKey.ControlBreak:
                        pySignal = SIGBREAK;
                        break;

                    default:
                        throw new InvalidOperationException("unreachable");
                }
                
                lock (PySignalToPyHandler) {
                    if (PySignalToPyHandler[pySignal].GetType() == typeof(int)) {
                        int tempId = (int)PySignalToPyHandler[pySignal];

                        if (tempId == SIG_DFL) {
                            //SIG_DFL - do whatever it normally would
                            return;
                        } else if (tempId == SIG_IGN) {
                            //SIG_IGN - we do nothing, but tell the OS we handled the signal
                            e.Cancel = false;
                            return;
                        } else {
                            throw new Exception("unreachable");
                        }
                    } else if (PySignalToPyHandler[pySignal] == default_int_handler) {
                        if (pySignal != SIGINT) {
                            //We're dealing with the default_int_handlerImpl which we
                            //know doesn't care about the frame parameter
                            e.Cancel = true;
                            default_int_handlerImpl(pySignal, null);
                            return;
                        } else {
                            //Let the real interrupt handler throw a KeyboardInterrupt for SIGINT.
                            //It handles this far more gracefully than we can
                            return;
                        }
                    } else {
                        //We're dealing with a callable matching PySignalHandler's signature
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
                        } catch (Exception ex) {
                            System.Console.WriteLine(SignalPythonContext.FormatException(ex));
                        }

                        e.Cancel = true;
                        return;
                    }
                }
            }
        }
    }
}

#endif

