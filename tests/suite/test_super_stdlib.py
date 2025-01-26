# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_super from StdLib
##

from iptest import is_ironpython, generate_suite, run_test

import test.test_super

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_super, pattern=pattern)

    if is_ironpython:
        failing_tests = [
            test.test_super.TestSuper('test___class___mro') # NotImplementedError: Overriding type.mro is not implemented
        ]

        return generate_suite(tests, failing_tests)

    else:
        return tests

run_test(__name__)
