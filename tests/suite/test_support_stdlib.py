# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_support from StdLib
##

from iptest import is_ironpython, generate_suite, run_test, is_osx

import test.test_support

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_support, pattern=pattern)

    if is_ironpython:
        failing_tests = [
            test.test_support.TestSupport('test_args_from_interpreter_flags'), # https://github.com/IronLanguages/ironpython3/issues/1541
            test.test_support.TestSupport('test_optim_args_from_interpreter_flags'), # https://github.com/IronLanguages/ironpython3/issues/1541
        ]
        if is_osx:
            failing_tests += [
                test.test_support.TestSupport('test_change_cwd'), # https://github.com/IronLanguages/ironpython3/issues/1543
            ]

        return generate_suite(tests, failing_tests)

    else:
        return tests

run_test(__name__)
