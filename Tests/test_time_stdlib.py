# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_time from StdLib
##

from iptest import is_ironpython, generate_suite, run_test

import test.test_time

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_time)

    if is_ironpython:

        failing_tests = [
            test.test_time.TestAsctime4dyear('test_large_year'), # ValueError: year is too high
            test.test_time.TestAsctime4dyear('test_negative'), # ValueError: year is too low
            test.test_time.TimeTestCase('test_asctime'), # ValueError: year is too high
            test.test_time.TimeTestCase('test_asctime_bounding_check'), # ValueError: Hour, Minute, and Second parameters describe an un-representable DateTime.
            test.test_time.TimeTestCase('test_clock'), # NotImplementedError: get_clock_info('clock')
            test.test_time.TimeTestCase('test_get_clock_info'), # NotImplementedError: get_clock_info
            test.test_time.TimeTestCase('test_insane_timestamps'), # ValueError: unreasonable date/time
            test.test_time.TimeTestCase('test_mktime_error'), # ValueError: year is too low
            test.test_time.TimeTestCase('test_process_time'), # AttributeError: 'module' object has no attribute 'process_time'
            test.test_time.TimeTestCase('test_strftime_bounding_check'), # ValueError: Hour, Minute, and Second parameters describe an un-representable DateTime.
            test.test_time.TimeTestCase('test_time'), # NotImplementedError: get_clock_info('time')
            test.test_time.TimeTestCase('test_default_values_for_zero'), # AssertionError: '2000 01 01 00 00 00 1 001' != '2000 01 01 00 00 00 6 001'
        ]

        skip_tests = []

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
