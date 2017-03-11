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
Tests for CPython's array module.
'''

#--IMPORTS---------------------------------------------------------------------
from iptest.assert_util import *
skiptest("silverlight")

import array

#--GLOBALS---------------------------------------------------------------------

#--HELPERS---------------------------------------------------------------------

#--TEST CASES------------------------------------------------------------------
def test_ArrayType():
    AreEqual(array.ArrayType,
             array.array)

def test_array___add__():
    '''
    TODO
    '''
    pass

def test_array___class__():
    '''
    TODO
    '''
    pass

def test_array___contains__():
    '''
    TODO
    '''
    pass

def test_array___copy__():
    '''
    TODO: revisit
    '''
    x = array.array('i', [1,2,3])
    y = x.__copy__()
    Assert(id(x) != id(y), "copy should copy")
    
    y = x.__deepcopy__(x)
    Assert(id(x) != id(y), "copy should copy")

def test_array___deepcopy__():
    '''
    TODO
    '''
    pass

def test_array___delattr__():
    '''
    TODO
    '''
    pass

def test_array___delitem__():
    '''
    TODO
    '''
    pass

def test_array___delslice__():
    '''
    TODO
    '''
    pass

def test_array___doc__():
    '''
    TODO
    '''
    pass

def test_array___eq__():
    '''
    TODO
    '''
    pass

def test_array___format__():
    '''
    TODO
    '''
    pass

def test_array___ge__():
    '''
    TODO
    '''
    pass

def test_array___getattribute__():
    '''
    TODO
    '''
    pass

def test_array___getitem__():
    '''
    TODO
    '''
    pass

def test_array___getslice__():
    '''
    TODO
    '''
    pass

def test_array___gt__():
    '''
    TODO
    '''
    pass

def test_array___hash__():
    '''
    TODO
    '''
    pass

def test_array___iadd__():
    '''
    TODO
    '''
    pass

def test_array___imul__():
    '''
    TODO
    '''
    pass

def test_array___init__():
    '''
    TODO: revist!
    '''
    #--I
    for x in [  0, 1, 2,
                (2**8)-2, (2**8)-1, (2**8), (2**8)+1, (2**8)+2,
                (2**16)-2, (2**16)-1, (2**16), (2**16)+1, (2**16)+2,
                (2**32)-2, (2**32)-1,
                ]:
                
        temp_array1 = array.array('I', [x])
        AreEqual(temp_array1[0], x)
        
        temp_array1 = array.array('I', [x, x])
        AreEqual(temp_array1[0], x)
        AreEqual(temp_array1[1], x)
        
    for x in [  (2**32), (2**32)+1, (2**32)+2 ]:
        AssertError(OverflowError, array.array, 'I', [x])

    #--c
    a = array.array('c', "stuff")
    a[1:0] = a
    b = array.array('c', "stuff"[:1] + "stuff" + "stuff"[1:])
    AreEqual(a, b)

    #--L
    a = array.array('L', "\x12\x34\x45\x67")
    AreEqual(1, len(a))
    AreEqual(1732588562, a[0])

    #--B
    a = array.array('B', [0]) * 2
    AreEqual(2, len(a))
    AreEqual("array('B', [0, 0])", str(a))
    
    #--b
    AreEqual(array.array('b', 'foo'), array.array('b', [102, 111, 111]))

def test_array___iter__():
    '''
    TODO
    '''
    pass

def test_array___le__():
    '''
    TODO
    '''
    pass

def test_array___len__():
    '''
    TODO
    '''
    pass

def test_array___lt__():
    '''
    TODO
    '''
    pass

def test_array___mul__():
    '''
    TODO
    '''
    pass

def test_array___ne__():
    '''
    TODO
    '''
    pass

def test_array___new__():
    '''
    TODO
    '''
    pass

def test_array___reduce__():
    '''
    TODO: revisit
    '''
    x = array.array('i', [1,2,3])
    AreEqual(repr(x.__reduce__()), "(<type 'array.array'>, ('i', [1, 2, 3]), None)")

def test_array___reduce_ex__():
    '''
    TODO: revisit
    '''
    x = array.array('i', [1,2,3])
    AreEqual(repr(x.__reduce_ex__(1)), "(<type 'array.array'>, ('i', [1, 2, 3]), None)")
    AreEqual(repr(x.__reduce_ex__()), "(<type 'array.array'>, ('i', [1, 2, 3]), None)")

def test_array___repr__():
    '''
    TODO
    '''
    pass
    
def test_array___rmul__():
    '''
    TODO
    '''
    pass

def test_array___setattr__():
    '''
    TODO
    '''
    pass

def test_array___setitem__():
    '''
    TODO
    '''
    pass

def test_array___setslice__():
    '''
    TODO
    '''
    pass

def test_array___sizeof__():
    '''
    TODO
    '''
    pass

def test_array___str__():
    '''
    TODO
    '''
    pass

def test_array___subclasshook__():
    '''
    TODO
    '''
    pass

def test_array_append():
    '''
    TODO
    '''
    pass

def test_array_buffer_info():
    '''
    TODO
    '''
    pass

def test_array_byteswap():
    '''
    TODO
    '''
    pass

def test_array_count():
    '''
    TODO
    '''
    pass

def test_array_extend():
    '''
    TODO
    '''
    pass

def test_array_fromfile():
    '''
    TODO
    '''
    pass

def test_array_fromlist():
    '''
    TODO
    '''
    pass

def test_array_fromstring():
    '''
    TODO
    '''
    pass

def test_array_fromunicode():
    '''
    TODO
    '''
    pass

def test_array_index():
    '''
    TODO
    '''
    pass

def test_array_insert():
    '''
    TODO
    '''
    pass

def test_array_itemsize():
    '''
    TODO
    '''
    pass

def test_array_pop():
    '''
    TODO
    '''
    pass

def test_array_read():
    '''
    TODO
    '''
    pass

def test_array_remove():
    '''
    TODO
    '''
    pass

def test_array_reverse():
    '''
    TODO
    '''
    pass

def test_array_tofile():
    '''
    TODO
    '''
    pass

def test_array_tolist():
    '''
    TODO
    '''
    pass

def test_array_tostring():
    import array
    AreEqual(array.array('u', 'abc').tostring(), 'a\x00b\x00c\x00')

def test_array_tounicode():
    '''
    TODO
    '''
    pass

def test_array_typecode():
    '''
    TODO: revisit
    '''
    x = array.array('i')
    AreEqual(type(x.typecode), str)

def test_array_write():
    '''
    TODO
    '''
    pass


def test_cp9348():
    test_cases = {  ('c', "a") : "array('c', 'a')",
                    ('b', "a") : "array('b', [97])",
                    ('B', "a") : "array('B', [97])",
                    ('u', "a") : "array('u', u'a')",
                    ('h', "\x12\x34") : "array('h', [13330])",
                    ('H', "\x12\x34") : "array('H', [13330])",
                    ('i', "\x12\x34\x45\x67") : "array('i', [1732588562])",
                    ('I', "\x12\x34\x45\x67") : "array('I', [1732588562L])",
                    ('I', "\x01\x00\x00\x00") : "array('I', [1L])",
                    ('l', "\x12\x34\x45\x67") : "array('l', [1732588562])",
                    ('L', "\x12\x34\x45\x67") : "array('L', [1732588562L])",
                }
    if is_cpython: #http://ironpython.codeplex.com/workitem/28212
        test_cases[('d', "\x12\x34\x45\x67\x12\x34\x45\x67")] = "array('d', [2.95224853258877e+189])"
        test_cases[('f', "\x12\x34\x45\x67")] = "array('f', [9.312667248538457e+23])"
    else:
        test_cases[('d', "\x12\x34\x45\x67\x12\x34\x45\x67")] = "array('d', [2.9522485325887698e+189])"
        test_cases[('f', "\x12\x34\x45\x67")] = "array('f', [9.3126672485384569e+23])"

    for key in list(test_cases.keys()):
        type_code, param = key
        temp_val = array.array(type_code, param)
        AreEqual(str(temp_val), test_cases[key])


def test_cp8736():
    a = array.array('b')
    for i in [-1, -2, -3, -(2**8), -1000, -(2**16)+1, -(2**16), -(2**16)-1, -(2**64)]:
        a[:i] = a
        AreEqual(str(a), "array('b')")

    a2 = array.array('b', 'a')
    a2[:-1] = a2
    AreEqual(str(a2), "array('b', [97, 97])")
    a2[:-(2**64)-1] = a2 
    AreEqual(str(a2), "array('b', [97, 97, 97, 97])")  


def test_cp9350():
    for i in [1, 1]:
        a = array.array('B', [0]) * i
        AreEqual(a, array.array('B', [0]))

    for i in [2, 2]:
        a = array.array('B', [0]) * i
        AreEqual(a, array.array('B', [0, 0]))
    
    for i in [2**8, int(2**8)]:
        a = array.array('B', [1]) * i
        AreEqual(a, array.array('B', [1]*2**8))

def test_gh870():
    string_types = ['c', 'b', 'B', 'u']
    number_types = ['h', 'H', 'i', 'I', 'I', 'l', 'L']

    for typecode in string_types:
        a = array.array(typecode, 'a')
        a += a
        a.extend(a)
        AreEqual(a, 4*array.array(typecode, 'a'))

    for typecode in number_types:
        a = array.array(typecode, [1])
        a += a
        a.extend(a)
        AreEqual(a, 4*array.array(typecode, [1]))

def test_coverage():
    '''
    Test holes as found by code coverage runs.  These need to be refactored and
    moved to other functions throughout this module (TODO).
    '''
    #--Postive
    a = array.array('b', 'a')
    for i in [  0, 1, 2, 3, 32766, 32767, 32768, 65534, 65535, 65536, 
                456720545, #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24314
                ]:
        AreEqual(i,
                 len(i*a))
        AreEqual(i, len(a*i))
    
    #--Negative
    AssertError(OverflowError, lambda: 4567206470*a)
    AssertError(OverflowError, lambda: a*4567206470)
    if not is_posix: # these do not fail on Mono
        AssertError(MemoryError,   lambda: 2147483646*a)
        AssertError(MemoryError,   lambda: a*2147483646)
    
    #--Positive
    a = array.array('b', 'abc')
    del a[:]
    AreEqual(a, array.array('b', ''))
    
    a = array.array('b', 'abc')
    del a[0:]
    AreEqual(a, array.array('b', ''))
    
    a = array.array('b', 'abc')
    del a[1:1]
    AreEqual(a, array.array('b', 'abc'))
    
    a = array.array('b', 'abc')
    del a[1:4]
    AreEqual(a, array.array('b', 'a'))
    
    a = array.array('b', 'abc')
    del a[0:1]
    AreEqual(a, array.array('b', 'bc'))
    
    
#--MAIN------------------------------------------------------------------------
run_test(__name__)
