# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_datetime from StdLib
##

from iptest import is_ironpython, generate_suite, run_test

import test.datetimetester

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.datetimetester, pattern=pattern)

    if is_ironpython:
        failing_tests = [
            test.datetimetester.IranTest('test_folds'), # https://github.com/IronLanguages/ironpython3/issues/1459
            test.datetimetester.IranTest('test_gaps'), # https://github.com/IronLanguages/ironpython3/issues/1459
            test.datetimetester.IranTest('test_system_transitions'), # AttributeError: 'module' object has no attribute 'tzset'
            test.datetimetester.Oddballs('test_check_arg_types'), # TypeError: an integer is required
            test.datetimetester.TestDate('test_backdoor_resistance'),
            test.datetimetester.TestDate('test_compat_unpickle'), # TypeError: date() takes exactly 3 arguments (1 given)
            test.datetimetester.TestDate('test_insane_fromtimestamp'),
            test.datetimetester.TestDate('test_subclass_replace'), # TypeError: replace() got an unexpected keyword argument 'year'
            test.datetimetester.TestDateTime('test_backdoor_resistance'), # AssertionError: "^bad tzinfo state arg$" does not match "function takes at least 3 arguments (2 given)"
            test.datetimetester.TestDateTime('test_bad_constructor_arguments'), # https://github.com/IronLanguages/ironpython3/issues/1459
            test.datetimetester.TestDateTime('test_combine'), # TypeError: combine() takes exactly 2 arguments (3 given)
            test.datetimetester.TestDateTime('test_compat_unpickle'), # TypeError: function takes at least 3 arguments (1 given)
            test.datetimetester.TestDateTime('test_extreme_timedelta'),
            test.datetimetester.TestDateTime('test_insane_fromtimestamp'),
            test.datetimetester.TestDateTime('test_insane_utcfromtimestamp'),
            test.datetimetester.TestDateTime('test_isoformat'), # TypeError: isoformat() got an unexpected keyword argument 'timespec'
            test.datetimetester.TestDateTime('test_microsecond_rounding'),
            test.datetimetester.TestDateTime('test_strftime_with_bad_tzname_replace'),
            test.datetimetester.TestDateTime('test_subclass_replace'), # TypeError: replace() got an unexpected keyword argument 'year'
            test.datetimetester.TestDateTime('test_timestamp_limits'), # ValueError: The added or subtracted value results in an un-representable DateTime.
            test.datetimetester.TestDateTimeTZ('test_backdoor_resistance'),
            test.datetimetester.TestDateTimeTZ('test_bad_constructor_arguments'), # https://github.com/IronLanguages/ironpython3/issues/1459
            test.datetimetester.TestDateTimeTZ('test_compat_unpickle'), # TypeError: function takes at least 3 arguments (2 given)
            test.datetimetester.TestDateTimeTZ('test_even_more_compare'),
            test.datetimetester.TestDateTimeTZ('test_extreme_hashes'),
            test.datetimetester.TestDateTimeTZ('test_extreme_timedelta'),
            test.datetimetester.TestDateTimeTZ('test_insane_fromtimestamp'),
            test.datetimetester.TestDateTimeTZ('test_insane_utcfromtimestamp'),
            test.datetimetester.TestDateTimeTZ('test_isoformat'), # TypeError: isoformat() got an unexpected keyword argument 'timespec'
            test.datetimetester.TestDateTimeTZ('test_microsecond_rounding'),
            test.datetimetester.TestDateTimeTZ('test_mixed_compare'),
            test.datetimetester.TestDateTimeTZ('test_strftime_with_bad_tzname_replace'),
            test.datetimetester.TestDateTimeTZ('test_subclass_replace'), # TypeError: replace() got an unexpected keyword argument 'year'
            test.datetimetester.TestDateTimeTZ('test_timestamp_limits'), # ValueError: The added or subtracted value results in an un-representable DateTime.
            test.datetimetester.TestDateTimeTZ('test_tz_aware_arithmetic'),
            test.datetimetester.TestLocalTimeDisambiguation('test_comparison'), # https://github.com/IronLanguages/ironpython3/issues/1459
            test.datetimetester.TestLocalTimeDisambiguation('test_constructors'), # https://github.com/IronLanguages/ironpython3/issues/1459
            test.datetimetester.TestLocalTimeDisambiguation('test_dst'), # https://github.com/IronLanguages/ironpython3/issues/1459
            test.datetimetester.TestLocalTimeDisambiguation('test_fromtimestamp_low_fold_detection'), # https://github.com/IronLanguages/ironpython3/issues/1459
            test.datetimetester.TestLocalTimeDisambiguation('test_fromutc'), # https://github.com/IronLanguages/ironpython3/issues/1459
            test.datetimetester.TestLocalTimeDisambiguation('test_hash'), # https://github.com/IronLanguages/ironpython3/issues/1459
            test.datetimetester.TestLocalTimeDisambiguation('test_hash_aware'), # https://github.com/IronLanguages/ironpython3/issues/1459
            test.datetimetester.TestLocalTimeDisambiguation('test_member'), # https://github.com/IronLanguages/ironpython3/issues/1459
            test.datetimetester.TestLocalTimeDisambiguation('test_mixed_compare_fold'), # https://github.com/IronLanguages/ironpython3/issues/1459
            test.datetimetester.TestLocalTimeDisambiguation('test_mixed_compare_gap'), # https://github.com/IronLanguages/ironpython3/issues/1459
            test.datetimetester.TestLocalTimeDisambiguation('test_pickle_fold'), # https://github.com/IronLanguages/ironpython3/issues/1459
            test.datetimetester.TestLocalTimeDisambiguation('test_replace'), # https://github.com/IronLanguages/ironpython3/issues/1459
            test.datetimetester.TestLocalTimeDisambiguation('test_repr'), # https://github.com/IronLanguages/ironpython3/issues/1459
            test.datetimetester.TestLocalTimeDisambiguation('test_utcoffset'), # https://github.com/IronLanguages/ironpython3/issues/1459
            test.datetimetester.TestLocalTimeDisambiguation('test_vilnius_1941_fromutc'), # https://github.com/IronLanguages/ironpython3/issues/1459
            test.datetimetester.TestLocalTimeDisambiguation('test_vilnius_1941_toutc'), # AssertionError: '06/23/41 19:59:59 ' != 'Mon Jun 23 19:59:59 1941 UTC'
            test.datetimetester.TestModule('test_divide_and_round'), # AttributeError: 'module' object has no attribute '_divide_and_round'
            test.datetimetester.TestSubclassDateTime('test_backdoor_resistance'),
            test.datetimetester.TestSubclassDateTime('test_bad_constructor_arguments'), # https://github.com/IronLanguages/ironpython3/issues/1459
            test.datetimetester.TestSubclassDateTime('test_combine'), # TypeError: combine() takes exactly 2 arguments (3 given)
            test.datetimetester.TestSubclassDateTime('test_compat_unpickle'), # TypeError: function takes at least 3 arguments (1 given)
            test.datetimetester.TestSubclassDateTime('test_extreme_timedelta'),
            test.datetimetester.TestSubclassDateTime('test_insane_fromtimestamp'),
            test.datetimetester.TestSubclassDateTime('test_insane_utcfromtimestamp'),
            test.datetimetester.TestSubclassDateTime('test_isoformat'), # TypeError: isoformat() got an unexpected keyword argument 'timespec'
            test.datetimetester.TestSubclassDateTime('test_microsecond_rounding'),
            test.datetimetester.TestSubclassDateTime('test_replace'), # TODO
            test.datetimetester.TestSubclassDateTime('test_strftime_with_bad_tzname_replace'),
            test.datetimetester.TestSubclassDateTime('test_subclass_replace'), # TypeError: replace() got an unexpected keyword argument 'year'
            test.datetimetester.TestSubclassDateTime('test_timestamp_limits'), # ValueError: The added or subtracted value results in an un-representable DateTime.
            test.datetimetester.TestTime('test_backdoor_resistance'), # AssertionError: "^bad tzinfo state arg$" does not match "expected Int32, got bytes"
            test.datetimetester.TestTime('test_compat_unpickle'), # TypeError: expected Int32, got str
            test.datetimetester.TestTime('test_isoformat'), # TypeError: isoformat() takes no arguments (1 given)
            test.datetimetester.TestTime('test_subclass_replace'), # AssertionError: <class '_datetime.time'> is not <class 'test.datetimetester.TimeSubclass'>
            test.datetimetester.TestTimeDelta('test_computations'), # rounding differences
            test.datetimetester.TestTimeDelta('test_issue31293'), # ZeroDivisionError: Attempted to divide by zero.
            test.datetimetester.TestTimeTZ('test_backdoor_resistance'), # AssertionError: "^bad tzinfo state arg$" does not match "expected Int32, got bytes"
            test.datetimetester.TestTimeTZ('test_compat_unpickle'), # TypeError: expected Int32, got str
            test.datetimetester.TestTimeTZ('test_isoformat'), # TypeError: isoformat() takes no arguments (1 given)
            test.datetimetester.TestTimeTZ('test_subclass_replace'), # AssertionError: <class '_datetime.time'> is not <class 'test.datetimetester.TimeSubclass'>
            test.datetimetester.TestTimeTZ('test_zones'),
            test.datetimetester.TestTimeZone('test_constructor'),
            test.datetimetester.ZoneInfoTest('test_folds'), # https://github.com/IronLanguages/ironpython3/issues/1459
            test.datetimetester.ZoneInfoTest('test_gaps'), # https://github.com/IronLanguages/ironpython3/issues/1459
            test.datetimetester.ZoneInfoTest('test_system_transitions'), # AttributeError: 'module' object has no attribute 'tzset'
        ]

        return generate_suite(tests, failing_tests)

    else:
        return tests

run_test(__name__)
