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
import unittest
from decimal import Decimal

from iptest import IronPythonTestCase, is_posix, is_cli, is_mono, is_netcoreapp, big, myint, run_test

class MyInt:
    def __init__(self, value):
        self.value = value
    def __int__(self):
        return self.value

class MyIndex:
    def __init__(self, value):
        self.value = value
    def __index__(self):
        return self.value

class MyIntIndex:
    def __init__(self, intValue, indexValue):
        self.intValue = intValue
        self.indexValue = indexValue
    def __int__(self):
        return self.intValue
    def __index__(self):
        return self.indexValue


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

        if is_mono:
            with c: pass # gc.collect() in Mono may return before collection is finished
        del c
        gc.collect()
        arr.append(100)
        self.assertEqual(arr[-1], 100)

    @unittest.skipIf(is_mono, "gc.collect() in Mono may return before collection is finished")
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

    @unittest.skipIf(is_posix, 'Windows specific test')
    def test_loadlibrary_error(self):
        with self.assertRaises(OSError) as cm:
            windll.LoadLibrary(__file__)

        self.assertEqual(cm.exception.errno, 8)
        self.assertEqual(cm.exception.winerror, 193)
        self.assertIn(" is not a valid Win32 application", cm.exception.strerror)
        if is_cli:
            self.assertEqual(cm.exception.filename, __file__)
        else:
            self.assertIsNone(cm.exception.filename)
        self.assertIsNone(cm.exception.filename2)


    def test_conversions_c_int(self):
        # normal case
        c_int_value = c_int(42)
        self.assertEqual(c_int_value.value, 42)

        # bool case
        c_int_value = c_int(True)
        self.assertEqual(c_int_value.value, 1)

        # BigInteger case
        c_int_value = c_int(big(42))
        self.assertEqual(c_int_value.value, 42)

        if is_cli or sys.version_info < (3, 10):
            # __int__ supported
            c_int_value = c_int(MyInt(42))
            self.assertEqual(c_int_value.value, 42)
            c_int_value.value = MyInt(24)
            self.assertEqual(c_int_value.value, 24)
        else:
            # __int__ not supported
            self.assertRaises(TypeError, c_int, MyInt(42))
            with self.assertRaises(TypeError):
                c_int_value.value = MyInt(42)

        if is_cli or sys.version_info >= (3, 8):
            # __index__ supported
            c_int_value = c_int(MyIndex(42))
            self.assertEqual(c_int_value.value, 42)
            c_int_value.value = MyIndex(24)
            self.assertEqual(c_int_value.value, 24)

            # __index__ takes priority over __int__
            c_int_value = c_int(MyIntIndex(44, 42))
            self.assertEqual(c_int_value.value, 42)
            c_int_value.value = MyIntIndex(22, 24)
            self.assertEqual(c_int_value.value, 24)
        else:
            # __index__ not supported
            self.assertRaises(TypeError, c_int, MyIndex(42))
            with self.assertRaises(TypeError):
                c_int_value.value = MyIndex(42)

        # str not supported
        self.assertRaises(TypeError, c_int, "abc")
        with self.assertRaises(TypeError):
             c_int_value.value = "abc"

        # float not supported
        self.assertRaises(TypeError, c_int, 42.6)
        with self.assertRaises(TypeError):
             c_int_value.value = 42.6

        # System.Single not supported
        if is_cli:
            import System
            self.assertRaises(TypeError, c_int, System.Single(42.6))
            with self.assertRaises(TypeError):
                c_int_value.value = System.Single(42.6)

        # System.Half not supported
        if is_netcoreapp:
            import System, clr
            half = clr.Convert(42.6, System.Half)
            self.assertRaises(TypeError, c_int, half)
            with self.assertRaises(TypeError):
                c_int_value.value = System.Half(42.6)

        # Decimal is supported as long as __int__ is supported
        if is_cli or sys.version_info < (3, 10):
            c_int_value = c_int(Decimal(42.6))
        else:
            self.assertRaises(TypeError, c_int, Decimal(42.6))


    def test_conversions_c_char(self):
        # normal case (c_char is unsigned)
        c_char_value = c_char(42)
        self.assertEqual(c_char_value.value, b"*")

        c_char_value = c_char(b"*")
        self.assertEqual(c_char_value.value, b"*")

        # bool case
        c_cbyte_value = c_char(True)
        self.assertEqual(c_cbyte_value.value, b"\x01")

        # BigInteger case
        c_cbyte_value = c_char(big(42))
        self.assertEqual(c_cbyte_value.value, b"*")

        # out of range int not supported
        self.assertRaises(TypeError, c_char, 256)
        self.assertRaises(TypeError, c_char, -1)
        with self.assertRaises(TypeError):
            c_char_value.value = 256
        with self.assertRaises(TypeError):
            c_char_value.value = -1

        # longer bytes not supported
        self.assertRaises(TypeError, c_char, b"abc")
        with self.assertRaises(TypeError):
            c_char_value.value = b"abc"

        # __int__ not supported
        self.assertRaises(TypeError, c_char, MyInt(42))
        with self.assertRaises(TypeError):
            c_char_value.value = MyInt(42)

        # __index__ not supported
        self.assertRaises(TypeError, c_char, MyIndex(42))
        with self.assertRaises(TypeError):
            c_char_value.value = MyIndex(42)

        # str not supported
        self.assertRaises(TypeError, c_char, "a")
        with self.assertRaises(TypeError):
            c_char_value.value = "a"

        # float not supported
        self.assertRaises(TypeError, c_char, 42.6)
        with self.assertRaises(TypeError):
            c_char_value.value = 42.6


    def test_conversions_overflow(self):
        # Overflow is clipped to lowest bits
        c_int_value = c_int((42 << 60) + 24)
        self.assertEqual(c_int_value.value, 24)
        c_int_value.value = (42 << 60) + 12
        self.assertEqual(c_int_value.value, 12)

        c_int_value = c_int((-42 << 60) - 24)
        self.assertEqual(c_int_value.value, -24)

        c_longlong_value = c_longlong((42 << 80) + 24)
        self.assertEqual(c_longlong_value.value, 24)
        c_longlong_value.value = (42 << 80) + 12
        self.assertEqual(c_longlong_value.value, 12)

        c_short_value = c_short((42 << 20) + 24)
        self.assertEqual(c_short_value.value, 24)
        c_short_value.value = (42 << 20) + 12
        self.assertEqual(c_short_value.value, 12)
        c_short_value.value = 32768
        self.assertEqual(c_short_value.value, -32768)
        c_short_value.value = 32769
        self.assertEqual(c_short_value.value, -32767)

        c_byte_value = c_byte((42 << 10) + 4)
        self.assertEqual(c_byte_value.value, 4)
        c_byte_value.value = (42 << 10) + 2
        self.assertEqual(c_byte_value.value, 2)
        c_byte_value.value = 128
        self.assertEqual(c_byte_value.value, -128)
        c_byte_value.value = 129
        self.assertEqual(c_byte_value.value, -127)


run_test(__name__)
