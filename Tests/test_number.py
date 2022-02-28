# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import sys
import unittest

from iptest import IronPythonTestCase, is_cli, big, run_test, skipUnlessIronPython

def get_builtins_dict():
    if type(__builtins__) is type(sys):
        return __builtins__.__dict__
    return __builtins__

class complextest:
    def __init__(self, value): self.value = value
    def __float__(self) : return self.value

class myfloat(float): pass

class SillyLong(int):
    def __rmul__(self, other):
        return big(42)

templates1 = [ "C(%s) %s C(%s)", "C2(%s) %s C2(%s)",
               "C(%s) %s D(%s)", "D(%s) %s C(%s)",
               "C2(%s) %s D(%s)", "D(%s) %s C2(%s)",
               "C(%s) %s D2(%s)", "D2(%s) %s C(%s)",
               "C2(%s) %s D2(%s)", "D2(%s) %s C2(%s)"]
templates2 = [x for x in templates1 if x.startswith('C')]

if is_cli:
    from System import *

class NumberTest(IronPythonTestCase):
    def setUp(self):
        super(NumberTest, self).setUp()
        self.load_iron_python_test()

    def test_complex(self):
        self.assertEqual(complex(complextest(2.0)), 2+0j)
        self.assertEqual(complex(complextest(myfloat(2.0))), 2+0j)
        self.assertRaises(TypeError, complex, complextest(2))

    def test_silly_long(self):
        self.assertEqual(1 * SillyLong(2), 42)
        self.assertEqual(SillyLong(2) * 1, 2)
        if is_cli:
            self.assertEqual((1).__mul__(SillyLong(2)), NotImplemented)

    @skipUnlessIronPython()
    def test_clr(self):
        self.assertTrue(Single.IsInfinity(Single.PositiveInfinity))
        self.assertTrue(not Single.IsInfinity(1.0))

        x = [333, 1234.5, 1, 333, -1, 66.6]
        x.sort()
        self.assertTrue(x == [-1, 1, 66.6, 333, 333, 1234.5])

        self.assertTrue(10 < 76927465928764592743659287465928764598274369258736489327465298374695287346592837496)
        self.assertTrue(76927465928764592743659287465928764598274369258736489327465298374695287346592837496 > 10)


        x = 3e1000
        self.assertTrue(Double.IsInfinity(x))
        self.assertTrue(Double.IsPositiveInfinity(x))
        x = -3e1000
        self.assertTrue(Double.IsInfinity(x))
        self.assertTrue(Double.IsNegativeInfinity(x))
        x = 3e1000 - 3e1000
        self.assertTrue(Double.IsNaN(x))

        f_x = "4.75"
        f_y = "3.25"
        i_x = "4"
        i_y = "3"

        def Parse(type, value):
            return type.Parse(value, Globalization.CultureInfo.InvariantCulture.NumberFormat)

        def VerifyTypes(v):
            self.assertEqual(str(v.x.GetType()), v.n)
            self.assertEqual(str(v.y.GetType()), v.n)

        class float:
            def __init__(self, type, name):
                self.x = Parse(type, f_x)
                self.y = Parse(type, f_y)
                self.n = name

        class fixed:
            def __init__(self, type, name):
                self.x = type.Parse(i_x)
                self.y = type.Parse(i_y)
                self.n = name

        s = float(Single, "System.Single")
        d = float(Double, "System.Double")
        sb = fixed(SByte, "System.SByte")
        sh = fixed(Int16, "System.Int16")
        i = fixed(Int32, "System.Int32")
        l = fixed(Int64, "System.Int64")
        ub = fixed(Byte, "System.Byte")
        ui = fixed(UInt32, "System.UInt32")
        ul = fixed(UInt64, "System.UInt64")

        def float_test(x,y):
            self.assertTrue(x + y == y + x)
            self.assertTrue(x * y == y * x)
            self.assertTrue(x / y == x / y)
            self.assertTrue(x % y == x % y)
            self.assertTrue(x - y == -(y - x))
            self.assertTrue(x ** y == x ** y)
            self.assertTrue(x // y == x // y)
            z = x
            z /= y
            self.assertTrue(z == x / y)
            z = x
            z *= y
            self.assertTrue(z == x * y)
            z = x
            z %= y
            self.assertTrue(z == x % y)
            z = x
            z += y
            self.assertTrue(z == x + y)
            z = x
            z -= y
            self.assertTrue(z == x - y)
            z = x
            z **= y
            self.assertTrue(z == x ** y)
            z = x
            z //= y
            self.assertTrue(z == x // y)
            self.assertTrue((x < y) == (not (x >= y)))
            self.assertTrue((x <= y) == (not (x > y)))
            self.assertTrue((x > y) == (not (x <= y)))
            self.assertTrue((x >= y) == (not (x < y)))
            self.assertTrue((x != y) == (not (x == y)))
            self.assertEqual((x == y), (y == x))
            self.assertTrue((x == y) == (y == x))
            self.assertTrue((x == y) == (not (x != y)))

        def type_test(tx, ty):
            x = tx.x
            y = ty.y
            float_test(x,x)
            float_test(x,y)
            float_test(y,y)
            float_test(y,x)

        test_types = [s,d,i,l]
        # BUG 10 : Add support for unsigned integer types (and other missing data types)
        #test_types = [s,d,i,l,sb,sh,ub,ui,ul]
        # /BUG

        for a in test_types:
            VerifyTypes(a)
            for b in test_types:
                VerifyTypes(b)
                type_test(a, b)
                type_test(b, a)

    @skipUnlessIronPython()
    def test_conversions(self):
        """implicit conversions (conversion defined on Derived)"""
        from IronPythonTest import Base, Base2, ConversionStorage, Derived, Derived2, EnumByte, EnumInt, EnumLong, EnumSByte, EnumShort, EnumTest, EnumUShort, EnumUInt, EnumULong
        a = ConversionStorage()
        b = Base(5)
        d = Derived(23)

        a.Base = d
        self.assertEqual(a.Base.value, d.value)
        a.Derived = d
        self.assertEqual(a.Derived.value, d.value)

        a.Base = b
        self.assertEqual(a.Base.value, b.value)


        def assignBaseToDerived(storage, base):
            storage.Derived = base

        self.assertRaises(TypeError, assignBaseToDerived, a, b)


        # implicit conversions (conversion defined on base)
        a = ConversionStorage()
        b = Base2(5)
        d = Derived2(23)


        a.Base2 = d
        self.assertEqual(a.Base2.value, d.value)
        a.Derived2 = d
        self.assertEqual(a.Derived2.value, d.value)

        a.Base2 = b
        self.assertEqual(a.Base2.value, b.value)


        def assignBaseToDerived(storage, base):
            storage.Derived2 = base

        self.assertRaises(TypeError, assignBaseToDerived, a, b)

        class myFakeInt:
            def __int__(self):
                return 23

        class myFakeLong:
            def __int__(self):
                return big(23)

        class myFakeComplex:
            def __complex__(self):
                return 0j + 23

        class myFakeFloat:
            def __float__(self):
                return 23.0

        class myNegative:
            def __pos__(self):
                return 23

        self.assertEqual(int(myFakeInt()), 23)
        self.assertEqual(int(myFakeLong()), 23)
        self.assertEqual(complex(myFakeComplex()), 0j + 23)
        self.assertEqual(get_builtins_dict()['float'](myFakeFloat()), 23.0)   # we redefined float above, go directly to the real float...
        self.assertEqual(+myNegative(), 23)


        # True/False and None...  They shouldn't convert to each other, but
        # a truth test against none should always be false.

        self.assertEqual(False == None, False)
        self.assertEqual(True == None, False)
        self.assertEqual(None == False, False)
        self.assertEqual(None == True, False)

        if None: self.fail("Unreachable code reached: none shouldn't be true")

        a = None
        if a: self.assertEqual(False, True)

        # Enum conversions

        class EnumRec:
            def __init__(self, code, min, max, enum, test):
                self.code = code
                self.min = min
                self.max = max
                self.enum = enum
                self.test = test

        enum_types = [
            EnumRec("SByte", -128, 127, EnumSByte, EnumTest.TestEnumSByte),
            EnumRec("Byte", 0, 255, EnumByte, EnumTest.TestEnumByte),
            EnumRec("Short", -32768, 32767, EnumShort, EnumTest.TestEnumShort),
            EnumRec("UShort", 0, 65535, EnumUShort, EnumTest.TestEnumUShort),
            EnumRec("Int", -2147483648, 2147483647, EnumInt, EnumTest.TestEnumInt),
            EnumRec("UInt", 0, 4294967295, EnumUInt, EnumTest.TestEnumUInt),
            EnumRec("Long", -9223372036854775808, 9223372036854775807, EnumLong, EnumTest.TestEnumLong),
            EnumRec("ULong", 0, 18446744073709551615, EnumULong, EnumTest.TestEnumULong),
        ]

        value_names = ["Zero"]
        value_values = {"Zero" : 0}
        for e in enum_types:
            value_names.append("Min" + e.code)
            value_names.append("Max" + e.code)
            value_values["Min" + e.code] = e.min
            value_values["Max" + e.code] = e.max

        """
        These tests are changed or obsoleted by new enum coercion rules
        for enum in enum_types:
            for name in value_names:
                val = value_values[name]
                if hasattr(enum.enum, name):
                    for test in enum_types:
                        func = test.test
                        ev = getattr(enum.enum, name)
                        if test.min <= val and val <= test.max:
                            func(ev)
                        else:
                            try:
                                func(ev)
                            except:
                                pass
                            else:
                                self.assertTrue(False)
                        EnumTest.TestEnumBoolean(ev)
        """

        self.assertEqual(int(Single.Parse("3.14159")), 3)

    #TODO: @skip("interpreted") #Too slow
    def test_operators(self):
        def operator_add(a, b) :
            return a + b

        def test_add(a,b,c):
            self.assertTrue(c == b + a)
            self.assertTrue(a + b == c)
            self.assertTrue(c - a == b)
            self.assertTrue(c - b == a)

        def operator_sub(a, b) :
            return a - b

        def test_sub(a,b,c):
            self.assertTrue(c == -(b - a))
            self.assertTrue(c == a - b)
            self.assertTrue(a == b + c)
            self.assertTrue(b == a - c)

        def operator_mul(a, b) :
            return a * b

        def test_mul(a,b,c):
            self.assertTrue(c == a * b)
            self.assertTrue(c == b * a)
            if a != 0:
                self.assertTrue(b == c // a)
            if b != 0:
                self.assertTrue(a == c // b)

        def operator_div(a, b) :
            if b != 0:
                return a // b

        def test_div(a,b,c):
            if b != 0:
                #print(a,b,c)
                self.assertTrue(a // b == c, '%s == %s' % (a//b, c))
                self.assertTrue(((c * b) + (a % b)) == a)

        def operator_mod(a, b) :
            if b != 0:
                return a % b

        def test_mod(a,b,c):
            if b != 0:
                self.assertTrue(a % b == c)
                self.assertTrue((a // b) * b + c == a)
                self.assertTrue((a - c) % b == 0)

        def operator_and(a, b) :
            return a & b

        def test_and(a,b,c):
            self.assertTrue(a & b == c)
            self.assertTrue(b & a == c)

        def operator_or(a, b) :
            return a | b

        def test_or(a,b,c):
            self.assertTrue(a | b == c)
            self.assertTrue(b | a == c)

        def operator_xor(a, b) :
            return a ^ b

        def test_xor(a,b,c):
            self.assertTrue(a ^ b == c)
            self.assertTrue(b ^ a == c)

        pats = [big(0), big(1), big(42), big(0x7fffffff), big(0x80000000), big(0xabcdef01), big(0xffffffff)]
        nums = []
        for p0 in pats:
            for p1 in pats:
                #for p2 in pats:
                    n = p0+(p1<<32)
                    nums.append(n)
                    nums.append(-n)

        bignums = []
        for p0 in pats:
            for p1 in pats:
                for p2 in pats:
                    n = p0+(p1<<32)+(p2<<64)
                    bignums.append(n)
                    bignums.append(-n)

        ops = [
            ('/', operator_div, test_div),
            ('+', operator_add, test_add),
            ('-', operator_sub, test_sub),
            ('*', operator_mul, test_mul),
            ('%', operator_mod, test_mod),
            ('&', operator_and, test_and),
            ('|', operator_or,  test_or),
            ('^', operator_xor, test_xor),
        ]

        def test_it_all(nums):
            for sym, op, test in ops:
                for x in nums:
                    for y in nums:
                        z = op(x, y)
                        try:
                            test(x,y,z)
                        except get_builtins_dict()['Exception'] as e:
                            print(x, " ", sym, " ", y, " ", z, "Failed")
                            print(e)
                            raise

        test_it_all(bignums)
        test_it_all(nums)


    def scenarios_helper(self, templates, cmps, gbls, lcls):
        values = [3.5, 4.5, 4, 0, big(-200), 12345678901234567890]
        for l in values:
            for r in values:
                for t in templates:
                    for c in cmps:
                        easy = t % (l, c, r)
                        # need to compare the real values the classes hold,
                        # not the values we expect them to hold, incase truncation
                        # has occured
                        easy = easy.replace(')', ').value')
                        inst = t % (l, c, r)
                        #print inst, eval(easy), eval(inst)
                        self.assertTrue(eval(easy, gbls, lcls) == eval(inst, gbls, lcls), "%s == %s" % (easy, inst))

    def test_usertype_cd(self):
        """UserType: both C and D define __lt__"""
        class C(object):
            def __init__(self, value):
                self.value = value
            def __lt__(self, other):
                return self.value < other.value
        class D(object):
            def __init__(self, value):
                self.value = value
            def __lt__(self, other):
                return self.value < other.value
        class C2(C): pass
        class D2(D): pass
        self.scenarios_helper(templates1, ["<", ">"], globals(), locals())

    def test_usertype_c(self):
        """UserType: C defines __lt__, D does not"""
        class C(object):
            def __init__(self, value):
                self.value = value
            def __lt__(self, other):
                return self.value < other.value
        class D(object):
            def __init__(self, value):
                self.value = value
        class C2(C): pass
        class D2(D): pass
        self.scenarios_helper(templates2, ["<"], globals(), locals())

    @skipUnlessIronPython()
    def test_comparisions(self):
        from IronPythonTest import ComparisonTest

        def comparisons_helper(typeObj):
            def assertEqual(first, second):
                self.assertEqual(first,second)

            def assertTrue(arg):
                self.assertTrue(arg)

            class Callback:
                called = False
                def __call__(self, value):
                    #print value, expected
                    assertEqual(value, expected)
                    self.called = True
                def check(self):
                    assertTrue(self.called)
                    self.called = False

            cb = Callback()
            ComparisonTest.report = cb

            values = [3.5, 4.5, 4, 0]

            for l in values:
                for r in values:
                    ctl = typeObj(l)
                    ctr = typeObj(r)

                    self.assertEqual(str(ctl), "ct<%s>" % str(l))
                    self.assertEqual(str(ctr), "ct<%s>" % str(r))

                    expected = "< on [ct<%s>, ct<%s>]" % (l, r)
                    self.assertEqual(ctl < ctr, l < r)
                    cb.check()
                    expected = "> on [ct<%s>, ct<%s>]" % (l, r)
                    self.assertEqual(ctl > ctr, l > r)
                    cb.check()
                    expected = "<= on [ct<%s>, ct<%s>]" % (l, r)
                    self.assertEqual(ctl <= ctr, l <= r)
                    cb.check()
                    expected = ">= on [ct<%s>, ct<%s>]" % (l, r)
                    self.assertEqual(ctl >= ctr, l >= r)
                    cb.check()

        class ComparisonTest2(ComparisonTest): pass

        comparisons_helper(ComparisonTest)
        comparisons_helper(ComparisonTest2)

        class C:
            def __init__(self, value):
                self.value = value
            def __lt__(self, other):
                return self.value < other.value
            def __gt__(self, other):
                return self.value > other.value
        class C2(C): pass
        D = ComparisonTest
        D2 = ComparisonTest2
        self.scenarios_helper(templates1, ["<", ">"], globals(), locals())

        def cmp(a, b): return (a > b) - (a < b)

        ComparisonTest.report = None
        self.assertTrue(cmp(ComparisonTest(5), ComparisonTest(5)) == 0)
        self.assertTrue(cmp(ComparisonTest(5), ComparisonTest(8)) == -1)
        self.assertTrue(cmp(ComparisonTest2(50), ComparisonTest(8)) == 1)

    @skipUnlessIronPython()
    def test_ipt_integertest(self):

        def f():
            self.assertTrue(it.self.assertEqual(it.UInt32Int32MaxValue,it.uintT(it.Int32Int32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Int32MaxValue,it.ulongT(it.Int32Int32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Int32MaxValue,it.intT(it.Int32Int32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Int32MaxValue,it.longT(it.Int32Int32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int32Int32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Int32MinValue,it.intT(it.Int32Int32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Int32MinValue,it.longT(it.Int32Int32MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int32Int32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.Int32UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.Int32UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.Int32UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.Int32UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.Int32UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.Int32UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.Int32UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.Int32UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.Int32UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.Int32UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Int16MaxValue,it.uintT(it.Int32Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Int16MaxValue,it.ushortT(it.Int32Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Int16MaxValue,it.ulongT(it.Int32Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Int16MaxValue,it.intT(it.Int32Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Int16MaxValue,it.shortT(it.Int32Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Int16MaxValue,it.longT(it.Int32Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.CharInt16MaxValue,it.charT(it.Int32Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int32Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Int16MinValue,it.intT(it.Int32Int16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Int16MinValue,it.shortT(it.Int32Int16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Int16MinValue,it.longT(it.Int32Int16MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int32Int16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32CharMaxValue,it.uintT(it.Int32UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16CharMaxValue,it.ushortT(it.Int32UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64CharMaxValue,it.ulongT(it.Int32UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32CharMaxValue,it.intT(it.Int32UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64CharMaxValue,it.longT(it.Int32UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.CharCharMaxValue,it.charT(it.Int32UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int32UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.Int32UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.Int32UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.Int32UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.Int32UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.Int32UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.Int32UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.Int32UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.Int32UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.Int32UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.Int32UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.Int32UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.Int32UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.Int32UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.Int32UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.Int32UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.Int32UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.Int32UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.Int32UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.Int32UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.Int32UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32ByteMaxValue,it.uintT(it.Int32ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16ByteMaxValue,it.ushortT(it.Int32ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64ByteMaxValue,it.ulongT(it.Int32ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32ByteMaxValue,it.intT(it.Int32ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int16ByteMaxValue,it.shortT(it.Int32ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64ByteMaxValue,it.longT(it.Int32ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.ByteByteMaxValue,it.byteT(it.Int32ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.CharByteMaxValue,it.charT(it.Int32ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int32ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.Int32ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.Int32ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.Int32ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.Int32ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.Int32ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.Int32ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.Int32ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.Int32ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.Int32ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.Int32ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32SByteMaxValue,it.uintT(it.Int32SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16SByteMaxValue,it.ushortT(it.Int32SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64SByteMaxValue,it.ulongT(it.Int32SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32SByteMaxValue,it.intT(it.Int32SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int16SByteMaxValue,it.shortT(it.Int32SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64SByteMaxValue,it.longT(it.Int32SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.ByteSByteMaxValue,it.byteT(it.Int32SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.SByteSByteMaxValue,it.sbyteT(it.Int32SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.CharSByteMaxValue,it.charT(it.Int32SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int32SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32SByteMinValue,it.intT(it.Int32SByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16SByteMinValue,it.shortT(it.Int32SByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64SByteMinValue,it.longT(it.Int32SByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteSByteMinValue,it.sbyteT(it.Int32SByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int32SByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32CharMaxValue,it.uintT(it.Int32CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16CharMaxValue,it.ushortT(it.Int32CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64CharMaxValue,it.ulongT(it.Int32CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32CharMaxValue,it.intT(it.Int32CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64CharMaxValue,it.longT(it.Int32CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.CharCharMaxValue,it.charT(it.Int32CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int32CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.Int32CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.Int32CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.Int32CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.Int32CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.Int32CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.Int32CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.Int32CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.Int32CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.Int32CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.Int32CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val0,it.uintT(it.Int32Val0)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val0,it.ushortT(it.Int32Val0)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val0,it.ulongT(it.Int32Val0)))
            self.assertTrue(it.self.assertEqual(it.Int32Val0,it.intT(it.Int32Val0)))
            self.assertTrue(it.self.assertEqual(it.Int16Val0,it.shortT(it.Int32Val0)))
            self.assertTrue(it.self.assertEqual(it.Int64Val0,it.longT(it.Int32Val0)))
            self.assertTrue(it.self.assertEqual(it.ByteVal0,it.byteT(it.Int32Val0)))
            self.assertTrue(it.self.assertEqual(it.SByteVal0,it.sbyteT(it.Int32Val0)))
            self.assertTrue(it.self.assertEqual(it.CharVal0,it.charT(it.Int32Val0)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int32Val0)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val1,it.uintT(it.Int32Val1)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val1,it.ushortT(it.Int32Val1)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val1,it.ulongT(it.Int32Val1)))
            self.assertTrue(it.self.assertEqual(it.Int32Val1,it.intT(it.Int32Val1)))
            self.assertTrue(it.self.assertEqual(it.Int16Val1,it.shortT(it.Int32Val1)))
            self.assertTrue(it.self.assertEqual(it.Int64Val1,it.longT(it.Int32Val1)))
            self.assertTrue(it.self.assertEqual(it.ByteVal1,it.byteT(it.Int32Val1)))
            self.assertTrue(it.self.assertEqual(it.SByteVal1,it.sbyteT(it.Int32Val1)))
            self.assertTrue(it.self.assertEqual(it.CharVal1,it.charT(it.Int32Val1)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int32Val1)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val2,it.uintT(it.Int32Val2)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val2,it.ushortT(it.Int32Val2)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val2,it.ulongT(it.Int32Val2)))
            self.assertTrue(it.self.assertEqual(it.Int32Val2,it.intT(it.Int32Val2)))
            self.assertTrue(it.self.assertEqual(it.Int16Val2,it.shortT(it.Int32Val2)))
            self.assertTrue(it.self.assertEqual(it.Int64Val2,it.longT(it.Int32Val2)))
            self.assertTrue(it.self.assertEqual(it.ByteVal2,it.byteT(it.Int32Val2)))
            self.assertTrue(it.self.assertEqual(it.SByteVal2,it.sbyteT(it.Int32Val2)))
            self.assertTrue(it.self.assertEqual(it.CharVal2,it.charT(it.Int32Val2)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int32Val2)))
            self.assertTrue(it.self.assertEqual(it.Int32Val3,it.intT(it.Int32Val3)))
            self.assertTrue(it.self.assertEqual(it.Int16Val3,it.shortT(it.Int32Val3)))
            self.assertTrue(it.self.assertEqual(it.Int64Val3,it.longT(it.Int32Val3)))
            self.assertTrue(it.self.assertEqual(it.SByteVal3,it.sbyteT(it.Int32Val3)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int32Val3)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.Int32Val6)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.Int32Val6)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.Int32Val6)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.Int32Val6)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.Int32Val6)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.Int32Val6)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.Int32Val6)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.Int32Val6)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.Int32Val6)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.Int32Val6)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val7,it.uintT(it.Int32Val7)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val7,it.ushortT(it.Int32Val7)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val7,it.ulongT(it.Int32Val7)))
            self.assertTrue(it.self.assertEqual(it.Int32Val7,it.intT(it.Int32Val7)))
            self.assertTrue(it.self.assertEqual(it.Int16Val7,it.shortT(it.Int32Val7)))
            self.assertTrue(it.self.assertEqual(it.Int64Val7,it.longT(it.Int32Val7)))
            self.assertTrue(it.self.assertEqual(it.ByteVal7,it.byteT(it.Int32Val7)))
            self.assertTrue(it.self.assertEqual(it.SByteVal7,it.sbyteT(it.Int32Val7)))
            self.assertTrue(it.self.assertEqual(it.CharVal7,it.charT(it.Int32Val7)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int32Val7)))
            self.assertTrue(it.self.assertEqual(it.Int32Val8,it.intT(it.Int32Val8)))
            self.assertTrue(it.self.assertEqual(it.Int16Val8,it.shortT(it.Int32Val8)))
            self.assertTrue(it.self.assertEqual(it.Int64Val8,it.longT(it.Int32Val8)))
            self.assertTrue(it.self.assertEqual(it.SByteVal8,it.sbyteT(it.Int32Val8)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int32Val8)))
            self.assertTrue(it.self.assertEqual(it.UInt32Int32MaxValue,it.uintT(it.UInt32Int32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Int32MaxValue,it.ulongT(it.UInt32Int32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Int32MaxValue,it.intT(it.UInt32Int32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Int32MaxValue,it.longT(it.UInt32Int32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt32Int32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32UInt32MaxValue,it.uintT(it.UInt32UInt32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64UInt32MaxValue,it.ulongT(it.UInt32UInt32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64UInt32MaxValue,it.longT(it.UInt32UInt32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt32UInt32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.UInt32UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.UInt32UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.UInt32UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.UInt32UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.UInt32UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.UInt32UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.UInt32UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.UInt32UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.UInt32UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.UInt32UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Int16MaxValue,it.uintT(it.UInt32Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Int16MaxValue,it.ushortT(it.UInt32Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Int16MaxValue,it.ulongT(it.UInt32Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Int16MaxValue,it.intT(it.UInt32Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Int16MaxValue,it.shortT(it.UInt32Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Int16MaxValue,it.longT(it.UInt32Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.CharInt16MaxValue,it.charT(it.UInt32Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt32Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32CharMaxValue,it.uintT(it.UInt32UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16CharMaxValue,it.ushortT(it.UInt32UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64CharMaxValue,it.ulongT(it.UInt32UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32CharMaxValue,it.intT(it.UInt32UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64CharMaxValue,it.longT(it.UInt32UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.CharCharMaxValue,it.charT(it.UInt32UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt32UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.UInt32UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.UInt32UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.UInt32UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.UInt32UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.UInt32UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.UInt32UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.UInt32UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.UInt32UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.UInt32UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.UInt32UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.UInt32UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.UInt32UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.UInt32UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.UInt32UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.UInt32UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.UInt32UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.UInt32UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.UInt32UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.UInt32UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.UInt32UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32ByteMaxValue,it.uintT(it.UInt32ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16ByteMaxValue,it.ushortT(it.UInt32ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64ByteMaxValue,it.ulongT(it.UInt32ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32ByteMaxValue,it.intT(it.UInt32ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int16ByteMaxValue,it.shortT(it.UInt32ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64ByteMaxValue,it.longT(it.UInt32ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.ByteByteMaxValue,it.byteT(it.UInt32ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.CharByteMaxValue,it.charT(it.UInt32ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt32ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.UInt32ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.UInt32ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.UInt32ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.UInt32ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.UInt32ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.UInt32ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.UInt32ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.UInt32ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.UInt32ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.UInt32ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32SByteMaxValue,it.uintT(it.UInt32SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16SByteMaxValue,it.ushortT(it.UInt32SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64SByteMaxValue,it.ulongT(it.UInt32SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32SByteMaxValue,it.intT(it.UInt32SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int16SByteMaxValue,it.shortT(it.UInt32SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64SByteMaxValue,it.longT(it.UInt32SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.ByteSByteMaxValue,it.byteT(it.UInt32SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.SByteSByteMaxValue,it.sbyteT(it.UInt32SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.CharSByteMaxValue,it.charT(it.UInt32SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt32SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32CharMaxValue,it.uintT(it.UInt32CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16CharMaxValue,it.ushortT(it.UInt32CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64CharMaxValue,it.ulongT(it.UInt32CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32CharMaxValue,it.intT(it.UInt32CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64CharMaxValue,it.longT(it.UInt32CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.CharCharMaxValue,it.charT(it.UInt32CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt32CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.UInt32CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.UInt32CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.UInt32CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.UInt32CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.UInt32CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.UInt32CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.UInt32CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.UInt32CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.UInt32CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.UInt32CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val0,it.uintT(it.UInt32Val0)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val0,it.ushortT(it.UInt32Val0)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val0,it.ulongT(it.UInt32Val0)))
            self.assertTrue(it.self.assertEqual(it.Int32Val0,it.intT(it.UInt32Val0)))
            self.assertTrue(it.self.assertEqual(it.Int16Val0,it.shortT(it.UInt32Val0)))
            self.assertTrue(it.self.assertEqual(it.Int64Val0,it.longT(it.UInt32Val0)))
            self.assertTrue(it.self.assertEqual(it.ByteVal0,it.byteT(it.UInt32Val0)))
            self.assertTrue(it.self.assertEqual(it.SByteVal0,it.sbyteT(it.UInt32Val0)))
            self.assertTrue(it.self.assertEqual(it.CharVal0,it.charT(it.UInt32Val0)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt32Val0)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val1,it.uintT(it.UInt32Val1)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val1,it.ushortT(it.UInt32Val1)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val1,it.ulongT(it.UInt32Val1)))
            self.assertTrue(it.self.assertEqual(it.Int32Val1,it.intT(it.UInt32Val1)))
            self.assertTrue(it.self.assertEqual(it.Int16Val1,it.shortT(it.UInt32Val1)))
            self.assertTrue(it.self.assertEqual(it.Int64Val1,it.longT(it.UInt32Val1)))
            self.assertTrue(it.self.assertEqual(it.ByteVal1,it.byteT(it.UInt32Val1)))
            self.assertTrue(it.self.assertEqual(it.SByteVal1,it.sbyteT(it.UInt32Val1)))
            self.assertTrue(it.self.assertEqual(it.CharVal1,it.charT(it.UInt32Val1)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt32Val1)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val2,it.uintT(it.UInt32Val2)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val2,it.ushortT(it.UInt32Val2)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val2,it.ulongT(it.UInt32Val2)))
            self.assertTrue(it.self.assertEqual(it.Int32Val2,it.intT(it.UInt32Val2)))
            self.assertTrue(it.self.assertEqual(it.Int16Val2,it.shortT(it.UInt32Val2)))
            self.assertTrue(it.self.assertEqual(it.Int64Val2,it.longT(it.UInt32Val2)))
            self.assertTrue(it.self.assertEqual(it.ByteVal2,it.byteT(it.UInt32Val2)))
            self.assertTrue(it.self.assertEqual(it.SByteVal2,it.sbyteT(it.UInt32Val2)))
            self.assertTrue(it.self.assertEqual(it.CharVal2,it.charT(it.UInt32Val2)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt32Val2)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.UInt32Val6)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.UInt32Val6)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.UInt32Val6)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.UInt32Val6)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.UInt32Val6)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.UInt32Val6)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.UInt32Val6)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.UInt32Val6)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.UInt32Val6)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.UInt32Val6)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val7,it.uintT(it.UInt32Val7)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val7,it.ushortT(it.UInt32Val7)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val7,it.ulongT(it.UInt32Val7)))
            self.assertTrue(it.self.assertEqual(it.Int32Val7,it.intT(it.UInt32Val7)))
            self.assertTrue(it.self.assertEqual(it.Int16Val7,it.shortT(it.UInt32Val7)))
            self.assertTrue(it.self.assertEqual(it.Int64Val7,it.longT(it.UInt32Val7)))
            self.assertTrue(it.self.assertEqual(it.ByteVal7,it.byteT(it.UInt32Val7)))
            self.assertTrue(it.self.assertEqual(it.SByteVal7,it.sbyteT(it.UInt32Val7)))
            self.assertTrue(it.self.assertEqual(it.CharVal7,it.charT(it.UInt32Val7)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt32Val7)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.Int16UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.Int16UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.Int16UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.Int16UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.Int16UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.Int16UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.Int16UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.Int16UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.Int16UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.Int16UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Int16MaxValue,it.uintT(it.Int16Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Int16MaxValue,it.ushortT(it.Int16Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Int16MaxValue,it.ulongT(it.Int16Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Int16MaxValue,it.intT(it.Int16Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Int16MaxValue,it.shortT(it.Int16Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Int16MaxValue,it.longT(it.Int16Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.CharInt16MaxValue,it.charT(it.Int16Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int16Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Int16MinValue,it.intT(it.Int16Int16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Int16MinValue,it.shortT(it.Int16Int16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Int16MinValue,it.longT(it.Int16Int16MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int16Int16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.Int16UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.Int16UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.Int16UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.Int16UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.Int16UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.Int16UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.Int16UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.Int16UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.Int16UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.Int16UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.Int16UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.Int16UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.Int16UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.Int16UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.Int16UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.Int16UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.Int16UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.Int16UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.Int16UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.Int16UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32ByteMaxValue,it.uintT(it.Int16ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16ByteMaxValue,it.ushortT(it.Int16ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64ByteMaxValue,it.ulongT(it.Int16ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32ByteMaxValue,it.intT(it.Int16ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int16ByteMaxValue,it.shortT(it.Int16ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64ByteMaxValue,it.longT(it.Int16ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.ByteByteMaxValue,it.byteT(it.Int16ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.CharByteMaxValue,it.charT(it.Int16ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int16ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.Int16ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.Int16ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.Int16ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.Int16ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.Int16ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.Int16ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.Int16ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.Int16ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.Int16ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.Int16ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32SByteMaxValue,it.uintT(it.Int16SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16SByteMaxValue,it.ushortT(it.Int16SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64SByteMaxValue,it.ulongT(it.Int16SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32SByteMaxValue,it.intT(it.Int16SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int16SByteMaxValue,it.shortT(it.Int16SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64SByteMaxValue,it.longT(it.Int16SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.ByteSByteMaxValue,it.byteT(it.Int16SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.SByteSByteMaxValue,it.sbyteT(it.Int16SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.CharSByteMaxValue,it.charT(it.Int16SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int16SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32SByteMinValue,it.intT(it.Int16SByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16SByteMinValue,it.shortT(it.Int16SByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64SByteMinValue,it.longT(it.Int16SByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteSByteMinValue,it.sbyteT(it.Int16SByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int16SByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.Int16CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.Int16CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.Int16CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.Int16CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.Int16CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.Int16CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.Int16CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.Int16CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.Int16CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.Int16CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val0,it.uintT(it.Int16Val0)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val0,it.ushortT(it.Int16Val0)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val0,it.ulongT(it.Int16Val0)))
            self.assertTrue(it.self.assertEqual(it.Int32Val0,it.intT(it.Int16Val0)))
            self.assertTrue(it.self.assertEqual(it.Int16Val0,it.shortT(it.Int16Val0)))
            self.assertTrue(it.self.assertEqual(it.Int64Val0,it.longT(it.Int16Val0)))
            self.assertTrue(it.self.assertEqual(it.ByteVal0,it.byteT(it.Int16Val0)))
            self.assertTrue(it.self.assertEqual(it.SByteVal0,it.sbyteT(it.Int16Val0)))
            self.assertTrue(it.self.assertEqual(it.CharVal0,it.charT(it.Int16Val0)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int16Val0)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val1,it.uintT(it.Int16Val1)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val1,it.ushortT(it.Int16Val1)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val1,it.ulongT(it.Int16Val1)))
            self.assertTrue(it.self.assertEqual(it.Int32Val1,it.intT(it.Int16Val1)))
            self.assertTrue(it.self.assertEqual(it.Int16Val1,it.shortT(it.Int16Val1)))
            self.assertTrue(it.self.assertEqual(it.Int64Val1,it.longT(it.Int16Val1)))
            self.assertTrue(it.self.assertEqual(it.ByteVal1,it.byteT(it.Int16Val1)))
            self.assertTrue(it.self.assertEqual(it.SByteVal1,it.sbyteT(it.Int16Val1)))
            self.assertTrue(it.self.assertEqual(it.CharVal1,it.charT(it.Int16Val1)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int16Val1)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val2,it.uintT(it.Int16Val2)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val2,it.ushortT(it.Int16Val2)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val2,it.ulongT(it.Int16Val2)))
            self.assertTrue(it.self.assertEqual(it.Int32Val2,it.intT(it.Int16Val2)))
            self.assertTrue(it.self.assertEqual(it.Int16Val2,it.shortT(it.Int16Val2)))
            self.assertTrue(it.self.assertEqual(it.Int64Val2,it.longT(it.Int16Val2)))
            self.assertTrue(it.self.assertEqual(it.ByteVal2,it.byteT(it.Int16Val2)))
            self.assertTrue(it.self.assertEqual(it.SByteVal2,it.sbyteT(it.Int16Val2)))
            self.assertTrue(it.self.assertEqual(it.CharVal2,it.charT(it.Int16Val2)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int16Val2)))
            self.assertTrue(it.self.assertEqual(it.Int32Val3,it.intT(it.Int16Val3)))
            self.assertTrue(it.self.assertEqual(it.Int16Val3,it.shortT(it.Int16Val3)))
            self.assertTrue(it.self.assertEqual(it.Int64Val3,it.longT(it.Int16Val3)))
            self.assertTrue(it.self.assertEqual(it.SByteVal3,it.sbyteT(it.Int16Val3)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int16Val3)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.Int16Val6)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.Int16Val6)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.Int16Val6)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.Int16Val6)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.Int16Val6)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.Int16Val6)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.Int16Val6)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.Int16Val6)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.Int16Val6)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.Int16Val6)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val7,it.uintT(it.Int16Val7)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val7,it.ushortT(it.Int16Val7)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val7,it.ulongT(it.Int16Val7)))
            self.assertTrue(it.self.assertEqual(it.Int32Val7,it.intT(it.Int16Val7)))
            self.assertTrue(it.self.assertEqual(it.Int16Val7,it.shortT(it.Int16Val7)))
            self.assertTrue(it.self.assertEqual(it.Int64Val7,it.longT(it.Int16Val7)))
            self.assertTrue(it.self.assertEqual(it.ByteVal7,it.byteT(it.Int16Val7)))
            self.assertTrue(it.self.assertEqual(it.SByteVal7,it.sbyteT(it.Int16Val7)))
            self.assertTrue(it.self.assertEqual(it.CharVal7,it.charT(it.Int16Val7)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int16Val7)))
            self.assertTrue(it.self.assertEqual(it.Int32Val8,it.intT(it.Int16Val8)))
            self.assertTrue(it.self.assertEqual(it.Int16Val8,it.shortT(it.Int16Val8)))
            self.assertTrue(it.self.assertEqual(it.Int64Val8,it.longT(it.Int16Val8)))
            self.assertTrue(it.self.assertEqual(it.SByteVal8,it.sbyteT(it.Int16Val8)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int16Val8)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.UInt16UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.UInt16UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.UInt16UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.UInt16UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.UInt16UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.UInt16UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.UInt16UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.UInt16UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.UInt16UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.UInt16UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Int16MaxValue,it.uintT(it.UInt16Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Int16MaxValue,it.ushortT(it.UInt16Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Int16MaxValue,it.ulongT(it.UInt16Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Int16MaxValue,it.intT(it.UInt16Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Int16MaxValue,it.shortT(it.UInt16Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Int16MaxValue,it.longT(it.UInt16Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.CharInt16MaxValue,it.charT(it.UInt16Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt16Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32CharMaxValue,it.uintT(it.UInt16UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16CharMaxValue,it.ushortT(it.UInt16UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64CharMaxValue,it.ulongT(it.UInt16UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32CharMaxValue,it.intT(it.UInt16UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64CharMaxValue,it.longT(it.UInt16UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.CharCharMaxValue,it.charT(it.UInt16UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt16UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.UInt16UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.UInt16UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.UInt16UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.UInt16UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.UInt16UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.UInt16UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.UInt16UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.UInt16UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.UInt16UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.UInt16UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.UInt16UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.UInt16UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.UInt16UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.UInt16UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.UInt16UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.UInt16UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.UInt16UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.UInt16UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.UInt16UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.UInt16UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32ByteMaxValue,it.uintT(it.UInt16ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16ByteMaxValue,it.ushortT(it.UInt16ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64ByteMaxValue,it.ulongT(it.UInt16ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32ByteMaxValue,it.intT(it.UInt16ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int16ByteMaxValue,it.shortT(it.UInt16ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64ByteMaxValue,it.longT(it.UInt16ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.ByteByteMaxValue,it.byteT(it.UInt16ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.CharByteMaxValue,it.charT(it.UInt16ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt16ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.UInt16ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.UInt16ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.UInt16ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.UInt16ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.UInt16ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.UInt16ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.UInt16ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.UInt16ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.UInt16ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.UInt16ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32SByteMaxValue,it.uintT(it.UInt16SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16SByteMaxValue,it.ushortT(it.UInt16SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64SByteMaxValue,it.ulongT(it.UInt16SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32SByteMaxValue,it.intT(it.UInt16SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int16SByteMaxValue,it.shortT(it.UInt16SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64SByteMaxValue,it.longT(it.UInt16SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.ByteSByteMaxValue,it.byteT(it.UInt16SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.SByteSByteMaxValue,it.sbyteT(it.UInt16SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.CharSByteMaxValue,it.charT(it.UInt16SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt16SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32CharMaxValue,it.uintT(it.UInt16CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16CharMaxValue,it.ushortT(it.UInt16CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64CharMaxValue,it.ulongT(it.UInt16CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32CharMaxValue,it.intT(it.UInt16CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64CharMaxValue,it.longT(it.UInt16CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.CharCharMaxValue,it.charT(it.UInt16CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt16CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.UInt16CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.UInt16CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.UInt16CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.UInt16CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.UInt16CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.UInt16CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.UInt16CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.UInt16CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.UInt16CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.UInt16CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val0,it.uintT(it.UInt16Val0)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val0,it.ushortT(it.UInt16Val0)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val0,it.ulongT(it.UInt16Val0)))
            self.assertTrue(it.self.assertEqual(it.Int32Val0,it.intT(it.UInt16Val0)))
            self.assertTrue(it.self.assertEqual(it.Int16Val0,it.shortT(it.UInt16Val0)))
            self.assertTrue(it.self.assertEqual(it.Int64Val0,it.longT(it.UInt16Val0)))
            self.assertTrue(it.self.assertEqual(it.ByteVal0,it.byteT(it.UInt16Val0)))
            self.assertTrue(it.self.assertEqual(it.SByteVal0,it.sbyteT(it.UInt16Val0)))
            self.assertTrue(it.self.assertEqual(it.CharVal0,it.charT(it.UInt16Val0)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt16Val0)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val1,it.uintT(it.UInt16Val1)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val1,it.ushortT(it.UInt16Val1)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val1,it.ulongT(it.UInt16Val1)))
            self.assertTrue(it.self.assertEqual(it.Int32Val1,it.intT(it.UInt16Val1)))
            self.assertTrue(it.self.assertEqual(it.Int16Val1,it.shortT(it.UInt16Val1)))
            self.assertTrue(it.self.assertEqual(it.Int64Val1,it.longT(it.UInt16Val1)))
            self.assertTrue(it.self.assertEqual(it.ByteVal1,it.byteT(it.UInt16Val1)))
            self.assertTrue(it.self.assertEqual(it.SByteVal1,it.sbyteT(it.UInt16Val1)))
            self.assertTrue(it.self.assertEqual(it.CharVal1,it.charT(it.UInt16Val1)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt16Val1)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val2,it.uintT(it.UInt16Val2)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val2,it.ushortT(it.UInt16Val2)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val2,it.ulongT(it.UInt16Val2)))
            self.assertTrue(it.self.assertEqual(it.Int32Val2,it.intT(it.UInt16Val2)))
            self.assertTrue(it.self.assertEqual(it.Int16Val2,it.shortT(it.UInt16Val2)))
            self.assertTrue(it.self.assertEqual(it.Int64Val2,it.longT(it.UInt16Val2)))
            self.assertTrue(it.self.assertEqual(it.ByteVal2,it.byteT(it.UInt16Val2)))
            self.assertTrue(it.self.assertEqual(it.SByteVal2,it.sbyteT(it.UInt16Val2)))
            self.assertTrue(it.self.assertEqual(it.CharVal2,it.charT(it.UInt16Val2)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt16Val2)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.UInt16Val6)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.UInt16Val6)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.UInt16Val6)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.UInt16Val6)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.UInt16Val6)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.UInt16Val6)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.UInt16Val6)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.UInt16Val6)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.UInt16Val6)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.UInt16Val6)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val7,it.uintT(it.UInt16Val7)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val7,it.ushortT(it.UInt16Val7)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val7,it.ulongT(it.UInt16Val7)))
            self.assertTrue(it.self.assertEqual(it.Int32Val7,it.intT(it.UInt16Val7)))
            self.assertTrue(it.self.assertEqual(it.Int16Val7,it.shortT(it.UInt16Val7)))
            self.assertTrue(it.self.assertEqual(it.Int64Val7,it.longT(it.UInt16Val7)))
            self.assertTrue(it.self.assertEqual(it.ByteVal7,it.byteT(it.UInt16Val7)))
            self.assertTrue(it.self.assertEqual(it.SByteVal7,it.sbyteT(it.UInt16Val7)))
            self.assertTrue(it.self.assertEqual(it.CharVal7,it.charT(it.UInt16Val7)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt16Val7)))
            self.assertTrue(it.self.assertEqual(it.UInt32Int32MaxValue,it.uintT(it.Int64Int32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Int32MaxValue,it.ulongT(it.Int64Int32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Int32MaxValue,it.intT(it.Int64Int32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Int32MaxValue,it.longT(it.Int64Int32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int64Int32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Int32MinValue,it.intT(it.Int64Int32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Int32MinValue,it.longT(it.Int64Int32MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int64Int32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32UInt32MaxValue,it.uintT(it.Int64UInt32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64UInt32MaxValue,it.ulongT(it.Int64UInt32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64UInt32MaxValue,it.longT(it.Int64UInt32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int64UInt32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.Int64UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.Int64UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.Int64UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.Int64UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.Int64UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.Int64UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.Int64UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.Int64UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.Int64UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.Int64UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Int16MaxValue,it.uintT(it.Int64Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Int16MaxValue,it.ushortT(it.Int64Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Int16MaxValue,it.ulongT(it.Int64Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Int16MaxValue,it.intT(it.Int64Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Int16MaxValue,it.shortT(it.Int64Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Int16MaxValue,it.longT(it.Int64Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.CharInt16MaxValue,it.charT(it.Int64Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int64Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Int16MinValue,it.intT(it.Int64Int16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Int16MinValue,it.shortT(it.Int64Int16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Int16MinValue,it.longT(it.Int64Int16MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int64Int16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32CharMaxValue,it.uintT(it.Int64UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16CharMaxValue,it.ushortT(it.Int64UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64CharMaxValue,it.ulongT(it.Int64UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32CharMaxValue,it.intT(it.Int64UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64CharMaxValue,it.longT(it.Int64UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.CharCharMaxValue,it.charT(it.Int64UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int64UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.Int64UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.Int64UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.Int64UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.Int64UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.Int64UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.Int64UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.Int64UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.Int64UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.Int64UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.Int64UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Int64MaxValue,it.ulongT(it.Int64Int64MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Int64MaxValue,it.longT(it.Int64Int64MaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int64Int64MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Int64MinValue,it.longT(it.Int64Int64MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int64Int64MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.Int64UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.Int64UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.Int64UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.Int64UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.Int64UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.Int64UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.Int64UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.Int64UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.Int64UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.Int64UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32ByteMaxValue,it.uintT(it.Int64ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16ByteMaxValue,it.ushortT(it.Int64ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64ByteMaxValue,it.ulongT(it.Int64ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32ByteMaxValue,it.intT(it.Int64ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int16ByteMaxValue,it.shortT(it.Int64ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64ByteMaxValue,it.longT(it.Int64ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.ByteByteMaxValue,it.byteT(it.Int64ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.CharByteMaxValue,it.charT(it.Int64ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int64ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.Int64ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.Int64ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.Int64ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.Int64ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.Int64ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.Int64ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.Int64ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.Int64ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.Int64ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.Int64ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32SByteMaxValue,it.uintT(it.Int64SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16SByteMaxValue,it.ushortT(it.Int64SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64SByteMaxValue,it.ulongT(it.Int64SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32SByteMaxValue,it.intT(it.Int64SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int16SByteMaxValue,it.shortT(it.Int64SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64SByteMaxValue,it.longT(it.Int64SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.ByteSByteMaxValue,it.byteT(it.Int64SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.SByteSByteMaxValue,it.sbyteT(it.Int64SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.CharSByteMaxValue,it.charT(it.Int64SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int64SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32SByteMinValue,it.intT(it.Int64SByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16SByteMinValue,it.shortT(it.Int64SByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64SByteMinValue,it.longT(it.Int64SByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteSByteMinValue,it.sbyteT(it.Int64SByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int64SByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32CharMaxValue,it.uintT(it.Int64CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16CharMaxValue,it.ushortT(it.Int64CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64CharMaxValue,it.ulongT(it.Int64CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32CharMaxValue,it.intT(it.Int64CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64CharMaxValue,it.longT(it.Int64CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.CharCharMaxValue,it.charT(it.Int64CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int64CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.Int64CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.Int64CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.Int64CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.Int64CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.Int64CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.Int64CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.Int64CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.Int64CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.Int64CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.Int64CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val0,it.uintT(it.Int64Val0)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val0,it.ushortT(it.Int64Val0)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val0,it.ulongT(it.Int64Val0)))
            self.assertTrue(it.self.assertEqual(it.Int32Val0,it.intT(it.Int64Val0)))
            self.assertTrue(it.self.assertEqual(it.Int16Val0,it.shortT(it.Int64Val0)))
            self.assertTrue(it.self.assertEqual(it.Int64Val0,it.longT(it.Int64Val0)))
            self.assertTrue(it.self.assertEqual(it.ByteVal0,it.byteT(it.Int64Val0)))
            self.assertTrue(it.self.assertEqual(it.SByteVal0,it.sbyteT(it.Int64Val0)))
            self.assertTrue(it.self.assertEqual(it.CharVal0,it.charT(it.Int64Val0)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int64Val0)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val1,it.uintT(it.Int64Val1)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val1,it.ushortT(it.Int64Val1)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val1,it.ulongT(it.Int64Val1)))
            self.assertTrue(it.self.assertEqual(it.Int32Val1,it.intT(it.Int64Val1)))
            self.assertTrue(it.self.assertEqual(it.Int16Val1,it.shortT(it.Int64Val1)))
            self.assertTrue(it.self.assertEqual(it.Int64Val1,it.longT(it.Int64Val1)))
            self.assertTrue(it.self.assertEqual(it.ByteVal1,it.byteT(it.Int64Val1)))
            self.assertTrue(it.self.assertEqual(it.SByteVal1,it.sbyteT(it.Int64Val1)))
            self.assertTrue(it.self.assertEqual(it.CharVal1,it.charT(it.Int64Val1)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int64Val1)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val2,it.uintT(it.Int64Val2)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val2,it.ushortT(it.Int64Val2)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val2,it.ulongT(it.Int64Val2)))
            self.assertTrue(it.self.assertEqual(it.Int32Val2,it.intT(it.Int64Val2)))
            self.assertTrue(it.self.assertEqual(it.Int16Val2,it.shortT(it.Int64Val2)))
            self.assertTrue(it.self.assertEqual(it.Int64Val2,it.longT(it.Int64Val2)))
            self.assertTrue(it.self.assertEqual(it.ByteVal2,it.byteT(it.Int64Val2)))
            self.assertTrue(it.self.assertEqual(it.SByteVal2,it.sbyteT(it.Int64Val2)))
            self.assertTrue(it.self.assertEqual(it.CharVal2,it.charT(it.Int64Val2)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int64Val2)))
            self.assertTrue(it.self.assertEqual(it.Int32Val3,it.intT(it.Int64Val3)))
            self.assertTrue(it.self.assertEqual(it.Int16Val3,it.shortT(it.Int64Val3)))
            self.assertTrue(it.self.assertEqual(it.Int64Val3,it.longT(it.Int64Val3)))
            self.assertTrue(it.self.assertEqual(it.SByteVal3,it.sbyteT(it.Int64Val3)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int64Val3)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.Int64Val6)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.Int64Val6)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.Int64Val6)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.Int64Val6)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.Int64Val6)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.Int64Val6)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.Int64Val6)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.Int64Val6)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.Int64Val6)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.Int64Val6)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val7,it.uintT(it.Int64Val7)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val7,it.ushortT(it.Int64Val7)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val7,it.ulongT(it.Int64Val7)))
            self.assertTrue(it.self.assertEqual(it.Int32Val7,it.intT(it.Int64Val7)))
            self.assertTrue(it.self.assertEqual(it.Int16Val7,it.shortT(it.Int64Val7)))
            self.assertTrue(it.self.assertEqual(it.Int64Val7,it.longT(it.Int64Val7)))
            self.assertTrue(it.self.assertEqual(it.ByteVal7,it.byteT(it.Int64Val7)))
            self.assertTrue(it.self.assertEqual(it.SByteVal7,it.sbyteT(it.Int64Val7)))
            self.assertTrue(it.self.assertEqual(it.CharVal7,it.charT(it.Int64Val7)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int64Val7)))
            self.assertTrue(it.self.assertEqual(it.Int32Val8,it.intT(it.Int64Val8)))
            self.assertTrue(it.self.assertEqual(it.Int16Val8,it.shortT(it.Int64Val8)))
            self.assertTrue(it.self.assertEqual(it.Int64Val8,it.longT(it.Int64Val8)))
            self.assertTrue(it.self.assertEqual(it.SByteVal8,it.sbyteT(it.Int64Val8)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.Int64Val8)))
            self.assertTrue(it.self.assertEqual(it.UInt32Int32MaxValue,it.uintT(it.UInt64Int32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Int32MaxValue,it.ulongT(it.UInt64Int32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Int32MaxValue,it.intT(it.UInt64Int32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Int32MaxValue,it.longT(it.UInt64Int32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt64Int32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32UInt32MaxValue,it.uintT(it.UInt64UInt32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64UInt32MaxValue,it.ulongT(it.UInt64UInt32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64UInt32MaxValue,it.longT(it.UInt64UInt32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt64UInt32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.UInt64UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.UInt64UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.UInt64UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.UInt64UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.UInt64UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.UInt64UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.UInt64UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.UInt64UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.UInt64UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.UInt64UInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Int16MaxValue,it.uintT(it.UInt64Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Int16MaxValue,it.ushortT(it.UInt64Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Int16MaxValue,it.ulongT(it.UInt64Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Int16MaxValue,it.intT(it.UInt64Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Int16MaxValue,it.shortT(it.UInt64Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Int16MaxValue,it.longT(it.UInt64Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.CharInt16MaxValue,it.charT(it.UInt64Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt64Int16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32CharMaxValue,it.uintT(it.UInt64UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16CharMaxValue,it.ushortT(it.UInt64UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64CharMaxValue,it.ulongT(it.UInt64UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32CharMaxValue,it.intT(it.UInt64UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64CharMaxValue,it.longT(it.UInt64UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.CharCharMaxValue,it.charT(it.UInt64UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt64UInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.UInt64UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.UInt64UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.UInt64UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.UInt64UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.UInt64UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.UInt64UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.UInt64UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.UInt64UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.UInt64UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.UInt64UInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Int64MaxValue,it.ulongT(it.UInt64Int64MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Int64MaxValue,it.longT(it.UInt64Int64MaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt64Int64MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64UInt64MaxValue,it.ulongT(it.UInt64UInt64MaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt64UInt64MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.UInt64UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.UInt64UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.UInt64UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.UInt64UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.UInt64UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.UInt64UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.UInt64UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.UInt64UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.UInt64UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.UInt64UInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32ByteMaxValue,it.uintT(it.UInt64ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16ByteMaxValue,it.ushortT(it.UInt64ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64ByteMaxValue,it.ulongT(it.UInt64ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32ByteMaxValue,it.intT(it.UInt64ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int16ByteMaxValue,it.shortT(it.UInt64ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64ByteMaxValue,it.longT(it.UInt64ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.ByteByteMaxValue,it.byteT(it.UInt64ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.CharByteMaxValue,it.charT(it.UInt64ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt64ByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.UInt64ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.UInt64ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.UInt64ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.UInt64ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.UInt64ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.UInt64ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.UInt64ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.UInt64ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.UInt64ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.UInt64ByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32SByteMaxValue,it.uintT(it.UInt64SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16SByteMaxValue,it.ushortT(it.UInt64SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64SByteMaxValue,it.ulongT(it.UInt64SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32SByteMaxValue,it.intT(it.UInt64SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int16SByteMaxValue,it.shortT(it.UInt64SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64SByteMaxValue,it.longT(it.UInt64SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.ByteSByteMaxValue,it.byteT(it.UInt64SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.SByteSByteMaxValue,it.sbyteT(it.UInt64SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.CharSByteMaxValue,it.charT(it.UInt64SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt64SByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32CharMaxValue,it.uintT(it.UInt64CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16CharMaxValue,it.ushortT(it.UInt64CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64CharMaxValue,it.ulongT(it.UInt64CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32CharMaxValue,it.intT(it.UInt64CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64CharMaxValue,it.longT(it.UInt64CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.CharCharMaxValue,it.charT(it.UInt64CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt64CharMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.UInt64CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.UInt64CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.UInt64CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.UInt64CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.UInt64CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.UInt64CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.UInt64CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.UInt64CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.UInt64CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.UInt64CharMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val0,it.uintT(it.UInt64Val0)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val0,it.ushortT(it.UInt64Val0)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val0,it.ulongT(it.UInt64Val0)))
            self.assertTrue(it.self.assertEqual(it.Int32Val0,it.intT(it.UInt64Val0)))
            self.assertTrue(it.self.assertEqual(it.Int16Val0,it.shortT(it.UInt64Val0)))
            self.assertTrue(it.self.assertEqual(it.Int64Val0,it.longT(it.UInt64Val0)))
            self.assertTrue(it.self.assertEqual(it.ByteVal0,it.byteT(it.UInt64Val0)))
            self.assertTrue(it.self.assertEqual(it.SByteVal0,it.sbyteT(it.UInt64Val0)))
            self.assertTrue(it.self.assertEqual(it.CharVal0,it.charT(it.UInt64Val0)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt64Val0)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val1,it.uintT(it.UInt64Val1)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val1,it.ushortT(it.UInt64Val1)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val1,it.ulongT(it.UInt64Val1)))
            self.assertTrue(it.self.assertEqual(it.Int32Val1,it.intT(it.UInt64Val1)))
            self.assertTrue(it.self.assertEqual(it.Int16Val1,it.shortT(it.UInt64Val1)))
            self.assertTrue(it.self.assertEqual(it.Int64Val1,it.longT(it.UInt64Val1)))
            self.assertTrue(it.self.assertEqual(it.ByteVal1,it.byteT(it.UInt64Val1)))
            self.assertTrue(it.self.assertEqual(it.SByteVal1,it.sbyteT(it.UInt64Val1)))
            self.assertTrue(it.self.assertEqual(it.CharVal1,it.charT(it.UInt64Val1)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt64Val1)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val2,it.uintT(it.UInt64Val2)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val2,it.ushortT(it.UInt64Val2)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val2,it.ulongT(it.UInt64Val2)))
            self.assertTrue(it.self.assertEqual(it.Int32Val2,it.intT(it.UInt64Val2)))
            self.assertTrue(it.self.assertEqual(it.Int16Val2,it.shortT(it.UInt64Val2)))
            self.assertTrue(it.self.assertEqual(it.Int64Val2,it.longT(it.UInt64Val2)))
            self.assertTrue(it.self.assertEqual(it.ByteVal2,it.byteT(it.UInt64Val2)))
            self.assertTrue(it.self.assertEqual(it.SByteVal2,it.sbyteT(it.UInt64Val2)))
            self.assertTrue(it.self.assertEqual(it.CharVal2,it.charT(it.UInt64Val2)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt64Val2)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.UInt64Val6)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.UInt64Val6)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.UInt64Val6)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.UInt64Val6)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.UInt64Val6)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.UInt64Val6)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.UInt64Val6)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.UInt64Val6)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.UInt64Val6)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.UInt64Val6)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val7,it.uintT(it.UInt64Val7)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val7,it.ushortT(it.UInt64Val7)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val7,it.ulongT(it.UInt64Val7)))
            self.assertTrue(it.self.assertEqual(it.Int32Val7,it.intT(it.UInt64Val7)))
            self.assertTrue(it.self.assertEqual(it.Int16Val7,it.shortT(it.UInt64Val7)))
            self.assertTrue(it.self.assertEqual(it.Int64Val7,it.longT(it.UInt64Val7)))
            self.assertTrue(it.self.assertEqual(it.ByteVal7,it.byteT(it.UInt64Val7)))
            self.assertTrue(it.self.assertEqual(it.SByteVal7,it.sbyteT(it.UInt64Val7)))
            self.assertTrue(it.self.assertEqual(it.CharVal7,it.charT(it.UInt64Val7)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.UInt64Val7)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.ByteUInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.ByteUInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.ByteUInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.ByteUInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.ByteUInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.ByteUInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.ByteUInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.ByteUInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.ByteUInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.ByteUInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.ByteUInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.ByteUInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.ByteUInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.ByteUInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.ByteUInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.ByteUInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.ByteUInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.ByteUInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.ByteUInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.ByteUInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.ByteUInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.ByteUInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.ByteUInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.ByteUInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.ByteUInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.ByteUInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.ByteUInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.ByteUInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.ByteUInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.ByteUInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32ByteMaxValue,it.uintT(it.ByteByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16ByteMaxValue,it.ushortT(it.ByteByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64ByteMaxValue,it.ulongT(it.ByteByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32ByteMaxValue,it.intT(it.ByteByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int16ByteMaxValue,it.shortT(it.ByteByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64ByteMaxValue,it.longT(it.ByteByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.ByteByteMaxValue,it.byteT(it.ByteByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.CharByteMaxValue,it.charT(it.ByteByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.ByteByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.ByteByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.ByteByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.ByteByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.ByteByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.ByteByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.ByteByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.ByteByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.ByteByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.ByteByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.ByteByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32SByteMaxValue,it.uintT(it.ByteSByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16SByteMaxValue,it.ushortT(it.ByteSByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64SByteMaxValue,it.ulongT(it.ByteSByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32SByteMaxValue,it.intT(it.ByteSByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int16SByteMaxValue,it.shortT(it.ByteSByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64SByteMaxValue,it.longT(it.ByteSByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.ByteSByteMaxValue,it.byteT(it.ByteSByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.SByteSByteMaxValue,it.sbyteT(it.ByteSByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.CharSByteMaxValue,it.charT(it.ByteSByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.ByteSByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.ByteCharMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.ByteCharMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.ByteCharMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.ByteCharMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.ByteCharMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.ByteCharMinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.ByteCharMinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.ByteCharMinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.ByteCharMinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.ByteCharMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val0,it.uintT(it.ByteVal0)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val0,it.ushortT(it.ByteVal0)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val0,it.ulongT(it.ByteVal0)))
            self.assertTrue(it.self.assertEqual(it.Int32Val0,it.intT(it.ByteVal0)))
            self.assertTrue(it.self.assertEqual(it.Int16Val0,it.shortT(it.ByteVal0)))
            self.assertTrue(it.self.assertEqual(it.Int64Val0,it.longT(it.ByteVal0)))
            self.assertTrue(it.self.assertEqual(it.ByteVal0,it.byteT(it.ByteVal0)))
            self.assertTrue(it.self.assertEqual(it.SByteVal0,it.sbyteT(it.ByteVal0)))
            self.assertTrue(it.self.assertEqual(it.CharVal0,it.charT(it.ByteVal0)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.ByteVal0)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val1,it.uintT(it.ByteVal1)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val1,it.ushortT(it.ByteVal1)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val1,it.ulongT(it.ByteVal1)))
            self.assertTrue(it.self.assertEqual(it.Int32Val1,it.intT(it.ByteVal1)))
            self.assertTrue(it.self.assertEqual(it.Int16Val1,it.shortT(it.ByteVal1)))
            self.assertTrue(it.self.assertEqual(it.Int64Val1,it.longT(it.ByteVal1)))
            self.assertTrue(it.self.assertEqual(it.ByteVal1,it.byteT(it.ByteVal1)))
            self.assertTrue(it.self.assertEqual(it.SByteVal1,it.sbyteT(it.ByteVal1)))
            self.assertTrue(it.self.assertEqual(it.CharVal1,it.charT(it.ByteVal1)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.ByteVal1)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val2,it.uintT(it.ByteVal2)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val2,it.ushortT(it.ByteVal2)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val2,it.ulongT(it.ByteVal2)))
            self.assertTrue(it.self.assertEqual(it.Int32Val2,it.intT(it.ByteVal2)))
            self.assertTrue(it.self.assertEqual(it.Int16Val2,it.shortT(it.ByteVal2)))
            self.assertTrue(it.self.assertEqual(it.Int64Val2,it.longT(it.ByteVal2)))
            self.assertTrue(it.self.assertEqual(it.ByteVal2,it.byteT(it.ByteVal2)))
            self.assertTrue(it.self.assertEqual(it.SByteVal2,it.sbyteT(it.ByteVal2)))
            self.assertTrue(it.self.assertEqual(it.CharVal2,it.charT(it.ByteVal2)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.ByteVal2)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.ByteVal6)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.ByteVal6)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.ByteVal6)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.ByteVal6)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.ByteVal6)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.ByteVal6)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.ByteVal6)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.ByteVal6)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.ByteVal6)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.ByteVal6)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val7,it.uintT(it.ByteVal7)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val7,it.ushortT(it.ByteVal7)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val7,it.ulongT(it.ByteVal7)))
            self.assertTrue(it.self.assertEqual(it.Int32Val7,it.intT(it.ByteVal7)))
            self.assertTrue(it.self.assertEqual(it.Int16Val7,it.shortT(it.ByteVal7)))
            self.assertTrue(it.self.assertEqual(it.Int64Val7,it.longT(it.ByteVal7)))
            self.assertTrue(it.self.assertEqual(it.ByteVal7,it.byteT(it.ByteVal7)))
            self.assertTrue(it.self.assertEqual(it.SByteVal7,it.sbyteT(it.ByteVal7)))
            self.assertTrue(it.self.assertEqual(it.CharVal7,it.charT(it.ByteVal7)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.ByteVal7)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.SByteUInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.SByteUInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.SByteUInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.SByteUInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.SByteUInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.SByteUInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.SByteUInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.SByteUInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.SByteUInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.SByteUInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.SByteUInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.SByteUInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.SByteUInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.SByteUInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.SByteUInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.SByteUInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.SByteUInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.SByteUInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.SByteUInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.SByteUInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.SByteUInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.SByteUInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.SByteUInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.SByteUInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.SByteUInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.SByteUInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.SByteUInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.SByteUInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.SByteUInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.SByteUInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.SByteByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.SByteByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.SByteByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.SByteByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.SByteByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.SByteByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.SByteByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.SByteByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.SByteByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.SByteByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32SByteMaxValue,it.uintT(it.SByteSByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16SByteMaxValue,it.ushortT(it.SByteSByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64SByteMaxValue,it.ulongT(it.SByteSByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32SByteMaxValue,it.intT(it.SByteSByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int16SByteMaxValue,it.shortT(it.SByteSByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64SByteMaxValue,it.longT(it.SByteSByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.ByteSByteMaxValue,it.byteT(it.SByteSByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.SByteSByteMaxValue,it.sbyteT(it.SByteSByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.CharSByteMaxValue,it.charT(it.SByteSByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.SByteSByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32SByteMinValue,it.intT(it.SByteSByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16SByteMinValue,it.shortT(it.SByteSByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64SByteMinValue,it.longT(it.SByteSByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteSByteMinValue,it.sbyteT(it.SByteSByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.SByteSByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.SByteCharMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.SByteCharMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.SByteCharMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.SByteCharMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.SByteCharMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.SByteCharMinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.SByteCharMinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.SByteCharMinValue)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.SByteCharMinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.SByteCharMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val0,it.uintT(it.SByteVal0)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val0,it.ushortT(it.SByteVal0)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val0,it.ulongT(it.SByteVal0)))
            self.assertTrue(it.self.assertEqual(it.Int32Val0,it.intT(it.SByteVal0)))
            self.assertTrue(it.self.assertEqual(it.Int16Val0,it.shortT(it.SByteVal0)))
            self.assertTrue(it.self.assertEqual(it.Int64Val0,it.longT(it.SByteVal0)))
            self.assertTrue(it.self.assertEqual(it.ByteVal0,it.byteT(it.SByteVal0)))
            self.assertTrue(it.self.assertEqual(it.SByteVal0,it.sbyteT(it.SByteVal0)))
            self.assertTrue(it.self.assertEqual(it.CharVal0,it.charT(it.SByteVal0)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.SByteVal0)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val1,it.uintT(it.SByteVal1)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val1,it.ushortT(it.SByteVal1)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val1,it.ulongT(it.SByteVal1)))
            self.assertTrue(it.self.assertEqual(it.Int32Val1,it.intT(it.SByteVal1)))
            self.assertTrue(it.self.assertEqual(it.Int16Val1,it.shortT(it.SByteVal1)))
            self.assertTrue(it.self.assertEqual(it.Int64Val1,it.longT(it.SByteVal1)))
            self.assertTrue(it.self.assertEqual(it.ByteVal1,it.byteT(it.SByteVal1)))
            self.assertTrue(it.self.assertEqual(it.SByteVal1,it.sbyteT(it.SByteVal1)))
            self.assertTrue(it.self.assertEqual(it.CharVal1,it.charT(it.SByteVal1)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.SByteVal1)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val2,it.uintT(it.SByteVal2)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val2,it.ushortT(it.SByteVal2)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val2,it.ulongT(it.SByteVal2)))
            self.assertTrue(it.self.assertEqual(it.Int32Val2,it.intT(it.SByteVal2)))
            self.assertTrue(it.self.assertEqual(it.Int16Val2,it.shortT(it.SByteVal2)))
            self.assertTrue(it.self.assertEqual(it.Int64Val2,it.longT(it.SByteVal2)))
            self.assertTrue(it.self.assertEqual(it.ByteVal2,it.byteT(it.SByteVal2)))
            self.assertTrue(it.self.assertEqual(it.SByteVal2,it.sbyteT(it.SByteVal2)))
            self.assertTrue(it.self.assertEqual(it.CharVal2,it.charT(it.SByteVal2)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.SByteVal2)))
            self.assertTrue(it.self.assertEqual(it.Int32Val3,it.intT(it.SByteVal3)))
            self.assertTrue(it.self.assertEqual(it.Int16Val3,it.shortT(it.SByteVal3)))
            self.assertTrue(it.self.assertEqual(it.Int64Val3,it.longT(it.SByteVal3)))
            self.assertTrue(it.self.assertEqual(it.SByteVal3,it.sbyteT(it.SByteVal3)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.SByteVal3)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.SByteVal6)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.SByteVal6)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.SByteVal6)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.SByteVal6)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.SByteVal6)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.SByteVal6)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.SByteVal6)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.SByteVal6)))
            self.assertTrue(it.self.assertEqual(it.CharVal6,it.charT(it.SByteVal6)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.SByteVal6)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val7,it.uintT(it.SByteVal7)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val7,it.ushortT(it.SByteVal7)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val7,it.ulongT(it.SByteVal7)))
            self.assertTrue(it.self.assertEqual(it.Int32Val7,it.intT(it.SByteVal7)))
            self.assertTrue(it.self.assertEqual(it.Int16Val7,it.shortT(it.SByteVal7)))
            self.assertTrue(it.self.assertEqual(it.Int64Val7,it.longT(it.SByteVal7)))
            self.assertTrue(it.self.assertEqual(it.ByteVal7,it.byteT(it.SByteVal7)))
            self.assertTrue(it.self.assertEqual(it.SByteVal7,it.sbyteT(it.SByteVal7)))
            self.assertTrue(it.self.assertEqual(it.CharVal7,it.charT(it.SByteVal7)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.SByteVal7)))
            self.assertTrue(it.self.assertEqual(it.Int32Val8,it.intT(it.SByteVal8)))
            self.assertTrue(it.self.assertEqual(it.Int16Val8,it.shortT(it.SByteVal8)))
            self.assertTrue(it.self.assertEqual(it.Int64Val8,it.longT(it.SByteVal8)))
            self.assertTrue(it.self.assertEqual(it.SByteVal8,it.sbyteT(it.SByteVal8)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.SByteVal8)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val7,it.uintT(it.BooleanInt32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val7,it.ushortT(it.BooleanInt32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val7,it.ulongT(it.BooleanInt32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val7,it.intT(it.BooleanInt32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val7,it.shortT(it.BooleanInt32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val7,it.longT(it.BooleanInt32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal7,it.byteT(it.BooleanInt32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal7,it.sbyteT(it.BooleanInt32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.BooleanInt32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val7,it.uintT(it.BooleanInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val7,it.ushortT(it.BooleanInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val7,it.ulongT(it.BooleanInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val7,it.intT(it.BooleanInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val7,it.shortT(it.BooleanInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val7,it.longT(it.BooleanInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal7,it.byteT(it.BooleanInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal7,it.sbyteT(it.BooleanInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.BooleanInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val7,it.uintT(it.BooleanUInt32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val7,it.ushortT(it.BooleanUInt32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val7,it.ulongT(it.BooleanUInt32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val7,it.intT(it.BooleanUInt32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val7,it.shortT(it.BooleanUInt32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val7,it.longT(it.BooleanUInt32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal7,it.byteT(it.BooleanUInt32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal7,it.sbyteT(it.BooleanUInt32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.BooleanUInt32MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.BooleanUInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.BooleanUInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.BooleanUInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.BooleanUInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.BooleanUInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.BooleanUInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.BooleanUInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.BooleanUInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.BooleanUInt32MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val7,it.uintT(it.BooleanInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val7,it.ushortT(it.BooleanInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val7,it.ulongT(it.BooleanInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val7,it.intT(it.BooleanInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val7,it.shortT(it.BooleanInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val7,it.longT(it.BooleanInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal7,it.byteT(it.BooleanInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal7,it.sbyteT(it.BooleanInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.BooleanInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val7,it.uintT(it.BooleanInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val7,it.ushortT(it.BooleanInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val7,it.ulongT(it.BooleanInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val7,it.intT(it.BooleanInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val7,it.shortT(it.BooleanInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val7,it.longT(it.BooleanInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal7,it.byteT(it.BooleanInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal7,it.sbyteT(it.BooleanInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.BooleanInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val7,it.uintT(it.BooleanUInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val7,it.ushortT(it.BooleanUInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val7,it.ulongT(it.BooleanUInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val7,it.intT(it.BooleanUInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val7,it.shortT(it.BooleanUInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val7,it.longT(it.BooleanUInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal7,it.byteT(it.BooleanUInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal7,it.sbyteT(it.BooleanUInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.BooleanUInt16MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.BooleanUInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.BooleanUInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.BooleanUInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.BooleanUInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.BooleanUInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.BooleanUInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.BooleanUInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.BooleanUInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.BooleanUInt16MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val7,it.uintT(it.BooleanInt64MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val7,it.ushortT(it.BooleanInt64MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val7,it.ulongT(it.BooleanInt64MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val7,it.intT(it.BooleanInt64MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val7,it.shortT(it.BooleanInt64MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val7,it.longT(it.BooleanInt64MaxValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal7,it.byteT(it.BooleanInt64MaxValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal7,it.sbyteT(it.BooleanInt64MaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.BooleanInt64MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val7,it.uintT(it.BooleanInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val7,it.ushortT(it.BooleanInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val7,it.ulongT(it.BooleanInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val7,it.intT(it.BooleanInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val7,it.shortT(it.BooleanInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val7,it.longT(it.BooleanInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal7,it.byteT(it.BooleanInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal7,it.sbyteT(it.BooleanInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.BooleanInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val7,it.uintT(it.BooleanUInt64MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val7,it.ushortT(it.BooleanUInt64MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val7,it.ulongT(it.BooleanUInt64MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val7,it.intT(it.BooleanUInt64MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val7,it.shortT(it.BooleanUInt64MaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val7,it.longT(it.BooleanUInt64MaxValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal7,it.byteT(it.BooleanUInt64MaxValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal7,it.sbyteT(it.BooleanUInt64MaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.BooleanUInt64MaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.BooleanUInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.BooleanUInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.BooleanUInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.BooleanUInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.BooleanUInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.BooleanUInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.BooleanUInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.BooleanUInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.BooleanUInt64MinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val7,it.uintT(it.BooleanByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val7,it.ushortT(it.BooleanByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val7,it.ulongT(it.BooleanByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val7,it.intT(it.BooleanByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val7,it.shortT(it.BooleanByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val7,it.longT(it.BooleanByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal7,it.byteT(it.BooleanByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal7,it.sbyteT(it.BooleanByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.BooleanByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.BooleanByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.BooleanByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.BooleanByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.BooleanByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.BooleanByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.BooleanByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.BooleanByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.BooleanByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.BooleanByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val7,it.uintT(it.BooleanSByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val7,it.ushortT(it.BooleanSByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val7,it.ulongT(it.BooleanSByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val7,it.intT(it.BooleanSByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val7,it.shortT(it.BooleanSByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val7,it.longT(it.BooleanSByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal7,it.byteT(it.BooleanSByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal7,it.sbyteT(it.BooleanSByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.BooleanSByteMaxValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val7,it.uintT(it.BooleanSByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val7,it.ushortT(it.BooleanSByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val7,it.ulongT(it.BooleanSByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int32Val7,it.intT(it.BooleanSByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int16Val7,it.shortT(it.BooleanSByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.Int64Val7,it.longT(it.BooleanSByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.ByteVal7,it.byteT(it.BooleanSByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.SByteVal7,it.sbyteT(it.BooleanSByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.BooleanSByteMinValue)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val7,it.uintT(it.BooleanVal1)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val7,it.ushortT(it.BooleanVal1)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val7,it.ulongT(it.BooleanVal1)))
            self.assertTrue(it.self.assertEqual(it.Int32Val7,it.intT(it.BooleanVal1)))
            self.assertTrue(it.self.assertEqual(it.Int16Val7,it.shortT(it.BooleanVal1)))
            self.assertTrue(it.self.assertEqual(it.Int64Val7,it.longT(it.BooleanVal1)))
            self.assertTrue(it.self.assertEqual(it.ByteVal7,it.byteT(it.BooleanVal1)))
            self.assertTrue(it.self.assertEqual(it.SByteVal7,it.sbyteT(it.BooleanVal1)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.BooleanVal1)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val7,it.uintT(it.BooleanVal2)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val7,it.ushortT(it.BooleanVal2)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val7,it.ulongT(it.BooleanVal2)))
            self.assertTrue(it.self.assertEqual(it.Int32Val7,it.intT(it.BooleanVal2)))
            self.assertTrue(it.self.assertEqual(it.Int16Val7,it.shortT(it.BooleanVal2)))
            self.assertTrue(it.self.assertEqual(it.Int64Val7,it.longT(it.BooleanVal2)))
            self.assertTrue(it.self.assertEqual(it.ByteVal7,it.byteT(it.BooleanVal2)))
            self.assertTrue(it.self.assertEqual(it.SByteVal7,it.sbyteT(it.BooleanVal2)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.BooleanVal2)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val7,it.uintT(it.BooleanVal3)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val7,it.ushortT(it.BooleanVal3)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val7,it.ulongT(it.BooleanVal3)))
            self.assertTrue(it.self.assertEqual(it.Int32Val7,it.intT(it.BooleanVal3)))
            self.assertTrue(it.self.assertEqual(it.Int16Val7,it.shortT(it.BooleanVal3)))
            self.assertTrue(it.self.assertEqual(it.Int64Val7,it.longT(it.BooleanVal3)))
            self.assertTrue(it.self.assertEqual(it.ByteVal7,it.byteT(it.BooleanVal3)))
            self.assertTrue(it.self.assertEqual(it.SByteVal7,it.sbyteT(it.BooleanVal3)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.BooleanVal3)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val7,it.uintT(it.BooleanVal4)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val7,it.ushortT(it.BooleanVal4)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val7,it.ulongT(it.BooleanVal4)))
            self.assertTrue(it.self.assertEqual(it.Int32Val7,it.intT(it.BooleanVal4)))
            self.assertTrue(it.self.assertEqual(it.Int16Val7,it.shortT(it.BooleanVal4)))
            self.assertTrue(it.self.assertEqual(it.Int64Val7,it.longT(it.BooleanVal4)))
            self.assertTrue(it.self.assertEqual(it.ByteVal7,it.byteT(it.BooleanVal4)))
            self.assertTrue(it.self.assertEqual(it.SByteVal7,it.sbyteT(it.BooleanVal4)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.BooleanVal4)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.BooleanVal5)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.BooleanVal5)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.BooleanVal5)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.BooleanVal5)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.BooleanVal5)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.BooleanVal5)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.BooleanVal5)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.BooleanVal5)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.BooleanVal5)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val6,it.uintT(it.BooleanVal6)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val6,it.ushortT(it.BooleanVal6)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val6,it.ulongT(it.BooleanVal6)))
            self.assertTrue(it.self.assertEqual(it.Int32Val6,it.intT(it.BooleanVal6)))
            self.assertTrue(it.self.assertEqual(it.Int16Val6,it.shortT(it.BooleanVal6)))
            self.assertTrue(it.self.assertEqual(it.Int64Val6,it.longT(it.BooleanVal6)))
            self.assertTrue(it.self.assertEqual(it.ByteVal6,it.byteT(it.BooleanVal6)))
            self.assertTrue(it.self.assertEqual(it.SByteVal6,it.sbyteT(it.BooleanVal6)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal6,it.boolT(it.BooleanVal6)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val7,it.uintT(it.BooleanVal7)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val7,it.ushortT(it.BooleanVal7)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val7,it.ulongT(it.BooleanVal7)))
            self.assertTrue(it.self.assertEqual(it.Int32Val7,it.intT(it.BooleanVal7)))
            self.assertTrue(it.self.assertEqual(it.Int16Val7,it.shortT(it.BooleanVal7)))
            self.assertTrue(it.self.assertEqual(it.Int64Val7,it.longT(it.BooleanVal7)))
            self.assertTrue(it.self.assertEqual(it.ByteVal7,it.byteT(it.BooleanVal7)))
            self.assertTrue(it.self.assertEqual(it.SByteVal7,it.sbyteT(it.BooleanVal7)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.BooleanVal7)))
            self.assertTrue(it.self.assertEqual(it.UInt32Val7,it.uintT(it.BooleanVal8)))
            self.assertTrue(it.self.assertEqual(it.UInt16Val7,it.ushortT(it.BooleanVal8)))
            self.assertTrue(it.self.assertEqual(it.UInt64Val7,it.ulongT(it.BooleanVal8)))
            self.assertTrue(it.self.assertEqual(it.Int32Val7,it.intT(it.BooleanVal8)))
            self.assertTrue(it.self.assertEqual(it.Int16Val7,it.shortT(it.BooleanVal8)))
            self.assertTrue(it.self.assertEqual(it.Int64Val7,it.longT(it.BooleanVal8)))
            self.assertTrue(it.self.assertEqual(it.ByteVal7,it.byteT(it.BooleanVal8)))
            self.assertTrue(it.self.assertEqual(it.SByteVal7,it.sbyteT(it.BooleanVal8)))
            self.assertTrue(it.self.assertEqual(it.BooleanVal8,it.boolT(it.BooleanVal8)))

    def test_long(self):
        class myint(int):
            def __str__(self): return 'myint'

        self.assertEqual(repr(myint(int(3))), '3')

    def test_override_eq(self):
        for base_type in [float, int]:
            class F(base_type):
                def __eq__(self, other):
                    return other == 'abc'
                def __ne__(self, other):
                    return other == 'def'

            self.assertEqual(F() == 'abc', True)
            self.assertEqual(F() != 'def', True)
            self.assertEqual(F() == 'qwe', False)
            self.assertEqual(F() != 'qwe', False)

    def test_bad_float_to_int(self):
        self.assertRaises(OverflowError, int, 1.0e340)           # Positive Infinity
        self.assertRaises(OverflowError, int, -1.0e340)          # Negative Infinity
        self.assertRaises(ValueError, int, 1.0e340-1.0e340)      # NAN

    def test_int___int__(self):
        for x in [-(int(2**(32-1)-1)), -3, -2, -1, 0, 1, 2, 3, int(2**(32-1)-1)]:
            self.assertEqual(x.__int__(), x)

    @skipUnlessIronPython()
    def test_long_conv(self):
        class Foo(int):
            def __int__(self):
                return big(42)

        self.assertEqual(int(Foo()), 42)

    def test_long_div(self):
        x = int('2'*400 + '9')
        y = int('3'*400 + '8')
        nx = -x
        self.assertEqual(x/y, 2/3)
        self.assertEqual(x/(x+1), 1.0)
        self.assertEqual((x+1)/x, 1.0)
        self.assertEqual(nx/(x+1), -1.0)
        self.assertEqual((x+1)/nx, -1.0)

    def test_pow_edges(self):
        class foo(object):
            def __pow__(self, *args): return NotImplemented

        self.assertRaisesPartialMessage(TypeError, "3rd argument not allowed unless all arguments are integers", pow, foo(), 2.0, 3.0)
        self.assertRaisesPartialMessage(TypeError, "unsupported operand type(s)", pow, foo(), 2, 3)

        x = 3
        self.assertEqual(x.__pow__(2.0, 3.0), NotImplemented)
        self.assertEqual(x.__pow__(2.0, 3), NotImplemented)
        self.assertEqual(x.__pow__(2, 3.0), NotImplemented)

    def test_int_from_long(self):
        """int(longVal) should return an Int32 if it's within range"""
        class x(int): pass
        if is_cli: import System

        for base in (int, x):
            for num, num_repr in [
                                    (big(-2**31-2), '-2147483650'),
                                    (big(-2**31-1), '-2147483649'),
                                    (big(-2**31), '-2147483648'),
                                    (big(-2**31+1), '-2147483647'),
                                    (big(-2**31+2), '-2147483646'),
                                    (big(0), '0'),
                                    (big(1), '1'),
                                    (big(2**31-2), '2147483646'),
                                    (big(2**31-1), '2147483647'),
                                    (big(2**31), '2147483648'),
                                    (big(2**31+1), '2147483649'),
                                    ]:
                self.assertEqual(repr(int(base(num))), num_repr)
                if is_cli:
                    if num < 2**31 and num >= -2**31:
                        self.assertTrue(hasattr(int(base(num)), "MaxValue"))
                        self.assertTrue(hasattr(int(base(num)), "MinValue"))
                    else:
                        self.assertFalse(hasattr(int(base(num)), "MaxValue"))
                        self.assertFalse(hasattr(int(base(num)), "MinValue"))


    def test_float_special_methods(self):
        self.assertEqual(float.__lt__(2.0, 3.0), True)
        self.assertEqual(float.__lt__(3.0, 2.0), False)
        self.assertEqual(float.__lt__(2.0, 2.0), False)
        self.assertEqual(float.__lt__(-1.0e340, 1.0e340), True)

        self.assertEqual(float.__gt__(2.0, 3.0), False)
        self.assertEqual(float.__gt__(3.0, 2.0), True)
        self.assertEqual(float.__gt__(2.0, 2.0), False)

        self.assertEqual(float.__ge__(2.0, 3.0), False)
        self.assertEqual(float.__ge__(3.0, 2.0), True)
        self.assertEqual(float.__ge__(2.0, 2.0), True)

        self.assertEqual(float.__le__(2.0, 3.0), True)
        self.assertEqual(float.__le__(3.0, 2.0), False)
        self.assertEqual(float.__le__(2.0, 2.0), True)

        self.assertEqual(float.__eq__(2.0, 3.0), False)
        self.assertEqual(float.__eq__(3.0, 3.0), True)

        self.assertEqual(float.__ne__(2.0, 3.0), True)
        self.assertEqual(float.__ne__(3.0, 3.0), False)

    def test_float_divmod(self):
        # https://github.com/IronLanguages/main/issues/1236
        self.assertEqual(divmod(0.123, 0.001), (122.0, 0.0009999999999999957))
        self.assertEqual(divmod(-0.123, 0.001), (-123.0, 4.336808689942018e-18))
        self.assertEqual(divmod(0.123, -0.001), (-123.0, -4.336808689942018e-18))
        self.assertEqual(divmod(-0.123, -0.001), (122.0, -0.0009999999999999957))

    def test_float_mod(self):
        self.assertEqual(0.123 % 0.001, 0.0009999999999999957)
        self.assertEqual(-0.123 % 0.001, 4.336808689942018e-18)
        self.assertEqual(0.123 % -0.001, -4.336808689942018e-18)
        self.assertEqual(-0.123 % -0.001, -0.0009999999999999957)

    def test_float_format_gprec(self):
        # https://github.com/IronLanguages/main/issues/1276
        self.assertEqual("%.17g" % 1021095.0286738087, '1021095.0286738087')

    def test_hex_and_octal(self):
        for num, num_repr in [
                                (big(0x20), '32'),
                                (big(0X20), '32'), #Capital X
                                (int(0x20), '32'),
                                (float(-0x20), '-32.0'),
                                (big(0o10), '8'),
                                (int(-0o10), '-8'),
                                (float(0o0010), '8.0'),
                            ]:
            self.assertEqual(repr(num), num_repr)

        for num in [ "0xx2", "09", "0P32", "0G" ]:
            self.assertRaises(SyntaxError, lambda: eval(num))

    def test_cp27383(self):
        self.assertEqual(int('0 ', 0), 0)
        self.assertEqual(int(' 0', 0), 0)
        self.assertEqual(int('0', 0), 0)

run_test(__name__)
