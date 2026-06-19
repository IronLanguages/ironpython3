# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_heapq from StdLib
##

import sys

from iptest import is_ironpython, generate_suite, run_test

import test.test_heapq

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_heapq, pattern=pattern)

    if is_ironpython:
        failing_tests = []
        if sys.version_info >= (3, 6):
            failing_tests += [
                test.test_heapq.TestErrorHandlingC('test_comparison_operator_modifiying_heap'), # TypeError: '<' not supported between instances of 'EvilClass' and 'int'
                test.test_heapq.TestErrorHandlingC('test_comparison_operator_modifiying_heap_two_heaps'), # AssertionError: (<class 'IndexError'>, <class 'RuntimeError'>) not raised by heappush
                test.test_heapq.TestErrorHandlingPython('test_comparison_operator_modifiying_heap'), # TypeError: '<' not supported between instances of 'EvilClass' and 'int'
                test.test_heapq.TestModules('test_c_functions'), # AssertionError: 'heapq' != '_heapq'
            ]

        skip_tests = []

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
