# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

"""
Tests for float repr. The floats are built from their bits or from exact
integers so these tests do not depend on the accuracy of decimal string parsing.
"""

import struct
import unittest

from iptest import run_test, skipUnlessIronPython

def from_bits(bits):
    return struct.unpack('<d', struct.pack('<Q', bits))[0]

class FloatReprTest(unittest.TestCase):
    def check(self, cases):
        for bits, expected in cases:
            with self.subTest(bits=hex(bits)):
                self.assertEqual(repr(from_bits(bits)), expected)
                self.assertEqual(repr(from_bits(bits | 0x8000000000000000)), '-' + expected)

    def test_zero(self):
        self.check([(0, '0.0')])

    def test_subnormal(self):
        self.check([
            (0x0000000000000001, '5e-324'),
            (0x0008000000000000, '1.1125369292536007e-308'), # 2**-1023
            (0x000fffffffffffff, '2.225073858507201e-308'),
        ])

    def test_min_max_normal(self):
        self.check([
            (0x0010000000000000, '2.2250738585072014e-308'),
            (0x7fefffffffffffff, '1.7976931348623157e+308'),
        ])

    def test_exponent_boundaries(self):
        # powers of two and their neighbours
        self.check([
            (0x001fffffffffffff, '4.4501477170144023e-308'),
            (0x0020000000000000, '4.450147717014403e-308'), # 2**-1021
            (0x0020000000000001, '4.450147717014404e-308'),
            (0x3fefffffffffffff, '0.9999999999999999'),
            (0x3ff0000000000000, '1.0'),
            (0x3ff0000000000001, '1.0000000000000002'),
            (0x7fdfffffffffffff, '8.988465674311579e+307'),
            (0x7fe0000000000000, '8.98846567431158e+307'), # 2**1023
            (0x7fe0000000000001, '8.988465674311582e+307'),

            # 2**53 is the end of the small integer path
            (0x432fffffffffffff, '4503599627370495.5'),
            (0x4330000000000000, '4503599627370496.0'), # 2**52
            (0x4330000000000001, '4503599627370497.0'),
            (0x433fffffffffffff, '9007199254740991.0'),
            (0x4340000000000000, '9007199254740992.0'), # 2**53
            (0x4340000000000001, '9007199254740994.0'),
            (0x434fffffffffffff, '1.8014398509481982e+16'),
            (0x4350000000000000, '1.8014398509481984e+16'), # 2**54
            (0x4350000000000001, '1.8014398509481988e+16'),
        ])

    def test_digit_counts(self):
        self.check([
            (0x3fc999999999999a, '0.2'),
            (0x3fcd70a3d70a3d71, '0.23'),
            (0x3fcdb22d0e560419, '0.232'),
            (0x3fcdbc01a36e2eb2, '0.2323'),
            (0x3fcdbca9691a75cd, '0.23232'),
            (0x3fcdbcc2938de6de, '0.232323'),
            (0x3fcdbcc4410d108a, '0.2323232'),
            (0x3fcdbcc48179bd30, '0.23232323'),
            (0x3fcdbcc485c5402a, '0.232323232'),
            (0x3fcdbcc4866a2d69, '0.2323232323'),
            (0x3fcdbcc486752c29, '0.23232323232'),
            (0x3fcdbcc48676d260, '0.232323232323'),
            (0x3fcdbcc48676ee86, '0.2323232323232'),
            (0x3fcdbcc48676f2be, '0.23232323232323'),
            (0x3fcdbcc48676f306, '0.232323232323232'),
            (0x3fcdbcc48676f311, '0.2323232323232323'),
            (0x3fcdbcc48676f312, '0.23232323232323232'),
        ])

    def test_notation(self):
        self.check([
            (0x3f1a36e2eb1c432d, '0.0001'),
            (0x3ee4f8b588e368f1, '1e-05'),
            (0x3f202e4b6ce5dc68, '0.00012345'),
            (0x3ee9e3abe16fc70d, '1.2345e-05'),
            (0x4341c37937e07fff, '9999999999999998.0'),
            (0x4341c37937e08000, '1e+16'),
            (0x4345ee2a2eb5a5c4, '1.2345678901234568e+16'),
            (0x42dc12218377de6b, '123456789012345.67'),
            (0x2b2bff2ee48e0530, '1e-100'),
            (0x54b249ad2594c37d, '1e+100'),
        ])

    def test_boundary_inclusion(self):
        # the shorter decimal is exactly halfway between two floats, so it only
        # parses back to (and is only the repr of) the one with the even significand
        self.check([
            (0x447017f7df96be18, '4.75e+21'),
            (0x447017f7df96be17, '4.749999999999999e+21'),
            (0x44b52d02c7e14af6, '1e+23'),
            (0x44b52d02c7e14af7, '1.0000000000000001e+23'),
        ])

    def test_rounding_ties(self):
        # the exact value is halfway between the two closest candidates
        self.check([
            (0x430c6bf526340002, '1000000000000000.2'), # 1000000000000000.25
            (0x430c6bf526340006, '1000000000000000.8'), # 1000000000000000.75
            (0x42d6bcc41e900008, '100000000000000.12'), # 100000000000000.125
            (0x42d6bcc41e900018, '100000000000000.38'), # 100000000000000.375
            (0x42a2309ce5400020, '10000000000000.062'), # 10000000000000.0625
            (0x42a2309ce5400060, '10000000000000.188'), # 10000000000000.1875
            (0x426d1a94a2000100, '1000000000000.0312'), # 1000000000000.03125
            (0x426d1a94a2000300, '1000000000000.0938'), # 1000000000000.09375
            (0x42374876e8000400, '100000000000.01562'), # 100000000000.015625
            (0x42374876e8000c00, '100000000000.04688'), # 100000000000.046875
        ])

    def test_integers(self):
        # every significand bit length up to 53
        for k in range(54):
            for i in {2**k - 1, 2**k}:
                with self.subTest(i=i):
                    self.assertEqual(repr(float(i)), str(i) + '.0')
                    self.assertEqual(repr(-float(i)), '-' + str(i) + '.0')

    @skipUnlessIronPython()
    def test_pow10_significands(self):
        import math
        import System
        from System.Reflection import BindingFlags

        schubfach = System.Type.GetType('IronPython.Runtime.Schubfach, IronPython')
        table = schubfach.GetField('pow10_significands', BindingFlags.NonPublic | BindingFlags.Static).GetValue(None)

        # Result 24 of https://fmt.dev/papers/Schubfach4.pdf, for 10**-k from 10**-292 to 10**324
        expected = []
        for k in range(292, -325, -1):
            r = math.floor(-k * math.log2(10)) - 125
            # g = floor(2**-r * 10**-k) + 1
            num, den = (10**-k, 1) if k < 0 else (1, 10**k)
            if r < 0:
                num <<= -r
            else:
                den <<= r
            g = num // den + 1
            expected += [g >> 63, g & ((1 << 63) - 1)]

        self.assertEqual(list(table), expected)

run_test(__name__)
