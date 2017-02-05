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

import sys
from iptest.assert_util import *

if is_cli or is_silverlight:
    import System
    import System.Collections
    load_iron_python_test()
    from IronPythonTest import *

@skip('win32')
def test_string():
    x = System.Array.CreateInstance(System.String, 2)
    x[0]="Hello"
    x[1]="Python"
    Assert(x[0] == "Hello")
    Assert(x[1] == "Python")

@skip('win32')
def test_hashtable():
    hashtables = [System.Collections.Generic.Dictionary[object, object]()]
    if not is_silverlight: # Hashtable isn't available in Silverlight
        hashtables.append(System.Collections.Hashtable())
    
    for x in hashtables:
        x["Hi"] = "Hello"
        x[1] = "Python"
        x[10,] = "Tuple Int"
        x["String",] = "Tuple String"
        x[2.4,] = "Tuple Double"
        
        Assert(x["Hi"] == "Hello")
        Assert(x[1] == "Python")
        Assert(x[(10,)] == "Tuple Int")
        Assert(x[("String",)] == "Tuple String")
        Assert(x[(2.4,)] == "Tuple Double")
        
        success=False
        try:
            x[1,2] = 10
        except TypeError as e:
            success=True
        Assert(success)
        
        x[(1,2)] = "Tuple key in hashtable"
        Assert(x[1,2,] == "Tuple key in hashtable")

@skip('win32')
def test_multidim_array():
    md = System.Array.CreateInstance(System.Int32, 2, 2, 2)
    
    for i in range(2):
        for j in range(2):
            for k in range(2):
                md[i,j,k] = i+j+k
    
    for i in range(2):
        for j in range(2):
            for k in range(2):
                Assert(md[i,j,k] == i+j+k)

@skip('win32')
def test_array():
    # verify that slicing an array returns an array of the proper type
    from System import Array
    data = Array[int]( (2,3,4,5,6) )
    
    AreEqual(type(data[:0]), Array[int])
    AreEqual(type(data[0:3:2]), Array[int])

def test_dict():
    d = dict()
    d[1,2,3,4,5] = 12345
    Assert(d[1,2,3,4,5] == d[(1,2,3,4,5)])
    Assert(d[1,2,3,4,5] == 12345)
    Assert(d[(1,2,3,4,5)] == 12345)
    
    d = {None:23}
    del d[None]
    AreEqual(d, {})

@skip('win32')
def test_custom_indexable():
    i = Indexable()
    
    i[10] = "Hello Integer"
    i["String"] = "Hello String"
    i[2.4] = "Hello Double"
    
    Assert(i[10] == "Hello Integer")
    Assert(i["String"] == "Hello String")
    Assert(i[2.4] == "Hello Double")
    
    indexes = (10, "String", 2.4)
    for a in indexes:
        for b in indexes:
            complicated = "Complicated " + str(a) + " " + str(b)
            i[a,b] = complicated
            Assert(i[a,b] == complicated)

@skip('win32')
def test_property_access():
    x = PropertyAccessClass()
    for i in range(3):
        Assert(x[i] == i)
        for j in range(3):
            x[i, j] = i + j
            Assert(x[i, j] == i + j)
            for k in range(3):
                x[i, j, k] = i + j + k
                Assert(x[i, j, k] == i + j + k)

@skip('win32')
def test_multiple_indexes():
    x = MultipleIndexes()
    
    def get_value(*i):
        value = ""
        append = False
        for v in i:
            if append:
                value = value + " : "
            value = value + str(v)
            append = True
        return value
    
    def get_tuple_value(*i):
        return get_value("Indexing as tuple", *i)
    
    def get_none(*i):
        return None
    
    def verify_values(mi, gv, gtv):
        for i in i_idx:
            Assert(x[i] == gv(i))
            Assert(x[i,] == gtv(i))
            for j in j_idx:
                Assert(x[i,j] == gv(i,j))
                Assert(x[i,j,] == gtv(i,j))
                for k in k_idx:
                    Assert(x[i,j,k] == gv(i,j,k))
                    Assert(x[i,j,k,] == gtv(i,j,k))
                    for l in l_idx:
                        Assert(x[i,j,k,l] == gv(i,j,k,l))
                        Assert(x[i,j,k,l,] == gtv(i,j,k,l))
                        for m in m_idx:
                            Assert(x[i,j,k,l,m] == gv(i,j,k,l,m))
                            Assert(x[i,j,k,l,m,] == gtv(i,j,k,l,m))
    
    i_idx = ("Hi", 2.5, 34)
    j_idx = (0, "*", "@")
    k_idx = list(range(3))
    l_idx = ("Sun", "Moon", "Star")
    m_idx = ((9,8,7), (6,5,4,3,2), (4,))
    
    for i in i_idx:
        x[i] = get_value(i)
        for j in j_idx:
            x[i,j] = get_value(i,j)
            for k in k_idx:
                x[i,j,k] = get_value(i,j,k)
                for l in l_idx:
                    x[i,j,k,l] = get_value(i,j,k,l)
                    for m in m_idx:
                        x[i,j,k,l,m] = get_value(i,j,k,l,m)
    
    verify_values(x, get_value, get_none)
    
    for i in i_idx:
        x[i,] = get_tuple_value(i)
        for j in j_idx:
            x[i,j,] = get_tuple_value(i,j)
            for k in k_idx:
                x[i,j,k,] = get_tuple_value(i,j,k)
                for l in l_idx:
                    x[i,j,k,l,] = get_tuple_value(i,j,k,l)
                    for m in m_idx:
                        x[i,j,k,l,m,] = get_tuple_value(i,j,k,l,m)
    
    verify_values(x, get_value, get_tuple_value)

@skip('win32')
def test_indexable_list():
    a = IndexableList()
    for i in range(5):
        result = a.Add(i)
    
    for i in range(5):
        AreEqual(a[str(i)], i)

@skip('win32')
def test_generic_function():
    # all should succeed at indexing
    x = GenMeth.StaticMeth[int, int]
    x = GenMeth.StaticMeth[int]
    x = GenMeth.StaticMeth[(int, int)]
    x = GenMeth.StaticMeth[(int,)]

def test_getorsetitem_override():
    class old_base: pass

    for base in [object, list, dict, int, str, tuple, float, int, complex, old_base]:
        class foo(base):
            def __getitem__(self, index):
                return index
            def __setitem__(self, index, value):
                self.res = (index, value)
    
        a = foo()
        AreEqual(a[1], 1)
        AreEqual(a[1,2], (1,2))
        AreEqual(a[1,2,3], (1,2,3))
        AreEqual(a[(1, 2)], (1, 2))
        AreEqual(a[(5,)], (5,))
        AreEqual(a[6,], (6,))

        a[1] = 23
        AreEqual(a.res, (1,23))
        a[1,2] = 23
        AreEqual(a.res, ((1,2),23))
        a[1,2,3] = 23
        AreEqual(a.res, ((1,2,3),23))

        a[(1, 2)] = "B"; AreEqual(a.res, ((1, 2), "B"))
        a[(5,)] = "D"; AreEqual(a.res, ((5,), "D"))
        a[6,] = "E"; AreEqual(a.res, ((6,), "E"))

def test_getorsetitem_super():
    tests = [  # base type, constructor arg, result of index 0
       (list,(1,2,3,4,5), 1),
        (dict,{0:2, 3:4, 5:6, 7:8}, 2),
        (str,'abcde', 'a'),
        (tuple, (1,2,3,4,5), 1),]
        
    for testInfo in tests:
        base = testInfo[0]
        arg  = testInfo[1]
        zero = testInfo[2]
                
        class foo(base):
            def __getitem__(self, index):
                if isinstance(index, tuple):
                    return base.__getitem__(self, index[0])
                return base.__getitem__(self, index)
            def __setitem__(self, index, value):
                if isinstance(index, tuple):
                    base.__setitem__(self, index[0], value)
                else:
                    base.__setitem__(self, index, value)

        a = foo(arg)
        AreEqual(a[0], zero)
        a = foo(arg)
        AreEqual(a[0,1], zero)
        a = foo(arg)
        AreEqual(a[0,1,2], zero)
        a = foo(arg)
        AreEqual(a[(0,)], zero)
        a = foo(arg)
        AreEqual(a[(0,1)], zero)
        a = foo(arg)
        AreEqual(a[(0,1,2)], zero)
        
        if hasattr(base, '__setitem__'):
            a[0] = 'x'
            AreEqual(a[0], 'x')
            a[0,1] = 'y'
            AreEqual(a[0,1], 'y')
            a[0,1,2] = 'z'
            AreEqual(a[0,1,2], 'z')
            a[(0,)] = 'x'
            AreEqual(a[(0,)], 'x')
            a[(0,1)] = 'y'
            AreEqual(a[(0,1)], 'y')
            a[(0,1,2)] = 'z'
            AreEqual(a[(0,1,2)], 'z')

        
def test_getorsetitem_slice():
    tests = [  # base type, constructor arg, result of index 0
       (list,(1,2,3,4,5), 1, lambda x: [x]),
        (str,'abcde', 'a', lambda x: x),
        (tuple, (1,2,3,4,5), 1, lambda x: (x,)),]
    
    for testInfo in tests:
        base = testInfo[0]
        arg  = testInfo[1]
        zero = testInfo[2]
        resL = testInfo[3]
                
        class foo(base):
            def __getitem__(self, index):
                if isinstance(index, tuple):
                    return base.__getitem__(self, index[0])
                return base.__getitem__(self, index)
            def __setitem__(self, index, value):
                if isinstance(index, tuple):
                    base.__setitem__(self, index[0], value)
                else:
                    base.__setitem__(self, index, value)

        a = foo(arg)
        AreEqual(a[0:1], resL(zero))
        a = foo(arg)
        AreEqual(a[0:1, 1:2], resL(zero))
        a = foo(arg)
        AreEqual(a[0:1, 1:2, 2:3], resL(zero))
        a = foo(arg)
        AreEqual(a[(slice(0,1),)], resL(zero))
        a = foo(arg)
        AreEqual(a[(slice(0,1),slice(1,2))], resL(zero))
        a = foo(arg)
        AreEqual(a[(slice(0,1),slice(1,2),slice(2,3))], resL(zero))
        
        if hasattr(base, '__setitem__'):
            a[0:1] = 'x'
            AreEqual(a[0:1], ['x'])
            a[0:1,1:2] = 'y'
            AreEqual(a[0:1,1:2], ['y'])
            a[0:1,1:2,2:3] = 'z'
            AreEqual(a[0:1,1:2,2:3], ['z'])
            a[(slice(0,1),)] = 'x'
            AreEqual(a[(slice(0,1),)], ['x'])
            a[(slice(0,1),slice(1,2))] = 'y'
            AreEqual(a[(slice(0,1),slice(1,2))], ['y'])
            a[(slice(0,1),slice(1,2),slice(2,3))] = 'z'
            AreEqual(a[(slice(0,1),slice(1,2),slice(2,3))], ['z'])


def test_index_by_tuple():
    class indexable:
        def __getitem__(self, index):
            return index
        def __setitem__(self, index, value):
            self.index = index
            self.value = value

    i = indexable()
    AreEqual(i["Hi"], "Hi")
    AreEqual(i[(1, 2)], (1, 2))
    AreEqual(i[3, 4], (3, 4))
    AreEqual(i[(5,)], (5,))
    AreEqual(i[6,], (6,))

    i["Hi"] = "A"; AreEqual(i.index, "Hi"); AreEqual(i.value, "A")
    i[(1, 2)] = "B"; AreEqual(i.index, (1, 2)); AreEqual(i.value, "B")
    i[3, 4] = "C"; AreEqual(i.index, (3, 4)); AreEqual(i.value, "C")
    i[(5,)] = "D"; AreEqual(i.index, (5,)); AreEqual(i.value, "D")
    i[6,] = "E"; AreEqual(i.index, (6,)); AreEqual(i.value, "E")


def test_assignment_order():
  # declare types to log the execution ordering
  class Q:
    def __init__(self):
      self.log = ""
    # we're just doing assignment, so don't define a __getitem__
    def __setitem__(self, idx, val):
      self.log += "(idx=%s, val=%s)" % ( idx, val)
  c=Q()
  def f():
    c[0]=1 # do a side effect to log execution order of f()
    return 'x'
  # Now execute the interesting statement. This has side-effects in c.log to log execution order.
  c[5]=c[2]=f()
  # now check that order is as expected
  # - assignments should occur from left to right
  # - rhs expression is evalled first, and should only be executed once,
  AreEqual(c.log, "(idx=0, val=1)(idx=5, val=x)(idx=2, val=x)")

def test_custom_indexer():
    class cust_index(object):
        def __init__(self, index):
            self.index = index
        def __index__(self):
            return self.index
    
    for sliceable in [x(list(range(5))) for x in (list, tuple)]:
        AreEqual(sliceable[cust_index(0)], 0)
        AreEqual(sliceable[cust_index(0)], 0)
        AreEqual(list(sliceable[cust_index(0) : cust_index(3)]), [0, 1, 2])
        AreEqual(list(sliceable[cust_index(0) : cust_index(3)]), [0, 1, 2])
    
    # dictionary indexing shouldn't be affected
    x = cust_index(42)
    d = {x:3}
    AreEqual(d[x], 3)
    for key in list(d.keys()):
        AreEqual(key, x)

    if is_cli:
        from System.Collections.Generic import List
        List[int]
    
@skip('win32')
def test_csharp_enumeration():
    a = CSharpEnumerable()
    for method in ('GetEnumerableOfInt', 'GetEnumerableOfObject', 'GetEnumerable',
            'GetEnumeratorOfInt', 'GetEnumeratorOfObject', 'GetEnumerator'):
        sum = 0
        for i in getattr(a, method)():
            sum = sum + i
        AreEqual(sum, 6)

def test_error():
    l = []
    if is_cpython: #http://ironpython.codeplex.com/workitem/28220
        AssertErrorWithPartialMessage(TypeError, "'builtin_function_or_method' object is not subscriptable", lambda: l.append[float](1.0))
        AssertErrorWithPartialMessage(TypeError, "'int' object is not subscriptable", lambda: 1[2])
    else:
        AssertErrorWithPartialMessage(TypeError, "'builtin_function_or_method' object is unsubscriptable", lambda: l.append[float](1.0))
        AssertErrorWithPartialMessage(TypeError, "'int' object is unsubscriptable", lambda: 1[2])
    
def test_cp19350_index_restrictions():
    global keyValue
    class X(object):
        def __setitem__(self, key, value):
            global keyValue
            keyValue = key
    
    def f(a, b):
         X()[a, b] = object()
    
    f(1, 2)
    AreEqual(keyValue, (1, 2))
    f('one', 'two') 
    AreEqual(keyValue, ('one', 'two'))

run_test(__name__)
