# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

"""Tests for async constructs that lower to .NET IAsyncEnumerable / IAsyncDisposable:
async generators (PEP 525) and `async with` over a .NET IAsyncDisposable."""

import unittest

from iptest import run_test, skipUnlessIronPython


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


@skipUnlessIronPython()
class DotNetAsyncDisposableTest(unittest.TestCase):
    """Tests for async with over a .NET IAsyncDisposable."""

    def test_async_with_dotnet_disposable(self):
        """async with over a .NET IAsyncDisposable enters with the resource and disposes on exit."""
        from System.IO import MemoryStream
        s = MemoryStream()
        self.assertTrue(hasattr(s, '__aenter__'))
        self.assertTrue(hasattr(s, '__aexit__'))

        async def test():
            async with s as entered:
                self.assertIs(entered, s)
                self.assertTrue(entered.CanWrite)
            return s.CanWrite  # disposed on exit -> False

        self.assertFalse(run_coro(test()))


@skipUnlessIronPython()
class AsyncGeneratorTest(unittest.TestCase):
    """Tests for PEP 525 async generators (await + yield). Bodies await .NET Tasks."""


    def test_await_and_yield(self):
        """An async generator body mixing await and yield, consumed via async for."""
        from System.Threading.Tasks import Task

        async def agen():
            yield 1
            x = await Task.FromResult(10)
            yield x + 1       # await result feeds the next yield
            if x > 5:
                return        # bare return ends the async iteration
            yield 'unreached'

        async def consume():
            out = []
            async for v in agen():
                out.append(v)
            return out

        self.assertEqual(run_coro(consume()), [1, 11])


    def test_yield_task_is_value_not_await(self):
        """A yielded Task is the produced value, not a suspension point."""
        from System.Threading.Tasks import Task

        async def agen():
            yield Task.FromResult(99)

        async def consume():
            out = []
            async for item in agen():
                out.append(item)
            return out

        items = run_coro(consume())
        self.assertEqual(len(items), 1)
        # The item is the Task itself; if it had been awaited we'd have gotten 99.
        self.assertEqual(items[0].Result, 99)


    def test_async_generator_type(self):
        async def agen():
            yield 1
        g = agen()
        self.assertEqual(type(g).__name__, 'async_generator')


    def test_asend(self):
        """asend feeds a value into `x = yield z` (PEP 525 two-way communication)."""
        async def agen():
            got1 = yield 1
            got2 = yield ('after1', got1)
            yield ('after2', got2)

        async def drive():
            g = agen()
            out = []
            out.append(await g.asend(None))    # start -> yields 1
            out.append(await g.asend('A'))     # got1 == 'A' -> ('after1', 'A')
            out.append(await g.asend('B'))     # got2 == 'B' -> ('after2', 'B')
            try:
                await g.asend('C')
            except StopAsyncIteration:
                out.append('stop')
            return out

        self.assertEqual(run_coro(drive()), [1, ('after1', 'A'), ('after2', 'B'), 'stop'])


    def test_athrow(self):
        """athrow injects an exception at the yield; the body can catch and continue."""
        async def agen():
            try:
                yield 1
            except ValueError as e:
                yield ('caught', str(e))
            yield 3

        async def drive():
            g = agen()
            out = []
            out.append(await g.asend(None))                 # -> 1
            out.append(await g.athrow(ValueError('boom')))  # caught -> ('caught', 'boom')
            out.append(await g.asend(None))                 # -> 3
            try:
                await g.asend(None)
            except StopAsyncIteration:
                out.append('stop')
            return out

        self.assertEqual(run_coro(drive()), [1, ('caught', 'boom'), 3, 'stop'])


    def test_aclose_runs_finally(self):
        """aclose injects GeneratorExit at the yield so the body's finally runs."""
        log = []

        async def agen():
            try:
                yield 1
                yield 2
            finally:
                log.append('cleanup')

        async def drive():
            g = agen()
            first = await g.asend(None)   # -> 1
            await g.aclose()              # GeneratorExit at the yield -> finally runs
            return first

        self.assertEqual(run_coro(drive()), 1)
        self.assertEqual(log, ['cleanup'])


@skipUnlessIronPython()
class NestedAsyncGeneratorTest(unittest.TestCase):
    """An async generator lexically nested inside another async generator.

    Both functions own the async send/throw slots; their slot variables must stay
    distinct per function so the inner generator's slots don't collide with the
    outer's when the inner lambda is nested inside the outer.
    """


    def test_nested_async_generator(self):
        """Outer async gen consumes a nested async gen via async for and re-yields."""
        async def outer():
            async def inner():
                yield 'a'
                yield 'b'
            async for x in inner():
                yield x.upper()

        async def consume():
            out = []
            async for v in outer():
                out.append(v)
            return out

        self.assertEqual(run_coro(consume()), ['A', 'B'])


    def test_nested_async_generator_with_await(self):
        """Both nested async gens await a .NET Task between yields."""
        from System.Threading.Tasks import Task

        async def outer():
            async def inner():
                yield 1
                n = await Task.FromResult(10)
                yield n + 1
            async for v in inner():
                doubled = await Task.FromResult(v * 2)
                yield doubled

        async def consume():
            out = []
            async for v in outer():
                out.append(v)
            return out

        self.assertEqual(run_coro(consume()), [2, 22])


    def test_nested_inner_asend(self):
        """Inner nested async gen driven with asend (sendSlot); outer re-yields the pairs."""
        async def outer():
            async def inner():
                a = yield 1
                b = yield ('a', a)
                yield ('b', b)
            g = inner()
            yield await g.asend(None)    # -> 1
            yield await g.asend('X')     # a == 'X' -> ('a', 'X')
            yield await g.asend('Y')     # b == 'Y' -> ('b', 'Y')

        async def consume():
            out = []
            async for v in outer():
                out.append(v)
            return out

        self.assertEqual(run_coro(consume()), [1, ('a', 'X'), ('b', 'Y')])


    def test_nested_inner_athrow(self):
        """athrow into the inner nested async gen (throwSlot); outer re-yields the recovery."""
        async def outer():
            async def inner():
                try:
                    yield 1
                except ValueError as e:
                    yield ('caught', str(e))
            g = inner()
            yield await g.asend(None)                 # -> 1
            yield await g.athrow(ValueError('boom'))  # inner catches -> ('caught', 'boom')

        async def consume():
            out = []
            async for v in outer():
                out.append(v)
            return out

        self.assertEqual(run_coro(consume()), [1, ('caught', 'boom')])


run_test(__name__)
