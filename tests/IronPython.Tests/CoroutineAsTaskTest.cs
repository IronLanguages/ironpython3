// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.Threading.Tasks;

using IronPython.Hosting;
using IronPython.Runtime;

using Microsoft.Scripting.Hosting;

using NUnit.Framework;

namespace IronPythonTest {
    public class CoroutineAsTaskTest {
        private readonly ScriptEngine _engine;
        private readonly ScriptScope _scope;

        public CoroutineAsTaskTest() {
            _engine = Python.CreateEngine();
            _scope = _engine.CreateScope();
        }

        [Test]
        public void AsTask_SimpleReturn() {
            _engine.Execute(@"
async def foo():
    return 42
coro = foo()
", _scope);

            var coro = (PythonCoroutine)_scope.GetVariable("coro");
            var result = coro.AsTask().GetAwaiter().GetResult();
            Assert.That(result, Is.EqualTo(42));
        }

        [Test]
        public void AsTask_StringReturn() {
            _engine.Execute(@"
async def foo():
    return 'hello'
coro = foo()
", _scope);

            var coro = (PythonCoroutine)_scope.GetVariable("coro");
            var result = coro.AsTask().GetAwaiter().GetResult();
            Assert.That(result, Is.EqualTo("hello"));
        }

        [Test]
        public void AsTask_AwaitCompletedTask() {
            _engine.Execute(@"
from System.Threading.Tasks import Task
async def foo():
    val = await Task.FromResult(10)
    return val + 5
coro = foo()
", _scope);

            var coro = (PythonCoroutine)_scope.GetVariable("coro");
            var result = coro.AsTask().GetAwaiter().GetResult();
            Assert.That(result, Is.EqualTo(15));
        }

        [Test]
        public void AsTask_AwaitRealAsync() {
            _engine.Execute(@"
from System.Threading.Tasks import Task
async def foo():
    await Task.Delay(50)
    return 'delayed'
coro = foo()
", _scope);

            var coro = (PythonCoroutine)_scope.GetVariable("coro");
            var result = coro.AsTask().GetAwaiter().GetResult();
            Assert.That(result, Is.EqualTo("delayed"));
        }

        [Test]
        public async Task AsTask_CanBeAwaited() {
            _engine.Execute(@"
from System.Threading.Tasks import Task
async def foo():
    await Task.Delay(50)
    return 99
coro = foo()
", _scope);

            var coro = (PythonCoroutine)_scope.GetVariable("coro");
            var result = await coro.AsTask();
            Assert.That(result, Is.EqualTo(99));
        }

        [Test]
        public void AsTask_PropagatesException() {
            _engine.Execute(@"
async def foo():
    raise ValueError('boom')
coro = foo()
", _scope);

            var coro = (PythonCoroutine)_scope.GetVariable("coro");
            Assert.Throws<AggregateException>(() => coro.AsTask().Wait());
        }

        [Test]
        public void AsTask_MultipleAwaits() {
            _engine.Execute(@"
from System.Threading.Tasks import Task
async def foo():
    a = await Task.FromResult(10)
    b = await Task.FromResult(20)
    return a + b
coro = foo()
", _scope);

            var coro = (PythonCoroutine)_scope.GetVariable("coro");
            var result = coro.AsTask().GetAwaiter().GetResult();
            Assert.That(result, Is.EqualTo(30));
        }

        [Test]
        public void AsTask_NoneReturn() {
            _engine.Execute(@"
async def foo():
    pass
coro = foo()
", _scope);

            var coro = (PythonCoroutine)_scope.GetVariable("coro");
            var result = coro.AsTask().GetAwaiter().GetResult();
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task DirectAwait_Simple() {
            _engine.Execute(@"
async def foo():
    return 42
coro = foo()
", _scope);

            var coro = (PythonCoroutine)_scope.GetVariable("coro");
            var result = await coro;
            Assert.That(result, Is.EqualTo(42));
        }

        [Test]
        public async Task DirectAwait_WithRealAsync() {
            _engine.Execute(@"
from System.Threading.Tasks import Task
async def foo():
    await Task.Delay(50)
    return 'done'
coro = foo()
", _scope);

            var coro = (PythonCoroutine)_scope.GetVariable("coro");
            var result = await coro;
            Assert.That(result, Is.EqualTo("done"));
        }
    }
}

#endif
