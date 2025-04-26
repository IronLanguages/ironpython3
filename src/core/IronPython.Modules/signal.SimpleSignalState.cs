// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

#if FEATURE_PROCESS

using System;
using System.Runtime.InteropServices;

using IronPython.Runtime;

namespace IronPython.Modules {
    public static partial class PythonSignal {
        private class SimpleSignalState : PythonSignalState {
            private readonly ConsoleCancelEventHandler? _consoleHandler;

            public SimpleSignalState(PythonContext pc) : base(pc) {
                Console.CancelKeyPress += new ConsoleCancelEventHandler(Console_CancelKeyPress);
                if (pc.Console is Microsoft.Scripting.Hosting.Shell.BasicConsole console) {
                    // in console hosting scenarios, we need to override the console handler of Ctrl+C
                    _consoleHandler = console.ConsoleCancelEventHandler;
                    console.ConsoleCancelEventHandler = null;
                }
            }


            private void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e) {
                int pySignal = !RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? SIGINT
                    : e.SpecialKey switch {
                        ConsoleSpecialKey.ControlC => SIGINT,
                        ConsoleSpecialKey.ControlBreak => SIGBREAK,
                        _ => throw new InvalidOperationException("unreachable"),
                    };

                object? handler = PySignalToPyHandler[pySignal];

                if (handler is int tempId) {
                    if (tempId == SIG_DFL) {
                        // SIG_DFL - do whatever it normally would
                        return;
                    } else if (tempId == SIG_IGN) {
                        // SIG_IGN - we do nothing, but tell the OS we handled the signal
                        e.Cancel = true;
                        return;
                    } else {
                        throw new InvalidOperationException("unreachable");
                    }
                } else if (ReferenceEquals(handler, default_int_handler) && pySignal == SIGINT) {
                    // Forward the signal to the console handler, if any
                    _consoleHandler?.Invoke(sender, e);
                    return;
                } else {
                    CallPythonHandler(pySignal, handler);
                    e.Cancel = true;
                    return;
                }
            }
        }
    }
}

#endif
