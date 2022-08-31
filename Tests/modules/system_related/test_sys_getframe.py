# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import sys
import unittest

from iptest import IronPythonTestCase, big, is_cli, run_test

def g(self):
    y = 42
    def f():
        x = sys._getframe(1)
        return x

    self.assertEqual(f().f_locals['y'], 42)
    self.assertTrue(f().f_builtins is f().f_globals['__builtins__'].__dict__)
    self.assertEqual(f().f_locals['y'], 42)
    self.assertEqual(f().f_trace, None)

    # replace __builtins__, func code should have the non-replaced value
    global __builtins__
    oldbuiltin = __builtins__
    try:
        __builtins__ = dict(__builtins__.__dict__)
        def f():
            x = sys._getframe(1)
            return x
        self.assertTrue(f().f_builtins is oldbuiltin.__dict__)
    finally:
        __builtins__ = oldbuiltin

    def f():
        x = sys._getframe()
        return x

    self.assertEqual(f().f_back.f_locals['y'], 42)

    def f():
        yield sys._getframe()

    frame = list(f())[0]
    if is_cli:
        # incompat, this works for us, but not CPython, not sure why.
        self.assertEqual(frame.f_back.f_locals['y'], 42)

    # call through a built-in function
    global gfres
    class x(object):
        def __hash__(self):
            global gfres
            gfres = sys._getframe(1)
            return -1

    hash(x())
    self.assertEqual(gfres.f_locals['y'], 42)

class SysGetFrameTest(IronPythonTestCase):
    def test_getframe(self):
        # This test requires -X:FullFrames, run it in separate instance of IronPython.

        _getframe = sys._getframe

        for val in [None, 0, big(1), str, False]:
            sys._getframe = val
            self.assertEqual(sys._getframe, val)

        del sys._getframe

        try:
            # try access again
            x = sys._getframe
        except AttributeError: pass
        else: self.fail("Deletion of sys._getframe didn't take effect")

        try:
            # try deletion again
            del sys._getframe
        except AttributeError: pass
        else: self.fail("Deletion of sys._getframe didn't take effect")

        # restore it back
        sys._getframe = _getframe

        g(self)

        def f():
            x = 42
            def g():
                    import sys
                    self.assertEqual(sys._getframe(1).f_locals['x'], 42)
            g()
            yield 42

        list(f())

        class x:
            abc = sys._getframe(0)

        self.assertEqual(x.abc.f_locals['abc'], x.abc)

        class x:
            class y:
                abc = sys._getframe(1)
            abc = y.abc

        self.assertEqual(x.abc.f_locals['abc'], x.abc)

        for i in range(2):
            x = _getframe(0)
            y = _getframe(0)
            self.assertEqual(id(x), id(y))

        # odd func_code tests which require on _getframe
        global get_odd_code
        def get_odd_code():
            c = sys._getframe(1)
            return c.f_code

        def verify(code, is_defined):
            d = { 'get_odd_code' : get_odd_code }
            exec(code, d)
            self.assertEqual('defined' in d, is_defined)

        class x(object):
            abc = get_odd_code()
            defined = 42

        verify(x.abc, True)

        class x(object):
            abc = get_odd_code()
            global defined
            defined = 42

        if is_cli: # bug 20553 http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=20553
            verify(x.abc, False)
        else:
            verify(x.abc, True)

        d = {'get_odd_code' : get_odd_code}
        exec('defined=42\nabc = get_odd_code()\n', d)
        verify(d['abc'], True)

        # bug 24243, code objects should never be null
        class x(object):
            f = sys._getframe()
            while f:
                self.assertTrue(f.f_code is not None)
                self.assertTrue(f.f_code.co_name is not None)
                f = f.f_back

        # bug 24313, sys._getframe should return the same frame objects
        # that tracing functions receive
        def method():
            global method_id
            method_id = id(sys._getframe())

        def trace_dispatch(frame, event, arg):
            global found_id
            found_id = id(frame)

        sys.settrace(trace_dispatch)
        method()
        sys.settrace(None)

        self.assertEqual(method_id, found_id)

        # verify thread safety of sys.settrace
        import _thread as thread
        import time
        global done, failed
        done = 0
        failed = False
        lock = thread.allocate_lock()
        def starter(events):
            def tracer(*args):
                events.append(args[1])
                return tracer
            def f1(): time.sleep(.25)
            def f2(): time.sleep(1)
            def f3(): time.sleep(.5)

            def test_thread():
                global done, failed
                try:
                    sys.settrace(tracer)
                    f1()
                    sys.settrace(None)
                    f2()
                    sys.settrace(tracer)
                    f3()
                except:
                    failed = True
                with lock: done += 1

            thread.start_new_thread(test_thread, ())

        lists = []
        for i in range(10):
            cur_list = []
            starter(cur_list)
            lists.append(cur_list)

        while done != 10:
            pass
        for i in range(1, 10):
            self.assertEqual(lists[i-1], lists[i])

        self.assertTrue(not failed)

        # verify we report <module> for top-level code
        frame = sys._getframe()
        outer_frame = frame.f_back
        while outer_frame is not None:
            frame = outer_frame
            outer_frame = frame.f_back
        self.assertEqual(frame.f_code.co_name, "<module>")

    def test_cp24242(self):
        # This test requires -X:FullFrames, run it in separate instance of IronPython.
        import gc

        frame = sys._getframe()
        ids = []
        while frame:
            ids.append(id(frame))
            frame = frame.f_back
        del frame
        gc.collect()

        frame = sys._getframe()
        new_ids = []
        while frame:
            new_ids.append(id(frame))
            frame = frame.f_back
        del frame
        gc.collect()

        self.assertEqual(ids, new_ids)

run_test(__name__)
