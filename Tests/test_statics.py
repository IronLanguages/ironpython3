# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import unittest

from iptest import IronPythonTestCase, run_test, skipUnlessIronPython

@skipUnlessIronPython()
class StaticsTest(IronPythonTestCase):
    def setUp(self):
        super(StaticsTest, self).setUp()

        self.load_iron_python_test()
        from IronPythonTest.StaticTest import Base, OverrideNothing, OverrideAll
        self.allTypes = [Base, OverrideNothing, OverrideAll]    

    #TODO: @skip("multiple_execute")
    def test_field(self):
        from IronPythonTest.StaticTest import Base, OverrideAll, OverrideNothing

        # read on class
        self.assertEqual(Base.Field, 'Base.Field')
        self.assertEqual(OverrideNothing.Field, 'Base.Field')
        self.assertEqual(OverrideAll.Field, 'OverrideAll.Field')
        
        # write and read back
        Base.Field = 'FirstString'
        self.assertEqual(Base.Field, 'FirstString')
        self.assertEqual(OverrideNothing.Field, 'FirstString')
        self.assertEqual(OverrideAll.Field, 'OverrideAll.Field')
        
        def f(): OverrideNothing.Field = 'SecondString'
        self.assertRaisesMessage(AttributeError, "'OverrideNothing' object has no attribute 'Field'", f)
    
        self.assertEqual(Base.Field, 'FirstString')
        self.assertEqual(OverrideNothing.Field, 'FirstString')
        self.assertEqual(OverrideAll.Field, 'OverrideAll.Field')
        
        OverrideAll.Field = 'ThirdString'
        self.assertEqual(Base.Field, 'FirstString')
        self.assertEqual(OverrideNothing.Field, 'FirstString')
        self.assertEqual(OverrideAll.Field, 'ThirdString')

        # reset back
        Base.Field = 'Base.Field'
        OverrideAll.Field = 'OverrideAll.Field'

        # read / write on instance
        b, o1, o2 = Base(), OverrideNothing(), OverrideAll()
        
        self.assertEqual(b.Field, 'Base.Field')
        self.assertEqual(o1.Field, 'Base.Field')
        self.assertEqual(o2.Field, 'OverrideAll.Field')
        
        b.Field = 'FirstString'
        self.assertEqual(b.Field, 'FirstString')
        self.assertEqual(o1.Field, 'FirstString')
        self.assertEqual(o2.Field, 'OverrideAll.Field')
        
        def f(): o1.Field = 'SecondString'
        self.assertRaisesMessage(AttributeError, "'OverrideNothing' object has no attribute 'Field'", f)

        o2.Field = 'ThirdString'
        self.assertEqual(b.Field, 'FirstString')
        self.assertEqual(o1.Field, 'FirstString')
        self.assertEqual(o2.Field, 'ThirdString')
        
        # del
        def f(target): del target.Field
        self.assertRaisesMessage(AttributeError, "cannot delete attribute 'Field' of builtin type 'Base'", f, Base)
        self.assertRaisesMessage(AttributeError, "cannot delete attribute 'Field' of builtin type 'Base'", f, OverrideNothing)
        self.assertRaisesMessage(AttributeError, "cannot delete attribute 'Field' of builtin type 'OverrideAll'", f, OverrideAll)
        
        self.assertRaisesMessage(AttributeError, "cannot delete attribute 'Field' of builtin type 'Base'", f, b)
        self.assertRaisesMessage(AttributeError, "cannot delete attribute 'Field' of builtin type 'Base'", f, o1)
        self.assertRaisesMessage(AttributeError, "cannot delete attribute 'Field' of builtin type 'OverrideAll'", f, o2)
    
    def test_property(self):
        from IronPythonTest.StaticTest import Base, OverrideAll, OverrideNothing

        # read on class
        self.assertEqual(Base.Property, 'Base.Property')
        self.assertEqual(OverrideNothing.Property, 'Base.Property')
        self.assertEqual(OverrideAll.Property, 'OverrideAll.Property')
        
        # write and read back
        Base.Property = 'FirstString'
        self.assertEqual(Base.Property, 'FirstString')
        self.assertEqual(OverrideNothing.Property, 'FirstString')
        self.assertEqual(OverrideAll.Property, 'OverrideAll.Property')
    
        def f(): OverrideNothing.Property = 'SecondString'
        self.assertRaisesMessage(AttributeError, "'OverrideNothing' object has no attribute 'Property'", f)
    
        self.assertEqual(Base.Property, 'FirstString')
        self.assertEqual(OverrideNothing.Property, 'FirstString')
        self.assertEqual(OverrideAll.Property, 'OverrideAll.Property')
        
        OverrideAll.Property = 'ThirdString'
        self.assertEqual(Base.Property, 'FirstString')
        self.assertEqual(OverrideNothing.Property, 'FirstString')
        self.assertEqual(OverrideAll.Property, 'ThirdString')
        
        # reset back
        Base.Property = 'Base.Property'
        OverrideAll.Property = 'OverrideAll.Property'

        # read / write on instance
        b, o1, o2 = Base(), OverrideNothing(), OverrideAll()

        self.assertEqual(b.Property, 'Base.Property')
        self.assertEqual(o1.Property, 'Base.Property')
        self.assertEqual(o2.Property, 'OverrideAll.Property')
        
        def f_write(target): target.Property = 'Anything'
        self.assertRaisesMessage(AttributeError, "static property 'Property' of 'Base' can only be assigned to through a type, not an instance", f_write, b)
        self.assertRaisesMessage(AttributeError, "static property 'Property' of 'OverrideAll' can only be assigned to through a type, not an instance", f_write, o2)
        self.assertRaisesMessage(AttributeError, "static property 'Property' of 'Base' can only be assigned to through a type, not an instance", f_write, o1)
        
        # del
        def f(target): del target.Property
        self.assertRaisesMessage(AttributeError, "cannot delete attribute 'Property' of builtin type 'Base'", f, Base)
        self.assertRaisesMessage(AttributeError, "cannot delete attribute 'Property' of builtin type 'Base'", f, OverrideNothing)
        self.assertRaisesMessage(AttributeError, "cannot delete attribute 'Property' of builtin type 'OverrideAll'", f, OverrideAll)
        
        self.assertRaisesMessage(AttributeError, "cannot delete attribute 'Property' of builtin type 'Base'", f, b)
        self.assertRaisesMessage(AttributeError, "cannot delete attribute 'Property' of builtin type 'Base'", f, o1)
        self.assertRaisesMessage(AttributeError, "cannot delete attribute 'Property' of builtin type 'OverrideAll'", f, o2)

    #TODO:@skip("multiple_execute")
    def test_event(self):
        from IronPythonTest.StaticTest import Base, OverrideAll, OverrideNothing
        lambda1 = lambda : 'FirstString'
        lambda2 = lambda : 'SecondString'
        lambda3 = lambda : 'ThirdString'
        
        self.assertEqual(Base.TryEvent(), 'Still None')
        self.assertEqual(OverrideNothing.TryEvent(), 'Still None')
        self.assertEqual(OverrideAll.TryEvent(), 'Still None here')

        Base.Event += lambda1
        self.assertEqual(Base.TryEvent(), 'FirstString')
        self.assertEqual(OverrideNothing.TryEvent(), 'FirstString')
        self.assertEqual(OverrideAll.TryEvent(), 'Still None here')

        Base.Event -= lambda1
        self.assertEqual(Base.TryEvent(), 'Still None')
            
        def f(): OverrideNothing.Event += lambda2
        self.assertRaisesMessage(AttributeError, "attribute 'Event' of 'OverrideNothing' object is read-only", f)
        
        # ISSUE
        Base.Event -= lambda2
        
        self.assertEqual(Base.TryEvent(), 'Still None')
        self.assertEqual(OverrideNothing.TryEvent(), 'Still None')
        self.assertEqual(OverrideAll.TryEvent(), 'Still None here')
        
        OverrideAll.Event += lambda3
        self.assertEqual(Base.TryEvent(), 'Still None')
        self.assertEqual(OverrideNothing.TryEvent(), 'Still None')
        self.assertEqual(OverrideAll.TryEvent(), 'ThirdString')
        
        OverrideAll.Event -= lambda3
        self.assertEqual(OverrideAll.TryEvent(), 'Still None here')

        # Play on instance
        b, o1, o2 = Base(), OverrideNothing(), OverrideAll()
        
        b.Event += lambda1
        self.assertEqual(Base.TryEvent(), 'FirstString')
        self.assertEqual(OverrideNothing.TryEvent(), 'FirstString')
        self.assertEqual(OverrideAll.TryEvent(), 'Still None here')
        b.Event -= lambda1
        
        def f(): o1.Event += lambda2
        self.assertRaisesMessage(AttributeError, "attribute 'Event' of 'OverrideNothing' object is read-only", f)
    
        # ISSUE
        try:    o1.Event -= lambda2
        except: pass
        
        self.assertEqual(Base.TryEvent(), 'Still None')
        self.assertEqual(OverrideNothing.TryEvent(), 'Still None')
        self.assertEqual(OverrideAll.TryEvent(), 'Still None here')
        
        o2.Event += lambda3
        self.assertEqual(Base.TryEvent(), 'Still None')
        self.assertEqual(OverrideNothing.TryEvent(), 'Still None')
        self.assertEqual(OverrideAll.TryEvent(), 'ThirdString')

        # del
        def f(target): del target.Event
        self.assertRaisesMessage(AttributeError, "cannot delete attribute 'Event' of builtin type 'Base'", f, Base)
        self.assertRaisesMessage(AttributeError, "cannot delete attribute 'Event' of builtin type 'Base'", f, OverrideNothing)
        self.assertRaisesMessage(AttributeError, "cannot delete attribute 'Event' of builtin type 'OverrideAll'", f, OverrideAll)
        
        self.assertRaisesMessage(AttributeError, "cannot delete attribute 'Event' of builtin type 'Base'", f, b)
        self.assertRaisesMessage(AttributeError, "cannot delete attribute 'Event' of builtin type 'Base'", f, o1)
        self.assertRaisesMessage(AttributeError, "cannot delete attribute 'Event' of builtin type 'OverrideAll'", f, o2)

    def test_method(self):
        from IronPythonTest.StaticTest import B, Base, D, OverrideAll, OverrideNothing

        self.assertEqual(Base.Method_None(), 'Base.Method_None')
        self.assertEqual(OverrideNothing.Method_None(), 'Base.Method_None')
        self.assertEqual(OverrideAll.Method_None(), 'OverrideAll.Method_None')
        
        for type in self.allTypes:
            self.assertRaises(TypeError, type.Method_None, None)
            self.assertRaises(TypeError, type.Method_None, 1)

        self.assertEqual(Base.Method_OneArg(1), 'Base.Method_OneArg')
        self.assertEqual(OverrideNothing.Method_OneArg(1), 'Base.Method_OneArg')
        self.assertEqual(OverrideAll.Method_OneArg(1), 'OverrideAll.Method_OneArg')
        
        for type in self.allTypes:
            self.assertRaises(TypeError, type.Method_OneArg)
            self.assertRaises(TypeError, type.Method_OneArg, None)

        #==============================================================
        
        b, d1, d2 = Base(), OverrideNothing(), OverrideAll()
        for x in [b, d1, d2]:
            self.assertEqual(Base.Method_Base(x), 'Base.Method_Base')
            self.assertEqual(OverrideNothing.Method_Base(x), 'Base.Method_Base')
            
        self.assertRaisesMessage(TypeError, 'expected OverrideAll, got Base', OverrideAll.Method_Base, b)
        self.assertRaisesMessage(TypeError, 'expected OverrideAll, got OverrideNothing', OverrideAll.Method_Base, d1)
        self.assertEqual(OverrideAll.Method_Base(d2), 'OverrideAll.Method_Base')

        #==============================================================

        b, d = B(), D()
        
        self.assertEqual(Base.Method_Inheritance1(b), 'Base.Method_Inheritance1')
        self.assertEqual(OverrideNothing.Method_Inheritance1(b), 'Base.Method_Inheritance1')
        self.assertRaisesMessage(TypeError, 'expected D, got B', OverrideAll.Method_Inheritance1, b)

        self.assertEqual(Base.Method_Inheritance1(d), 'Base.Method_Inheritance1')
        self.assertEqual(OverrideNothing.Method_Inheritance1(d), 'Base.Method_Inheritance1')
        self.assertEqual(OverrideAll.Method_Inheritance1(d), 'OverrideAll.Method_Inheritance1')

        self.assertRaisesMessage(TypeError, 'expected D, got B', Base.Method_Inheritance2, b)
        self.assertRaisesMessage(TypeError, 'expected D, got B', OverrideNothing.Method_Inheritance2, b)
        self.assertEqual(OverrideAll.Method_Inheritance2(b), 'OverrideAll.Method_Inheritance2')

        self.assertEqual(Base.Method_Inheritance2(d), 'Base.Method_Inheritance2')
        self.assertEqual(OverrideNothing.Method_Inheritance2(d), 'Base.Method_Inheritance2')
        self.assertEqual(OverrideAll.Method_Inheritance2(d), 'OverrideAll.Method_Inheritance2')

        self.assertRaises(TypeError, OverrideAll.Method_Inheritance3, b, b)
        # OverrideAll only gets the (D, B) overload because (B, D) would cause a conflict
        self.assertRaises(TypeError, OverrideAll.Method_Inheritance3, b, d)
        self.assertEqual(OverrideAll.Method_Inheritance3(d, b), 'OverrideAll.Method_Inheritance3')
        self.assertEqual(OverrideAll.Method_Inheritance3(d, d), 'OverrideAll.Method_Inheritance3')

        # play with instance
        b, o1, o2 = Base(), OverrideNothing(), OverrideAll()
        self.assertEqual(b.Method_None(), 'Base.Method_None')
        self.assertEqual(o1.Method_None(), 'Base.Method_None')
        self.assertEqual(o2.Method_None(), 'OverrideAll.Method_None')
        
        self.assertEqual(b.Method_Base(b), 'Base.Method_Base')
        self.assertEqual(o1.Method_Base(b), 'Base.Method_Base')
        self.assertRaisesMessage(TypeError, 'expected OverrideAll, got Base', o2.Method_Base, b)

        self.assertEqual(b.Method_Base(o1), 'Base.Method_Base')
        self.assertEqual(o1.Method_Base(o1), 'Base.Method_Base')
        self.assertRaisesMessage(TypeError, 'expected OverrideAll, got OverrideNothing', o2.Method_Base, o1)
        
        self.assertEqual(b.Method_Base(o2), 'Base.Method_Base')
        self.assertEqual(o1.Method_Base(o2), 'Base.Method_Base')
        self.assertEqual(o2.Method_Base(o2), 'OverrideAll.Method_Base')

        # del
        def f(target): del target.Method_None

        self.assertRaisesMessage(AttributeError, "cannot delete attribute 'Method_None' of builtin type 'Base'", f, Base)
        self.assertRaisesMessage(AttributeError, "cannot delete attribute 'Method_None' of builtin type 'Base'", f, OverrideNothing)
        self.assertRaisesMessage(AttributeError, "cannot delete attribute 'Method_None' of builtin type 'OverrideAll'", f, OverrideAll)
        
        self.assertRaisesMessage(AttributeError, "cannot delete attribute 'Method_None' of builtin type 'Base'", f, b)
        self.assertRaisesMessage(AttributeError, "cannot delete attribute 'Method_None' of builtin type 'Base'", f, o1)
        self.assertRaisesMessage(AttributeError, "cannot delete attribute 'Method_None' of builtin type 'OverrideAll'", f, o2)

    def test_extra_generics(self):
        from IronPythonTest.StaticTest import B, GD, KD
        b = B()
        self.assertEqual(GD.M1(1), 'GD.M1')
        #self.assertEqual(GD.M1('s'), 'GB.M1')
        self.assertRaises(TypeError, GD.M1, b)
        
        #self.assertEqual(GD.M2('s'), 'GB.M2')
        self.assertEqual(GD.M2[str]('s'), 'GD.M2')
        self.assertEqual(GD.M2[int](2), 'GD.M2')
        self.assertEqual(GD.M2(1), 'GD.M2')
        
        self.assertEqual(GD.M3('s'), 'GD.M3')
        self.assertRaises(TypeError, GD.M3, 1)
        
        #self.assertEqual(GD.M4('s'), ('GB.M4-B', 's'))
        #self.assertEqual(GD.M4(), 'GB.M4-A')
        self.assertEqual(GD.M4(1), ('GD.M4', 1))
        
        self.assertEqual(GD.M21(1), 'GD.M21')
        #self.assertEqual(GD.M21(1,2), 'GB.M21')
        
        self.assertEqual(GD.M22(1), 'GD.M22')
        self.assertEqual(GD.M22(1,2), 'GD.M22')

        self.assertRaises(TypeError, GD.M23)
        self.assertEqual(GD.M23(1), 'GD.M23')

        self.assertEqual(GD.M24(), 'GD.M24')
        #self.assertEqual(GD.M24(1), 'GB.M24')
        
        self.assertEqual(GD.M25(), 'GD.M25')
        self.assertEqual(GD.M25(1), 'GD.M25')
        
        self.assertEqual(KD[int].M1(10), 'KD.M1')
        #self.assertEqual(KD[int].M1('s'), 'KB.M1')
        
        self.assertRaises(TypeError, KD[str].M1, 10)
        self.assertEqual(KD[str].M1('s'), 'KD.M1')
    
    def test_operator(self):
        from IronPythonTest.StaticTest.Operator import *
        sc = SC(123, "hello")
        o = Use()
        
        self.assertEqual(o.M1(sc), 123)
        self.assertEqual(o.M2(sc), "hello")
        self.assertRaises(TypeError, o.M3, sc)
        
        self.assertEqual(o.M6(sc), 123)

        # op_Explicit
        x = G1[int](456)
        y = G2[int, str].op_Explicit(x)
        z = G2[str, int].op_Explicit(x)
        
        self.assertEqual((y.Field1, y.Field2), (456, None))
        self.assertEqual((z.Field1, z.Field2), (None, 456))

run_test(__name__)
