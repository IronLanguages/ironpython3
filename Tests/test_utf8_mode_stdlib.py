# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_utf8_mode from StdLib
##

from iptest import is_ironpython, generate_suite, run_test, is_netcoreapp21, is_windows

import test.test_utf8_mode

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_utf8_mode)

    if is_ironpython:
        failing_tests = [
            test.test_utf8_mode.UTF8ModeTests('test_env_var'), # https://github.com/IronLanguages/ironpython3/issues/1694
            test.test_utf8_mode.UTF8ModeTests('test_stdio'), # https://github.com/IronLanguages/ironpython3/issues/1694
        ]

        if is_netcoreapp21 and is_windows:
            # these are failing because the ipy.bat wrapper is not handling arguments properly
            failing_tests += [
                test.test_utf8_mode.UTF8ModeTests('test_filesystemencoding'),
                test.test_utf8_mode.UTF8ModeTests('test_io'),
                test.test_utf8_mode.UTF8ModeTests('test_io_encoding'),
                test.test_utf8_mode.UTF8ModeTests('test_pyio_encoding'),
            ]

        # posix locales
        skip_tests = [
            test.test_utf8_mode.UTF8ModeTests('test_posix_locale'),
            test.test_utf8_mode.UTF8ModeTests('test_cmd_line'),
        ]

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
