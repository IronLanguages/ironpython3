// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

#if FEATURE_NET_ASYNC

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime {
    /// <summary>
    ///   PEP 525 async generator object — what an <c>async def</c> with <c>yield</c> returns.
    /// </summary>
    /// <remarks>
    ///   Wrapper over the DLR's <c>IAsyncEnumerable&lt;object&gt;</c> (produced by AsyncEnumerableExpression /
    ///   AsyncHelpers.DriveAsyncEnumerable) plus the channels that let the consumer drive it:
    ///   <list type="bullet">
    ///     <item><c>_sendSlot</c> — the value delivered into the body at the next <c>yield</c> resume
    ///       (<c>x = yield z</c>). Set by <see cref="asend"/> / <see cref="__anext__"/> before advancing.</item>
    ///     <item><c>_throwSlot</c> — an exception rethrown at the next <c>yield</c> resume (athrow/aclose).</item>
    ///     <item><c>_cts</c> — backs the underlying enumerator's cancellation.</item>
    ///   </list>
    /// </remarks>
    [PythonType("async_generator")]
    public sealed class PythonAsyncGenerator {
        private readonly IAsyncEnumerator<object?> _enumerator;
        private readonly StrongBox<object?> _sendSlot;
        private readonly StrongBox<Exception?> _throwSlot;
        private readonly CancellationTokenSource _cts;
        private readonly string _name;
        private bool _started;
        private bool _closed;

        internal PythonAsyncGenerator(IAsyncEnumerable<object?> source,
                                       StrongBox<object?> sendSlot,
                                       StrongBox<Exception?> throwSlot,
                                       CancellationTokenSource cts,
                                       string name) {
            _enumerator = source.GetAsyncEnumerator(cts.Token);
            _sendSlot = sendSlot;
            _throwSlot = throwSlot;
            _cts = cts;
            _name = name;
        }

        public PythonAsyncGenerator __aiter__() => this;


        /// <summary>
        ///   Advance the generator with no value sent in (equivalent to <c>asend(None)</c>).
        ///   Returns an awaitable yielding the next produced value or raising StopAsyncIteration.
        /// </summary>
        public object __anext__() {
            _sendSlot.Value = null;
            return new AsyncEnumeratorAwaitable<object?>(_enumerator);
        }


        /// <summary>
        ///   Send a value into the generator; it becomes the value of the <c>yield</c> the body is suspended
        ///   at (<c>x = yield z</c>). The first call after creation must send None (the body hasn't reached a
        ///   yield yet), matching CPython.
        /// </summary>
        public object asend(object? value) {
            if (!_started) {
                _started = true;
                if (value is not null) {
                    throw PythonOps.TypeError("can't send non-None value to a just-started async generator");
                }
            }
            _sendSlot.Value = value;
            return new AsyncEnumeratorAwaitable<object?>(_enumerator);
        }


        [LightThrowing]
        public object athrow(object? value) => athrow(value, null, null);

        [LightThrowing]
        public object athrow(object? type, object? value) => athrow(type, value, null);

        /// <summary>
        ///   Throw an exception into the generator at the suspended <c>yield</c>. Returns an awaitable.
        /// </summary>
        /// <remarks>
        ///   When awaited the exception is rethrown at the resume point:
        ///   if the body catches it and yields again that value is produced;
        ///   otherwise the exception propagates (or StopAsyncIteration if the body finishes).
        ///   <br/>
        ///   Changed in CPython 3.12: The signature (type[, value[, traceback]]) is deprecated
        ///   and may be removed in a future version of Python.
        /// </remarks>
        [LightThrowing]
        public object athrow(object? type, object? value, object? traceback) {
            // Validate shape (mirrors PythonGenerator/PythonCoroutine.throw).
            if (type is Exception || type is PythonExceptions.BaseException) {
                if (value is not null)
                    return LightExceptions.Throw(PythonOps.TypeError("instance exception may not have a separate value"));
            } else if (type is PythonType pt && typeof(PythonExceptions.BaseException).IsAssignableFrom(pt.UnderlyingSystemType)) {
                // ok — class form
            } else {
                return LightExceptions.Throw(PythonOps.TypeError(
                    "exceptions must be classes or instances deriving from BaseException, not {0}",
                    PythonOps.GetPythonTypeName(type)));
            }

            Exception ex = PythonOps.MakeExceptionForGenerator(DefaultContext.Default, type, value, traceback, cause: null);
            _started = true;
            _throwSlot.Value = ex;
            _sendSlot.Value = null;
            return new AsyncEnumeratorAwaitable<object?>(_enumerator);
        }

        /// <summary>
        ///   Close the generator: returns an awaitable that injects GeneratorExit at the suspended <c>yield</c>
        ///   (running the body's try/finally), then disposes the underlying async iterator.
        /// </summary>
        public object aclose() {
            return AcloseAsync();
        }

        private async Task<object?> AcloseAsync() {
            if (_closed) return null;
            _closed = true;
            if (_started) {
                _throwSlot.Value = new GeneratorExitException();
                _sendSlot.Value = null;
                try {
                    bool produced = await _enumerator.MoveNextAsync();
                    if (produced) {
                        throw PythonOps.RuntimeError("async generator ignored GeneratorExit");
                    }
                } catch (GeneratorExitException) {
                    // expected — the body let GeneratorExit propagate
                } catch (StopIterationException) {
                    // body finished — also fine
                }
            }
            await _enumerator.DisposeAsync();
            _cts.Cancel();
            return null;
        }

        public string __name__ => _name;

        public string __qualname__ => _name;

        public bool ag_running => _started && !_closed;
    }
}

#endif
