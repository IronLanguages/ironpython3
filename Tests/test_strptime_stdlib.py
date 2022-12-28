# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_strptime from StdLib
##

from iptest import is_ironpython, generate_suite, run_test

import test.test_strptime

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_strptime)

    if is_ironpython:
        failing_tests = [
            test.test_strptime.CalculationTests('test_day_of_week_calculation'), # https://github.com/IronLanguages/ironpython3/issues/1121
            test.test_strptime.CalculationTests('test_gregorian_calculation'), # https://github.com/IronLanguages/ironpython3/issues/1121
            test.test_strptime.CalculationTests('test_julian_calculation'), # https://github.com/IronLanguages/ironpython3/issues/1121
            test.test_strptime.StrptimeTests('test_feb29_on_leap_year_without_year'),
            test.test_strptime.StrptimeTests('test_mar1_comes_after_feb29_even_when_omitting_the_year'),
            test.test_strptime.StrptimeTests('test_timezone'), # https://github.com/IronLanguages/ironpython3/issues/1121
            test.test_strptime.TimeRETests('test_compile'),
        ]
        return generate_suite(tests, failing_tests)

    else:
        return tests

run_test(__name__)
