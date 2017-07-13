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

class BigIntTest(unittest.TestCase):

    def axiom_helper(self, a, b):
        self.assertTrue((a / b) * b + (a % b) == a, "(" + str(a) + " / " + str(b) + ") * " + str(b) + " + ( " + str(a) + " % " + str(b) + ") != " + str(a))

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
        s = hex(27)  # 0x1b
        self.assertTrue(s == "0x1bL", "27L: Expect lowercase digits. Received: %s." % (s));
        
        s = hex(-27)
        self.assertTrue(s == "-0x1bL", "-27L: Expect lowercase digits. Received: %s." % (s));

    def test_negative_misc(self):
        self.assertRaises(ValueError, #"invalid literal for long() with base 10: ''",
                    lambda: int(''))

run_test(__name__)
