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
How to define new, equivalent constructors.
'''
#------------------------------------------------------------------------------

from iptest.assert_util import *
skiptest("silverlight")

add_clr_assemblies("baseclasscs", "typesamples")

from Merlin.Testing import *
from Merlin.Testing.BaseClass import *

from System import Array
from clr import StrongBox
box_int = StrongBox[int]
array_int = Array[int]

# http://docs.python.org/ref/customization.html

def test_0():
    class C(CCtor10): pass
    C()
    Flag.Check(42)
    AssertErrorWithMessage(TypeError, "object.__new__() takes no parameters", C, 1)

    class C(CCtor10): 
        def __new__(cls, arg1, arg2):
            return super(C, cls).__new__(cls)
    C(1, 2)
    Flag.Check(42)

def test_1_normal(): 
    class C(CCtor20): pass
    C(1)
    Flag.Check(1)
    
    AssertError(TypeError, C)
    AssertError(TypeError, C, 1, 2)
    
    class C(CCtor20):
        def __new__(cls, arg):
            return super(C, cls).__new__(cls, arg)
   
    C(2)
    Flag.Check(2)
    
    class C(CCtor20):
        def __new__(cls, arg):
            return super(C, cls).__new__(cls)
            
    AssertError(TypeError, C, 2)
    
    class C(CCtor20):
        def __new__(cls):
            return super(C, cls).__new__(cls, 3)
    
    C()
    Flag.Check(3)

def test_1_ref():
    class C(CCtor21): pass
    #x, y = C(1)
    #Flag.Check(1)
    #AreEqual(y, 1)   # 313045
    
    #y = box_int(2)
    #C(y)
    #Flag.Check(2)
    #AreEqual(y.Value, 2)  # 313045
    
    # TODO
    class C(CCtor21):
        def __new__(cls, arg):
            return super(C, cls).__new__(cls, arg)
    C(3)    
    
def test_1_array():
    class C1(CCtor30): pass
    class C2(CCtor30):
        def __new__(cls, arg):
            return super(cls, C2).__new__(cls, arg)

    for C in [C1, C2]:
        AssertError(TypeError, C)
        AssertError(TypeError, C, 1)
        
        C(array_int([]))
        Flag.Check(0)
        
        C(array_int([1, 2]))
        Flag.Check(3)
        
        #C(None)  # 374293
        #Flag.Check(-10)
    
def test_1_param_array():
    class C1(CCtor31): pass
    class C2(CCtor31):
        def __new__(cls, *arg):
            return super(cls, C2).__new__(cls, *arg)

    for C in [C1, C2]:
        C(); Flag.Check(-20)
        #C(None); Flag.Check(-40)
        C(1); Flag.Check(1)
        C(2, 3); Flag.Check(5)
        
        C(array_int([])); Flag.Check(-20)
        C(array_int([4, 5, 6])); Flag.Check(15)

        AssertError(TypeError, lambda: C([4, 5, 6]))

    class C3(CCtor31):
        def __new__(cls, arg):
            return super(cls, C3).__new__(cls, *arg)
            
    C3([1, 2]); Flag.Check(3)
    C(array_int([4, 5, 6])); Flag.Check(15)


def test_5_args():
    class C(CCtor40): pass
    C(1, 2, arg4=4, *(3, ), **{'arg5':5})
    Flag.Check(12345)
    
    class C(CCtor40):
        def __new__(cls, *args): 
            return super(cls, C).__new__(cls, *args)

    #AssertErrorWithMessage(TypeError, "CCtor40() takes exactly 6 arguments (2 given)", C, 2) # bug 374515
    C(2, 1, 3, 5, 4)
    Flag.Check(21354)
    
    class C(CCtor40):
        def __new__(cls, arg1, arg2, *arg3, **arg4): 
            return super(cls, C).__new__(cls, arg1, arg2, *arg3, **arg4)
    
    AssertErrorWithMessage(TypeError, "__new__() got multiple values for keyword argument 'arg2'", eval, "C(3, arg2=1, *(2, 5), **{'arg5' : 4})", globals(), locals())
    
    C(3, 1, *(2, 5), **{'arg5' : 4})
    Flag.Check(31254)

def test_overload1():
    class C(CCtor50):
        def __new__(cls, arg):
            return super(C, cls).__new__(cls, arg)
    C(1)
    Flag.Check(1)

    AssertError(TypeError, C, 1, 2)
    
    class C(CCtor50):
        def __new__(cls, arg):
            return super(C, cls).__new__(cls, arg, 10)
    C(2)
    Flag.Check(12)
    
    class C(CCtor50):
        def __new__(cls, arg1, arg2):
            return super(C, cls).__new__(cls, arg1 + arg2)
            
    C(3, 4)
    Flag.Check(7)
    
    AssertError(TypeError, C, 3)
    
    class C(CCtor50):
        def __new__(cls, arg1, arg2):
            return super(C, cls).__new__(cls, arg1, arg2)
    C(5, 6)
    Flag.Check(11)
   
def test_overload2():
    class C1(CCtor51):
        def __new__(cls, *args): 
            return super(cls, C1).__new__(cls, *args)
    class C2(CCtor51):
        def __new__(cls, arg1, *arg2): 
            return super(cls, C2).__new__(cls, arg1, *arg2)

    # more?
    
    for C in [C1, C2]:
        C(1);         Flag.Check(10)
        C(1, 2);      Flag.Check(20)
        C(array_int([1, 2, 3]));        Flag.Check(20)
        
    
def test_related_to_init():    
    class C(CCtor20):
        def __new__(cls, arg):
            x = super(C, cls).__new__(cls, arg)
            Flag.Check(arg)
            Flag.Set(arg * 2)
            return x
        def __init__(self, arg):
            Flag.Check(arg * 2)
            Flag.Set(arg * 3)
    
    C(4)
    Flag.Check(12)
    
    C.__init__ = lambda self, arg1, arg2: None
    AssertError(TypeError, C, 4)
    
    C.__init__ = lambda self, arg: arg
    #AssertError(TypeError, C, 4) # bug 374136
    
    class C(CCtor20):
        def __new__(cls, arg):
            super(C, cls).__new__(cls, arg)  # no return
        def __init__(self, arg):
            Flag.Set(2)                      # then not called here
    
    C(5)
    Flag.Check(5)

    class C(CCtor20):                        # no explicit __new__
        def __init__(self, arg):
            Flag.Check(6)
            Flag.Set(7)
    C(6)
    Flag.Check(7)
    
run_test(__name__)

