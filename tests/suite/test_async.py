# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

"""Tests for PEP 492: async/await support."""

import unittest

from iptest import run_test, skipUnlessIronPython, is_netcoreapp
from iptest.ipunittest import load_ironpython_test


def run_coro(coro):
    """Run a coroutine to completion, blocking on yielded .NET Tasks."""
    value = None
    while True:
        try:
            task = coro.send(value)
            # .NET Task yielded — block on it (test runner is synchronous)
            task.Wait()
            value = None
        except StopIteration as e:
            return e.value


class AsyncIter:
    """Async iterator for testing async for."""
    def __init__(self, items):
        self.items = list(items)
        self.index = 0

    def __aiter__(self):
        return self

    async def __anext__(self):
        if self.index >= len(self.items):
            raise StopAsyncIteration
        val = self.items[self.index]
        self.index += 1
        return val


class AsyncDefTest(unittest.TestCase):
    """Tests for basic async def and coroutine type."""

    def test_basic_return(self):
        async def foo():
            return 42
        self.assertEqual(run_coro(foo()), 42)

    def test_coroutine_type(self):
        async def foo():
            return 1
        coro = foo()
        self.assertEqual(type(coro).__name__, 'coroutine')
        try:
            coro.send(None)
        except StopIteration:
            pass

    def test_coroutine_properties(self):
        async def named_coro():
            return 1
        coro = named_coro()
        self.assertTrue(hasattr(coro, 'cr_code'))
        self.assertTrue(hasattr(coro, 'cr_running'))
        self.assertTrue(hasattr(coro, 'cr_frame'))
        self.assertTrue(hasattr(coro, '__name__'))
        self.assertTrue(hasattr(coro, '__qualname__'))
        self.assertEqual(coro.__name__, 'named_coro')
        try:
            coro.send(None)
        except StopIteration:
            pass

    def test_async_def_no_await(self):
        async def foo():
            x = 1
            y = 2
            return x + y
        self.assertEqual(run_coro(foo()), 3)

    def test_coroutine_close(self):
        async def foo():
            return 1
        coro = foo()
        coro.close()  # should not raise

    def test_coroutine_throw(self):
        async def foo():
            return 42
        coro = foo()
        with self.assertRaises(ValueError) as cm:
            coro.throw(ValueError('boom'))
        self.assertEqual(str(cm.exception), 'boom')


class AwaitTest(unittest.TestCase):
    """Tests for await expression."""

    def test_basic_await(self):
        async def inner():
            return 10

        async def outer():
            val = await inner()
            return val + 5

        self.assertEqual(run_coro(outer()), 15)

    def test_multiple_awaits(self):
        async def val(x):
            return x

        async def test():
            return await val(1) + await val(2) + await val(3)

        self.assertEqual(run_coro(test()), 6)

    def test_await_protocol(self):
        async def foo():
            return 99
        coro = foo()
        wrapper = coro.__await__()
        self.assertEqual(type(wrapper).__name__, 'coroutine_wrapper')
        try:
            wrapper.__next__()
        except StopIteration as e:
            self.assertEqual(e.value, 99)

    def test_custom_awaitable(self):
        class MyAwaitable:
            def __await__(self):
                return iter([])

        async def test():
            await MyAwaitable()
            return 'done'

        self.assertEqual(run_coro(test()), 'done')


class AsyncWithTest(unittest.TestCase):
    """Tests for async with statement."""

    def test_basic_async_with(self):
        class CM:
            def __init__(self):
                self.entered = False
                self.exited = False
            async def __aenter__(self):
                self.entered = True
                return self
            async def __aexit__(self, *args):
                self.exited = True

        async def test():
            cm = CM()
            async with cm:
                self.assertTrue(cm.entered)
            self.assertTrue(cm.exited)
            return 'ok'

        self.assertEqual(run_coro(test()), 'ok')

    def test_async_with_as(self):
        class CM:
            async def __aenter__(self):
                return 'value'
            async def __aexit__(self, *args):
                pass

        async def test():
            async with CM() as v:
                return v

        self.assertEqual(run_coro(test()), 'value')

    def test_async_with_order(self):
        class CM:
            def __init__(self):
                self.log = []
            async def __aenter__(self):
                self.log.append('enter')
                return self
            async def __aexit__(self, *args):
                self.log.append('exit')

        async def test():
            cm = CM()
            async with cm:
                cm.log.append('body')
            return cm.log

        self.assertEqual(run_coro(test()), ['enter', 'body', 'exit'])

    def test_async_with_no_as(self):
        class CM:
            async def __aenter__(self):
                return 'unused'
            async def __aexit__(self, *args):
                pass

        async def test():
            async with CM():
                return 'ok'

        self.assertEqual(run_coro(test()), 'ok')


class AsyncForTest(unittest.TestCase):
    """Tests for async for statement."""

    def test_basic_async_for(self):
        async def test():
            result = []
            async for x in AsyncIter([1, 2, 3]):
                result.append(x)
            return result

        self.assertEqual(run_coro(test()), [1, 2, 3])

    def test_async_for_empty(self):
        async def test():
            result = []
            async for x in AsyncIter([]):
                result.append(x)
            return result

        self.assertEqual(run_coro(test()), [])

    def test_async_for_else(self):
        async def test():
            result = []
            async for x in AsyncIter([]):
                result.append(x)
            else:
                result.append('else')
            return result

        self.assertEqual(run_coro(test()), ['else'])

    def test_async_for_else_on_completion(self):
        async def test():
            result = []
            async for x in AsyncIter([1, 2]):
                result.append(x)
            else:
                result.append('else')
            return result

        self.assertEqual(run_coro(test()), [1, 2, 'else'])

    def test_async_for_break(self):
        async def test():
            result = []
            async for x in AsyncIter([1, 2, 3, 4, 5]):
                if x == 3:
                    break
                result.append(x)
            return result

        self.assertEqual(run_coro(test()), [1, 2])

    def test_async_for_break_skips_else(self):
        async def test():
            result = []
            async for x in AsyncIter([1, 2, 3]):
                if x == 2:
                    break
                result.append(x)
            else:
                result.append('else')
            return result

        self.assertEqual(run_coro(test()), [1])

    def test_async_for_continue(self):
        async def test():
            result = []
            async for x in AsyncIter([1, 2, 3, 4, 5]):
                if x % 2 == 0:
                    continue
                result.append(x)
            return result

        self.assertEqual(run_coro(test()), [1, 3, 5])

    def test_nested_async_for(self):
        async def test():
            result = []
            async for x in AsyncIter([1, 2]):
                async for y in AsyncIter([10, 20]):
                    result.append(x * 100 + y)
            return result

        self.assertEqual(run_coro(test()), [110, 120, 210, 220])

    def test_special_method_lookup(self):
        """Ensure async for looks up __aiter__/__anext__ on the type, not the instance."""

        a = AsyncIter([1, 2, 3])
        a.__aiter__ = lambda: AsyncIter([98])  # should be ignored
        a.__anext__ = lambda: 99  # should be ignored

        async def test():
            result = []
            async for x in a:
                result.append(x)
            return result

        self.assertEqual(run_coro(test()), [1, 2, 3])

class AsyncCombinedTest(unittest.TestCase):
    """Tests combining async with and async for."""

    def test_async_with_and_for(self):
        class CM:
            def __init__(self):
                self.log = []
            async def __aenter__(self):
                self.log.append('enter')
                return self
            async def __aexit__(self, *args):
                self.log.append('exit')

        async def test():
            cm = CM()
            async with cm:
                async for x in AsyncIter([1, 2]):
                    cm.log.append(x)
            return cm.log

        self.assertEqual(run_coro(test()), ['enter', 1, 2, 'exit'])


@skipUnlessIronPython()
class DotNetAsyncInteropTest(unittest.TestCase):
    """Tests for .NET async interop (await Task, async for IAsyncEnumerable, CancelledError)."""

    def test_await_completed_task(self):
        """await a Task that is already completed (Task.CompletedTask)."""
        from System.Threading.Tasks import Task
        async def test():
            await Task.CompletedTask
            return 'done'
        self.assertEqual(run_coro(test()), 'done')

    def test_await_task_delay(self):
        """await Task.Delay -a real async .NET operation."""
        from System.Threading.Tasks import Task
        async def test():
            await Task.Delay(10)
            return 'delayed'
        self.assertEqual(run_coro(test()), 'delayed')

    def test_await_task_from_result(self):
        """await Task<T>.FromResult should return the value."""
        from System.Threading.Tasks import Task
        async def test():
            result = await Task.FromResult(42)
            return result
        self.assertEqual(run_coro(test()), 42)

    def test_await_task_from_result_string(self):
        """await Task<string>.FromResult should return the string."""
        from System.Threading.Tasks import Task
        async def test():
            result = await Task.FromResult("hello")
            return result
        self.assertEqual(run_coro(test()), "hello")

    def test_await_multiple_tasks(self):
        """Multiple awaits in sequence."""
        from System.Threading.Tasks import Task
        async def test():
            a = await Task.FromResult(10)
            b = await Task.FromResult(20)
            c = await Task.FromResult(30)
            return a + b + c
        self.assertEqual(run_coro(test()), 60)

    def test_task_has_await(self):
        """Task objects should have __await__ method."""
        from System.Threading.Tasks import Task
        task = Task.FromResult(99)
        self.assertTrue(hasattr(task, '__await__'))

    def test_task_awaitable_protocol(self):
        """Task.__await__() should return an iterable that raises StopIteration(value)."""
        from System.Threading.Tasks import Task
        task = Task.FromResult(42)
        awaitable = task.__await__()
        it = iter(awaitable)
        try:
            next(it)
            self.fail("Expected StopIteration")
        except StopIteration as e:
            self.assertEqual(e.value, 42)

    def test_await_faulted_task(self):
        """Awaiting a faulted task should propagate the exception."""
        from System import Exception as DotNetException
        from System.Threading.Tasks import Task
        async def test():
            await Task.FromException(DotNetException("boom"))
        with self.assertRaises(DotNetException):
            run_coro(test())

    def test_cancelled_error_from_cancelled_task(self):
        """Awaiting a cancelled task should raise CancelledError."""
        from System.Threading import CancellationTokenSource
        from System.Threading.Tasks import Task
        cts = CancellationTokenSource()
        cts.Cancel()
        async def test():
            await Task.FromCanceled(cts.Token)
        with self.assertRaises(CancelledError):
            run_coro(test())

    def test_cancelled_error_type(self):
        """CancelledError should be a subclass of Exception."""
        self.assertTrue(issubclass(CancelledError, Exception))

    def test_operation_cancelled_maps_to_cancelled_error(self):
        """System.OperationCanceledException should map to CancelledError."""
        from System import OperationCanceledException
        try:
            raise OperationCanceledException("test cancel")
        except CancelledError:
            pass  # expected

    def test_cancellation_token_cancel(self):
        """CancellationToken can be used with .NET async APIs."""
        from System.Threading import CancellationTokenSource
        cts = CancellationTokenSource()
        token = cts.Token
        self.assertFalse(token.IsCancellationRequested)
        cts.Cancel()
        self.assertTrue(token.IsCancellationRequested)


@unittest.skipUnless(is_netcoreapp, "ValueTask is not available on .NET Framework")
class ValueTaskInteropTest(unittest.TestCase):
    def test_await_valuetask(self):
        """await a ValueTask (non-generic)."""
        from System.Threading.Tasks import ValueTask
        async def test():
            await ValueTask.CompletedTask
            return 'done'
        self.assertEqual(run_coro(test()), 'done')

    def test_await_valuetask_generic(self):
        """await a ValueTask<T> should return the value."""
        from System.Threading.Tasks import ValueTask
        async def test():
            vt = ValueTask[int](42)
            result = await vt
            return result
        self.assertEqual(run_coro(test()), 42)

    def test_valuetask_has_await(self):
        """ValueTask should have __await__ method."""
        from System.Threading.Tasks import ValueTask
        vt = ValueTask.CompletedTask
        self.assertTrue(hasattr(vt, '__await__'))

    def test_valuetask_generic_has_await(self):
        """ValueTask<T> should have __await__ method."""
        from System.Threading.Tasks import ValueTask
        vt = ValueTask[str]("hello")
        self.assertTrue(hasattr(vt, '__await__'))

    def test_await_valuetask_string(self):
        """await a ValueTask<string>."""
        from System.Threading.Tasks import ValueTask
        async def test():
            vt = ValueTask[str]("world")
            return await vt
        self.assertEqual(run_coro(test()), "world")


import sys
if sys.implementation.name == 'ironpython':
    import clr
    load_ironpython_test()
    try:
        from IronPythonTest import AsyncInteropHelpers
        _has_async_helpers = True
    except Exception:
        _has_async_helpers = False
else:
    _has_async_helpers = False


@unittest.skipUnless(_has_async_helpers, "requires IronPythonTest with AsyncInteropHelpers")
class DotNetAsyncEnumerableTest(unittest.TestCase):
    """Tests for async for over .NET IAsyncEnumerable<T>."""

    def test_async_for_ints(self):
        """async for over IAsyncEnumerable<int>."""
        async def test():
            result = []
            async for x in AsyncInteropHelpers.GetAsyncInts(1, 2, 3):
                result.append(x)
            return result
        self.assertEqual(run_coro(test()), [1, 2, 3])

    def test_async_for_strings(self):
        """async for over IAsyncEnumerable<string>."""
        async def test():
            result = []
            async for s in AsyncInteropHelpers.GetAsyncStrings("a", "b", "c"):
                result.append(s)
            return result
        self.assertEqual(run_coro(test()), ["a", "b", "c"])

    def test_async_for_empty(self):
        """async for over empty IAsyncEnumerable."""
        async def test():
            result = []
            async for x in AsyncInteropHelpers.GetEmptyAsyncInts():
                result.append(x)
            return result
        self.assertEqual(run_coro(test()), [])

    def test_async_for_break(self):
        """break inside async for over IAsyncEnumerable."""
        async def test():
            result = []
            async for x in AsyncInteropHelpers.GetAsyncInts(10, 20, 30, 40, 50):
                if x == 30:
                    break
                result.append(x)
            return result
        self.assertEqual(run_coro(test()), [10, 20])

    def test_async_for_has_aiter(self):
        """IAsyncEnumerable<T> objects should have __aiter__."""
        stream = AsyncInteropHelpers.GetAsyncInts(1)
        self.assertTrue(hasattr(stream, '__aiter__'))


@unittest.skipUnless(_has_async_helpers, "requires IronPythonTest with AsyncInteropHelpers")
class DotNetRealAsyncTaskTest(unittest.TestCase):
    """Tests for await on real async Task<T> methods.

    These test real .NET async methods where the runtime type is
    AsyncStateMachineBox<TResult, TStateMachine>, not Task<T> directly.
    All methods include real delays (Task.Delay) to ensure truly async behavior.
    """

    def test_await_real_async_int(self):
        """await a real async Task<int> with delay."""
        async def test():
            return await AsyncInteropHelpers.GetAsyncInt(42)
        self.assertEqual(run_coro(test()), 42)

    def test_await_real_async_string(self):
        """await a real async Task<string> with delay."""
        async def test():
            return await AsyncInteropHelpers.GetAsyncString("hello")
        self.assertEqual(run_coro(test()), "hello")

    def test_await_real_async_void(self):
        """await a real async Task (void) with delay."""
        async def test():
            await AsyncInteropHelpers.DoAsync()
            return 'done'
        self.assertEqual(run_coro(test()), 'done')

    def test_await_real_async_multiple(self):
        """Multiple awaits on real async Task<T> in sequence."""
        async def test():
            a = await AsyncInteropHelpers.GetAsyncInt(10)
            b = await AsyncInteropHelpers.GetAsyncInt(20)
            s = await AsyncInteropHelpers.GetAsyncString("!")
            return str(a + b) + s
        self.assertEqual(run_coro(test()), "30!")

    def test_await_real_async_mixed_with_python(self):
        """Mix real .NET async with Python coroutines."""
        async def py_double(x):
            return x * 2

        async def test():
            val = await AsyncInteropHelpers.GetAsyncInt(5)
            doubled = await py_double(val)
            return doubled
        self.assertEqual(run_coro(test()), 10)


@unittest.skipUnless(_has_async_helpers, "requires IronPythonTest with AsyncInteropHelpers")
class DotNetCancellationTest(unittest.TestCase):
    """Tests for CancellationToken and CancelledError with .NET async methods."""

    def test_cancel_async_task_int(self):
        """Cancelling a Task<int> should raise CancelledError."""
        from System.Threading import CancellationTokenSource
        cts = CancellationTokenSource()
        cts.Cancel()
        async def test():
            return await AsyncInteropHelpers.GetAsyncIntWithCancellation(42, cts.Token)
        with self.assertRaises(CancelledError):
            run_coro(test())

    def test_cancel_async_task_void(self):
        """Cancelling a Task (void) should raise CancelledError."""
        from System.Threading import CancellationTokenSource
        cts = CancellationTokenSource()
        cts.Cancel()
        async def test():
            await AsyncInteropHelpers.DoAsyncWithCancellation(cts.Token)
        with self.assertRaises(CancelledError):
            run_coro(test())

    def test_cancel_async_enumerable(self):
        """Cancelling during async for over IAsyncEnumerable should raise CancelledError."""
        from System.Threading import CancellationTokenSource
        cts = CancellationTokenSource()
        cts.Cancel()
        async def test():
            result = []
            async for x in AsyncInteropHelpers.GetAsyncIntsWithCancellation(cts.Token, 1, 2, 3):
                result.append(x)
            return result
        with self.assertRaises(CancelledError):
            run_coro(test())

    def test_cancelled_error_is_exception_subclass(self):
        """CancelledError should be a subclass of Exception."""
        self.assertTrue(issubclass(CancelledError, Exception))

    def test_cancelled_error_catch_as_exception(self):
        """CancelledError should be catchable as Exception."""
        from System.Threading import CancellationTokenSource
        cts = CancellationTokenSource()
        cts.Cancel()
        async def test():
            try:
                await AsyncInteropHelpers.GetAsyncIntWithCancellation(99, cts.Token)
            except Exception:
                return 'caught'
        self.assertEqual(run_coro(test()), 'caught')

    def test_operation_cancelled_maps_to_cancelled_error(self):
        """System.OperationCanceledException raised directly should be catchable as CancelledError."""
        from System import OperationCanceledException
        caught = False
        try:
            raise OperationCanceledException("test")
        except CancelledError:
            caught = True
        self.assertTrue(caught)


run_test(__name__)
