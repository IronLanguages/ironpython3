# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

from decimal import Decimal
from fractions import Fraction
from numbers import Rational
import operator
import unittest

from iptest import run_test, skipUnlessIronPython

def SystemDecimalParse(s):
    import System
    import System.Globalization
    return System.Decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture)

@skipUnlessIronPython()
class DecimalTest(unittest.TestCase):

    def test_explicit_from_System_Decimal(self):
        #int
        self.assertEqual(str(Decimal(SystemDecimalParse('45'))), '45')

        #float
        self.assertEqual(str(Decimal(SystemDecimalParse('45.34'))), '45.34')

    def test_formatting(self):
        d = SystemDecimalParse('1.4274243253253245432543254545')
        self.assertEqual('{}'.format(d), '1.4274243253253245432543254545')
        self.assertEqual('{:,.2f}'.format(d), '1.43')
        self.assertEqual('{:e}'.format(d), '1.427424325325e+00')

        d = SystemDecimalParse('4000000000.40000000')
        self.assertEqual('{}'.format(d), '4000000000.40000000')
        self.assertEqual('{:e}'.format(d), '4.000000000400e+09')

    def test_hash(self):
        # https://github.com/IronLanguages/ironpython2/issues/527
        for s in ['0', '1', '-1', '2', '1.0', '-2.00', '0.5', '-0.5', '0.1', '1.50', '123456789.123456789',
                  '-1073741824.5', '18014398509481984.5', '0.0000000000000000000000000001',
                  '79228162514264337593543950335', '-79228162514264337593543950335']:
            d = SystemDecimalParse(s)
            self.assertEqual(hash(d), hash(Fraction(s)), s)
            if d == int(d):
                self.assertEqual(hash(d), hash(int(d)), s)
            if d == float(d):
                self.assertEqual(hash(d), hash(float(d)), s)

        self.assertEqual({0.5: 'x'}[SystemDecimalParse('0.5')], 'x')
        self.assertEqual({1: 'x'}[SystemDecimalParse('1.00')], 'x')
        self.assertEqual(hash(-1073741824.5), hash(Fraction(-2147483649, 2))) # float hash maps -1 to -2

    def test_compare_decimal(self):
        # https://github.com/IronLanguages/ironpython2/issues/527
        for s in ['0', '1', '-1', '1.50', '0.1', '-2.25', '18014398509481984.5', '0.0000000000000000000000000001',
                  '79228162514264337593543950335', '-79228162514264337593543950335']:
            d, p = SystemDecimalParse(s), Decimal(s)
            self.assertTrue(p == d, s)
            self.assertTrue(d == p, s)
            self.assertFalse(p != d, s)
            self.assertFalse(d != p, s)
            self.assertEqual(hash(p), hash(d), s)
            self.assertEqual({p: 'x'}[d], 'x')
            self.assertEqual({d: 'x'}[p], 'x')

        d, p = SystemDecimalParse('0.1'), Decimal('0.2')
        self.assertTrue(d < p)
        self.assertTrue(p > d)
        self.assertTrue(d <= p)
        self.assertTrue(p >= d)
        self.assertFalse(p < d)
        self.assertFalse(d > p)
        self.assertTrue(Decimal('0.1') == SystemDecimalParse('0.10'))
        self.assertFalse(Decimal('nan') == SystemDecimalParse('1'))
        self.assertTrue(Decimal('inf') > SystemDecimalParse('79228162514264337593543950335'))
        self.assertEqual(sorted([Decimal('0.3'), d, p]), [d, p, Decimal('0.3')])

    def test_arithmetic_decimal(self):
        # by design: System.Decimal compares with decimal.Decimal but doesn't mix in arithmetic.
        d, p = SystemDecimalParse('1.5'), Decimal('0.25')
        for op in (operator.add, operator.sub, operator.mul, operator.truediv, operator.floordiv, operator.mod, divmod, pow):
            self.assertRaises(TypeError, op, p, d)
            self.assertRaises(TypeError, op, d, p)
        self.assertRaises(TypeError, Decimal.from_float, d)

        # convert explicitly instead
        self.assertEqual(Decimal(d) + p, Decimal('1.75'))
        self.assertEqual(p.quantize(Decimal(SystemDecimalParse('0.1'))), Decimal('0.2'))

    def test_rational(self):
        d = SystemDecimalParse('1.50')
        self.assertIsInstance(d, Rational)
        for s, n, den in [('1.50', 3, 2), ('-2.25', -9, 4), ('0.000', 0, 1), ('-7', -7, 1),
                          ('0.0000000000000000000000000001', 1, 10**28),
                          ('79228162514264337593543950335', 79228162514264337593543950335, 1)]:
            v = SystemDecimalParse(s)
            self.assertEqual((v.numerator, v.denominator), (n, den), s)
            self.assertIs(type(v.numerator), int)

        self.assertEqual(Fraction(d), Fraction(3, 2))
        self.assertTrue(Fraction(3, 2) == d)
        self.assertTrue(d == Fraction(3, 2))
        self.assertFalse(Fraction(1, 3) == SystemDecimalParse('0.3333333333333333333333333333'))
        self.assertTrue(Fraction(1, 2) < d)
        self.assertTrue(d > Fraction(1, 2))
        self.assertEqual(hash(Fraction(3, 2)), hash(d))

    def test_compare_float(self):
        # comparisons with float are exact
        d = SystemDecimalParse('0.1')
        self.assertFalse(d == 0.1)
        self.assertFalse(0.1 == d)
        self.assertTrue(d != 0.1)
        self.assertTrue(0.1 != d)
        self.assertTrue(d < 0.1) # 0.1 is 0.100000000000000005...
        self.assertTrue(0.1 > d)
        self.assertTrue(d <= 0.1)
        self.assertFalse(d >= 0.1)

        d = SystemDecimalParse('0.5')
        self.assertTrue(d == 0.5)
        self.assertTrue(0.5 == d)
        self.assertFalse(d != 0.5)
        self.assertTrue(d <= 0.5 <= d)

        d = SystemDecimalParse('18014398509481984.5')
        self.assertFalse(d == float(d))
        self.assertTrue(d > float(d))

        self.assertTrue(SystemDecimalParse('-0.0000000000000000000000000001') < -0.0)
        self.assertTrue(-0.0 < SystemDecimalParse('0.0000000000000000000000000001'))
        self.assertTrue(SystemDecimalParse('0') == -0.0)
        self.assertTrue(SystemDecimalParse('79228162514264337593543950335') < 1e300)
        self.assertTrue(-1e300 < SystemDecimalParse('-79228162514264337593543950335'))
        self.assertTrue(5e-324 > SystemDecimalParse('0'))

        d = SystemDecimalParse('1.5')
        self.assertTrue(d < float('inf'))
        self.assertTrue(float('-inf') < d)

        nan = float('nan')
        for op in (operator.lt, operator.le, operator.gt, operator.ge, operator.eq):
            self.assertFalse(op(d, nan))
            self.assertFalse(op(nan, d))
        self.assertTrue(d != nan)
        self.assertTrue(nan != d)

        # float doesn't know about System.Decimal
        self.assertIs((0.5).__eq__(SystemDecimalParse('0.5')), NotImplemented)
        self.assertIs((0.5).__lt__(SystemDecimalParse('0.5')), NotImplemented)
        self.assertIs(SystemDecimalParse('0.5').__eq__(0.5), True)

run_test(__name__)
