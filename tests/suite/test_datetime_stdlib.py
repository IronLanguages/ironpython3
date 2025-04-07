# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_datetime from StdLib
##

from iptest import is_ironpython, is_mono, is_arm64, generate_suite, run_test

import test.datetimetester

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.datetimetester)

    if is_ironpython:
        failing_tests = [
            test.datetimetester.TestDate('test_backdoor_resistance'),
            test.datetimetester.TestDate('test_insane_fromtimestamp'),
            test.datetimetester.TestDateTime('test_backdoor_resistance'),
            test.datetimetester.TestDateTime('test_extreme_timedelta'),
            test.datetimetester.TestDateTime('test_insane_fromtimestamp'),
            test.datetimetester.TestDateTime('test_insane_utcfromtimestamp'),
            test.datetimetester.TestDateTime('test_microsecond_rounding'),
            test.datetimetester.TestDateTime('test_strftime_with_bad_tzname_replace'),
            test.datetimetester.TestDateTimeTZ('test_backdoor_resistance'),
            test.datetimetester.TestDateTimeTZ('test_even_more_compare'),
            test.datetimetester.TestDateTimeTZ('test_extreme_hashes'),
            test.datetimetester.TestDateTimeTZ('test_extreme_timedelta'),
            test.datetimetester.TestDateTimeTZ('test_insane_fromtimestamp'),
            test.datetimetester.TestDateTimeTZ('test_insane_utcfromtimestamp'),
            test.datetimetester.TestDateTimeTZ('test_microsecond_rounding'),
            test.datetimetester.TestDateTimeTZ('test_mixed_compare'),
            test.datetimetester.TestDateTimeTZ('test_strftime_with_bad_tzname_replace'),
            test.datetimetester.TestDateTimeTZ('test_tz_aware_arithmetic'),
            test.datetimetester.TestSubclassDateTime('test_backdoor_resistance'),
            test.datetimetester.TestSubclassDateTime('test_extreme_timedelta'),
            test.datetimetester.TestSubclassDateTime('test_insane_fromtimestamp'),
            test.datetimetester.TestSubclassDateTime('test_insane_utcfromtimestamp'),
            test.datetimetester.TestSubclassDateTime('test_microsecond_rounding'),
            test.datetimetester.TestSubclassDateTime('test_replace'), # TODO
            test.datetimetester.TestSubclassDateTime('test_strftime_with_bad_tzname_replace'),
            test.datetimetester.TestTimeDelta('test_computations'), # rounding differences
            test.datetimetester.TestTimeTZ('test_zones'),
            test.datetimetester.TestTimeZone('test_constructor'),
        ]

        skip_tests = []
        if is_mono and is_arm64:
            skip_tests += [
                test.datetimetester.TestTimeDelta('test_overflow'), # rounding differences
            ]

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
