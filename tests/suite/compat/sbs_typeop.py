# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

from common import *
import sys

def add(a, b): return a + b
def sub(a, b): return a - b
def mul(a, b): return a * b
def div(a, b): return a / b
def inn(a, b): return a in b
def iss(a, b): return a is b

def eq(a, b): return a == b
def ne(a, b): return a != b
def lt(a, b): return a < b
def le(a, b): return a <= b
def gt(a, b): return a > b
def ge(a, b): return a >= b

funclist1 = [ add, sub, mul, div, inn, iss, eq, ne ]
funclist2 = [ add, sub, mul, div, inn, iss, eq, ne, lt, le, gt, ge ]

def case_repr(x, y, func):
    return "%s(%s) %s %s(%s)" % (x, type(x), func.__name__, y, type(y))

class mybase(object):
    def mycase(self, side1, side2, funclist):
        symmetric = side1 == side2
            
class number_types(mybase):
    def test_ops(self):
        side1 = [ None, 0, 1, 2, 10, 1.5, 30, 1+2j ]
        side2 = [
                    '', '1', '23', '456', 
                    (), (1,), (2, 3), (4, 5, 6),
                    [], [1], [2, 3], [4, 5, 6],
                    set(), set([1]), set([2,3]), set([4, 5, 6]),
                    frozenset(), frozenset([1]), frozenset([2, 3]), frozenset([4, 5, 6]),
                    {}, {1:1}, {2:2, 3:3}, {4:4, 5:5, 6:6},
                ]
        for x in side1:
            for y in side2:
                for func in funclist1:
                    try:
                        printwith("case", case_repr(x, y, func));
                        printwith("same", func(x, y))
                    except:
                        printwith("same", sys.exc_info()[0])
                    try:
                        printwith("case", case_repr(y, x, func));
                        printwith("same", func(y, x))
                    except:
                        printwith("same", sys.exc_info()[0])
                        
    def test_str(self):
        side1 = [ None, '', 'b', 'abc' ]
        for x in side1:
            for y in side1:
                for func in funclist2:
                    try:
                        printwith("case", case_repr(x, y, func));
                        printwith("same", func(x, y))
                    except:
                        printwith("same", sys.exc_info()[0])
    
    def test_set(self):
        side1 = [ None, set(), set('a'), set('abc'), set('bde')]
        for x in side1:
            for y in side1:
                for func in funclist2:
                    try:
                        printwith("case", case_repr(x, y, func));
                        res = func(x, y)
                        printwith("case", res)
                        
                        if isinstance(res, set):
                            for c in 'abcde':
                                printwith("same", c in res)
                        else:
                            printwith("same", res)                        
                    except:
                        printwith("same", sys.exc_info()[0])
                    
runtests(number_types)
