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

##
## Test built-in types: set/frozenset
##

from iptest.assert_util import *
from iptest.type_util import myset, myfrozenset

#--GLOBALS---------------------------------------------------------------------
s1 = [2, 4, 5]
s2 = [4, 7, 9, 10]
s3 = [2, 4, 5, 6]

#--TEST CASES------------------------------------------------------------------
def test_equality():
    ne_list = [1]
    
    for z in [s1, s2, s3, []]:
        for x in (set, frozenset, myset, myfrozenset):
            for y in (set, frozenset, myset, myfrozenset):
                AreEqual(x(z), y(z))
                AreEqual(list(x(z)), list(y(z)))
                AreEqual([x(z)], [y(z)])
                AreEqual(tuple(x(z)), tuple(y(z)))
                AreEqual((x(z)), (y(z)))
            Assert(x(z) != x(ne_list))
            Assert(list(x(z)) != list(x(ne_list)))
            Assert([x(z)] != [x(ne_list)])
            Assert(tuple(x(z)) != tuple(x(ne_list)))
            Assert((x(z)) != (x(ne_list)))

def test_sanity():
    for x in (set, frozenset, myset, myfrozenset):
        # creating as default
        y = x()
        AreEqual(len(y), 0)
        # creating with 2 args
        AssertError(TypeError, x, list(range(3)), 3)
        #!!!AssertError(TypeError, x.__new__, str)
        #!!!AssertError(TypeError, x.__new__, str, 'abc')

        xs1, xs2, xs3 = x(s1), x(s2), x(s3)
        
        # membership
        AreEqual(4 in xs1, True)
        AreEqual(6 in xs1, False)
        
        # relation with another of the same type
        AreEqual(xs1.issubset(xs2), False)
        AreEqual(xs1.issubset(xs3), True)
        AreEqual(xs3.issuperset(xs1), True)
        AreEqual(xs3.issuperset(xs2), False)

        # equivalent op
        AreEqual(xs1 <= xs2, False)
        AreEqual(xs1 <= xs3, True)
        AreEqual(xs3 >= xs1, True)
        AreEqual(xs3 >= xs2, False)
        
        AreEqual(xs1.union(xs2), x([2, 4, 5, 7, 9, 10]))
        AreEqual(xs1.intersection(xs2), x([4]))
        AreEqual(xs1.difference(xs2), x([2, 5]))
        AreEqual(xs2.difference(xs1), x([7, 9, 10]))
        AreEqual(xs2.symmetric_difference(xs1), x([2, 5, 7, 9, 10]))
        AreEqual(xs3.symmetric_difference(xs1), x([6]))

        # equivalent op
        AreEqual(xs1 | xs2, x([2, 4, 5, 7, 9, 10]))
        AreEqual(xs1 & xs2, x([4]))
        AreEqual(xs1 - xs2, x([2, 5]))
        AreEqual(xs2 - xs1, x([7, 9, 10]))
        AreEqual(xs2 ^ xs1, x([2, 5, 7, 9, 10]))
        AreEqual(xs3 ^ xs1, x([6]))

        # repeat with list
        AreEqual(xs1.issubset(s2), False)
        AreEqual(xs1.issubset(s3), True)
        AreEqual(xs3.issuperset(s1), True)
        AreEqual(xs3.issuperset(s2), False)
        
        AreEqual(xs1.union(s2), x([2, 4, 5, 7, 9, 10]))
        AreEqual(xs1.intersection(s2), x([4]))
        AreEqual(xs1.difference(s2), x([2, 5]))
        AreEqual(xs2.difference(s1), x([7, 9, 10]))
        AreEqual(xs2.symmetric_difference(s1), x([2, 5, 7, 9, 10]))
        AreEqual(xs3.symmetric_difference(s1), x([6]))

def test_ops():
    s1, s2, s3 = 'abcd', 'be', 'bdefgh'
    for t1 in (set, frozenset, myset, myfrozenset):
        for t2 in (set, frozenset, myset, myfrozenset):
            # set/frozenset creation
            AreEqual(t1(t2(s1)), t1(s1))
            
            # ops
            for (op, exp1, exp2) in [('&', 'b', 'bd'), ('|', 'abcde', 'abcdefgh'), ('-', 'acd', 'ac'), ('^', 'acde', 'acefgh')]:
                
                x1 = t1(s1)
                exec("x1   %s= t2(s2)" % op)
                AreEqual(x1, t1(exp1))

                x1 = t1(s1)
                exec("x1   %s= t2(s3)" % op)
                AreEqual(x1, t1(exp2))
                
                x1 = t1(s1)
                exec("y = x1 %s t2(s2)" % op)
                AreEqual(y, t1(exp1))

                x1 = t1(s1)
                exec("y = x1 %s t2(s3)" % op)
                AreEqual(y, t1(exp2))

def test_none():
    x, y = set([None, 'd']), set(['a', 'b', 'c', None])
    AreEqual(x | y, set([None, 'a', 'c', 'b', 'd']))
    AreEqual(y | x, set([None, 'a', 'c', 'b', 'd']))
    AreEqual(x & y, set([None]))
    AreEqual(y & x, set([None]))
    AreEqual(x - y, set('d'))
    AreEqual(y - x, set('abc'))
    
    a = set()
    a.add(None)
    AreEqual(repr(a), 'set([None])')


def test_cmp():
    """Verify we can compare sets that aren't the same type"""
    
    a = frozenset([1,2])
    b = set([1,2])
    
    abig = frozenset([1,2,3])
    bbig = set([1,2,3])
    
    AreEqual(cmp(a,b), 0)
    AreEqual(cmp(a,bbig), -1)
    AreEqual(cmp(abig,b), 1)
    
    class sset(set): pass
    
    class fset(frozenset): pass
    
    a = fset([1,2])
    b = sset([1,2])
    
    abig = fset([1,2,3])
    bbig = sset([1,2,3])
    
    AreEqual(cmp(a,b), 0)
    AreEqual(cmp(a,bbig), -1)
    AreEqual(cmp(abig,b), 1)

def test_deque():
    if is_cli or is_silverlight:
        from _collections import deque
    else:
        from collections import deque
    x = deque([2,3,4,5,6])
    x.remove(2)
    AreEqual(x, deque([3,4,5,6]))
    x.remove(6)
    AreEqual(x, deque([3,4,5]))
    x.remove(4)
    AreEqual(x, deque([3,5]))
    
    # get a deque w/ head/tail backwards...
    x = deque([1,2,3,4,5,6,7,8])
    x.popleft()
    x.popleft()
    x.popleft()
    x.popleft()
    x.append(1)
    x.append(2)
    x.append(3)
    x.append(4)
    AreEqual(x, deque([5,6,7,8, 1, 2, 3, 4]))
    x.remove(5)
    AreEqual(x, deque([6,7,8, 1, 2, 3, 4]))
    x.remove(4)
    AreEqual(x, deque([6,7,8, 1, 2, 3]))
    x.remove(8)
    AreEqual(x, deque([6,7,1, 2, 3]))
    x.remove(2)
    AreEqual(x, deque([6,7,1, 3]))

    class BadCmp:
        def __eq__(self, other):
            raise RuntimeError
    
    d = deque([1,2, BadCmp()])
    AssertError(RuntimeError, d.remove, 3)

    x = deque()
    class y(object):
        def __eq__(self, other):
            return True
    
    x.append(y())
    AreEqual(y() in x, True)
    
    x = deque({}, None)
    AreEqual(x, deque([]))
    
    AssertErrorWithPartialMessage(TypeError, "takes at most 2 arguments (3 given)", deque, 'abc', 2, 2)

def test_singleton():
    """Verify that an empty frozenset is a singleton"""
    AreEqual(frozenset([]) is frozenset([]), True)
    x = frozenset([1, 2, 3])
    AreEqual(x is frozenset(x), True)

@skip("silverlight") # no random
def test_iteration_no_mutation_bad_hash():
    """create a set w/ objects with a bad hash and enumerate through it.  No exceptions should be thrown"""
       
    import random
    class c(object):
        def __hash__(self):
                return int(random.random()*200)
     
    l = [c() for i in range(1000)]
    b = set(l)
    for x in b:
        pass

def test_null_elements():
    class SetSubclass(set):
        pass
    class FrozenSetSubclass(frozenset):
        pass
    
    for thetype in [set, frozenset, SetSubclass, FrozenSetSubclass]:
        s = thetype([None])
        
        AreEqual(s, set([None]))
        AreEqual(s.copy(), set([None]))
        
        AreEqual(s.isdisjoint(set()), True)
        AreEqual(s.isdisjoint(set([None])), False)
        AreEqual(s.isdisjoint(set([42])), True)
        AreEqual(s.isdisjoint(set([None, 42])), False)
        AreEqual(s.issubset(set()), False)
        AreEqual(s.issubset(set([42])), False)
        AreEqual(s.issubset(set([None])), True)
        AreEqual(s.issubset(set([None, 42])), True)
        AreEqual(s.issuperset(set()), True)
        AreEqual(s.issuperset(set([42])), False)
        AreEqual(s.issuperset(set([None])), True)
        AreEqual(s.issuperset(set([None, 42])), False)
        
        AreEqual(s.union(), set([None]))
        AreEqual(s.union(set([None])), set([None]))
        AreEqual(s.union(set()), set([None]))
        AreEqual(s.intersection(), set([None]))
        AreEqual(s.intersection(set([None])), set([None]))
        AreEqual(s.intersection(set()), set())
        AreEqual(s.difference(), set([None]))
        AreEqual(s.difference(set([None])), set())
        AreEqual(s.difference(set()), set([None]))
        AreEqual(s.symmetric_difference(set([None])), set())
        AreEqual(s.symmetric_difference(set()), set([None]))
        
        # Test mutating operations
        if 'add' in dir(s):
            s.remove(None)
            AreEqual(s, set())
            s.add(None)
            AreEqual(s, set([None]))
            s.discard(None)
            AreEqual(s, set())
            s.discard(None) # make sure we don't raise exception
            AssertError(KeyError, s.remove, None)
            s.add(None)
            s.clear()
            AreEqual(s, set())
            s.add(None)
            AreEqual(s.pop(), None)
            AreEqual(s, set())
            
            s.update(set([None]))
            AreEqual(s, set([None]))
            s.intersection_update(set([42]))
            AreEqual(s, set())
            s.update(set([None, 42]))
            s.difference_update(set([None]))
            AreEqual(s, set([42]))
            s.symmetric_difference_update(set([None, 42]))
            AreEqual(s, set([None]))

def test_frozenness():
    s = set([1,2,3])
    f = frozenset(s)
    s.add(4)
    AreEqual(4 in f, False)

def test_set_comp():
    AreEqual({locals()['x'] for x in (2,3,4)}, set([2, 3, 4]))
    
    x = 100
    {x for x in (2,3,4)}
    AreEqual(x, 100)
    
    class C:
        {x for x in (2,3,4)}
    
    AreEqual(hasattr(C, 'x'), False)
    
    class C:
        abc = {locals()['x'] for x in (2,3,4)}
    
    AreEqual(C.abc, set([2,3,4]))

    d = {}
    exec(compile("abc = {locals()['x'] for x in (2,3,4)}", 'exec', 'exec'), d, d)
    AreEqual(d['abc'], set([2,3,4]))
    
    d = {'y':42}
    exec(compile("abc = {y for x in (2,3,4)}", 'exec', 'exec'), d, d)
    AreEqual(d['abc'], set([42]))

    d = {'y':42, 't':(2,3,42)}
    exec(compile("abc = {y for x in t if x == y}", 'exec', 'exec'), d, d)
    AreEqual(d['abc'], set([42]))

    t = (2,3,4)
    v = 2
    abc = {v for x in t}
    AreEqual(abc, set([2]))

    abc = {x for x in t if x == v}
    AreEqual(abc, set([2]))
    
    def f():
        abc = {x for x in t if x == v}
        AreEqual(abc, set([2]))
        
    f()
    
    def f():
        abc = {v for x in t}
        AreEqual(abc, set([2]))
        
        
    class C:
        abc = {v for x in t}
        AreEqual(abc, set([2]))
        
    class C:
        abc = {x for x in t if x == v}
        AreEqual(abc, set([2]))

#--MAIN------------------------------------------------------------------------    
run_test(__name__)
