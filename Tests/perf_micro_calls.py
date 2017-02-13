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

import time
import sys
import clr
clr.AddReference('System.Drawing')
clr.AddReference('IronPythonTest')
import System.Drawing
import IronPythonTest

loops = 1000
"""
# simple tests
def zero_arg_maker(f):
    for x in range(loops):
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()

def one_arg_maker(f):
    for x in range(loops):
        f(1)
        f(1)
        f(1)
        f(1)
        f(1)
        f(1)
        f(1)
        f(1)
        f(1)
        f(1)
        f(1)
        f(1)
        f(1)
        f(1)
        f(1)
        f(1)
        f(1)
        f(1)
        f(1)
        f(1)
        f(1)
        f(1)
        f(1)
        f(1)
        f(1)
        f(1)
        f(1)

def test_one_arg():
    def f(a): pass
    
    one_arg_maker(f)
    
def test_one_arg_oldclass():
    class C:
        def __init__(self, other): pass
    
    one_arg_maker(C)
    
def test_one_arg_int():
    one_arg_maker(int)

def test_zero_arg_func():
    def f(): pass
    
    zero_arg_maker(f)

def test_zero_arg_oldclass():
    class C: pass
    
    zero_arg_maker(C)

def test_zero_arg_newclass():
    class C(object): pass
    
    zero_arg_maker(C)

def test_zero_arg_reflected_type():
    zero_arg_maker(int)

def test_two_args():
    global f
    def f(a, b): pass

    for x in range(loops):
        f(1, 2)
        f(1, 2)
        f(1, 2)
        f(1, 2)
        f(1, 2)
        f(1, 2)
        f(1, 2)
        f(1, 2)
        f(1, 2)
        f(1, 2)
        f(1, 2)
        f(1, 2)
        f(1, 2)
        f(1, 2)
        f(1, 2)
        f(1, 2)
        f(1, 2)
        f(1, 2)
        f(1, 2)
        f(1, 2)
        f(1, 2)
        f(1, 2)
        f(1, 2)
        f(1, 2)
        f(1, 2)
        f(1, 2)
        f(1, 2)
        
def test_three_args():
    global f
    def f(a, b, c): pass

    for x in range(loops):
        f(1, 2, 3)
        f(1, 2, 3)
        f(1, 2, 3)
        f(1, 2, 3)
        f(1, 2, 3)
        f(1, 2, 3)
        f(1, 2, 3)
        f(1, 2, 3)
        f(1, 2, 3)
        f(1, 2, 3)
        f(1, 2, 3)
        f(1, 2, 3)
        f(1, 2, 3)
        f(1, 2, 3)
        f(1, 2, 3)
        f(1, 2, 3)
        f(1, 2, 3)
        f(1, 2, 3)
        f(1, 2, 3)
        f(1, 2, 3)
        f(1, 2, 3)
        f(1, 2, 3)
        f(1, 2, 3)
        f(1, 2, 3)
        f(1, 2, 3)
        f(1, 2, 3)
        f(1, 2, 3)

def test_four_args():
    global f
    def f(a, b, c, d): pass

    for x in range(loops):
        f(1, 2, 3, 4)
        f(1, 2, 3, 4)
        f(1, 2, 3, 4)
        f(1, 2, 3, 4)
        f(1, 2, 3, 4)
        f(1, 2, 3, 4)
        f(1, 2, 3, 4)
        f(1, 2, 3, 4)
        f(1, 2, 3, 4)
        f(1, 2, 3, 4)
        f(1, 2, 3, 4)
        f(1, 2, 3, 4)
        f(1, 2, 3, 4)
        f(1, 2, 3, 4)
        f(1, 2, 3, 4)
        f(1, 2, 3, 4)
        f(1, 2, 3, 4)
        f(1, 2, 3, 4)
        f(1, 2, 3, 4)
        f(1, 2, 3, 4)
        f(1, 2, 3, 4)
        f(1, 2, 3, 4)
        f(1, 2, 3, 4)
        f(1, 2, 3, 4)
        f(1, 2, 3, 4)
        f(1, 2, 3, 4)
        f(1, 2, 3, 4)

def test_five_args():
    global f
    def f(a, b, c, d, e): pass

    for x in range(loops):
        f(1, 2, 3, 4, 5)
        f(1, 2, 3, 4, 5)
        f(1, 2, 3, 4, 5)
        f(1, 2, 3, 4, 5)
        f(1, 2, 3, 4, 5)
        f(1, 2, 3, 4, 5)
        f(1, 2, 3, 4, 5)
        f(1, 2, 3, 4, 5)
        f(1, 2, 3, 4, 5)
        f(1, 2, 3, 4, 5)
        f(1, 2, 3, 4, 5)
        f(1, 2, 3, 4, 5)
        f(1, 2, 3, 4, 5)
        f(1, 2, 3, 4, 5)
        f(1, 2, 3, 4, 5)
        f(1, 2, 3, 4, 5)
        f(1, 2, 3, 4, 5)
        f(1, 2, 3, 4, 5)
        f(1, 2, 3, 4, 5)
        f(1, 2, 3, 4, 5)
        f(1, 2, 3, 4, 5)
        f(1, 2, 3, 4, 5)
        f(1, 2, 3, 4, 5)
        f(1, 2, 3, 4, 5)
        f(1, 2, 3, 4, 5)
        f(1, 2, 3, 4, 5)
        f(1, 2, 3, 4, 5)

def test_six_args():
    global f
    def f(a, b, c, d, e, f): pass

    for x in range(loops):
        f(1, 2, 3, 4, 5, 6)
        f(1, 2, 3, 4, 5, 6)
        f(1, 2, 3, 4, 5, 6)
        f(1, 2, 3, 4, 5, 6)
        f(1, 2, 3, 4, 5, 6)
        f(1, 2, 3, 4, 5, 6)
        f(1, 2, 3, 4, 5, 6)
        f(1, 2, 3, 4, 5, 6)
        f(1, 2, 3, 4, 5, 6)
        f(1, 2, 3, 4, 5, 6)
        f(1, 2, 3, 4, 5, 6)
        f(1, 2, 3, 4, 5, 6)
        f(1, 2, 3, 4, 5, 6)
        f(1, 2, 3, 4, 5, 6)
        f(1, 2, 3, 4, 5, 6)
        f(1, 2, 3, 4, 5, 6)
        f(1, 2, 3, 4, 5, 6)
        f(1, 2, 3, 4, 5, 6)
        f(1, 2, 3, 4, 5, 6)
        f(1, 2, 3, 4, 5, 6)
        f(1, 2, 3, 4, 5, 6)
        f(1, 2, 3, 4, 5, 6)
        f(1, 2, 3, 4, 5, 6)
        f(1, 2, 3, 4, 5, 6)
        f(1, 2, 3, 4, 5, 6)
        f(1, 2, 3, 4, 5, 6)
        f(1, 2, 3, 4, 5, 6)
        
def test_seven_args():
    global f
    def f(a, b, c, d, e, f, g): pass

    for x in range(loops):
        f(1, 2, 3, 4, 5, 6, 7)
        f(1, 2, 3, 4, 5, 6, 7)
        f(1, 2, 3, 4, 5, 6, 7)
        f(1, 2, 3, 4, 5, 6, 7)
        f(1, 2, 3, 4, 5, 6, 7)
        f(1, 2, 3, 4, 5, 6, 7)
        f(1, 2, 3, 4, 5, 6, 7)
        f(1, 2, 3, 4, 5, 6, 7)
        f(1, 2, 3, 4, 5, 6, 7)
        f(1, 2, 3, 4, 5, 6, 7)
        f(1, 2, 3, 4, 5, 6, 7)
        f(1, 2, 3, 4, 5, 6, 7)
        f(1, 2, 3, 4, 5, 6, 7)
        f(1, 2, 3, 4, 5, 6, 7)
        f(1, 2, 3, 4, 5, 6, 7)
        f(1, 2, 3, 4, 5, 6, 7)
        f(1, 2, 3, 4, 5, 6, 7)
        f(1, 2, 3, 4, 5, 6, 7)
        f(1, 2, 3, 4, 5, 6, 7)
        f(1, 2, 3, 4, 5, 6, 7)
        f(1, 2, 3, 4, 5, 6, 7)
        f(1, 2, 3, 4, 5, 6, 7)
        f(1, 2, 3, 4, 5, 6, 7)
        f(1, 2, 3, 4, 5, 6, 7)
        f(1, 2, 3, 4, 5, 6, 7)
        f(1, 2, 3, 4, 5, 6, 7)
        f(1, 2, 3, 4, 5, 6, 7)


def default_test_maker(size):
    global f
    
    if size == 1:
        def f(a=1): pass
    elif size == 2:
        def f(a=1,b=2): pass
    elif size == 3:
        def f(a=1,b=2,c=3): pass
    elif size == 4:
        def f(a=1,b=2,c=3,d=4): pass
    elif size == 5:
        def f(a=1,b=2,c=3,d=4,e=5): pass
    elif size == 6:
        def f(a=1,b=2,c=3,d=4,e=5,f=6): pass
    elif size == 7:
        def f(a=1,b=2,c=3,d=4,e=5,f=6,g=7): pass
        
    for x in range(loops):
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
        f()
    
def kwdict_test_maker(size):
    global f
    
    if size == 1:
        def f(**a): pass
    elif size == 2:
        def f(a=1,**b): pass
    elif size == 3:
        def f(a=1,b=2,**c): pass
    elif size == 4:
        def f(a=1,b=2,c=3,**d): pass
    elif size == 5:
        def f(a=1,b=2,c=3,d=4,**e): pass
    elif size == 6:
        def f(a=1,b=2,c=3,d=4,e=5,**f): pass
    elif size == 7:
        def f(a=1,b=2,c=3,d=4,e=5,f=6,**g): pass

    dt = {}
    for x in range(loops):
        f(a=dt,b=dt,c=dt,d=dt,e=dt,f=dt,g=dt)
        f(a=dt,b=dt,c=dt,d=dt,e=dt,f=dt,g=dt)
        f(a=dt,b=dt,c=dt,d=dt,e=dt,f=dt,g=dt)
        f(a=dt,b=dt,c=dt,d=dt,e=dt,f=dt,g=dt)
        f(a=dt,b=dt,c=dt,d=dt,e=dt,f=dt,g=dt)
        f(a=dt,b=dt,c=dt,d=dt,e=dt,f=dt,g=dt)
        f(a=dt,b=dt,c=dt,d=dt,e=dt,f=dt,g=dt)
        f(a=dt,b=dt,c=dt,d=dt,e=dt,f=dt,g=dt)
        f(a=dt,b=dt,c=dt,d=dt,e=dt,f=dt,g=dt)
        f(a=dt,b=dt,c=dt,d=dt,e=dt,f=dt,g=dt)
        f(a=dt,b=dt,c=dt,d=dt,e=dt,f=dt,g=dt)
        f(a=dt,b=dt,c=dt,d=dt,e=dt,f=dt,g=dt)
        f(a=dt,b=dt,c=dt,d=dt,e=dt,f=dt,g=dt)
        f(a=dt,b=dt,c=dt,d=dt,e=dt,f=dt,g=dt)
        f(a=dt,b=dt,c=dt,d=dt,e=dt,f=dt,g=dt)
        f(a=dt,b=dt,c=dt,d=dt,e=dt,f=dt,g=dt)
        f(a=dt,b=dt,c=dt,d=dt,e=dt,f=dt,g=dt)
        f(a=dt,b=dt,c=dt,d=dt,e=dt,f=dt,g=dt)
        f(a=dt,b=dt,c=dt,d=dt,e=dt,f=dt,g=dt)
        f(a=dt,b=dt,c=dt,d=dt,e=dt,f=dt,g=dt)
        f(a=dt,b=dt,c=dt,d=dt,e=dt,f=dt,g=dt)
        f(a=dt,b=dt,c=dt,d=dt,e=dt,f=dt,g=dt)
        f(a=dt,b=dt,c=dt,d=dt,e=dt,f=dt,g=dt)
        f(a=dt,b=dt,c=dt,d=dt,e=dt,f=dt,g=dt)
        f(a=dt,b=dt,c=dt,d=dt,e=dt,f=dt,g=dt)
        f(a=dt,b=dt,c=dt,d=dt,e=dt,f=dt,g=dt)
        f(a=dt,b=dt,c=dt,d=dt,e=dt,f=dt,g=dt)

def splat_test_maker(size):
    global f
    
    if size == 1:
        def f(*a): pass
    elif size == 2:
        def f(a,*b): pass
    elif size == 3:
        def f(a,b,*c): pass
    elif size == 4:
        def f(a,b,c,*d): pass
    elif size == 5:
        def f(a,b,c,d,*e): pass
    elif size == 6:
        def f(a,b,c,d,e,*f): pass
    elif size == 7:
        def f(a,b,c,d,e,f,*g): pass
    
    l = (1,2,3,4,5,6,7)
    for x in range(loops):
        f(*l)
        f(*l)
        f(*l)
        f(*l)
        f(*l)
        f(*l)
        f(*l)
        f(*l)
        f(*l)
        f(*l)
        f(*l)
        f(*l)
        f(*l)
        f(*l)
        f(*l)
        f(*l)
        f(*l)
        f(*l)
        f(*l)
        f(*l)
        f(*l)
        f(*l)
        f(*l)
        f(*l)
        f(*l)
        f(*l)
        f(*l)

def dict_splat_test_maker(size):
    global f
    
    if size == 1:
        def f(**a): pass
    elif size == 2:
        def f(a=1,**b): pass
    elif size == 3:
        def f(a=1,b=2,**c): pass
    elif size == 4:
        def f(a=1,b=2,c=3,**d): pass
    elif size == 5:
        def f(a=1,b=2,c=3,d=4,**e): pass
    elif size == 6:
        def f(a=1,b=2,c=3,d=4,e=5,**f): pass
    elif size == 7:
        def f(a=1,b=2,c=3,d=4,e=5,f=6,**g): pass
    
    l = {'a':1,'b':2,'c':3,'d':4, 'e':5, 'f':6, 'g':7 }
    for x in range(loops):
        f(**l)
        f(**l)
        f(**l)
        f(**l)
        f(**l)
        f(**l)
        f(**l)
        f(**l)
        f(**l)
        f(**l)
        f(**l)
        f(**l)
        f(**l)
        f(**l)
        f(**l)
        f(**l)
        f(**l)
        f(**l)
        f(**l)
        f(**l)
        f(**l)
        f(**l)
        f(**l)
        f(**l)
        f(**l)
        f(**l)
        f(**l)

# default arg tests
def test_default_one():
    default_test_maker(1)

def test_default_two():
    default_test_maker(2)

def test_default_three():
    default_test_maker(3)

def test_default_four():
    default_test_maker(4)

def test_default_five():
    default_test_maker(5)

def test_default_six():
    default_test_maker(6)

def test_default_seven():
    default_test_maker(7)

## kwdict tests
def test_kwdict_one():
    kwdict_test_maker(1)

def test_kwdict_two():
    kwdict_test_maker(2)

def test_kwdict_three():
    kwdict_test_maker(3)

def test_kwdict_four():
    kwdict_test_maker(4)

def test_kwdict_five():
    kwdict_test_maker(5)

def test_kwdict_six():
    kwdict_test_maker(6)

def test_kwdict_seven():
    kwdict_test_maker(7)

## splat tests
def test_splat_one():
    splat_test_maker(1)

def test_splat_two():
    splat_test_maker(2)

def test_splat_three():
    splat_test_maker(3)

def test_splat_four():
    splat_test_maker(4)

def test_splat_five():
    splat_test_maker(5)

def test_splat_six():
    splat_test_maker(6)

def test_splat_seven():
    splat_test_maker(7)
    
    
## dict_splat tests
def test_dict_splat_one():
    dict_splat_test_maker(1)

def test_dict_splat_two():
    dict_splat_test_maker(2)

def test_dict_splat_three():
    dict_splat_test_maker(3)

def test_dict_splat_four():
    dict_splat_test_maker(4)

def test_dict_splat_five():
    dict_splat_test_maker(5)

def test_dict_splat_six():
    dict_splat_test_maker(6)

def test_dict_splat_seven():
    dict_splat_test_maker(7)

def test_kwarg_type():
    t = System.Drawing.Point
    for x in range(loops):
        t(X=3, Y=5)
        t(X=3, Y=5)
        t(X=3, Y=5)
        t(X=3, Y=5)
        t(X=3, Y=5)
        t(X=3, Y=5)
        t(X=3, Y=5)
        t(X=3, Y=5)
        t(X=3, Y=5)
        t(X=3, Y=5)
        t(X=3, Y=5)
        t(X=3, Y=5)
        t(X=3, Y=5)
        t(X=3, Y=5)
        t(X=3, Y=5)
        t(X=3, Y=5)
        t(X=3, Y=5)
        t(X=3, Y=5)
        t(X=3, Y=5)
        t(X=3, Y=5)
        t(X=3, Y=5)
        t(X=3, Y=5)
        t(X=3, Y=5)
        t(X=3, Y=5)
        t(X=3, Y=5)
        t(X=3, Y=5)
        t(X=3, Y=5)
"""
def test_gen_method_call():
    t = IronPythonTest.Dispatch().M90
    for x in range(loops):
        t[int](1)
        t[int](1)
        t[int](1)
        t[int](1)
        t[int](1)
        t[int](1)
        t[int](1)
        t[int](1)
        t[int](1)
        t[int](1)
        t[int](1)
        t[int](1)
        t[int](1)
        t[int](1)
        t[int](1)
        t[int](1)
        t[int](1)
        t[int](1)
        t[int](1)
        t[int](1)
        t[int](1)
        t[int](1)
        t[int](1)
        t[int](1)
        t[int](1)
        t[int](1)
        t[int](1)


def run_all_tests():
    times = []
    names = []
    
    tests = [(testname, test) for testname, test in sys.modules[__name__].__dict__.items() if isinstance(testname, str) and testname.startswith('test_')]
    tests.sort(lambda x,y: cmp(x[0], y[0]))
    start = prev = time.clock()
    for testname, test in tests:
        if not isinstance(testname, str): continue
        if not testname.startswith('test_'): continue
        
        test()

        times += time.clock(),
        names += testname,
    
    for thetime, name in zip(times, names):
        print(name, thetime-prev, 'seconds')
        prev = thetime
        
    print('total', prev-start)

if __name__ == "__main__":
    run_all_tests()
