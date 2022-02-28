# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

import operator
import unittest

from iptest import IronPythonTestCase, is_cli, is_netcoreapp, big, run_test, skipUnlessIronPython

class OperaterTest(IronPythonTestCase):
    def setUp(self):
        super(OperaterTest, self).setUp()

        if is_cli:
            import clr
            self.load_iron_python_test()
            if is_netcoreapp:
                clr.AddReference("System.Drawing.Primitives")
            else:
                clr.AddReference("System.Drawing")

    @skipUnlessIronPython()
    def test_sys_drawing(self):
        from IronPythonTest import DaysInt, DaysShort, DaysLong, DaysSByte, DaysByte, DaysUShort, DaysUInt, DaysULong
        from System.Drawing import Point, Size, PointF, SizeF, Rectangle, RectangleF
        x = Point()
        self.assertTrue(x == Point(0,0))
        x = Size()
        self.assertTrue(x == Size(0,0))
        x = PointF()
        self.assertTrue(x == PointF(0,0))
        x = SizeF()
        self.assertTrue(x == SizeF(0,0))
        x = Rectangle()
        self.assertTrue(x == Rectangle(0,0,0,0))
        x = RectangleF()
        self.assertTrue(x == RectangleF(0,0,0,0))

        p = Point(3,4)
        s = Size(2,9)

        q = p + s
        self.assertTrue(q == Point(5,13))
        self.assertTrue(q != Point(13,5))
        q = p - s
        self.assertTrue(q == Point(1,-5))
        self.assertTrue(q != Point(0,4))
        q += s
        self.assertTrue(q == Point(3,4))
        self.assertTrue(q != Point(2,4))
        q -= Size(1,2)
        self.assertTrue(q == Point(2,2))
        self.assertTrue(q != Point(1))

        t = s
        self.assertTrue(t == s)
        self.assertTrue(t != s - Size(1,0))
        t += Size(3,1)
        self.assertTrue(t == Size(5,10))
        self.assertTrue(t != Size(5,0))
        t -= Size(2,8)
        self.assertTrue(t == Size(3,2))
        self.assertTrue(t != Size(0,2))
        t = s + Size(-1,-2)
        self.assertTrue(t == Size(1,7))
        self.assertTrue(t != Size(1,5))
        t = s - Size(1,2)
        self.assertTrue(t == Size(1,7))
        self.assertTrue(t != Size(1,3))

        def weekdays(enum):
            return enum.Mon|enum.Tue|enum.Wed|enum.Thu|enum.Fri

        def weekend(enum):
            return enum.Sat|enum.Sun

        def enum_helper(enum):
            days = [enum.Mon,enum.Tue,enum.Wed,enum.Thu,enum.Fri,enum.Sat,enum.Sun]
            x = enum.Mon|enum.Tue|enum.Wed|enum.Thu|enum.Fri|enum.Sat|enum.Sun
            y = enum.Mon
            for day in days:
                y |= day
            self.assertTrue(x == y)
            self.assertFalse(x != y)
            if x == y:  # EqualRetBool
                b = True
            else :
                b = False
            self.assertTrue(b)

            self.assertTrue(x == weekdays(enum)|weekend(enum))
            self.assertTrue(x == (weekdays(enum)^weekend(enum)))
            self.assertTrue((weekdays(enum)&weekend(enum)) == enum["None"])
            self.assertTrue(weekdays(enum) == enum.Weekdays)
            self.assertTrue(weekend(enum) == enum.Weekend)
            self.assertTrue(weekdays(enum) != enum.Weekend)
            self.assertTrue(weekdays(enum) != weekend(enum))

        for e in [DaysInt, DaysShort, DaysLong, DaysSByte, DaysByte, DaysUShort, DaysUInt, DaysULong]:
            enum_helper(e)

        for e in [DaysInt, DaysShort, DaysLong, DaysSByte]:
            z = operator.inv(e.Mon)
            self.assertEqual(type(z), e)
            self.assertEqual(z.ToString(), "-2")

        for (e, v) in [ (DaysByte,254), (DaysUShort,65534), (DaysUInt,4294967294), (DaysULong,18446744073709551614) ]:
            z = operator.inv(e.Mon)
            self.assertEqual(type(z), e)
            self.assertEqual(z.ToString(), str(v))

        self.assertRaises(ValueError, lambda: DaysInt.Mon & DaysShort.Mon)
        self.assertRaises(ValueError, lambda: DaysInt.Mon | DaysShort.Mon)
        self.assertRaises(ValueError, lambda: DaysInt.Mon ^ DaysShort.Mon)
        self.assertRaises(ValueError, lambda: DaysInt.Mon & 1)
        self.assertRaises(ValueError, lambda: DaysInt.Mon | 1)
        self.assertRaises(ValueError, lambda: DaysInt.Mon ^ 1)

        def f():
            if DaysInt.Mon == DaysShort.Mon: return True
            return False

        self.assertEqual(f(), False)

        self.assertTrue(not DaysInt.Mon == None)
        self.assertTrue(DaysInt.Mon != None)

    @skipUnlessIronPython()
    def test_cp3982(self):
        from System.Drawing import Color
        test_funcs = [  lambda x: x,
                        lambda x: [x],
                        lambda x: (x),
                        lambda x: [[x]],
                        lambda x: [(x)],
                        lambda x: ((x)),
                        lambda x: ([x]),
                        lambda x: [[[x]]],
                        lambda x: (((x))),
                        lambda x: [x, x],
                        lambda x: (x, x),
                        lambda x: [(x), [x, x]],
                        lambda x: ([x, x], (x)),
                    ]

        for test_func in test_funcs:
            self.assertTrue(test_func(Color.Red)==test_func(Color.Red))
            self.assertTrue(test_func(Color.Red)!=test_func(Color.Green))
            self.assertTrue(test_func(Color.Green)!=test_func(Color.Red))

        self.assertTrue( [Color.Green, Color.Red]  == [Color.Green, Color.Red])
        self.assertTrue([(Color.Green, Color.Red)] == [(Color.Green, Color.Red)])
        self.assertTrue( [Color.Green, Color.Red]  != (Color.Green, Color.Red))
        self.assertTrue( [Color.Green, Color.Red]  != [Color.Green, Color.Black])

    def test_operator_module(self):
        x = ['a','b','c','d']
        g = operator.itemgetter(2)
        self.assertEqual(g(x), 'c')

        class C:
            a = 10
        g = operator.attrgetter("a")
        self.assertEqual(g(C), 10)
        self.assertEqual(g(C()), 10)

        a = { 'k' : 'v' }
        g = operator.itemgetter('x')
        self.assertRaises(KeyError, g, a)

        x = True
        self.assertEqual(x, True)
        self.assertEqual(not x, False)
        x = False
        self.assertEqual(x, False)
        self.assertEqual(not x, True)

        class C:
            def func(self):
                pass

        a = C.func
        b = C.func
        self.assertEqual(a, b)

        c = C()
        a = c.func
        b = c.func
        self.assertEqual(a, b)

        # __setitem__
        x = {}
        operator.__setitem__(x, 'abc', 'def')
        self.assertEqual(x, {'abc':'def'})

        # __not__
        x = True
        self.assertEqual(operator.__not__(x), False)

    def test_string_mult(self):
        """string multiplication"""
        class foo(int): pass

        fooInst = foo(3)

        self.assertEqual('aaa', 'a' * 3)
        self.assertEqual('aaa', 'a' * big(3))
        self.assertEqual('aaa', 'a' * fooInst)

        self.assertEqual('', 'a' * False)
        self.assertEqual('a', 'a' * True)

    def test_eq_ne_overloads(self):
        """(not)equals overloading semantics"""
        class CustomEqual:
            def __eq__(self, other):
                return 7

        self.assertEqual((CustomEqual() == 1), 7)

        for base_type in [
                            dict, list, tuple,
                            float, int, complex,
                            bytes, str,
                            object,
                        ]:

            class F(base_type):
                def __eq__(self, other):
                    return other == 'abc'
                def __ne__(self, other):
                    return other == 'def'

            self.assertEqual(F() == 'abc', True)
            self.assertEqual(F() != 'def', True)
            self.assertEqual(F() == 'qwe', False)
            self.assertEqual(F() != 'qwe', False)


    def test_num_binary_ops(self):
        """Test binary operators for all numeric types and types inherited from them"""
        class myint(int): pass
        class myfloat(float): pass
        class mycomplex(complex): pass

        l = [2, big(10), (1+2j), 3.4, myint(7), myfloat(2.32), mycomplex(3, 2), True]

        if is_cli:
            import System
            l.append(System.Int64.Parse("5"))

        def add(a, b): return a + b
        def sub(a, b): return a - b
        def mul(a, b): return a * b
        def div(a, b): return a / b
        def mod(a, b): return a % b
        def truediv(a,b): return a / b
        def floordiv(a,b): return a // b
        def pow(a,b): return a ** b

        op = [
            ('+', add, True),
            ('-', sub, True),
            ('*', mul, True),
            ('/', div, True),
            ('%', mod, False),
            ('//', floordiv, False),
            ('**', pow, True)
            ]

        for a in l:
            for b in l:
                for sym, fnc, cmp in op:
                    if cmp or (not isinstance(a, complex) and not isinstance(b, complex)):
                        try:
                            r = fnc(a,b)
                        except:
                            (exc_type, exc_value, exc_traceback) = sys.exc_info()
                            Fail("Binary operator failed: %s, %s: %s %s %s (Message=%s)" % (type(a).__name__, type(b).__name__, str(a), sym, str(b), str(exc_value)))

        threes = [ 3, big(3), 3.0 ]
        zeroes = [ 0, big(0), 0.0 ]

        if is_cli:
            threes.append(System.Int64.Parse("3"))
            zeroes.append(System.Int64.Parse("0"))

        for i in threes:
            for j in zeroes:
                for fnc in [div, mod, truediv, floordiv]:
                    try:
                        r = fnc(i, j)
                    except ZeroDivisionError:
                        pass
                    else:
                        (exc_type, exc_value, exc_traceback) = sys.exc_info()
                        Fail("Didn't get ZeroDivisionError %s, %s, %s, %s, %s (Message=%s)" % (str(func), type(i).__name__, type(j).__name__, str(i), str(j), str(exc_value)))

        def test_unary_ops(self):
            if is_cli:
                from IronPythonTest import UnaryClass
                unary = UnaryClass(9)
                self.assertEqual(-(unary.value), (-unary).value)
                self.assertEqual(~(unary.value), (~unary).value)

            # testing customized unary op
            class C1:
                def __pos__(self):
                    return -10
                def __neg__(self):
                    return 10
                def __invert__(self):
                    return 20
                def __abs__(self):
                    return 30

            class C2(object):
                def __pos__(self):
                    return -10
                def __neg__(self):
                    return 10
                def __invert__(self):
                    return 20
                def __abs__(self):
                    return 30

            for x in C1(), C2():
                self.assertEqual(+x, -10)
                self.assertEqual(-x, 10)
                self.assertEqual(~x, 20)
                self.assertEqual(abs(x), 30)


    def test_custom_divmod(self):
        """testing custom divmod operator"""
        class DM:
            def __divmod__(self, other):
                return "__divmod__"

        class NewDM(int): pass

        class Callable:
            def __call__(self, other):
                return "__call__"

        class CallDM:
            __divmod__ = Callable()

        self.assertEqual(divmod(DM(), DM()), "__divmod__")
        self.assertEqual(divmod(DM(), 10), "__divmod__")
        self.assertEqual(divmod(NewDM(10), NewDM(5)), (2, 0))
        self.assertEqual(divmod(CallDM(), 2), "__call__")

def test_bool_obj_id(self):
    """object identity of booleans - __ne__ should return "True" or "False", not a new boxed bool"""
    self.assertEqual(id(complex.__ne__(1+1j, 1+1j)), id(False))
    self.assertEqual(id(complex.__ne__(1+1j, 1+2j)), id(True))

    def test_sanity(self):
        """Performs a set of simple sanity checks on most operators."""

        #__abs__
        self.assertEqual(operator.__abs__(0), 0)
        self.assertEqual(operator.__abs__(1), 1)
        self.assertEqual(operator.__abs__(-1), 1)
        self.assertEqual(operator.__abs__(0.0), 0.0)
        self.assertEqual(operator.__abs__(1.1), 1.1)
        self.assertEqual(operator.__abs__(-1.1), 1.1)
        self.assertEqual(operator.__abs__(big(0)), big(0))
        self.assertEqual(operator.__abs__(big(1)), big(1))
        self.assertEqual(operator.__abs__(-big(1)), big(1))

        #__neg__
        self.assertEqual(operator.__neg__(0), 0)
        self.assertEqual(operator.__neg__(1), -1)
        self.assertEqual(operator.__neg__(-1), 1)
        self.assertEqual(operator.__neg__(0.0), 0.0)
        self.assertEqual(operator.__neg__(1.1), -1.1)
        self.assertEqual(operator.__neg__(-1.1), 1.1)
        self.assertEqual(operator.__neg__(big(0)), big(0))
        self.assertEqual(operator.__neg__(big(1)), -big(1))
        self.assertEqual(operator.__neg__(-big(1)), big(1))

        #__pos__
        self.assertEqual(operator.__pos__(0), 0)
        self.assertEqual(operator.__pos__(1), 1)
        self.assertEqual(operator.__pos__(-1), -1)
        self.assertEqual(operator.__pos__(0.0), 0.0)
        self.assertEqual(operator.__pos__(1.1), 1.1)
        self.assertEqual(operator.__pos__(-1.1), -1.1)
        self.assertEqual(operator.__pos__(big(0)), big(0))
        self.assertEqual(operator.__pos__(big(1)), big(1))
        self.assertEqual(operator.__pos__(-big(1)), -big(1))

        #__add__
        self.assertEqual(operator.__add__(0, 0), 0)
        self.assertEqual(operator.__add__(1, 2), 3)
        self.assertEqual(operator.__add__(-1, 2), 1)
        self.assertEqual(operator.__add__(0.0, 0.0), 0.0)
        self.assertEqual(operator.__add__(1.1, 2.1), 3.2)
        self.assertEqual(operator.__add__(-1.1, 2.1), 1.0)
        self.assertEqual(operator.__add__(big(0), big(0)), big(0))
        self.assertEqual(operator.__add__(big(1), big(2)), big(3))
        self.assertEqual(operator.__add__(-big(1), big(2)), big(1))

        #__sub__
        self.assertEqual(operator.__sub__(0, 0), 0)
        self.assertEqual(operator.__sub__(1, 2), -1)
        self.assertEqual(operator.__sub__(-1, 2), -3)
        self.assertEqual(operator.__sub__(0.0, 0.0), 0.0)
        self.assertEqual(operator.__sub__(1.1, 2.1), -1.0)
        self.assertEqual(operator.__sub__(-1.1, 2.1), -3.2)
        self.assertEqual(operator.__sub__(big(0), big(0)), big(0))
        self.assertEqual(operator.__sub__(big(1), big(2)), -big(1))
        self.assertEqual(operator.__sub__(-big(1), big(2)), -big(3))

        #__mul__
        self.assertEqual(operator.__mul__(0, 0), 0)
        self.assertEqual(operator.__mul__(1, 2), 2)
        self.assertEqual(operator.__mul__(-1, 2), -2)
        self.assertEqual(operator.__mul__(0.0, 0.0), 0.0)
        self.assertEqual(operator.__mul__(2.0, 3.0), 6.0)
        self.assertEqual(operator.__mul__(-2.0, 3.0), -6.0)
        self.assertEqual(operator.__mul__(big(0), big(0)), big(0))
        self.assertEqual(operator.__mul__(big(1), big(2)), big(2))
        self.assertEqual(operator.__mul__(-big(1), big(2)), -big(2))

        #__div__
        self.assertEqual(operator.__div__(0, 1), 0)
        self.assertEqual(operator.__div__(4, 2), 2)
        self.assertEqual(operator.__div__(-1, 2), -1)
        self.assertEqual(operator.__div__(0.0, 1.0), 0.0)
        self.assertEqual(operator.__div__(4.0, 2.0), 2.0)
        self.assertEqual(operator.__div__(-4.0, 2.0), -2.0)
        self.assertEqual(operator.__div__(big(0), big(1)), big(0))
        self.assertEqual(operator.__div__(big(4), big(2)), big(2))
        self.assertEqual(operator.__div__(-big(4), big(2)), -big(2))

        #__floordiv__
        self.assertEqual(operator.__floordiv__(0, 1), 0)
        self.assertEqual(operator.__floordiv__(4, 2), 2)
        self.assertEqual(operator.__floordiv__(-1, 2), -1)
        self.assertEqual(operator.__floordiv__(0.0, 1.0), 0.0)
        self.assertEqual(operator.__floordiv__(4.0, 2.0), 2.0)
        self.assertEqual(operator.__floordiv__(-4.0, 2.0), -2.0)
        self.assertEqual(operator.__floordiv__(big(0), big(1)), big(0))
        self.assertEqual(operator.__floordiv__(big(4), big(2)), big(2))
        self.assertEqual(operator.__floordiv__(-big(4), big(2)), -big(2))

        #__truediv__
        self.assertEqual(operator.__truediv__(0, 1), 0)
        self.assertEqual(operator.__truediv__(4, 2), 2)
        self.assertEqual(operator.__truediv__(-1, 2), -0.5)
        self.assertEqual(operator.__truediv__(0.0, 1.0), 0.0)
        self.assertEqual(operator.__truediv__(4.0, 2.0), 2.0)
        self.assertEqual(operator.__truediv__(-1.0, 2.0), -0.5)
        self.assertEqual(operator.__truediv__(big(0), big(1)), big(0))
        self.assertEqual(operator.__truediv__(big(4), big(2)), big(2))
        self.assertEqual(operator.__truediv__(-big(4), big(2)), -big(2))

        #__mod__
        self.assertEqual(operator.__mod__(0, 1), 0)
        self.assertEqual(operator.__mod__(4, 2), 0)
        self.assertEqual(operator.__mod__(-1, 2), 1)
        self.assertEqual(operator.__mod__(0.0, 1.0), 0.0)
        self.assertEqual(operator.__mod__(4.0, 2.0), 0.0)
        self.assertEqual(operator.__mod__(-1.0, 2.0), 1.0)
        self.assertEqual(operator.__mod__(big(0), big(1)), big(0))
        self.assertEqual(operator.__mod__(big(4), big(2)), big(0))
        self.assertEqual(operator.__mod__(-big(4), big(2)), big(0))

        #__inv__
        self.assertEqual(operator.__inv__(0), -1)
        self.assertEqual(operator.__inv__(1), -2)
        self.assertEqual(operator.__inv__(-1), 0)
        self.assertEqual(operator.__inv__(big(0)), -big(1))
        self.assertEqual(operator.__inv__(big(1)), -big(2))
        self.assertEqual(operator.__inv__(-big(1)), big(0))

        #__invert__
        self.assertEqual(operator.__invert__(0), -1)
        self.assertEqual(operator.__invert__(1), -2)
        self.assertEqual(operator.__invert__(-1), 0)
        self.assertEqual(operator.__invert__(big(0)), -big(1))
        self.assertEqual(operator.__invert__(big(1)), -big(2))
        self.assertEqual(operator.__invert__(-big(1)), big(0))

        #__lshift__
        self.assertEqual(operator.__lshift__(0, 1), 0)
        self.assertEqual(operator.__lshift__(1, 1), 2)
        self.assertEqual(operator.__lshift__(-1, 1), -2)
        self.assertEqual(operator.__lshift__(big(0), 1), big(0))
        self.assertEqual(operator.__lshift__(big(1), 1), big(2))
        self.assertEqual(operator.__lshift__(-big(1), 1), -big(2))

        #__rshift__
        self.assertEqual(operator.__rshift__(1, 1), 0)
        self.assertEqual(operator.__rshift__(2, 1), 1)
        self.assertEqual(operator.__rshift__(-1, 1), -1)
        self.assertEqual(operator.__rshift__(big(1), 1), big(0))
        self.assertEqual(operator.__rshift__(big(2), 1), big(1))
        self.assertEqual(operator.__rshift__(-big(1), 1), -big(1))

        #__not__
        self.assertEqual(operator.__not__(0), 1)
        self.assertEqual(operator.__not__(1), 0)
        self.assertEqual(operator.__not__(-1), 0)
        self.assertEqual(operator.__not__(big(0)), 1)
        self.assertEqual(operator.__not__(big(1)), 0)
        self.assertEqual(operator.__not__(-big(1)), 0)

        #__and__
        self.assertEqual(operator.__and__(0, 0), 0)
        self.assertEqual(operator.__and__(1, 1), 1)
        self.assertEqual(operator.__and__(0, 1), 0)
        self.assertEqual(operator.__and__(1, 0), 0)

        #__xor__
        self.assertEqual(operator.__xor__(0, 0), 0)
        self.assertEqual(operator.__xor__(1, 1), 0)
        self.assertEqual(operator.__xor__(0, 1), 1)
        self.assertEqual(operator.__xor__(1, 0), 1)

        #__or__
        self.assertEqual(operator.__or__(0, 0), 0)
        self.assertEqual(operator.__or__(1, 1), 1)
        self.assertEqual(operator.__or__(0, 1), 1)
        self.assertEqual(operator.__or__(1, 0), 1)

        #__concat__
        self.assertEqual(operator.__concat__([0], [1]), [0,1])
        self.assertEqual(operator.__concat__([2], [1]), [2,1])
        self.assertEqual(operator.__concat__([-1], [1]), [-1,1])

        #__contains__
        self.assertTrue(operator.__contains__("abc", "c"))
        self.assertTrue(not operator.__contains__("abc", "d"))
        self.assertTrue(operator.__contains__("abc", ""))
        self.assertTrue(not operator.__contains__("", "c"))
        self.assertTrue(operator.__contains__([1,2,3], 1))
        self.assertTrue(not operator.__contains__([1,2,3], 4))

        #__getitem__
        self.assertEqual(operator.__getitem__("abc", 2), "c")
        self.assertRaises(IndexError, operator.__getitem__, "abc", 3)
        self.assertEqual(operator.__getitem__([1,2,3], 2), 3)
        self.assertRaises(IndexError, operator.__getitem__, [1,2,3], 3)

        #__setitem__
        self.assertRaises(TypeError, operator.__setitem__, "abc", 2, "d")
        t_list = [1,2,3]
        operator.__setitem__(t_list, 2, 4)
        self.assertEqual(t_list, [1,2,4])
        self.assertRaises(IndexError, operator.__setitem__, [1,2,3], 4, 9)

        #__delitem__
        #UNIMPLEMENTED
        #self.assertRaises(TypeError, operator.__delitem__, "abc", 2)
        t_list = [1,2,3]
        operator.__delitem__(t_list, 2)
        self.assertEqual(t_list, [1,2])
        self.assertRaises(IndexError, operator.__delitem__, [1,2,3], 4)

        #__repeat__
        self.assertEqual(operator.__repeat__("abc", 2), "abcabc")
        self.assertEqual(operator.__repeat__("", 2), "")
        self.assertEqual(operator.__repeat__([1,2,3], 2), [1,2,3,1,2,3])

        #__getslice__
        self.assertEqual(operator.__getslice__("abc", 1, 2), "b")
        self.assertEqual(operator.__getslice__("abc", 0, 3), "abc")
        self.assertEqual(operator.__getslice__("", 0, 0), "")
        self.assertEqual(operator.__getslice__([1,2,3], 1, 2), [2])
        self.assertEqual(operator.__getslice__([1,2,3], 0, 3), [1,2,3])
        self.assertEqual(operator.__getslice__([], 0, 0), [])

        #__delslice__
        t_list = [1,2,3]
        operator.__delslice__(t_list, 1, 2)
        self.assertEqual(t_list, [1,3])

        t_list = [1,2,3]
        operator.__delslice__(t_list, 0, 3)
        self.assertEqual(t_list, [])

        t_list = [1,2,3]
        operator.__delslice__(t_list, 0, 0)
        self.assertEqual(t_list, [1,2,3])

        #__setslice__
        t_list = [1,2,3]
        operator.__setslice__(t_list, 1, 2, [9])
        self.assertEqual(t_list, [1,9,3])

        t_list = [1,2,3]
        operator.__setslice__(t_list, 0, 3, [9, 8])
        self.assertEqual(t_list, [9, 8])

        t_list = [1,2,3]
        operator.__setslice__(t_list, 0, 0, [9])
        self.assertEqual(t_list, [9,1, 2,3])

    def test_py25_operator(self):
        ops = ['iadd', 'isub', 'idiv', 'ilshift', 'imod', 'imul', 'ior', 'ipow', 'irshift', 'isub', 'itruediv', 'ifloordiv', 'ixor']

        class foo(object):
            for x in ops:
                exec('def __%s__(self, other): return "%s", other' % (x, x), foo)

        for x in ops:
            self.assertEqual(getattr(operator, x)(foo(), 42), (x, 42))
            self.assertEqual(getattr(operator, '__' + x + '__')(foo(), 42), (x, 42))

    def test_concat_repeat(self):
        self.assertRaises(TypeError, operator.concat, 2, 3)
        self.assertRaises(TypeError, operator.repeat, 2, 3)

    def test_addition_error(self):
        self.assertRaisesMessage(TypeError, "unsupported operand type(s) for +: 'int' and 'str'", lambda : 2 + 'abc')

run_test(__name__)
