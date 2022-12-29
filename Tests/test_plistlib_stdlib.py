# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_plistlib from StdLib
##

from iptest import is_ironpython, generate_suite, run_test

import test.test_plistlib

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_plistlib, pattern=pattern)

    if is_ironpython:
        failing_tests = [
            test.test_plistlib.TestPlistlib('test_xml_plist_with_entity_decl'), # https://github.com/IronLanguages/ironpython2/issues/464
        ]

        skip_tests = [
            test.test_plistlib.TestBinaryPlistlib('test_deep_nesting'), # StackOverflowException
        ]

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
