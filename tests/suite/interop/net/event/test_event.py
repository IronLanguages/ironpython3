# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

'''
Operations on event type.
'''

import unittest

from iptest import IronPythonTestCase, run_test, skipUnlessIronPython

@skipUnlessIronPython()
class EventTest(IronPythonTestCase):
    def setUp(self):
        super(EventTest, self).setUp()
        self.add_clr_assemblies("eventdefinitions", "typesamples")

    def test_basic(self):
        from System import ApplicationException
        from Merlin.Testing import Flag
        from Merlin.Testing.Event import ClassImplicitlyImplementInterface, ClassWithSimpleEvent, Int32Int32Delegate, TargetClass, TargetStruct
        # t1 is where the event is declared
        for t1 in [  ClassImplicitlyImplementInterface,
                    #StructImplicitlyImplementInterface,  # bug: 361955
                    ClassWithSimpleEvent,
                    #StructWithSimpleEvent,               # bug: 361955
                ]:

            # t2 is where the handler is defined
            for t2 in [ TargetClass, TargetStruct ]:
                o = t2()

                # try both static and instance method
                for (double, square, negate, bad) in [
                        (t2.s_Double, t2.s_Square, t2.s_Negate, t2.s_Throw),
                        (o.i_Double, o.i_Square, o.i_Negate, o.i_Throw),
                    ]:

                    # no duplicate handlers, add/remove
                    x = t1()

                    Flag.Set(0)
                    self.assertEqual(x.CallInside(1), -1)
                    Flag.Check(0)

                    Flag.Set(0)
                    x.OnAction += double
                    self.assertEqual(x.CallInside(2), 4)
                    Flag.Check(1)

                    Flag.Set(0)
                    x.OnAction += square
                    self.assertEqual(x.CallInside(3), 9)
                    Flag.Check(101)

                    Flag.Set(0)
                    x.OnAction += negate
                    self.assertEqual(x.CallInside(4), -4)
                    Flag.Check(111)

                    Flag.Set(0)
                    x.OnAction -= square
                    self.assertEqual(x.CallInside(5), -5)
                    Flag.Check(11)

                    Flag.Set(0)
                    x.OnAction -= double
                    self.assertEqual(x.CallInside(6), -6)
                    Flag.Check(10)

                    Flag.Set(0)
                    x.OnAction -= negate
                    self.assertEqual(x.CallInside(7), -1)
                    Flag.Check(0)

                    # duplicate: which one get removed
                    x = t1()

                    x.OnAction += double
                    x.OnAction += square
                    x.OnAction += double
                    x.OnAction += double

                    Flag.Set(0)
                    self.assertEqual(x.CallInside(8), 16)
                    Flag.Check(103)

                    x.OnAction -= double
                    self.assertEqual(x.CallInside(9), 18)

                    x.OnAction -= double    # verify the last one is removed
                    Flag.Set(0)
                    self.assertEqual(x.CallInside(10), 100)  # bug 361971
                    Flag.Check(101)

                    x.OnAction -= double
                    self.assertEqual(x.CallInside(11), 121)

                    x.OnAction -= square
                    self.assertEqual(x.CallInside(12), -1)

                    # remove from empty invocation list
                    x.OnAction -= double
                    Flag.Set(0)
                    self.assertEqual(x.CallInside(13), -1)
                    Flag.Check(0)

                    # troubling event handler in the middle
                    x = t1()
                    x.OnAction += double
                    x.OnAction += bad
                    x.OnAction += negate

                    Flag.Set(0)
                    self.assertRaises(ApplicationException, lambda: x.CallInside(14))
                    Flag.Check(1)  # this also verified double was added/thus called first


                    # different handler handling path:
                    #  - explicitly created delegate objects (d_xxx)
                    #  - mixed

                    x = t1()

                    d_double = Int32Int32Delegate(double)
                    d_negate = Int32Int32Delegate(negate)
                    d_square = Int32Int32Delegate(square)

                    x.OnAction += d_double
                    x.OnAction += d_square
                    x.OnAction += double

                    Flag.Set(0)
                    self.assertEqual(x.CallInside(15), 30)
                    Flag.Check(102)

                    x.OnAction += d_negate
                    self.assertEqual(x.CallInside(16), -16)

                    x.OnAction -= d_square
                    Flag.Set(0)
                    self.assertEqual(x.CallInside(17), -17)
                    Flag.Check(12)

                    x.OnAction -= negate                # remove the "native"
                    self.assertEqual(x.CallInside(18), -18)

                    x.OnAction -= d_negate              # remove the 'stub'ed
                    self.assertEqual(x.CallInside(19), 38)

                    x.OnAction -= negate        # list is not empty, try to remove the not-in-list
                    x.OnAction -= d_negate      # same
                    self.assertEqual(x.CallInside(20), 40)

                    x.OnAction -= double
                    x.OnAction -= d_double
                    self.assertEqual(x.CallInside(21), -1)

    #TODO:@skip("multiple_execute")
    def test_static_event(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.Event import ClassWithStaticEvent, TargetClass, TargetStruct
        for t1 in [
                        ClassWithStaticEvent,
                        #StructWithStaticEvent
                ]:

            x = t1()

            tc, ts = TargetClass(), TargetStruct()

            t1.OnAction += TargetClass.s_Double
            t1.OnAction += TargetStruct.s_Negate
            t1.OnAction += tc.i_Square

            Flag.Set(0)
            self.assertEqual(x.CallInside(30), 900)
            Flag.Set(111)

            t1.OnAction += ts.i_Negate
            Flag.Set(0)
            self.assertEqual(x.CallInside(31), -31)
            Flag.Check(121)

            t1.OnAction -= TargetStruct.s_Negate
            t1.OnAction -= ts.i_Double  # not added before
            t1.OnAction -= tc.i_Square

            Flag.Set(0)
            self.assertEqual(x.CallInside(32), -32)
            Flag.Set(11)

            Flag.Set(0)
            t1.OnAction -= ts.i_Negate
            self.assertEqual(x.CallInside(33), 66)
            Flag.Set(1)

    def test_access_static_event_from_derived_type(self):
        from Merlin.Testing.Event import DerivedClassWithStaticEvent, TargetClass, TargetStruct
        def f1(): DerivedClassWithStaticEvent.OnAction += TargetClass.s_Double
        def f2(): DerivedClassWithStaticEvent.OnAction -= TargetStruct().s_Double

        for f in [f1, f2]:
            self.assertRaisesMessage(AttributeError,
                "attribute 'OnAction' of 'DerivedClassWithStaticEvent' object is read-only",
                f)

        #x = DerivedClassWithStaticEvent()
        #def f1(): x.OnAction += TargetClass.s_Double
        #def f2(): x.OnAction -= TargetStruct().s_Double
        #f1()

    def test_assignment(self):
        from Merlin.Testing.Event import ClassWithSimpleEvent, TargetClass
        x = ClassWithSimpleEvent()

        # 362440
        def f(): x.OnAction = TargetClass.s_Double
        self.assertRaisesMessage(AttributeError,
            "attribute 'OnAction' of 'ClassWithSimpleEvent' object is read-only",
            f)

    def test_add_sub(self):
        from Merlin.Testing.Event import ClassWithSimpleEvent, TargetClass
        x = ClassWithSimpleEvent()

        def f(): x.OnAction = x.OnAction + TargetClass.s_Negate
        self.assertRaisesMessage(TypeError,
            "unsupported operand type(s) for +: 'BoundEvent' and 'builtin_function_or_method'",
            f)

        def f(): x.OnAction - TargetClass.s_Negate
        self.assertRaisesMessage(TypeError,
            "unsupported operand type(s) for -: 'BoundEvent' and 'builtin_function_or_method'",
            f)

    def test_iadd_isub(self):
        from Merlin.Testing.Event import ClassWithSimpleEvent
        x = ClassWithSimpleEvent()

        # 362447
        #x.OnAction.__iadd__(TargetClass.s_Negate)
        #self.assertEqual(x.CallInside(101), -101)
        #x.OnAction.__isub__(TargetClass.s_Negate)
        #self.assertEqual(x.CallInside(102), -1)

    def test_add_method_descriptor(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.Event import ClassWithSimpleEvent, TargetClass
        x = ClassWithSimpleEvent()

        x.OnAction += TargetClass.s_Negate
        x.OnAction += TargetClass.i_Double  # method
        Flag.Set(0)
        self.assertRaisesMessage(TypeError,
            "i_Double() takes exactly 2 arguments (1 given)",
            lambda: x.CallInside(4))
        Flag.Check(10)

    def test_call_outside(self):
        from Merlin.Testing.Event import ClassWithSimpleEvent, TargetClass
        x = ClassWithSimpleEvent()

        x.OnAction += TargetClass.s_Negate
        #x.OnAction(3)  # 362449

run_test(__name__)
