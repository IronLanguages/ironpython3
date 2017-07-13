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

from iptest.type_util import *
from iptest import run_test

class ComplexTest(unittest.TestCase):

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
        self.assertEqual(complex()%a, complex())
        self.assertEqual(complex() // a, complex())
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
        self.assertEqual(repr(-1.0e340j),'-infj')

run_test(__name__)