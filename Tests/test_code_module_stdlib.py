# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_code_module from StdLib
##

from iptest import is_ironpython, generate_suite, run_test

import test.test_code_module

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_code_module, pattern=pattern)

    if is_ironpython:
        failing_tests = [
            test.test_code_module.TestInteractiveConsole('test_context_tb'), # https://github.com/IronLanguages/ironpython3/issues/1557
        ]

        return generate_suite(tests, failing_tests)

    else:
        return tests

run_test(__name__)
