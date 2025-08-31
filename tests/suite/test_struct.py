# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.
#
# Copyright (c) Jeffrey Bester
#

import array
import struct
import unittest

from iptest import run_test

class StructTest(unittest.TestCase):

    def test_pack(self):
        # test string format string
        result = struct.pack(">HH", 1, 2)
        self.assertSequenceEqual(result, b"\x00\x01\x00\x02")

        # test bytes format string
        result = struct.pack(b">HH", 1, 2)
        self.assertSequenceEqual(result, b"\x00\x01\x00\x02")

    def test_unpack(self):
        # test bytes/string combination
        a,b = struct.unpack(b">HH", b"\x00\x01\x00\x02")
        self.assertEqual(a, 1)
        self.assertEqual(b, 2)

        # test string/bytes combination
        a,b = struct.unpack(">HH", b"\x00\x01\x00\x02")
        self.assertEqual(a, 1)
        self.assertEqual(b, 2)

    def test_unpack_from(self):
        # test string format string
        a, = struct.unpack_from('>H', b"\x00\x01")
        self.assertEqual(a, 1)

        # test bytes format string
        a, = struct.unpack_from(b'>H', b"\x00\x01")
        self.assertEqual(a, 1)

    def test_pack_into(self):
        # test string format string
        result = array.array('b', [0, 0])
        struct.pack_into('>H', result, 0, 0xABCD)
        self.assertSequenceEqual(result, array.array('b', b"\xAB\xCD"))

        # test bytes format string
        result = array.array('b', [0, 0])
        struct.pack_into(b'>H', result, 0, 0xABCD)
        self.assertSequenceEqual(result, array.array('b', b"\xAB\xCD"))

        # test bytearray
        result = bytearray(b'\x00\x00')
        struct.pack_into('>H', result, 0, 0xABCD)
        self.assertSequenceEqual(result, bytearray(b"\xAB\xCD"))

    def test_ipy2_gh407(self):
        """https://github.com/IronLanguages/ironpython2/issues/407"""

        self.assertRaisesRegex(struct.error, '^unpack requires', struct.unpack, "H", b"a")
        struct.unpack("H", b"aa")
        self.assertRaisesRegex(struct.error, '^unpack requires', struct.unpack, "H", b"aaa")

    def test_nN_code(self):
        """Copied as-is from AllCPython/test_struct.py because that test is currently ignored."""
        # n and N don't exist in standard sizes
        def assertStructError(func, *args, **kwargs):
            with self.assertRaises(struct.error) as cm:
                func(*args, **kwargs)
            self.assertIn("bad char in struct format", str(cm.exception))
        for code in 'nN':
            for byteorder in ('=', '<', '>', '!'):
                format = byteorder+code
                assertStructError(struct.calcsize, format)
                assertStructError(struct.pack, format, 0)
                assertStructError(struct.unpack, format, b"")

    def test_iter_unpack(self):
        import operator

        packed = struct.pack('hlhlhl', 1, 2, 3, 4, 5, 6)
        it = struct.iter_unpack('hl', packed)

        self.assertEqual(operator.length_hint(it), 3)
        self.assertEqual(it.__next__(), (1, 2))
        self.assertEqual(operator.length_hint(it), 2)
        self.assertEqual(it.__next__(), (3, 4))
        self.assertEqual(operator.length_hint(it), 1)
        self.assertEqual(it.__next__(), (5, 6))
        self.assertEqual(operator.length_hint(it), 0)
        self.assertRaises(StopIteration, next, it)

        # struct.error: iterative unpacking requires a buffer of a multiple of {N} bytes
        self.assertRaises(struct.error, struct.iter_unpack, "h", b"\0")


    def test_sizes(self):
        # test sizes of standard struct types
        for mode in ('<', '>', '=', '!'):
            self.assertEqual(struct.calcsize(mode + 'b'), 1)
            self.assertEqual(struct.calcsize(mode + 'B'), 1)
            self.assertEqual(struct.calcsize(mode + 'h'), 2)
            self.assertEqual(struct.calcsize(mode + 'H'), 2)
            self.assertEqual(struct.calcsize(mode + 'i'), 4)
            self.assertEqual(struct.calcsize(mode + 'I'), 4)
            self.assertEqual(struct.calcsize(mode + 'l'), 4)
            self.assertEqual(struct.calcsize(mode + 'L'), 4)
            self.assertEqual(struct.calcsize(mode + 'q'), 8)
            self.assertEqual(struct.calcsize(mode + 'Q'), 8)
            self.assertEqual(struct.calcsize(mode + 'f'), 4)
            self.assertEqual(struct.calcsize(mode + 'd'), 8)


run_test(__name__)
