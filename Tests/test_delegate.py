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
skiptest("win32")
load_iron_python_test()
import IronPythonTest
import clr

from System.Threading import *
from System import EventArgs

are = AutoResetEvent(False)

#for silverlight only
class TimerProxy: pass

def MyTick(state):
    global superTimer
    if is_silverlight==False:
        AreEqual(state, superTimer)
    else:
        AreEqual(state.superTimer, superTimer)
    are.Set()

def SimpleHandler(sender, args):
    global superTimer
    if not is_silverlight:
        superTimer = Timer(MyTick)
    else:
        tp = TimerProxy()
        superTimer = Timer.__new__.Overloads[(TimerCallback,
                                              System.Object,
                                              int,
                                              int)](Timer, MyTick, tp, Timeout.Infinite, Timeout.Infinite)
        tp.superTimer = superTimer

    superTimer.Change(1000, 0)
        

dlgTst = IronPythonTest.DelegateTest()
dlgTst.Event += SimpleHandler

dlgTst.FireInstance(None, EventArgs.Empty)
are.WaitOne()
superTimer.Dispose()

############################################################
# test various combinations of delegates...

dlgTst = IronPythonTest.DelegateTest()

def Handler(self, args):
    global glblSelf, glblArgs, handlerCalled
    
    AreEqual(self, glblSelf)
    AreEqual(args, glblArgs)
    handlerCalled = True

# check the methods w/ object sender, EventArgs sigs first...
glblSelf = 0
for x in [(IronPythonTest.DelegateTest.StaticEvent, IronPythonTest.DelegateTest.FireStatic),
           (dlgTst.Event, dlgTst.FireInstance),
           (IronPythonTest.DelegateTest.StaticGenericEvent, IronPythonTest.DelegateTest.FireGenericStatic),
           (dlgTst.GenericEvent, dlgTst.FireGeneric)]:
    event, fire = x[0], x[1]
    
    event += Handler
    
    glblSelf = glblSelf + 1
    glblArgs = EventArgs()
    handlerCalled = False
    
    fire(glblSelf, glblArgs)
        
    AreEqual(handlerCalled, True)
    
    event -= Handler

def ParamsHandler(self, args):
    global glblSelf, glblArgs, handlerCalled
    
    AreEqual(self, glblSelf)
    AreEqual(tuple(args), tuple(range(glblArgs)))
    handlerCalled = True

for x in [(IronPythonTest.DelegateTest.StaticParamsEvent, IronPythonTest.DelegateTest.FireParamsStatic),
           (dlgTst.ParamsEvent, dlgTst.FireParams)]:
    event, fire = x[0], x[1]
    
    event += ParamsHandler
    
    glblSelf = glblSelf + 1
    
    for x in range(6):
        handlerCalled = False
        
        glblArgs = x
        fire(glblSelf, *tuple(range(x)))
        
        AreEqual(handlerCalled, True)
        
    event -= ParamsHandler

def BigParamsHandler(self, a, b, c, d, args):
    global glblSelf, glblArgs, handlerCalled
    
    AreEqual(self, glblSelf)
    AreEqual(tuple(args), tuple(range(glblArgs)))
    AreEqual(a, 1)
    AreEqual(b, 2)
    AreEqual(c, 3)
    AreEqual(d, 4)
    handlerCalled = True

for x in [(IronPythonTest.DelegateTest.StaticBigParamsEvent, IronPythonTest.DelegateTest.FireBigParamsStatic),
           (dlgTst.BigParamsEvent, dlgTst.FireBigParams)]:
    event, fire = x[0], x[1]
    
    event += BigParamsHandler
    
    glblSelf = glblSelf + 1
    
    for x in range(6):
        handlerCalled = False
        
        glblArgs = x
        fire(glblSelf, 1, 2, 3, 4, *tuple(range(x)))
        
        AreEqual(handlerCalled, True)

    event -= BigParamsHandler

# out param
def OutHandler(sender, ret):
    global glblSelf, handlerCalled
    
    AreEqual(sender, glblSelf)
    handlerCalled = True

    ret.Value = 23
    
for x in [(IronPythonTest.DelegateTest.StaticOutEvent, IronPythonTest.DelegateTest.FireOutStatic),
           (dlgTst.OutEvent, dlgTst.FireOut)]:
    event, fire = x[0], x[1]
    
    event += OutHandler
    
    glblSelf = glblSelf + 1
    
    handlerCalled = False
    
    AreEqual(fire(glblSelf), 23)
    
    AreEqual(handlerCalled, True)
    
    event -= OutHandler

# ref param
def RefHandler(sender, refArg):
    global glblSelf, handlerCalled
    
    AreEqual(sender, glblSelf)
    AreEqual(refArg.Value, 42)
    handlerCalled = True
    
    refArg.Value = 23
    
for x in [(IronPythonTest.DelegateTest.StaticRefEvent, IronPythonTest.DelegateTest.FireRefStatic),
           (dlgTst.RefEvent, dlgTst.FireRef)]:
    event, fire = x[0], x[1]
    
    event += RefHandler
    
    glblSelf = glblSelf + 1
    
    handlerCalled = False
    
    AreEqual(fire(glblSelf, 42), 23)
    
    AreEqual(handlerCalled, True)
    
    event -= RefHandler

# out w/ return type
def OutHandler(sender, ret):
    global glblSelf, handlerCalled
    
    AreEqual(sender, glblSelf)
    handlerCalled = True

    ret.Value = 42
    
    return "23"
    
for x in [(IronPythonTest.DelegateTest.StaticOutReturnEvent, IronPythonTest.DelegateTest.FireOutReturnStatic),
           (dlgTst.OutReturnEvent, dlgTst.FireOutReturn)]:
    event, fire = x[0], x[1]
    
    event += OutHandler
    
    glblSelf = glblSelf + 1
    
    handlerCalled = False
    
    AreEqual(fire(glblSelf), ("23", 42))
    
    AreEqual(handlerCalled, True)
    
    event -= OutHandler
    
# ref w/ a return type
def RefHandler(sender, refArg):
    global glblSelf, handlerCalled
    
    AreEqual(sender, glblSelf)
    AreEqual(refArg.Value, 42)
    handlerCalled = True

    refArg.Value = 42
    return 23
    
for x in [(IronPythonTest.DelegateTest.StaticRefReturnEvent, IronPythonTest.DelegateTest.FireRefReturnStatic),
           (dlgTst.RefReturnEvent, dlgTst.FireRefReturn)]:
    event, fire = x[0], x[1]
    
    event += RefHandler
    
    glblSelf = glblSelf + 1
    
    handlerCalled = False
    
    AreEqual(fire(glblSelf, 42), (23, 42))
    
    AreEqual(handlerCalled, True)
    
    event -= RefHandler


#######

def identity(x): return x

r = IronPythonTest.ReturnTypes()
r.floatEvent += identity

import System
AreEqual(r.RunFloat(1.4), System.Single(1.4))

################################################################################################
# verify bound / unbound methods go to the right delegates...
# ParameterizedThreadStart vs ThreadStart is a good example of this, we have a delegate
# that takes a parameter, and one that doesn't, and we need to correctly disambiguiate

class foo(object):
    def bar(self):
        global called, globalSelf
        called = True
        globalSelf = self
    def baz(self, arg):
        global called, globalSelf, globalArg
        called = True
        globalSelf = self
        globalArg = arg

if not is_silverlight:
    # try parameterized thread
    a = foo()
    t = Thread(ParameterizedThreadStart(foo.bar))
    t.Start(a)
    t.Join()

    AreEqual(called, True)
    AreEqual(globalSelf, a)

# try non-parameterized
a = foo()
called = False

t = Thread(ThreadStart(a.bar))
t.Start()
t.Join()

AreEqual(called, True)
AreEqual(globalSelf, a)

if not is_silverlight:
    # parameterized w/ self
    a = foo()
    called = False

    t = Thread(ParameterizedThreadStart(a.baz))
    t.Start('hello')
    t.Join()

    AreEqual(called, True)
    AreEqual(globalSelf, a)
    AreEqual(globalArg, 'hello')

if not is_silverlight:
    # parameterized w/ self & extra arg, should throw
    try:
        pts = ParameterizedThreadStart(foo.baz)
        pts("Hello")
        AssertUnreachable()
    except TypeError: pass

# SuperDelegate Tests

def test_basic():
    class TestCounter:
        myhandler = 0
        myhandler2 = 0
        trivial_d = 0
        trivial_d2 = 0
        string_d = 0
        enum_d = 0
        long_d = 0
        complex_d = 0
        structhandler = 0

    counter = TestCounter()

    class MyHandler:
        def __init__(self, x):
            self.y = x
        def att(self, s1):
            Assert(self.y == "Hi from Python!")
            Assert(s1 == "string value")
            counter.myhandler += 1
            return ""
        def att2(self, s1):
            Assert(self.y == "Hi from Python!")
            Assert(s1 == "second string value")
            counter.myhandler2 += 1
            return ""

    def handle_struct(s):
        Assert(s.intVal == 12345)
        counter.structhandler += 1
        return s

    def att(s1):
        Assert(s1 == "string value")
        counter.trivial_d += 1
        return ""

    def att2(s1):
        Assert(s1 == "second string value")
        counter.trivial_d2 += 1
        return ""

    def ats(s1, s2, s3, s4, s5, s6):
        Assert(s1 == "string value")
        Assert(s2 == "second string value")
        Assert(s3 == "string value")
        Assert(s4 == "second string value")
        Assert(s5 == "string value")
        Assert(s6 == "second string value")
        counter.string_d += 1
        return ""

    def ate(e):
        Assert(e == IronPythonTest.DeTestEnum.Value_2)
        counter.enum_d += 1
        return 1;

    def atel(e):
        Assert(e == IronPythonTest.DeTestEnumLong.Value_1)
        counter.long_d += 1
        return 1;

    def atc(s, i, f, d, e, s2, e2, e3):
        Assert(s == "string value")
        Assert(i == 12345)
        Assert(f == 3.5)
        Assert(d == 3.141592653)
        Assert(e == IronPythonTest.DeTestEnum.Value_2)
        Assert(s2 == "second string value")
        Assert(e2 == IronPythonTest.DeTestEnum.Value_2)
        Assert(e3 == IronPythonTest.DeTestEnumLong.Value_1)
        counter.complex_d += 1
        return s

    d = IronPythonTest.DeTest()

    d.stringVal = "string value"
    d.stringVal2 = "second string value"
    d.intVal    = 12345
    d.floatVal  = 3.5
    d.doubleVal = 3.141592653
    d.enumVal   = IronPythonTest.DeTestEnum.Value_2
    d.longEnumVal = IronPythonTest.DeTestEnumLong.Value_1

    d.e_tt += att
    d.e_tt2 += att2
    d.e_tsd += ats
    d.e_ted += ate
    d.e_tedl += atel
    d.e_tcd += atc
    d.RunTest()

    c = MyHandler("Hi from Python!")
    d.e_tt += c.att
    d.e_tt2 += c.att2
    d.e_struct += handle_struct
    d.RunTest()

    Assert(counter.myhandler == 1)
    Assert(counter.myhandler2 == 1)
    Assert(counter.trivial_d == 2 )
    Assert(counter.trivial_d2 == 2 )
    Assert(counter.string_d == 2)
    Assert(counter.enum_d == 2)
    Assert(counter.long_d == 2)
    Assert(counter.complex_d == 2)
    Assert(counter.structhandler == 1)

    d.e_tt -= att
    d.e_tt2 -= att2
    d.e_tsd -= ats
    d.e_ted -= ate
    d.e_tedl -= atel
    d.e_tcd -= atc
    d.e_tt -= c.att
    d.e_tt2 -= c.att2
    d.e_struct -= handle_struct

    d.RunTest()

    # even though they're different methods we should have succeeded at removing them
    Assert(counter.myhandler == 1)
    Assert(counter.myhandler2 == 1)

    #All the rest of the event handlers are removed correctly
    Assert(counter.trivial_d == 2 )
    Assert(counter.trivial_d2 == 2 )
    Assert(counter.string_d == 2)
    Assert(counter.enum_d == 2)
    Assert(counter.long_d == 2)
    Assert(counter.complex_d == 2)
    Assert(counter.structhandler == 1)

###############################################################
##    Event Handler Add / Removal
###############################################################

def test_event_handler_add_removal_sequence():
    class C: pass
    c = C()
    c.flag = 0

    do_remove = -1
    do_nothing = 0
    do_add = 1

    def f1(): c.flag += 10
    def f2(): c.flag += 20

    class D:
        def f1(self): c.flag += 30
        def f2(self): c.flag += 40
    d = D()

    def Step(obj, action, handler, expected):
        if action == do_remove:
            obj.SimpleEvent -= handler
        elif action == do_add:
            obj.SimpleEvent += handler

        c.flag = 0
        obj.RaiseEvent()
        Assert(c.flag == expected)

    def RunSequence(listOfSteps):
        newobj = IronPythonTest.SimpleType()
        expected = 0
        for step in listOfSteps:
            (action, handler, delta) = step
            expected += delta
            Step(newobj, action, handler, expected)

    ls = []
    ls.append((do_nothing, None, 0))
    ls.append((do_add, f1, 10))
    ls.append((do_remove, f2, 0)) ## remove not-added handler
    ls.append((do_remove, f1, -10))
    ls.append((do_remove, f1, 0)) ## remove again

    RunSequence(ls)

    ## Two events add/remove

    ls = []
    ls.append((do_add, f1, 10))
    ls.append((do_add, f2, 20))
    ls.append((do_remove, f1, -10))
    ls.append((do_remove, f2, -20))
    ls.append((do_remove, f1, 0))

    RunSequence(ls)

    ## Two events add/remove (different order)

    ls = []
    ls.append((do_add, f1, 10))
    ls.append((do_add, f2, 20))
    ls.append((do_remove, f2, -20))
    ls.append((do_remove, f1, -10))

    RunSequence(ls)

    ## Event handler is function in class instance

    ls = []
    ls.append((do_add, d.f1, 30))
    ls.append((do_add, d.f2, 40))
    ls.append((do_remove, d.f2, -40))
    ls.append((do_remove, d.f1, -30))

    RunSequence(ls)

    ls = []
    ls.append((do_add, d.f2, 40))
    ls.append((do_add, d.f1, 30))
    ls.append((do_remove, d.f2, -40))
    ls.append((do_remove, d.f1, -30))

    RunSequence(ls)


    ls = []
    ls.append((do_add, f1, 10))
    ls.append((do_remove, f2, 0))
    ls.append((do_add, d.f2, 40))
    ls.append((do_add, f2, 20))
    ls.append((do_remove, d.f2, -40))
    ls.append((do_add, d.f1, 30))
    ls.append((do_nothing, None, 0))
    ls.append((do_remove, f1, -10))
    ls.append((do_remove, d.f1, -30))

    RunSequence(ls)

# verify delegates are calling

myfuncCalled = False
passedarg = None
def test_handler_get_invoked():
    def myfunc():
        global myfuncCalled
        myfuncCalled = True
        
    def myotherfunc(arg):
        global myfuncCalled, passedarg
        myfuncCalled = True
        passedarg = arg

    class myclass(object):
        def myfunc(self):
            global myfuncCalled
            myfuncCalled = True
        def myotherfunc(self, arg):
            global myfuncCalled,passedarg
            myfuncCalled = True
            passedarg = arg

    class myoldclass:
        def myfunc(self):
            global myfuncCalled
            myfuncCalled = True
        def myotherfunc(self, arg):
            global myfuncCalled, passedarg
            myfuncCalled = True
            passedarg = arg

    global myfuncCalled, passedarg

    for target in [myfunc, myclass().myfunc, myoldclass().myfunc]:
        myfuncCalled = False
        ThreadStart(target)()
        AreEqual(myfuncCalled, True)

    for target in [myotherfunc, myclass().myotherfunc, myoldclass().myotherfunc]:
        myfuncCalled = False
        passedarg = None
        if is_silverlight:
            break
            
        ParameterizedThreadStart(target)(1)
        AreEqual(myfuncCalled, True)
        AreEqual(passedarg, 1)

    # verify we can call a delegate that's bound to a static method
    IronPythonTest.DelegateTest.Simple()

    # Untyped delegate. System.Windows.Forms.Control.Invoke(Delegate) is an example of such an API
    AssertError(TypeError, IronPythonTest.DelegateTest.InvokeUntypedDelegate, myfunc)

    myfuncCalled = False
    IronPythonTest.DelegateTest.InvokeUntypedDelegate(IronPythonTest.SimpleDelegate(myfunc))
    AreEqual(myfuncCalled, True)

    myfuncCalled = False
    passedarg = 0
    IronPythonTest.DelegateTest.InvokeUntypedDelegate(IronPythonTest.SimpleDelegateWithOneArg(myotherfunc), 100)
    AreEqual((myfuncCalled, passedarg), (True, 100))

def test_error_message():
    def func(a, b, c, d, e): pass
    import System
    try:
        ts =  ThreadStart(func)
        ts()
        AssertUnreachable()
    except TypeError:
        pass

called = False
def test_python_code_as_event_handler():
    global called

    a = IronPythonTest.Events()
    def MyEventHandler():
        global called
        called = True

    called = False
    a.InstanceTest += IronPythonTest.EventTestDelegate(MyEventHandler)
    a.CallInstance()
    AreEqual(called, True)

    called = False
    IronPythonTest.Events.StaticTest += IronPythonTest.EventTestDelegate(MyEventHandler)
    IronPythonTest.Events.CallStatic()
    AreEqual(called, True)

    called = False
    def myhandler(*args):
        global called
        called = True
        AreEqual(args[0], 'abc')
        AreEqual(args[1], EventArgs.Empty)
        
    IronPythonTest.Events.OtherStaticTest += myhandler
    IronPythonTest.Events.CallOtherStatic('abc', EventArgs.Empty)
    AreEqual(called, True)
    IronPythonTest.Events.OtherStaticTest -= myhandler

def test_AddDelegateFromCSharpAndRemoveFromPython():
    a = IronPythonTest.Events()

    a.Marker = False
    a.AddSetMarkerDelegateToInstanceTest()
    a.InstanceTest -= IronPythonTest.EventTestDelegate(a.SetMarker)
    a.CallInstance()
    AreEqual(a.Marker, False)

    IronPythonTest.Events.StaticMarker = False
    IronPythonTest.Events.AddSetMarkerDelegateToStaticTest()
    IronPythonTest.Events.StaticTest -= IronPythonTest.EventTestDelegate(IronPythonTest.Events.StaticSetMarker)
    IronPythonTest.Events.CallStatic()
    AreEqual(IronPythonTest.Events.StaticMarker, False)

def test_AddRemoveDelegateFromPython():
    a = IronPythonTest.Events()

    a.Marker = False
    a.InstanceTest += IronPythonTest.EventTestDelegate(a.SetMarker)
    a.InstanceTest -= IronPythonTest.EventTestDelegate(a.SetMarker)
    a.CallInstance()
    AreEqual(a.Marker, False)

def test_event_as_attribute_disallowed_ops():
    a = IronPythonTest.Events()
    
    def f1(): del a.InstanceTest
    def f2(): a.InstanceTest = 'abc'
    def f3(): IronPythonTest.Events.StaticTest = 'abc'
    
    for x in [f1, f2, f3]:
        AssertError(AttributeError, x)

@skip("silverlight") # no weakref module
def test_event_lifetime():
    """ensures that circular references between an event on an instance
       and the handling delegate don't call leaks and don't release too soon"""
    def keep_alive(o): pass
    
    def test_runner():
        import _weakref
        global called
        called = 0
        a = IronPythonTest.Events()
        
        # wire up an event w/ a circular reference
        # back to the object, ensure the event
        # is delivered
        def foo():
            global called
            called += 1

        foo.abc = a
        a.InstanceTest += foo
        
        a.CallInstance()
        AreEqual(called, 1)
        
        ret_val = _weakref.ref(foo)
        #Assert(hasattr(ret_val, "abc")) #BUG
        keep_alive(foo)
        
        # ensure as long as the objects are still alive the event
        # handler remains alive
        import gc
        for i in range(10):
            gc.collect()
            
        a.CallInstance()
        AreEqual(called, 2)
        
        return ret_val

    func_ref = test_runner()

    # now all references are dead, the function should be collectible
    import gc
    for i in range(10):
        gc.collect()
    
    Assert(not hasattr(func_ref, "abc"))
    AreEqual(func_ref(), None)
    AreEqual(called, 2)

def test_reflected_event_ops():
    '''
    Test to hit IronPython.Runtime.Operations.ReflectedEventOps
    
    Needs more cases (__set__, __delete__).
    '''
    
    #__str__
    AreEqual(str(IronPythonTest.Events.StaticTest.Event),
             "<event# StaticTest on Events>")
    
    #__set__, __delete__
    t_list = [  IronPythonTest.Events.StaticTest.Event,
                IronPythonTest.Events().InstanceTest.Event,
                ]
                
    for stuff in t_list:
        for inst, val in [(None, None), (1, None), (None, 1), (1, 1), ("abc", "xyz")]:
            AssertError(AttributeError,
                        stuff.__set__,
                        inst, val)
                                
            AssertError(AttributeError,
                        stuff.__delete__,
                        inst)
                    
    AssertError(AttributeError,
                IronPythonTest.Events.StaticTest.Event.__set__,
                None, IronPythonTest.Events().InstanceTest)
                
    AssertError(AttributeError,
                IronPythonTest.Events.StaticTest.Event.__delete__,
                IronPythonTest.Events().InstanceTest)
                    
    for stuff in [ None, 1, "abc"]:
        #Just ensure it doesn't throw
        IronPythonTest.Events.StaticTest.Event.__set__(stuff, IronPythonTest.Events.StaticTest)

def test_strongly_typed_events():
    """verify recreating a strongly typed delegate doesn't prevent
       us from removing the event
   """
    def f():
        global called
        called = True
    
    global called
    called = False
    ev = IronPythonTest.Events() 
    ev.InstanceTest += IronPythonTest.EventTestDelegate(f)
    
    ev.CallInstance(); AreEqual(called, True); called = False;

    ev.InstanceTest -= IronPythonTest.EventTestDelegate(f)
    ev.CallInstance(); AreEqual(called, False); 


def test_bound_builtin_functions():
    d  = {}
    func = d.setdefault    
    args = EventArgs()    
    ev = IronPythonTest.Events() 
    
    
    ev.InstanceOther += func
    ev.CallOtherInstance('abc', args)    
    AreEqual(d, {'abc': args})
    
    ev.InstanceOther -= func
    ev.CallOtherInstance('def', args)
    AreEqual(d, {'abc': args})

    d.clear()

    ev.InstanceOther += IronPythonTest.OtherEvent(func)
    ev.CallOtherInstance('abc', args)    
    AreEqual(d, {'abc': args})
    
    ev.InstanceOther -= IronPythonTest.OtherEvent(func)
    ev.CallOtherInstance('def', args)
    AreEqual(d, {'abc': args})

def test_delegate___call__():
    import System
    global received
    def f(*args):
        global received
        received = args
    
    x = System.EventHandler(f)
    
    x.__call__('abc', None)
    AreEqual(received, ('abc', None))
    x.__call__(sender = 42, e = None)
    AreEqual(received, (42, None))
    Assert('__call__' in dir(x))
    
def test_combine():
    """in-place addition on delegates maps to Delegate.Combine"""
    global log
    def f(sender, args):
        global log
        log += 'f'
        
    def g(sender, args):
        global log
        log += 'g'
            
    fe = System.EventHandler(f)
    ge = System.EventHandler(g)
    
    fege = fe
    fege += ge
    
    log = ''
    fege(None, None)
    AreEqual(log, 'fg')
    
    log = ''
    fe(None, None)
    AreEqual(log, 'f')
    
    log = ''
    ge(None, None)
    AreEqual(log, 'g')
    
    fege -= ge
    log = ''
    fege(None, None)
    AreEqual(log, 'f')
    
run_test(__name__)
