# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_genericpath from StdLib
##

from iptest import is_ironpython, generate_suite, run_test, is_exfat, is_netcoreapp, is_windows

import test.test_genericpath

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_genericpath, pattern=pattern)

    if is_ironpython:
        failing_tests = []
        if is_windows and is_netcoreapp: # os.symlink is not implemented on .NET Framework
            failing_tests += [
                test.test_genericpath.TestGenericTest('test_samefile_on_symlink'), # AssertionError: os.stat does not follow symlinks
                test.test_genericpath.TestGenericTest('test_samestat_on_symlink'), # AssertionError: os.stat does not follow symlinks
            ]

        skip_tests = []
        if is_windows and is_exfat():
            skip_tests += [
                test.test_genericpath.TestGenericTest('test_samefile_on_link'),
                test.test_genericpath.TestGenericTest('test_samestat_on_link'),
            ]

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
