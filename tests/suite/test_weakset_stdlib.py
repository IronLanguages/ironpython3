# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_weakset from StdLib
##

from iptest import is_ironpython, generate_suite, run_test

import test.test_weakset

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_weakset, pattern=pattern)

    if is_ironpython:
        failing_tests = [
            test.test_weakset.TestWeakSet('test_len_cycles'), # TODO: figure out
            test.test_weakset.TestWeakSet('test_weak_destroy_and_mutate_while_iterating'), # TODO: figure out
        ]

        return generate_suite(tests, failing_tests)

    else:
        return tests

run_test(__name__)
