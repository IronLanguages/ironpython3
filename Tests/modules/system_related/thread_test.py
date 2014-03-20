#####################################################################################
#
#  Copyright (c) Microsoft Corporation. All rights reserved.
#
# This source code is subject to terms and conditions of the Apache License, Version 2.0. A
# copy of the license can be found in the License.html file at the root of this distribution. If
# you cannot locate the  Apache License, Version 2.0, please send an email to
# ironpy@microsoft.com. By using this source code in any fashion, you are agreeing to be bound
# by the terms of the Apache License, Version 2.0.
#
# You must not remove this notice, or any other, from this software.
#
#
#####################################################################################

from iptest.assert_util import *
import thread
import time
import sys
if not is_cpython:
    from System import *
    from System.Threading import *

@skip("win32")
def test_thread():
    
    class Sync:
        hit = 0
    
    def ThreadProcParm(parm):
        parm.hit = 1
    
    def ThreadProcNoParm():
        pass
    
    def Main():
        if not is_silverlight:
            sync = Sync()
            t = Thread(ParameterizedThreadStart(ThreadProcParm))
            t.Start(sync)
            t.Join()
            Assert(sync.hit == 1)
    
        t = Thread(ThreadStart(ThreadProcNoParm))
        t.Start()
        t.Join()
    
    Main()
    
    
    def import_sys():
        import sys
        Assert(sys != None)
    
    t = Thread(ThreadStart(import_sys))
    t.Start()
    
    t.Join()
    
    so = sys.stdout
    se = sys.stderr
    class myStdOut:
        def write(self, text): pass
    
    
    sys.stdout = myStdOut()
    sys.stderr = myStdOut()
    
    import thread
    
    def raises(*p):
        raise Exception
    
    id = thread.start_new_thread(raises, ())
    Thread.Sleep(1000)  # wait a bit and make sure we don't get ripped.
    
    sys.stdout = so
    sys.stderr = se

def test_stack_size():
    import sys
    if is_cli or (sys.version_info[0] == 2 and sys.version_info[1] > 4) or sys.version_info[0] > 2:
        import thread
        
        size = thread.stack_size()
        Assert(size==0 or size>=32768)

        bad_size_list = [ 1, -1, -32768, -32769, -32767, -40000, 32767, 32766]
        for bad_size in bad_size_list:
            AssertError(ValueError, thread.stack_size, bad_size)
            
        good_size_list = [4096*10, 4096*100, 4096*1000, 4096*10000]
        for good_size in good_size_list:
            #CodePlex Work Item 7827
            if (is_cli or is_silverlight) and good_size<=50000: print "Ignoring", good_size, "for CLI"; continue
            temp = thread.stack_size(good_size)
            Assert(temp>=32768 or temp==0)
        
        def temp(): pass
        thread.start_new_thread(temp, ())
        temp = thread.stack_size(1024*1024)
        Assert(temp>=32768 or temp==0)

@skip("win32")
def test_new_thread_is_background():
    """verify new threads created during Python are background threads"""
    import thread
    global done
    done = None
    def f():
        global done
        done = Thread.CurrentThread.IsBackground
    thread.start_new_thread(f, ())
    while done == None:
        Thread.Sleep(1000)
    Assert(done)

@skip("silverlight")
def test_threading_waits_for_thread_exit():
    import os
    from iptest.process_util import launch
    f = file('temp.py', 'w+')
    try:
        f.write("""
import sys
def f():
    print 'bye bye'

def f(*args):
    print 'bye bye'

sys.exitfunc = f
from threading import Thread
def count(n):
    while n > 0:
            n -= 1
    print 'done'

t1 = Thread(target=count, args=(50000000,))
t1.start()
    """)
        f.close()
        stdin, stdout = os.popen2(sys.executable +  ' temp.py')
        Assert('bye bye\n' in list(stdout))
    finally:
        import nt
        nt.unlink('temp.py')
    
@skip("win32")
def test_thread_local():
    import thread
    x = thread._local()
    
    #--Sanity
    x.foo = 42
    AreEqual(x.foo, 42)
    
    global found
    found = None
    def f():
        global found
        found = hasattr(x, 'foo')
        
    thread.start_new_thread(f, ())

    while found == None:
        Thread.Sleep(1000)

    Assert(not found)
    
    AreEqual(x.__dict__, {'foo': 42})
    try:
        x.__dict__ = None
        Fail("Should not be able to set thread._local().__dict__!")
    except AttributeError, e:
        pass
    
    try:
        print x.bar
        Fail("There is no 'bar' member on thread._local()")
    except AttributeError, e:
        pass
    
    del x.foo
    AreEqual(x.__dict__, {})

def test_start_new():
    #--Sanity
    global CALLED
    CALLED = False
    
    def tempFunc():
        global CALLED
        CALLED = 3.14
    
    thread.start_new(tempFunc, ())
    while CALLED==False:
        print ".",
        time.sleep(1)
    AreEqual(CALLED, 3.14)
    CALLED = False

def test_start_new_thread():
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
        print ".",
        time.sleep(1)
    AreEqual(CALLED, 7)
    
    id = thread.start_new_thread(tempFunc, (), {"mykw_param":8})
    while CALLED!=8:  #Hang forever if this is broken
        print ".",
        time.sleep(1)
    
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
        time.sleep(5)
        if not is_silverlight:
            se.flush()
    finally:
        sys.stderr = se
    
    AreEqual(CALLED, 8)
    Assert("tempFunc() got an unexpected keyword argument 'my_misspelled_kw_param" in temp_stderr)

    
@skip("win32")
def test_thread_interrupt_main():
    AssertError(NotImplementedError, thread.interrupt_main)

#------------------------------------------------------------------------------
run_test(__name__)
