# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_datetime from StdLib
##

from iptest import is_ironpython, generate_suite, run_test

import test.datetimetester

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.datetimetester)

    if is_ironpython:
        failing_tests = [
            test.datetimetester.Oddballs('test_bug_1028306'),
            test.datetimetester.TestDate('test_backdoor_resistance'),
            test.datetimetester.TestDate('test_insane_fromtimestamp'),
            test.datetimetester.TestDate('test_mixed_compare'),
            test.datetimetester.TestDateTime('test_astimezone'), # https://github.com/IronLanguages/ironpython3/issues/1136
            test.datetimetester.TestDateTime('test_backdoor_resistance'),
            test.datetimetester.TestDateTime('test_extreme_timedelta'),
            test.datetimetester.TestDateTime('test_insane_fromtimestamp'),
            test.datetimetester.TestDateTime('test_insane_utcfromtimestamp'),
            test.datetimetester.TestDateTime('test_microsecond_rounding'),
            test.datetimetester.TestDateTime('test_mixed_compare'),
            test.datetimetester.TestDateTime('test_overflow'),
            test.datetimetester.TestDateTime('test_strftime_with_bad_tzname_replace'),
            test.datetimetester.TestDateTime('test_timestamp_aware'), # AttributeError: 'datetime' object has no attribute 'timestamp'
            test.datetimetester.TestDateTimeTZ('test_astimezone'), # https://github.com/IronLanguages/ironpython3/issues/1136
            test.datetimetester.TestDateTimeTZ('test_backdoor_resistance'),
            test.datetimetester.TestDateTimeTZ('test_even_more_compare'),
            test.datetimetester.TestDateTimeTZ('test_extreme_hashes'),
            test.datetimetester.TestDateTimeTZ('test_extreme_timedelta'),
            test.datetimetester.TestDateTimeTZ('test_insane_fromtimestamp'),
            test.datetimetester.TestDateTimeTZ('test_insane_utcfromtimestamp'),
            test.datetimetester.TestDateTimeTZ('test_microsecond_rounding'),
            test.datetimetester.TestDateTimeTZ('test_mixed_compare'),
            test.datetimetester.TestDateTimeTZ('test_overflow'),
            test.datetimetester.TestDateTimeTZ('test_strftime_with_bad_tzname_replace'),
            test.datetimetester.TestDateTimeTZ('test_timestamp_aware'), # AttributeError: 'datetime' object has no attribute 'timestamp'
            test.datetimetester.TestDateTimeTZ('test_tz_aware_arithmetic'),
            test.datetimetester.TestDateTimeTZ('test_utctimetuple'), # SystemError: Object reference not set to an instance of an object.
            test.datetimetester.TestSubclassDateTime('test_astimezone'), # https://github.com/IronLanguages/ironpython3/issues/1136
            test.datetimetester.TestSubclassDateTime('test_backdoor_resistance'),
            test.datetimetester.TestSubclassDateTime('test_extreme_timedelta'),
            test.datetimetester.TestSubclassDateTime('test_insane_fromtimestamp'),
            test.datetimetester.TestSubclassDateTime('test_insane_utcfromtimestamp'),
            test.datetimetester.TestSubclassDateTime('test_microsecond_rounding'),
            test.datetimetester.TestSubclassDateTime('test_mixed_compare'),
            test.datetimetester.TestSubclassDateTime('test_overflow'),
            test.datetimetester.TestSubclassDateTime('test_replace'), # TODO
            test.datetimetester.TestSubclassDateTime('test_strftime_with_bad_tzname_replace'),
            test.datetimetester.TestSubclassDateTime('test_strptime'),
            test.datetimetester.TestSubclassDateTime('test_timestamp_aware'),
            test.datetimetester.TestSubclassDateTime('test_tz_independent_comparing'),
            test.datetimetester.TestTimeDelta('test_computations'), # rounding differences
            test.datetimetester.TestTimeTZ('test_zones'),
            test.datetimetester.TestTimeZone('test_constructor'),
        ]

        return generate_suite(tests, failing_tests)

    else:
        return tests

run_test(__name__)
