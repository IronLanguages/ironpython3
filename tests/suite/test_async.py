# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

"""Tests for PEP 492: async/await support."""

import unittest

from iptest import run_test


def run_coro(coro):
    """Run a coroutine to completion and return its result."""
    try:
        coro.send(None)
        raise AssertionError("Coroutine did not raise StopIteration")
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


run_test(__name__)
