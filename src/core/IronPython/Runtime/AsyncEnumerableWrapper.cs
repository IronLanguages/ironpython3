// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

// IAsyncEnumerable<T> / IAsyncEnumerator<T> require .NET Core 3.0+
#if NET

#nullable enable

using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

namespace IronPython.Runtime {
    /// <summary>
    /// Wraps <see cref="IAsyncEnumerator{T}"/> to implement the Python
    /// async iterator protocol (__aiter__, __anext__).
    /// Returned by <see cref="InstanceOps.AsyncIterMethod{T}"/>.
    /// </summary>
    [PythonType("async_enumerator_wrapper")]
    public sealed class AsyncEnumeratorWrapper<T> {
        private readonly IAsyncEnumerator<T> _enumerator;

        internal AsyncEnumeratorWrapper(IAsyncEnumerator<T> enumerator) {
            _enumerator = enumerator;
        }

        public AsyncEnumeratorWrapper<T> __aiter__() => this;

        /// <summary>
        /// Returns an awaitable that, when awaited, advances the async enumerator
        /// and returns the next value or raises StopAsyncIteration.
        /// </summary>
        public object __anext__() {
            return new AsyncEnumeratorAwaitable<T>(_enumerator);
        }
    }

    /// <summary>
    /// The awaitable object returned by <see cref="AsyncEnumeratorWrapper{T}.__anext__"/>.
    /// Implements both __await__ and __iter__/__next__ (the yield-from protocol) to
    /// block on MoveNextAsync and return the current value via StopIteration.
    /// </summary>
    [PythonType("async_enumerator_awaitable")]
    public sealed class AsyncEnumeratorAwaitable<T> {
        private readonly IAsyncEnumerator<T> _enumerator;

        internal AsyncEnumeratorAwaitable(IAsyncEnumerator<T> enumerator) {
            _enumerator = enumerator;
        }

        public AsyncEnumeratorAwaitable<T> __await__() => this;

        public AsyncEnumeratorAwaitable<T> __iter__() => this;

        [LightThrowing]
        public object __next__() {
            bool hasNext = _enumerator.MoveNextAsync().AsTask().GetAwaiter().GetResult();
            if (!hasNext) {
                return LightExceptions.Throw(
                    new PythonExceptions._StopAsyncIteration().InitAndGetClrException());
            }
            return LightExceptions.Throw(
                new PythonExceptions._StopIteration().InitAndGetClrException(_enumerator.Current!));
        }
    }
}

#endif
