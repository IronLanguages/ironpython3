# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_raise from StdLib
##

import sys

from iptest import is_ironpython, generate_suite, run_test

import test.test_raise

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_raise, pattern=pattern)

    if is_ironpython:
        failing_tests = [
            test.test_raise.TestContext('test_c_exception_raise'), # AssertionError: None is not an instance of <class 'ZeroDivisionError'>
            test.test_raise.TestContext('test_context_manager'), # AssertionError: None is not an instance of <class 'ZeroDivisionError'>
            test.test_raise.TestTraceback('test_accepts_traceback'), # AssertionError: None != <traceback object at 0x000000000000002B>
        ]

        skip_tests = []

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
