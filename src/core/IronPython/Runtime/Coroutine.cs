// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime {
#if FEATURE_NET_ASYNC
    [PythonType("coroutine")]
    [DontMapIDisposableToContextManager, DontMapIEnumerableToContains]
    public sealed class PythonCoroutine : ICodeFormattable, IWeakReferenceable {
        // Under .NET async, `async def` is compiled directly to a Task<object?>
        // via the DLR's AsyncExpression + AsyncHelpers. PythonCoroutine is a
        // facade over the Task with two cancellation channels pre-bound at the
        // codegen level (see FunctionDefinition):
        //   _cts                  — CancellationTokenSource whose Token was passed
        //                            into AsyncExpression. throw(exc) cancels this.
        //   _cancellationException — StrongBox<Exception?> shared with DriveAsync's
        //                            `cancellationException` parameter; when non-null
        //                            at cancellation time, that exception is surfaced
        //                            to the body instead of OperationCanceledException.
        //                            throw(non-OCE) writes here before cancelling.
        private readonly Task<object?> _task;
        private readonly string _name;
        private readonly FunctionCode? _code;
        private readonly CancellationTokenSource _cts;
        private readonly StrongBox<Exception?> _cancellationException;
        private WeakRefTracker? _tracker;

        internal PythonCoroutine(Task<object?> task, string name, FunctionCode? code,
                                  CancellationTokenSource cts,
                                  StrongBox<Exception?> cancellationException) {
            _task = task;
            _name = name;
            _code = code;
            _cts = cts;
            _cancellationException = cancellationException;
        }

        [LightThrowing]
        public object send(object? value) {
            // Fast path: task already completed. Dispatch on terminal state without
            // any try/catch — Result on a Faulted/Canceled task throws, so we must
            // check IsCanceled / IsFaulted before reading it.
            //
            // Slow path: not completed. Block via AsyncWaitHandle.WaitOne() so we do
            // not pay for exception unwinding (GetAwaiter().GetResult() would observe
            // and rethrow cancel/fault). After waiting, the same terminal-state
            // dispatch below applies.
            if (!_task.IsCompleted) {
                ((IAsyncResult)_task).AsyncWaitHandle.WaitOne();
            }
            if (_task.IsCanceled) {
                // Surfaces as Python CancelledError via the OCE -> CancelledError mapping in PythonExceptions.
                // We need an OperationCanceledException instance because the Canceled task carries no Exception object.
                return LightExceptions.Throw(new TaskCanceledException(_task));
            }
            if (_task.IsFaulted) {
                return LightExceptions.Throw(_task.Exception?.InnerException ?? _task.Exception);
            }
            return LightExceptions.Throw(new PythonExceptions._StopIteration().InitAndGetClrException(_task.Result!));
        }

        [LightThrowing]
        public object @throw(object? type) => @throw(type, null, null);

        [LightThrowing]
        public object @throw(object? type, object? value) => @throw(type, value, null);

        [LightThrowing]
        public object @throw(object? type, object? value, object? traceback) {
            // Validate the shape of (type, value, traceback) up front. Mirrors PythonGenerator.@throw
            // so we don't mutate any state on bad input.
            if (type is Exception || type is PythonExceptions.BaseException) {
                if (value is not null)
                    return LightExceptions.Throw(PythonOps.TypeError("instance exception may not have a separate value"));
            } else if (type is PythonType pt && typeof(PythonExceptions.BaseException).IsAssignableFrom(pt.UnderlyingSystemType)) {
                // ok — class form, MakeExceptionForGenerator will construct.
            } else {
                return LightExceptions.Throw(PythonOps.TypeError(
                    "exceptions must be classes or instances deriving from BaseException, not {0}",
                    PythonOps.GetPythonTypeName(type)));
            }

            // Construct the actual exception object. DefaultContext.Default suffices here — throw()'s
            // CodeContext role is limited to resolving class-form exception construction, and the
            // coroutine's own context will re-bind the chain when the exception unwinds inside the body.
            Exception ex = PythonOps.MakeExceptionForGenerator(
                DefaultContext.Default, type, value, traceback, cause: null);

            if (_task.IsCompleted) {
                // Regime 1: coroutine has finished. throw() raises the exception immediately —
                // the body has nothing left to observe it. Matches CPython.
                return LightExceptions.Throw(ex);
            }

            // Regime 2: body is still running (or suspended at an await). Stash the exception in the
            // shared StrongBox so DriveAsync surfaces it (instead of OCE) the moment it observes the
            // cancellation, then cancel. The body sees `ex` rethrown at its next resume point via the
            // rewriter's per-await ExceptionDispatchInfo block.
            _cancellationException.Value = ex;
            _cts.Cancel();

            // Wait for the body to settle. After this the task is in a terminal state — dispatch via
            // the same logic as send() so the body's reaction (catch-and-return, propagate, etc.) is
            // reflected back to the caller of throw().
            ((IAsyncResult)_task).AsyncWaitHandle.WaitOne();
            return SettleCompletedTask();
        }

        private object SettleCompletedTask() {
            if (_task.IsCanceled) {
                return LightExceptions.Throw(new TaskCanceledException(_task));
            }
            if (_task.IsFaulted) {
                return LightExceptions.Throw(_task.Exception?.InnerException ?? _task.Exception);
            }
            return LightExceptions.Throw(new PythonExceptions._StopIteration().InitAndGetClrException(_task.Result!));
        }

        [LightThrowing]
        public object? close() => null;

        public object __await__() => new CoroutineWrapper(this);

        public FunctionCode? cr_code => _code;

        public int cr_running => _task.IsCompleted ? 0 : 1;

        public TraceBackFrame? cr_frame => null;

        public string __name__ => _name;

        public string __qualname__ => _name;

        /// <summary>Returns the underlying <see cref="Task{Object}"/>.</summary>
        public Task<object?> AsTask() => _task;

        /// <summary>Enables <c>await coroutine</c> from C# code.</summary>
        public TaskAwaiter<object?> GetAwaiter() => _task.GetAwaiter();

        internal Task<object?> Task => _task;
#else
    [PythonType("coroutine")]
    [DontMapIDisposableToContextManager, DontMapIEnumerableToContains]
    public sealed class PythonCoroutine : ICodeFormattable, IWeakReferenceable {
        private readonly PythonGenerator _generator;
        private WeakRefTracker? _tracker;

        internal PythonCoroutine(PythonGenerator generator) {
            _generator = generator;
        }

        [LightThrowing]
        public object send(object? value) {
            return _generator.send(value);
        }

        [LightThrowing]
        public object @throw(object? type) {
            return _generator.@throw(type);
        }

        [LightThrowing]
        public object @throw(object? type, object? value) {
            return _generator.@throw(type, value);
        }

        [LightThrowing]
        public object @throw(object? type, object? value, object? traceback) {
            return _generator.@throw(type, value, traceback);
        }

        [LightThrowing]
        public object? close() {
            return _generator.close();
        }

        public object __await__() {
            return new CoroutineWrapper(this);
        }

        public FunctionCode cr_code => _generator.gi_code;

        public int cr_running => _generator.gi_running;

        public TraceBackFrame cr_frame => _generator.gi_frame;

        public string __name__ => _generator.__name__;

        public string __qualname__ {
            get => _generator.__name__;
        }

        /// <summary>
        /// Converts this coroutine into a .NET <see cref="Task{Object}"/>,
        /// allowing C# code to <c>await</c> an IronPython async method.
        /// The coroutine is driven on a single thread to avoid issues with
        /// thread-local state in the Python generator runtime.
        /// </summary>
        public Task<object?> AsTask() {
            return Task.Run(() => {
                while (true) {
                    object result = send(null);

                    if (LightExceptions.IsLightException(result)) {
                        var clrExc = LightExceptions.GetLightException(result);
                        if (clrExc is StopIterationException) {
                            var pyExc = ((IPythonAwareException)clrExc).PythonException;
                            return pyExc is PythonExceptions._StopIteration si ? si.value : null;
                        }
                        throw clrExc;
                    }

                    if (result is Task task) {
                        task.Wait();
                    }
                }
            });
        }

        /// <summary>
        /// Enables <c>await coroutine</c> from C# code.
        /// </summary>
        public TaskAwaiter<object?> GetAwaiter() => AsTask().GetAwaiter();

        internal PythonGenerator Generator => _generator;
#endif

        #region ICodeFormattable Members

        public string __repr__(CodeContext context) {
            return $"<coroutine object {__name__} at {PythonOps.HexId(this)}>";
        }

        #endregion

        #region IWeakReferenceable Members

        WeakRefTracker? IWeakReferenceable.GetWeakRef() {
            return _tracker;
        }

        bool IWeakReferenceable.SetWeakRef(WeakRefTracker value) {
            _tracker = value;
            return true;
        }

        void IWeakReferenceable.SetFinalizer(WeakRefTracker value) {
            _tracker = value;
        }

        #endregion
    }

    [PythonType("coroutine_wrapper")]
    public sealed class CoroutineWrapper {
        private readonly PythonCoroutine _coroutine;

        internal CoroutineWrapper(PythonCoroutine coroutine) {
            _coroutine = coroutine;
        }

        [LightThrowing]
        public object __next__() {
            return _coroutine.send(null);
        }

        [LightThrowing]
        public object send(object? value) {
            return _coroutine.send(value);
        }

        [LightThrowing]
        public object @throw(object? type) {
            return _coroutine.@throw(type);
        }

        [LightThrowing]
        public object @throw(object? type, object? value) {
            return _coroutine.@throw(type, value);
        }

        [LightThrowing]
        public object @throw(object? type, object? value, object? traceback) {
            return _coroutine.@throw(type, value, traceback);
        }

        public object? close() {
            return _coroutine.close();
        }

        public CoroutineWrapper __iter__() {
            return this;
        }
    }
}
