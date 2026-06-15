# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_regrtest from StdLib
##

import sys

from iptest import is_ironpython, generate_suite, run_test

import test.test_regrtest

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_regrtest, pattern=pattern)

    if is_ironpython:
        failing_tests = []

        skip_tests = []
        if sys.version_info >= (3, 6):
            skip_tests += [
                # TODO: these all fail (and are slow)
                test.test_regrtest.ArgsTestCase('test_coverage'),
                test.test_regrtest.ArgsTestCase('test_crashed'),
                test.test_regrtest.ArgsTestCase('test_env_changed'),
                test.test_regrtest.ArgsTestCase('test_failing_test'),
                test.test_regrtest.ArgsTestCase('test_forever'),
                test.test_regrtest.ArgsTestCase('test_fromfile'),
                test.test_regrtest.ArgsTestCase('test_huntrleaks'),
                test.test_regrtest.ArgsTestCase('test_huntrleaks_fd_leak'),
                test.test_regrtest.ArgsTestCase('test_interrupted'),
                test.test_regrtest.ArgsTestCase('test_list_cases'),
                test.test_regrtest.ArgsTestCase('test_list_tests'),
                test.test_regrtest.ArgsTestCase('test_matchfile'),
                test.test_regrtest.ArgsTestCase('test_no_test_ran_some_test_exist_some_not'),
                test.test_regrtest.ArgsTestCase('test_no_tests_ran'),
                test.test_regrtest.ArgsTestCase('test_no_tests_ran_multiple_tests_nonexistent'),
                test.test_regrtest.ArgsTestCase('test_random'),
                test.test_regrtest.ArgsTestCase('test_rerun_fail'),
                test.test_regrtest.ArgsTestCase('test_resources'),
                test.test_regrtest.ArgsTestCase('test_slow_interrupted'),
                test.test_regrtest.ArgsTestCase('test_slowest'),
                test.test_regrtest.ArgsTestCase('test_wait'),
                test.test_regrtest.ProgramsTestCase('test_module_autotest'),
                test.test_regrtest.ProgramsTestCase('test_module_from_test_autotest'),
                test.test_regrtest.ProgramsTestCase('test_module_regrtest'),
                test.test_regrtest.ProgramsTestCase('test_module_test'),
                test.test_regrtest.ProgramsTestCase('test_pcbuild_rt'),
                test.test_regrtest.ProgramsTestCase('test_script_autotest'),
                test.test_regrtest.ProgramsTestCase('test_script_regrtest'),
                test.test_regrtest.ProgramsTestCase('test_tools_buildbot_test'),
                test.test_regrtest.ProgramsTestCase('test_tools_script_run_tests'),
            ]

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
