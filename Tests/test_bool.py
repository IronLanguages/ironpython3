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

from iptest import is_cli, run_test

class BoolTest(unittest.TestCase):
    def test_types(self):
        for x in [str, int, int, float, bool]:
            if not x:
                self.fail("should be true: %r", x)

    def test_bool_dir(self):
        bool_dir = ['__abs__', '__add__', '__and__', '__class__', '__cmp__',
                    '__coerce__', '__delattr__', '__div__', '__divmod__', '__doc__',
                    '__float__', '__floordiv__', '__getattribute__', '__getnewargs__',
                    '__hash__', '__hex__', '__index__', '__init__', '__int__',
                    '__invert__', '__long__', '__lshift__', '__mod__', '__mul__',
                    '__neg__', '__new__', '__nonzero__', '__oct__', '__or__', '__pos__',
                    '__pow__', '__radd__', '__rand__', '__rdiv__', '__rdivmod__', '__reduce__',
                    '__reduce_ex__', '__repr__', '__rfloordiv__', '__rlshift__', '__rmod__',
                    '__rmul__', '__ror__', '__rpow__', '__rrshift__', '__rshift__',
                    '__rsub__', '__rtruediv__', '__rxor__', '__setattr__', '__str__',
                    '__sub__', '__truediv__', '__xor__']

        for t_list in [dir(bool), dir(True), dir(False)]:
            for stuff in bool_dir:
                self.assertTrue(stuff in t_list, "%s should be in dir(bool), but is not" % (stuff))


    def test__coerce__(self):
        for simple_type in [int, int, float, str, str, bool, object]:
            self.assertEqual(NotImplemented, True.__coerce__(simple_type))
            self.assertEqual(NotImplemented, False.__coerce__(simple_type))
        
    def test__float__(self):
        self.assertEqual(float(True), 1.0)
        self.assertEqual(float(False), 0.0)
    
    def test__index__(self):
        self.assertEqual(True.__index__(), 1)
        self.assertEqual(False.__index__(), 0)
    
    def test__long__(self):
        self.assertEqual(int(True), 1)
        self.assertEqual(int(False), 0)
    
    def test__rdivmod__(self):
        self.assertEqual(divmod(True, True),  (1, 0))
        self.assertEqual(divmod(False, True), (0, 0))
        self.assertRaises(ZeroDivisionError, divmod, True,  False)
        self.assertRaises(ZeroDivisionError, divmod, False, False)

    @unittest.skipUnless(is_cli, 'IronPython specific test')
    def test_decimal(self):
        import System
        if not System.Decimal:
            Fail("should be true: %r", System.Decimal)
            
        self.assertEqual(bool(System.Decimal(0)), False)
        self.assertEqual(bool(System.Decimal(1)), True)
        self.assertEqual(System.Decimal(True), System.Decimal(1))
        self.assertEqual(System.Decimal(False), System.Decimal(0))
    
run_test(__name__)

