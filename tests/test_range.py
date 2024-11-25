# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Test range
##
## * sbs_builtin\test_xrange covers many range corner cases
##

import sys

from iptest import IronPythonTestCase, is_cli, run_test

if is_cli:
    from System import Int64

class RangeTest(IronPythonTestCase):

    def test_range(self):
        self.assertTrue(list(range(10)) == [0, 1, 2, 3, 4, 5, 6, 7, 8, 9])
        self.assertTrue(list(range(0)) == [])
        self.assertTrue(list(range(-10)) == [])

        self.assertTrue(list(range(3,10)) == [3, 4, 5, 6, 7, 8, 9])
        self.assertTrue(list(range(10,3)) == [])
        self.assertTrue(list(range(-3,-10)) == [])
        self.assertTrue(list(range(-10,-3)) == [-10, -9, -8, -7, -6, -5, -4])

        self.assertTrue(list(range(3,20,2)) == [3, 5, 7, 9, 11, 13, 15, 17, 19])
        self.assertTrue(list(range(3,20,-2)) == [])
        self.assertTrue(list(range(20,3,2)) == [])
        self.assertTrue(list(range(20,3,-2)) == [20, 18, 16, 14, 12, 10, 8, 6, 4])
        self.assertTrue(list(range(-3,-20,2)) == [])
        self.assertTrue(list(range(-3,-20,-2)) == [-3, -5, -7, -9, -11, -13, -15, -17, -19])
        self.assertTrue(list(range(-20,-3, 2)) == [-20, -18, -16, -14, -12, -10, -8, -6, -4])
        self.assertTrue(list(range(-20,-3,-2)) == [])

    def test_range_collections(self):
        self.assertTrue(range(0, 100, 2).count(10) == 1)
        self.assertTrue(range(0, 100, 2).index(10) == 5)
        self.assertTrue(range(0, 100, 2)[5] == 10)
        self.assertTrue(str(range(0, 100, 2)[0:5]) == 'range(0, 10, 2)')

    def test_range_corner_cases(self):
        x = range(0, sys.maxsize, sys.maxsize-1)
        self.assertEqual(x[0], 0)
        self.assertEqual(x[1], sys.maxsize-1)
        self.assertEqual(len(x), 2)

        x = range(sys.maxsize, 0, -(sys.maxsize-1))
        self.assertEqual(x[0], sys.maxsize)
        self.assertEqual(x[1], 1)
        self.assertEqual(len(x), 2)

        if is_cli:
            x = range(0, Int64.MaxValue, Int64.MaxValue-1)
            self.assertEqual(x[0], 0)
            self.assertEqual(x[1], Int64.MaxValue-1)

            self.assertEqual(len(range(0, Int64.MaxValue, Int64.MaxValue-1)), 2)

            r = range(-Int64.MaxValue, Int64.MaxValue, 2)
            with self.assertRaises(OverflowError): # len protocol expects an Int32
                len(r)

    def test_range_coverage(self):
        ## ToString
        self.assertEqual(str(range(0, 3, 1)), "range(0, 3)")
        self.assertEqual(str(range(1, 3, 1)), "range(1, 3)")
        self.assertEqual(str(range(0, 5, 2)), "range(0, 5, 2)")

        ## Long
        self.assertEqual([x for x in range(5)], list(range(5)))
        self.assertEqual([x for x in range(10, 15)], list(range(10, 15)))
        self.assertEqual([x for x in range(10, 15, 2)], list(range(10, 15, 2)))

        ## Ops
        self.assertRaises(TypeError, lambda: range(4) + 4)
        self.assertRaises(TypeError, lambda: range(4) * 4)

    def test_range_equal(self):
         self.assertEqual(range(0, 3, 2), range(0, 4, 2))
         self.assertEqual(range(0), range(1, -1, 1))

run_test(__name__)
