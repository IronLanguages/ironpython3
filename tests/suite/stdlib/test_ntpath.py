# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_ntpath from StdLib
##

from iptest import is_ironpython, generate_suite, run_test, is_exfat, is_windows

import test.test_ntpath

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_ntpath, pattern=pattern)

    if is_ironpython:
        failing_tests = []

        skip_tests = []
        if is_windows and is_exfat():
            skip_tests += [
                test.test_ntpath.NtCommonTest('test_samefile_on_link'),
                test.test_ntpath.NtCommonTest('test_samestat_on_link'),
            ]

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
