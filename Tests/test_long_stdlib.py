# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_long from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_long

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_long.LongTest('test__format__'))
        suite.addTest(test.test_long.LongTest('test_access_to_nonexistent_digit_0'))
        suite.addTest(test.test_long.LongTest('test_bit_length'))
        suite.addTest(test.test_long.LongTest('test_bitop_identities'))
        suite.addTest(test.test_long.LongTest('test_conversion'))
        suite.addTest(unittest.expectedFailure(test.test_long.LongTest('test_correctly_rounded_true_division'))) # https://github.com/IronLanguages/ironpython3/issues/907
        suite.addTest(test.test_long.LongTest('test_division'))
        suite.addTest(unittest.expectedFailure(test.test_long.LongTest('test_float_conversion'))) # https://github.com/IronLanguages/ironpython3/issues/907
        suite.addTest(test.test_long.LongTest('test_float_overflow'))
        suite.addTest(test.test_long.LongTest('test_format'))
        suite.addTest(test.test_long.LongTest('test_from_bytes'))
        suite.addTest(test.test_long.LongTest('test_karatsuba'))
        suite.addTest(test.test_long.LongTest('test_logs'))
        suite.addTest(test.test_long.LongTest('test_long'))
        suite.addTest(test.test_long.LongTest('test_mixed_compares'))
        suite.addTest(test.test_long.LongTest('test_nan_inf'))
        suite.addTest(test.test_long.LongTest('test_round'))
        suite.addTest(test.test_long.LongTest('test_shift_bool'))
        suite.addTest(unittest.expectedFailure(test.test_long.LongTest('test_small_ints'))) # https://github.com/IronLanguages/ironpython3/issues/975
        suite.addTest(test.test_long.LongTest('test_to_bytes'))
        suite.addTest(unittest.expectedFailure(test.test_long.LongTest('test_true_division'))) # https://github.com/IronLanguages/ironpython3/issues/907
        return suite

    else:
        return loader.loadTestsFromModule(test.test_long, pattern)

run_test(__name__)
