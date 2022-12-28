# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_httplib from StdLib
##

from iptest import is_ironpython, generate_suite, run_test, is_mono, is_osx

import test.test_httplib

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_httplib)

    if is_ironpython:
        failing_tests = [
            test.test_httplib.HTTPSTest('test_networked'), # AttributeError: 'SSLError' object has no attribute 'reason'
            test.test_httplib.HTTPSTest('test_networked_bad_cert'), # AttributeError: 'SSLError' object has no attribute 'reason'
            test.test_httplib.HeaderTests('test_putheader'), # https://github.com/IronLanguages/ironpython3/issues/1100
        ]
        if is_mono and is_osx:
            failing_tests += [
                test.test_httplib.HTTPSTest('test_networked_good_cert'), # https://github.com/IronLanguages/ironpython3/issues/1523
            ]

        skip_tests = [
            test.test_httplib.HTTPSTest('test_local_bad_hostname'), # StackOverflowException
            test.test_httplib.HTTPSTest('test_local_good_hostname'), # StackOverflowException
            test.test_httplib.HTTPSTest('test_local_unknown_cert'), # StackOverflowException
        ]
        if is_mono:
            skip_tests += [
                test.test_functools.TestPartialPy('test_weakref'),
            ]

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
