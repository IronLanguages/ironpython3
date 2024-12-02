# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import sys

from iptest import IronPythonTestCase, is_cli, is_net70, is_net80, is_netstandard, is_mono, big, myint, skipUnlessIronPython, run_test

class IntNoClrTest(IronPythonTestCase):
    """Must be run before IntTest because it depends on CLR API not being visible."""

    def test_instance_set(self):
        i = 1
        j = big(1)
        self.assertSetEqual(set(dir(i)), set(dir(j)))

class IntTest(IronPythonTestCase):
    def test_instance_set(self):
        i = 1
        j = big(1)
        from System import Int32

        if not is_mono:
            self.assertSetEqual(set(dir(j)) - set(dir(i)), set())
            self.assertSetEqual(set(dir(int)) - set(dir(Int32)), set())
        else:
            self.assertSetEqual(set(dir(j)) - set(dir(i)), {'GetByteCount', 'TryWriteBytes'})
            self.assertSetEqual(set(dir(int)) - set(dir(Int32)), {'GetByteCount', 'TryWriteBytes'})

        if is_net70 or is_net80: # https://github.com/IronLanguages/ironpython3/issues/1485
            diff = {'TryConvertToChecked', 'MinValue', 'TryConvertFromChecked', 'MaxValue', 'TryConvertFromTruncating', 'WriteBigEndian', 'GetShortestBitLength', 'TryWriteLittleEndian', 'WriteLittleEndian', 'TryConvertToSaturating', 'TryConvertFromSaturating', 'TryWriteBigEndian', 'TryConvertToTruncating'}
            self.assertSetEqual(set(dir(i)) - set(dir(j)), diff)
            self.assertSetEqual(set(dir(Int32)) - set(dir(int)), diff)
        else:
            # these two assertions fail on IronPython compiled for .NET Standard
            if not is_netstandard:
                self.assertSetEqual(set(dir(i)) - set(dir(j)), {'MaxValue', 'MinValue'})
                self.assertSetEqual(set(dir(Int32)) - set(dir(int)), {'MaxValue', 'MinValue'})

            # weaker assertions that should always hold
            self.assertTrue((set(dir(i)) - set(dir(j))).issubset({'MaxValue', 'MinValue', 'GetByteCount', 'TryWriteBytes', 'GetBitLength'}))
            self.assertTrue((set(dir(Int32)) - set(dir(int))).issubset({'MaxValue', 'MinValue', 'GetByteCount', 'TryWriteBytes', 'GetBitLength'}))

    def test_from_bytes(self):
        self.assertEqual(type(int.from_bytes(b"abc", "big")), int)
        self.assertEqual(type(myint.from_bytes(b"abc", "big")), myint) # https://github.com/IronLanguages/ironpython3/pull/973

    def test_to_bytes_bigint(self):
        self.assertEqual((0x01<<64).to_bytes(9, 'little', signed=False), b'\x00\x00\x00\x00\x00\x00\x00\x00\x01')
        self.assertEqual((0xff<<64).to_bytes(9, 'little', signed=False), b'\x00\x00\x00\x00\x00\x00\x00\x00\xff')
        self.assertEqual((0x01<<64).to_bytes(9, 'big', signed=False), b'\x01\x00\x00\x00\x00\x00\x00\x00\x00')
        self.assertEqual((0xff<<64).to_bytes(9, 'big', signed=False), b'\xff\x00\x00\x00\x00\x00\x00\x00\x00')

        self.assertEqual((-0x01<<64).to_bytes(9, 'little', signed=True), b'\x00\x00\x00\x00\x00\x00\x00\x00\xff')
        self.assertEqual((-0x80<<64).to_bytes(9, 'little', signed=True), b'\x00\x00\x00\x00\x00\x00\x00\x00\x80')
        self.assertEqual((-0x01<<64).to_bytes(9, 'big', signed=True), b'\xff\x00\x00\x00\x00\x00\x00\x00\x00')
        self.assertEqual((-0x80<<64).to_bytes(9, 'big', signed=True), b'\x80\x00\x00\x00\x00\x00\x00\x00\x00')

        self.assertRaisesMessage(OverflowError, "int too big to convert", (0x01<<64).to_bytes, 8, 'little', signed=False)
        self.assertRaisesMessage(OverflowError, "int too big to convert", (0xFF<<64).to_bytes, 8, 'little', signed=False)
        self.assertRaisesMessage(OverflowError, "int too big to convert", (0x01<<63).to_bytes, 8, 'little', signed=True)
        self.assertRaisesMessage(OverflowError, "int too big to convert", (0xFF<<64).to_bytes, 9, 'little', signed=True)
        self.assertRaisesMessage(OverflowError, "int too big to convert", (-0x01<<64).to_bytes, 8, 'little', signed=True)
        self.assertRaisesMessage(OverflowError, "int too big to convert", (-0x80<<64).to_bytes, 8, 'little', signed=True)

        self.assertRaisesMessage(ValueError, "byteorder must be either 'little' or 'big'", (-1<<64).to_bytes, -1, 'medium')
        self.assertRaisesMessage(ValueError, "length argument must be non-negative", (-1<<64).to_bytes, -1, 'little')
        self.assertRaisesMessage(OverflowError, "can't convert negative int to unsigned", (-1<<64).to_bytes, 0, 'little')

    @skipUnlessIronPython()
    def test_to_bytes_clrint(self):
        from iptest import clr_int_types

        for t in clr_int_types:
            for v in (0, 1, -1, 2, -2, 127, -128, 128, -129, 255, 256, -256, 257, -257,
                        2**15-1, -2**15, 2**15, -2**15-1, 2**16-1, -2**16, 2**16, -2**16-1,
                        2**31-1, -2**31, 2**31, -2**31-1, 2**32-1, -2**32, 2**32, -2**32-1,
                        2**63-1, -2**63, 2**63, -2**63-1, 2**64-1):
                for byteorder in ('little', 'big'):
                    for signed in (True, False):
                        if signed and v < 0:
                            continue
                        if v < t.MinValue or v > t.MaxValue:
                            continue
                        for length in range(0, 10):
                            try:
                                expected = big(v).to_bytes(length, byteorder, signed)
                            except OverflowError:
                                pass # length too short
                            else:
                                actual = t(v).to_bytes(length, byteorder, signed)
                                self.assertEqual(actual, expected)

    def test_int(self):
        class MyTrunc:
            def __init__(self, x):
                self.x = x
            def __trunc__(self):
                return self.x

        class MyInt:
            def __init__(self, x):
                self.x = x
            def __int__(self):
                return self.x

        for value in ("0", b"0"):
            # int(str/bytes)
            v = int(value)
            self.assertEqual(v, 0)
            self.assertIs(type(v), int)

        for t in (bool, int, myint):
            # int()
            v = int(t(0))
            self.assertEqual(v, 0)
            self.assertIs(type(v), int)

            # __trunc__
            v = int(MyTrunc(t(0)))
            self.assertEqual(v, 0)
            self.assertIs(type(v), int if is_cli or sys.version_info >= (3,6) else t)

            # __int__
            if t in (bool, myint):
                with self.assertWarns(DeprecationWarning):
                    v = int(MyInt(t(0)))
            else:
                v = int(MyInt(t(0)))
            self.assertEqual(v, 0)
            self.assertIs(type(v), int if is_cli or sys.version_info >= (3,6) else t)

        if is_cli:
            from iptest import clr_all_types
            for t in clr_all_types:
                # int(System.*)
                v = int(t(0))
                self.assertEqual(v, 0)
                self.assertIs(type(v), int)

    @skipUnlessIronPython()
    def test_type_name(self):
        import System

        i = 1
        j = big(1)

        self.assertEqual(int.__name__, "int")
        self.assertEqual(System.Numerics.BigInteger.__name__, "int")
        self.assertEqual(System.Int32.__name__, "Int32")
        self.assertEqual(System.Int64.__name__, "Int64")

        self.assertEqual(i.__class__.__name__, "int")
        self.assertEqual(j.__class__.__name__, "int")

    def test_type_repr(self):
        i = 1
        j = big(1)

        self.assertEqual(repr(type(i)), repr(int))
        self.assertEqual(repr(type(j)), repr(int))
        self.assertEqual(repr(type(i)), repr(type(j)))

        self.assertEqual(str(type(i)), str(int))
        self.assertEqual(str(type(j)), str(int))
        self.assertEqual(str(type(i)), str(type(j)))

        self.assertEqual(str(i.__class__), "<class 'int'>")
        self.assertEqual(str(j.__class__), "<class 'int'>")

    def test_type_set(self):
        i = 1
        j = big(1)
        k = myint(1)

        self.assertSetEqual({int, type(i), type(j)}, {int})
        self.assertSetEqual({int, type(i), type(j), type(k)}, {int, myint})
        self.assertEqual(len({int, myint}), 2)

        self.assertSetEqual({i, j, k}, {i})
        self.assertSetEqual({i, j, k}, {j})
        self.assertSetEqual({i, j, k}, {k})
        self.assertSequenceEqual({i : 'i', j : 'j', k : 'k'}.keys(), {i})
        self.assertSequenceEqual({i : 'i', j : 'j', k : 'k'}.keys(), {j})
        self.assertSequenceEqual({i : 'i', j : 'j', k : 'k'}.keys(), {k})

run_test(__name__)
