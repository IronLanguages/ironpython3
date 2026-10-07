# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_hashlib from StdLib
##

import sys

from iptest import is_ironpython, generate_suite, run_test

import test.test_hashlib

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_hashlib, pattern=pattern)

    if is_ironpython:
        failing_tests = []
        if sys.version_info >= (3, 6):
            failing_tests += [
                test.test_hashlib.HashLibTestCase('test_blake2b'), # NotImplementedError: BLAKE2 tree hashing parameters are not supported
                test.test_hashlib.HashLibTestCase('test_blake2s'), # NotImplementedError: BLAKE2 tree hashing parameters are not supported
                test.test_hashlib.HashLibTestCase('test_case_blake2b_all_parameters'), # NotImplementedError: BLAKE2 tree hashing parameters are not supported
                test.test_hashlib.HashLibTestCase('test_case_blake2s_all_parameters'), # NotImplementedError: BLAKE2 tree hashing parameters are not supported
            ]

        skip_tests = []

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
