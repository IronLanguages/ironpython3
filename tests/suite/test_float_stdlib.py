# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_float from StdLib
##

from iptest import is_ironpython, generate_suite, run_test

import test.test_float

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_float)

    if is_ironpython:
        failing_tests = [
            test.test_float.FormatTestCase('test_format'),
            test.test_float.FormatTestCase('test_format_testfile'),
            test.test_float.FormatTestCase('test_issue5864'),
            test.test_float.InfNanTest('test_nan_signs'),
            test.test_float.ReprTestCase('test_short_repr'),
            test.test_float.RoundTestCase('test_large_n'),
            test.test_float.RoundTestCase('test_matches_float_format'),
            test.test_float.RoundTestCase('test_previous_round_bugs'),
        ]

        return generate_suite(tests, failing_tests)

    else:
        return tests

run_test(__name__)
