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

from iptest.assert_util import *
import sys

def test_simple():
    # Simple conditional true case
    Assert(100 if 1 else 200 == 100)

    # Simple conditional false case
    Assert(0 if 0 else 200 == 200)

def test_multiple_assignment():
    # Conditional multiple assignment
    x, y, z, w, u  = 1 if 0 else 2, 2 if 1 else 3, 3 if 10 else 4, 1 & 0 if 0 and 3 or 4 else 100, 1 and 0 if 0 and 3 or 4 & 0 else 100
    AreEqual((x,y,z,w,u), (2,2,3,0,100))

def test_in_expressions():
    # combination of operators and conditional
    Assert(100 + 1 & 3 if 1 else 2 == 1)
    Assert(100 + (1 & 3 if 1 else 2) == 101)

def test_if_else():
    # conditional in if-else
    x, y, z = 0,1,2
    if x if y else z:
        p = 100
    else:
        p = 200
    Assert(p == 200)

def test_nested_conditionals():
    # nested conditionals
    if 0 if (0 if 100 else 1 ) else 10:
        x = 300
    else:
        x = 400
    Assert(x == 300)

def test_conditionals_with_test_list_1():
    # conditionals with test-list #test1
    x,y,z = 1,2,3
    if 20 if (x,y,z == 1,2,3) else 0:
        x = 300
    else:
        x = 400
    Assert(x == 300)


def test_conditionals_with_test_list_2():
    # conditionals with test-list #test2
    list = [[1 if 1 else 0,0 if 0 else 2,3],[4,5 if 1 and 1 else 0,8 if 0 and 1 else 6 & 7]]
    if 20 if (list == [[1,2,3],[4,5,7]]) else 0 if 1 else 200 :
        x = 300
    else:
        x = 400
    Assert(x == 400)

def test_generator_expressions():
    #test for gen_for
    Assert(sum(x*x for x in range(10) if not x%2 if not x%3) == sum([x*x for x in range(10) if not x%6]))
    
    #test for gen_for gen_if combined
    Assert(sum(x*x for x in range(10) for x in range(5) if not x %2) == 200)

def test_list_for():
    #test for list_for
    list = [10,20,30,40,50,60,70,80,90,100,110,120,130]
    mysum = 0
    for i in (0,1,2,3,4,5,6,7,8,9,10,11,12):
        mysum += list[i]
    Assert(mysum == 910)

    #test for list_for list_if combined
    list = [10,20,30,40,50,60,70,80,90,100,110,120,130]
    Assert(sum(list[i] if not i%2 else 0 for i in (0,1,2,3,4,5,6,7,8,9,10,11,12) if not i %3 if not 0) == 210)

def test_errors():
    #test for null list
    AssertError(SyntaxError, compile, "mysum = 0;for i in 10:pass", "Error", "exec")
    # test for lambda function
    AssertError(SyntaxError, compile, "[f for f in 1, lambda x: x if x >= 0 else -1]", "", "exec")

def test_conditional_in_lambda():
    try:
        list = [f for f in (1, lambda x: x if x >= 0 else -1)]
        list = [f for f in (1, lambda x: (x if x >= 0 else -1))]
        list = [f for f in (1, (lambda x: x if x >= 0 else -1))]
    except e:
        Assert(False, e.message)

def test_conditional_return_types():
    '''
    11491
    '''
    class OldK: pass
    
    class NewK(object): pass
    
    for x in [
                -2, -1, 0, 1, 2, 2**16,
                -2, -1, 0, 1, 2, 2**32,
                3.14,
                2j,
                "", "abc",
                {}, {'a':'b'}, {'a':'b', 'c':'d'},
                [], [1], [1, 2],
                range(0), range(1), range(2),
                OldK, NewK, OldK(), NewK(),
                None, str, object,
                ]:
        temp = 0 if 0 else x
        AreEqual(temp, x)

def test_conversions():
    AreEqual(1 if False else "Hello", "Hello")
    AreEqual("Hello" if False else 1, 1)
    AreEqual(1 if True else "Hello", 1)
    AreEqual("Hello" if False else "Goodbye", "Goodbye")

    if (1 if True else False):
        pass
    else:
        Fail("Expression incorrectly evaluated")

def test_cp13299():
    true_conditions = [ 1, 1, -1, -1, True, 1.1, -1.1, "abc", int, 0.1, -0.1]
    false_conditions = [ 0, 0, None, 0.0, -0, -0.0, False, (), [], ""]
    
    for condition in true_conditions:
        x = condition if condition else False
        AreEqual(x, condition)

    for condition in false_conditions:
        x = True if condition else condition
        AreEqual(x, condition)
            
    AreEqual(3.14 if True else 1, 3.14)
    AreEqual(3.14 if True else 1, 3.14)
    AreEqual(3.14 if True else -1, 3.14)
    AreEqual(3.14 if True else True, 3.14)
    AreEqual(3.14 if True else 1.1, 3.14)
    AreEqual(3.14 if True else -1.1, 3.14)
    AreEqual(3.14 if True else "abc", 3.14)
    AreEqual(3.14 if True else int, 3.14)
    AreEqual(3.14 if True else 0, 3.14)
    AreEqual(3.14 if True else 0, 3.14)
    AreEqual(3.14 if True else None, 3.14)
    AreEqual(3.14 if True else False, 3.14)
    AreEqual(3.14 if True else (), 3.14)
    AreEqual(3.14 if True else [[1]], 3.14)
    AreEqual(3.14 if True else "", 3.14)
    
    AreEqual(1 if False else 3.14, 3.14)
    AreEqual(1 if False else 3.14, 3.14)
    AreEqual(-1 if False else 3.14, 3.14)
    AreEqual(True if False else 3.14, 3.14)
    AreEqual(1.1 if False else 3.14, 3.14)
    AreEqual(-1.1 if False else 3.14, 3.14)
    AreEqual("abc" if False else 3.14, 3.14)
    AreEqual(int if False else 3.14, 3.14)
    AreEqual(0 if False else 3.14, 3.14)
    AreEqual(0 if False else 3.14, 3.14)
    AreEqual(None if False else 3.14, 3.14)
    AreEqual(False if False else 3.14, 3.14)
    AreEqual(() if False else 3.14, 3.14)
    AreEqual([[1]] if False else 3.14, 3.14)
    AreEqual("" if False else 3.14, 3.14)
    
def test_large_if():    
    def f(value):
        if value:
            return 42
        elif value:
            raise Exception()
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif value:
            pass
        elif True:
            return 23

    for i in range(10000):
        AreEqual(f(True), 42)

    for i in range(10000):
        AreEqual(f(False), 23)

run_test(__name__)
