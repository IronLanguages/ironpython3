# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_base64 from StdLib
##

from iptest import is_ironpython, generate_suite, run_test, is_posix

import test.test_base64

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_base64, pattern=pattern)

    if is_ironpython:
        failing_tests = []
        if not is_posix:
            failing_tests += [
                test.test_base64.TestMain('test_encode_from_stdin'), # https://github.com/IronLanguages/ironpython3/issues/1135
            ]

        return generate_suite(tests, failing_tests)

    else:
        return tests

run_test(__name__)
