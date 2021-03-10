# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_float from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_float

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_float.FormatFunctionsTestCase('test_getformat'))
        suite.addTest(test.test_float.FormatFunctionsTestCase('test_setformat'))
        suite.addTest(unittest.expectedFailure(test.test_float.FormatTestCase('test_format')))
        suite.addTest(unittest.expectedFailure(test.test_float.FormatTestCase('test_format_testfile')))
        suite.addTest(unittest.expectedFailure(test.test_float.FormatTestCase('test_issue5864')))
        suite.addTest(test.test_float.GeneralFloatCases('test_error_message'))
        suite.addTest(test.test_float.GeneralFloatCases('test_float'))
        suite.addTest(test.test_float.GeneralFloatCases('test_float_containment'))
        suite.addTest(test.test_float.GeneralFloatCases('test_float_memoryview'))
        suite.addTest(test.test_float.GeneralFloatCases('test_float_mod'))
        suite.addTest(test.test_float.GeneralFloatCases('test_float_pow'))
        suite.addTest(test.test_float.GeneralFloatCases('test_float_with_comma'))
        suite.addTest(test.test_float.GeneralFloatCases('test_floatasratio'))
        suite.addTest(test.test_float.GeneralFloatCases('test_floatconversion'))
        suite.addTest(test.test_float.GeneralFloatCases('test_is_integer'))
        suite.addTest(test.test_float.GeneralFloatCases('test_non_numeric_input_types'))
        suite.addTest(test.test_float.HexFloatTestCase('test_ends'))
        suite.addTest(test.test_float.HexFloatTestCase('test_from_hex'))
        suite.addTest(test.test_float.HexFloatTestCase('test_invalid_inputs'))
        suite.addTest(test.test_float.HexFloatTestCase('test_roundtrip'))
        suite.addTest(test.test_float.HexFloatTestCase('test_whitespace'))
        suite.addTest(test.test_float.IEEEFormatTestCase('test_double_specials_do_unpack'))
        suite.addTest(test.test_float.IEEEFormatTestCase('test_float_specials_do_unpack'))
        suite.addTest(test.test_float.InfNanTest('test_inf_as_str'))
        suite.addTest(test.test_float.InfNanTest('test_inf_from_str'))
        suite.addTest(test.test_float.InfNanTest('test_inf_signs'))
        suite.addTest(test.test_float.InfNanTest('test_nan_as_str'))
        suite.addTest(test.test_float.InfNanTest('test_nan_from_str'))
        suite.addTest(unittest.expectedFailure(test.test_float.InfNanTest('test_nan_signs')))
        suite.addTest(test.test_float.ReprTestCase('test_repr'))
        suite.addTest(unittest.expectedFailure(test.test_float.ReprTestCase('test_short_repr')))
        suite.addTest(unittest.expectedFailure(test.test_float.RoundTestCase('test_format_specials')))
        suite.addTest(test.test_float.RoundTestCase('test_inf_nan'))
        suite.addTest(unittest.expectedFailure(test.test_float.RoundTestCase('test_large_n')))
        suite.addTest(unittest.expectedFailure(test.test_float.RoundTestCase('test_matches_float_format')))
        suite.addTest(test.test_float.RoundTestCase('test_overflow'))
        suite.addTest(unittest.expectedFailure(test.test_float.RoundTestCase('test_previous_round_bugs')))
        suite.addTest(test.test_float.RoundTestCase('test_small_n'))
        suite.addTest(test.test_float.UnknownFormatTestCase('test_double_specials_dont_unpack'))
        suite.addTest(test.test_float.UnknownFormatTestCase('test_float_specials_dont_unpack'))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_float, pattern)

run_test(__name__)
