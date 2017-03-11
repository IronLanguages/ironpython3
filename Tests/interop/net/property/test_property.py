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
Operations on property.
'''
#------------------------------------------------------------------------------
from iptest.assert_util import *
skiptest("silverlight")
add_clr_assemblies("propertydefinitions", "typesamples")

from Merlin.Testing import *
from Merlin.Testing.Property import *  #Merlin 315120 - please do not remove/modify this line
from Merlin.Testing.TypeSample import *

def test_explicitly_implemented_property():
    for t in [
                ClassExplicitlyImplement, 
                StructExplicitlyImplement,
             ]:
        x = t()

        Assert(hasattr(x, 'Number'))
        
        d = IData.Number
        d.SetValue(x, 20)
        AreEqual(d.GetValue(x), 20)
        
        d.__set__(x, 30)
        AreEqual(d.__get__(x), 30)
        
    x = ClassExplicitlyReadOnly()

    d = IReadOnlyData.Number
    AssertErrorWithMessage(SystemError, "cannot set property", lambda: d.SetValue(x, "abc"))
    AreEqual(d.GetValue(x), "python")
    #AssertErrorWithMessage(AttributeError, "ddd", lambda: d.__set__(x, "abc"))    # bug 362857
    AreEqual(d.__get__(x), "python")
    
    x = StructExplicitlyWriteOnly()
    
    d = IWriteOnlyData.Number
    d.SetValue(x, SimpleStruct(3)); 
    Flag.Check(13)
    AssertErrorWithMessage(AttributeError, "unreadable property", lambda: d.GetValue(x))
    
    d.__set__(x, SimpleStruct(4)); 
    Flag.Check(14)
    AssertErrorWithMessage(AttributeError, "unreadable property", lambda: d.__get__(x))
    
    
def test_readonly():
    x = ClassWithReadOnly()
    
    AreEqual(x.InstanceProperty, 9)
    def f(): x.InstanceProperty = 10
    AssertErrorWithMessage(AttributeError, "can't assign to read-only property InstanceProperty of type 'ClassWithReadOnly'", f)
    
    AreEqual(ClassWithReadOnly.StaticProperty, "dlr")
    def f(): ClassWithReadOnly.StaticProperty = 'abc'
    AssertErrorWithMessage(AttributeError, "can't assign to read-only property StaticProperty of type 'ClassWithReadOnly'", f)
    

def test_writeonly():
    x = ClassWithWriteOnly()
    
    AssertErrorWithMessage(AttributeError, "InstanceProperty", lambda: x.InstanceProperty)  # msg
    x.InstanceProperty = 1; Flag.Check(11)
    
    #ClassWithWriteOnly.StaticProperty  # bug 362862
    ClassWithWriteOnly.StaticProperty = "dlr"
    Flag.Check(12)
    
    AssertErrorWithMatch(AttributeError, 
        "unreadable property", 
        lambda: ClassWithWriteOnly.__dict__['InstanceProperty'].__get__(x))
    AssertErrorWithMatch(AttributeError, 
        "unreadable property", 
        lambda: ClassWithWriteOnly.__dict__['InstanceProperty'].GetValue(x))
    
def test_readonly_writeonly_derivation():
    x = WriteOnlyDerived()
    
    x.Number = 100
    Flag.Check(100)
    AssertErrorWithMessage(AttributeError, "Number", lambda: x.Number)
    
    AreEqual(ReadOnlyBase.Number.GetValue(x), 21) 
    x.Number = 101
    Flag.Check(101)
    AreEqual(ReadOnlyBase.Number.GetValue(x), 21) 
    
    # repeat ReadOnlyDerived?

@skip("multiple_execute") 
def test_basic():
    print() 
    for t in [
                ClassWithProperties,
                StructWithProperties,
             ]:
        # very basic: object.InstanceProperty, Type.StaticProperty
        x, y = t(), t()
        a, b, c = 1234, SimpleStruct(23), SimpleClass(45)
        
        AreEqual(x.InstanceInt32Property, 0)
        x.InstanceInt32Property = a
        AreEqual(x.InstanceInt32Property, a)
        
        Assert(x.InstanceSimpleStructProperty.Flag == 0)
        x.InstanceSimpleStructProperty = b
        Assert(b == x.InstanceSimpleStructProperty)  
        AreEqual(b.Flag, x.InstanceSimpleStructProperty.Flag)
        
        AreEqual(x.InstanceSimpleClassProperty, None)
        x.InstanceSimpleClassProperty = c
        AreEqual(c, x.InstanceSimpleClassProperty)
        
        AreEqual(t.StaticInt32Property, 0)
        t.StaticInt32Property = a
        AreEqual(t.StaticInt32Property, a)
        
        t.StaticSimpleStructProperty = b
        AreEqual(b.Flag, t.StaticSimpleStructProperty.Flag)
        
        t.StaticSimpleClassProperty = c
        AreEqual(c, t.StaticSimpleClassProperty)
        
        # Type.InstanceProperty: SetValue/GetValue (on x), __set__/__get__ (on y)
        a, b, c = 34, SimpleStruct(56), SimpleClass(78)
        
        p = t.InstanceInt32Property
        AreEqual(p.SetValue(x, a), None)
        AreEqual(p.GetValue(x), a)
        
        p.__set__(y, a)
        #AreEqual(p.__get__(y), a)
        
        p = t.InstanceSimpleStructProperty
        p.SetValue(x, b)
        AreEqual(p.GetValue(x).Flag, b.Flag)
        
        p.__set__(y, b)
        #AreEqual(p.__get__(y).Flag, b.Flag)
        
        p = t.InstanceSimpleClassProperty
        p.SetValue(x, c)
        AreEqual(p.GetValue(x), c) 
        
        p.__set__(y, c)
        #AreEqual(p.__get__(y), c)
        
        # instance.StaticProperty
        a, b, c = 21, SimpleStruct(32), SimpleClass(43)

        # can read static properties through instances...
        AreEqual(x.StaticInt32Property, 1234)
        AreEqual(type(x.StaticSimpleStructProperty), SimpleStruct)
        AreEqual(type(x.StaticSimpleClassProperty), SimpleClass)
        
        def w1(): x.StaticInt32Property = a
        def w2(): x.StaticSimpleStructProperty = b
        def w3(): x.StaticSimpleClassProperty = c
        
        for w in [w1, w2, w3]:
            AssertErrorWithMatch(AttributeError, 
                "static property '.*' of '.*' can only be assigned to through a type, not an instance", 
                w)
        
        #
        # type.__dict__['xxxProperty']
        #
        x = t()
        a, b, c = 8, SimpleStruct(7), SimpleClass(6)
        
        p = t.__dict__['StaticInt32Property']
        #p.SetValue(None, a)                # bug 363241
        #AreEqual(a, p.GetValue(None))

        # static property against instance        
        AssertErrorWithMatch(SystemError, "cannot set property", lambda: p.SetValue(x, a))
        #AssertErrorWithMatch(SystemError, "cannot get property", lambda: p.GetValue(x))  # bug 363242

        p = t.__dict__['InstanceInt32Property']
        p.SetValue(x, a)
        #AreEqual(p.GetValue(x), a)         # value type issue again

        # instance property against None
        AssertErrorWithMatch(SystemError, "cannot set property", lambda: p.SetValue(None, a))
        #AssertErrorWithMatch(SystemError, "cannot get property", lambda: p.GetValue(None))  # bug 363247
        
        p = t.__dict__['StaticSimpleStructProperty']
        p.__set__(None, b)
        #AreEqual(b.Flag, p.__get__(None).Flag)
        
        # do we care???
        #print p.__set__(x, b)
        #print p.__get__(x)
        
        p = t.__dict__['InstanceSimpleStructProperty']
        p.__set__(x, b)        
        #AreEqual(b.Flag, p.__get__(x).Flag)
        
        # do we care?
        #print p.__set__(None, b)
        #print p.__get__(None)
        
        p = t.__dict__['StaticSimpleClassProperty']
        p.__set__(None, c)              # similar to bug 363241
        #AreEqual(c, p.__get__(None))
        
        p = t.__dict__['InstanceSimpleClassProperty']
        p.__set__(x, c)        
        #AreEqual(c, p.__get__(x))

def test_delete():
    def del_p(): del ClassWithProperties.InstanceSimpleStructProperty
    AssertErrorWithMatch(AttributeError, 
        "cannot delete attribute 'InstanceSimpleStructProperty' of builtin type 'ClassWithProperties'",
        del_p)

    def del_p(): del ClassWithReadOnly.InstanceProperty
    AssertErrorWithMatch(AttributeError, 
        "cannot delete attribute 'InstanceProperty' of builtin type 'ClassWithReadOnly'",
        del_p)

def test_from_derived_type():
    t = DerivedClass
    x = t()
    a, b, c = 8, SimpleStruct(7), SimpleClass(6)
    
    t.StaticInt32Property  # read
    def f(): t.StaticInt32Property = a
    AssertErrorWithMatch(AttributeError, 
        "'DerivedClass' object has no attribute 'StaticInt32Property'", 
        f)   # write

    x.InstanceInt32Property = a
    AreEqual(a, x.InstanceInt32Property)

    Assert('StaticSimpleStructProperty' not in t.__dict__)
    Assert('InstanceSimpleStructProperty' not in t.__dict__)
    p = t.__bases__[0].__dict__['InstanceSimpleStructProperty']
    p.SetValue(x, b)
    AreEqual(b.Flag, p.GetValue(x).Flag)
    
    Assert('StaticSimpleClassProperty' not in t.__dict__)
    Assert('InstanceSimpleClassProperty' not in t.__dict__)
    p = t.__bases__[0].__dict__['InstanceSimpleClassProperty']
    p.__set__(x, c)
    AreEqual(c, p.__get__(x))


def test_other_reflected_property_ops():
    p = ClassWithProperties.InstanceSimpleStructProperty
    AssertError(TypeError, lambda: p())
    AssertError(TypeError, lambda: p[1])
    
def test_none_as_value():
    pass

run_test(__name__)
