# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_string_literals from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_string_literals

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_string_literals.TestLiterals('test_eval_bytes_incomplete'))
        suite.addTest(unittest.expectedFailure(test.test_string_literals.TestLiterals('test_eval_bytes_invalid_escape'))) # https://github.com/IronLanguages/ironpython3/issues/1343
        suite.addTest(test.test_string_literals.TestLiterals('test_eval_bytes_normal'))
        suite.addTest(test.test_string_literals.TestLiterals('test_eval_bytes_raw'))
        suite.addTest(test.test_string_literals.TestLiterals('test_eval_str_incomplete'))
        suite.addTest(unittest.expectedFailure(test.test_string_literals.TestLiterals('test_eval_str_invalid_escape'))) # https://github.com/IronLanguages/ironpython3/issues/1343
        suite.addTest(test.test_string_literals.TestLiterals('test_eval_str_normal'))
        suite.addTest(test.test_string_literals.TestLiterals('test_eval_str_raw'))
        suite.addTest(test.test_string_literals.TestLiterals('test_eval_str_u'))
        suite.addTest(test.test_string_literals.TestLiterals('test_file_iso_8859_1'))
        suite.addTest(test.test_string_literals.TestLiterals('test_file_latin9'))
        suite.addTest(test.test_string_literals.TestLiterals('test_file_latin_1'))
        suite.addTest(test.test_string_literals.TestLiterals('test_file_utf8'))
        suite.addTest(test.test_string_literals.TestLiterals('test_file_utf_8'))
        suite.addTest(test.test_string_literals.TestLiterals('test_file_utf_8_error'))
        suite.addTest(test.test_string_literals.TestLiterals('test_template'))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_string_literals, pattern)

run_test(__name__)
