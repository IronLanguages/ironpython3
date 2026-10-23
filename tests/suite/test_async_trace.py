# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

"""
Tests for sys.settrace tracing of async functions and async generators.

They also run on CPython (checked with 3.7 to 3.15), which serves as the reference: put src/core/IronPython/Lib on PYTHONPATH for iptest.
"""

import sys
import unittest

from iptest import run_test, is_cli, is_netcoreapp

if is_cli:
    import clr
    clr.AddReference("IronPython")
    from IronPython.Runtime.Operations import PythonOps


def drive(coro):
    """Drive a coroutine to completion without an event loop; return its result."""
    try:
        while True:
            coro.send(None)
    except StopIteration as e:
        return e.value


def stack_depth():
    """The depth of the thread's Python frame stack (the one sys._getframe walks)."""
    if is_cli:
        return PythonOps.GetFunctionStack().Count

    depth, frame = 0, sys._getframe(1)
    while frame is not None:
        depth += 1
        frame = frame.f_back
    return depth


class Recorder:
    """
    Trace function recording the events of the given functions as (event, name, line), with lines relative to the
    function's first line, and the argument of each 'return' event as (name, arg).

    It ignores the 'exception' event CPython reports for the StopIteration that ends an awaited coroutine (with no traceback).
    IronPython does not report it, as no exception is involved.
    Debuggers based on bdb, like pdb, skip that event anyway, and coverage tools do not use exception events.
    """
    def __init__(self, *functions):
        self.codes = {f.__code__ for f in functions}
        self.events = []
        self.returns = []

    def __call__(self, frame, event, arg):
        code = frame.f_code
        if code in self.codes and not (event == "exception" and arg[0] is StopIteration and arg[2] is None):
            self.events.append((event, code.co_name, frame.f_lineno - code.co_firstlineno))
            if event == "return":
                self.returns.append((code.co_name, arg))
        return self

    def run(self, fn, *args):
        sys.settrace(self)
        try:
            return fn(*args)
        finally:
            sys.settrace(None)

    def drive(self, async_fn, *args):
        """
        Create the coroutine async_fn(*args) and drive it, both under tracing. (Unless run with -X:Tracing, IronPython decides
        whether a coroutine or generator body is traced when it is created.)
        """
        return self.run(lambda: drive(async_fn(*args)))


@unittest.skipIf(is_cli and not is_netcoreapp, "tests tracing of the .NET async implementation (FEATURE_NET_ASYNC)")
class AsyncTraceTest(unittest.TestCase):

    def test_creating_coroutine_is_not_traced(self):
        async def coro():
            return 1

        rec = Recorder(coro)
        c = rec.run(coro)
        self.assertEqual(rec.events, [])

        self.assertEqual(rec.run(drive, c), 1)
        self.assertEqual(rec.events, [("call", "coro", 0), ("line", "coro", 1), ("return", "coro", 1)])

    def test_call_location_matches_sync_function(self):
        # Like for a synchronous function (and in CPython), 'call' is at the first line of the header, also when it spans several lines.
        def f(a,
              b):
            return a

        async def cf(a,
                     b):
            return a

        rec = Recorder(f, cf)
        rec.run(f, 1, 2)
        rec.drive(cf, 1, 2)
        self.assertEqual(rec.events, [
            ("call", "f", 0), ("line", "f", 2), ("return", "f", 2),
            ("call", "cf", 0), ("line", "cf", 2), ("return", "cf", 2),
        ])

    def test_coroutine_events(self):
        # Awaits of coroutines that complete synchronously do not suspend the awaiting coroutine: no 'return'/'call' pairs at them.
        async def inner(x):
            return x * 2

        async def coro(n):
            total = 0
            for i in range(n):
                total += await inner(i)
            return total

        rec = Recorder(inner, coro)
        self.assertEqual(rec.drive(coro, 2), 2)
        inner_events = [("call", "inner", 0), ("line", "inner", 1), ("return", "inner", 1)]
        self.assertEqual(rec.events, [
            ("call", "coro", 0), ("line", "coro", 1), ("line", "coro", 2), ("line", "coro", 3),
            *inner_events,
            ("line", "coro", 2), ("line", "coro", 3),
            *inner_events,
            ("line", "coro", 2), ("line", "coro", 4), ("return", "coro", 4),
        ])
        self.assertEqual(rec.returns, [("inner", 0), ("inner", 2), ("coro", 2)])

    def test_async_generator_traced_per_resumption(self):
        # As for a generator in CPython: 'call' when resumed (at the line it resumes at), 'return' when it yields (with the value).
        async def agen():
            yield 1
            yield 2

        async def consume(out):
            async for v in agen():
                out.append(v)

        rec = Recorder(agen)
        out = []
        rec.run(drive, consume(out))
        self.assertEqual(out, [1, 2])
        self.assertEqual(rec.events, [
            ("call", "agen", 0), ("line", "agen", 1), ("return", "agen", 1),
            ("call", "agen", 1), ("line", "agen", 2), ("return", "agen", 2),
            ("call", "agen", 2), ("return", "agen", 2),
        ])
        if is_cli:
            # CPython reports an internal wrapper (async_generator_wrapped_value) of the yielded value instead
            self.assertEqual(rec.returns, [("agen", 1), ("agen", 2), ("agen", None)])

    def test_abandoned_async_generator_leaves_no_frame(self):
        async def agen():
            yield 1
            yield 2

        async def consume(ag):
            async for v in ag:
                break

        def runner():
            before = stack_depth()
            ag = agen()
            drive(consume(ag))
            # The generator is kept alive until tracing ends, so that CPython does not finalize it (PEP 525) while traced.
            return before, stack_depth(), sys._getframe().f_code.co_name, ag

        rec = Recorder(agen)
        before, after, name, ag = rec.run(runner)
        self.assertEqual(after, before)
        self.assertEqual(name, "runner")
        self.assertEqual(rec.events, [("call", "agen", 0), ("line", "agen", 1), ("return", "agen", 1)])

    def test_interleaved_async_generators(self):
        async def g1():
            yield 1
            yield 2

        async def g2():
            yield 10
            yield 20
            yield 30

        async def zip2(a, b, out):
            while True:
                try:
                    x = await a.__anext__()
                    y = await b.__anext__()
                except StopAsyncIteration:
                    break
                out.append((x, y))

        def runner(out):
            before = stack_depth()
            a, b = g1(), g2()
            drive(zip2(a, b, out))
            # The generators are kept alive until tracing ends, so that CPython does not finalize g2 (PEP 525) while traced.
            return before, stack_depth(), (a, b)

        rec = Recorder(g1, g2)
        out = []
        before, after, generators = rec.run(runner, out)
        self.assertEqual(out, [(1, 10), (2, 20)])
        self.assertEqual(after, before)
        self.assertEqual([e for e in rec.events if e[1] == "g1"], [
            ("call", "g1", 0), ("line", "g1", 1), ("return", "g1", 1),
            ("call", "g1", 1), ("line", "g1", 2), ("return", "g1", 2),
            ("call", "g1", 2), ("return", "g1", 2),
        ])
        self.assertEqual([e for e in rec.events if e[1] == "g2"], [
            ("call", "g2", 0), ("line", "g2", 1), ("return", "g2", 1),
            ("call", "g2", 1), ("line", "g2", 2), ("return", "g2", 2),
        ])

    @unittest.skipUnless(is_cli, "awaits a .NET Task")
    def test_resumption_on_another_thread(self):
        # The coroutine suspends on the driving thread and resumes on a thread-pool thread (which has no trace function,
        # as sys.settrace is per thread): its frame must not stay behind on the driving thread.
        from System.Threading.Tasks import Task

        async def coro():
            a = 1
            await Task.Delay(20)
            return a

        def runner():
            before = stack_depth()
            result = drive(coro())
            return result, before, stack_depth(), sys._getframe().f_code.co_name

        rec = Recorder(coro)
        result, before, after, name = rec.run(runner)
        self.assertEqual(result, 1)
        self.assertEqual(after, before)
        self.assertEqual(name, "runner")
        self.assertEqual(rec.events, [("call", "coro", 0), ("line", "coro", 1), ("line", "coro", 2), ("return", "coro", 2)])

    def test_caught_exception_is_reported(self):
        async def coro():
            try:
                raise ValueError()
            except ValueError:
                pass
            return 1

        rec = Recorder(coro)
        self.assertEqual(rec.drive(coro), 1)
        self.assertEqual(rec.events, [
            ("call", "coro", 0), ("line", "coro", 1), ("line", "coro", 2), ("exception", "coro", 2),
            ("line", "coro", 3), ("line", "coro", 4), ("line", "coro", 5), ("return", "coro", 5),
        ])

    def test_escaping_exception_is_reported_once(self):
        async def coro():
            raise KeyError()

        rec = Recorder(coro)
        with self.assertRaises(KeyError):
            rec.drive(coro)
        self.assertEqual(rec.events, [("call", "coro", 0), ("line", "coro", 1), ("exception", "coro", 1), ("return", "coro", 1)])

    def test_tracing_does_not_rerun_exception_handler(self):
        # A handler that leaves its loop iteration early must not run again in a later iteration that does not raise; the handler has no
        # await, so it is only tracing that puts a suspension point (its debug points) into it.
        async def left_by_continue():
            log = []
            for i in range(3):
                try:
                    if i == 0:
                        raise ValueError()
                    log.append(("try", i))
                except ValueError:
                    log.append(("except", i))
                    continue
            return log

        async def left_by_break():
            log = []
            for j in range(2):
                for i in range(2):
                    try:
                        if j == 0:
                            raise ValueError()
                        log.append(("try", j, i))
                    except ValueError:
                        log.append(("except", j, i))
                        break
            return log

        with self.subTest("continue"):
            self.assertEqual(Recorder(left_by_continue).drive(left_by_continue), [("except", 0), ("try", 1), ("try", 2)])
        with self.subTest("break"):
            self.assertEqual(Recorder(left_by_break).drive(left_by_break), [("except", 0, 0), ("try", 1, 0), ("try", 1, 1)])

    def test_trace_function_exception_surfaces_in_body(self):
        class Boom(Exception):
            pass

        log = []

        async def coro():
            try:
                x = 1
            finally:
                log.append("finally")

        def tracer(frame, event, arg):
            if frame.f_code is coro.__code__ and event == "line" and frame.f_lineno == coro.__code__.co_firstlineno + 2:
                raise Boom()
            return tracer

        sys.settrace(tracer)
        try:
            with self.assertRaises(Boom):
                drive(coro())
        finally:
            sys.settrace(None)
        self.assertEqual(log, ["finally"])

    @unittest.skipIf(not is_cli and sys.version_info < (3, 11), "CPython before 3.11 raises a trace function's exception outside the resumed frame")
    def test_trace_function_exception_on_resumption_surfaces_in_async_generator(self):
        # The trace function raises on the 'call' event of the generator's second resumption, right after its first yield: as in CPython,
        # the exception surfaces at that yield, inside the generator, and ends the iteration.
        class Boom(Exception):
            pass

        log = []

        async def agen():
            try:
                yield 1
                log.append("resumed")
                yield 2
            finally:
                log.append("finally")

        async def consume(out):
            async for v in agen():
                out.append(v)

        calls = []

        def tracer(frame, event, arg):
            if frame.f_code is agen.__code__ and event == "call":
                calls.append(event)
                if len(calls) == 2:
                    raise Boom()
            return tracer

        out = []
        sys.settrace(tracer)
        try:
            with self.assertRaises(Boom):
                drive(consume(out))
        finally:
            sys.settrace(None)
        self.assertEqual(out, [1])
        self.assertEqual(log, ["finally"])

    @unittest.skipUnless(is_cli, "an IronPython limitation: CPython can jump in async frames")
    def test_setting_lineno_is_refused(self):
        errors = []

        async def coro():
            x = 1
            x = 2
            return x

        def tracer(frame, event, arg):
            if frame.f_code is coro.__code__ and event == "line" and frame.f_lineno == coro.__code__.co_firstlineno + 2:
                try:
                    frame.f_lineno += 1
                except Exception as e:
                    errors.append(type(e))
            return tracer

        sys.settrace(tracer)
        try:
            result = drive(coro())
        finally:
            sys.settrace(None)
        self.assertEqual(errors, [ValueError])
        self.assertEqual(result, 2)


run_test(__name__)
