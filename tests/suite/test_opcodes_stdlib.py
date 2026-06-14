# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_opcodes from StdLib
##

import sys

from iptest import is_ironpython, generate_suite, run_test

import test.test_opcodes

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_opcodes, pattern=pattern)

    if is_ironpython:
        failing_tests = []
        if sys.version_info >= (3, 6):
            failing_tests += [
                test.test_opcodes.OpcodeTest('test_do_not_recreate_annotations'), # https://github.com/IronLanguages/ironpython3/issues/106
                test.test_opcodes.OpcodeTest('test_setup_annotations_line'), # https://github.com/IronLanguages/ironpython3/issues/106
                test.test_opcodes.OpcodeTest('test_use_existing_annotations'), # https://github.com/IronLanguages/ironpython3/issues/106
            ]

        skip_tests = []

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
