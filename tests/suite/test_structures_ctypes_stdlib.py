# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_structures from StdLib
##

from iptest import is_ironpython, generate_suite, run_test, is_osx

import ctypes.test.test_structures

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(ctypes.test.test_structures, pattern=pattern)

    if is_ironpython:
        failing_tests = [
            ctypes.test.test_structures.StructureTestCase('test_conflicting_initializers'), # AssertionError
            ctypes.test.test_structures.StructureTestCase('test_pass_by_value_in_register'), # NotImplementedError: in dll
        ]

        return generate_suite(tests, failing_tests)

    else:
        return tests

run_test(__name__)
