# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_builtin from StdLib
##

from iptest import is_ironpython, generate_suite, run_test, is_netcoreapp

import test.test_builtin

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_builtin)

    if is_ironpython:
        failing_tests = [
            test.test_builtin.BuiltinTest('test_compile'),
            test.test_builtin.BuiltinTest('test_dir'),
            test.test_builtin.BuiltinTest('test_exec_globals'),
            test.test_builtin.BuiltinTest('test_input'),
            test.test_builtin.BuiltinTest('test_len'),
            test.test_builtin.BuiltinTest('test_open_non_inheritable'), # https://github.com/IronLanguages/ironpython3/issues/1225
        ]

        skip_tests = [
            # module `pty` is importable but not functional on .NET Core
            test.test_builtin.PtyTests('test_input_no_stdout_fileno'),
            test.test_builtin.PtyTests('test_input_tty'),
            test.test_builtin.PtyTests('test_input_tty_non_ascii'),
            test.test_builtin.PtyTests('test_input_tty_non_ascii_unicode_errors'),
            test.test_builtin.ShutdownTest('test_cleanup'),
        ]
        if is_netcoreapp:
            skip_tests += [
            ]

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
