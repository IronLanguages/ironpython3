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
Try different python calls to clr method with different signatures.
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

o = VariousParameters()

def test_0_1_args():

    # public void M100() { Flag.Reset(); Flag.Set(10); }
    f = o.M100
    f()
    AssertErrorWithMessage(TypeError, 'M100() takes no arguments (1 given)', lambda: f(1))
    f(*())
    AssertErrorWithMessage(TypeError, 'M100() takes no arguments (2 given)', lambda: f(*(1,2)))
    AssertErrorWithMessage(TypeError, 'M100() takes no arguments (1 given)', lambda: f(x = 10))
    AssertErrorWithMessage(TypeError, 'M100() takes no arguments (2 given)',lambda: f(x = 10, y = 20))
    f(**{})
    AssertErrorWithMessage(TypeError, 'M100() takes no arguments (1 given)', lambda: f(**{'x':10}))
    f(*(), **{})
    
    # public void M200(int arg) { Flag.Reset(); Flag.Set(arg); }
    f = o.M200
    AssertErrorWithMessage(TypeError, "M200() takes exactly 1 argument (0 given)", lambda: f())
    f(1)
    AssertErrorWithMessage(TypeError, "M200() takes exactly 1 argument (2 given)", lambda: f(1, 2))
    f(*(1,))
    f(1, *())
    AssertErrorWithMessage(TypeError, "M200() takes exactly 1 argument (2 given)", lambda: f(1, *(2,)))
    f(arg = 1); AssertError(NameError, lambda: arg)
    f(arg = 1, *())
    f(arg = 1, **{})
    f(**{"arg" : 1})
    f(*(), **{"arg" : 1})
    AssertErrorWithMessage(TypeError, "M200() takes exactly 1 argument (2 given)", lambda: f(1, arg = 1))
    AssertErrorWithMessage(TypeError, "M200() takes exactly 1 argument (2 given)", lambda: f(arg = 1, *(1,)))
    AssertErrorWithMessage(TypeError, "M200() got an unexpected keyword argument 'other'", lambda: f(other = 1))
    AssertErrorWithMessage(TypeError, "M200() takes exactly 1 argument (2 given)", lambda: f(1, other = 1))
    AssertErrorWithMessage(TypeError, "M200() takes exactly 1 argument (2 given)", lambda: f(other = 1, arg = 2))
    AssertErrorWithMessage(TypeError, "M200() takes exactly 1 argument (2 given)", lambda: f(arg = 1, other = 2))
    AssertErrorWithMessage(TypeError, "M200() takes exactly 1 argument (2 given)", lambda: f(arg = 1, **{'arg' : 2})) # msg

    # public void M201([DefaultParameterValue(20)] int arg) { Flag.Reset(); Flag.Set(arg); }
    f = o.M201
    f()
    f(1)
    AssertErrorWithMessage(TypeError, 'M201() takes at most 1 argument (2 given)', lambda: f(1, 2))# msg
    f(*())
    f(1, *())
    f(*(1,))
    AssertErrorWithMessage(TypeError, 'M201() takes at most 1 argument (3 given)', lambda: f(1, *(2, 3)))# msg
    AssertErrorWithMessage(TypeError, 'M201() takes at most 1 argument (2 given)', lambda: f(*(1, 2)))# msg
    f(arg = 1)
    f(arg = 1, *())
    f(arg = 1, **{})
    f(**{"arg" : 1})
    f(*(), **{"arg" : 1})
    AssertErrorWithMessage(TypeError, "M201() takes at most 1 argument (2 given)", lambda: f(1, arg = 1))# msg
    AssertErrorWithMessage(TypeError, "M201() takes at most 1 argument (2 given)", lambda: f(arg = 1, *(1,)))# msg
    AssertErrorWithMessage(TypeError, "M201() got an unexpected keyword argument 'other'", lambda: f(other = 1))
    AssertErrorWithMessage(TypeError, "M201() takes at most 1 argument (2 given)", lambda: f(1, other = 1))
    AssertErrorWithMessage(TypeError, "M201() takes at most 1 argument (2 given)", lambda: f(**{ "other" : 1, "arg" : 2}))
    AssertErrorWithMessage(TypeError, "M201() takes at most 1 argument (2 given)", lambda: f(arg = 1, other = 2))
    AssertErrorWithMessage(TypeError, "M201() takes at most 1 argument (2 given)", lambda: f(arg1 = 1, other = 2))
    AssertErrorWithMessage(TypeError, "M201() got an unexpected keyword argument 'arg1'", lambda: f(**{ "arg1" : 1}))

    # public void M202(params int[] arg) { Flag.Reset(); Flag.Set(arg.Length); }
    f = o.M202
    f()
    f(1)
    f(1,2)
    f(*())
    f(1, *(), **{})
    f(1, *(2, 3))
    f(*(1, 2, 3, 4))
    AssertErrorWithMessage(TypeError, "M202() got an unexpected keyword argument 'arg'", lambda: f(arg = 1))# msg
    AssertErrorWithMessage(TypeError, "M202() takes at least 0 arguments (2 given)", lambda: f(1, arg = 2))# msg
    AssertErrorWithMessage(TypeError, "M202() got an unexpected keyword argument 'arg'", lambda: f(**{'arg': 3}))# msg
    AssertErrorWithMessage(TypeError, "M202() got an unexpected keyword argument 'other'", lambda: f(**{'other': 4}))
    
    # public void M203([ParamDictionaryAttribute] IAttributesCollection arg) { Flag.Set(arg.Count); }
    f = o.M203
    f()
    AssertErrorWithMessage(TypeError, "M203() takes no arguments (1 given)", lambda: f(1))
    AssertErrorWithMessage(TypeError, "M203() takes no arguments (1 given)", lambda: f({'a':1}))
    f(a=1)
    f(a=1, b=2)
    f(**{})
    f(**{'a':2, 'b':3})
    f(a=1, **{'b':2, 'c':5})
    AssertErrorWithMessage(TypeError, "M203() got multiple values for keyword argument 'a'", lambda: f(a=1, **{'a':2, 'c':5}))
    AssertErrorWithMessage(TypeError, "M203() takes no arguments (3 given)", lambda: f(*(1,2,3)))
    

def test_optional():
    #public void M231([Optional] int arg) { Flag.Set(arg); }  // not reset any
    #public void M232([Optional] bool arg) { Flag<bool>.Set(arg); }
    #public void M233([Optional] object arg) { Flag<object>.Set(arg); }
    #public void M234([Optional] string arg) { Flag<string>.Set(arg); }
    #public void M235([Optional] EnumInt32 arg) { Flag<EnumInt32>.Set(arg); }
    #public void M236([Optional] SimpleClass arg) { Flag<SimpleClass>.Set(arg); }
    #public void M237([Optional] SimpleStruct arg) { Flag<SimpleStruct>.Set(arg); }

    ## testing the passed in value, and the default values
    o.M231(12); Flag.Check(12)
    o.M231(); Flag.Check(0)
    
    o.M232(True); Flag[bool].Check(True) 
    o.M232(); Flag[bool].Check(False)
        
    def t(): pass
    o.M233(t); Flag[object].Check(t)
    o.M233(); Flag[object].Check(System.Type.Missing.Value)
    
    o.M234("ironpython"); Flag[str].Check("ironpython")
    o.M234(); Flag[str].Check(None)
    
    o.M235(EnumInt32.B); Flag[EnumInt32].Check(EnumInt32.B)
    o.M235(); Flag[EnumInt32].Check(EnumInt32.A)
    
    x = SimpleClass(23)
    o.M236(x); Flag[SimpleClass].Check(x)
    o.M236(); Flag[SimpleClass].Check(None)
    
    x = SimpleStruct(24)
    o.M237(x); Flag[SimpleStruct].Check(x)
    o.M237(); AreEqual(Flag[SimpleStruct].Value1.Flag, 0) 
    
    ## testing the argument style
    f = o.M231
    
    f(*()); Flag.Check(0)
    f(*(2, )); Flag.Check(2)
    f(arg = 3); Flag.Check(3)
    f(**{}); Flag.Check(0)
    f(*(), **{'arg':4}); Flag.Check(4)
    
    AssertErrorWithMessage(TypeError, "M231() takes at most 1 argument (2 given)", lambda: f(1, 2))  # msg
    AssertErrorWithMessage(TypeError, "M231() takes at most 1 argument (2 given)", lambda: f(1, **{'arg': 2}))  # msg
    AssertErrorWithMessage(TypeError, "M231() takes at most 1 argument (2 given)", lambda: f(arg = 3, **{'arg': 4}))  # msg
    AssertErrorWithMessage(TypeError, "M231() takes at most 1 argument (2 given)", lambda: f(arg = 3, **{'other': 4}))  # msg
    
def test_two_args():
    #public void M300(int x, int y) { }
    f = o.M300
    AssertErrorWithMessage(TypeError, "M300() takes exactly 2 arguments (0 given)", lambda: f())
    AssertErrorWithMessage(TypeError, "M300() takes exactly 2 arguments (1 given)", lambda: f(1))
    f(1, 2)
    AssertErrorWithMessage(TypeError, "M300() takes exactly 2 arguments (3 given)", lambda: f(1, 2, 3))
    
    AssertErrorWithMessage(TypeError, "M300() takes exactly 2 arguments (0 given)", lambda: f(*()))
    AssertErrorWithMessage(TypeError, "M300() takes exactly 2 arguments (1 given)", lambda: f(*(1,)))
    f(1, *(2,))
    f(*(3, 4))
    AssertErrorWithMessage(TypeError, "M300() takes exactly 2 arguments (3 given)", lambda: f(1, *(2, 3)))
    
    AssertErrorWithMessage(TypeError, "M300() takes exactly 2 arguments (1 given)", lambda: f(y = 1))
    f(y = 2, x = 1)
    AssertErrorWithMessage(TypeError, "M300() got an unexpected keyword argument 'x2'", lambda: f(y = 1, x2 = 2))
    AssertErrorWithMessage(TypeError, "M300() takes exactly 2 arguments (3 given)", lambda: f(x = 1, y = 1, z = 3))
    #AssertError(SyntaxError, eval, "f(x=1, y=2, y=3)")
    
    AssertErrorWithMessage(TypeError, "M300() takes exactly 2 arguments (1 given)", lambda: f(**{"x":1}))  # msg
    f(**{"x":1, "y":2})
    
    # ...
    
    # mixed
    # positional/keyword
    f(1, y = 2)
    AssertErrorWithMessage(TypeError, "M300() got multiple values for keyword argument 'x'", lambda: f(2, x = 1))    # msg    
    AssertErrorWithMessage(TypeError, "M300() takes exactly 2 arguments (3 given)", lambda: f(1, y = 1, x = 2)) # msg
    
    # positional / **
    f(1, **{'y': 2})
    AssertErrorWithMessage(TypeError, "M300() got multiple values for keyword argument 'x'", lambda: f(2, ** {'x':1}))
    AssertErrorWithMessage(TypeError, "M300() takes exactly 2 arguments (3 given)", lambda: f(1, ** {'y':1, 'x':2})) 
    
    # keyword / *
    f(y = 2, *(1,))
    AssertErrorWithMessage(TypeError, "M300() takes exactly 2 arguments (3 given)", lambda: f(y = 2, *(1,2)))
    AssertErrorWithMessage(TypeError, "M300() takes exactly 2 arguments (3 given)", lambda: f(y = 2, x = 1, *(3,)))
    
    # keyword / **
    f(y = 2, **{'x' : 1})
    
    #public void M350(int x, params int[] y) { }
    
    f = o.M350
    AssertErrorWithMessage(TypeError, "M350() takes at least 1 argument (0 given)", lambda: f())
    f(1)
    f(1, 2)
    f(1, 2, 3, 4, 5, 6, 7, 8, 9, 10)
    
    AssertErrorWithMessage(TypeError, "M350() takes at least 1 argument (0 given)", lambda: f(*()))
    f(*(1,))
    f(1, 2, *(3, 4))
    f(1, 2, *())
    f(1, 2, 3, *(4, 5, 6, 7, 8, 9, 10))
    
    f(x = 1)
    AssertErrorWithMessage(TypeError, "M350() got an unexpected keyword argument 'y'", lambda: f(x = 1, y = 2))
    
    f(**{'x' : 1})
    AssertErrorWithMessage(TypeError, "M350() got an unexpected keyword argument 'y'", lambda: f(**{'x' : 1, 'y' : 2}))
    AssertErrorWithMessage(TypeError, "M350() got multiple values for keyword argument 'x'", lambda: f(2, 3, 4, x = 1))
    
    # TODO: mixed 
    f(x = 1)  # check the value
    
    #public void M351(int x, [ParamDictionary] IAttributesCollection arg) { Flag<object>.Set(arg); }
    f = o.M351
    AssertErrorWithMessage(TypeError, "M351() takes exactly 1 argument (0 given)", lambda: f())
    f(1); AreEqual(Flag[object].Value1, {})
    f(1, a=3); AreEqual(Flag[object].Value1, {'a':3})
    f(1, a=3,b=4); AreEqual(Flag[object].Value1, {'a':3, 'b':4})
    f(1, a=3, **{'b':4, 'd':5}); AreEqual(Flag[object].Value1, {'a':3, 'b':4, 'd':5})
    f(x=1); AreEqual(Flag[object].Value1, {})
    f(**{'x' : 1}); AreEqual(Flag[object].Value1, {})
    
    #public void M352([ParamDictionary] IAttributesCollection arg, params int[] x) { Flag<object>.Set(arg); }
    
    f=o.M352
    f(); AreEqual(Flag[object].Value1, {})
    f(1); AreEqual(Flag[object].Value1, {})
    f(1,2,3); AreEqual(Flag[object].Value1, {})    
    f(a=1,b=2); AreEqual(Flag[object].Value1, {'a':1, 'b':2})
    f(1,2,3, a=1); AreEqual(Flag[object].Value1, {'a':1})
    f(a=1, *(1,2,3)); AreEqual(Flag[object].Value1, {'a':1})
    f(*(1,2,3), **{'a':1, 'b':2}); AreEqual(Flag[object].Value1, {'a':1, 'b':2})
    f(*(), **{}); AreEqual(Flag[object].Value1, {})    
    
def test_default_values_2():
    # public void M310(int x, [DefaultParameterValue(30)]int y) { Flag.Reset(); Flag.Set(x + y); }
    f = o.M310
    AssertErrorWithMessage(TypeError, "M310() takes at least 1 argument (0 given)", f) 
    f(1); Flag.Check(31)
    f(1, 2); Flag.Check(3)
    AssertErrorWithMessage(TypeError, "M310() takes at most 2 arguments (3 given)", lambda : f(1, 2, 3))
    
    f(x = 2); Flag.Check(32)
    f(4, y = 5); Flag.Check(9)
    f(y = 7, x = 10); Flag.Check(17)
    f(*(8,)); Flag.Check(38)
    f(*(9, 10)); Flag.Check(19)
    
    f(1, **{'y':2}); Flag.Check(3)
    
    # public void M320([DefaultParameterValue(40)] int y, int x) { Flag.Reset(); Flag.Set(x + y); }
    f = o.M320
    AssertErrorWithMessage(TypeError, "M320() takes at least 1 argument (0 given)", f) 
    f(1); Flag.Check(41)  # !!!
    f(2, 3); Flag.Check(5)
    AssertErrorWithMessage(TypeError, "M320() takes at most 2 arguments (3 given)", lambda : f(1, 2, 3))
    
    f(x = 2); Flag.Check(42)
    f(x = 2, y = 3); Flag.Check(5)
    f(*(1,)); Flag.Check(41)
    f(*(1, 2)); Flag.Check(3)
    
    AssertErrorWithMessage(TypeError, "M320() got multiple values for keyword argument 'y'", lambda : f(5, y = 6)) # !!!
    f(6, x = 7); Flag.Check(13)
    
    # public void M330([DefaultParameterValue(50)] int x, [DefaultParameterValue(60)] int y) { Flag.Reset(); Flag.Set(x + y); }
    f = o.M330
    f(); Flag.Check(110)
    f(1); Flag.Check(61)
    f(1, 2); Flag.Check(3)
    
    f(x = 1); Flag.Check(61)
    f(y = 2); Flag.Check(52)
    f(y = 3, x = 4); Flag.Check(7)
    
    f(*(5,)); Flag.Check(65)
    f(**{'y' : 6}); Flag.Check(56)

def test_3_args():
    # public void M500(int x, int y, int z) { Flag.Reset(); Flag.Set(x * 100 + y * 10 + z); }
    f = o.M500
    f(1, 2, 3); Flag.Check(123)
    f(y = 1, z = 2, x = 3); Flag.Check(312)
    f(3, *(2, 1)); Flag.Check(321)
    f(1, z = 2, **{'y':3}); Flag.Check(132)
    f(z = 1, **{'x':2, 'y':3}); Flag.Check(231)
    f(1, z = 2, *(3,)); #Flag.Check(132)

    # public void M510(int x, int y, [DefaultParameterValue(70)] int z) { Flag.Reset(); Flag.Set(x * 100 + y * 10 + z); }
    f = o.M510
    f(1, 2); Flag.Check(120 + 70)
    f(2, y = 1); Flag.Check(210 + 70)

    f(1, 2, 3); Flag.Check(123)
    
    # public void M520(int x, [DefaultParameterValue(80)]int y, int z) { Flag.Reset(); Flag.Set(x * 100 + y * 10 + z); }
    f = o.M520
    f(1, 2); Flag.Check(102 + 800)
    f(2, z = 1); Flag.Check(201 + 800)
    f(z=1, **{'x': 2}); Flag.Check(201 + 800)
    f(2, *(1,)); Flag.Check(201 + 800)
    
    f(1, z = 2, y = 3); Flag.Check(132)
    f(1, 2, 3); Flag.Check(123)
    
    # public void M530([DefaultParameterValue(90)]int x, int y, int z) { Flag.Reset(); Flag.Set(x * 100 + y * 10 + z); }
    f = o.M530
    f(1, 2); Flag.Check(12 + 9000)
    f(3, z = 4); Flag.Check(34 + 9000)
    f(*(5,), **{'z':6}); Flag.Check(56 + 9000)
    AssertErrorWithMessage(TypeError, "M530() got multiple values for keyword argument 'y'", lambda: f(2, y = 2)) # msg
    
    f(1, 2, 3); Flag.Check(123)
    
    # public void M550(int x, int y, params int[] z) { Flag.Reset(); Flag.Set(x * 100 + y * 10 + z.Length); }
    f = o.M550
    f(1, 2); Flag.Check(120)
    f(1, 2, 3); Flag.Check(121)
    f(1, 2, 3, 4, 5, 6, 7, 8, 9, 10); Flag.Check(128)
    
    f(1, 2, *()); Flag.Check(120)
    f(1, 2, *(3,)); Flag.Check(121)
    
    # bug 311155
    ##def  f(x, y, *z): print x, y, z
    #f(1, y = 2); Flag.Check(120)
    #f(x = 2, y = 3); Flag.Check(230)
    #f(1, y = 2, *()); Flag.Check(120)

    #f(1, y = 2, *(3, )); Flag.Check(121)
    #f(y = 2, x = 3; *(3, 4)); Flag.Check(322)
    #f(1, *(2, 3), **{'y': 4}); Flag.Check(142)
    #f(*(4, 5, 6), **{'y':7}); Flag.Check(472)
    #f(*(1, 2, 0, 1), **{'y':3, 'x':4}); Flag.Check(434)

def test_many_args():
    #public void M650(int arg1, int arg2, int arg3, int arg4, int arg5, int arg6, int arg7, int arg8, int arg9, int arg10) { }
    f = o.M650
    expect = "1 2 3 4 5 6 7 8 9 10"
    
    f(1, 2, 3, 4, 5, 6, 7, 8, 9, 10); Flag[str].Check(expect)
    
    #def f(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10): print arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10
    f(arg2 = 2, arg3 = 3, arg4 = 4, arg5 = 5, arg6 = 6, arg7 = 7, arg8 = 8, arg9 = 9, arg10 = 10, arg1 = 1); Flag[str].Check(expect) 
    f(1, 2, arg6 = 6, arg7 = 7, arg8 = 8, arg3 = 3, arg4 = 4, arg5 = 5, arg9 = 9, arg10 = 10); Flag[str].Check(expect) 

    #AssertErrorWithMessage(TypeError, "M650() got multiple values for keyword argument 'arg5'", lambda: f(1, 2, 3, arg5 = 5, *(4, 6, 7, 8, 9, 10))) 
    #AssertErrorWithMessage(TypeError, "M650() got multiple values for keyword argument 'arg1'", lambda: f(arg3 = 3, arg2 = 2, arg1 = 1, *(4, 5, 6, 7, 9, 10), **{'arg8': 8})) 
    #AssertErrorWithMessage(TypeError, "M650() got multiple values for keyword argument 'arg3'", lambda: f(1, 2, 4, 5, 6, 7, 8, 9, 10, **{'arg3' : 3})) 
    
    f(1, 2, 3, arg9 = 9, arg10 = 10, *(4, 5, 6, 7, 8)); # Flag[str].Check(expect)  # bug 311195
    f(1, 2, 3, arg10 = 10, *(4, 5, 6, 7, 8), ** {'arg9': 9}); # Flag[str].Check(expect) # bug 311195
    
    AssertErrorWithMessage(TypeError, "M650() got multiple values for keyword argument 'arg5'", lambda: f(2, 3, arg5 = 5, arg10 = 10, *(4, 6, 7, 9), **{'arg8': 8, 'arg1': 1})) # msg (should be 6 given)
    
    #public void M700(int arg1, string arg2, bool arg3, object arg4, EnumInt16 arg5, SimpleClass arg6, SimpleStruct arg7) { }
    
    f = o.M700

def test_special_name():
    #// keyword argument name, or **dict style
    #public void M800(int True) { }
    #public void M801(int def) { }
    
    f = o.M800
    f(True=9); Flag.Check(9)
    AreEqual(str(True), "True")
    
    f(**{"True": 19}); Flag.Check(19)

    f = o.M801
    AssertError(SyntaxError, eval, "f(def = 3)")
    f(**{"def": 8}); Flag.Check(8)
    
def test_1_byref_arg():
    obj = ByRefParameters()

    #public void M100(ref int arg) { arg = 1; }
    f = obj.M100
    
    AreEqual(f(2), 1)
    AreEqual(f(arg = 3), 1)
    AreEqual(f(*(4,)), 1)
    AreEqual(f(**{'arg': 5}), 1)
    
    x = box_int(6); AreEqual(f(x), None); AreEqual(x.Value, 1)
    x = box_int(7); f(arg = x); AreEqual(x.Value, 1)
    x = box_int(8); f(*(x,)); AreEqual(x.Value, 1)
    x = box_int(9); f(**{'arg':x}); AreEqual(x.Value, 1)
    
    #public void M120(out int arg) { arg = 2; }
    f = obj.M120
    AreEqual(f(), 2)
    #AssertError(TypeError, lambda: f(1))  # bug 311218
    
    x = box_int(); AreEqual(f(x), None); AreEqual(x.Value, 2)
    x = box_int(7); f(arg = x); AreEqual(x.Value, 2)
    x = box_int(8); f(*(x,)); AreEqual(x.Value, 2)
    x = box_int(9); f(**{'arg':x}); AreEqual(x.Value, 2)

def test_2_byref_args():
    obj = ByRefParameters()

    #public void M200(int arg1, ref int arg2) { Flag.Reset(); Flag.Value1 = arg1 * 10 + arg2; arg2 = 10; }
    f = obj.M200
    AreEqual(f(1, 2), 10); Flag.Check(12)
    AreEqual(f(3, arg2 = 4), 10); Flag.Check(34)
    AreEqual(f(arg2 = 6, arg1 = 5), 10); Flag.Check(56)
    AreEqual(f(*(7, 8)), 10); Flag.Check(78)
    AreEqual(f(9, *(1,)), 10); Flag.Check(91)
    
    x = box_int(5); AreEqual(f(1, x), None); AreEqual(x.Value, 10); Flag.Check(15)
    x = box_int(6); f(2, x); AreEqual(x.Value, 10); Flag.Check(26)
    x = box_int(7); f(3, *(x,)); AreEqual(x.Value, 10); Flag.Check(37)
    x = box_int(8); f(**{'arg1': 4, 'arg2' : x}); AreEqual(x.Value, 10); Flag.Check(48)
    
    #public void M201(ref int arg1, int arg2) { Flag.Reset(); Flag.Value1 = arg1 * 10 + arg2; arg1 = 20; }
    f = obj.M201
    AreEqual(f(1, 2), 20)
    x = box_int(2); f(x, *(2,)); AreEqual(x.Value, 20); Flag.Check(22)
    
    #public void M202(ref int arg1, ref int arg2) { Flag.Reset(); Flag.Value1 = arg1 * 10 + arg2; arg1 = 30; arg2 = 40; }
    f = obj.M202
    AreEqual(f(1, 2), (30, 40))
    AreEqual(f(arg2 = 1, arg1 = 2), (30, 40)); Flag.Check(21)
    
    AssertErrorWithMessage(TypeError, "expected int, got StrongBox[int]", lambda: f(box_int(3), 4))  # bug 311239
    x = box_int(3)
    y = box_int(4)
    #f(arg2 = y, *(x,)); Flag.Check(34) # bug 311169
    AssertErrorWithMessage(TypeError, "M202() got multiple values for keyword argument 'arg1'", lambda: f(arg1 = x, *(y,))) # msg
    
    # just curious
    x = y = box_int(5)
    f(x, y); AreEqual(x.Value, 40); AreEqual(y.Value, 40); Flag.Check(55)

def test_2_out_args():
    obj = ByRefParameters()
    
    #public void M203(int arg1, out int arg2) { Flag.Reset(); Flag.Value1 = arg1 * 10; arg2 = 50; }
    f = obj.M203
    AreEqual(f(1), 50)
    AreEqual(f(*(2,)), 50)
    #AssertError(TypeError, lambda: f(1, 2))  # bug 311218
    
    x = box_int(4)
    f(1, x); AreEqual(x.Value, 50)
    
    #public void M204(out int arg1, int arg2) { Flag.Reset(); Flag.Value1 = arg2; arg1 = 60; }
    # TODO
    
    #public void M205(out int arg1, out int arg2) { arg1 = 70; arg2 = 80; }
    f = obj.M205
    AreEqual(f(), (70, 80))
    AssertErrorWithMessage(TypeError, "M205() takes at most 2 arguments (1 given)", lambda: f(1))
    #AssertErrorWithMessage(TypeError, "M205() ??)", lambda: f(1, 2))
    
    AssertErrorWithMessage(TypeError, "M205() takes at most 2 arguments (1 given)", lambda: f(arg2 = box_int(2)))
    AssertErrorWithMessage(TypeError, "M205() takes at most 2 arguments (1 given)", lambda: f(arg1 = box_int(2)))
    
    for l in [
        lambda: f(*(x, y)),
        lambda: f(x, y, *()),
        lambda: f(arg2 = y, arg1 = x, *()),
        lambda: f(x, arg2 = y, ),
        lambda: f(x, **{"arg2":y})
             ]:
        x, y = box_int(1), box_int(2)
        #print l
        l()
        AreEqual(x.Value, 70)
        AreEqual(y.Value, 80)
    
    
    #public void M206(ref int arg1, out int arg2) { Flag.Reset(); Flag.Value1 = arg1 * 10; arg1 = 10; arg2 = 20; }
    f = obj.M206
    AreEqual(f(1), (10, 20))
    AreEqual(f(arg1 = 2), (10, 20))
    AreEqual(f(*(3,)), (10, 20))
    AssertError(TypeError, lambda: f(box_int(5)))
   
    x, y = box_int(4), box_int(5)
    f(x, y); AreEqual(x.Value, 10); AreEqual(y.Value, 20); 
    
    #public void M207(out int arg1, ref int arg2) { Flag.Reset(); Flag.Value1 = arg2; arg1 = 30; arg2 = 40; }
    
    f = obj.M207
    AreEqual(f(1), (30, 40))
    AreEqual(f(arg2 = 2), (30, 40)); Flag.Check(2)
    #AssertError(TypeError, lambda: f(1, 2))
    AssertError(TypeError, lambda: f(arg2 = 1, arg1 = 2))
    
    for l in [ 
            lambda: f(x, y), 
            lambda: f(arg2 = y, arg1 = x),
            lambda: f(x, *(y,)),
            lambda: f(*(x, y,)),
            #lambda: f(arg1 = x, *(y,)),
            lambda: f(arg1 = x, **{'arg2': y}),
             ]:
        x, y = box_int(1), box_int(2)
        #print l
        l()
        AreEqual(x.Value, 30)
        AreEqual(y.Value, 40)
        Flag.Check(2)

def test_splatting_errors():
    # public void M200(int arg) { Flag.Reset(); Flag.Set(arg); }
    f = o.M200
    
    AssertErrorWithMessage(TypeError, "M200() argument after * must be a sequence, not NoneType", lambda: f(*None))
    AssertErrorWithMessage(TypeError, "expected int, got str", lambda: f(*'x'))
    AssertErrorWithMessage(TypeError, "M200() argument after * must be a sequence, not int", lambda: f(*1))

run_test(__name__)

