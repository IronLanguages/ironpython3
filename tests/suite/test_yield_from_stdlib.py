# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_pep380 from StdLib
##

import sys

from iptest import is_ironpython, generate_suite, run_test

if sys.version_info >= (3, 6):
    import test.yield_from as test_yield_from
else:
    import test.test_pep380 as test_yield_from

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test_yield_from, pattern=pattern)

    if is_ironpython:
        failing_tests = [
            test_yield_from.TestPEP380Operation('test_broken_getattr_handling'), # TODO: figure out
            test_yield_from.TestPEP380Operation('test_catching_exception_from_subgen_and_returning'), # TODO: figure out
        ]

        skip_tests = []

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
