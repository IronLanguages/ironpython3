# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_strptime from StdLib
##

import unittest
import sys

from iptest import run_test

import test.test_strptime

def load_tests(loader, standard_tests, pattern):
    if sys.implementation.name == 'ironpython':
        suite = unittest.TestSuite()
        suite.addTest(test.test_strptime.CacheTests('test_TimeRE_recreation_locale'))
        suite.addTest(test.test_strptime.CacheTests('test_TimeRE_recreation_timezone'))
        suite.addTest(test.test_strptime.CacheTests('test_new_localetime'))
        suite.addTest(test.test_strptime.CacheTests('test_regex_cleanup'))
        suite.addTest(test.test_strptime.CacheTests('test_time_re_recreation'))
        suite.addTest(unittest.expectedFailure(test.test_strptime.CalculationTests('test_day_of_week_calculation'))) # https://github.com/IronLanguages/ironpython3/issues/1121
        suite.addTest(unittest.expectedFailure(test.test_strptime.CalculationTests('test_gregorian_calculation'))) # https://github.com/IronLanguages/ironpython3/issues/1121
        suite.addTest(unittest.expectedFailure(test.test_strptime.CalculationTests('test_julian_calculation'))) # https://github.com/IronLanguages/ironpython3/issues/1121
        suite.addTest(test.test_strptime.CalculationTests('test_week_0'))
        suite.addTest(test.test_strptime.CalculationTests('test_week_of_year_and_day_of_week_calculation'))
        suite.addTest(test.test_strptime.JulianTests('test_all_julian_days'))
        suite.addTest(test.test_strptime.LocaleTime_Tests('test_am_pm'))
        suite.addTest(test.test_strptime.LocaleTime_Tests('test_date_time'))
        suite.addTest(test.test_strptime.LocaleTime_Tests('test_lang'))
        suite.addTest(test.test_strptime.LocaleTime_Tests('test_month'))
        suite.addTest(test.test_strptime.LocaleTime_Tests('test_timezone'))
        suite.addTest(test.test_strptime.LocaleTime_Tests('test_weekday'))
        suite.addTest(test.test_strptime.Strptime12AMPMTests('test_twelve_noon_midnight'))
        suite.addTest(test.test_strptime.StrptimeTests('test_ValueError'))
        suite.addTest(test.test_strptime.StrptimeTests('test_bad_timezone'))
        suite.addTest(test.test_strptime.StrptimeTests('test_caseinsensitive'))
        suite.addTest(test.test_strptime.StrptimeTests('test_date'))
        suite.addTest(test.test_strptime.StrptimeTests('test_date_time'))
        suite.addTest(test.test_strptime.StrptimeTests('test_day'))
        suite.addTest(test.test_strptime.StrptimeTests('test_defaults'))
        suite.addTest(test.test_strptime.StrptimeTests('test_escaping'))
        suite.addTest(unittest.expectedFailure(test.test_strptime.StrptimeTests('test_feb29_on_leap_year_without_year')))
        suite.addTest(test.test_strptime.StrptimeTests('test_fraction'))
        suite.addTest(test.test_strptime.StrptimeTests('test_hour'))
        suite.addTest(test.test_strptime.StrptimeTests('test_julian'))
        suite.addTest(unittest.expectedFailure(test.test_strptime.StrptimeTests('test_mar1_comes_after_feb29_even_when_omitting_the_year')))
        suite.addTest(test.test_strptime.StrptimeTests('test_minute'))
        suite.addTest(test.test_strptime.StrptimeTests('test_month'))
        suite.addTest(test.test_strptime.StrptimeTests('test_percent'))
        suite.addTest(test.test_strptime.StrptimeTests('test_second'))
        suite.addTest(test.test_strptime.StrptimeTests('test_strptime_exception_context'))
        suite.addTest(test.test_strptime.StrptimeTests('test_time'))
        suite.addTest(unittest.expectedFailure(test.test_strptime.StrptimeTests('test_timezone'))) # https://github.com/IronLanguages/ironpython3/issues/1121
        suite.addTest(test.test_strptime.StrptimeTests('test_unconverteddata'))
        suite.addTest(test.test_strptime.StrptimeTests('test_weekday'))
        suite.addTest(test.test_strptime.StrptimeTests('test_year'))
        suite.addTest(test.test_strptime.TimeRETests('test_blankpattern'))
        suite.addTest(unittest.expectedFailure(test.test_strptime.TimeRETests('test_compile')))
        suite.addTest(test.test_strptime.TimeRETests('test_locale_data_w_regex_metacharacters'))
        suite.addTest(test.test_strptime.TimeRETests('test_matching_with_escapes'))
        suite.addTest(test.test_strptime.TimeRETests('test_pattern'))
        suite.addTest(test.test_strptime.TimeRETests('test_pattern_escaping'))
        suite.addTest(test.test_strptime.TimeRETests('test_whitespace_substitution'))
        suite.addTest(test.test_strptime.getlang_Tests('test_basic'))
        return suite

    else:
        return loader.loadTestsFromModule(test.test_strptime, pattern)

run_test(__name__)
