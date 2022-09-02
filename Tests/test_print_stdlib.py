# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_print from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_print

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_print.TestPrint('test_print'))
        suite.addTest(test.test_print.TestPrint('test_print_flush'))
        suite.addTest(unittest.expectedFailure(test.test_print.TestPy2MigrationHint('test_normal_string'))) # https://github.com/IronLanguages/ironpython3/issues/374
        suite.addTest(unittest.expectedFailure(test.test_print.TestPy2MigrationHint('test_stream_redirection_hint_for_py2_migration'))) # https://github.com/IronLanguages/ironpython3/issues/374
        suite.addTest(unittest.expectedFailure(test.test_print.TestPy2MigrationHint('test_string_in_loop_on_same_line'))) # https://github.com/IronLanguages/ironpython3/issues/374
        suite.addTest(unittest.expectedFailure(test.test_print.TestPy2MigrationHint('test_string_with_excessive_whitespace'))) # https://github.com/IronLanguages/ironpython3/issues/374
        suite.addTest(unittest.expectedFailure(test.test_print.TestPy2MigrationHint('test_string_with_leading_whitespace'))) # https://github.com/IronLanguages/ironpython3/issues/374
        suite.addTest(unittest.expectedFailure(test.test_print.TestPy2MigrationHint('test_string_with_semicolon'))) # https://github.com/IronLanguages/ironpython3/issues/374
        suite.addTest(unittest.expectedFailure(test.test_print.TestPy2MigrationHint('test_string_with_soft_space'))) # https://github.com/IronLanguages/ironpython3/issues/374
        return suite

    else:
        return loader.loadTestsFromModule(test.test_print, pattern)

run_test(__name__)
