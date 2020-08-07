# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import sys
import _thread as thread
import time
import unittest

from iptest import is_cli, run_test, skipUnlessIronPython, is_netcoreapp

if is_netcoreapp:
    import clr
    clr.AddReference("System.Threading.Thread")

class ThreadTest(unittest.TestCase):
    @skipUnlessIronPython()
    def test_thread(self):
        from System.Threading import ParameterizedThreadStart, Thread, ThreadStart

        class Sync:
            hit = 0

        def ThreadProcParm(parm):
            parm.hit = 1

        def ThreadProcNoParm():
            pass

        def Main():
            sync = Sync()
            t = Thread(ParameterizedThreadStart(ThreadProcParm))
            t.Start(sync)
            t.Join()
            self.assertTrue(sync.hit == 1)

            t = Thread(ThreadStart(ThreadProcNoParm))
            t.Start()
            t.Join()

        Main()


        def import_sys():
            import sys
            self.assertTrue(sys != None)

        t = Thread(ThreadStart(import_sys))
        t.Start()

        t.Join()

        so = sys.stdout
        se = sys.stderr
        class myStdOut:
            def write(self, text): pass


        sys.stdout = myStdOut()
        sys.stderr = myStdOut()

        import _thread as thread

        def raises(*p):
            raise Exception

        id = thread.start_new_thread(raises, ())
        Thread.Sleep(1000)  # wait a bit and make sure we don't get ripped.

        sys.stdout = so
        sys.stderr = se

    def test_stack_size(self):
        import sys
        if is_cli or (sys.version_info[0] == 2 and sys.version_info[1] > 4) or sys.version_info[0] > 2:
            import _thread as thread

            size = thread.stack_size()
            self.assertTrue(size==0 or size>=32768)

            bad_size_list = [ 1, -1, -32768, -32769, -32767, -40000, 32767, 32766]
            for bad_size in bad_size_list:
                self.assertRaises(ValueError, thread.stack_size, bad_size)

            good_size_list = [4096*10, 4096*100, 4096*1000, 4096*10000]
            for good_size in good_size_list:
                #CodePlex Work Item 7827
                if is_cli and good_size<=50000: print("Ignoring", good_size, "for CLI"); continue
                temp = thread.stack_size(good_size)
                self.assertTrue(temp>=32768 or temp==0)

            def temp(): pass
            thread.start_new_thread(temp, ())
            temp = thread.stack_size(1024*1024)
            self.assertTrue(temp>=32768 or temp==0)

    @skipUnlessIronPython()
    def test_new_thread_is_background(self):
        from System.Threading import Thread
        """verify new threads created during Python are background threads"""
        import _thread as thread
        global done
        done = None
        def f():
            global done
            done = Thread.CurrentThread.IsBackground
        thread.start_new_thread(f, ())
        while done == None:
            Thread.Sleep(100)
        self.assertTrue(done)


    def test_threading_waits_for_thread_exit(self):
        import os
        fname = 'temp_%d.py' % os.getpid()
        f = open(fname, 'w+')
        try:
            f.write("""
import atexit
def f():
    print('bye bye')

def f(*args):
    print('bye bye')

atexit.register(f)
from threading import Thread
def count(n):
    while n > 0:
            n -= 1
    print('done')

t1 = Thread(target=count, args=(50000000,))
t1.start()
    """)
            f.close()
            stdout = os.popen('"' + sys.executable + '" ' + fname)
            res = list(stdout)
            self.assertIn('bye bye\n', res)
        finally:
            os.unlink(fname)

    @skipUnlessIronPython()
    def test_thread_local(self):
        import _thread as thread
        from System.Threading import Thread
        x = thread._local()

        #--Sanity
        x.foo = 42
        self.assertEqual(x.foo, 42)

        global found
        found = None
        def f():
            global found
            found = hasattr(x, 'foo')

        thread.start_new_thread(f, ())

        while found == None:
            Thread.Sleep(100)

        self.assertTrue(not found)

        self.assertEqual(x.__dict__, {'foo': 42})
        try:
            x.__dict__ = None
            self.fail("Should not be able to set thread._local().__dict__!")
        except AttributeError as e:
            pass

        try:
            print(x.bar)
            self.fail("There is no 'bar' member on thread._local()")
        except AttributeError as e:
            pass

        del x.foo
        self.assertEqual(x.__dict__, {})

    def test_start_new(self):
        #--Sanity
        global CALLED
        CALLED = False

        def tempFunc():
            global CALLED
            CALLED = 3.14

        thread.start_new(tempFunc, ())
        while CALLED==False:
            print(".", end=" ")
            time.sleep(0.1)
        self.assertEqual(CALLED, 3.14)
        CALLED = False

    def test_start_new_thread(self):
        #--Sanity
        global CALLED
        CALLED = False

        lock = thread.allocate()
        def tempFunc(mykw_param=1):
            global CALLED
            lock.acquire()
            CALLED = mykw_param
            lock.release()
            thread.exit_thread()

        id = thread.start_new_thread(tempFunc, (), {"mykw_param":7})
        while CALLED==False:
            print(".", end=" ")
            time.sleep(0.1)
        self.assertEqual(CALLED, 7)

        id = thread.start_new_thread(tempFunc, (), {"mykw_param":8})
        while CALLED!=8:  #Hang forever if this is broken
            print(".", end=" ")
            time.sleep(0.1)

        #--Sanity Negative
        global temp_stderr
        temp_stderr = ""
        se = sys.stderr

        class myStdOut:
            def write(self, text):
                global temp_stderr
                temp_stderr += text

        try:
            sys.stderr = myStdOut()

            id = thread.start_new_thread(tempFunc, (), {"my_misspelled_kw_param":9})
            time.sleep(1)
            se.flush()
        finally:
            sys.stderr = se

        self.assertEqual(CALLED, 8)
        self.assertTrue("tempFunc() got an unexpected keyword argument 'my_misspelled_kw_param" in temp_stderr)


    @unittest.skip('TODO: add real test for thread_interrupt_main')
    def test_thread_interrupt_main():
        self.assertRaises(NotImplementedError, thread.interrupt_main)

run_test(__name__)
