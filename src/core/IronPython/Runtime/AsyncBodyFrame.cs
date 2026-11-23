// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

#if FEATURE_NET_ASYNC

using System;
using System.Diagnostics;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Operations;

namespace IronPython.Runtime {
    /// <summary>
    ///   Keeps the frame-stack entry of an async function or async generator body on the thread that runs the body.
    /// </summary>
    /// <remarks>
    ///   <para>An async body can suspend on one thread and resume on another, so it can't pop its entry itself: it would pop it from the
    ///   thread it started on. The body hands its entry over when it starts (<see cref="PushFrame"/>). From then on the entry is on the frame
    ///   stack of the thread running the body, and on no thread's stack while the body is suspended. It observes the body's driver
    ///   (<see cref="AsyncHelpers.DriveAsync"/> or <see cref="AsyncHelpers.DriveAsyncEnumerable"/>), which calls it on the thread running
    ///   the body at each suspension and resumption, and when the body ends.</para>
    ///
    ///   <para>It stays inert for a body that doesn't push its own entry: with frames disabled, or when the body was compiled for tracing,
    ///   where the DLR's tracing state machine pushes and pops an entry at each resumption. This is decided when the body is compiled, so
    ///   it doesn't change when tracing is switched on or off while the body is suspended.</para>
    /// </remarks>
    public sealed class AsyncBodyFrame : IObserver<AsyncBodyEvent> {
        // The body's entry, from the moment the body pushes it; stays null for a body that doesn't push one.
        private FunctionStack? _entry;

        // Whether the entry is on the frame stack of the thread running the body.
        private bool _onStack;

        /// <summary>
        ///   Called by the body when it starts, to push its entry on the current thread's frame stack and hand the entry over.
        /// </summary>
        /// <remarks>
        ///   The body pushes the entry itself, rather than this frame at <see cref="AsyncBodyEventKind.Started"/>, because the entry needs the
        ///   body's own <see cref="CodeContext"/>, and whether the body pushes one at all is decided when it is compiled.
        /// </remarks>
        internal void PushFrame(CodeContext context, FunctionCode code) {
            Debug.Assert(_entry is null, "the body of an async function starts only once");

            // The entry never keeps a frame object, since the frame's f_back changes whenever the body resumes.
            var entry = new FunctionStack(context, code);
            PythonOps.GetFunctionStack().Add(entry);
            _entry = entry;
            _onStack = true;
        }

        void IObserver<AsyncBodyEvent>.OnNext(AsyncBodyEvent value) {
            switch (value.Kind) {
                case AsyncBodyEventKind.Awaiting:
                case AsyncBodyEventKind.Yielding:
                case AsyncBodyEventKind.Returning:
                    RemoveEntry();
                    break;

                case AsyncBodyEventKind.Resumed:
                    AddEntry();
                    break;
            }
        }

        // The body has raised. Also called when the body fails before it starts, or when an interrupt pending at its end fails it,
        // and then the entry isn't on the stack.
        void IObserver<AsyncBodyEvent>.OnError(Exception error) => RemoveEntry();

        // Comes after Returning, or after Yielding when the consumer abandons an async generator: the entry has been removed already.
        void IObserver<AsyncBodyEvent>.OnCompleted() { }


        private void AddEntry() {
            if (_entry is { } entry && !_onStack) {
                PythonOps.GetFunctionStack().Add(entry);
                _onStack = true;
            }
        }

        private void RemoveEntry() {
            if (_onStack) {
                _onStack = false;
                PythonOps.PopOwnFrame(_entry!.Value);
            }
        }
    }
}

#endif
