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

from iptest import run_test

class ListCompTest(unittest.TestCase):
    def test_positive(self):
        self.assertEqual([x for x in ""], [])
        self.assertEqual([x for x in range(2)], [0, 1])
        self.assertEqual([x + 10 for x in [-11, 4]], [-1, 14])
        self.assertEqual([x for x in [y for y in range(3)]], [0, 1, 2])
        self.assertEqual([x for x in range(3) if x > 1], [2])
        self.assertEqual([x for x in range(10) if x > 1 if x < 4], [2, 3])
        self.assertEqual([x for x in range(30) for y in range(2) if x > 1 if x < 4], [2, 2, 3, 3])
        self.assertEqual([(x,y) for x in range(30) for y in range(3) if x > 1 if x < 4 if y > 1], [(2, 2), (3, 2)])
        self.assertEqual([(x,y) for x in range(30) if x > 1 for y in range(3) if x < 4 if y > 1], [(2, 2), (3, 2)])
        self.assertEqual([(x,y) for x in range(30) if x > 1 if x < 4 for y in range(3) if y > 1], [(2, 2), (3, 2)])
        self.assertEqual([(x,y) for x in range(30) if x > 1 for y in range(5) if x < 4 if y > x], [(2, 3), (2, 4), (3, 4)])
        self.assertEqual([(x,y) for x in range(30) if x > 1 for y in range(5) if y > x if x < 4], [(2, 3), (2, 4), (3, 4)])
        self.assertEqual([(y, x) for (x, y) in ((1, 2), (2, 4))], [(2, 1), (4, 2)])
        y = 10
        self.assertEqual([y for x in "python"], [y] * 6)
        self.assertEqual([y for y in "python"], list("python"))
        y = 10
        self.assertEqual([x for x in "python" if y > 5], list("python"))
        self.assertEqual([x for x in "python" if y > 15], list())
        self.assertEqual([x for x, in [(1,)]], [1])

    def test_negative(self):
        self.assertRaises(SyntaxError, compile, "[x if x > 1 for x in range(3)]", "", "eval")
        self.assertRaises(SyntaxError, compile, "[x for x in range(3);]", "", "eval")
        self.assertRaises(SyntaxError, compile, "[x for x in range(3) for y]", "", "eval")

        self.assertRaises(NameError, lambda: [z for x in "python"])
        self.assertRaises(NameError, lambda: [x for x in "python" if z > 5])
        self.assertRaises(NameError, lambda: [x for x in "iron" if z > x for z in "python" ])
        self.assertRaises(NameError, lambda: [x for x in "iron" if never_shown_before > x ])
        self.assertRaises(NameError, lambda: [(x, z) for x in "iron" if z > x for z in "python" ])
        self.assertRaises(NameError, lambda: [(i, j) for i in range(10) if j < 'c' for j in ['a', 'b', 'c'] if i % 3 == 0])

run_test(__name__)
