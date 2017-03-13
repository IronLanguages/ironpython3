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

import unittest

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
        
if __name__ == '__main__':
    unittest.main()
