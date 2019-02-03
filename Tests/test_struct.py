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

        self.assertRaisesRegexp(struct.error, '^unpack requires', struct.unpack, "H", b"a")
        struct.unpack("H", b"aa")
        self.assertRaisesRegexp(struct.error, '^unpack requires', struct.unpack, "H", b"aaa")

run_test(__name__)
