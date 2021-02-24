# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_pep380 from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_pep380

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_pep380.TestPEP380Operation('test_attempted_yield_from_loop'))
        suite.addTest(test.test_pep380.TestPEP380Operation('test_attempting_to_send_to_non_generator'))
        suite.addTest(unittest.expectedFailure(test.test_pep380.TestPEP380Operation('test_broken_getattr_handling'))) # TODO: figure out
        suite.addTest(unittest.expectedFailure(test.test_pep380.TestPEP380Operation('test_catching_exception_from_subgen_and_returning'))) # TODO: figure out
        suite.addTest(test.test_pep380.TestPEP380Operation('test_close_with_cleared_frame'))
        suite.addTest(test.test_pep380.TestPEP380Operation('test_conversion_of_sendNone_to_next'))
        suite.addTest(test.test_pep380.TestPEP380Operation('test_custom_iterator_return'))
        suite.addTest(test.test_pep380.TestPEP380Operation('test_delegating_close'))
        suite.addTest(test.test_pep380.TestPEP380Operation('test_delegating_generators_claim_to_be_running'))
        suite.addTest(test.test_pep380.TestPEP380Operation('test_delegating_throw'))
        suite.addTest(test.test_pep380.TestPEP380Operation('test_delegating_throw_to_non_generator'))
        suite.addTest(test.test_pep380.TestPEP380Operation('test_delegation_of_close_to_non_generator'))
        suite.addTest(test.test_pep380.TestPEP380Operation('test_delegation_of_initial_next_to_subgenerator'))
        suite.addTest(test.test_pep380.TestPEP380Operation('test_delegation_of_next_call_to_subgenerator'))
        suite.addTest(test.test_pep380.TestPEP380Operation('test_delegation_of_next_to_non_generator'))
        suite.addTest(test.test_pep380.TestPEP380Operation('test_delegation_of_send'))
        suite.addTest(test.test_pep380.TestPEP380Operation('test_delegator_is_visible_to_debugger'))
        suite.addTest(test.test_pep380.TestPEP380Operation('test_exception_in_initial_next_call'))
        suite.addTest(test.test_pep380.TestPEP380Operation('test_exception_value_crash'))
        suite.addTest(unittest.expectedFailure(test.test_pep380.TestPEP380Operation('test_generator_return_value'))) # https://github.com/IronLanguages/ironpython3/issues/260
        suite.addTest(test.test_pep380.TestPEP380Operation('test_handing_exception_while_delegating_close'))
        suite.addTest(test.test_pep380.TestPEP380Operation('test_handling_exception_while_delegating_send'))
        suite.addTest(unittest.expectedFailure(test.test_pep380.TestPEP380Operation('test_next_and_return_with_value'))) # https://github.com/IronLanguages/ironpython3/issues/260
        suite.addTest(test.test_pep380.TestPEP380Operation('test_raising_exception_in_delegated_next_call'))
        suite.addTest(test.test_pep380.TestPEP380Operation('test_raising_exception_in_initial_next_call'))
        suite.addTest(test.test_pep380.TestPEP380Operation('test_returning_value_from_delegated_throw'))
        suite.addTest(unittest.expectedFailure(test.test_pep380.TestPEP380Operation('test_send_and_return_with_value'))) # https://github.com/IronLanguages/ironpython3/issues/260
        suite.addTest(test.test_pep380.TestPEP380Operation('test_send_tuple_with_custom_generator'))
        suite.addTest(test.test_pep380.TestPEP380Operation('test_throwing_GeneratorExit_into_subgen_that_raises'))
        suite.addTest(test.test_pep380.TestPEP380Operation('test_throwing_GeneratorExit_into_subgen_that_returns'))
        suite.addTest(test.test_pep380.TestPEP380Operation('test_throwing_GeneratorExit_into_subgenerator_that_yields'))
        suite.addTest(test.test_pep380.TestPEP380Operation('test_value_attribute_of_StopIteration_exception'))
        suite.addTest(test.test_pep380.TestPEP380Operation('test_yield_from_empty'))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_pep380, pattern)

run_test(__name__)
