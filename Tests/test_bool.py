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

from iptest.assert_util import *

# types are always true.
def test_types():
    for x in [str, int, int, float, bool]:
        if not x:
            Fail("should be true: %r", x)

def test_bool_dir():
    bool_dir = ['__abs__', '__add__', '__and__', '__class__', '__cmp__',
                '__coerce__', '__delattr__', '__div__', '__divmod__', '__doc__',
                '__float__', '__floordiv__', '__getattribute__', '__getnewargs__',
                '__hash__', '__hex__', '__index__', '__init__', '__int__',
                '__invert__', '__long__', '__lshift__', '__mod__', '__mul__',
                '__neg__', '__new__', '__bool__', '__oct__', '__or__', '__pos__',
                '__pow__', '__radd__', '__rand__', '__rdiv__', '__rdivmod__', '__reduce__',
                '__reduce_ex__', '__repr__', '__rfloordiv__', '__rlshift__', '__rmod__',
                '__rmul__', '__ror__', '__rpow__', '__rrshift__', '__rshift__',
                '__rsub__', '__rtruediv__', '__rxor__', '__setattr__', '__str__',
                '__sub__', '__truediv__', '__xor__']

    for t_list in [dir(bool), dir(True), dir(False)]:
        for stuff in bool_dir:
            Assert(stuff in t_list, "%s should be in dir(bool), but is not" % (stuff))


def test__coerce__():
    for simple_type in [int, int, float, str, str, bool, object]:
        AreEqual(NotImplemented, True.__coerce__(simple_type))
        AreEqual(NotImplemented, False.__coerce__(simple_type))
        
def test__float__():
    AreEqual(float(True), 1.0)
    AreEqual(float(False), 0.0)
    
def test__index__():
    AreEqual(True.__index__(), 1)
    AreEqual(False.__index__(), 0)
    
def test__long__():
    AreEqual(int(True), 1)
    AreEqual(int(False), 0)
    
def test__rdivmod__():
    AreEqual(divmod(True, True),  (1, 0))
    AreEqual(divmod(False, True), (0, 0))
    AssertError(ZeroDivisionError, divmod, True,  False)
    AssertError(ZeroDivisionError, divmod, False, False)

@skip("win32")    
def test_decimal():
    import System
    if not System.Decimal:
        Fail("should be true: %r", System.Decimal)
        
    AreEqual(bool(System.Decimal(0)), False)
    AreEqual(bool(System.Decimal(1)), True)
    AreEqual(System.Decimal(True), System.Decimal(1))
    AreEqual(System.Decimal(False), System.Decimal(0))
    
run_test(__name__)
