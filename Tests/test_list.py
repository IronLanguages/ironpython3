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

def test_extend_self():
    l=['a','b','c']
    l.extend(l)
    Assert(l==['a','b','c','a','b','c'])

# verify repr and print have the same result for a recursive list
@skip('silverlight')
def test_append_self():
    a = list('abc')
    a.append(a)
    AreEqual(str(a), "['a', 'b', 'c', [...]]")

    ## file
    from iptest.file_util import path_combine
    fn = path_combine(testpath.temporary_dir, "testfile.txt")

    fo = open(fn, "wb")
    a = list('abc')
    a.append(a)
    print(a, end=' ', file=fo)
    fo.close()

    fo = open(fn, "rb")
    Assert(fo.read() == repr(a))
    fo.close()

if is_cli or is_silverlight:
    import clr
    x = [1,2,3]
    y = []
    xenum = iter(x)
    while xenum.MoveNext():
        y.append(xenum.Current)
    AreEqual(x, y)

def test_assign_to_empty():
    # should all succeed
    y = []
    [] = y
    [], t = y, 0
    [[[]]] = [[y]]
    del y

def test_unpack():
    listOfSize2 = [1, 2]

    # Disallow unequal unpacking assignment
    def f1(): [a, b, c] = listOfSize2
    def f2(): del a
    def f3(): [a] = listOfSize2
    
    AssertError(ValueError, f1)
    AssertError(NameError, f2)
    AssertError(ValueError, f3)
    AssertError(NameError, f2)

    [a, [b, c]] = [listOfSize2, listOfSize2]
    AreEqual(a, listOfSize2)
    AreEqual(b, 1)
    AreEqual(c, 2)
    del a, b, c

    [[a, b], c] = (listOfSize2, listOfSize2)
    AreEqual(a, 1)
    AreEqual(b, 2)
    AreEqual(c, listOfSize2)
    del a, b, c

def test_sort():
    # named params passed to sort
    LExpected = ['A', 'b', 'c', 'D']
    L = ['D', 'c', 'b', 'A']
    L.sort(key=lambda x: x.lower())
    Assert(L == LExpected)

    l = [1, 2, 3]
    l2 = l[:]
    l.sort(lambda x, y: x > y)
    AreEqual(l, l2)
    l.sort(lambda x, y: x > y)
    AreEqual(l, l2)

def test_list_in_list():
    aList = [['a']]
    anItem = ['a']
    AreEqual( aList.index(anItem), 0 )
    Assert(anItem in aList)

def test_pop():
    x = [1,2,3,4,5,6,7,8,9,0]
    Assert(x.pop() == 0)
    Assert(x.pop(3) == 4)
    Assert(x.pop(-5) == 5)
    Assert(x.pop(0) == 1)
    Assert(x.pop() == 9)
    Assert(x.pop(2) == 6)
    Assert(x.pop(3) == 8)
    Assert(x.pop(-1) == 7)
    Assert(x.pop(-2) == 2)
    Assert(x.pop() == 3)

def test_add_mul():
    x = [1,2,3]
    x += [4,5,6]
    Assert(x == [1,2,3,4,5,6])
    
    x = [1,2,3]
    AreEqual(x * 2, [1,2,3,1,2,3])
    AreEqual(2 * x, [1,2,3,1,2,3])

    class mylong(long): pass
    AreEqual([1, 2] * mylong(2), [1, 2, 1, 2])
    AreEqual([3, 4].__mul__(mylong(2)), [3, 4, 3, 4])
    AreEqual([5, 6].__rmul__(mylong(2)), [5, 6, 5, 6])
    AreEqual(mylong(2) * [7,8] , [7, 8, 7, 8])
    AssertError(TypeError, lambda: [1,2] * [3,4])
    AssertError(OverflowError, lambda: [1,2] * mylong(203958720984752098475023957209))

def test_reverse():
    x = ["begin",1,2,3,4,5,6,7,8,9,0,"end"]
    del x[6:]
    x.reverse()
    Assert(x == [5, 4, 3, 2, 1, "begin"])

    x = list("iron python")
    x.reverse()
    Assert(x == ['n','o','h','t','y','p',' ','n','o','r','i'])

    # should return listreverseenumerator, not reversed
    Assert(type(reversed([2,3,4])) != reversed)

def test_equal():
    AreEqual([2,3] == '', False)
    AreEqual(list.__eq__([], None), NotImplemented)
    
    class MyEquality(object):
        def __eq__(self, other):
            return 'abc'
    
    class MyOldEquality(object):
        def __eq__(self, other):
            return 'def'
            
    AreEqual([] == MyEquality(), 'abc')
    AreEqual([] == MyOldEquality(), 'def')
    
    AreEqual([2,3] == (2,3), False)

    class MyIterable(object):
        def __iter__(self): return MyIterable()
        def __next__(self):
            yield 'a'
            yield 'b'
            
    AreEqual(['a', 'b'] == MyIterable(), False)

def test_self_init():
    a = [1, 2, 3]
    list.__init__(a, a)
    AreEqual(a, [])

######################################################################
# Verify behavior of index when the list changes...

class clears(object):
    def __eq__(self, other):
        global hitCount
        hitCount = hitCount + 1
        del a[:]
        return False

class appends(object):
    def __eq__(self, other):
        global hitCount
        hitCount = hitCount + 1
        a.append(self)
        return False

a = [clears(), clears(),clears(),clears(),clears()]
hitCount = 0
AssertError(ValueError, a.index, 23)
AreEqual(hitCount, 1)       # should stop after the first equality check

a = [appends(), appends(), appends()]
hitCount = 0
AssertError(ValueError, a.index, 2)
AreEqual(hitCount, 3)       # should have only checked existing items

@runonly('cli')
def test_pass_pythonlist_to_clr():
    ##
    ## test passing pythonlist to clr where IList or ArrayList is requested
    ## also borrow this place to test passing python dict to clr where
    ##      IDictionary or Hashtable is requested
    ##
    
    def contains_all_1s(x):
        '''check the return value are 11111 or similar'''
        if type(x) == tuple:
            x = x[0]
        s = str(x)
        AreEqual(s.count("1"), len(s))
            
    def do_something(thetype, pl, cl, check_func):
        pt = thetype(pl)
        pt.AddRemove()
        
        ct = thetype(cl)
        ct.AddRemove()
            
        check_func()
        
        x = pt.Inspect()
        y = ct.Inspect()
        contains_all_1s(x)
        contains_all_1s(y)
        AreEqual(x, y)
            
        AreEqual(pt.Loop(), ct.Loop())
        check_func()
            
    load_iron_python_test()
    import System
    import IronPythonTest
        
    # test ListWrapperForIList
    pl = list(range(40))
    cl = System.Collections.Generic.List[int]()
    for x in pl: cl.Add(x)
        
    def check_content():
        for x, y in zip(cl, pl): AreEqual(x, y)
            
    do_something(IronPythonTest.UsePythonListAsList, pl, cl, check_content)
        
    # test DictWrapperForIDict
    pl = {"redmond" : 10, "seattle" : 20}
    cl = System.Collections.Generic.Dictionary[str, int]()
    for x, y in pl.items(): cl.Add(x, y)
    
    pll = list(pl.items())
    cll = list(cl)
    pll.sort(lambda x, y: cmp(x[0], y[0]))
    cll.sort(lambda x, y: cmp(x.Key, y.Key))

    def check_content():
        for x, y in zip(cll, pll):
            AreEqual(x.Key, y[0])
            AreEqual(x.Value, y[1])
      
    do_something(IronPythonTest.UsePythonDictAsDictionary, pl, cl, check_content)

def test_inplace_addition():
    x = [2,3,4]
    x += x
    AreEqual(x, [2,3,4,2,3,4])
    
    test_cases = [ ([],     [],     []),
                   ([1],    [],     [1]),
                   ([],     [1],    [1]),
                   ([1],    [1],    [1, 1]),
                   ([1],    [2],    [1, 2]),
                   ([2],    [1],    [2, 1]),
                   ([1, 2], [],     [1, 2]),
                   ([],     [1, 2], [1, 2]),
                   ([1, 2], [3],    [1, 2, 3]),
                   ([3],    [1, 2], [3, 1, 2]),
                   ([1, 2], [3, 4], [1, 2, 3, 4]),
                   ([3, 4], [1, 2], [3, 4, 1, 2]),
                   ([None], [],     [None]),
                   ([None], [2],    [None, 2]),
                   ([""],   [],     [""]),
                   ]
                   
    for left_operand, right_operand, result in test_cases:
    
        #(No access to copy.deepcopy in IP)
        #  Create new list to verify no side effects to the RHS list
        orig_right = [x for x in right_operand]
            
        left_operand += right_operand
        
        AreEqual(left_operand, result)
        
        #Side effects...
        AreEqual(orig_right, right_operand)
        
    #interesting cases
    x = [None]
    x += range(3)
    AreEqual(x, [None, 0, 1, 2])
    
    x = [None]
    x += (0, 1, 2)
    AreEqual(x, [None, 0, 1, 2])
    
    x = [None]
    x += "012"
    AreEqual(x, [None, "0", "1", "2"])
    
    x = [None]
    x += Exception()
    AreEqual(x, [None])
    
    #negative cases
    neg_cases = [   ([],    None),
                    ([],    1),
                    ([],    1),
                    ([],    3.14),
                    ([],    object),
                    ([],    object()),
                 ]
    for left_operand, right_operand in neg_cases:
        try:
            left_operand += right_operand
            AssertUnreachable()
        except TypeError as e:
            pass
            
def test_indexing():
    l = [2,3,4]
    def set(x, i, v): x[i] = v
    AssertError(TypeError, lambda : l[2.0])
    AssertError(TypeError, lambda : set(l, 2.0, 1))
    
    class mylist(list):
        def __getitem__(self, index):
            return list.__getitem__(self, int(index))
        def __setitem__(self, index, value):
            return list.__setitem__(self, int(index), value)

    l = mylist(l)
    AreEqual(l[2.0], 4)
    l[2.0] = 1
    AreEqual(l[2], 1)


def test_getslice():
    """overriding __len__ doesn't get called when doing __getslice__"""
    class l(list):
        def __len__(self):
            raise Exception()

    x = l()
    AreEqual(x.__getslice__(-1, -200), [])
    
    class mylist(list):
        def __getslice__(self, i, j):
            return i, j

    class mylong(long): pass
    class myint(int): pass
    
    # all indexes to __getslice__ should be ints
    for listType in list, mylist:
        for input in [0, 1, False, True, myint(0), myint(1), mylong(0), mylong(1), -1, myint(-1), mylong(-1)]:
            for x in listType(list(range(5)))[input:input]:
                AreEqual(type(x), int)
    
    
def test_repr():
    class mylist(list):
        def __repr__(self): return 'abc'

    AreEqual(repr(mylist()), 'abc')

def test_index_multiply():
    for data in ([1,2], (1,2), 'ab'):
    
        class M:
            def __rmul__(self, other):
                return 1
    
        class Index(object):
            def __index__(self): return 2
            
        class OldIndex:
            def __index__(self): return 2
        
        AreEqual(data * M(), 1)
        AssertError(TypeError, lambda : data.__mul__(M()))
        
        AreEqual(data * Index(), data * 2)
        AreEqual(data * OldIndex(), data * 2)
        AreEqual(data.__mul__(Index()), data * 2)
        AreEqual(data.__mul__(OldIndex()), data * 2)
        
        AssertErrorWithMessage(TypeError, "'NoneType' object cannot be interpreted as an index", lambda : data.__mul__(None))
        AssertError(TypeError, lambda : data * None)
        AssertError(TypeError, lambda : None * data)

def test_sequence_assign():
    tokens = [(chr(ord('a') + val), val) for val in range(0,10)]
    (first,pos),tokens = tokens[0], tokens[1:]
    
    AreEqual(first, 'a')
    AreEqual(pos, 0)
    AreEqual(tokens, [('b', 1), ('c', 2), ('d', 3), ('e', 4), ('f', 5), ('g', 6), ('h', 7), ('i', 8), ('j', 9)])

def test_inheritance():
    listIter = type(iter([2,3,4]))
    reverseListIter = type(reversed([2,3,4]))

    for base in (listIter, reverseListIter):
        def subclass():
            class x(base): pass
            
        AssertError(TypeError, subclass)


def test_backwards_slicing_no_step():    
    class mylist(object):
        def __getitem__(self, index):
            return 'stuff'[index]
    
    a = list('stuff')
    for val in (a, 'stuff', tuple('stuff'), mylist()):
        a[1:0] = val
        AreEqual(a, list("stuff"[:1] + "stuff" + "stuff"[1:]))
        a = list('stuff')

    for val in (a, 'stuff', tuple('stuff'), mylist()):
        a[1:0:1] = a
        AreEqual(a, list("stuff"[:1] + "stuff" + "stuff"[1:]))
        a = list('stuff')

def test_cp20125():
    class Temp(list):
        def __init__(self, value):
          self.value = value
        def __mul__(self, other):
            return self.value * other

    t1 = Temp(3.0)
    AreEqual(t1 * 3.0, 9.0)


#--MAIN------------------------------------------------------------------------
run_test(__name__)
