# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_opcodes from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_opcodes

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_opcodes.OpcodeTest('test_compare_function_objects'))
        suite.addTest(unittest.expectedFailure(test.test_opcodes.OpcodeTest('test_do_not_recreate_annotations'))) # https://github.com/IronLanguages/ironpython3/issues/106
        suite.addTest(test.test_opcodes.OpcodeTest('test_modulo_of_string_subclasses'))
        suite.addTest(test.test_opcodes.OpcodeTest('test_no_annotations_if_not_needed'))
        suite.addTest(test.test_opcodes.OpcodeTest('test_raise_class_exceptions'))
        suite.addTest(unittest.expectedFailure(test.test_opcodes.OpcodeTest('test_setup_annotations_line'))) # https://github.com/IronLanguages/ironpython3/issues/106
        suite.addTest(test.test_opcodes.OpcodeTest('test_try_inside_for_loop'))
        suite.addTest(unittest.expectedFailure(test.test_opcodes.OpcodeTest('test_use_existing_annotations'))) # https://github.com/IronLanguages/ironpython3/issues/106
        return suite

    else:
        return loader.loadTestsFromModule(test.test_opcodes, pattern)

run_test(__name__)
