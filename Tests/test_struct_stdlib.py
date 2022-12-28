# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_struct from StdLib
##

from iptest import is_ironpython, generate_suite, run_test

import test.test_struct

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_struct)

    if is_ironpython:
        failing_tests = [
            test.test_struct.StructTest('test_705836'), # TODO: figure out
            test.test_struct.StructTest('test_bool'), # TODO: figure out
            test.test_struct.StructTest('test_calcsize'), # TODO: figure out
            test.test_struct.StructTest('test_count_overflow'), # TODO: figure out
            test.test_struct.StructTest('test_trailing_counter'), # TODO: figure out
            test.test_struct.UnpackIteratorTest('test_construct'), # TODO: figure out
        ]

        return generate_suite(tests, failing_tests)

    else:
        return tests

run_test(__name__)
