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

import sys
import unittest

from iptest import IronPythonTestCase, run_test, skipUnlessIronPython

@skipUnlessIronPython()
class ProtectedTest(IronPythonTestCase):
    def setUp(self):
        super(ProtectedTest, self).setUp()
        self.load_iron_python_test()

    def test_base(self):
        """properties w/ differening access"""
        from IronPythonTest import BaseClass
        a = BaseClass()
        self.assertEqual(a.Area, 0)
        def setA(a,val):
            a.Area = val
        self.assertRaises(AttributeError, setA, a, 16)
        self.assertEqual(a.Area, 0)
            
        class WrapBaseClass(BaseClass): pass
        a = WrapBaseClass()
        self.assertEqual(a.Area, 0)
        a.Area = 16
        self.assertEqual(a.Area, 16)

    def test_derived(self):
        from IronPythonTest import BaseClass
        class MyBaseClass(BaseClass):
            def MySetArea(self, size):
                self.Area = size

        a = MyBaseClass()
        self.assertEqual(a.Area, 0)

        a.MySetArea(16)
        self.assertEqual(a.Area, 16)

        a.Area = 36
        self.assertEqual(a.Area, 36)

        # protected fields
        self.assertEqual(a.foo, 0)
        a.foo = 7
        self.assertEqual(a.foo, 7)

    def test_super_protected(self):
        class x(object): pass
        
        clone = super(x, x()).MemberwiseClone()
        self.assertEqual(type(clone), x)

    def test_override(self):
        """overriding methods"""
        from IronPythonTest import Inherited

        # can't access protected methods directly
        a = Inherited()
        
        # they are present...
        self.assertTrue('ProtectedMethod' in dir(a))
        self.assertTrue('ProtectedProperty' in dir(a))
        self.assertTrue(hasattr(a, 'ProtectedMethod'))
        
        # hasattr returns false if the getter raises...
        self.assertTrue(not hasattr(a, 'ProtectedProperty'))
        self.assertRaisesMessage(TypeError, "cannot access protected member ProtectedProperty without a python subclass of Inherited", lambda : a.ProtectedProperty)
        
        class WrapInherited(Inherited): pass
        a = WrapInherited()
        self.assertEqual(a.ProtectedMethod(), 'Inherited.ProtectedMethod')
        self.assertEqual(a.ProtectedProperty, 'Inherited.Protected')

        class MyInherited(Inherited):
            def ProtectedMethod(self):
                return "MyInherited"
            def ProtectedMethod(self):
                return "MyInherited Override"
            def ProtectedPropertyGetter(self):
                return "MyInherited.Protected"
            ProtectedProperty = property(ProtectedPropertyGetter)

        a = MyInherited()
        
        self.assertEqual(a.ProtectedMethod(), 'MyInherited Override')
        self.assertEqual(a.CallProtected(), 'MyInherited Override')
        self.assertEqual(a.ProtectedProperty, "MyInherited.Protected")
        self.assertEqual(a.CallProtectedProp(), "MyInherited.Protected")

    def test_events(self):
        from IronPythonTest import Events
        # can't access protected methods directly
        a = Events()
        
        # they are present...
        self.assertTrue('OnProtectedEvent' in dir(a))
        self.assertTrue('OnExplicitProtectedEvent' in dir(a))
        self.assertTrue(hasattr(a, 'OnProtectedEvent'))
        self.assertTrue(hasattr(a, 'OnExplicitProtectedEvent'))
        
        # they should not be present
        self.assertTrue('add_OnProtectedEvent' not in dir(a))
        self.assertTrue('remove_OnProtectedEvent' not in dir(a))
        self.assertTrue('add_OnExplicitProtectedEvent' not in dir(a))
        self.assertTrue('remove_OnExplicitProtectedEvent' not in dir(a))

        # should not be present as its private
        self.assertTrue('ExplicitProtectedEvent' not in dir(a))
        
        def OuterEventHandler(source, args):
            global called
            called = True

        global called
        # Testing accessing protected Events fails.  
        # TODO: Currently adding non-protected events do not generate errors due to lack of context checking
        called = False
        #AssertErrorWithMessage(TypeError, "Cannot add handler to a private event.", lambda : a.OnProtectedEvent += OuterEventHandler)
        a.OnProtectedEvent += OuterEventHandler
        a.FireProtectedTest()
        a.OnProtectedEvent -= OuterEventHandler
        #AssertErrorWithMessage(TypeError, "Cannot remove handler to a private event.", lambda : a.OnProtectedEvent -= OuterEventHandler)
        #self.assertEqual(called, False) # indicates that event fired and set value which should not be allowed
        
        called = False
        #AssertErrorWithMessage(TypeError, "Cannot add handler to a private event.", lambda : a.OnExplicitProtectedEvent += OuterEventHandler)
        a.OnExplicitProtectedEvent += OuterEventHandler
        a.FireProtectedTest()
        a.OnExplicitProtectedEvent -= OuterEventHandler
        #AssertErrorWithMessage(TypeError, "Cannot remove handler to a private event.", lambda : a.OnExplicitProtectedEvent -= OuterEventHandler)
        #self.assertEqual(called, False)
        
        
        class MyInheritedEvents(Events):
            called3 = False
            called4 = False
            
            def __init__(self):
                self.called1 = False
                self.called2 = False
                
            def InnerEventHandler1(self, source, args):
                self.called1 = True
                
            def InnerEventHandler2(self, source, args):
                self.called2 = True
                
            def RegisterEventsInstance(self):
                self.OnProtectedEvent += OuterEventHandler
                self.OnProtectedEvent += self.InnerEventHandler1
                self.OnExplicitProtectedEvent += self.InnerEventHandler2
                
            def UnregisterEventsInstance(self):
                self.OnProtectedEvent -= self.InnerEventHandler1
                self.OnExplicitProtectedEvent -= self.InnerEventHandler2

            @classmethod
            def InnerEventHandler3(cls, source, args):
                cls.called3 = True
                
            @classmethod
            def InnerEventHandler4(cls, source, args):
                cls.called4 = True
                
            @classmethod        
            def RegisterEventsStatic(cls, events):
                events.OnProtectedEvent += OuterEventHandler
                events.OnProtectedEvent += cls.InnerEventHandler3
                events.OnExplicitProtectedEvent += cls.InnerEventHandler4
                
            @classmethod
            def UnregisterEventsStatic(cls, events):
                events.OnProtectedEvent -= OuterEventHandler
                events.OnProtectedEvent -= cls.InnerEventHandler3
                events.OnExplicitProtectedEvent -= cls.InnerEventHandler4
        
        # validate instance methods work
        b = MyInheritedEvents()
        called = b.called1 = b.called2 = False
        b.RegisterEventsInstance()
        b.FireProtectedTest()
        self.assertEqual(called, True)
        self.assertEqual(b.called1, True)
        self.assertEqual(b.called2, True)

        # validate theat static methods work
        c = MyInheritedEvents()
        called = MyInheritedEvents.called3 = MyInheritedEvents.called4 = False
        MyInheritedEvents.RegisterEventsStatic(c)
        c.FireProtectedTest()
        MyInheritedEvents.UnregisterEventsStatic(c)
        self.assertEqual(called, True)
        self.assertEqual(MyInheritedEvents.called3, True)
        self.assertEqual(MyInheritedEvents.called4, True)

        class WrapEvents(Events): 
            @classmethod        
            def RegisterEventsStatic(cls, events):
                events.OnProtectedEvent += OuterEventHandler
            @classmethod
            def UnregisterEventsStatic(cls, events):
                events.OnProtectedEvent -= OuterEventHandler
        
        # baseline empty test 
        d = Events()
        called = False
        d.FireProtectedTest()
        self.assertEqual(called, False)
            
        # use wrapevents to bypass protection
        called = False
        WrapEvents.RegisterEventsStatic(d)
        d.FireProtectedTest()
        WrapEvents.UnregisterEventsStatic(d)
        self.assertEqual(called, True)


run_test(__name__)
