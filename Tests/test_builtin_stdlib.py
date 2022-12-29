# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_builtin from StdLib
##

from iptest import is_ironpython, generate_suite, run_test, is_netcoreapp

import test.test_builtin

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_builtin, pattern=pattern)

    if is_ironpython:
        failing_tests = [
            test.test_builtin.BuiltinTest('test_compile'),
            test.test_builtin.BuiltinTest('test_dir'),
            test.test_builtin.BuiltinTest('test_exec_globals'),
            test.test_builtin.BuiltinTest('test_input'),
            test.test_builtin.BuiltinTest('test_len'),
            test.test_builtin.BuiltinTest('test_open_non_inheritable'), # https://github.com/IronLanguages/ironpython3/issues/1225
            test.test_builtin.PtyTests('test_input_no_stdout_fileno'),
            test.test_builtin.PtyTests('test_input_tty'),
            test.test_builtin.PtyTests('test_input_tty_non_ascii'),
            test.test_builtin.PtyTests('test_input_tty_non_ascii_unicode_errors'),
            test.test_builtin.TestSorted('test_bad_arguments'), # AssertionError: TypeError not raised
            test.test_builtin.TestType('test_bad_args'), # AssertionError: TypeError not raised
            test.test_builtin.TestType('test_bad_slots'), # AssertionError: TypeError not raised
            test.test_builtin.TestType('test_namespace_order'), # https://github.com/IronLanguages/ironpython3/issues/1468
            test.test_builtin.TestType('test_new_type'), # AssertionError: <class 'test.test_builtin.B'> is not <class 'int'>
            test.test_builtin.TestType('test_type_doc'), # AssertionError: UnicodeEncodeError not raised
            test.test_builtin.TestType('test_type_name'), # AssertionError: ValueError not raised
            test.test_builtin.TestType('test_type_nokwargs'), # AssertionError: TypeError not raised
            test.test_builtin.TestType('test_type_qualname'), # https://github.com/IronLanguages/ironpython3/issues/30
        ]

        skip_tests = []
        if is_netcoreapp:
            skip_tests += [
                test.test_builtin.ShutdownTest('test_cleanup'),
            ]

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
