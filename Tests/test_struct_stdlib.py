# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_struct from StdLib
##

from iptest import is_ironpython, is_64, generate_suite, run_test

import test.test_struct

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_struct)

    if is_ironpython:
        failing_tests = [
            test.test_struct.StructTest('test_705836'), # AssertionError: OverflowError not raised by pack
            test.test_struct.StructTest('test_bool'), # struct.error: expected bool value got IronPython.NewTypes.System.Object_1$1
            test.test_struct.StructTest('test_count_overflow'), # AssertionError: error not raised by calcsize
        ]

        if is_64:
            failing_tests += [
                test.test_struct.StructTest('test_calcsize'), # AssertionError: 4 not greater than or equal to 8 - https://github.com/IronLanguages/ironpython3/pull/869
            ]

        return generate_suite(tests, failing_tests)

    else:
        return tests

run_test(__name__)
