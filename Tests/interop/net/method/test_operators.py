# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.
'''
All operators
'''

import unittest

from iptest import IronPythonTestCase, run_test, skipUnlessIronPython

@skipUnlessIronPython()
class OperatorsTest(IronPythonTestCase):
    def setUp(self):
        super(OperatorsTest, self).setUp()
        self.add_clr_assemblies("operators", "typesamples")

    def AreValueFlagEqual(self, x, v, f):
        from Merlin.Testing import Flag
        self.assertEqual(x.Value, v)
        Flag.Check(f)

    def test_all_ops(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.Call import AllOpsClass
        x = AllOpsClass(5)
        y = AllOpsClass(6)
        z = AllOpsClass(-6)

        #if x: pass
        #else: self.assertUnreachable()
        #
        #Flag.Check(100)
        #
        #if z: self.assertUnreachable()
        #else: pass
        #
        #Flag.Check(100)
        #
        #if not x: self.assertUnreachable()
        #else: pass
        #
        #Flag.Check(100)
        #
        #if not z: pass
        #else: self.assertUnreachable()
        #
        #Flag.Check(100)

        # ! is not supported in python

        self.AreValueFlagEqual(~x, ~5, 140)

        # ++/-- not supported

        self.AreValueFlagEqual(+x, 5, 180)
        self.AreValueFlagEqual(-x, -5, 170)

        self.AreValueFlagEqual(+z, -6, 180)
        self.AreValueFlagEqual(-z, 6, 170)

        self.AreValueFlagEqual(x + y, 11, 200)
        self.AreValueFlagEqual(x - y, -1, 210)
        self.AreValueFlagEqual(x * y, 30, 220)
        self.AreValueFlagEqual(x / y, 0, 230)
        self.AreValueFlagEqual(x % y, 5, 240)
        self.AreValueFlagEqual(x ^ y, 5^6, 250)
        self.AreValueFlagEqual(x & y, 5&6, 260)
        self.AreValueFlagEqual(x | y, 5|6, 270)

        self.AreValueFlagEqual(x << 2, 5 << 2, 280)
        self.AreValueFlagEqual(x >> 1, 5 >> 1, 290)

        self.assertEqual(x == y, False); Flag.Check(300)
        self.assertEqual(x > y, False); Flag.Check(310)
        self.assertEqual(x < y, True); Flag.Check(320)
        self.assertEqual(x != y, True); Flag.Check(330)
        self.assertEqual(x >= y, False); Flag.Check(340)
        self.assertEqual(x <= y, True); Flag.Check(350)

        self.assertEqual(z == z, True); Flag.Check(300)
        self.assertEqual(z > z, False); Flag.Check(310)
        self.assertEqual(z < z, False); Flag.Check(320)
        self.assertEqual(z != z, False); Flag.Check(330)
        self.assertEqual(z >= z, True); Flag.Check(340)
        self.assertEqual(z <= z, True); Flag.Check(350)

        # the sequence below need be fixed
        x *= y; self.AreValueFlagEqual(x, 30, 380)
        x /= y; self.AreValueFlagEqual(x, 5, 480)

        x -= y; self.AreValueFlagEqual(x, -1, 390)
        x += y; self.AreValueFlagEqual(x, 5, 450)

        x <<= 2; self.AreValueFlagEqual(x, 5 << 2, 410)
        x >>= 2; self.AreValueFlagEqual(x, 5, 420)

        x ^= y; self.AreValueFlagEqual(x, 3, 400)
        x |= y; self.AreValueFlagEqual(x, 7, 470)
        x &= y; self.AreValueFlagEqual(x, 6, 460)

        y = AllOpsClass(4)
        x %= y; self.AreValueFlagEqual(x, 2, 440)

        # , not supported

    def test_same_target(self):
        from Merlin.Testing.Call import AllOpsClass
        x = AllOpsClass(4)
        x *= x; self.AreValueFlagEqual(x, 16, 380)

        y = AllOpsClass(6)
        y /= y; self.AreValueFlagEqual(y, 1, 480)

        x = y = AllOpsClass(3)
        x *= y; self.AreValueFlagEqual(x, 9, 380); self.assertEqual(y.Value, 3)

    def test_explicitly_call(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.Call import AllOpsClass
        x = AllOpsClass(1)
        y = AllOpsClass(2)

        # binary
        z = AllOpsClass.op_Comma(x, y)
        self.assertEqual(z.Count, 2)
        self.assertEqual(z[1].Value, 2)
        Flag.Check(490)

        # unary
        z = AllOpsClass.op_LogicalNot(x)
        self.assertEqual(z, False)
        Flag.Check(130)

        self.AreValueFlagEqual(AllOpsClass.op_Increment(x), 2, 150)
        self.AreValueFlagEqual(AllOpsClass.op_Decrement(x), 0, 160)

        # try keyword
        #z = AllOpsClass.op_MultiplicationAssignment(x, other = y)  # bug
        #self.AreValueFlagEqual(z, 2, 380)

    def test_negative_scenario(self):
        from Merlin.Testing.Call import BinaryWithWrongParamOp, InstanceOp, UnaryWithWrongParamOp
        x = InstanceOp()
        y = InstanceOp()
        self.assertRaisesMessage(TypeError, "unsupported operand type(s) for +: 'InstanceOp' and 'InstanceOp'", lambda: x + y)

        x = UnaryWithWrongParamOp()
        self.assertRaisesMessage(TypeError, "bad operand type for unary -: 'UnaryWithWrongParamOp'", lambda: -x)
        self.assertRaisesMessage(TypeError, "bad operand type for unary +: 'UnaryWithWrongParamOp'", lambda: +x)
        self.assertRaisesMessage(TypeError, "bad operand type for unary ~: 'UnaryWithWrongParamOp'", lambda: ~x)
        self.assertRaisesMessage(TypeError, "bad operand type for abs(): 'UnaryWithWrongParamOp'", lambda: abs(x))

        x = BinaryWithWrongParamOp()
        y = BinaryWithWrongParamOp()
        self.assertRaisesMessage(TypeError, "unsupported operand type(s) for -: 'BinaryWithWrongParamOp' and 'BinaryWithWrongParamOp'", lambda: x - y)
        self.assertRaisesMessage(TypeError, "unsupported operand type(s) for +: 'BinaryWithWrongParamOp' and 'BinaryWithWrongParamOp'", lambda: x + y)
        self.assertRaisesMessage(TypeError, "unsupported operand type(s) for /: 'BinaryWithWrongParamOp' and 'BinaryWithWrongParamOp'", lambda: x / y)

        self.assertRaisesMessage(TypeError, "bad operand type for unary +: 'BinaryWithWrongParamOp'", lambda: +x)

    def test_unusal_signature(self):
        from Merlin.Testing.Call import FirstArgOp, InstanceMethodOp
        x = FirstArgOp(2)
        y = FirstArgOp(5)
        #self.AreValueFlagEqual(x + y, 5, 100) # bug 313481

        x = InstanceMethodOp(3)
        y = InstanceMethodOp(7)
        self.AreValueFlagEqual(x + y, 21, 123)
        self.AreValueFlagEqual(~y, -7, 125)

    def test_one_side_op(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.Call import EqualOp, GreaterThanOp, GreaterThanOrEqualOp, LessThanOp, LessThanOrEqualOp, NotEqualOp
        t = LessThanOp
        x, y = t(3), t(8)
        self.assertEqual(x < y, True); Flag.Check(328)
        self.assertEqual(x > y, False); Flag.Check(328)

        t = GreaterThanOp
        x, y = t(3), t(8)
        self.assertEqual(x < y, True); Flag.Check(329)
        self.assertEqual(x > y, False); Flag.Check(329)

        t = LessThanOrEqualOp
        x, y = t(3), t(8)
        self.assertEqual(x <= y, True); Flag.Check(330)
        self.assertEqual(x >= y, False); Flag.Check(330)

        x, y = t(4), t(4)
        self.assertEqual(x <= y, True); Flag.Check(330)
        self.assertEqual(x >= y, True); Flag.Check(330)

        t = GreaterThanOrEqualOp
        x, y = t(3), t(8)
        self.assertEqual(x <= y, True); Flag.Check(331)
        self.assertEqual(x >= y, False); Flag.Check(331)

        t = EqualOp
        x, y = t(3), t(8)
        self.assertEqual(x == y, False); Flag.Check(332)
        #self.assertEqual(x != y, True); Flag.Check(332)  # bug 313820

        x, y = t(4), t(4)
        self.assertEqual(x == y, True); Flag.Check(332)
        #self.assertEqual(x != y, False); Flag.Check(332)

        t = NotEqualOp
        x, y = t(3), t(8)
        #self.assertEqual(x == y, False); Flag.Check(333)
        self.assertEqual(x != y, True); Flag.Check(333)

        x, y = t(4), t(4)
        #self.assertEqual(x == y, True); Flag.Check(333)
        self.assertEqual(x != y, False); Flag.Check(333)

    def test_no_in_place_op(self):
        from Merlin.Testing.Call import NoInPlaceOp
        x = NoInPlaceOp(4)
        y = NoInPlaceOp(9)

        # sequence need be fixed
        x += y; self.AreValueFlagEqual(x, 13, 493)
        x -= y; self.AreValueFlagEqual(x, 4, 494)
        x *= y; self.AreValueFlagEqual(x, 36, 495)
        x /= y; self.AreValueFlagEqual(x, 4, 496)
        x ^= y; self.AreValueFlagEqual(x, 13, 497)
        x |= y; self.AreValueFlagEqual(x, 13, 498)
        x &= y; self.AreValueFlagEqual(x, 9, 499)
        x >>= 2; self.AreValueFlagEqual(x, 2, 500)
        x <<= 2; self.AreValueFlagEqual(x, 8, 501)
        x %= y; self.AreValueFlagEqual(x, 8, 502)

    def test_python_style(self):
        from Merlin.Testing import Flag
        from Merlin.Testing.Call import AllOpsClass
        x = AllOpsClass(7)
        y = AllOpsClass(2)

        # http://www.python.org/doc/ref/numeric-types.html

        z = AllOpsClass.__add__(x, y); self.AreValueFlagEqual(z, 9, 200)
        z = AllOpsClass.__sub__(x, y); self.AreValueFlagEqual(z, 5, 210)
        z = AllOpsClass.__mul__(x, y); self.AreValueFlagEqual(z, 14, 220)
        z = AllOpsClass.__truediv__(x, y); self.AreValueFlagEqual(z, 3, 230)
        z = AllOpsClass.__mod__(x, y); self.AreValueFlagEqual(z, 1, 240)
        z = AllOpsClass.__xor__(x, y); self.AreValueFlagEqual(z, 5, 250)
        z = AllOpsClass.__and__(x, y); self.AreValueFlagEqual(z, 2, 260)
        z = AllOpsClass.__or__(x, y); self.AreValueFlagEqual(z, 7, 270)
        z = AllOpsClass.__lshift__(x, 2); self.AreValueFlagEqual(z, 7 << 2, 280)
        z = AllOpsClass.__rshift__(x, 2); self.AreValueFlagEqual(z, 7 >> 2, 290)

        z = AllOpsClass.__radd__(y, x); self.AreValueFlagEqual(z, 9, 200)
        z = AllOpsClass.__rsub__(y, x); self.AreValueFlagEqual(z, 5, 210)
        z = AllOpsClass.__rmul__(y, x); self.AreValueFlagEqual(z, 14, 220)
        z = AllOpsClass.__rtruediv__(y, x); self.AreValueFlagEqual(z, 3, 230)
        z = AllOpsClass.__rmod__(y, x); self.AreValueFlagEqual(z, 1, 240)
        z = AllOpsClass.__rxor__(y, x); self.AreValueFlagEqual(z, 5, 250)
        z = AllOpsClass.__rand__(y, x); self.AreValueFlagEqual(z, 2, 260)
        z = AllOpsClass.__ror__(y, x); self.AreValueFlagEqual(z, 7, 270)

        # bad msg? 'type'
        self.assertRaisesMessage(AttributeError, "'type' object has no attribute '__rlshift__'", lambda: AllOpsClass.__rlshift__(2, x))
        self.assertRaisesMessage(AttributeError, "'type' object has no attribute '__rrshift__'", lambda: AllOpsClass.__rrshift__(2, x))

        z = AllOpsClass.__iadd__(x, y); self.AreValueFlagEqual(z, 9, 450)
        z = AllOpsClass.__isub__(x, y); self.AreValueFlagEqual(z, 5, 390)
        z = AllOpsClass.__imul__(x, y); self.AreValueFlagEqual(z, 14, 380)
        z = AllOpsClass.__itruediv__(x, y); self.AreValueFlagEqual(z, 3, 480)
        z = AllOpsClass.__imod__(x, y); self.AreValueFlagEqual(z, 1, 440)
        z = AllOpsClass.__ixor__(x, y); self.AreValueFlagEqual(z, 5, 400)
        z = AllOpsClass.__iand__(x, y); self.AreValueFlagEqual(z, 2, 460)
        z = AllOpsClass.__ior__(x, y); self.AreValueFlagEqual(z, 7, 470)
        z = AllOpsClass.__ilshift__(x, 2); self.AreValueFlagEqual(z, 7 << 2, 410)
        z = AllOpsClass.__irshift__(x, 2); self.AreValueFlagEqual(z, 7 >> 2, 420)

        z = AllOpsClass.__neg__(x); self.AreValueFlagEqual(z, -7, 170)
        z = AllOpsClass.__pos__(x); self.AreValueFlagEqual(z, 7, 180)
        z = AllOpsClass.__invert__(x); self.AreValueFlagEqual(z, ~7, 140)

        # http://www.python.org/doc/ref/customization.html

        self.assertEqual(AllOpsClass.__lt__(x, y), False); Flag.Check(320)
        self.assertEqual(AllOpsClass.__le__(x, y), False); Flag.Check(350)
        self.assertEqual(AllOpsClass.__eq__(x, y), False); Flag.Check(300)
        self.assertEqual(AllOpsClass.__ne__(x, y), True); Flag.Check(330)
        self.assertEqual(AllOpsClass.__gt__(x, y), True); Flag.Check(310)
        self.assertEqual(AllOpsClass.__ge__(x, y), True); Flag.Check(340)

run_test(__name__)