# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import itertools
import math
import unittest

from iptest import IronPythonTestCase, is_cli, run_test, skipUnlessIronPython

if is_cli:
    from System import Int64, Byte, Int16

class MathTest(IronPythonTestCase):

    def test_nonnumeric_multiply(self):
        self.assertTrue("pypypypypy" == 5 * "py")
        self.assertTrue("pypypypypy" == "py" * 5)
        self.assertTrue("pypypypy" == 2 * "py" * 2)

        self.assertTrue(['py', 'py', 'py'] == ['py'] * 3)
        self.assertTrue(['py', 'py', 'py'] == 3 * ['py'])

        self.assertTrue(['py', 'py', 'py', 'py', 'py', 'py', 'py', 'py', 'py'] == 3 * ['py'] * 3)

    def test_misc(self):
        self.assertTrue(3782452410 > 0)

    def test_complex(self):
        # Complex tests
        self.assertTrue((2+4j)/(1+1j) == (3+1j))
        self.assertTrue((2+10j)/4.0 == (0.5+2.5j))
        self.assertEqual(1j ** 2, (-1.0+0j))
        self.assertEqual(pow(1j, 2), (-1.0+0j))
        self.assertEqual(1+0j, 1)
        self.assertEqual(1+0j, 1.0)
        self.assertEqual(1+0j, long(1))
        self.assertEqual((1+1j)/long(1), (1+1j))
        self.assertEqual((1j) + long(1), (1+1j))
        self.assertEqual(0j ** 0, 1)
        self.assertEqual(0j ** 0j, 1)
        self.assertEqual(pow(0j, 0), 1)
        self.assertEqual(pow(0j, 0j), 1)

        if is_cli: self.assertEqual((1j) + Int64(), 1j)

        self.assertRaises(TypeError, (lambda:(1+1j)+[]))
        self.assertRaises(TypeError, lambda : type(2j).__dict__['real'].__set__, 2j, 0)

    def test_floor_divide(self):
        self.assertRaises(TypeError, lambda: 12j // 5j)
        self.assertRaises(TypeError, lambda: (12.0+0j) // (5.0+0j))
        self.assertRaises(TypeError, lambda: (12+0j) // (0+3j))
        self.assertRaises(TypeError, lambda: (0+12j) // (3+0j))
        self.assertRaises(TypeError, lambda: 2j // "astring")
        self.assertRaises(TypeError, lambda: 3.0 // (0j))
        self.assertRaises(TypeError, lambda: 3.0 // (2j))
        self.assertRaises(TypeError, lambda: 12 // (3j))
        self.assertRaises(TypeError, lambda: 25 % (5+3j))

    def test_more_complex(self):
        self.assertEqual((12+3j)/long(3), (4+1j))
        self.assertEqual(3j - long(5), -5+3j)
        if is_cli: self.assertEqual(3j - Int64(), 3j)
        self.assertRaises(TypeError, (lambda:3j-[]))
        if is_cli: self.assertEqual(pow(5j, Int64()), (1+0j))
        self.assertEqual(pow(5j, long(0)), (1+0j))
        self.assertRaises(TypeError, (lambda:pow(5j, [])))
        if is_cli: self.assertEqual(5j * Int64(), 0)
        self.assertEqual(5j * long(3), 15j)
        self.assertRaises(TypeError, (lambda:(5j*[])))

    def test_erf(self):
        table = [
            (0.0,  0.0000000),
            (0.05, 0.0563720),
            (0.1,  0.1124629),
            (0.15, 0.1679960),
            (0.2,  0.2227026),
            (0.25, 0.2763264),
            (0.3,  0.3286268),
            (0.35, 0.3793821),
            (0.4,  0.4283924),
            (0.45, 0.4754817),
            (0.5,  0.5204999),
            (0.55, 0.5633234),
            (0.6,  0.6038561),
            (0.65, 0.6420293),
            (0.7,  0.6778012),
            (0.75, 0.7111556),
            (0.8,  0.7421010),
            (0.85, 0.7706681),
            (0.9,  0.7969082),
            (0.95, 0.8208908),
            (1.0,  0.8427008),
            (1.1,  0.8802051),
            (1.2,  0.9103140),
            (1.3,  0.9340079),
            (1.4,  0.9522851),
            (1.5,  0.9661051),
            (1.6,  0.9763484),
            (1.7,  0.9837905),
            (1.8,  0.9890905),
            (1.9,  0.9927904),
            (2.0,  0.9953223),
            (2.1,  0.9970205),
            (2.2,  0.9981372),
            (2.3,  0.9988568),
            (2.4,  0.9993115),
            (2.5,  0.9995930),
            (2.6,  0.9997640),
            (2.7,  0.9998657),
            (2.8,  0.9999250),
            (2.9,  0.9999589),
            (3.0,  0.9999779),
            (3.1,  0.9999884),
            (3.2,  0.9999940),
            (3.3,  0.9999969),
            (3.4,  0.9999985),
            (3.5,  0.9999993),
            (4.0,  1.0000000),
        ]

        for x, y in table:
            self.assertAlmostEqual(y, math.erf(x), places=7)
            self.assertAlmostEqual(-y, math.erf(-x), places=7)

    def test_erfc(self):
        table = [
            (0.0,  1.0000000),
            (0.05, 0.9436280),
            (0.1,  0.8875371),
            (0.15, 0.8320040),
            (0.2,  0.7772974),
            (0.25, 0.7236736),
            (0.3,  0.6713732),
            (0.35, 0.6206179),
            (0.4,  0.5716076),
            (0.45, 0.5245183),
            (0.5,  0.4795001),
            (0.55, 0.4366766),
            (0.6,  0.3961439),
            (0.65, 0.3579707),
            (0.7,  0.3221988),
            (0.75, 0.2888444),
            (0.8,  0.2578990),
            (0.85, 0.2293319),
            (0.9,  0.2030918),
            (0.95, 0.1791092),
            (1.0,  0.1572992),
            (1.1,  0.1197949),
            (1.2,  0.0896860),
            (1.3,  0.0659921),
            (1.4,  0.0477149),
            (1.5,  0.0338949),
            (1.6,  0.0236516),
            (1.7,  0.0162095),
            (1.8,  0.0109095),
            (1.9,  0.0072096),
            (2.0,  0.0046777),
            (2.1,  0.0029795),
            (2.2,  0.0018628),
            (2.3,  0.0011432),
            (2.4,  0.0006885),
            (2.5,  0.0004070),
            (2.6,  0.0002360),
            (2.7,  0.0001343),
            (2.8,  0.0000750),
            (2.9,  0.0000411),
            (3.0,  0.0000221),
            (3.1,  0.0000116),
            (3.2,  0.0000060),
            (3.3,  0.0000031),
            (3.4,  0.0000015),
            (3.5,  0.0000007),
            (4.0,  0.0000000),
        ]

        for x, y in table:
            self.assertAlmostEqual(y, math.erfc(x), places=7)
            self.assertAlmostEqual(2.0 - y, math.erfc(-x), places=7)

    def test_erf_erfc(self):
        tolerance = 15
        for x in itertools.count(0.0, 0.001):
            if x > 5.0:
                break
            self.assertAlmostEqual(math.erf(x), -math.erf(-x), places=tolerance)
            self.assertAlmostEqual(math.erfc(x), 2.0 - math.erfc(-x), places=tolerance)

            self.assertAlmostEqual(1.0 - math.erf(x), math.erfc(x), places=tolerance)
            self.assertAlmostEqual(1.0 - math.erf(-x), math.erfc(-x), places=tolerance)
            self.assertAlmostEqual(1.0 - math.erfc(x), math.erf(x), places=tolerance)
            self.assertAlmostEqual(1.0 - math.erfc(-x), math.erf(-x), places=tolerance)

    def test_gamma(self):
        self.assertAlmostEqual(math.gamma(0.5), math.sqrt(math.pi), places=15)
        for i in range(1, 20):
            self.assertEqual(math.factorial(i-1), math.gamma(i))
        self.assertEqual(math.gamma(float('inf')), float('inf'))
        self.assertRaises(ValueError, math.gamma, float('-inf'))
        self.assertTrue(math.isnan(math.gamma(float('nan'))))
        for i in range(0, -1001, -1):
            self.assertRaises(ValueError, math.gamma, i)

    def test_lgamma(self):
        tolerance = 14
        self.assertAlmostEqual(math.lgamma(0.5), 0.5 * math.log(math.pi), places=15)
        for i in range(1, 20):
            if i > 14:
                tolerance = 13
            self.assertAlmostEqual(math.log(math.factorial(i-1)), math.lgamma(i), places=tolerance)
        self.assertEqual(math.lgamma(float('inf')), float('inf'))
        self.assertEqual(math.lgamma(float('-inf')), float('inf'))
        self.assertTrue(math.isnan(math.lgamma(float('nan'))))
        for i in range(0, -1001, -1):
            self.assertRaises(ValueError, math.lgamma, i)

    def test_gamma_lgamma(self):
        tolerance = 13
        for x in itertools.count(0.001, 0.001):
            if x > 5.0:
                break
            self.assertAlmostEqual(math.lgamma(x), math.log(math.gamma(x)), places=tolerance)
            self.assertAlmostEqual(math.lgamma(x*x), math.log(math.gamma(x*x)), places=tolerance)
            self.assertAlmostEqual(math.lgamma(2.0**x), math.log(math.gamma(2.0**x)), places=tolerance)

            # Test negative values too, but not integers
            if x % 1.0 != 0.0:
                self.assertAlmostEqual(math.lgamma(-x), math.log(abs(math.gamma(-x))), places=tolerance)
                self.assertAlmostEqual(math.lgamma(-x*x), math.log(abs(math.gamma(-x*x))), places=tolerance)
                self.assertAlmostEqual(math.lgamma(-2.0**x), math.log(abs(math.gamma(-2.0**x))), places=tolerance)

    def test_pow(self):
        self.assertEqual(pow(2, 1000000000, 2147483647 + 10), 511677409)
        self.assertEqual(pow(2, 2147483647*2147483647, 2147483647*2147483647), 297528129805479806)
        nums = [3, 2.3, (2+1j), (2-1j), 1j, (-1j), 1]
        for x in nums:
            for y in nums:
                z = x ** y

    def test_mod_pow(self):
        for i in range(-100, 100, 7):
            l = long(i)
            self.assertTrue(type(i) == int)
            self.assertTrue(type(l) == long)
            for exp in [1, 17, 2863, 234857, 1435435, 234636554, 2147483647]:
                lexp = long(exp)
                self.assertTrue(type(exp) == int)
                self.assertTrue(type(lexp) == long)
                for mod in [-7, -5293, -2147483647, 7, 5293, 23745, 232474276, 534634665, 2147483647]:
                    lmod = long(mod)
                    self.assertTrue(type(mod) == int)
                    self.assertTrue(type(lmod) == long)

                    ir = pow(i, exp, mod)
                    lr = pow(l, lexp, lmod)

                    self.assertEqual(ir, lr)

                    for zero in [0, long(0)]:
                        ir = pow(i, zero, mod)
                        lr = pow(l, zero, lmod)

                        if mod > 0:
                            self.assertEqual(ir, 1)
                            self.assertEqual(lr, 1)
                        else:
                            self.assertEqual(ir, mod+1)
                            self.assertEqual(lr, mod+1)
                self.assertRaises(ValueError, pow, i, exp, 0)
                self.assertRaises(ValueError, pow, l, lexp, long(0))


            for exp in [0, long(0)]:
                for mod in [-1,1,-long(1),long(1)]:
                    ir = pow(i, exp, mod)
                    lr = pow(l, exp, mod)
                    self.assertEqual(ir, 0)
                    self.assertEqual(lr, 0)

    def test_user_ops(self):
        class powtest:
            def __pow__(self, exp, mod = None):
                return ("powtest.__pow__", exp, mod)
            def __rpow__(self, exp):
                return ("powtest.__rpow__", exp)

        self.assertEqual(pow(powtest(), 1, 2), ("powtest.__pow__", 1, 2))
        self.assertEqual(pow(powtest(), 3), ("powtest.__pow__", 3, None))
        self.assertEqual(powtest() ** 4, ("powtest.__pow__", 4, None))
        self.assertEqual(5 ** powtest(), ("powtest.__rpow__", 5))
        self.assertEqual(pow(7, powtest()), ("powtest.__rpow__", 7))
        self.assertRaises(TypeError, pow, 1, powtest(), 7)

        # Extensible Float tests
        class XFloat(float): pass

        self.assertEqual(XFloat(3.14), 3.14)
        self.assertTrue(XFloat(3.14) < 4.0)
        self.assertTrue(XFloat(3.14) > 3.0)
        self.assertTrue(XFloat(3.14) < XFloat(4.0))
        self.assertTrue(XFloat(3.14) > XFloat(3.0))

        self.assertTrue(0xabcdef01 + (0xabcdef01<<32)+(0xabcdef01<<64) == 0xabcdef01abcdef01abcdef01)

    def test_rounding(self):
        self.assertTrue(round(-5.5489) == (-6.0))
        self.assertTrue(round(5.5519) == (6.0))
        self.assertTrue(round(-5.5) == (-6.0))
        self.assertTrue(round(-5.0) == (-5.0))

        self.assertTrue(round(-4.5) == (-4.0))
        self.assertTrue(round(-2.5) == (-2.0))
        self.assertTrue(round(-0.5) == (-0.0))
        self.assertTrue(round(0.5) == (0.0))
        self.assertTrue(round(2.5) == (2.0))
        self.assertTrue(round(4.5) == (4.0))

        self.assertTrue(round(-4.0) == (-4.0))
        self.assertTrue(round(-3.5) == (-4.0))
        self.assertTrue(round(-3.0) == (-3.0))
        self.assertTrue(round(-2.0) == (-2.0))
        self.assertTrue(round(-1.5) == (-2.0))
        self.assertTrue(round(-1.0) == (-1.0))
        self.assertTrue(round(0.0) == (0.0))
        self.assertTrue(round(1.0) == (1.0))
        self.assertTrue(round(1.5) == (2.0))
        self.assertTrue(round(2.0) == (2.0))
        self.assertTrue(round(3.0) == (3.0))
        self.assertTrue(round(3.5) == (4.0))
        self.assertTrue(round(4.0) == (4.0))
        self.assertTrue(round(5.0) == (5.0))

        # two parameter round overload
        self.assertTrue(round(-4.0, 0) == (-4.0))
        self.assertTrue(round(-3.5, 0) == (-4.0))
        self.assertTrue(round(-3.0, 0) == (-3.0))
        self.assertTrue(round(-2.0, 0) == (-2.0))
        self.assertTrue(round(-1.5, 0) == (-2.0))
        self.assertTrue(round(-1.0, 0) == (-1.0))
        self.assertTrue(round(0.0, 0) == (0.0))
        self.assertTrue(round(1.0, 0) == (1.0))
        self.assertTrue(round(1.5, 0) == (2.0))
        self.assertTrue(round(2.0, 0) == (2.0))
        self.assertTrue(round(3.0, 0) == (3.0))
        self.assertTrue(round(3.5, 0) == (4.0))
        self.assertTrue(round(4.0, 0) == (4.0))
        self.assertTrue(round(5.0, 0) == (5.0))
        self.assertTrue(round(123.41526375, 1) == 123.4)
        self.assertTrue(round(123.41526375, 2) == 123.42)
        self.assertTrue(round(123.41526375, 3) == 123.415)
        self.assertTrue(round(123.41526375, 4) == 123.4153)
        self.assertTrue(round(123.41526375, 5) == 123.41526)
        self.assertTrue(round(123.41526375, 6) == 123.415264)
        if not is_cli: # https://github.com/IronLanguages/main/issues/856
            self.assertTrue(round(123.41526375, 7) == 123.4152637)
        else:
            self.assertTrue(round(123.41526375, 7) == 123.4152638)
        self.assertTrue(round(-123.41526375, 1) == -123.4)
        self.assertTrue(round(-123.41526375, 2) == -123.42)
        self.assertTrue(round(-123.41526375, 3) == -123.415)
        self.assertTrue(round(-123.41526375, 4) == -123.4153)
        self.assertTrue(round(-123.41526375, 5) == -123.41526)
        self.assertTrue(round(-123.41526375, 6) == -123.415264)
        if not is_cli: # https://github.com/IronLanguages/main/issues/856
            self.assertTrue(round(-123.41526375, 7) == -123.4152637)
        else:
            self.assertTrue(round(-123.41526375, 7) == -123.4152638)
        for i in range(8, 307):
            # Note: We can't do exact equality here due to the inexact nature of IEEE
            # double precision floats when multiplied and later divided by huge powers of 10.
            # Neither CPython nor IronPython mantain exact equality for precisions >= 17
            if i < 17:
                self.assertTrue(round(123.41526375, i) == 123.41526375)
                self.assertTrue(round(-123.41526375, i) == -123.41526375)
            else:
                self.assertTrue(abs(round(123.41526375, i) - 123.41526375) < 0.0000000001)
                self.assertTrue(abs(round(-123.41526375, i) - -123.41526375) < 0.0000000001)

        self.assertTrue(round(7182930456.0, -1) == 7182930460.0)
        self.assertTrue(round(7182930456.0, -2) == 7182930500.0)
        self.assertTrue(round(7182930456.0, -3) == 7182930000.0)
        self.assertTrue(round(7182930456.0, -4) == 7182930000.0)
        self.assertTrue(round(7182930456.0, -5) == 7182900000.0)
        self.assertTrue(round(7182930456.0, -6) == 7183000000.0)
        self.assertTrue(round(7182930456.0, -7) == 7180000000.0)
        self.assertTrue(round(7182930456.0, -8) == 7200000000.0)
        self.assertTrue(round(7182930456.0, -9) == 7000000000.0)
        self.assertTrue(round(7182930456.0, -10) == 10000000000.0)
        self.assertTrue(round(7182930456.0, -11) == 0.0)
        self.assertTrue(round(-7182930456.0, -1) == -7182930460.0)
        self.assertTrue(round(-7182930456.0, -2) == -7182930500.0)
        self.assertTrue(round(-7182930456.0, -3) == -7182930000.0)
        self.assertTrue(round(-7182930456.0, -4) == -7182930000.0)
        self.assertTrue(round(-7182930456.0, -5) == -7182900000.0)
        self.assertTrue(round(-7182930456.0, -6) == -7183000000.0)
        self.assertTrue(round(-7182930456.0, -7) == -7180000000.0)
        self.assertTrue(round(-7182930456.0, -8) == -7200000000.0)
        self.assertTrue(round(-7182930456.0, -9) == -7000000000.0)
        self.assertTrue(round(-7182930456.0, -10) == -10000000000.0)
        self.assertTrue(round(-7182930456.0, -11) == 0.0)
        for i in range(-12, -309, -1):
            self.assertTrue(round(7182930456.0, i) == 0.0)
            self.assertTrue(round(-7182930456.0, i) == 0.0)

    def test_other(self):
        x = ('a', 'b', 'c')
        y = x
        y *= 3
        z = x
        z += x
        z += x
        self.assertTrue(y == z)

        self.assertTrue(1 << 32 == 4294967296)
        self.assertTrue(2 << 32 == (1 << 32) << 1)
        self.assertTrue(((1 << 16) << 16) << 16 == 1 << 48)
        self.assertTrue(((1 << 16) << 16) << 16 == 281474976710656)

        for i in [1, 10, 42, 1000000000, 34141235135135135, 13523525234523452345235235234523, 100000000000000000000000000000000000000]:
            self.assertTrue(~i == -i - 1)

        self.assertTrue(7 ** 5 == 7*7*7*7*7)
        self.assertTrue(long(7) ** long(5) == long(7)*long(7)*long(7)*long(7)*long(7))
        self.assertTrue(7 ** long(5) == 7*7*7*7*7)
        self.assertTrue(long(7) ** 5 == long(7)*long(7)*long(7)*long(7)*long(7))
        self.assertTrue(1 ** 735293857239475 == 1)
        self.assertTrue(0 ** 735293857239475 == 0)

        self.assertTrue(2 ** 3.0 == 8.0)
        self.assertTrue(2.0 ** 3 == 8.0)
        self.assertTrue(4 ** 0.5 == 2.0)

    def test_divmod(self):
        self.assertEqual(7.1//2.1, 3.0)
        self.assertEqual(divmod(5.0, 2), (2.0,1.0))
        self.assertEqual(divmod(5,2), (2,1))
        self.assertEqual(divmod(-5,2), (-3,1))

    def test_boolean(self):
        self.assertEqual(True | False, True)
        self.assertEqual(True | 4, 5)
        self.assertEqual(True & 3, 1)
        self.assertEqual(True + 3, 4)
        self.assertEqual(True - 10, -9)
        self.assertEqual(True * 8, 8)
        self.assertEqual(True // 3, 0)
        self.assertEqual(True ** 5, 1)
        self.assertEqual(True % 2, 1)
        self.assertEqual(True << 4, 1 << 4)
        self.assertEqual(True >> 2, 0)
        self.assertEqual(True ^ 3, 2)

    @skipUnlessIronPython()
    def test_byte(self):
        a = Byte()
        self.assertEqual(type(a), Byte)
        self.assertEqual(a, 0)

        b = a + Byte(1)
        self.assertEqual(b, 1)
        self.assertEqual(type(b), Byte)

        bprime = b * Byte(10)
        self.assertEqual(type(bprime), Byte)

        d = a + Byte(255)
        self.assertEqual(type(d), Byte)

        c = b + Byte(255)
        self.assertEqual(c, 256)
        self.assertEqual(type(c), Int16)

    def test_negated_comparisons(self):
        self.assertTrue(not (20 == False))
        self.assertTrue(not (20 == None))
        self.assertTrue(not (False == 20))
        self.assertTrue(not (None == 20))
        self.assertTrue(not (20 == 'a'))
        self.assertTrue(not ('a' == 20))
        self.assertTrue(not (2.5 == None))
        self.assertTrue(not (20 == (2,3)))

        self.assertEqual(long(1234793454934), 1234793454934)
        self.assertEqual(4 ** -2, 0.0625)
        self.assertEqual(long(4) ** -2, 0.0625)

    def test_zero_division(self):
        self.assertRaises(ZeroDivisionError, (lambda: (0 ** -1)))
        self.assertRaises(ZeroDivisionError, (lambda: (0.0 ** -1)))
        self.assertRaises(ZeroDivisionError, (lambda: (0 ** -1.0)))
        self.assertRaises(ZeroDivisionError, (lambda: (0.0 ** -1.0)))
        self.assertRaises(ZeroDivisionError, (lambda: (False ** -1)))
        self.assertRaises(ZeroDivisionError, (lambda: (long(0) ** -(2 ** 65))))
        self.assertRaises(ZeroDivisionError, (lambda: (0j ** -1)))
        self.assertRaises(ZeroDivisionError, (lambda: (0j ** 1j)))

    def test_extensible_math(self):
        operators = ['__add__', '__sub__', '__pow__', '__mul__', '__div__', '__floordiv__', '__truediv__', '__mod__']
        opSymbol  = ['+',       '-',       '**',      '*',       '/',       '//',           '/',           '%']

        types = []
        for baseType in [(int, (100,2)), (long, (long(100), long(2))), (float, (100.0, 2.0))]:
        # (complex, (100+0j, 2+0j)) - cpython doesn't call reverse ops for complex ?
            class prototype(baseType[0]):
                for op in operators:
                    exec('''def %s(self, other):
    global opCalled
    opCalled.append('%s')
    return super(self.__class__, self).%s(other)

def %s(self, other):
    global opCalled
    opCalled.append('%s')
    return super(self.__class__, self).%s(other)''' % (op, op, op, op[:2] + 'r' + op[2:], op[:2] + 'r' + op[2:], op[:2] + 'r' + op[2:]))

            types.append( (prototype, baseType[1]) )

        global opCalled
        opCalled = []
        for op in opSymbol:
                for typeInfo in types:
                    ex = typeInfo[0](typeInfo[1][0])
                    ey = typeInfo[0](typeInfo[1][1])
                    nx = typeInfo[0].__bases__[0](typeInfo[1][0])
                    ny = typeInfo[0].__bases__[0](typeInfo[1][1])

                    #print 'nx %s ey' % op, type(nx), type(ey)
                    res1 = eval('nx %s ey' % op)
                    res2 = eval('nx %s ny' % op)
                    self.assertEqual(res1, res2)
                    self.assertEqual(len(opCalled), 1)
                    opCalled = []

                    #print 'ex %s ny' % op, type(ex), type(ny)
                    res1 = eval('ex %s ny' % op)
                    res2 = eval('nx %s ny' % op)
                    self.assertEqual(res1, res2)
                    self.assertEqual(len(opCalled), 1)
                    opCalled = []


    def test_nan(self):
        x  = 1e66666
        self.assertTrue(x==x)
        self.assertTrue(x<=x)
        self.assertTrue(x>=x)
        self.assertTrue(not x!=x)
        self.assertTrue(not x<x)
        self.assertTrue(not x>x)

        y = x/x
        self.assertEqual(y == y, False)
        self.assertEqual(y >= y, False)
        self.assertEqual(y <= y, False)
        self.assertEqual(y != y, True)
        self.assertEqual(y > y, False)
        self.assertEqual(y < y, False)

        self.assertTrue(not x==y)
        self.assertTrue(x!=y)
        self.assertTrue(not x>=y)
        self.assertTrue(not x<=y)
        self.assertTrue(not x<y)
        self.assertTrue(not x>y)

    def test_long_log(self):
        """logon big ints should work"""
        self.assertEqual(round(math.log10(10 ** 1000), 5), 1000.0)
        self.assertEqual(round(math.log(10 ** 1000), 5), 2302.58509)

        self.assertEqual(round(math.log10(18446744073709551615), 5),  19.26592)
        self.assertEqual(round(math.log(18446744073709551615), 5), 44.36142)

        self.assertEqual(round(math.log10(18446744073709551616), 5),  19.26592)
        self.assertEqual(round(math.log(18446744073709551616), 5), 44.36142)

        self.assertEqual(round(math.log10(18446744073709551614), 5),  19.26592)
        self.assertEqual(round(math.log(18446744073709551614), 5), 44.36142)

        # log in a new base
        self.assertEqual(round(math.log(2 ** 1000, 2), 5), 1000.0)

        self.assertRaises(ValueError, math.log, long(0))
        self.assertRaises(ValueError, math.log, long(-1))
        self.assertEqual(math.log(long(2), 1e666), 0.0)
        self.assertRaises(ValueError, math.log, long(2), -1e666)
        self.assertRaises(ZeroDivisionError, math.log, long(2), 1.0)

        #Make sure that an object is converted to float before being passed into log funcs
        class N(object):
            def __float__(self):
                return 10.0
            def __long__(self):
                return 100

        self.assertEqual(round(math.log10(N()), 5),1.0)
        self.assertEqual(round(math.log(N()), 5),2.30259)

    def test_log_neg(self):
        for x in [[2,0], [0,2.0], [0], [long(0)], [long(0), 3.14]]:
            self.assertRaisesMessage(ValueError, "math domain error",
                                math.log, *x)

    def test_math_subclass(self):
        """verify subtypes of float/long work w/ math functions"""
        import math
        class myfloat(float): pass
        class mylong(long): pass

        mf = myfloat(1)
        ml = mylong(1)

        for x in math.log, math.log10, math.log1p, math.asinh, math.acosh, math.atanh, math.factorial, math.trunc, math.isinf:
            try:
                resf = x(mf)
            except ValueError:
                resf = None
            try:
                resl = x(ml)
            except ValueError:
                resl = None
            self.assertEqual(resf, resl)

    def test_float_26(self):
        from_hex_tests = [('1.fffffffffffff7', 1.9999999999999998),
                        ('1.fffffffffffff8', 2.0),
                        ('-1.1fffffffffffffffffffffffffffffffff', -1.125),
                        ('-1.ffffffffffffffffffffffffffffffffff', -2),
                        ('10.4', 16.25),
                        ('1.fffffffffffffp1023', 1.7976931348623157e+308),
                        ('1p1', 2.0),
                        ('-1.0p1', -2.0),
                        ('+1.0p1', 2.0),
                        ('-0x1.0p1', -2.0),
                        ('1.0p1023', 8.9884656743115795e+307),
                        ('1.0p-1023', 1.1125369292536007e-308),
                        ('1.1234p1', 2.1422119140625),
                        ('1.' + 'f'*1000+ 'p1', 4.0),
                        ('1.ap1', 3.25),
                        ('1.Ap1', 3.25),
                        ('1.0p-1', 0.5),
                        ('1.0p-1', 0.5),
                        ('1.0p-1074', 4.9406564584124654e-324),
                        ('2.0p-1075', 4.9406564584124654e-324),
                        ('1.0p-1075', 0),
                        ('10.0p-1075', 3.9525251667299724e-323),
                        ('0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.a', 1.634661910256948e+55),
                        ('0x1.Ap1', 3.25),
                        ('0x1.Ap2', 6.5),
                        ('0x1.Ap3', 13),
                        ('  0x1.Ap1   ', 3.25),
                        ]

        for string, value in from_hex_tests:
            #print string, value
            self.assertEqual(float.fromhex(string), value)

        from_hex_errors = [(OverflowError, '1.0p1024'),
                        (OverflowError, '1.0p1025'),
                        (OverflowError, '10.0p1023'),
                        (OverflowError, '1.ffffffffffffffp1023'),
                        (ValueError, 'xxxx'),
                        (OverflowError, '1.0p99999999999999999999999')]
        for excep, error in from_hex_errors:
            self.assertRaises(excep, float.fromhex, error)


    def test_float_subclass(self):
        global calledCount
        calledCount = 0
        class MyFloat(float):
            def __float__(self):
                global calledCount
                calledCount += 1
                return 42

        self.assertRaisesMessage(TypeError, "__float__ returned non-float (type int)", float, MyFloat(1.1))
        self.assertEqual(calledCount, 1)

    def test_integer_ratio(self):
        int_ratio_tests = [ (2.5, (5, 2)), (1.3, (5854679515581645, 4503599627370496))]

        for flt, res in int_ratio_tests:
            self.assertEqual(flt.as_integer_ratio(), res)

run_test(__name__)
