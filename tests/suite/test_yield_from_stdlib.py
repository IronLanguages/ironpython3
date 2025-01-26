# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_yield_from from StdLib
##

from iptest import is_ironpython, generate_suite, run_test

import test.test_yield_from

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_yield_from, pattern=pattern)

    if is_ironpython:
        failing_tests = [
            test.test_yield_from.TestPEP380Operation('test_broken_getattr_handling'), # TODO: figure out
            test.test_yield_from.TestPEP380Operation('test_catching_exception_from_subgen_and_returning'), # TODO: figure out
        ]

        return generate_suite(tests, failing_tests)

    else:
        return tests

run_test(__name__)
