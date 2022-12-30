# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_memoryio from StdLib
##

from iptest import is_ironpython, generate_suite, run_test

import test.test_memoryio

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_memoryio, pattern=pattern)

    if is_ironpython:
        failing_tests = [
            test.test_memoryio.CBytesIOTest('test_getbuffer'), # https://github.com/IronLanguages/ironpython3/issues/1002
            test.test_memoryio.CBytesIOTest('test_pickling'), # https://github.com/IronLanguages/ironpython3/issues/1003
            test.test_memoryio.CStringIOTest('test_pickling'), # https://github.com/IronLanguages/ironpython3/issues/1003
        ]

        skip_tests = [
            test.test_memoryio.CBytesIOTest('test_instance_dict_leak'), # https://github.com/IronLanguages/ironpython3/issues/1004
            test.test_memoryio.CStringIOTest('test_instance_dict_leak'), # https://github.com/IronLanguages/ironpython3/issues/1004
        ]

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
