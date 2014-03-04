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
'''
All operators
'''
#------------------------------------------------------------------------------
from iptest.assert_util import *
skiptest("silverlight")

add_clr_assemblies("operators", "typesamples")

from Merlin.Testing import *
from Merlin.Testing.Call import *
from Merlin.Testing.TypeSample import *

# to avoid calling == on x
def AreValueFlagEqual(x, v, f): 
    AreEqual(x.Value, v)
    Flag.Check(f)

def test_all_ops():
    x = AllOpsClass(5)
    y = AllOpsClass(6)
    z = AllOpsClass(-6)
    
    #if x: pass
    #else: AssertUnreachable()
    #
    #Flag.Check(100)
    #
    #if z: AssertUnreachable()
    #else: pass
    #
    #Flag.Check(100)
    #
    #if not x: AssertUnreachable()
    #else: pass
    #
    #Flag.Check(100)
    #
    #if not z: pass
    #else: AssertUnreachable()
    #
    #Flag.Check(100)
    
    # ! is not supported in python
    
    AreValueFlagEqual(~x, ~5, 140)
    
    # ++/-- not supported

    AreValueFlagEqual(+x, 5, 180)
    AreValueFlagEqual(-x, -5, 170)

    AreValueFlagEqual(+z, -6, 180)
    AreValueFlagEqual(-z, 6, 170)
    
    AreValueFlagEqual(x + y, 11, 200)
    AreValueFlagEqual(x - y, -1, 210)
    AreValueFlagEqual(x * y, 30, 220)
    AreValueFlagEqual(x / y, 0, 230)
    AreValueFlagEqual(x % y, 5, 240)
    AreValueFlagEqual(x ^ y, 5^6, 250)
    AreValueFlagEqual(x & y, 5&6, 260)
    AreValueFlagEqual(x | y, 5|6, 270)
    
    AreValueFlagEqual(x << 2, 5 << 2, 280)
    AreValueFlagEqual(x >> 1, 5 >> 1, 290)
    
    AreEqual(x == y, False); Flag.Check(300)
    AreEqual(x > y, False); Flag.Check(310)
    AreEqual(x < y, True); Flag.Check(320)
    AreEqual(x != y, True); Flag.Check(330)
    AreEqual(x >= y, False); Flag.Check(340)
    AreEqual(x <= y, True); Flag.Check(350)

    AreEqual(z == z, True); Flag.Check(300)
    AreEqual(z > z, False); Flag.Check(310)
    AreEqual(z < z, False); Flag.Check(320)
    AreEqual(z != z, False); Flag.Check(330)
    AreEqual(z >= z, True); Flag.Check(340)
    AreEqual(z <= z, True); Flag.Check(350)
    
    # the sequence below need be fixed
    x *= y; AreValueFlagEqual(x, 30, 380)
    x /= y; AreValueFlagEqual(x, 5, 480)
    
    x -= y; AreValueFlagEqual(x, -1, 390)
    x += y; AreValueFlagEqual(x, 5, 450)
    
    x <<= 2; AreValueFlagEqual(x, 5 << 2, 410)
    x >>= 2; AreValueFlagEqual(x, 5, 420)
    
    x ^= y; AreValueFlagEqual(x, 3, 400)
    x |= y; AreValueFlagEqual(x, 7, 470)
    x &= y; AreValueFlagEqual(x, 6, 460)
    
    y = AllOpsClass(4)
    x %= y; AreValueFlagEqual(x, 2, 440)
    
    # , not supported

def test_same_target():
    x = AllOpsClass(4)
    x *= x; AreValueFlagEqual(x, 16, 380)

    y = AllOpsClass(6)
    y /= y; AreValueFlagEqual(y, 1, 480)
    
    x = y = AllOpsClass(3)
    x *= y; AreValueFlagEqual(x, 9, 380); AreEqual(y.Value, 3)

def test_explicitly_call():
    x = AllOpsClass(1)
    y = AllOpsClass(2)
    
    # binary
    z = AllOpsClass.op_Comma(x, y)
    AreEqual(z.Count, 2)
    AreEqual(z[1].Value, 2)
    Flag.Check(490)
    
    # unary
    z = AllOpsClass.op_LogicalNot(x)
    AreEqual(z, False)
    Flag.Check(130)
    
    AreValueFlagEqual(AllOpsClass.op_Increment(x), 2, 150)
    AreValueFlagEqual(AllOpsClass.op_Decrement(x), 0, 160)
    
    # try keyword
    #z = AllOpsClass.op_MultiplicationAssignment(x, other = y)  # bug
    #AreValueFlagEqual(z, 2, 380)

def test_negative_scenario():
    x = InstanceOp()
    y = InstanceOp()
    AssertErrorWithMessage(TypeError, "unsupported operand type(s) for +: 'InstanceOp' and 'InstanceOp'", lambda: x + y)
    
    x = UnaryWithWrongParamOp()
    AssertErrorWithMessage(TypeError, "bad operand type for unary -: 'UnaryWithWrongParamOp'", lambda: -x)
    AssertErrorWithMessage(TypeError, "bad operand type for unary +: 'UnaryWithWrongParamOp'", lambda: +x)
    AssertErrorWithMessage(TypeError, "bad operand type for unary ~: 'UnaryWithWrongParamOp'", lambda: ~x)
    AssertErrorWithMessage(TypeError, "bad operand type for abs(): 'UnaryWithWrongParamOp'", lambda: abs(x))
    
    x = BinaryWithWrongParamOp()
    y = BinaryWithWrongParamOp()
    AssertErrorWithMessage(TypeError, "unsupported operand type(s) for -: 'BinaryWithWrongParamOp' and 'BinaryWithWrongParamOp'", lambda: x - y)
    AssertErrorWithMessage(TypeError, "unsupported operand type(s) for +: 'BinaryWithWrongParamOp' and 'BinaryWithWrongParamOp'", lambda: x + y)
    AssertErrorWithMessage(TypeError, "unsupported operand type(s) for /: 'BinaryWithWrongParamOp' and 'BinaryWithWrongParamOp'", lambda: x / y)
    
    AssertErrorWithMessage(TypeError, "bad operand type for unary +: 'BinaryWithWrongParamOp'", lambda: +x)

def test_unusal_signature():
    x = FirstArgOp(2)
    y = FirstArgOp(5)
    #AreValueFlagEqual(x + y, 5, 100) # bug 313481
    
    x = InstanceMethodOp(3)
    y = InstanceMethodOp(7)
    AreValueFlagEqual(x + y, 21, 123)
    AreValueFlagEqual(~y, -7, 125)

def test_one_side_op():
    t = LessThanOp
    x, y = t(3), t(8)
    AreEqual(x < y, True); Flag.Check(328)
    AreEqual(x > y, False); Flag.Check(328)
    
    t = GreaterThanOp
    x, y = t(3), t(8)
    AreEqual(x < y, True); Flag.Check(329)
    AreEqual(x > y, False); Flag.Check(329)
    
    t = LessThanOrEqualOp
    x, y = t(3), t(8)
    AreEqual(x <= y, True); Flag.Check(330)
    AreEqual(x >= y, False); Flag.Check(330)
    
    x, y = t(4), t(4)
    AreEqual(x <= y, True); Flag.Check(330)
    AreEqual(x >= y, True); Flag.Check(330)
    
    t = GreaterThanOrEqualOp
    x, y = t(3), t(8)
    AreEqual(x <= y, True); Flag.Check(331)
    AreEqual(x >= y, False); Flag.Check(331)
    
    t = EqualOp
    x, y = t(3), t(8)
    AreEqual(x == y, False); Flag.Check(332)
    #AreEqual(x != y, True); Flag.Check(332)  # bug 313820
    
    x, y = t(4), t(4)
    AreEqual(x == y, True); Flag.Check(332)
    #AreEqual(x != y, False); Flag.Check(332)  
    
    t = NotEqualOp
    x, y = t(3), t(8)
    #AreEqual(x == y, False); Flag.Check(333)  
    AreEqual(x != y, True); Flag.Check(333)
    
    x, y = t(4), t(4)
    #AreEqual(x == y, True); Flag.Check(333)
    AreEqual(x != y, False); Flag.Check(333)  

def test_no_in_place_op():
    x = NoInPlaceOp(4)
    y = NoInPlaceOp(9)

    # sequence need be fixed
    x += y; AreValueFlagEqual(x, 13, 493)
    x -= y; AreValueFlagEqual(x, 4, 494)
    x *= y; AreValueFlagEqual(x, 36, 495)
    x /= y; AreValueFlagEqual(x, 4, 496)
    x ^= y; AreValueFlagEqual(x, 13, 497)
    x |= y; AreValueFlagEqual(x, 13, 498)
    x &= y; AreValueFlagEqual(x, 9, 499)
    x >>= 2; AreValueFlagEqual(x, 2, 500)
    x <<= 2; AreValueFlagEqual(x, 8, 501)
    x %= y; AreValueFlagEqual(x, 8, 502)    
    
def test_python_style():
    x = AllOpsClass(7)
    y = AllOpsClass(2)
    
    # http://www.python.org/doc/ref/numeric-types.html
    
    z = AllOpsClass.__add__(x, y); AreValueFlagEqual(z, 9, 200)
    z = AllOpsClass.__sub__(x, y); AreValueFlagEqual(z, 5, 210)
    z = AllOpsClass.__mul__(x, y); AreValueFlagEqual(z, 14, 220)
    z = AllOpsClass.__div__(x, y); AreValueFlagEqual(z, 3, 230)
    z = AllOpsClass.__mod__(x, y); AreValueFlagEqual(z, 1, 240)
    z = AllOpsClass.__xor__(x, y); AreValueFlagEqual(z, 5, 250)
    z = AllOpsClass.__and__(x, y); AreValueFlagEqual(z, 2, 260)
    z = AllOpsClass.__or__(x, y); AreValueFlagEqual(z, 7, 270)
    z = AllOpsClass.__lshift__(x, 2); AreValueFlagEqual(z, 7 << 2, 280)
    z = AllOpsClass.__rshift__(x, 2); AreValueFlagEqual(z, 7 >> 2, 290)
    
    z = AllOpsClass.__radd__(y, x); AreValueFlagEqual(z, 9, 200)
    z = AllOpsClass.__rsub__(y, x); AreValueFlagEqual(z, 5, 210)
    z = AllOpsClass.__rmul__(y, x); AreValueFlagEqual(z, 14, 220)
    z = AllOpsClass.__rdiv__(y, x); AreValueFlagEqual(z, 3, 230)
    z = AllOpsClass.__rmod__(y, x); AreValueFlagEqual(z, 1, 240)
    z = AllOpsClass.__rxor__(y, x); AreValueFlagEqual(z, 5, 250)
    z = AllOpsClass.__rand__(y, x); AreValueFlagEqual(z, 2, 260)
    z = AllOpsClass.__ror__(y, x); AreValueFlagEqual(z, 7, 270)
    
    # bad msg? 'type'
    AssertErrorWithMessage(AttributeError, "'type' object has no attribute '__rlshift__'", lambda: AllOpsClass.__rlshift__(2, x))
    AssertErrorWithMessage(AttributeError, "'type' object has no attribute '__rrshift__'", lambda: AllOpsClass.__rrshift__(2, x))

    z = AllOpsClass.__iadd__(x, y); AreValueFlagEqual(z, 9, 450)
    z = AllOpsClass.__isub__(x, y); AreValueFlagEqual(z, 5, 390)
    z = AllOpsClass.__imul__(x, y); AreValueFlagEqual(z, 14, 380)
    z = AllOpsClass.__idiv__(x, y); AreValueFlagEqual(z, 3, 480)
    z = AllOpsClass.__imod__(x, y); AreValueFlagEqual(z, 1, 440)
    z = AllOpsClass.__ixor__(x, y); AreValueFlagEqual(z, 5, 400)
    z = AllOpsClass.__iand__(x, y); AreValueFlagEqual(z, 2, 460)
    z = AllOpsClass.__ior__(x, y); AreValueFlagEqual(z, 7, 470)
    z = AllOpsClass.__ilshift__(x, 2); AreValueFlagEqual(z, 7 << 2, 410)
    z = AllOpsClass.__irshift__(x, 2); AreValueFlagEqual(z, 7 >> 2, 420)
    
    z = AllOpsClass.__neg__(x); AreValueFlagEqual(z, -7, 170)
    z = AllOpsClass.__pos__(x); AreValueFlagEqual(z, 7, 180)
    z = AllOpsClass.__invert__(x); AreValueFlagEqual(z, ~7, 140)

    # http://www.python.org/doc/ref/customization.html
    
    AreEqual(AllOpsClass.__lt__(x, y), False); Flag.Check(320)
    AreEqual(AllOpsClass.__le__(x, y), False); Flag.Check(350)
    AreEqual(AllOpsClass.__eq__(x, y), False); Flag.Check(300)
    AreEqual(AllOpsClass.__ne__(x, y), True); Flag.Check(330)
    AreEqual(AllOpsClass.__gt__(x, y), True); Flag.Check(310)
    AreEqual(AllOpsClass.__ge__(x, y), True); Flag.Check(340)
    
run_test(__name__)

