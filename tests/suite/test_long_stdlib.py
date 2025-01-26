# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_long from StdLib
##

from iptest import is_ironpython, generate_suite, run_test

import test.test_long

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_long, pattern=pattern)

    if is_ironpython:
        failing_tests = [
            test.test_long.LongTest('test_correctly_rounded_true_division'), # https://github.com/IronLanguages/ironpython3/issues/907
            test.test_long.LongTest('test_float_conversion'), # https://github.com/IronLanguages/ironpython3/issues/907
            test.test_long.LongTest('test_small_ints'), # https://github.com/IronLanguages/ironpython3/issues/975
            test.test_long.LongTest('test_true_division'), # https://github.com/IronLanguages/ironpython3/issues/907
        ]

        return generate_suite(tests, failing_tests)

    else:
        return tests

run_test(__name__)
