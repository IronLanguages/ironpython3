# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_bitfields from StdLib
##

from iptest import is_ironpython, generate_suite, run_test, is_windows

import ctypes.test.test_bitfields

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(ctypes.test.test_bitfields)

    if is_ironpython:
        failing_tests = []
        if not is_windows:
            failing_tests += [
                ctypes.test.test_bitfields.C_Test('test_shorts'),
            ]

        return generate_suite(tests, failing_tests)

    else:
        return tests

run_test(__name__)
