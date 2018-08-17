# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import unittest

from iptest import IronPythonTestCase, is_cli, is_netcoreapp, is_netcoreapp20, is_posix, run_test, skipUnlessIronPython

myfuncCalled = False
passedarg = None
called = False
selph = None

def MyTick(state):
    global superTimer
    selph.assertEqual(state, superTimer)
    are.Set()

def SimpleHandler(sender, args):
    from System.Threading import Timer
    global superTimer
    superTimer = Timer(MyTick)
    superTimer.Change(1000, 0)

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


if is_cli:
    import clr

    if is_netcoreapp:
        clr.AddReference("System.Threading.Thread")

    from System import EventArgs
    from System.Threading import AutoResetEvent

    are = AutoResetEvent(False)


@skipUnlessIronPython()
class DelegateTest(IronPythonTestCase):
    def setUp(self):
        super(DelegateTest, self).setUp()
        global selph
        self.load_iron_python_test()
        import IronPythonTest

        selph = self

    def test_SimpleHandler(self):
        import IronPythonTest
        dlgTst = IronPythonTest.DelegateTest()
        dlgTst.Event += SimpleHandler

        dlgTst.FireInstance(None, EventArgs.Empty)
        are.WaitOne()
        superTimer.Dispose()

    def test_delegate_combinations(self):
        """test various combinations of delegates..."""
        import IronPythonTest
        global glblSelf, glblArgs, handlerCalled
        dlgTst = IronPythonTest.DelegateTest()

        def Handler(slf, args):
            global glblSelf, glblArgs, handlerCalled

            self.assertEqual(slf, glblSelf)
            self.assertEqual(args, glblArgs)
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

            self.assertEqual(handlerCalled, True)

            event -= Handler

        def ParamsHandler(slf, args):
            global glblSelf, glblArgs, handlerCalled

            self.assertEqual(slf, glblSelf)
            self.assertEqual(tuple(args), tuple(range(glblArgs)))
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

                self.assertEqual(handlerCalled, True)

            event -= ParamsHandler

        def BigParamsHandler(slf, a, b, c, d, args):
            global glblSelf, glblArgs, handlerCalled

            self.assertEqual(slf, glblSelf)
            self.assertEqual(tuple(args), tuple(range(glblArgs)))
            self.assertEqual(a, 1)
            self.assertEqual(b, 2)
            self.assertEqual(c, 3)
            self.assertEqual(d, 4)
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

                self.assertEqual(handlerCalled, True)

            event -= BigParamsHandler

        # out param
        def OutHandler(sender, ret):
            global glblSelf, handlerCalled

            self.assertEqual(sender, glblSelf)
            handlerCalled = True

            ret.Value = 23

        for x in [(IronPythonTest.DelegateTest.StaticOutEvent, IronPythonTest.DelegateTest.FireOutStatic),
                (dlgTst.OutEvent, dlgTst.FireOut)]:
            event, fire = x[0], x[1]

            event += OutHandler

            glblSelf = glblSelf + 1

            handlerCalled = False

            self.assertEqual(fire(glblSelf), 23)

            self.assertEqual(handlerCalled, True)

            event -= OutHandler

        # ref param
        def RefHandler(sender, refArg):
            global glblSelf, handlerCalled

            self.assertEqual(sender, glblSelf)
            self.assertEqual(refArg.Value, 42)
            handlerCalled = True

            refArg.Value = 23

        for x in [(IronPythonTest.DelegateTest.StaticRefEvent, IronPythonTest.DelegateTest.FireRefStatic),
                (dlgTst.RefEvent, dlgTst.FireRef)]:
            event, fire = x[0], x[1]

            event += RefHandler

            glblSelf = glblSelf + 1

            handlerCalled = False

            self.assertEqual(fire(glblSelf, 42), 23)

            self.assertEqual(handlerCalled, True)

            event -= RefHandler

        # out w/ return type
        def OutHandler(sender, ret):
            global glblSelf, handlerCalled

            self.assertEqual(sender, glblSelf)
            handlerCalled = True

            ret.Value = 42

            return "23"

        for x in [(IronPythonTest.DelegateTest.StaticOutReturnEvent, IronPythonTest.DelegateTest.FireOutReturnStatic),
                (dlgTst.OutReturnEvent, dlgTst.FireOutReturn)]:
            event, fire = x[0], x[1]

            event += OutHandler

            glblSelf = glblSelf + 1

            handlerCalled = False

            self.assertEqual(fire(glblSelf), ("23", 42))

            self.assertEqual(handlerCalled, True)

            event -= OutHandler

        # ref w/ a return type
        def RefHandler(sender, refArg):
            global glblSelf, handlerCalled

            self.assertEqual(sender, glblSelf)
            self.assertEqual(refArg.Value, 42)
            handlerCalled = True

            refArg.Value = 42
            return 23

        for x in [(IronPythonTest.DelegateTest.StaticRefReturnEvent, IronPythonTest.DelegateTest.FireRefReturnStatic),
                (dlgTst.RefReturnEvent, dlgTst.FireRefReturn)]:
            event, fire = x[0], x[1]

            event += RefHandler

            glblSelf = glblSelf + 1

            handlerCalled = False

            self.assertEqual(fire(glblSelf, 42), (23, 42))

            self.assertEqual(handlerCalled, True)

            event -= RefHandler


    def test_identity(self):
        import IronPythonTest
        from System.Threading import ParameterizedThreadStart, Thread, ThreadStart
        global called
        global globalSelf
        def identity(x): return x

        r = IronPythonTest.ReturnTypes()
        r.floatEvent += identity

        import System
        self.assertEqual(r.RunFloat(1.4), System.Single(1.4))


        # try parameterized thread
        a = foo()
        t = Thread(ParameterizedThreadStart(foo.bar))
        t.Start(a)
        t.Join()

        self.assertEqual(called, True)
        self.assertEqual(globalSelf, a)

        # try non-parameterized
        a = foo()
        called = False

        t = Thread(ThreadStart(a.bar))
        t.Start()
        t.Join()

        self.assertEqual(called, True)
        self.assertEqual(globalSelf, a)

        # parameterized w/ self
        a = foo()
        called = False

        t = Thread(ParameterizedThreadStart(a.baz))
        t.Start('hello')
        t.Join()

        self.assertEqual(called, True)
        self.assertEqual(globalSelf, a)
        self.assertEqual(globalArg, 'hello')

        # parameterized w/ self & extra arg, should throw
        try:
            pts = ParameterizedThreadStart(foo.baz)
            pts("Hello")
            self.assertUnreachable()
        except TypeError: pass

# SuperDelegate Tests

    @unittest.skipIf(is_netcoreapp20 and is_posix, "bug with .NET Core 2.0")
    def test_basic(self):
        import IronPythonTest
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

        s = self
        class MyHandler:
            def __init__(self, x):
                self.y = x
            def att(self, s1):
                s.assertTrue(self.y == "Hi from Python!")
                s.assertTrue(s1 == "string value")
                counter.myhandler += 1
                return ""
            def att2(self, s1):
                s.assertTrue(self.y == "Hi from Python!")
                s.assertTrue(s1 == "second string value")
                counter.myhandler2 += 1
                return ""

        def handle_struct(s):
            self.assertTrue(s.intVal == 12345)
            counter.structhandler += 1
            return s

        def att(s1):
            self.assertTrue(s1 == "string value")
            counter.trivial_d += 1
            return ""

        def att2(s1):
            self.assertTrue(s1 == "second string value")
            counter.trivial_d2 += 1
            return ""

        def ats(s1, s2, s3, s4, s5, s6):
            self.assertTrue(s1 == "string value")
            self.assertTrue(s2 == "second string value")
            self.assertTrue(s3 == "string value")
            self.assertTrue(s4 == "second string value")
            self.assertTrue(s5 == "string value")
            self.assertTrue(s6 == "second string value")
            counter.string_d += 1
            return ""

        def ate(e):
            self.assertTrue(e == IronPythonTest.DeTestEnum.Value_2)
            counter.enum_d += 1
            return 1

        def atel(e):
            self.assertTrue(e == IronPythonTest.DeTestEnumLong.Value_1)
            counter.long_d += 1
            return 1

        def atc(s, i, f, d, e, s2, e2, e3):
            self.assertTrue(s == "string value")
            self.assertTrue(i == 12345)
            self.assertTrue(f == 3.5)
            self.assertTrue(d == 3.141592653)
            self.assertTrue(e == IronPythonTest.DeTestEnum.Value_2)
            self.assertTrue(s2 == "second string value")
            self.assertTrue(e2 == IronPythonTest.DeTestEnum.Value_2)
            self.assertTrue(e3 == IronPythonTest.DeTestEnumLong.Value_1)
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

        self.assertTrue(counter.myhandler == 1)
        self.assertTrue(counter.myhandler2 == 1)
        self.assertTrue(counter.trivial_d == 2 )
        self.assertTrue(counter.trivial_d2 == 2 )
        self.assertTrue(counter.string_d == 2)
        self.assertTrue(counter.enum_d == 2)
        self.assertTrue(counter.long_d == 2)
        self.assertTrue(counter.complex_d == 2)
        self.assertTrue(counter.structhandler == 1)

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
        self.assertTrue(counter.myhandler == 1)
        self.assertTrue(counter.myhandler2 == 1)

        #All the rest of the event handlers are removed correctly
        self.assertTrue(counter.trivial_d == 2 )
        self.assertTrue(counter.trivial_d2 == 2 )
        self.assertTrue(counter.string_d == 2)
        self.assertTrue(counter.enum_d == 2)
        self.assertTrue(counter.long_d == 2)
        self.assertTrue(counter.complex_d == 2)
        self.assertTrue(counter.structhandler == 1)

    ###############################################################
    ##    Event Handler Add / Removal
    ###############################################################

    def test_event_handler_add_removal_sequence(self):
        import IronPythonTest
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
            self.assertTrue(c.flag == expected)

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

    def test_handler_get_invoked(self):
        import IronPythonTest
        from System.Threading import ParameterizedThreadStart, ThreadStart
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
            self.assertEqual(myfuncCalled, True)

        for target in [myotherfunc, myclass().myotherfunc, myoldclass().myotherfunc]:
            myfuncCalled = False
            passedarg = None

            ParameterizedThreadStart(target)(1)
            self.assertEqual(myfuncCalled, True)
            self.assertEqual(passedarg, 1)

        # verify we can call a delegate that's bound to a static method
        IronPythonTest.DelegateTest.Simple()

        # Untyped delegate. System.Windows.Forms.Control.Invoke(Delegate) is an example of such an API
        self.assertRaises(TypeError, IronPythonTest.DelegateTest.InvokeUntypedDelegate, myfunc)

        myfuncCalled = False
        IronPythonTest.DelegateTest.InvokeUntypedDelegate(IronPythonTest.SimpleDelegate(myfunc))
        self.assertEqual(myfuncCalled, True)

        myfuncCalled = False
        passedarg = 0
        IronPythonTest.DelegateTest.InvokeUntypedDelegate(IronPythonTest.SimpleDelegateWithOneArg(myotherfunc), 100)
        self.assertEqual((myfuncCalled, passedarg), (True, 100))

    def test_error_message(self):
        def func(a, b, c, d, e): pass
        from System.Threading import ThreadStart
        ts =  ThreadStart(func)
        self.assertRaises(TypeError, ts)

    def test_python_code_as_event_handler(self):
        import IronPythonTest
        global called

        a = IronPythonTest.Events()
        def MyEventHandler():
            global called
            called = True

        called = False
        a.InstanceTest += IronPythonTest.EventTestDelegate(MyEventHandler)
        a.CallInstance()
        self.assertEqual(called, True)

        called = False
        IronPythonTest.Events.StaticTest += IronPythonTest.EventTestDelegate(MyEventHandler)
        IronPythonTest.Events.CallStatic()
        self.assertEqual(called, True)

        called = False
        def myhandler(*args):
            global called
            called = True
            self.assertEqual(args[0], 'abc')
            self.assertEqual(args[1], EventArgs.Empty)

        IronPythonTest.Events.OtherStaticTest += myhandler
        IronPythonTest.Events.CallOtherStatic('abc', EventArgs.Empty)
        self.assertEqual(called, True)
        IronPythonTest.Events.OtherStaticTest -= myhandler

    def test_AddDelegateFromCSharpAndRemoveFromPython(self):
        import IronPythonTest
        a = IronPythonTest.Events()

        a.Marker = False
        a.AddSetMarkerDelegateToInstanceTest()
        a.InstanceTest -= IronPythonTest.EventTestDelegate(a.SetMarker)
        a.CallInstance()
        self.assertEqual(a.Marker, False)

        IronPythonTest.Events.StaticMarker = False
        IronPythonTest.Events.AddSetMarkerDelegateToStaticTest()
        IronPythonTest.Events.StaticTest -= IronPythonTest.EventTestDelegate(IronPythonTest.Events.StaticSetMarker)
        IronPythonTest.Events.CallStatic()
        self.assertEqual(IronPythonTest.Events.StaticMarker, False)

    def test_AddRemoveDelegateFromPython(self):
        import IronPythonTest
        a = IronPythonTest.Events()

        a.Marker = False
        a.InstanceTest += IronPythonTest.EventTestDelegate(a.SetMarker)
        a.InstanceTest -= IronPythonTest.EventTestDelegate(a.SetMarker)
        a.CallInstance()
        self.assertEqual(a.Marker, False)

    def test_event_as_attribute_disallowed_ops(self):
        import IronPythonTest
        a = IronPythonTest.Events()

        def f1(): del a.InstanceTest
        def f2(): a.InstanceTest = 'abc'
        def f3(): IronPythonTest.Events.StaticTest = 'abc'

        for x in [f1, f2, f3]:
            self.assertRaises(AttributeError, x)

    def test_event_lifetime(self):
        """ensures that circular references between an event on an instance
        and the handling delegate don't call leaks and don't release too soon"""
        import IronPythonTest
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
            self.assertEqual(called, 1)

            ret_val = _weakref.ref(foo)
            #self.assertTrue(hasattr(ret_val, "abc")) #BUG
            keep_alive(foo)

            # ensure as long as the objects are still alive the event
            # handler remains alive
            import gc
            for i in xrange(10):
                gc.collect()

            a.CallInstance()
            self.assertEqual(called, 2)

            return ret_val

        func_ref = test_runner()

        # now all references are dead, the function should be collectible
        import gc
        for i in xrange(10):
            gc.collect()

        self.assertTrue(not hasattr(func_ref, "abc"))
        self.assertEqual(func_ref(), None)
        self.assertEqual(called, 2)

    def test_reflected_event_ops(self):
        '''
        Test to hit IronPython.Runtime.Operations.ReflectedEventOps

        Needs more cases (__set__, __delete__).
        '''
        import IronPythonTest

        #__str__
        self.assertEqual(str(IronPythonTest.Events.StaticTest.Event),
                "<event# StaticTest on Events>")

        #__set__, __delete__
        t_list = [  IronPythonTest.Events.StaticTest.Event,
                    IronPythonTest.Events().InstanceTest.Event,
                    ]

        for stuff in t_list:
            for inst, val in [(None, None), (1, None), (None, 1), (1, 1), ("abc", "xyz")]:
                self.assertRaises(AttributeError,
                            stuff.__set__,
                            inst, val)

                self.assertRaises(AttributeError,
                            stuff.__delete__,
                            inst)

        self.assertRaises(AttributeError,
                    IronPythonTest.Events.StaticTest.Event.__set__,
                    None, IronPythonTest.Events().InstanceTest)

        self.assertRaises(AttributeError,
                    IronPythonTest.Events.StaticTest.Event.__delete__,
                    IronPythonTest.Events().InstanceTest)

        for stuff in [ None, 1, "abc"]:
            #Just ensure it doesn't throw
            IronPythonTest.Events.StaticTest.Event.__set__(stuff, IronPythonTest.Events.StaticTest)

    def test_strongly_typed_events(self):
        """verify recreating a strongly typed delegate doesn't prevent
        us from removing the event
        """
        import IronPythonTest
        def f():
            global called
            called = True

        global called
        called = False
        ev = IronPythonTest.Events()
        ev.InstanceTest += IronPythonTest.EventTestDelegate(f)

        ev.CallInstance(); self.assertEqual(called, True); called = False

        ev.InstanceTest -= IronPythonTest.EventTestDelegate(f)
        ev.CallInstance(); self.assertEqual(called, False)


    def test_bound_builtin_functions(self):
        import IronPythonTest
        d  = {}
        func = d.setdefault
        args = EventArgs()
        ev = IronPythonTest.Events()


        ev.InstanceOther += func
        ev.CallOtherInstance('abc', args)
        self.assertEqual(d, {'abc': args})

        ev.InstanceOther -= func
        ev.CallOtherInstance('def', args)
        self.assertEqual(d, {'abc': args})

        d.clear()

        ev.InstanceOther += IronPythonTest.OtherEvent(func)
        ev.CallOtherInstance('abc', args)
        self.assertEqual(d, {'abc': args})

        ev.InstanceOther -= IronPythonTest.OtherEvent(func)
        ev.CallOtherInstance('def', args)
        self.assertEqual(d, {'abc': args})

    def test_delegate___call__(self):
        import System
        global received
        def f(*args):
            global received
            received = args

        x = System.EventHandler(f)

        x.__call__('abc', None)
        self.assertEqual(received, ('abc', None))
        x.__call__(sender = 42, e = None)
        self.assertEqual(received, (42, None))
        self.assertTrue('__call__' in dir(x))

    def test_combine(self):
        """in-place addition on delegates maps to Delegate.Combine"""
        import System
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
        self.assertEqual(log, 'fg')

        log = ''
        fe(None, None)
        self.assertEqual(log, 'f')

        log = ''
        ge(None, None)
        self.assertEqual(log, 'g')

        fege -= ge
        log = ''
        fege(None, None)
        self.assertEqual(log, 'f')

run_test(__name__)
