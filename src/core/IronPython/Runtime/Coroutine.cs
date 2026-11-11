// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime {

    [PythonType("coroutine")]
    [DontMapIDisposableToContextManager, DontMapIEnumerableToContains]
    public sealed class PythonCoroutine : ICodeFormattable, IWeakReferenceable {

#if FEATURE_NET_ASYNC

        // Under .NET async, `async def` is compiled directly to a Task<object?> via the DLR's AsyncExpression + AsyncHelpers.
        // PythonCoroutine is a wrapper over the Task. _interrupts is the AsyncInterruptSource passed into AsyncExpression;
        // throw(exc) interrupts the body with it.
        //
        // Lazy start: the body is not executed at construction. _factory is the thunk that creates (and thereby starts driving) the Task on
        // first access; _task caches the materialized Task. Calling an async def is therefore side-effect free until the coroutine is first
        // driven (send/AsTask/GetAwaiter) — PEP 492.
        private readonly Func<Task<object?>> _factory;
        private Task<object?>? _task;
        private readonly string _name;
        private readonly FunctionCode? _code;
        private readonly AsyncInterruptSource _interrupts;
        private WeakRefTracker? _tracker;

        internal PythonCoroutine(Func<Task<object?>> factory, string name, FunctionCode? code, AsyncInterruptSource interrupts) {
            _factory = factory;
            _name = name;
            _code = code;
            _interrupts = interrupts;
        }

        private Task<object?> EnsureTask() => _task ??= _factory();


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

            var task = EnsureTask();
            if (!task.IsCompleted) {
                // TODO: coroutines should be steppable via send(), one await at a time.
                ((IAsyncResult)task).AsyncWaitHandle.WaitOne();
            }

            return SettleCompletedTask(task);
        }


        [LightThrowing]
        public object @throw(object? value) => @throw(value, null, null);

        [LightThrowing]
        public object @throw(object? type, object? value) => @throw(type, value, null);

        /// <summary>
        ///   Throw an exception into the generator at the suspended <c>yield</c>. Returns an awaitable.
        /// </summary>
        /// <remarks>
        ///   When awaited the exception is rethrown at the resume point:
        ///   if the body catches it and yields again that value is produced;
        ///   otherwise the exception propagates (or StopAsyncIteration if the body finishes).
        ///   <br/>
        ///   In typical use, this is called with a single exception instance similar to the way the raise keyword is used.
        ///   <br/>
        ///   Changed in CPython 3.12: The signature (type[, value[, traceback]]) is deprecated
        ///   and may be removed in a future version of Python.
        /// </remarks>
        [LightThrowing]
        public object @throw(object? type, object? value, object? traceback) {
            // Validate up front so we don't mutate any state on bad input.
            if (PythonOps.ValidateThrowArguments(type, value) is Exception error) return LightExceptions.Throw(error);

            // Construct the actual exception object. DefaultContext.Default suffices here — throw()'s
            // CodeContext role is limited to resolving class-form exception construction, and the
            // coroutine's own context will re-bind the chain when the exception unwinds inside the body.
            Exception ex = PythonOps.MakeExceptionForGenerator(
                DefaultContext.Default, type, value, traceback, cause: null);

            // The body sees `ex` raised at the await it is suspended at, or at its next await if it is running.
            // If the body hasn't started, it doesn't run at all: its task fails with `ex`, as in CPython.
            if (!_interrupts.Interrupt(ex)) {
                // The coroutine has finished. throw() raises the exception immediately —
                // the body has nothing left to observe it. Matches CPython.
                return LightExceptions.Throw(ex);
            }

            // Wait for the body to settle. After this the task is in a terminal state — dispatch via
            // the same logic as send() so the body's reaction (catch-and-return, propagate, etc.) is
            // reflected back to the caller of throw().
            var task = EnsureTask();
            ((IAsyncResult)task).AsyncWaitHandle.WaitOne();
            return SettleCompletedTask(task);
        }


        private static object SettleCompletedTask(Task<object?> task) {
            if (task.IsCanceled) {
                // Surfaces as Python CancelledError via the OCE -> CancelledError mapping in PythonExceptions.
                return LightExceptions.Throw(new TaskCanceledException(task));
            }
            if (task.IsFaulted) {
                return LightExceptions.Throw(task.Exception?.InnerException ?? task.Exception);
            }
            return LightExceptions.Throw(new PythonExceptions._StopIteration().InitAndGetClrException(task.Result!));
        }


        [LightThrowing]
        public object? close() => null;

        public object __await__() => new CoroutineWrapper(this);

        public FunctionCode? cr_code => _code;

        // A not-yet-driven coroutine reports 0 (not running) without materializing the Task.
        // Merely inspecting cr_running must not start the body.
        public int cr_running => _task is null || _task.IsCompleted ? 0 : 1;

        public TraceBackFrame? cr_frame => null;

        public string __name__ => _name;

        public string __qualname__ => _name;

        /// <summary>Returns the underlying <see cref="Task{Object}"/>, starting the body on first call.</summary>
        public Task<object?> AsTask() => EnsureTask();

        /// <summary>Enables <c>await coroutine</c> from C# code.</summary>
        public TaskAwaiter<object?> GetAwaiter() => EnsureTask().GetAwaiter();

        internal Task<object?> Task => EnsureTask();

#else  // !FEATURE_NET_ASYNC

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
        // The signature (type[, value[, traceback]]) is deprecated in CPython 3.12
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

#endif  // FEATURE_NET_ASYNC - the rest of the members are shared between both implementations of PythonCoroutine

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
