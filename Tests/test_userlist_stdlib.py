# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_userlist from StdLib
##

from iptest import is_ironpython, generate_suite, run_test

import test.test_userlist

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_userlist, pattern=pattern)

    if is_ironpython:
        failing_tests = [
            test.test_userlist.UserListTest('test_free_after_iterating'), # https://github.com/IronLanguages/ironpython3/issues/1246
        ]

        return generate_suite(tests, failing_tests)

    else:
        return tests

run_test(__name__)
