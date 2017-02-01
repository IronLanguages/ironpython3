"""Tests for futures.py."""

import concurrent.futures
import re
import sys
import threading
import unittest
from unittest import mock

import asyncio
from asyncio import test_utils
try:
    from test import support
except ImportError:
    from asyncio import test_support as support


def _fakefunc(f):
    return f

def first_cb():
    pass

def last_cb():
    pass


class FutureTests(test_utils.TestCase):

    def setUp(self):
        self.loop = self.new_test_loop()
        self.addCleanup(self.loop.close)

    def test_initial_state(self):
        f = asyncio.Future(loop=self.loop)
        self.assertFalse(f.cancelled())
        self.assertFalse(f.done())
        f.cancel()
        self.assertTrue(f.cancelled())

    def test_init_constructor_default_loop(self):
        asyncio.set_event_loop(self.loop)
        f = asyncio.Future()
        self.assertIs(f._loop, self.loop)

    def test_constructor_positional(self):
        # Make sure Future doesn't accept a positional argument
        self.assertRaises(TypeError, asyncio.Future, 42)

    def test_cancel(self):
        f = asyncio.Future(loop=self.loop)
        self.assertTrue(f.cancel())
        self.assertTrue(f.cancelled())
        self.assertTrue(f.done())
        self.assertRaises(asyncio.CancelledError, f.result)
        self.assertRaises(asyncio.CancelledError, f.exception)
        self.assertRaises(asyncio.InvalidStateError, f.set_result, None)
        self.assertRaises(asyncio.InvalidStateError, f.set_exception, None)
        self.assertFalse(f.cancel())

    def test_result(self):
        f = asyncio.Future(loop=self.loop)
        self.assertRaises(asyncio.InvalidStateError, f.result)

        f.set_result(42)
        self.assertFalse(f.cancelled())
        self.assertTrue(f.done())
        self.assertEqual(f.result(), 42)
        self.assertEqual(f.exception(), None)
        self.assertRaises(asyncio.InvalidStateError, f.set_result, None)
        self.assertRaises(asyncio.InvalidStateError, f.set_exception, None)
        self.assertFalse(f.cancel())

    def test_exception(self):
        exc = RuntimeError()
        f = asyncio.Future(loop=self.loop)
        self.assertRaises(asyncio.InvalidStateError, f.exception)

        f.set_exception(exc)
        self.assertFalse(f.cancelled())
        self.assertTrue(f.done())
        self.assertRaises(RuntimeError, f.result)
        self.assertEqual(f.exception(), exc)
        self.assertRaises(asyncio.InvalidStateError, f.set_result, None)
        self.assertRaises(asyncio.InvalidStateError, f.set_exception, None)
        self.assertFalse(f.cancel())

    def test_exception_class(self):
        f = asyncio.Future(loop=self.loop)
        f.set_exception(RuntimeError)
        self.assertIsInstance(f.exception(), RuntimeError)

    def test_yield_from_twice(self):
        f = asyncio.Future(loop=self.loop)

        def fixture():
            yield 'A'
            x = yield from f
            yield 'B', x
            y = yield from f
            yield 'C', y

        g = fixture()
        self.assertEqual(next(g), 'A')  # yield 'A'.
        self.assertEqual(next(g), f)  # First yield from f.
        f.set_result(42)
        self.assertEqual(next(g), ('B', 42))  # yield 'B', x.
        # The second "yield from f" does not yield f.
        self.assertEqual(next(g), ('C', 42))  # yield 'C', y.

    def test_future_repr(self):
        self.loop.set_debug(True)
        f_pending_debug = asyncio.Future(loop=self.loop)
        frame = f_pending_debug._source_traceback[-1]
        self.assertEqual(repr(f_pending_debug),
                         '<Future pending created at %s:%s>'
                         % (frame[0], frame[1]))
        f_pending_debug.cancel()

        self.loop.set_debug(False)
        f_pending = asyncio.Future(loop=self.loop)
        self.assertEqual(repr(f_pending), '<Future pending>')
        f_pending.cancel()

        f_cancelled = asyncio.Future(loop=self.loop)
        f_cancelled.cancel()
        self.assertEqual(repr(f_cancelled), '<Future cancelled>')

        f_result = asyncio.Future(loop=self.loop)
        f_result.set_result(4)
        self.assertEqual(repr(f_result), '<Future finished result=4>')
        self.assertEqual(f_result.result(), 4)

        exc = RuntimeError()
        f_exception = asyncio.Future(loop=self.loop)
        f_exception.set_exception(exc)
        self.assertEqual(repr(f_exception),
                         '<Future finished exception=RuntimeError()>')
        self.assertIs(f_exception.exception(), exc)

        def func_repr(func):
            filename, lineno = test_utils.get_function_source(func)
            text = '%s() at %s:%s' % (func.__qualname__, filename, lineno)
            return re.escape(text)

        f_one_callbacks = asyncio.Future(loop=self.loop)
        f_one_callbacks.add_done_callback(_fakefunc)
        fake_repr = func_repr(_fakefunc)
        self.assertRegex(repr(f_one_callbacks),
                         r'<Future pending cb=\[%s\]>' % fake_repr)
        f_one_callbacks.cancel()
        self.assertEqual(repr(f_one_callbacks),
                         '<Future cancelled>')

        f_two_callbacks = asyncio.Future(loop=self.loop)
        f_two_callbacks.add_done_callback(first_cb)
        f_two_callbacks.add_done_callback(last_cb)
        first_repr = func_repr(first_cb)
        last_repr = func_repr(last_cb)
        self.assertRegex(repr(f_two_callbacks),
                         r'<Future pending cb=\[%s, %s\]>'
                         % (first_repr, last_repr))

        f_many_callbacks = asyncio.Future(loop=self.loop)
        f_many_callbacks.add_done_callback(first_cb)
        for i in range(8):
            f_many_callbacks.add_done_callback(_fakefunc)
        f_many_callbacks.add_done_callback(last_cb)
        cb_regex = r'%s, <8 more>, %s' % (first_repr, last_repr)
        self.assertRegex(repr(f_many_callbacks),
                         r'<Future pending cb=\[%s\]>' % cb_regex)
        f_many_callbacks.cancel()
        self.assertEqual(repr(f_many_callbacks),
                         '<Future cancelled>')

    def test_copy_state(self):
        from asyncio.futures import _copy_future_state

        f = asyncio.Future(loop=self.loop)
        f.set_result(10)

        newf = asyncio.Future(loop=self.loop)
        _copy_future_state(f, newf)
        self.assertTrue(newf.done())
        self.assertEqual(newf.result(), 10)

        f_exception = asyncio.Future(loop=self.loop)
        f_exception.set_exception(RuntimeError())

        newf_exception = asyncio.Future(loop=self.loop)
        _copy_future_state(f_exception, newf_exception)
        self.assertTrue(newf_exception.done())
        self.assertRaises(RuntimeError, newf_exception.result)

        f_cancelled = asyncio.Future(loop=self.loop)
        f_cancelled.cancel()

        newf_cancelled = asyncio.Future(loop=self.loop)
        _copy_future_state(f_cancelled, newf_cancelled)
        self.assertTrue(newf_cancelled.cancelled())

    def test_iter(self):
        fut = asyncio.Future(loop=self.loop)

        def coro():
            yield from fut

        def test():
            arg1, arg2 = coro()

        self.assertRaises(AssertionError, test)
        fut.cancel()

    @mock.patch('asyncio.base_events.logger')
    def test_tb_logger_abandoned(self, m_log):
        fut = asyncio.Future(loop=self.loop)
        del fut
        self.assertFalse(m_log.error.called)

    @mock.patch('asyncio.base_events.logger')
    def test_tb_logger_result_unretrieved(self, m_log):
        fut = asyncio.Future(loop=self.loop)
        fut.set_result(42)
        del fut
        self.assertFalse(m_log.error.called)

    @mock.patch('asyncio.base_events.logger')
    def test_tb_logger_result_retrieved(self, m_log):
        fut = asyncio.Future(loop=self.loop)
        fut.set_result(42)
        fut.result()
        del fut
        self.assertFalse(m_log.error.called)

    @mock.patch('asyncio.base_events.logger')
    def test_tb_logger_exception_unretrieved(self, m_log):
        fut = asyncio.Future(loop=self.loop)
        fut.set_exception(RuntimeError('boom'))
        del fut
        test_utils.run_briefly(self.loop)
        self.assertTrue(m_log.error.called)

    @mock.patch('asyncio.base_events.logger')
    def test_tb_logger_exception_retrieved(self, m_log):
        fut = asyncio.Future(loop=self.loop)
        fut.set_exception(RuntimeError('boom'))
        fut.exception()
        del fut
        self.assertFalse(m_log.error.called)

    @mock.patch('asyncio.base_events.logger')
    def test_tb_logger_exception_result_retrieved(self, m_log):
        fut = asyncio.Future(loop=self.loop)
        fut.set_exception(RuntimeError('boom'))
        self.assertRaises(RuntimeError, fut.result)
        del fut
        self.assertFalse(m_log.error.called)

    def test_wrap_future(self):

        def run(arg):
            return (arg, threading.get_ident())
        ex = concurrent.futures.ThreadPoolExecutor(1)
        f1 = ex.submit(run, 'oi')
        f2 = asyncio.wrap_future(f1, loop=self.loop)
        res, ident = self.loop.run_until_complete(f2)
        self.assertIsInstance(f2, asyncio.Future)
        self.assertEqual(res, 'oi')
        self.assertNotEqual(ident, threading.get_ident())

    def test_wrap_future_future(self):
        f1 = asyncio.Future(loop=self.loop)
        f2 = asyncio.wrap_future(f1)
        self.assertIs(f1, f2)

    @mock.patch('asyncio.futures.events')
    def test_wrap_future_use_global_loop(self, m_events):
        def run(arg):
            return (arg, threading.get_ident())
        ex = concurrent.futures.ThreadPoolExecutor(1)
        f1 = ex.submit(run, 'oi')
        f2 = asyncio.wrap_future(f1)
        self.assertIs(m_events.get_event_loop.return_value, f2._loop)

    def test_wrap_future_cancel(self):
        f1 = concurrent.futures.Future()
        f2 = asyncio.wrap_future(f1, loop=self.loop)
        f2.cancel()
        test_utils.run_briefly(self.loop)
        self.assertTrue(f1.cancelled())
        self.assertTrue(f2.cancelled())

    def test_wrap_future_cancel2(self):
        f1 = concurrent.futures.Future()
        f2 = asyncio.wrap_future(f1, loop=self.loop)
        f1.set_result(42)
        f2.cancel()
        test_utils.run_briefly(self.loop)
        self.assertFalse(f1.cancelled())
        self.assertEqual(f1.result(), 42)
        self.assertTrue(f2.cancelled())

    def test_future_source_traceback(self):
        self.loop.set_debug(True)

        future = asyncio.Future(loop=self.loop)
        lineno = sys._getframe().f_lineno - 1
        self.assertIsInstance(future._source_traceback, list)
        self.assertEqual(future._source_traceback[-1][:3],
                         (__file__,
                          lineno,
                          'test_future_source_traceback'))

    @mock.patch('asyncio.base_events.logger')
    def check_future_exception_never_retrieved(self, debug, m_log):
        self.loop.set_debug(debug)

        def memory_error():
            try:
                raise MemoryError()
            except BaseException as exc:
                return exc
        exc = memory_error()

        future = asyncio.Future(loop=self.loop)
        if debug:
            source_traceback = future._source_traceback
        future.set_exception(exc)
        future = None
        test_utils.run_briefly(self.loop)
        support.gc_collect()

        if sys.version_info >= (3, 4):
            if debug:
                frame = source_traceback[-1]
                regex = (r'^Future exception was never retrieved\n'
                         r'future: <Future finished exception=MemoryError\(\) '
                             r'created at {filename}:{lineno}>\n'
                         r'source_traceback: Object '
                            r'created at \(most recent call last\):\n'
                         r'  File'
                         r'.*\n'
                         r'  File "{filename}", line {lineno}, '
                            r'in check_future_exception_never_retrieved\n'
                         r'    future = asyncio\.Future\(loop=self\.loop\)$'
                         ).format(filename=re.escape(frame[0]),
                                  lineno=frame[1])
            else:
                regex = (r'^Future exception was never retrieved\n'
                         r'future: '
                            r'<Future finished exception=MemoryError\(\)>$'
                         )
            exc_info = (type(exc), exc, exc.__traceback__)
            m_log.error.assert_called_once_with(mock.ANY, exc_info=exc_info)
        else:
            if debug:
                frame = source_traceback[-1]
                regex = (r'^Future/Task exception was never retrieved\n'
                         r'Future/Task created at \(most recent call last\):\n'
                         r'  File'
                         r'.*\n'
                         r'  File "{filename}", line {lineno}, '
                            r'in check_future_exception_never_retrieved\n'
                         r'    future = asyncio\.Future\(loop=self\.loop\)\n'
                         r'Traceback \(most recent call last\):\n'
                         r'.*\n'
                         r'MemoryError$'
                         ).format(filename=re.escape(frame[0]),
                                  lineno=frame[1])
            else:
                regex = (r'^Future/Task exception was never retrieved\n'
                         r'Traceback \(most recent call last\):\n'
                         r'.*\n'
                         r'MemoryError$'
                         )
            m_log.error.assert_called_once_with(mock.ANY, exc_info=False)
        message = m_log.error.call_args[0][0]
        self.assertRegex(message, re.compile(regex, re.DOTALL))

    def test_future_exception_never_retrieved(self):
        self.check_future_exception_never_retrieved(False)

    def test_future_exception_never_retrieved_debug(self):
        self.check_future_exception_never_retrieved(True)

    def test_set_result_unless_cancelled(self):
        from asyncio import futures
        fut = asyncio.Future(loop=self.loop)
        fut.cancel()
        futures._set_result_unless_cancelled(fut, 2)
        self.assertTrue(fut.cancelled())


class FutureDoneCallbackTests(test_utils.TestCase):

    def setUp(self):
        self.loop = self.new_test_loop()

    def run_briefly(self):
        test_utils.run_briefly(self.loop)

    def _make_callback(self, bag, thing):
        # Create a callback function that appends thing to bag.
        def bag_appender(future):
            bag.append(thing)
        return bag_appender

    def _new_future(self):
        return asyncio.Future(loop=self.loop)

    def test_callbacks_invoked_on_set_result(self):
        bag = []
        f = self._new_future()
        f.add_done_callback(self._make_callback(bag, 42))
        f.add_done_callback(self._make_callback(bag, 17))

        self.assertEqual(bag, [])
        f.set_result('foo')

        self.run_briefly()

        self.assertEqual(bag, [42, 17])
        self.assertEqual(f.result(), 'foo')

    def test_callbacks_invoked_on_set_exception(self):
        bag = []
        f = self._new_future()
        f.add_done_callback(self._make_callback(bag, 100))

        self.assertEqual(bag, [])
        exc = RuntimeError()
        f.set_exception(exc)

        self.run_briefly()

        self.assertEqual(bag, [100])
        self.assertEqual(f.exception(), exc)

    def test_remove_done_callback(self):
        bag = []
        f = self._new_future()
        cb1 = self._make_callback(bag, 1)
        cb2 = self._make_callback(bag, 2)
        cb3 = self._make_callback(bag, 3)

        # Add one cb1 and one cb2.
        f.add_done_callback(cb1)
        f.add_done_callback(cb2)

        # One instance of cb2 removed. Now there's only one cb1.
        self.assertEqual(f.remove_done_callback(cb2), 1)

        # Never had any cb3 in there.
        self.assertEqual(f.remove_done_callback(cb3), 0)

        # After this there will be 6 instances of cb1 and one of cb2.
        f.add_done_callback(cb2)
        for i in range(5):
            f.add_done_callback(cb1)

        # Remove all instances of cb1. One cb2 remains.
        self.assertEqual(f.remove_done_callback(cb1), 6)

        self.assertEqual(bag, [])
        f.set_result('foo')

        self.run_briefly()

        self.assertEqual(bag, [2])
        self.assertEqual(f.result(), 'foo')


if __name__ == '__main__':
    unittest.main()
