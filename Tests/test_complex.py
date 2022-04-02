# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

from iptest.type_util import *
from iptest import IronPythonTestCase, run_test

class ComplexTest(IronPythonTestCase):

    def test_from_string(self):
        # complex from string: negative
        # - space related
        l = ['1.2', '.3', '4e3', '.3e-4', "0.031"]

        for x in l:
            for y in l:
                self.assertRaises(ValueError, complex, "%s +%sj" % (x, y))
                self.assertRaises(ValueError, complex, "%s+ %sj" % (x, y))
                self.assertRaises(ValueError, complex, "%s - %sj" % (x, y))
                self.assertRaises(ValueError, complex, "%s-  %sj" % (x, y))
                self.assertRaises(ValueError, complex, "%s-\t%sj" % (x, y))
                self.assertRaises(ValueError, complex, "%sj+%sj" % (x, y))
                self.assertEqual(complex("   %s+%sj" % (x, y)), complex(" %s+%sj  " % (x, y)))

    def test_constructor(self):
        import sys

        class my_complex_number:
            def __init__(self, value):
                self.value = value
            def __complex__(self):
                return self.value

        class my_float_number:
            def __init__(self, value):
                self.value = value
            def __float__(self):
                return self.value

        class my_index_number:
            def __init__(self, value):
                self.value = value
            def __index__(self):
                return self.value

        self.assertEqual(complex(), 0j)
        self.assertEqual(complex(1), 1+0j)
        self.assertEqual(complex(1.0), 1+0j)
        self.assertEqual(complex(1, 2), 1+2j)
        self.assertEqual(complex(1, 2.0), 1+2j)
        self.assertEqual(complex(my_complex_number(2j)), 2j)
        self.assertEqual(complex(my_float_number(1.0)), 1+0j)
        self.assertEqual(complex(my_float_number(1.0), my_float_number(2.0)), 1+2j)
        self.assertEqual(complex(my_complex_number(1.0+0j), my_float_number(2.0)), 1+2j)
        if sys.version_info >= (3,8) or sys.implementation.name == 'ironpython':
            self.assertEqual(complex(my_complex_number(1.0+0j), my_index_number(2)), 1+2j)
        else:
            self.assertRaisesMessage(TypeError, "complex() second argument must be a number, not 'my_index_number'", complex, my_complex_number(1j), my_index_number(2))

        def bad_return_msg(x):
            if sys.version_info >= (3,7) or sys.implementation.name == 'ironpython':
                return "__complex__ returned non-complex (type {0})".format(x)
            else:
                return "__complex__ should return a complex object"

        self.assertRaisesMessage(TypeError, bad_return_msg("int"), complex, my_complex_number(1))
        self.assertRaisesMessage(TypeError, bad_return_msg("int"), complex, my_complex_number(big(1)))
        self.assertRaisesMessage(TypeError, bad_return_msg("float"), complex, my_complex_number(1.0))
        self.assertRaisesMessage(TypeError, bad_return_msg("NoneType"), complex, my_complex_number(None))
        self.assertRaisesMessage(TypeError, bad_return_msg("int"), complex, my_complex_number(1), 2)
        self.assertRaisesMessage(TypeError, bad_return_msg("int"), complex, my_complex_number(1), 2.0)
        self.assertRaisesMessage(TypeError, bad_return_msg("int"), complex, my_complex_number(1), 2j)
        self.assertRaisesMessage(TypeError, bad_return_msg("float"), complex, my_complex_number(1.0), 2)
        self.assertRaisesMessage(TypeError, bad_return_msg("float"), complex, my_complex_number(1.0), None)

        self.assertRaisesPartialMessage(TypeError, "__float__ returned non-float (type int)", complex, my_float_number(1))
        self.assertRaisesPartialMessage(TypeError, "__float__ returned non-float (type complex)", complex, my_float_number(1j))

        self.assertRaisesMessage(TypeError, "complex() can't take second arg if first is a string", complex, 'abc', 'abc')
        self.assertRaisesMessage(TypeError, "complex() can't take second arg if first is a string", complex, 'abc', None)
        self.assertRaisesMessage(TypeError, "complex() second arg can't be a string", complex, my_complex_number(1), 'abc')
        self.assertRaisesMessage(TypeError, "complex() second arg can't be a string", complex, my_float_number(1), 'abc')
        self.assertRaisesMessage(TypeError, "complex() second arg can't be a string", complex, None, 'abc')

        self.assertRaisesMessage(TypeError, "complex() first argument must be a string or a number, not 'NoneType'", complex, None)
        self.assertRaisesMessage(TypeError, "complex() first argument must be a string or a number, not 'NoneType'", complex, None, 2)
        self.assertRaisesMessage(TypeError, "complex() first argument must be a string or a number, not 'NoneType'", complex, None, None)
        self.assertRaisesMessage(TypeError, "complex() first argument must be a string or a number, not 'bytes'", complex, b"1", None)

        # the following results are surprising, bug in CPython?
        self.assertRaisesMessage(TypeError, "complex() second argument must be a number, not 'my_complex_number'", complex, my_complex_number(1j), my_complex_number(2))
        self.assertRaisesMessage(TypeError, "complex() second argument must be a number, not 'my_complex_number'", complex, my_complex_number(1j), my_complex_number(2.0))
        self.assertRaisesMessage(TypeError, "complex() second argument must be a number, not 'my_complex_number'", complex, my_complex_number(1j), my_complex_number(2j))

        self.assertEqual(complex(my_complex_number(1j), 2j), -2+1j)

        if sys.version_info >= (3, 8) or sys.implementation.name == 'ironpython':
            self.assertWarns(DeprecationWarning, complex, my_complex_number(mycomplex()))

        self.assertRaisesMessage(OverflowError, "int too large to convert to float", complex, 1<<10000)
        self.assertRaisesMessage(OverflowError, "int too large to convert to float", complex, 1<<10000, 1)
        self.assertRaisesMessage(OverflowError, "int too large to convert to float", complex, 1, 1<<10000)

        class complex_with_complex(complex):
            def __complex__(self):
                return 1j

        class complex_with_float(complex):
            def __float__(self):
                return 1.0

        class complex_with_index(complex):
            def __index__(self):
                return 1

        self.assertEqual(complex(complex_with_complex(2)), 1j)
        self.assertEqual(complex(complex_with_float(2)), 2+0j)
        self.assertEqual(complex(complex_with_index(2)), 2+0j)

        c = mycomplex(2)
        c.__complex__ = lambda: 1j
        self.assertEqual(complex(c), 2+0j)

        class float_with_float(float):
            def __float__(self):
                return 1.0

        class float_with_index(float):
            def __index__(self):
                return 1

        self.assertEqual(complex(float_with_float(2)), 1+0j)
        self.assertEqual(complex(float_with_index(2)), 2+0j)

        class int_with_float(int):
            def __float__(self):
                return 1.0

        class int_with_index(int):
            def __index__(self):
                return 1

        self.assertEqual(complex(int_with_float(2)), 1+0j)
        self.assertEqual(complex(int_with_index(2)), 2+0j)

    def test_misc(self):
        self.assertEqual(mycomplex(), complex())
        a = mycomplex(1)
        b = mycomplex(1,0)
        c = complex(1)
        d = complex(1,0)

        for x in [a,b,c,d]:
            for y in [a,b,c,d]:
                self.assertEqual(x,y)

        self.assertEqual(a ** 2, a)
        self.assertEqual(a-complex(), a)
        self.assertEqual(a+complex(), a)
        self.assertEqual(complex()/a, complex())
        self.assertEqual(complex()*a, complex())
        with self.assertRaises(TypeError): # can't mod complex numbers
            complex() % a
        with self.assertRaises(TypeError): # can't take floor of complex number
            complex() // a
        self.assertEqual(complex(2), complex(2, 0))

    def test_inherit(self):
        class mycomplex(complex): pass

        a = mycomplex(2+1j)
        self.assertEqual(a.real, 2)
        self.assertEqual(a.imag, 1)


    def test_repr(self):
        self.assertEqual(repr(1-6j), '(1-6j)')


    def test_infinite(self):
        self.assertEqual(repr(1.0e340j),  'infj')
        self.assertEqual(repr(-1.0e340j),'(-0-infj)')

run_test(__name__)