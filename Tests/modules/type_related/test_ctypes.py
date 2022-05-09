# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

'''
Tests for CPython's ctypes module.
'''

from ctypes import *
from array import array
import sys
import gc

from iptest import IronPythonTestCase, is_cli, big, myint, run_test

class CTypesTest(IronPythonTestCase):
    export_error_msg = "Existing exports of data: object cannot be re-sized" if is_cli else "cannot resize an array that is exporting buffers"
    readonly_error_msg = "underlying buffer is not writable"

    def test_from_array(self):
        arr = array('i', range(16))
        c = (c_int * 15).from_buffer(arr, sizeof(c_int))

        self.assertEqual(c[:], arr.tolist()[1:])

        arr[9] = -1
        self.assertEqual(c[8], -1)

        self.assertRaisesMessage(BufferError, self.export_error_msg, arr.append, 100)
        self.assertRaisesMessage(BufferError, self.export_error_msg, arr.insert, 10, 100)

        del c
        gc.collect()
        arr.append(100)
        self.assertEqual(arr[-1], 100)

    def test_from_memoryview(self):
        arr = array('i', range(16))
        with memoryview(arr) as mv:
            self.assertRaisesMessage(BufferError, self.export_error_msg, arr.append, 100)
            mv[10] = -2
            c = (c_int * 15).from_buffer(mv, sizeof(c_int))

        del mv
        gc.collect()

        arr[9] = -1
        self.assertEqual(c[8], -1)
        self.assertEqual(c[9], -2)

        self.assertRaisesMessage(BufferError, self.export_error_msg, arr.append, 100)
        self.assertRaisesMessage(BufferError, self.export_error_msg, arr.insert, 10, 100)
        l = [c]
        l.append(l)
        del c
        gc.collect()

        self.assertRaisesMessage(BufferError, self.export_error_msg, arr.append, 100)
        del l
        gc.collect()
        arr.append(100)
        self.assertEqual(arr[-1], 100)

    def test_from_bytes(self):
        b = b"abcd" * 4
        ba = bytearray(b)

        c = (c_char * 15).from_buffer(ba, sizeof(c_char))
        self.assertEqual(c[:], b[1:])
        self.assertRaisesMessage(TypeError, self.readonly_error_msg, (c_char * 15).from_buffer, b, sizeof(c_char))

    def test_bitfield_bool(self):
        """
        Tests pertaining of bitfields of type c_bool
        # https://github.com/IronLanguages/ironpython3/issues/1439
        """

        # simple 1-field, 1-bit structure
        class Test1(Structure):
            _fields_ = [("x", c_bool, 1)]

        self.assertIs(Test1(True).x, True)

        # 2-field, 1-bit structure
        class Test2(Structure):
            _fields_ = [("x", c_bool, 1), ("y", c_bool, 1)]

        class Test3(Structure):
            _fields_ = [("x", c_bool, 3), ("y", c_bool, 4)]

        for T in (Test2, Test3):
            self.assertIs(T().x, False)
            self.assertIs(T().y, False)
            self.assertIs(T(True).x, True)
            self.assertIs(T(True, True).x, True)
            self.assertIs(T(True, True).y, True)
            self.assertIs(T(False, False).x, False)
            self.assertIs(T(False, False).y, False)
            self.assertIs(T(True, False).y, False)
            self.assertIs(T(False, True).y, True)
            if is_cli: # bug in CPython: https://github.com/python/cpython/issues/90914
                self.assertIs(T(True, False).x, True)
                self.assertIs(T(False, True).x, False)
                self.assertIs(T(True).y, False)
            self.assertRaisesMessage(TypeError, "too many initializers", T, True, False, True)

            # Testing assignments
            s = T()
            self.assertIs(s.x, False)
            self.assertIs(s.y, False)
            s.x = True
            self.assertIs(s.x, True)
            if is_cli: # bug in CPython: https://github.com/python/cpython/issues/90914
                self.assertIs(s.y, False)

            s = T()
            self.assertIs(s.x, False)
            self.assertIs(s.y, False)
            s.y = True
            self.assertIs(s.y, True)
            if is_cli: # bug in CPython: https://github.com/python/cpython/issues/90914
                self.assertIs(s.x, False)

            s = T()
            self.assertIs(s.x, False)
            self.assertIs(s.y, False)
            s.y = 2
            self.assertIs(s.y, True)
            if is_cli: # bug in CPython: https://github.com/python/cpython/issues/90914
                self.assertIs(s.x, False)

            s = T()
            self.assertIs(s.x, False)
            self.assertIs(s.y, False)
            s.y = 3
            self.assertIs(s.y, True)
            if is_cli: # bug in CPython: https://github.com/python/cpython/issues/90914
                self.assertIs(s.x, False)

        b = bytearray(1)
        s =Test3.from_buffer(b)
        s.x = 2 # meaning True, i.e. 0x01
        s.y = 4 # meaning True, i.e 0x01 << 3 == 0x08
        if is_cli: # bug in CPython: https://github.com/python/cpython/issues/90914
            self.assertEqual(b, b"\x09")

    def test_bitfield_bool_truthy(self):
        """Testing non-bool arguments for of bitfields of type c_bool"""

        class Test(Structure):
            _fields_ = [("x", c_bool, 1)]

        class myindex:
            def __init__(self, value):
                self.value = value
            def __index__(self):
                return self.value

        for val in (None, "", "False", "True", "0", "1", 0, 1, 2, 0.0, 1.0, [], (), {}, object(), myindex(0), myindex(1)):
            with self.subTest(val=val):
                self.assertIs(Test(val).x, bool(val))

                s = Test()
                s.x = val
                self.assertIs(s.x, bool(val))

    def test_bitfield_long(self):
        """Tests for bitfields of type c_long"""

        class Test(Structure):
            _fields_ = [("x", c_long, 16), ("y", c_long, 16), ("z", c_long, 32)]

        class myindex:
            def __init__(self, value):
                self.value = value
            def __index__(self):
                return self.value

        self.assertEqual(Test(0x1234).x, 0x1234)
        self.assertEqual(Test(big(0x1234)).x, 0x1234)
        self.assertEqual(Test(myint(0x1234)).x, 0x1234)
        self.assertEqual(Test(True).x, 1)
        if is_cli or sys.version_info >= (3, 8):
            self.assertEqual(Test(myindex(0x1234)).x, 0x1234)
            if is_cli or sys.version_info >= (3, 10):
                msg = "'float' object cannot be interpreted as an integer"
            else:
                msg = "int expected instead of float"
            self.assertRaisesMessage(TypeError, msg, Test, 2.3)

        with self.assertRaisesMessage(ValueError, "number of bits invalid for bit field"):
            class Test(Structure):
                _fields_ = [("x", c_int, 0)]

        self.assertEqual(repr(Test.x), "<Field type=c_long, ofs=0:0, bits=16>")
        self.assertEqual(repr(Test.y), "<Field type=c_long, ofs=0:16, bits=16>")
        self.assertEqual(repr(Test.z), "<Field type=c_long, ofs=4:0, bits=32>")

        self.assertEqual((Test.x.offset, Test.x.size), (0, (16 << 16) + 0))
        self.assertEqual((Test.y.offset, Test.y.size), (0, (16 << 16) + 16))
        self.assertEqual((Test.z.offset, Test.z.size), (4, (32 << 16) + 0))

    def test_bitfield_longlong(self):
        """Tests for bitfields of type c_longlong"""

        class TestS(Structure):
            _fields_ = [("x", c_longlong, 63)]

        self.assertEqual(TestS((1 << 64)).x, 0)
        self.assertEqual(TestS((1 << 64) - 1).x, -1)
        self.assertEqual(TestS(-(1 << 64)).x, 0)
        self.assertEqual(TestS(-(1 << 64) - 1).x, -1)

        class TestU(Structure):
            _fields_ = [("x", c_ulonglong, 63)]

        self.assertEqual(TestU((1 << 64)).x, 0)
        self.assertEqual(TestU((1 << 64) - 1).x, 0x7fffffffffffffff)
        self.assertEqual(TestU(-(1 << 64)).x, 0)
        self.assertEqual(TestU(-(1 << 64) - 1).x, 0x7fffffffffffffff)

run_test(__name__)
