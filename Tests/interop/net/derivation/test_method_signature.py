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
Various method signatures to override.
'''
#------------------------------------------------------------------------------

from iptest.assert_util import *
skiptest("silverlight")

add_clr_assemblies("baseclasscs", "typesamples")

from Merlin.Testing import *
from Merlin.Testing.BaseClass import *

from clr import StrongBox
box_int = StrongBox[int]
    
def test_one_ref():
    f = IInterface600.m_a
    
    class C(IInterface600): 
        def m_a(self, arg):
            arg.Value = arg.Value + 3
            
    x = C()
    a = box_int(10)
    AreEqual(f(x, a), None)
    AreEqual(a.Value, 13)
    
    a = 20
    AreEqual(f(x, a), 23)
    AreEqual(a, 20)
    
    a = box_int(10)
    x.m_a(a)
    AreEqual(a.Value, 13)
    
    a = 20
    AssertError(AttributeError, x.m_a, a)
    
    # inproper usage ...
    class C(IInterface600):
        def m_a(self, arg):
            arg = 20
            
    x = C()
    a = box_int(10)
    AreEqual(f(x, a), None)
    AreEqual(a.Value, 10)
    
    a = 11
    AreEqual(f(x, a), 11)
    AreEqual(a, 11)
    
    class C(IInterface600):
        def m_a(self):
            pass
    x = C()
    a = box_int(10)
    AssertError(TypeError, f, x, a)

global expected

@skip("multiple_execute")
def test_one_out():
    f = IInterface600.m_b
    
    class C(IInterface600):
        def m_b(self, arg):
            AreEqual(arg.Value, expected)
            arg.Value = 14
    x = C()
    a = box_int(10)
    expected = 10
    AreEqual(f(x, a), None)
    AreEqual(a.Value, 14)
    
    a = box_int(10)
    expected = 10
    x.m_b(a)
    AreEqual(a.Value, 14)
    
    AssertErrorWithMessage(TypeError, 
        "expected StrongBox[int], got int", 
        f, x, 10)
    AssertErrorWithMessage(TypeError, 
        "expected StrongBox[int], got NoneType", 
        f, x, None)
    
    class C(IInterface600):
        def m_b(self, arg): pass    # do not assign arg in the body
    x = C()
    a = box_int(10)
    f(x, a)
    AreEqual(a.Value, 10)

    class C(IInterface600):
        def m_b(self): return 16    # omit "out" arg
    x = C()
    AssertErrorWithMessage(TypeError, 
        "m_b() takes exactly 1 argument (2 given)", 
        f, x)                       # bug 370002
    AssertErrorWithMessage(TypeError, 
        "expected StrongBox[int], got int", 
        f, x, 1)
    a = box_int(10)
    AssertErrorWithMessage(TypeError, 
        "m_b() takes exactly 1 argument (2 given)", 
        f, x, a)

def test_one_array():
    f = IInterface600.m_c
    
    class C(IInterface600):
        def m_c(self, arg):
            if arg:
                AreEqual(sum(arg), expected)
                arg[0] = 10

    x = C()
    a = System.Array[int]([1,2])
    expected = 3
    f(x, a)
    AreEqual(a[0], 10)
    
    f(x, None)
    
def test_one_param_array():
    f = IInterface600.m_d
    
    class C(IInterface600):
        def m_d(self, *arg): 
            AreEqual(len(arg), expected)

    x = C()
    expected = 0
    f(x)
    
    expected = 1
    f(x, 1)
    
    expected = 3
    f(x, *(1, 2, 3))
    
    class C(IInterface600):
        def m_d(self):
            pass
    x = C()
    f(x)
    AssertError(TypeError, f, x, 1)
    
    class C(IInterface600):
        def m_d(self, arg1, arg2, arg3):
            pass
    x = C()
    f(x, 1, 2, 3)
    AssertError(TypeError, f, x, 1)

    class C(IInterface600):
        def m_d(self, arg1, *arg2):
            AreEqual(len(arg2) + arg1, expected)
            
    x = C()
    expected = 1; f(x, 1)
    expected = 2; f(x, 1, 2)
    expected = 3; f(x, 1, 2, 3)
    expected = 4; f(x, 1, 2, 3, 4)
    expected = 5; f(x, 1, 2, 3, 4, 5)

def test_return_something():
    f = IInterface600.m_e
    
    class C(IInterface600):
        def m_e(self): 
            pass
    x = C()
    AssertErrorWithMessage(TypeError,
        "expected int, got NoneType", 
        f, x)
    
    C.m_e = lambda self: 10
    AreEqual(f(x), 10)
    
    C.m_e = lambda self: "abc"
    AssertErrorWithMessage(TypeError,
        "expected int, got str", 
        f, x)
        
        
    f = IInterface600.m_f
    class C(IInterface600):
        def m_f(self): 
            return 10
    x = C()            
    AreEqual(f(x), None)

@skip("multiple_execute")            
def test_two_args():
    f = IInterface600.m_g
    class C(IInterface600):
        def m_g(self, arg1, arg2):
            temp = arg1.Value + 9
            arg1.Value = arg2 + 8
            return temp
    
    x = C()
    
    a = box_int(10)
    b = 20
    
    AreEqual(f(x, a, b), 19)
    AreEqual(a.Value, 28)
    
    AreEqual(f(x, 1, 2), (10, 10))
    
    f = IInterface600.m_h

    class C(IInterface600):
        def m_h(self, arg1, arg2): 
            AreEqual(arg1.Value, 10)
            arg1.Value = arg2.Value + 7
            arg2.Value = 6
            return 5
            
    x = C()
    
    a = box_int(10)
    b = box_int(20)
    AreEqual(f(x, a, b), 5)
    AreEqual(a.Value, 27)
    AreEqual(b.Value, 6)
    
    AssertError(TypeError, f, x, a, 20)
    AssertError(TypeError, f, x, 10, b)
    AssertError(TypeError, f, x, 10, 20)

def test_ref_out_normal():
    f = IInterface600.m_l
    
    class C(IInterface600):
        def m_l(self, arg1, arg2, arg3): 
            return 1
    
    x = C()
    a = box_int(10)
    b = box_int(20)
    c = 30
    
    #f(x, a, b, c) # bug 370075
    
    C.m_l = lambda self, arg1, arg2, arg3, *arg4: 2
    #f(x, a, b, c, 1, 2)

    C.m_l = lambda self, arg1, arg2, arg3, arg4, arg5: 3
    #f(x, a, b, c, 1, 2)
    
    f = IInterface600.m_k
    
    class C(IInterface600):
        def m_k(self, arg1, arg2, arg3, arg4):
            arg2.Value = arg1.Value * 3
            arg1.Value = arg3 * 5
            temp = sum(arg4)
            arg4[0] = 100            
            return temp

    x = C()
    a = box_int(10)
    b = box_int(20)
    c = 3
    d = System.Array[int]([40, 50])
    AreEqual(f(x, a, b, c, d), 90)
    AreEqual(a.Value, 15)
    AreEqual(b.Value, 30)
    AreEqual(d[0], 100)
    

run_test(__name__)
