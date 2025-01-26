# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_scope from StdLib
##

from iptest import is_ironpython, generate_suite, run_test

import test.test_scope

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_scope, pattern=pattern)

    if is_ironpython:
        failing_tests = []

        skip_tests = [
            test.test_scope.ScopeTests('testLeaks'), # https://github.com/IronLanguages/ironpython3/issues/1056
        ]

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
