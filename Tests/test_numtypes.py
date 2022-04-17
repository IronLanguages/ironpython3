# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

"""
This test validates integrity of the operators implemented on the .NET data types
(Byte, SByte, ... UInt64, Single, Double). It does so by evaluating given operator
using standard Python types (int, long, float) and then comparing the results with
results produced by the operators implemented on the .NET types.
"""

import unittest
from operator import add, sub, mul, mod, and_, or_, xor, floordiv, truediv, lshift, rshift, neg, pos, abs, invert

from iptest import is_cli, big, run_test, skipUnlessIronPython
from iptest.type_util import *

if is_cli:
    from System import Boolean, Byte, UInt16, UInt32, UInt64, SByte, Int16, Int32, Int64, Single, Double
    import clr

biops_math_add = [
    ("add", add),
    ]

biops_math_sub = [
    ("sub", sub),
    ]

biops_math_mul = [
    ("mul", mul),
    ]

biops_math_floordiv = [
    ("floordiv", floordiv),
    ]

biops_math_truediv = [
    ("truediv", truediv),
    ]

biops_math_mod = [
    ("mod", mod),
    ]

biops_math_pow = [
    ("pow", pow),
    #("divmod", divmod) #, !!! not supporting divmod on non-standard types
    ]

biops_bool_simple = [
    ("and", and_),
    ("or", or_),
    ("xor", xor),
    ]

biops_bool_shift = [
    ("lshift", lshift),
    ("rshift", rshift),
    ]


unops = [
    ("neg", neg),
    ("pos", pos),
    ("abs", abs),
    ("invert", invert),
    ]

def get_clr_values(string, types):
    clr_values = []
    for t in types:
        try:
            r = t.Parse(string)
            clr_values.append(r)
        except:     # do not include values that cannot be parsed as given type
            pass
    return clr_values


# TODO: check if still true
# Some values are not generated (myint, mylong, myfloat) because of the semantic difference
# between calling (2L).__div__(single) and (mylong(2L)).__div__(single)

def get_values(values, itypes, ftypes):
    """
    This will return structure of values converted to variety of types as a list of tuples:
    [ ...
    ( python_value, [ all_values ] ),
    ... ]

    all_values: Byte, UInt16, UInt32, UInt64, SByte, Int16, Int32, Int64, myint, Single, Double, myfloat, Complex, mycomplex
    """
    all = []
    for v in values:
        sv  = str(v)

        py  = int(v)
        clr = get_clr_values(sv, itypes)
        clr.append(int(py))
        clr.append(big(int(py)))
        clr.append(myint(py))
        all.append( (py, clr) )

        py  = float(v)
        clr = get_clr_values(sv, ftypes)
        clr.append(myfloat(py))
        all.append( (py, clr) )

        for imag in [0j, 1j, -1j]:
            py = complex(v + imag)
            all.append( (py, [ py, mycomplex(py) ] ) )

    all.append( (True, [ True ] ))
    all.append( (False, [ False ] ))

    return all


def mystr(x):
    if isinstance(x, tuple):
        return "(" + ", ".join(mystr(e) for e in x) + ")"
    elif isinstance(x, Single):
        return str(round(float(str(x)), 3))
    elif isinstance(x, Double):
        return str(round(x, 3))
    else:
        return str(x)

def get_message(a, b, op, x_s, x_v, g_s, g_v):
    return """
    Math test failed, operation: %(op)s
    %(op)s( (%(ta)s) (%(a)s), (%(tb)s) (%(b)s) )
    Expected: (%(x_s)s, %(x_v)s)
    Got:      (%(g_s)s, %(g_v)s)
    """ % {
        'ta'  : str(a.GetType()),
        'tb'  : str(b.GetType()),
        'a'   : str(a),
        'b'   : str(b),
        'op'  : str(op),
        'x_s' : str(x_s),
        'x_v' : str(x_v),
        'g_s' : str(g_s),
        'g_v' : str(g_v)
    }

def get_messageun(a, op, x_s, x_v, g_s, g_v):
    return """
    Math test failed, operation: %(op)s
    %(op)s( (%(ta)s) (%(a)s) )
    Expected: (%(x_s)s, %(x_v)s)
    Got:      (%(g_s)s, %(g_v)s)
    """ % {
        'ta'  : str(a.GetType()),
        'a'   : str(a),
        'op'  : str(op),
        'x_s' : str(x_s),
        'x_v' : str(x_v),
        'g_s' : str(g_s),
        'g_v' : str(g_v)
    }

def calc_1(op, arg1):
    try:
        return True, op(arg1)
    except Exception as e:
        return False, e.clsException

def calc_2(op, arg1, arg2):
    try:
        return True, op(arg1, arg2)
    except Exception as e:
        return False, e.clsException

def calc_0(op):
    try:
        return True, op()
    except Exception as e:
        return False, e.clsException

def extensible(l, r):
    ii = isinstance
    return ii(l, myint) or ii(l, myfloat) or ii(l, mycomplex) or ii(r, myint) or ii(r, myfloat) or ii(r, mycomplex)

if is_cli:
    values = [-2, -3, -5, 2, 3, 5, 0]
    itypes = [Byte, UInt16, UInt32, UInt64, SByte, Int16, Int32, Int64]
    ftypes = [Single, Double]
    all = get_values(values, itypes, ftypes)

@skipUnlessIronPython()
class NumTypesTest(unittest.TestCase):
    def verify_b(self, a, b, op, x_s, x_v, g_s, g_v):
        if not x_s == g_s:
            self.fail(get_message(a, b, op, x_s, x_v, g_s, g_v))

        if x_s:
            # same value
            if not mystr(x_v) == mystr(g_v):
                self.fail(get_message(a, b, op, x_s, x_v, g_s, g_v))
        else:
            # same exception
            if not type(x_v) == type(g_v):
                self.fail(get_message(a, b, op, x_s, x_v, g_s, g_v))

    def verify_u(self, a, op, x_s, x_v, g_s, g_v):
        if not x_s == g_s:
            self.fail(get_messageun(a, op, x_s, x_v, g_s, g_v))

        if x_s:
            # same value
            if not mystr(x_v) == mystr(g_v):
                self.fail(get_messageun(a, op, x_s, x_v, g_s, g_v))
        else:
            # unary operator should never fail
            if not type(x_v) == type(g_v):
                self.fail(get_messageun(a, op, x_s, x_v, g_s, g_v))



    def verify_implemented_b(self, implemented, op, a, b):
        if not implemented:
            self.fail("Operation not defined: %(op)s( (%(ta)s) (%(a)s), (%(tb)s) (%(b)s) )" % {
                'op' : op, 'ta' : str(a.GetType()), 'tb' : str(b.GetType()), 'a'  : str(a), 'b'  : str(b)
                })

    def verify_implemented_u(self, implemented, op, a):
        if not implemented:
            self.fail("Operation not defined: %(op)s( (%(ta)s) (%(a)s))" % { 'op' : op, 'ta' : str(a.GetType()), 'a'  : str(a), })


    def validate_binary_ops(self, all, biops):
        total = 0
        last  = 0
        for name, bin in biops:
            l_name = "__" + name + "__"
            r_name = "__r" + name + "__"

            for l_rec in all:
                for r_rec in all:
                    py_l, clr_l = l_rec
                    py_r, clr_r = r_rec

                    x_s, x_v = calc_2(bin, py_l, py_r)

                    for l in clr_l:
                        for r in clr_r:
                            implemented = False

                            # direct binary operator
                            g_s, g_v = calc_2(bin, l, r)
                            if g_v != NotImplemented:
                                implemented = True
                                self.verify_b(l, r, name, x_s, x_v, g_s, g_v)
                                total += 1

                            # call __xxx__ and __rxxx__ for all types
                            # l.__xxx__(r)
                            m = getattr(l, l_name, None)
                            if m is not None:
                                g_s, g_v = calc_1(m, r)
                                if g_v != NotImplemented:
                                    implemented = True
                                    self.verify_b(l, r, l_name, x_s, x_v, g_s, g_v)
                                    total += 1

                            # r.__rxxx__(l)
                            m = getattr(r, r_name, None)
                            if m is not None:
                                g_s, g_v = calc_1(m, l)
                                if g_v != NotImplemented:
                                    implemented = True
                                    self.verify_b(l, r, r_name, x_s, x_v, g_s, g_v)
                                    total += 1

                            self.verify_implemented_b(implemented, name, l, r)

                            if total - last > 10000:
                                print(".", end=' ')
                                last = total

        return total

    def validate_unary_ops(self, all):
        total = 0
        for l_rec in all:
            py_l, clr_l = l_rec

            for name, un in unops:
                x_s, x_v = calc_1(un, py_l)

                for l in clr_l:
                    implemented = False

                    # direct unary operator
                    g_s, g_v = calc_1(un, l)
                    if g_v != NotImplemented:
                        implemented = True
                        self.verify_u(l, name, x_s, x_v, g_s, g_v)
                        total += 1

                    # l.__xxx__()
                    m_name = "__" + name + "__"
                    if hasattr(l, m_name):
                        m = getattr(l, m_name)
                        g_s, g_v = calc_0(m)
                        if g_v != NotImplemented:
                            implemented = True
                            self.verify_u(l, m_name, x_s, x_v, g_s, g_v)
                            total += 1

                    self.verify_implemented_u(implemented, name, l)
        return total

    def validate_constructors(self, values):
        total = 0
        for value in values:
            for first in clr_int_types:
                if first in clr_unsigned_types and value < 0:
                    continue
                v1 = first(value)
                for second in clr_int_types:
                    if second in clr_unsigned_types and value < 0:
                        continue
                    v2 = first(second((value)))
                total += 1
        return total

    def test_validate_biops_bool_simple(self):
        total = self.validate_binary_ops(all, biops_bool_simple)
        print(total, "tests ran.")

    def test_validate_biops_bool_shift(self):
        total = self.validate_binary_ops(all, biops_bool_shift)
        print(total, "tests ran.")

    def test_validate_biops_math_add(self):
        total = self.validate_binary_ops(all, biops_math_add)
        print(total, "tests ran.")

    def test_validate_biops_math_sub(self):
        total = self.validate_binary_ops(all, biops_math_sub)
        print(total, "tests ran.")

    def test_validate_biops_math_mul(self):
        total = self.validate_binary_ops(all, biops_math_mul)
        print(total, "tests ran.")

    def test_validate_biops_math_floordiv(self):
        total = self.validate_binary_ops(all, biops_math_floordiv)
        print(total, "tests ran.")

    def test_validate_biops_math_truediv(self):
        total = self.validate_binary_ops(all, biops_math_truediv)
        print(total, "tests ran.")

    def test_validate_biops_math_mod(self):
        total = self.validate_binary_ops(all, biops_math_mod)
        print(total, "tests ran.")

    def test_validate_biops_math_pow(self):
        total = self.validate_binary_ops(all, biops_math_pow)
        print(total, "tests ran.")

    def test_validate_unary_ops(self):
        total = self.validate_unary_ops(all)
        print(total, "tests ran.")

    def test_validate_constructors(self):
        total = self.validate_constructors(values)
        print(total, "tests ran.")

run_test(__name__)
