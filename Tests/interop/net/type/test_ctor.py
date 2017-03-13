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
Calls to constructor.
'''
#------------------------------------------------------------------------------
from iptest.assert_util import *
skiptest("silverlight")

add_clr_assemblies("methodargs", "typesamples")

from Merlin.Testing import *
from Merlin.Testing.Call import *
from Merlin.Testing.TypeSample import *

from clr import StrongBox
box_int = StrongBox[int]

#// 1 argument 
#public class Ctor100 {
    #public Ctor100(int arg) { }
#}
#public class Ctor101 {
    #public Ctor101([DefaultParameterValue(10)]int arg) { }
#}
#public class Ctor102 {
    #public Ctor102([Optional]int arg) { }
#}
    
def test_ctor_1_arg():
    Ctor101()
    
    #public class Ctor103 {
    #   public Ctor103(params int[] arg) { }
    #}
    Ctor103()
    Ctor103(1)
    Ctor103(1, 2, 3)
    Ctor105(a=1,b=2,c=3)
    #public class Ctor110 {
    #   public Ctor110(ref int arg) { arg = 10; }
    #}
    
    
    x, y = Ctor110(2)

    x = box_int()
    Ctor110(x)
    AreEqual(x.Value, 10)  # bug 313045
    

    #public class Ctor111 {
    #   public Ctor111(out int arg) { arg = 10; }
    #}

    #Ctor111() # bug 312989

    #x = box_int()
    #Ctor111(x)
    #AreEqual(x.Value, 10)   # bug 313045
    
def test_object_array_as_ctor_args():
    from System import Array
    Ctor104(Array[object]([1,2]))
    
def test_ctor_keyword():
    def check(o):
        Flag[int, int, int].Check(1, 2, 3)
        AreEqual(o.Arg4, 4)
        Flag[int, int, int].Reset()
        
    x = 4
    o = Ctor610(1, arg2 = 2, Arg3 = 3, Arg4 = x); check(o)
    o = Ctor610(Arg3 = 3, Arg4 = x, arg1 = 1, arg2 = 2); check(o)
    #o = Ctor610(Arg3 = 3, Arg4 = x, *(1, 2)); check(o)

# parameter name is same as property
def test_ctor_keyword2():
    Ctor620(arg1 = 1)
    f = Flag[int, int, int, str]
    o = Ctor620(arg1 = 1, arg2 = 2); f.Check(1, 2, 0, None); f.Reset()
    o = Ctor620(arg1 = 1, arg2 = "hello"); f.Check(1, 0, 0, "hello"); f.Reset()
    #Ctor620(arg1 = 1, arg2 = 2, **{ 'arg1' : 3})
    pass

def test_ctor_bad_property_field():
    AssertErrorWithMessage(AttributeError, "Property ReadOnlyProperty is read-only", lambda: Ctor700(1, ReadOnlyProperty = 1))
    AssertErrorWithMessage(AttributeError, "Field ReadOnlyField is read-only", lambda: Ctor720(ReadOnlyField = 2))
    AssertErrorWithMessage(AttributeError, "Field LiteralField is read-only", lambda: Ctor730(LiteralField = 3))
    #AssertErrorWithMessage(AttributeError, "xxx", lambda: Ctor710(StaticField = 10))
    #AssertErrorWithMessage(AttributeError, "xxx", lambda: Ctor750(StaticProperty = 10))
    AssertErrorWithMessage(TypeError, "Ctor760() takes no arguments (1 given)", lambda: Ctor760(InstanceMethod = 1))
    AssertErrorWithMessage(TypeError, "expected EventHandler, got int", lambda: Ctor760(MyEvent = 1))

def test_set_field_for_value_type_in_ctor():
    # with all fields set
    x = Struct(IntField = 2, StringField = "abc", ObjectField = 4)
    AreEqual(x.IntField, 2)
    AreEqual(x.StringField, "abc")
    AreEqual(x.ObjectField, 4)

    # with partial field set
    x = Struct(StringField = "def")
    AreEqual(x.IntField, 0)
    AreEqual(x.StringField, "def")
    AreEqual(x.ObjectField, None)
    
    # with not-existing field as keyword
    # bug: 361389
    AssertErrorWithMessage(TypeError, 
        "CreateInstance() takes no arguments (2 given)", 
        lambda: Struct(IntField = 2, FloatField = 3.4))
    
    # set with value of "wrong" type
    # bug: 361389
    AssertErrorWithMessage(TypeError, 
        "expected str, got int", 
        lambda: Struct(StringField = 2))

def test_cp14861():
    def foo():
        x = Struct(IntField = 2, StringField = "abc", ObjectField = 4)
        AreEqual(x.IntField, 2)
        AreEqual(x.StringField, "abc")
        AreEqual(x.ObjectField, 4)
    for i in range(2):
        foo()
        exec("foo()", globals(), locals()) 

run_test(__name__)

