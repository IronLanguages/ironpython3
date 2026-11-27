# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the Apache 2.0 License.
# See the LICENSE file in the project root for more information.

##
## Run selected tests from test_pdb from StdLib
##

import doctest
import sys

from iptest import is_ironpython, is_posix, generate_suite, run_test

import test.test_pdb

def load_tests(loader, standard_tests, pattern):
    tests = loader.loadTestsFromModule(test.test_pdb, pattern=pattern)

    if is_ironpython:
        # doctests can't be created by name like other tests, so they are taken from the loaded tests
        doctests = {t.id(): t for suite in tests for t in suite if isinstance(t, doctest.DocTestCase)}

        failing_tests = [doctests['test.test_pdb.' + name] for name in (
            # the output of pdb differs from CPython's
            'test_list_commands',
            'test_next_until_return_at_return_event',
            'test_pdb_basic_commands',
            'test_pdb_breakpoint_commands',
            'test_pdb_continue_in_bottomframe',
            'test_pdb_displayhook',
            'test_pdb_next_command_for_generator',
            'test_pdb_next_command_in_generator_for_loop',
            'test_pdb_next_command_subiterator',
            'test_pdb_return_command_for_generator',
            'test_pdb_run_with_code_object',
            'test_pdb_run_with_incorrect_argument',
            'test_pdb_skip_modules',
            'test_pdb_skip_modules_with_callback',
            'test_pdb_until_command_for_generator',
            'test_post_mortem',
        )]
        if sys.version_info >= (3, 6):
            failing_tests += [doctests['test.test_pdb.' + name] for name in (
                # the second set_trace stops on the line after it (in CI, an order-dependent "Value cannot be null. Parameter name: del")
                'test_pdb_issue_20766',
                # asyncio.new_event_loop() fails: socket.fileno() returns Int64, which selectors rejects
                'test_pdb_next_command_for_asyncgen',
                'test_pdb_next_command_for_coroutine',
                'test_pdb_return_command_for_coroutine',
                'test_pdb_until_command_for_coroutine',
            )]

        if is_posix:
            failing_tests += [
                test.test_pdb.PdbTestCase('test_issue16180'),  # the subprocess's stderr is lost: Popen on .NET's Process doesn't implement stderr=STDOUT
            ]

        skip_tests = []

        return generate_suite(tests, failing_tests, skip_tests)

    else:
        return tests

run_test(__name__)
