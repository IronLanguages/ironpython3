# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

'''
Tests for CPython's ctypes module.
'''

import _ctypes
from ctypes import *
from array import array
from struct import calcsize
import sys
import gc
import unittest
from decimal import Decimal

from iptest import IronPythonTestCase, is_posix, is_cli, is_32, is_mono, is_netcoreapp, big, myint, run_test

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

    def check_bitfield(self, bitfield, fieldtype, offset, bitoffset, bitwidth):
        self.assertEqual(repr(bitfield), "<Field type={}, ofs={}:{}, bits={}>".format(fieldtype.__name__, offset, bitoffset, bitwidth))
        self.assertEqual(bitfield.offset, offset)
        self.assertEqual(bitfield.size & 0xffff, bitoffset)
        self.assertEqual(bitfield.size >> 16, bitwidth)


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

        for val in (None, "", "False", "True", "0", "1", 0, 1, 2, 0.0, 1.0, [], (), {}, object(), MyIndex(0), MyIndex(1)):
            with self.subTest(val=val):
                self.assertIs(Test(val).x, bool(val))

                s = Test()
                s.x = val
                self.assertIs(s.x, bool(val))

    def test_bitfield_int(self):
        """Tests for bitfields of type c_int"""

        class Test(Structure):
            _fields_ = [("x", c_int, 16), ("y", c_int, 16), ("z", c_int, 32)]

        self.assertEqual(Test(0x1234).x, 0x1234)
        self.assertEqual(Test(big(0x1234)).x, 0x1234)
        self.assertEqual(Test(myint(0x1234)).x, 0x1234)
        self.assertEqual(Test(True).x, 1)
        if is_cli or sys.version_info >= (3, 8):
            self.assertEqual(Test(MyIndex(0x1234)).x, 0x1234)
            if is_cli or sys.version_info >= (3, 10):
                msg = "'float' object cannot be interpreted as an integer"
            else:
                msg = "int expected instead of float"
            self.assertRaisesMessage(TypeError, msg, Test, 2.3)

        with self.assertRaisesMessage(ValueError, "number of bits invalid for bit field"):
            class Test(Structure):
                _fields_ = [("x", c_int, 0)]
        # if c_long and c_int are the same size, c_long is used
        typename = "c_long" if calcsize('l') == calcsize('i') else "c_int"
        self.assertEqual(repr(Test.x), "<Field type={}, ofs=0:0, bits=16>".format(typename))
        self.assertEqual(repr(Test.y), "<Field type={}, ofs=0:16, bits=16>".format(typename))
        self.assertEqual(repr(Test.z), "<Field type={}, ofs=4:0, bits=32>".format(typename))

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


    @unittest.skipIf(is_32 and is_posix, "assumes 64-bit long on POSIX")
    def test_bitfield_mixed_B(self):
        """
        struct B   // GCC: 8, MSVC: 24
        {
            long long a : 3;        // GCC, MSVC: 0 (0:0)
            int b : 4;              // GCC: 3 (0:3) (fits in the same container as a)
                                    // MSVC: 64 (8:0) (different type than a)
            unsigned int c : 1;     // GCC: 7 (0:7) (fits in the same container as a)
                                    // MSVC: 68 (8:4) (different type than b but same size and alignment)
            long d : 5;             // GCC: 8 (1:0) (fits in the same container as a)
                                    // MSVC: 69 (8:5) (different type than c, but same size and alignment)
            long long e : 5;        // GCC: 13 (1:5) (fits in the same container as a)
                                    // MSVC: 128 (16:0) (different type than d)
            long long f : 1;        // GCC: 18 (2:2) (fits in the same container as a)
                                    // MSVC: 133 (16:5) (fits in the same container as e)
            ssize_t g : 2;          // GCC: 19 (2:3) (fits in the same container as a)
                                    // MSVC: 134 (16:6) (equivalent type, fits in the same container as e)
        };
        """
        class Test(Structure):
            _fields_ = [
                ("a", c_longlong, 3),
                ("b", c_int, 4),
                ("c", c_uint, 1),
                ("d", c_long, 5),
                ("e", c_longlong, 5),
                ("f", c_longlong, 1),
                ("g", c_ssize_t, 2),
            ]

        self.check_bitfield(Test.a, c_longlong, 0, 0, 3)
        if is_posix:
            if is_cli:  # GCC results
                self.check_bitfield(Test.b, c_int, 0, 3, 4)
                self.check_bitfield(Test.c, c_uint, 0, 7, 1)
            else:  # bug in CPython
                self.check_bitfield(Test.b, c_int, 4, 3, 4)
                self.check_bitfield(Test.c, c_uint, 4, 7, 1)
            self.check_bitfield(Test.d, c_long, 0, 8, 5)
            self.check_bitfield(Test.e, c_longlong, 0, 13, 5)
            self.check_bitfield(Test.f, c_longlong, 0, 18, 1)
            self.check_bitfield(Test.g, c_ssize_t, 0, 19, 2)
        else:
            self.check_bitfield(Test.b, c_int, 8, 0, 4)
            self.check_bitfield(Test.c, c_uint, 8, 4, 1)
            self.check_bitfield(Test.d, c_long, 8, 5, 5)
            self.check_bitfield(Test.e, c_longlong, 16, 0, 5)
            self.check_bitfield(Test.f, c_longlong, 16, 5, 1)
            self.check_bitfield(Test.g, c_ssize_t, 16, 6, 2)


    def test_bitfield_mixed_C(self):
        """
        struct C   // GCC: 8, MSVC: 8
        {
            int x;
            wchar_t a : 2;          // GCC, MSVC: 32 (4:0)
            unsigned short b : 3;   // GCC: 34 (4:2) (fits in the same container as a)
                                    // MSVC: 34 (4:2) (equivalent type, fits in the same container as a)
            wchar_t c : 1;          // GCC: 37 (4:5) (fits in the same container as a)
                                    // MSVC: 37 (4:5) (equivalent type, fits in the same container as a)
            unsigned short d : 5;   // GCC: 38 (4:6) (fits in the same container as a)
                                    // MSVC: 38 (4:6) (equivalent type, fits in the same container as a)
        };
        """
        class Test(Structure):
            _fields_ = [
                ("x", c_int),
                ("a", c_short, 2),
                ("b", c_ushort, 3),
                ("c", c_short, 1),
                ("d", c_ushort, 5),
            ]

        self.check_bitfield(Test.a, c_short, 4, 0, 2)
        self.check_bitfield(Test.b, c_ushort, 4, 2, 3)
        self.check_bitfield(Test.c, c_short, 4, 5, 1)
        self.check_bitfield(Test.d, c_ushort, 4, 6, 5)


    @unittest.skipIf(is_32 and is_posix, "assumes 64-bit long on POSIX")
    def test_bitfield_mixed_D1(self):
        """
        struct D1  // GCC: 8, MSVC: 8
        {
            long a : 3;     // GCC, MSVC: 0 (0:0)
            int b : 30;     // GCC: 32 (4:0) (doesn't fit in the same container as a)
                            // MSVC: 32 (4:0) (same type but doesn't fit in the same container as a)
            long c : 2;     // GCC: 62 (7:6) (fits in the same container as a)
                            // MSVC: 62 (7:6) (fits in the same container as b)
        };
        """
        class Test(Structure):
            _fields_ = [
                ("a", c_long, 3),
                ("b", c_int, 30),
                ("c", c_long, 2),
            ]

        self.check_bitfield(Test.a, c_long, 0, 0, 3)
        if is_cli:  # GCC results
            self.check_bitfield(Test.b, c_int, 4, 0, 30)
        else: # bug in CPython
            self.check_bitfield(Test.b, c_int, 4, 3, 30)
        if is_posix:
            if is_cli:  # GCC results
                self.check_bitfield(Test.c, c_long, 0, 62, 2)
            else: # bug in CPython
                self.check_bitfield(Test.c, c_long, 0, 33, 2)
        else:
            self.check_bitfield(Test.c, c_long, 4, 30, 2)


    def test_bitfield_mixed_D2(self):
        """
        struct D2  // GCC: 16, MSVC: 24
        {
            long long a : 3;    // GCC, MSVC: 0 (0:0)
            int b : 32;         // GCC: 32 (4:0) (fits in the same container as a, padded to satisfy alignment)
                                // MSVC: 64 (8:0) (different type than a)
            long long c : 2;    // GCC: 64 (8:0) (doesn't fit in the same container as b)
                                // MSVC: 128 (16:0) (different type than b)
        };
        """
        class Test(Structure):
            _fields_ = [
                ("a", c_longlong, 3),
                ("b", c_int, 32),
                ("c", c_longlong, 2),
            ]

        self.check_bitfield(Test.a, c_longlong, 0, 0, 3)
        if is_posix:
            if is_cli:  # GCC results
                self.check_bitfield(Test.b, c_int, 4, 0, 32)
                self.check_bitfield(Test.c, c_longlong, 8, 0, 2)
            else: # bug in CPython
                self.check_bitfield(Test.b, c_int, 4, 3, 32)
                self.check_bitfield(Test.c, c_longlong, 0, 35, 2)
        else:
            self.check_bitfield(Test.b, c_int, 8, 0, 32)
            self.check_bitfield(Test.c, c_longlong, 16, 0, 2)


    def test_bitfield_mixed_D3(self):
        """
        struct D3  // GCC: 8, MSVC: 16
        {
            char x;
            char a : 3;         // GCC: 8 (1:0)
                                // MSVC: 8 (1:0)
            short b : 4;        // GCC: 11 (1:3) (fits in the same container as a)
                                // MSVC: 16 (2:0) (different type than a)
            long long c : 2;    // GCC: 15 (1:7) (fits in the same container as a and b)
                                // MSVC: 64 (8:0) (different type than b)
        };
        """
        class Test(Structure):
            _fields_ = [
                ("x", c_char),
                ("a", c_byte, 3),
                ("b", c_short, 4),
                ("c", c_longlong, 2),
            ]

        self.check_bitfield(Test.a, c_byte, 1, 0, 3)
        if is_posix:
            if is_cli:  # GCC results
                self.check_bitfield(Test.b, c_short, 0, 11, 4)
                self.check_bitfield(Test.c, c_longlong, 0, 15, 2)
            else: # bug in CPython
                self.check_bitfield(Test.b, c_short, 1, 3, 4)
                self.check_bitfield(Test.c, c_longlong, 1, 7, 2)
        else:
            self.check_bitfield(Test.b, c_short, 2, 0, 4)
            self.check_bitfield(Test.c, c_longlong, 8, 0, 2)


    def test_bitfield_mixed_E(self):
        """
        struct E  // GCC: 8, MSVC: 16
        {
            long long a : 20;   // GCC, MSVC: 0 (0:0)
            short b : 2;        // GCC: 20 (2:4) (fits in the same container as a)
                                // MSVC: 64 (8:0) (different type than a)
            short c : 15;       // GCC: 32 (4:0) (doesn't fit in the same container as b)
                                // MSVC: 80 (10:0) (doesn't fit in the same container as b)
        };
        """
        class Test(Structure):
            _fields_ = [
                ("a", c_longlong, 20),
                ("b", c_short, 2),
                ("c", c_short, 15),
            ]

        self.check_bitfield(Test.a, c_longlong, 0, 0, 20)
        if is_posix:
            if is_cli:  # GCC results
                self.check_bitfield(Test.b, c_short, 2, 4, 2)
                self.check_bitfield(Test.c, c_short, 4, 0, 15)
            else: # bug in CPython
                self.check_bitfield(Test.b, c_short, 6, 20, 2)
                self.check_bitfield(Test.c, c_short, 6, 22, 15)
        else:
            self.check_bitfield(Test.b, c_short, 8, 0, 2)
            self.check_bitfield(Test.c, c_short, 10, 0, 15)


    @unittest.skipIf(is_cli, "TODO: NotImplementedError: pack with bitfields")
    def test_bitfield_mixed_E_packed(self):
        """
        // same as E but packed along 1 byte
        #pragma pack(push, 1)
        struct E_packed  // GCC: 5, MSVC: 12
        {
            long long a : 20;   // GCC, MSVC: 0 (0:0)
            short b : 2;        // GCC: 20 (2:4) (fits in the same container as a)
                                // MSVC: 64 (8:0) (different type than a)
            short c : 15;       // GCC: 22 (2:6) (straddles alignment boundary for `short`)
                                // MSVC: 80 (10:0) (doesn't fit in the same container as b)
        };
        #pragma pack(pop)
        """
        class Test(Structure):
            _pack_ = 1
            _fields_ = [
                ("a", c_longlong, 20),
                ("b", c_short, 2),
                ("c", c_short, 15),
            ]

        self.check_bitfield(Test.a, c_longlong, 0, 0, 20)
        if is_posix:
            if is_cli: # GCC results
                self.check_bitfield(Test.b, c_short, 2, 4, 2)
                self.check_bitfield(Test.c, c_short, 2, 6, 15)
            else: # bug in CPython
                self.check_bitfield(Test.b, c_short, 6, 20, 2)
                self.check_bitfield(Test.c, c_short, 6, 22, 15)
        else:
            self.check_bitfield(Test.b, c_short, 8, 0, 2)
            self.check_bitfield(Test.c, c_short, 10, 0, 15)


    def test_bitfield_mixed_F1(self):
        """
        struct F1  // GCC: 16, MSVC: 24
        {
            long long a : 3;    // GCC, MSVC: 0 (0:0)
            int b : 31;         // GCC: 32 (4:0) (fits in the same container as a, padded to satisfy alignment)
                                // MSVC: 64 (8:0) (different type than a)
            long long c : 3;    // GCC: 64 (8:0) (doesn't fit in the same container as b)
                                // MSVC: 128 (16:0) (different type than b)
        };
        """
        class Test(Structure):
            _fields_ = [
                ("a", c_longlong, 3),
                ("b", c_int, 31),
                ("c", c_longlong, 3),
            ]

        self.check_bitfield(Test.a, c_longlong, 0, 0, 3)
        if is_posix:
            if is_cli:  # GCC results
                self.check_bitfield(Test.b, c_int, 4, 0, 31)
                self.check_bitfield(Test.c, c_longlong, 8, 0, 3)
            else: # bug in CPython
                self.check_bitfield(Test.b, c_int, 4, 3, 31)
                self.check_bitfield(Test.c, c_longlong, 0, 34, 3)
        else:
            self.check_bitfield(Test.b, c_int, 8, 0, 31)
            self.check_bitfield(Test.c, c_longlong, 16, 0, 3)


    def test_bitfield_mixed_F2(self):
        """
        struct F2  // GCC: 8, MSVC: 24
        {
            long long a : 3;    // GCC, MSVC: 0 (0:0)
            int b : 29;         // GCC: 3 (0:3) (fits in the same container as a)
                                // MSVC: 64 (8:0) (different type than a)
            long long c : 3;    // GCC: 32 (4:0) (doesn't fit in the same container as b, alignment 4)
                                // MSVC: 128 (16:0) (different type than b)
        };
        """
        class Test(Structure):
            _fields_ = [
                ("a", c_longlong, 3),
                ("b", c_int, 29),
                ("c", c_longlong, 3),
            ]

        self.check_bitfield(Test.a, c_longlong, 0, 0, 3)
        if is_posix:
            if is_cli:  # GCC results
                self.check_bitfield(Test.b, c_int, 0, 3, 29)
            else: # bug in CPython
                self.check_bitfield(Test.b, c_int, 4, 3, 29)
            self.check_bitfield(Test.c, c_longlong, 0, 32, 3)
        else:
            self.check_bitfield(Test.b, c_int, 8, 0, 29)
            self.check_bitfield(Test.c, c_longlong, 16, 0, 3)


    def test_bitfield_mixed_F3(self):
        """
        struct F3  // GCC: 8, MSVC: 24
        {
            long long a : 4; 	// GCC, MSVC: 0 (0:0)
            int b : 29;      	// GCC: 32 (4:0) (doesn't fit in the same container as a)
                                // MSVC: 64 (8:0) (different type than a)
            long long c : 3; 	// GCC: 61 (7:5) (fits in the same container as b)
                                // MSVC: 128 (16:0) (different type than b)
        };
        """
        class Test(Structure):
            _fields_ = [
                ("a", c_longlong, 4),
                ("b", c_int, 29),
                ("c", c_longlong, 3),
            ]

        self.check_bitfield(Test.a, c_longlong, 0, 0, 4)
        if is_posix:
            if is_cli:
                self.check_bitfield(Test.b, c_int, 4, 0, 29)
                self.check_bitfield(Test.c, c_longlong, 0, 61, 3)
            else:  # bug in CPython
                self.check_bitfield(Test.b, c_int, 4, 4, 29)
                self.check_bitfield(Test.c, c_longlong, 0, 33, 3)
        else:
            self.check_bitfield(Test.b, c_int, 8, 0, 29)
            self.check_bitfield(Test.c, c_longlong, 16, 0, 3)


    def test_bitfield_mixed_F4(self):
        class Test(Structure):
            _fields_ = [
                ("a", c_int),
                ("b1", c_short, 3),
                ("b2", c_short, 3),
                ("c", c_int, 3),
            ]

        self.assertEqual(Test.a.offset, 0)
        self.assertEqual(Test.a.size, 4)

        self.check_bitfield(Test.b1, c_short, 4, 0, 3)
        self.check_bitfield(Test.b2, c_short, 4, 3, 3)
        if is_posix:
            self.check_bitfield(Test.c, c_int, 4, 6, 3)
        else:
            self.check_bitfield(Test.c, c_int, 8, 0, 3)

        instance = Test()
        self.assertTrue(isinstance(instance.a, int))
        instance.a = 1
        instance.b1 = 5  # equals -3 in 2-complement on 3 bits
        instance.b2 = 7  # equals -1 in 2-complement on 3 bits
        instance.c = 3
        self.assertEqual(instance.a, 1)
        self.assertEqual(instance.b1, -3)
        self.assertEqual(instance.b2, -1)
        self.assertEqual(instance.c, 3)


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
