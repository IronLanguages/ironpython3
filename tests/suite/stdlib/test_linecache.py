# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_linecache from StdLib
##

import sys

from iptest import is_ironpython, generate_suite, run_test

import test.test_linecache

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_linecache, pattern=pattern)

    if is_ironpython:
        # There are no failing tests when running via test_linecache_stdlib, but when running test_linecache directly there are some failures.
        # See https://github.com/IronLanguages/ironpython3/issues/1245 for details.

        failing_tests = []

        skip_tests = []

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
