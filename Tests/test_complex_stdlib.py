# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_complex from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_complex

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_complex.ComplexTest('test_abs'))
        suite.addTest(test.test_complex.ComplexTest('test_boolcontext'))
        suite.addTest(test.test_complex.ComplexTest('test_conjugate'))
        suite.addTest(test.test_complex.ComplexTest('test_constructor'))
        suite.addTest(test.test_complex.ComplexTest('test_constructor_special_numbers'))
        suite.addTest(test.test_complex.ComplexTest('test_divmod'))
        suite.addTest(test.test_complex.ComplexTest('test_file'))
        suite.addTest(test.test_complex.ComplexTest('test_floordiv'))
        suite.addTest(unittest.expectedFailure(test.test_complex.ComplexTest('test_format')))
        suite.addTest(test.test_complex.ComplexTest('test_getnewargs'))
        suite.addTest(test.test_complex.ComplexTest('test_hash'))
        suite.addTest(test.test_complex.ComplexTest('test_mod'))
        suite.addTest(test.test_complex.ComplexTest('test_neg'))
        suite.addTest(test.test_complex.ComplexTest('test_negated_imaginary_literal'))
        suite.addTest(test.test_complex.ComplexTest('test_negative_zero_repr_str'))
        suite.addTest(test.test_complex.ComplexTest('test_overflow'))
        suite.addTest(test.test_complex.ComplexTest('test_plus_minus_0j'))
        suite.addTest(test.test_complex.ComplexTest('test_pow'))
        suite.addTest(test.test_complex.ComplexTest('test_repr_roundtrip'))
        suite.addTest(test.test_complex.ComplexTest('test_repr_str'))
        suite.addTest(test.test_complex.ComplexTest('test_richcompare'))
        suite.addTest(test.test_complex.ComplexTest('test_richcompare_boundaries'))
        suite.addTest(test.test_complex.ComplexTest('test_truediv'))
        suite.addTest(test.test_complex.ComplexTest('test_underscores'))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_complex, pattern)

run_test(__name__)
