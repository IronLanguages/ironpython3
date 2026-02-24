// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Threading.Tasks;

using Microsoft.Scripting.Runtime;

using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Types;

namespace IronPython.Runtime {
    /// <summary>
    /// Provides an __await__ protocol wrapper for <see cref="Task"/>,
    /// enabling <c>await task</c> from Python async code.
    /// Blocks on the task and returns None via StopIteration.
    /// </summary>
    [PythonType("task_awaitable")]
    public sealed class TaskAwaitable {
        private readonly Task _task;

        internal TaskAwaitable(Task task) {
            _task = task;
        }

        public TaskAwaitable __await__() => this;

        public TaskAwaitable __iter__() => this;

        [LightThrowing]
        public object __next__() {
            _task.GetAwaiter().GetResult();
            return LightExceptions.Throw(new PythonExceptions._StopIteration().InitAndGetClrException());
        }
    }

    /// <summary>
    /// Provides an __await__ protocol wrapper for <see cref="Task{T}"/>,
    /// enabling <c>result = await task</c> from Python async code.
    /// Blocks on the task and returns the result via StopIteration.value.
    /// </summary>
    [PythonType("task_awaitable")]
    public sealed class TaskAwaitable<T> {
        private readonly Task<T> _task;

        internal TaskAwaitable(Task<T> task) {
            _task = task;
        }

        public TaskAwaitable<T> __await__() => this;

        public TaskAwaitable<T> __iter__() => this;

        [LightThrowing]
        public object __next__() {
            T result = _task.GetAwaiter().GetResult();
            return LightExceptions.Throw(new PythonExceptions._StopIteration().InitAndGetClrException(result!));
        }
    }

#if NET
    /// <summary>
    /// Provides an __await__ protocol wrapper for <see cref="ValueTask"/>,
    /// enabling <c>await valuetask</c> from Python async code.
    /// </summary>
    [PythonType("task_awaitable")]
    public sealed class ValueTaskAwaitable {
        private readonly ValueTask _task;

        internal ValueTaskAwaitable(ValueTask task) {
            _task = task;
        }

        public ValueTaskAwaitable __await__() => this;

        public ValueTaskAwaitable __iter__() => this;

        [LightThrowing]
        public object __next__() {
            _task.AsTask().GetAwaiter().GetResult();
            return LightExceptions.Throw(new PythonExceptions._StopIteration().InitAndGetClrException());
        }
    }

    /// <summary>
    /// Provides an __await__ protocol wrapper for <see cref="ValueTask{T}"/>,
    /// enabling <c>result = await valuetask</c> from Python async code.
    /// </summary>
    [PythonType("task_awaitable")]
    public sealed class ValueTaskAwaitable<T> {
        private readonly ValueTask<T> _task;

        internal ValueTaskAwaitable(ValueTask<T> task) {
            _task = task;
        }

        public ValueTaskAwaitable<T> __await__() => this;

        public ValueTaskAwaitable<T> __iter__() => this;

        [LightThrowing]
        public object __next__() {
            T result = _task.AsTask().GetAwaiter().GetResult();
            return LightExceptions.Throw(new PythonExceptions._StopIteration().InitAndGetClrException(result!));
        }
    }
#endif
}
