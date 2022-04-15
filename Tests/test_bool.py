# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

from iptest import big, myint, run_test, skipUnlessIronPython

class BoolTest(IronPythonTestCase):
    def test_types(self):
        for x in [str, int, float, bool]:
            if not x:
                self.fail("should be true: %r", x)

    def test_bool_dir(self):
        bool_dir = ['__abs__', '__add__', '__and__', '__class__',
                    '__eq__', '__ne__', '__gt__', '__ge__', '__le__', '__lt__',
                    '__delattr__', '__divmod__', '__doc__',
                    '__float__', '__floordiv__', '__getattribute__', '__getnewargs__',
                    '__hash__', '__index__', '__init__', '__int__',
                    '__invert__', '__lshift__', '__mod__', '__mul__',
                    '__neg__', '__new__', '__bool__', '__or__', '__pos__',
                    '__pow__', '__radd__', '__rand__', '__rdivmod__', '__reduce__',
                    '__reduce_ex__', '__repr__', '__rfloordiv__', '__rlshift__', '__rmod__',
                    '__rmul__', '__ror__', '__rpow__', '__rrshift__', '__rshift__',
                    '__rsub__', '__rtruediv__', '__rxor__', '__setattr__', '__str__',
                    '__sub__', '__truediv__', '__xor__']

        for t_list in [dir(bool), dir(True), dir(False)]:
            for stuff in bool_dir:
                self.assertTrue(stuff in t_list, "%s should be in dir(bool), but is not" % (stuff))

    def test__float__(self):
        self.assertEqual(float(True), 1.0)
        self.assertEqual(float(False), 0.0)

    def test__index__(self):
        self.assertEqual(True.__index__(), 1)
        self.assertEqual(False.__index__(), 0)

    def test__int__(self):
        self.assertEqual(int(True), 1)
        self.assertEqual(int(False), 0)

    def test__rdivmod__(self):
        self.assertEqual(divmod(True, True),  (1, 0))
        self.assertEqual(divmod(False, True), (0, 0))
        self.assertRaises(ZeroDivisionError, divmod, True,  False)
        self.assertRaises(ZeroDivisionError, divmod, False, False)

    @skipUnlessIronPython()
    def test_decimal(self):
        import System
        if not System.Decimal:
            self.fail("should be true: %r", System.Decimal)

        self.assertEqual(bool(System.Decimal(0)), False)
        self.assertEqual(bool(System.Decimal(1)), True)
        self.assertEqual(System.Decimal(True), System.Decimal(1))
        self.assertEqual(System.Decimal(False), System.Decimal(0))

    def test__bool__(self):
        class ClassWithBool:
            def __init__(self, val):
                self.val = val
            def __bool__(self):
                return self.val

        class ClassWithLen:
            def __init__(self, val):
                self.val = val
            def __len__(self):
                return self.val

        class MyIndex:
            def __init__(self, val):
                self.val = val
            def __index__(self):
                return self.val

        bool_cases = [
            (True, True), (False, False), (0.0, TypeError), (-1, TypeError), (1<<64, TypeError), (MyIndex(0), TypeError),
        ]
        len_cases = [
            (1, True), (0, False), (0.0, TypeError), (-1, ValueError), (1<<64, OverflowError),
        ]

        cases = []
        cases += [(ClassWithBool(x), y) for x, y in bool_cases]
        cases += [(ClassWithLen(x), y) for x, y in len_cases]
        cases += [(ClassWithLen(big(x)), y) for x, y in len_cases if isinstance(x, int)]
        cases += [(ClassWithLen(myint(x)), y) for x, y in len_cases if isinstance(x, int)]
        cases += [(ClassWithLen(MyIndex(x)), y) for x, y in len_cases]

        for val, res in cases:
            if type(res) == type:
                with self.assertRaises(res):
                    bool(val)
                with self.assertRaises(res):
                    not val
            else:
                self.assertEqual(bool(val), res)
                self.assertEqual(not val, not res)

run_test(__name__)
