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
'''
Operations on delegate type.
'''

import unittest

from iptest import IronPythonTestCase, run_test, skipUnlessIronPython

class DelegateTest(IronPythonTestCase):
    def setUp(self):
        super(DelegateTest, self).setUp()
        self.add_clr_assemblies("delegatedefinitions", "typesamples")

    def test_instantiation(self):
        import clr
        from Merlin.Testing.Delegate import ClassWithTargetMethods, VoidInt32Delegate, VoidVoidDelegate
        d = VoidInt32Delegate
        x = ClassWithTargetMethods()

        # positive
        y = d(x.MVoidInt32)
        y(3)
        y = d(x.MVoidByte)
        y(3)
        y = d(x.MVoidDouble)
        y(3.234)

        # negative
        y = d(x.MInt32Void)
        self.assertRaises(TypeError, y)
        
        y = d(x.MVoidInt32Int32)
        self.assertRaises(TypeError, y, 1, 2)
        
        # need more scenario coverage
        y = d(x.MVoidRefInt32)
        y(2)
        
        y = d(x.MVoidOutInt32)
        z = clr.StrongBox[int](2)
        #y(z)
        
        d = VoidVoidDelegate
        y = d(x.MVoidOutInt32)
        y()

    def test_overloads(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.Delegate import ClassWithTargetMethods, VoidInt32Delegate
        d = VoidInt32Delegate
        target = ClassWithTargetMethods()
        
        y = d(target.MOverload1)
        y(1)
        Flag.Check(110)
        
        y = d(target.MOverload2)
        y(2)
        Flag.Check(200)

        y = d(target.MOverload3)
        y(3)
        Flag.Check(300)

    def test_overloads2(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.Delegate import A100, B100, C100, ClassWithTargetMethods, VoidB100Delegate
        d = VoidB100Delegate
        target = ClassWithTargetMethods()
        
        y = d(target.MOverload8)
        a, b, c = A100(), B100(), C100()
        y(None)
        Flag.Check(820) #!!! 810 is what C# expects.
        y(b)
        Flag.Check(810)
        y(c)
        Flag.Check(820) #!!!

    def test_no_matching_overload(self):
        from Merlin.Testing.Delegate import ClassWithTargetMethods, Int32VoidDelegate
        d = Int32VoidDelegate
        target = ClassWithTargetMethods()
        
        y = d(target.MOverload2)
        # y()


    def test_by_ref(self):
        from Merlin.Testing.Delegate import ClassWithTargetMethods, VoidRefInt32Delegate
        d = VoidRefInt32Delegate
        target = ClassWithTargetMethods()
        
        y = d(target.MVoidRefInt32)
        #y(3)
        #Flag.Check(3)
    
    def test_create_from_another_delegate_object(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.Delegate import ClassWithTargetMethods, VoidInt32Delegate, VoidInt32Int32Delegate, VoidInt32ParamsArrayDelegate
        d = VoidInt32Delegate
        target = ClassWithTargetMethods()
        y = d(target.MVoidInt32)
        y(-1)
        Flag.Check(-1)
        
        # positive
        z = d(y)
        z(2)
        Flag.Check(2)
        
        # negative
        self.assertRaisesMessage(TypeError, 
            "Cannot cast Merlin.Testing.Delegate.VoidInt32Delegate to Merlin.Testing.Delegate.VoidInt32Int32Delegate.", 
            VoidInt32Int32Delegate, y)

        self.assertRaisesMessage(TypeError, 
            "Cannot cast Merlin.Testing.Delegate.VoidInt32Delegate to Merlin.Testing.Delegate.VoidInt32ParamsArrayDelegate.", 
            VoidInt32ParamsArrayDelegate, y)

    def test_generic_delegate(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.Delegate import ClassWithTargetMethods, VoidTDelegate
        d = VoidTDelegate
        target = ClassWithTargetMethods()

        d(target.MVoidInt32)(1)
        Flag.Check(1)
        
        d[()](target.MVoidInt32)(-3)
        Flag.Check(-3)
        
        d[int](target.MVoidInt32)(5)
        Flag.Check(5)

    def test_static_instance_methods(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.Delegate import ClassWithTargetMethods, VoidInt32Delegate, VoidSelfInt32Delegate
        target = ClassWithTargetMethods()
        
        x = VoidInt32Delegate(ClassWithTargetMethods.SMVoidInt32)
        x(2)
        Flag.Check(20)
        
        x = VoidSelfInt32Delegate(ClassWithTargetMethods.MVoidInt32)
        x(target, 3)
        Flag.Check(3)
        
        x = VoidInt32Delegate(target.SMVoidInt32)
        x(4)
        Flag.Check(40)
        
        x = VoidSelfInt32Delegate(target.MVoidInt32)
        self.assertRaises(TypeError, lambda: x(target, 5))

    def test_relaxed_delegate_binding(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.Delegate import Base, BaseDerivedDelegate, ClassWithTargetMethods, Derived, DerivedBaseDelegate
        b, d = Base(), Derived()
        
        target = ClassWithTargetMethods()
        x = BaseDerivedDelegate(target.MDerivedBase)
        x(d)
        Flag.Check(345)
        self.assertRaises(TypeError, lambda: x(b))
        
        x = DerivedBaseDelegate(target.MBaseDerivedReturnNull)
        x(d)
        Flag.Check(678)

        x = DerivedBaseDelegate(target.MBaseDerived)
        self.assertRaises(TypeError, lambda: x(d))
        self.assertRaises(TypeError, lambda: x(b))

    def test_interface_method(self):
        from Merlin.Testing.Delegate import InterfaceInt32Delegate, InterfaceWithTargetMethods, VoidInt32Delegate
        x = VoidInt32Delegate(InterfaceWithTargetMethods.MVoidInt32)
        self.assertRaisesMessage(TypeError, 
            "MVoidInt32() takes exactly 2 arguments (1 given)",
            lambda: x(1))

        # !!!
        x = InterfaceInt32Delegate(InterfaceWithTargetMethods.MVoidInt32)
        self.assertRaisesMessage(TypeError, 
            "expected InterfaceWithTargetMethods, got NoneType",
            lambda: x(None, 2))

    def test_methods_from_value_type(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.Delegate import StructWithTargetMethods, VoidInt32Delegate
        d = VoidInt32Delegate
        target = StructWithTargetMethods()
        x = d(target.MVoidInt32)
        x(123)
        Flag.Check(246)
        
        x = d(StructWithTargetMethods.SMVoidInt32)
        x(321)
        Flag.Check(963)

    def test_fill_with_none(self):
        from Merlin.Testing.Delegate import ClassWithTargetMethods, VoidInt32Delegate
        d = VoidInt32Delegate
        target = ClassWithTargetMethods()
        
        #x = d(None)
        x = d(target.MVoidInt32)
        #x += None

    def test_explicit_invocation(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.Delegate import ClassWithTargetMethods, VoidInt32Delegate
        d = VoidInt32Delegate
        target = ClassWithTargetMethods()
        x = d(target.MVoidInt32)

        y = x.Invoke(432)
        Flag.Check(432)

        #y = x.BeginInvoke(543, None, None)   # bug 363772
        #y.AsyncWaitHandle.WaitOne()
        #Flag.Check(543)
        
        def callback(ar): 
            print("callbacked")
            
        #y = x.BeginInvoke(321, callback, None)
        #Flag.Check(321)
        
        #y = x.__call__(642)
        #Flag.Check(642)
    

run_test(__name__)
