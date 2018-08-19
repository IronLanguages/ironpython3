# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import unittest

from iptest import run_test

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
        
run_test(__name__)
