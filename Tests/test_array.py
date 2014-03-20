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
## Test array support by IronPython (System.Array)
##

from iptest.assert_util import *

if not is_cpython:
    import System

@skip("win32")
def test_sanity():
    # 1-dimension array
    array1 = System.Array.CreateInstance(int, 2)
    for i in range(2): array1[i] = i * 10
    
    AssertError(IndexError, lambda: array1[2])
        
    array2 = System.Array.CreateInstance(int, 4)
    for i in range(2, 6): array2[i - 2] = i * 10

    array3 = System.Array.CreateInstance(float, 3)
    array3[0] = 2.1
    array3[1] = 3.14
    array3[2] = 0.11

    ## __setitem__/__getitem__
    System.Array.__setitem__(array3, 2, 0.14)
    AreEqual(System.Array.__getitem__(array3, 1), 3.14)
    AreEqual([x for x in System.Array.__getitem__(array3, slice(2))], [2.1, 3.14])

    ## __repr__

    # 2-dimension array
    array4 = System.Array.CreateInstance(int, 2, 2)
    array4[0, 1] = 1
    Assert(repr(array4).startswith("<2 dimensional Array[int] at"), "bad repr for 2-dimensional array")

    # 3-dimension array
    array5 = System.Array.CreateInstance(object, 2, 2, 2)
    array5[0, 1, 1] = int
    Assert(repr(array5).startswith("<3 dimensional Array[object] at "), "bad repr for 3-dimensional array")

    ## index access
    AssertError(TypeError, lambda : array5['s'])
    def f1(): array5[0, 1] = 0
    AssertError(ValueError, f1)
    def f2(): array5['s'] = 0
    AssertError(TypeError, f2)

    ## __add__/__mul__
    for f in (
        lambda a, b : System.Array.__add__(a, b),
        lambda a, b : a + b
        ) :
        
        temp = System.Array.__add__(array1, array2)
        result = f(array1, array2)
        
        for i in range(6): AreEqual(i * 10, result[i])
        AreEqual(repr(result), "Array[int]((0, 10, 20, 30, 40, 50))")
        
        result = f(array1, array3)
        AreEqual(len(result), 2 + 3)
        AreEqual([x for x in result], [0, 10, 2.1, 3.14, 0.14])
        
        AssertError(NotImplementedError, f, array1, array4)
        
    for f in [
        lambda a, x: System.Array.__mul__(a, x),
        lambda a, x: array1 * x
        ]:

        AreEqual([x for x in f(array1, 4)], [0, 10, 0, 10, 0, 10, 0, 10])
        AreEqual([x for x in f(array1, 5)], [0, 10, 0, 10, 0, 10, 0, 10, 0, 10])
        AreEqual([x for x in f(array1, 0)], [])
        AreEqual([x for x in f(array1, -10)], [])

@skip("win32")
def test_slice():
    array1 = System.Array.CreateInstance(int, 20)
    for i in range(20): array1[i] = i * i
    
    # positive
    array1[::2] = [x * 2 for x in range(10)]

    for i in range(0, 20, 2):
        AreEqual(array1[i], i)
    for i in range(1, 20, 2):
        AreEqual(array1[i], i * i)

    # negative: not-same-length
    def f(): array1[::2] = [x * 2 for x in range(11)]
    AssertError(ValueError, f)

@skip("win32")
def test_creation():
    t = System.Array
    ti = type(System.Array.CreateInstance(int, 1))

    AssertError(TypeError, t, [1, 2])
    for x in (ti([1,2]), t[int]([1, 2]), ti([1.5, 2.3])):
        AreEqual([i for i in x], [1, 2])
        t.Reverse(x)
        AreEqual([i for i in x], [2, 1])


def _ArrayEqual(a,b):
    AreEqual(a.Length, b.Length)
    for x in xrange(a.Length):
        AreEqual(a[x], b[x])
    
## public static Array CreateInstance (
##    Type elementType,
##    int[] lengths,
##    int[] lowerBounds
##)

@skip('silverlight', 'win32')
def test_nonzero_lowerbound():
    a = System.Array.CreateInstance(int, (5,), (5,))
    for i in xrange(5): a[i] = i
    
    _ArrayEqual(a[:2], System.Array[int]((0,1)))
    _ArrayEqual(a[2:], System.Array[int]((2,3,4)))
    _ArrayEqual(a[2:4], System.Array[int]((2,3)))
    AreEqual(a[-1], 4)

    AreEqual(repr(a), 'Array[int]((0, 1, 2, 3, 4))')

    a = System.Array.CreateInstance(int, (5,), (15,))
    b = System.Array.CreateInstance(int, (5,), (20,))
    _ArrayEqual(a,b)

    ## 5-dimension
    a = System.Array.CreateInstance(int, (2,2,2,2,2), (1,2,3,4,5))
    AreEqual(a[0,0,0,0,0], 0)

    for i in range(5):
        index = [0,0,0,0,0]
        index[i] = 1
        
        a[index[0], index[1], index[2], index[3], index[4]] = i
        AreEqual(a[index[0], index[1], index[2], index[3], index[4]], i)
        
    for i in range(5):
        index = [0,0,0,0,0]
        index[i] = 0
        
        a[index[0], index[1], index[2], index[3], index[4]] = i
        AreEqual(a[index[0], index[1], index[2], index[3], index[4]], i)

    def sliceArray(arr, index):
        arr[:index]

    def sliceArrayAssign(arr, index, val):
        arr[:index] = val

    AssertError(NotImplementedError, sliceArray, a, 1)
    AssertError(NotImplementedError, sliceArray, a, 200)
    AssertError(NotImplementedError, sliceArray, a, -200)
    AssertError(NotImplementedError, sliceArrayAssign, a, -200, 1)
    AssertError(NotImplementedError, sliceArrayAssign, a, 1, 1)

@skip("win32")
def test_array_type():
    
    def type_helper(array_type, instance):
        #create the array type
        AT = System.Array[array_type]
        
        a0 = AT([])
        a1 = AT([instance])
        a2 = AT([instance, instance])
                
        a_normal = System.Array.CreateInstance(array_type, 3)
        Assert(str(AT)==str(type(a_normal)))
        for i in xrange(3):
            a_normal[i] = instance
            Assert(str(AT)==str(type(a_normal)))
   
        a_multi  = System.Array.CreateInstance(array_type, 2, 3)
        Assert(str(AT)==str(type(a_multi)))
        for i in xrange(2):
            for j in xrange(3):
                Assert(str(AT)==str(type(a_multi)))
                a_multi[i, j]=instance
                
        Assert(str(AT)==str(type(a0)))
        Assert(str(AT)==str(type(a0[0:])))
        Assert(str(AT)==str(type(a0[:0])))
        Assert(str(AT)==str(type(a1)))
        Assert(str(AT)==str(type(a1[1:])))
        Assert(str(AT)==str(type(a1[:0])))
        Assert(str(AT)==str(type(a_normal)))
        Assert(str(AT)==str(type(a_normal[:0])))
        Assert(str(AT)==str(type(a_normal[3:])))
        Assert(str(AT)==str(type(a_normal[4:])))
        Assert(str(AT)==str(type(a_normal[1:])))
        Assert(str(AT)==str(type(a_normal[1:1:50])))
        Assert(str(AT)==str(type(a_multi)))
        def silly(): a_multi[0:][1:0]
        AssertError(NotImplementedError, silly)
        Assert(str(AT)==str(type((a0+a1)[:0])))
            
    type_helper(int, 0)
    type_helper(int, 1)
    type_helper(int, 100)
    type_helper(bool, False)
    type_helper(bool, True)
    #type_helper(bool, 1)
    type_helper(long, 0L)
    type_helper(long, 1L)
    type_helper(long, 100L)
    type_helper(float, 0.0)
    type_helper(float, 1.0)
    type_helper(float, 3.14)
    type_helper(str, "")
    type_helper(str, " ")
    type_helper(str, "abc")

#--MAIN------------------------------------------------------------------------
run_test(__name__)
