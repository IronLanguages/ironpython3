// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

#if FEATURE_PROCESS
#if NET

using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

using Mono.Unix;
using Mono.Unix.Native;

using IronPython.Runtime;
using IronPython.Runtime.Operations;
using System.Threading.Tasks;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Hosting.Shell;

namespace IronPython.Modules {
    public static partial class PythonSignal {

        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        private class PosixSignalState : PythonSignalState {
            private readonly PosixSignalRegistration?[] _signalRegistrations;
            private ConsoleCancelEventHandler? _consoleHandler;


            public PosixSignalState(PythonContext pc) : base(pc) {
                _signalRegistrations = new PosixSignalRegistration[NSIG];
            }


            protected override void Dispose(bool disposing) {
                if (disposing) {
                    Array.ForEach(_signalRegistrations, reg => reg?.Dispose());
                    Array.Clear(_signalRegistrations, 0, _signalRegistrations.Length);
                }
                base.Dispose(disposing);
            }


            public override void SetPyHandler(int signalnum, object value) {
                // normalize handler reference
                if (value is int i) {
                    value = (i == SIG_IGN) ? sig_ign : sig_dfl;
                }

                if (TryGetPyHandler(signalnum, out object? existingValue) && ReferenceEquals(existingValue, value)) {
                    // no change
                    return;
                }

                base.SetPyHandler(signalnum, value);

                if (signalnum == SIGINT) {
                    // SIGINT is special, we need to disable the handler on the console so that we can handle Ctrl+C here
                    if (DefaultContext.DefaultPythonContext.Console is BasicConsole console) {
                        if (!ReferenceEquals(value, default_int_handler)) {
                            // save the console handler so it can be restored later if necessary
                            _consoleHandler ??= console.ConsoleCancelEventHandler;
                            // disable the console handler
                            console.ConsoleCancelEventHandler = null;
                        } else if (_consoleHandler != null) {
                            // default_int_handler: restore the console handler, which is de facto default_int_handler
                            console.ConsoleCancelEventHandler = _consoleHandler;
                            _consoleHandler = null;
                        }
                    }
                }

                // remember to unregister any previous handler
                var oldReg = _signalRegistrations[signalnum];

                if (ReferenceEquals(value, sig_dfl)) {
                    _signalRegistrations[signalnum] = null;
                } else if (ReferenceEquals(value, sig_ign)) {
                    _signalRegistrations[signalnum] = PosixSignalRegistration.Create((PosixSignal)signalnum,
                        (PosixSignalContext psc) => {
                            psc.Cancel = true;
                        });
                } else {
                    _signalRegistrations[signalnum] = PosixSignalRegistration.Create((PosixSignal)signalnum, 
                        (PosixSignalContext psc) => {
                            // This is called on a thread from the thread pool for most signals,
                            // and on a dedicated thread ".NET Signal Handler" for SIGINT, SIGQUIT, and SIGTERM.
                            CallPythonHandler(signalnum, value);
                            psc.Cancel = true;
                        });
                }
                oldReg?.Dispose();
            }
        }
    }
}

#endif  // NET
#endif  // FEATURE_PROCESS
