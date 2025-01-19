# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

'''
Tests for CPython's array module.
'''

import array
import sys

from iptest import IronPythonTestCase, is_cli, is_mono, big, run_test

class ArrayTest(IronPythonTestCase):
    def test_ArrayType(self):
        self.assertEqual(array.ArrayType,
                array.array)

    def test_array___add__(self):
        '''
        TODO
        '''
        pass

    def test_array___class__(self):
        '''
        TODO
        '''
        pass

    def test_array___contains__(self):
        a = array.array('b', (1,))
        self.assertNotIn(object(), a)
        self.assertIn(1, a)

    def test_array___copy__(self):
        '''
        TODO: revisit
        '''
        x = array.array('i', [1,2,3])
        y = x.__copy__()
        self.assertTrue(id(x) != id(y), "copy should copy")

        y = x.__deepcopy__(x)
        self.assertTrue(id(x) != id(y), "copy should copy")

    def test_array___deepcopy__(self):
        '''
        TODO
        '''
        pass

    def test_array___delattr__(self):
        '''
        TODO
        '''
        pass

    def test_array___delitem__(self):
        '''
        TODO
        '''
        pass

    def test_array___delslice__(self):
        '''
        TODO
        '''
        pass

    def test_array___doc__(self):
        '''
        TODO
        '''
        pass

    def test_array___eq__(self):
        '''
        TODO
        '''
        pass

    def test_array___format__(self):
        '''
        TODO
        '''
        pass

    def test_array___ge__(self):
        '''
        TODO
        '''
        pass

    def test_array___getattribute__(self):
        '''
        TODO
        '''
        pass

    def test_array___getitem__(self):
        '''
        TODO
        '''
        pass

    def test_array___getslice__(self):
        '''
        TODO
        '''
        pass

    def test_array___gt__(self):
        '''
        TODO
        '''
        pass

    def test_array___hash__(self):
        '''
        TODO
        '''
        pass

    def test_array___iadd__(self):
        '''
        TODO
        '''
        pass

    def test_array___imul__(self):
        '''
        TODO
        '''
        pass

    def test_array___init__(self):
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
            self.assertEqual(temp_array1[0], x)

            temp_array1 = array.array('I', [x, x])
            self.assertEqual(temp_array1[0], x)
            self.assertEqual(temp_array1[1], x)

        for x in [  (2**32), (2**32)+1, (2**32)+2 ]:
            self.assertRaises(OverflowError, array.array, 'I', [x])

        #--u
        a = array.array('u', "stuff")
        a[1:0] = a
        b = array.array('u', "stuff"[:1] + "stuff" + "stuff"[1:])
        self.assertEqual(a, b)

        #--L
        a = array.array('L', b"\x12\x34\x45\x67")
        self.assertEqual(1, len(a))
        self.assertEqual(1732588562, a[0])

        #--B
        a = array.array('B', [0]) * big(2)
        self.assertEqual(2, len(a))
        self.assertEqual("array('B', [0, 0])", str(a))

        #--b
        self.assertEqual(array.array('b', b'foo'), array.array('b', [102, 111, 111]))

        #--H
        self.assertEqual(array.array('H', b'foof'), array.array('H', [28518, 26223]))
        self.assertEqual(array.array('H', bytearray(b'foof')), array.array('H', [28518, 26223]))
        self.assertEqual(array.array('H', array.array('b', b'foof')), array.array('H', [102, 111, 111, 102]))
        self.assertEqual(array.array('H', memoryview(b'foof')), array.array('H', [102, 111, 111, 102]))
        self.assertEqual(array.array('H', memoryview(b"foof").cast('h')), array.array('H', [28518, 26223]))
        self.assertEqual(array.array('H', memoryview(b"f.o.")[::2]), array.array('H', [102, 111]))
        self.assertEqual(array.array('H', memoryview(b"fo\0\0of\0\0").cast('I')), array.array('H', [28518, 26223]))

        self.assertRaisesRegex(ValueError, "^bytes length not a multiple of item size$", array.array, 'H', b'foo')
        with self.assertRaisesRegex(NotImplementedError, "^multi-dimensional sub-views are not implemented$"):
            array.array('H', memoryview(bytes(range(16))).cast('H', (4,2)))
        with self.assertRaisesRegex(TypeError, "^invalid indexing of 0-dim memory$"):
            array.array('H', memoryview(b"xy").cast('H', ()))

    def test_array___iter__(self):
        '''
        TODO
        '''
        pass

    def test_array___le__(self):
        '''
        TODO
        '''
        pass

    def test_array___len__(self):
        '''
        TODO
        '''
        pass

    def test_array___lt__(self):
        '''
        TODO
        '''
        pass

    def test_array___mul__(self):
        '''
        TODO
        '''
        pass

    def test_array___ne__(self):
        '''
        TODO
        '''
        pass

    def test_array___new__(self):
        '''
        TODO
        '''
        pass

    def test_array___reduce__(self):
        '''
        TODO: revisit
        '''
        x = array.array('i', [1,2,3])
        with self.assertRaises(TypeError):
            x.__reduce__()

    def test_array___reduce_ex__(self):
        '''
        TODO: revisit
        '''
        x = array.array('i', [1,2,3])
        self.assertEqual(repr(x.__reduce_ex__(1)), "(<class 'array.array'>, ('i', [1, 2, 3]), None)")
        with self.assertRaises(TypeError):
            x.__reduce_ex__()

    def test_array___repr__(self):
        '''
        TODO
        '''
        pass

    def test_array___rmul__(self):
        '''
        TODO
        '''
        pass

    def test_array___setattr__(self):
        '''
        TODO
        '''
        pass

    def test_array___setitem__(self):
        '''
        TODO
        '''
        pass

    def test_array___setslice__(self):
        '''
        TODO
        '''
        pass

    def test_array___sizeof__(self):
        '''
        TODO
        '''
        pass

    def test_array___str__(self):
        '''
        TODO
        '''
        pass

    def test_array___subclasshook__(self):
        '''
        TODO
        '''
        pass

    def test_array_append(self):
        '''
        TODO
        '''
        pass

    def test_array_buffer_info(self):
        '''
        TODO
        '''
        pass

    def test_array_byteswap(self):
        '''
        TODO
        '''
        pass

    def test_array_count(self):
        a = array.array('b', (1,)*4)
        self.assertEqual(a.count(1), 4)
        self.assertEqual(a.count(object()), 0)

    def test_array_extend(self):
        '''
        TODO
        '''
        pass

    def test_array_fromfile(self):
        '''
        TODO
        '''
        pass

    def test_array_frombytes(self):
        # TODO: add more tests
        a = array.array('H')

        a.frombytes(b"foof")
        self.assertEqual(a, array.array('H', [28518, 26223]))

        a.frombytes(memoryview(b"foof"))
        self.assertEqual(a, array.array('H', [28518, 26223, 28518, 26223]))

        with self.assertRaisesRegex(BufferError, "^memoryview: underlying buffer is not C[- ]contiguous$"):
            a.frombytes(memoryview(b"f.o.")[::2])

    def test_array_fromlist(self):
        '''
        TODO
        '''
        pass

    def test_array_fromstring(self):
        '''
        TODO
        '''
        pass

    def test_array_fromunicode(self):
        # TODO: add more tests
        a = array.array('u')
        a.fromunicode(u"a"*20)
        self.assertEqual(len(a), 20)
        a.fromunicode(u"b"*100)
        self.assertEqual(len(a), 120)
        self.assertEqual(a.tolist(), [u"a"]*20 + [u"b"]*100)

    def test_array_index(self):
        a = array.array('b', (1,))
        with self.assertRaises(ValueError):
            a.remove(object())
        self.assertEqual(a.index(1), 0)


    def test_array_insert(self):
        '''
        TODO
        '''
        pass

    def test_array_itemsize(self):
        '''
        TODO
        '''
        pass

    def test_array_pop(self):
        '''
        TODO
        '''
        pass

    def test_array_read(self):
        '''
        TODO
        '''
        pass

    def test_array_remove(self):
        a = array.array('b', (1,))
        with self.assertRaises(ValueError):
            a.remove(object())
        a.remove(1)
        self.assertEqual(len(a), 0)

    def test_array_reverse(self):
        '''
        TODO
        '''
        pass

    def test_array_tofile(self):
        '''
        TODO
        '''
        pass

    def test_array_tolist(self):
        '''
        TODO
        '''
        pass

    def test_array_tostring(self):
        self.assertEqual(array.array('u', u'abc').tostring(), b'a\x00b\x00c\x00')

    def test_array_tounicode(self):
        '''
        TODO
        '''
        pass

    def test_array_typecode(self):
        '''
        TODO: revisit
        '''
        x = array.array('i')
        self.assertEqual(type(x.typecode), str)

    def test_array_write(self):
        '''
        TODO
        '''
        pass


    def test_cp9348(self):
        test_cases = {
                        ('b', b"a") : "array('b', [97])",
                        ('B', b"a") : "array('B', [97])",
                        ('u', u"a") : "array('u', 'a')",
                        ('h', b"\x12\x34") : "array('h', [13330])",
                        ('H', b"\x12\x34") : "array('H', [13330])",
                        ('i', b"\x12\x34\x45\x67") : "array('i', [1732588562])",
                        ('I', b"\x12\x34\x45\x67") : "array('I', [1732588562])",
                        ('I', b"\x01\x00\x00\x00") : "array('I', [1])",
                        ('l', b"\x12\x34\x45\x67") : "array('l', [1732588562])",
                        ('L', b"\x12\x34\x45\x67") : "array('L', [1732588562])",
                    }
        if is_cli: # https://github.com/IronLanguages/ironpython2/issues/102
            test_cases[('d', b"\x12\x34\x45\x67\x12\x34\x45\x67")] = "array('d', [2.9522485325887698e+189])"
            test_cases[('f', b"\x12\x34\x45\x67")] = "array('f', [9.3126672485384569e+23])"
        else:
            test_cases[('d', b"\x12\x34\x45\x67\x12\x34\x45\x67")] = "array('d', [2.95224853258877e+189])"
            test_cases[('f', b"\x12\x34\x45\x67")] = "array('f', [9.312667248538457e+23])"

        for key in test_cases.keys():
            type_code, param = key
            temp_val = array.array(type_code, param)
            self.assertEqual(str(temp_val), test_cases[key])


    def test_cp8736(self):
        a = array.array('b')
        for i in [-1, -2, -3, -(2**8), -1000, -(2**16)+1, -(2**16), -(2**16)-1, -(2**64)]:
            a[:i] = a
            self.assertEqual(str(a), "array('b')")

        a2 = array.array('b', b'a')
        a2[:-1] = a2
        self.assertEqual(str(a2), "array('b', [97, 97])")
        a2[:-(2**64)-1] = a2
        self.assertEqual(str(a2), "array('b', [97, 97, 97, 97])")


    def test_cp9350(self):
        for i in [1, big(1)]:
            a = array.array('B', [0]) * i
            self.assertEqual(a, array.array('B', [0]))

        for i in [2, big(2)]:
            a = array.array('B', [0]) * i
            self.assertEqual(a, array.array('B', [0, 0]))

        for i in [2**8, big(2**8)]:
            a = array.array('B', [1]) * i
            self.assertEqual(a, array.array('B', [1]*2**8))

    def test_gh870(self):
        string_types = ['u']
        number_types = ['b', 'B', 'h', 'H', 'i', 'I', 'I', 'l', 'L', 'q', 'Q']

        for typecode in string_types:
            a = array.array(typecode, 'a')
            a += a
            a.extend(a)
            self.assertEqual(a, 4*array.array(typecode, 'a'))

        for typecode in number_types:
            a = array.array(typecode, [1])
            a += a
            a.extend(a)
            self.assertEqual(a, 4*array.array(typecode, [1]))

    def test_coverage(self):
        '''
        Test holes as found by code coverage runs.  These need to be refactored and
        moved to other functions throughout this module (TODO).
        '''
        #--Postive
        a = array.array('b', b'a')
        for i in [  big(0), big(1), big(2), big(3), big(32766), big(32767), big(32768), big(65534), big(65535), big(65536),
                    big(456720545), #http://ironpython.codeplex.com/WorkItem/View.aspx?WorkItemId=24314
                    ]:
            self.assertEqual(i,
                    len(i*a))
            self.assertEqual(i, len(a*i))

        #--Negative
        self.assertRaises(OverflowError, lambda: big(sys.maxsize+1)*a)
        self.assertRaises(OverflowError, lambda: a*big(sys.maxsize+1))
        if not is_mono: # these do not fail on Mono
            self.assertRaises(MemoryError, lambda: big(sys.maxsize)*a)
            self.assertRaises(MemoryError, lambda: a*big(sys.maxsize))

        #--Positive
        a = array.array('b', b'abc')
        del a[:]
        self.assertEqual(a, array.array('b', b''))

        a = array.array('b', b'abc')
        del a[0:]
        self.assertEqual(a, array.array('b', b''))

        a = array.array('b', b'abc')
        del a[1:1]
        self.assertEqual(a, array.array('b', b'abc'))

        a = array.array('b', b'abc')
        del a[1:4]
        self.assertEqual(a, array.array('b', b'a'))

        a = array.array('b', b'abc')
        del a[0:1]
        self.assertEqual(a, array.array('b', b'bc'))

run_test(__name__)
