# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import unittest

from iptest import long, run_test

class BigIntTest(unittest.TestCase):

    def axiom_helper(self, a, b):
        self.assertTrue((a // b) * b + (a % b) == a, "(" + str(a) + " // " + str(b) + ") * " + str(b) + " + (" + str(a) + " % " + str(b) + ") != " + str(a))

    def misc_helper(self, i, j, k):
        u = i * j + k
        self.axiom_helper(u, j)

    def test_axioms(self):
        a = -209681412991024529003047811046079621104607962110459585190118809030105845255159325119855216402270708
        b = 37128952704582304957243524

        self.axiom_helper(a,b)

        a = 209681412991024529003047811046079621104607962110459585190118809030105845255159325119855216402270708
        b = 37128952704582304957243524

        self.axiom_helper(a,b)

    def test_misc(self):
        i = -5647382910564738291056473829105647382910564738291023857209485209457092435
        j = 37128952704582304957243524
        k = 37128952704582304957243524
        k = k - j

        while j > k:
            self.misc_helper(i, j, k)
            k = k * 2 + 312870870232

        i = 5647382910564738291056473829105647382910564738291023857209485209457092435

        while j > k:
            self.misc_helper(i, j, k)
            k = k * 2 + 312870870232

        self.assertTrue(12297829382473034410)

    def test_hex_conversions(self):
        # Test hex conversions. CPython 2.5 uses capital L, lowercase letters a...f)
        s = hex(long(27))  # 0x1b
        self.assertTrue(s == "0x1b", "27: Expect lowercase digits. Received: %s." % (s));

        s = hex(-long(27))
        self.assertTrue(s == "-0x1b", "-27: Expect lowercase digits. Received: %s." % (s));

    def test_negative_misc(self):
        self.assertRaises(ValueError, #"invalid literal for long() with base 10: ''",
                    lambda: long(''))

run_test(__name__)
