# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import array
import itertools
import unittest

from iptest import is_cli, run_test

class SliceTests(unittest.TestCase):
    def testGet(self):
        m = memoryview(bytearray(range(5)))
        self.assertEqual(bytearray([3, 4]), m[3:5].tobytes())
        
    def testGetNested(self):
        m = memoryview(bytearray(range(5)))
        self.assertEqual(bytearray([2, 3]), m[1:-1][1:3].tobytes())

    def testUpdate(self):
        b = bytearray(range(4))
        m = memoryview(b)
        m[1:3] = [11, 22]
        self.assertEqual(bytearray([0,11,22,3]), b)

    def testUpdateNested(self):
        b = bytearray(range(4))
        m = memoryview(b)
        m[1:][1:3] = [11, 22]
        self.assertEqual(bytearray([0,1,11,22]), b)

class TestGH1387(unittest.TestCase):
    # from https://github.com/IronLanguages/main/issues/1387
    data = bytearray(range(10))
    mview = memoryview(data)

    def testMemoryViewEmptySliceData(self):
        chunk = self.mview[-1:2]
        chunk_data = bytearray(chunk)
        self.assertEqual(chunk_data, bytearray(0))

    def testMemoryViewEmptySliceLength(self):
        chunk = self.mview[-1:2]
        self.assertEqual(len(chunk), 0)

    def testMemoryViewTruncatedSliceData(self):
        chunk = self.mview[8:12]
        chunk_data = bytearray(chunk)
        self.assertEqual(chunk_data, bytearray([8,9]))

    def testMemoryViewTruncatedSliceLength(self):
        chunk = self.mview[8:12]
        self.assertEqual(len(chunk), 2)

class MemoryViewTests(unittest.TestCase):
    def test_init(self):
        x = memoryview(b"abc")
        self.assertEqual(x, b"abc")

        y = memoryview(x)
        self.assertEqual(x, y)
        self.assertIsNot(x, y)

    def test_equality(self):
        b = b"aaa"
        ba = bytearray(b)
        mv = memoryview(b)
        a = array.array("b", b)

        # check __eq__
        # DO NOT USE assertTrue SINCE IT DOES NOT FAIL ON NotImplemented
        self.assertEqual(b.__eq__(b), True)
        self.assertEqual(b.__eq__(ba), NotImplemented)
        self.assertEqual(b.__eq__(mv), NotImplemented)
        self.assertEqual(b.__eq__(a), NotImplemented)
        self.assertEqual(ba.__eq__(b), True)
        self.assertEqual(ba.__eq__(ba), True)
        self.assertEqual(ba.__eq__(mv), True)
        self.assertEqual(ba.__eq__(a), True)
        self.assertEqual(mv.__eq__(b), True)
        self.assertEqual(mv.__eq__(ba), True)
        self.assertEqual(mv.__eq__(mv), True)
        self.assertEqual(mv.__eq__(a), True)
        self.assertEqual(a.__eq__(b), NotImplemented)
        self.assertEqual(a.__eq__(ba), NotImplemented)
        self.assertEqual(a.__eq__(mv), NotImplemented)
        self.assertEqual(a.__eq__(a), True)

        # check that equality works for all combinations
        for x, y in itertools.product((b, ba, mv), repeat=2):
            self.assertTrue(x == y, "{} {}".format(x, y))

        for x, y in itertools.product((a, mv), repeat=2):
            self.assertTrue(x == y, "{} {}".format(x, y))

        # check __ne__
        self.assertFalse(b.__ne__(b))
        self.assertEqual(b.__ne__(ba), NotImplemented)
        self.assertEqual(b.__ne__(mv), NotImplemented)
        self.assertEqual(b.__ne__(a), NotImplemented)
        self.assertFalse(ba.__ne__(b))
        self.assertFalse(ba.__ne__(ba))
        self.assertFalse(ba.__ne__(mv))
        self.assertFalse(mv.__ne__(b))
        self.assertFalse(mv.__ne__(ba))
        self.assertFalse(mv.__ne__(mv))
        self.assertFalse(mv.__ne__(a))
        self.assertEqual(a.__ne__(b), NotImplemented)
        self.assertEqual(a.__ne__(ba), NotImplemented)
        self.assertEqual(a.__ne__(mv), NotImplemented)
        self.assertFalse(a.__ne__(a))

        # check that equality works for all combinations
        for x, y in itertools.product((b, ba, mv), repeat=2):
            self.assertFalse(x != y, "{} {}".format(x, y))

        for x, y in itertools.product((a, mv), repeat=2):
            self.assertFalse(x != y, "{} {}".format(x, y))

run_test(__name__)
