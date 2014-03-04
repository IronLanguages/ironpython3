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

#
# test static members
#

from iptest.assert_util import *
skiptest("win32")

load_iron_python_test()
from IronPythonTest.StaticTest import *

allTypes = [Base, OverrideNothing, OverrideAll]

@skip("multiple_execute")
def test_field():
    # read on class
    AreEqual(Base.Field, 'Base.Field')
    AreEqual(OverrideNothing.Field, 'Base.Field')
    AreEqual(OverrideAll.Field, 'OverrideAll.Field')
    
    # write and read back
    Base.Field = 'FirstString'
    AreEqual(Base.Field, 'FirstString')
    AreEqual(OverrideNothing.Field, 'FirstString')
    AreEqual(OverrideAll.Field, 'OverrideAll.Field')
    
    def f(): OverrideNothing.Field = 'SecondString'
    AssertErrorWithMessage(AttributeError, "'OverrideNothing' object has no attribute 'Field'", f)
   
    AreEqual(Base.Field, 'FirstString')
    AreEqual(OverrideNothing.Field, 'FirstString')
    AreEqual(OverrideAll.Field, 'OverrideAll.Field')
    
    OverrideAll.Field = 'ThirdString'
    AreEqual(Base.Field, 'FirstString')
    AreEqual(OverrideNothing.Field, 'FirstString')
    AreEqual(OverrideAll.Field, 'ThirdString')

    # reset back
    Base.Field = 'Base.Field'
    OverrideAll.Field = 'OverrideAll.Field'

    # read / write on instance
    b, o1, o2 = Base(), OverrideNothing(), OverrideAll()
    
    AreEqual(b.Field, 'Base.Field')
    AreEqual(o1.Field, 'Base.Field')
    AreEqual(o2.Field, 'OverrideAll.Field')
    
    b.Field = 'FirstString'
    AreEqual(b.Field, 'FirstString')
    AreEqual(o1.Field, 'FirstString')
    AreEqual(o2.Field, 'OverrideAll.Field')
    
    def f(): o1.Field = 'SecondString'
    AssertErrorWithMessage(AttributeError, "'OverrideNothing' object has no attribute 'Field'", f)

    o2.Field = 'ThirdString'
    AreEqual(b.Field, 'FirstString')
    AreEqual(o1.Field, 'FirstString')
    AreEqual(o2.Field, 'ThirdString')
    
    # del
    def f(target): del target.Field
    AssertErrorWithMessage(AttributeError, "cannot delete attribute 'Field' of builtin type 'Base'", f, Base)
    AssertErrorWithMessage(AttributeError, "cannot delete attribute 'Field' of builtin type 'Base'", f, OverrideNothing)
    AssertErrorWithMessage(AttributeError, "cannot delete attribute 'Field' of builtin type 'OverrideAll'", f, OverrideAll)
    
    AssertErrorWithMessage(AttributeError, "cannot delete attribute 'Field' of builtin type 'Base'", f, b)
    AssertErrorWithMessage(AttributeError, "cannot delete attribute 'Field' of builtin type 'Base'", f, o1)
    AssertErrorWithMessage(AttributeError, "cannot delete attribute 'Field' of builtin type 'OverrideAll'", f, o2)
    
def test_property():
    # read on class
    AreEqual(Base.Property, 'Base.Property')
    AreEqual(OverrideNothing.Property, 'Base.Property')
    AreEqual(OverrideAll.Property, 'OverrideAll.Property')
    
    # write and read back
    Base.Property = 'FirstString'
    AreEqual(Base.Property, 'FirstString')
    AreEqual(OverrideNothing.Property, 'FirstString')
    AreEqual(OverrideAll.Property, 'OverrideAll.Property')
   
    def f(): OverrideNothing.Property = 'SecondString'
    AssertErrorWithMessage(AttributeError, "'OverrideNothing' object has no attribute 'Property'", f)
 
    AreEqual(Base.Property, 'FirstString')
    AreEqual(OverrideNothing.Property, 'FirstString')
    AreEqual(OverrideAll.Property, 'OverrideAll.Property')
    
    OverrideAll.Property = 'ThirdString'
    AreEqual(Base.Property, 'FirstString')
    AreEqual(OverrideNothing.Property, 'FirstString')
    AreEqual(OverrideAll.Property, 'ThirdString')
    
    # reset back
    Base.Property = 'Base.Property'
    OverrideAll.Property = 'OverrideAll.Property'

    # read / write on instance
    b, o1, o2 = Base(), OverrideNothing(), OverrideAll()

    AreEqual(b.Property, 'Base.Property')
    AreEqual(o1.Property, 'Base.Property')
    AreEqual(o2.Property, 'OverrideAll.Property')
    
    def f_write(target): target.Property = 'Anything'
    AssertErrorWithMessage(AttributeError, "static property 'Property' of 'Base' can only be assigned to through a type, not an instance", f_write, b)
    AssertErrorWithMessage(AttributeError, "static property 'Property' of 'OverrideAll' can only be assigned to through a type, not an instance", f_write, o2)
    AssertErrorWithMessage(AttributeError, "static property 'Property' of 'Base' can only be assigned to through a type, not an instance", f_write, o1)
      
    # del
    def f(target): del target.Property
    AssertErrorWithMessage(AttributeError, "cannot delete attribute 'Property' of builtin type 'Base'", f, Base)
    AssertErrorWithMessage(AttributeError, "cannot delete attribute 'Property' of builtin type 'Base'", f, OverrideNothing)
    AssertErrorWithMessage(AttributeError, "cannot delete attribute 'Property' of builtin type 'OverrideAll'", f, OverrideAll)
    
    AssertErrorWithMessage(AttributeError, "cannot delete attribute 'Property' of builtin type 'Base'", f, b)
    AssertErrorWithMessage(AttributeError, "cannot delete attribute 'Property' of builtin type 'Base'", f, o1)
    AssertErrorWithMessage(AttributeError, "cannot delete attribute 'Property' of builtin type 'OverrideAll'", f, o2)

@skip("multiple_execute")
def test_event():
    lambda1 = lambda : 'FirstString'
    lambda2 = lambda : 'SecondString'
    lambda3 = lambda : 'ThirdString'
    
    AreEqual(Base.TryEvent(), 'Still None')
    AreEqual(OverrideNothing.TryEvent(), 'Still None')
    AreEqual(OverrideAll.TryEvent(), 'Still None here')

    Base.Event += lambda1
    AreEqual(Base.TryEvent(), 'FirstString')
    AreEqual(OverrideNothing.TryEvent(), 'FirstString')
    AreEqual(OverrideAll.TryEvent(), 'Still None here')

    Base.Event -= lambda1
    AreEqual(Base.TryEvent(), 'Still None')
        
    def f(): OverrideNothing.Event += lambda2
    AssertErrorWithMessage(AttributeError, "attribute 'Event' of 'OverrideNothing' object is read-only", f)
    
    # ISSUE
    Base.Event -= lambda2
    
    AreEqual(Base.TryEvent(), 'Still None')
    AreEqual(OverrideNothing.TryEvent(), 'Still None')
    AreEqual(OverrideAll.TryEvent(), 'Still None here')
    
    OverrideAll.Event += lambda3
    AreEqual(Base.TryEvent(), 'Still None')
    AreEqual(OverrideNothing.TryEvent(), 'Still None')
    AreEqual(OverrideAll.TryEvent(), 'ThirdString')
    
    OverrideAll.Event -= lambda3
    AreEqual(OverrideAll.TryEvent(), 'Still None here')

    # Play on instance
    b, o1, o2 = Base(), OverrideNothing(), OverrideAll()
    
    b.Event += lambda1
    AreEqual(Base.TryEvent(), 'FirstString')
    AreEqual(OverrideNothing.TryEvent(), 'FirstString')
    AreEqual(OverrideAll.TryEvent(), 'Still None here')
    b.Event -= lambda1
    
    def f(): o1.Event += lambda2
    AssertErrorWithMessage(AttributeError, "attribute 'Event' of 'OverrideNothing' object is read-only", f)
   
    # ISSUE
    try:    o1.Event -= lambda2
    except: pass
    
    AreEqual(Base.TryEvent(), 'Still None')
    AreEqual(OverrideNothing.TryEvent(), 'Still None')
    AreEqual(OverrideAll.TryEvent(), 'Still None here')
    
    o2.Event += lambda3
    AreEqual(Base.TryEvent(), 'Still None')
    AreEqual(OverrideNothing.TryEvent(), 'Still None')
    AreEqual(OverrideAll.TryEvent(), 'ThirdString')

    # del
    def f(target): del target.Event
    AssertErrorWithMessage(AttributeError, "cannot delete attribute 'Event' of builtin type 'Base'", f, Base)
    AssertErrorWithMessage(AttributeError, "cannot delete attribute 'Event' of builtin type 'Base'", f, OverrideNothing)
    AssertErrorWithMessage(AttributeError, "cannot delete attribute 'Event' of builtin type 'OverrideAll'", f, OverrideAll)
    
    AssertErrorWithMessage(AttributeError, "cannot delete attribute 'Event' of builtin type 'Base'", f, b)
    AssertErrorWithMessage(AttributeError, "cannot delete attribute 'Event' of builtin type 'Base'", f, o1)
    AssertErrorWithMessage(AttributeError, "cannot delete attribute 'Event' of builtin type 'OverrideAll'", f, o2)

def test_method():
    AreEqual(Base.Method_None(), 'Base.Method_None')
    AreEqual(OverrideNothing.Method_None(), 'Base.Method_None')
    AreEqual(OverrideAll.Method_None(), 'OverrideAll.Method_None')
    
    for type in allTypes:
        AssertError(TypeError, type.Method_None, None)
        AssertError(TypeError, type.Method_None, 1)

    AreEqual(Base.Method_OneArg(1), 'Base.Method_OneArg')
    AreEqual(OverrideNothing.Method_OneArg(1), 'Base.Method_OneArg')
    AreEqual(OverrideAll.Method_OneArg(1), 'OverrideAll.Method_OneArg')
    
    for type in allTypes:
        AssertError(TypeError, type.Method_OneArg)
        AssertError(TypeError, type.Method_OneArg, None)

    #==============================================================
    
    b, d1, d2 = Base(), OverrideNothing(), OverrideAll()
    for x in [b, d1, d2]:
        AreEqual(Base.Method_Base(x), 'Base.Method_Base')
        AreEqual(OverrideNothing.Method_Base(x), 'Base.Method_Base')
        
    AssertErrorWithMessage(TypeError, 'expected OverrideAll, got Base', OverrideAll.Method_Base, b)
    AssertErrorWithMessage(TypeError, 'expected OverrideAll, got OverrideNothing', OverrideAll.Method_Base, d1)
    AreEqual(OverrideAll.Method_Base(d2), 'OverrideAll.Method_Base')

    #==============================================================

    b, d = B(), D()
    
    AreEqual(Base.Method_Inheritance1(b), 'Base.Method_Inheritance1')
    AreEqual(OverrideNothing.Method_Inheritance1(b), 'Base.Method_Inheritance1')
    AssertErrorWithMessage(TypeError, 'expected D, got B', OverrideAll.Method_Inheritance1, b)

    AreEqual(Base.Method_Inheritance1(d), 'Base.Method_Inheritance1')
    AreEqual(OverrideNothing.Method_Inheritance1(d), 'Base.Method_Inheritance1')
    AreEqual(OverrideAll.Method_Inheritance1(d), 'OverrideAll.Method_Inheritance1')

    AssertErrorWithMessage(TypeError, 'expected D, got B', Base.Method_Inheritance2, b)
    AssertErrorWithMessage(TypeError, 'expected D, got B', OverrideNothing.Method_Inheritance2, b)
    AreEqual(OverrideAll.Method_Inheritance2(b), 'OverrideAll.Method_Inheritance2')

    AreEqual(Base.Method_Inheritance2(d), 'Base.Method_Inheritance2')
    AreEqual(OverrideNothing.Method_Inheritance2(d), 'Base.Method_Inheritance2')
    AreEqual(OverrideAll.Method_Inheritance2(d), 'OverrideAll.Method_Inheritance2')

    AssertError(TypeError, OverrideAll.Method_Inheritance3, b, b)
    # OverrideAll only gets the (D, B) overload because (B, D) would cause a conflict
    AssertError(TypeError, OverrideAll.Method_Inheritance3, b, d)
    AreEqual(OverrideAll.Method_Inheritance3(d, b), 'OverrideAll.Method_Inheritance3')
    AreEqual(OverrideAll.Method_Inheritance3(d, d), 'OverrideAll.Method_Inheritance3')

    # play with instance
    b, o1, o2 = Base(), OverrideNothing(), OverrideAll()
    AreEqual(b.Method_None(), 'Base.Method_None')
    AreEqual(o1.Method_None(), 'Base.Method_None')
    AreEqual(o2.Method_None(), 'OverrideAll.Method_None')
    
    AreEqual(b.Method_Base(b), 'Base.Method_Base')
    AreEqual(o1.Method_Base(b), 'Base.Method_Base')
    AssertErrorWithMessage(TypeError, 'expected OverrideAll, got Base', o2.Method_Base, b)

    AreEqual(b.Method_Base(o1), 'Base.Method_Base')
    AreEqual(o1.Method_Base(o1), 'Base.Method_Base')
    AssertErrorWithMessage(TypeError, 'expected OverrideAll, got OverrideNothing', o2.Method_Base, o1)
    
    AreEqual(b.Method_Base(o2), 'Base.Method_Base')
    AreEqual(o1.Method_Base(o2), 'Base.Method_Base')
    AreEqual(o2.Method_Base(o2), 'OverrideAll.Method_Base')

    # del
    def f(target): del target.Method_None

    AssertErrorWithMessage(AttributeError, "cannot delete attribute 'Method_None' of builtin type 'Base'", f, Base)
    AssertErrorWithMessage(AttributeError, "cannot delete attribute 'Method_None' of builtin type 'Base'", f, OverrideNothing)
    AssertErrorWithMessage(AttributeError, "cannot delete attribute 'Method_None' of builtin type 'OverrideAll'", f, OverrideAll)
    
    AssertErrorWithMessage(AttributeError, "cannot delete attribute 'Method_None' of builtin type 'Base'", f, b)
    AssertErrorWithMessage(AttributeError, "cannot delete attribute 'Method_None' of builtin type 'Base'", f, o1)
    AssertErrorWithMessage(AttributeError, "cannot delete attribute 'Method_None' of builtin type 'OverrideAll'", f, o2)

def test_extra_generics():
    b = B()
    AreEqual(GD.M1(1), 'GD.M1')
    #AreEqual(GD.M1('s'), 'GB.M1')
    AssertError(TypeError, GD.M1, b)
    
    #AreEqual(GD.M2('s'), 'GB.M2')
    AreEqual(GD.M2[str]('s'), 'GD.M2')
    AreEqual(GD.M2[int](2), 'GD.M2')
    AreEqual(GD.M2(1), 'GD.M2')
    
    AreEqual(GD.M3('s'), 'GD.M3')
    AssertError(TypeError, GD.M3, 1)
    
    #AreEqual(GD.M4('s'), ('GB.M4-B', 's'))
    #AreEqual(GD.M4(), 'GB.M4-A')
    AreEqual(GD.M4(1), ('GD.M4', 1))
    
    AreEqual(GD.M21(1), 'GD.M21')
    #AreEqual(GD.M21(1,2), 'GB.M21')
    
    AreEqual(GD.M22(1), 'GD.M22')
    AreEqual(GD.M22(1,2), 'GD.M22')

    AssertError(TypeError, GD.M23)
    AreEqual(GD.M23(1), 'GD.M23')

    AreEqual(GD.M24(), 'GD.M24')
    #AreEqual(GD.M24(1), 'GB.M24')
    
    AreEqual(GD.M25(), 'GD.M25')
    AreEqual(GD.M25(1), 'GD.M25')
    
    AreEqual(KD[int].M1(10), 'KD.M1')
    #AreEqual(KD[int].M1('s'), 'KB.M1')
    
    AssertError(TypeError, KD[str].M1, 10)
    AreEqual(KD[str].M1('s'), 'KD.M1')
    
def test_operator():
    from IronPythonTest.StaticTest.Operator import *
    sc = SC(123, "hello")
    o = Use()
    
    AreEqual(o.M1(sc), 123)
    AreEqual(o.M2(sc), "hello")
    AssertError(TypeError, o.M3, sc)
    
    AreEqual(o.M6(sc), 123)

    # op_Explicit
    x = G1[int](456)
    y = G2[int, str].op_Explicit(x)
    z = G2[str, int].op_Explicit(x)
    
    if not is_silverlight:
        AreEqual((y.Field1, y.Field2), (456, None))
        AreEqual((z.Field1, z.Field2), (None, 456))

run_test(__name__)
